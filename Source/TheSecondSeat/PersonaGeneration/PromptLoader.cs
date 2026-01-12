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
        /// <returns>The content of the prompt file.</returns>
        public static string Load(string promptName)
        {
            // ⭐ v1.7.0: 检查缓存
            string activeLangFolder = LanguageDatabase.activeLanguage.folderName;
            string cacheKey = $"{activeLangFolder}_{promptName}";

            if (_promptCache.TryGetValue(cacheKey, out string cachedContent))
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
                return CacheAndReturn(cacheKey, File.ReadAllText(userLangPath));
            }

            // 2. User Override - Global (Root of Prompts folder in Config)
            string userGlobalPath = Path.Combine(configRoot, "TheSecondSeat", PromptsFolderName, fileName);
            if (File.Exists(userGlobalPath))
            {
                return CacheAndReturn(cacheKey, File.ReadAllText(userGlobalPath));
            }

            // --- Mod Defaults ---
            var modContent = LoadedModManager.GetMod<TheSecondSeat.Settings.TheSecondSeatMod>()?.Content;
            if (modContent != null)
            {
                // 3. Mod Default - Active Language
                string activeLangPath = Path.Combine(modContent.RootDir, "Languages", activeLangFolder, PromptsFolderName, fileName);
                if (File.Exists(activeLangPath))
                {
                    return CacheAndReturn(cacheKey, File.ReadAllText(activeLangPath));
                }

                // 4. Mod Fallback - English (Default)
                if (activeLangFolder != DefaultLanguage)
                {
                    string defaultPath = Path.Combine(modContent.RootDir, "Languages", DefaultLanguage, PromptsFolderName, fileName);
                    if (File.Exists(defaultPath))
                    {
                        return CacheAndReturn(cacheKey, File.ReadAllText(defaultPath));
                    }
                }
            }

            Log.Warning($"[The Second Seat] Prompt file not found: {promptName}.txt");
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
            
            // Open folder cross-platform
            if (System.Environment.OSVersion.Platform == System.PlatformID.Win32NT)
            {
                System.Diagnostics.Process.Start("explorer.exe", path);
            }
            else
            {
                // Fallback for other OS (Mac/Linux - though RimWorld mods are mostly Windows)
                UnityEngine.Application.OpenURL(path);
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
            string sourcePath = Path.Combine(modContent.RootDir, "Languages", activeLangFolder, PromptsFolderName);
            
            // Fallback source: Mod/Languages/English/Prompts/
            if (!Directory.Exists(sourcePath) && activeLangFolder != DefaultLanguage)
            {
                sourcePath = Path.Combine(modContent.RootDir, "Languages", DefaultLanguage, PromptsFolderName);
            }
            
            if (Directory.Exists(sourcePath))
            {
                string[] files = Directory.GetFiles(sourcePath, "*.txt");
                foreach (string file in files)
                {
                    string fileName = Path.GetFileName(file);
                    string destFile = Path.Combine(targetFolder, fileName);
                    
                    // Only copy if not exists to avoid overwriting user changes
                    if (!File.Exists(destFile))
                    {
                        File.Copy(file, destFile);
                    }
                }
                Log.Message($"[The Second Seat] Initialized {files.Length} prompt files to {targetFolder}");
                Messages.Message("提示词初始化完成", MessageTypeDefOf.PositiveEvent, false);
            }
            else
            {
                Log.Warning($"[The Second Seat] Could not find source prompts at {sourcePath}");
                Messages.Message("未找到源提示词文件", MessageTypeDefOf.RejectInput, false);
            }
            
            // Clear cache to ensure new files are used
            ClearCache();
        }
    }
}
