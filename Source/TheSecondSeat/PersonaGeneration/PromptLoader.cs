using System;
using System.Collections.Generic;
using System.IO;
using RimWorld;
using Verse;

namespace TheSecondSeat.PersonaGeneration
{
    /// <summary>
    /// Utility class to load prompt templates from language-specific folders.
    /// Supports modular prompt design and localization.
    /// ⭐ v1.7.0: 添加内存缓存，减少同步 IO 造成的卡顿
    /// </summary>
    public static class PromptLoader
    {
        private const string PromptsFolderName = "Prompts";
        private const string DefaultLanguage = "English";
        
        // ⭐ v1.7.0: 静态缓存
        private static Dictionary<string, string> _promptCache = new Dictionary<string, string>();

        /// <summary>
        /// Clears the prompt cache (e.g. when language changes)
        /// </summary>
        public static void ClearCache()
        {
            _promptCache.Clear();
            Log.Message("[The Second Seat] Prompt cache cleared.");
        }

        /// <summary>
        /// Loads a prompt template by name.
        /// Priority:
        /// 1. Config/TheSecondSeat/Prompts/{Language}/{file} (User Override - Specific Language)
        /// 2. Config/TheSecondSeat/Prompts/{file} (User Override - Global)
        /// 3. Mod/Languages/{Language}/Prompts/{file} (Mod Default)
        /// 4. Mod/Languages/English/Prompts/{file} (Mod Fallback)
        /// </summary>
        /// <param name="promptName">The name of the prompt file (without extension).</param>
        /// <param name="silent">If true, don't log warning when file is not found. Default is false.</param>
        /// <returns>The content of the prompt file.</returns>
        public static string Load(string promptName, bool silent = false)
        {
            // ⭐ v1.7.0: 检查缓存
            string activeLangFolder = LanguageDatabase.activeLanguage.folderName;
            string cacheKey = $"{activeLangFolder}_{promptName}";

            // 开发模式下禁用缓存，方便热重载
            if (!Prefs.DevMode && _promptCache.TryGetValue(cacheKey, out string cachedContent))
            {
                return cachedContent;
            }

            string fileName = promptName + ".txt";
            
            // --- User Overrides (Config Folder) ---
            string configRoot = GenFilePaths.ConfigFolderPath;
            
            // 1. User Override - Specific Language
            string userLangPath = Path.Combine(configRoot, "TheSecondSeat", PromptsFolderName, activeLangFolder, fileName);
            if (File.Exists(userLangPath))
            {
                if (Prefs.DevMode) Log.Message($"[The Second Seat] Loading prompt from User Override (Lang): {userLangPath}");
                return CacheAndReturn(cacheKey, File.ReadAllText(userLangPath));
            }

            // 2. User Override - Global (Root of Prompts folder in Config)
            string userGlobalPath = Path.Combine(configRoot, "TheSecondSeat", PromptsFolderName, fileName);
            if (File.Exists(userGlobalPath))
            {
                if (Prefs.DevMode) Log.Message($"[The Second Seat] Loading prompt from User Override (Global): {userGlobalPath}");
                return CacheAndReturn(cacheKey, File.ReadAllText(userGlobalPath));
            }

            // --- Mod Defaults ---
            var modContent = LoadedModManager.GetMod<TheSecondSeat.Settings.TheSecondSeatMod>()?.Content;
            if (modContent != null)
            {
                // 3. Mod Default - Active Language
                string modPromptsDir = GetModPromptsPath(modContent, activeLangFolder);
                if (modPromptsDir != null)
                {
                    string activeLangPath = Path.Combine(modPromptsDir, fileName);
                    if (File.Exists(activeLangPath))
                    {
                        if (Prefs.DevMode) Log.Message($"[The Second Seat] Loading prompt from Mod Default: {activeLangPath}");
                        return CacheAndReturn(cacheKey, File.ReadAllText(activeLangPath));
                    }
                }

                // 4. Mod Fallback - English (Default)
                if (activeLangFolder != DefaultLanguage)
                {
                    string defaultPromptsDir = GetModPromptsPath(modContent, DefaultLanguage);
                    if (defaultPromptsDir != null)
                    {
                        string defaultPath = Path.Combine(defaultPromptsDir, fileName);
                        if (File.Exists(defaultPath))
                        {
                            return CacheAndReturn(cacheKey, File.ReadAllText(defaultPath));
                        }
                    }
                }
            }

            // ⭐ v1.9.4: 支持静默模式，避免对动态标签文件输出警告
            if (!silent)
            {
                Log.Warning($"[The Second Seat] Prompt file not found: {promptName}.txt");
            }
            string errorContent = $"[Error: Prompt {promptName} not found]";
            
            // 即使失败也缓存错误信息，避免重复尝试读取不存在的文件
            _promptCache[cacheKey] = errorContent;
            return errorContent;
        }

        // ⭐ v1.7.0: 将结果存入缓存
        private static string CacheAndReturn(string key, string content)
        {
            _promptCache[key] = content;
            return content;
        }

        /// <summary>
        /// Helper to find the prompts directory for a specific language in the mod content.
        /// Performs case-insensitive search to be robust.
        /// </summary>
        private static string GetModPromptsPath(ModContentPack modContent, string langFolder)
        {
            if (modContent == null) return null;

            string languagesDir = Path.Combine(modContent.RootDir, "Languages");
            string specificPath = Path.Combine(languagesDir, langFolder, PromptsFolderName);

            if (Directory.Exists(specificPath)) return specificPath;

            // Case-insensitive search
            if (Directory.Exists(languagesDir))
            {
                foreach (string dir in Directory.GetDirectories(languagesDir))
                {
                    if (Path.GetFileName(dir).Equals(langFolder, StringComparison.OrdinalIgnoreCase))
                    {
                        string p = Path.Combine(dir, PromptsFolderName);
                        if (Directory.Exists(p)) return p;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Ensures the user override directory exists and optionally dumps default prompts there.
        /// </summary>
        public static void EnsureConfigDirectory()
        {
            string configPromptsPath = Path.Combine(GenFilePaths.ConfigFolderPath, "TheSecondSeat", PromptsFolderName);
            if (!Directory.Exists(configPromptsPath))
            {
                Directory.CreateDirectory(configPromptsPath);
            }
            
            // Ensure language subfolders exist for current language
            string langPath = Path.Combine(configPromptsPath, LanguageDatabase.activeLanguage.folderName);
            if (!Directory.Exists(langPath))
            {
                Directory.CreateDirectory(langPath);
            }
        }

        /// <summary>
        /// Creates a README file explaining the prompt files.
        /// </summary>
        public static void CreateReadme()
        {
            EnsureConfigDirectory();
            string configPromptsPath = Path.Combine(GenFilePaths.ConfigFolderPath, "TheSecondSeat", PromptsFolderName);
            string readmePath = Path.Combine(configPromptsPath, "README.txt");

            string content = @"
=== The Second Seat Custom Prompts Guide ===

To customize the AI's system prompts, create .txt files with the same names as the internal prompt files in this folder.
These files will override the mod's default prompts.

You can place files directly in this folder (Global Override) or in language-specific subfolders (e.g., 'ChineseSimplified', 'English').
Language-specific overrides take precedence over global overrides.

=== File List & Descriptions ===

-- Core (高维TTRPG框架) --
Identity_Core.txt           : Defines the meta-identity (Higher-dimensional TTRPG framework, GMPC protocol)
SystemPrompt_Master.txt     : Master system prompt integrating all components
Language_Instruction.txt    : Critical instructions about which language to speak.

-- Behavior Rules (双频道意识) --
BehaviorRules_Assistant.txt : Rules for Assistant Mode (Meta Channel & IC Channel)
BehaviorRules_Opponent.txt  : Rules for Opponent Mode (Challenging, controlling)
BehaviorRules_Engineer.txt  : Rules for Engineer Mode (Technical, diagnostic)
BehaviorRules_Universal.txt : Universal rules (真实契约 Reality Pact)

-- Output Format --
OutputFormat_Structure.txt  : Defines the structure of the JSON/Text response
OutputFormat_Fields.txt     : Explains specific fields in the output
OutputFormat_Examples.txt   : Examples of correct output

-- Romantic (Affinity System) --
Romantic_Intro.txt          : Instructions for romantic interactions
Romantic_Soulmate.txt       : Behavior when affinity is very high (90+)
Romantic_Partner.txt        : Behavior when affinity is high (60-89)
Romantic_Yandere.txt        : Special behavior for Yandere trait
Romantic_Tsundere.txt       : Special behavior for Tsundere trait

-- Diagnostics --
LogDiagnosis.txt            : Instructions for analyzing game logs

";
            File.WriteAllText(readmePath, content);
        }
        
        /// <summary>
        /// Opens the config folder in the OS file explorer.
        /// </summary>
        public static void OpenConfigFolder()
        {
            EnsureConfigDirectory();
            string path = Path.Combine(GenFilePaths.ConfigFolderPath, "TheSecondSeat", PromptsFolderName);
            
            // ⭐ 修复：使用 Application.OpenURL 处理路径，避免 Windows 上因空格导致的路径错误
            try 
            {
                UnityEngine.Application.OpenURL(path);
            }
            catch
            {
                // 回退方案
                if (System.Environment.OSVersion.Platform == System.PlatformID.Win32NT)
                {
                    System.Diagnostics.Process.Start("explorer.exe", $"\"{path}\"");
                }
            }
        }

        /// <summary>
        /// Initializes user prompts by copying default prompts to the config folder.
        /// </summary>
        public static void InitializeUserPrompts()
        {
            EnsureConfigDirectory();
            CreateReadme();
            
            string configPromptsPath = Path.Combine(GenFilePaths.ConfigFolderPath, "TheSecondSeat", PromptsFolderName);
            string activeLangFolder = LanguageDatabase.activeLanguage.folderName;
            string targetFolder = Path.Combine(configPromptsPath, activeLangFolder);
            
            var modContent = LoadedModManager.GetMod<TheSecondSeat.Settings.TheSecondSeatMod>()?.Content;
            if (modContent == null) return;
            
            // Source path: Mod/Languages/{Language}/Prompts/
            Log.Message($"[The Second Seat] Initializing User Prompts. Active Language: {activeLangFolder}");

            string sourcePath = GetModPromptsPath(modContent, activeLangFolder);

            // Special handling for Chinese: if active language is Chinese-variant but exact folder not found, try standard "ChineseSimplified"
            if (sourcePath == null && (activeLangFolder.Contains("Chinese") || activeLangFolder.Contains("CN")))
            {
                sourcePath = GetModPromptsPath(modContent, "ChineseSimplified");
                if (sourcePath != null)
                {
                    Log.Message($"[The Second Seat] Exact match for '{activeLangFolder}' not found, falling back to 'ChineseSimplified'.");
                }
            }
            
            // Fallback source: Mod/Languages/English/Prompts/
            if (sourcePath == null && activeLangFolder != DefaultLanguage)
            {
                Log.Warning($"[The Second Seat] Prompts for language '{activeLangFolder}' not found. Falling back to English.");
                sourcePath = GetModPromptsPath(modContent, DefaultLanguage);
            }
            
            if (sourcePath != null && Directory.Exists(sourcePath))
            {
                string[] files = Directory.GetFiles(sourcePath, "*.txt");
                foreach (string file in files)
                {
                    string fileName = Path.GetFileName(file);
                    string destFile = Path.Combine(targetFolder, fileName);
                    
                    // Always overwrite to ensure correct language version
                    // ⭐ v2.2.1: 强制删除旧文件以避免覆盖失败
                    if (File.Exists(destFile)) File.Delete(destFile);
                    File.Copy(file, destFile);
                }
                Log.Message($"[The Second Seat] Initialized {files.Length} prompt files to {targetFolder}");
                Messages.Message("提示词初始化完成", MessageTypeDefOf.PositiveEvent, false);
            }
            else
            {
                Log.Warning($"[The Second Seat] Could not find source prompts for language {activeLangFolder} or fallback.");
                Messages.Message("未找到源提示词文件", MessageTypeDefOf.RejectInput, false);
            }
            
            // Clear cache to ensure new files are used
            ClearCache();
        }
    }
}
