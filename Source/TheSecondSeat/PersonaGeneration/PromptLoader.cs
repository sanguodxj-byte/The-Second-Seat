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

        // 判断是否使用中文
        private static bool IsChinese => LanguageDatabase.activeLanguage?.folderName?.Contains("Chinese") == true;

        /// <summary>
        /// Checks if a prompt is disabled in settings.
        /// </summary>
        public static bool IsDisabled(string promptName)
        {
            return Settings.TheSecondSeatMod.Settings.disabledPrompts?.Contains(promptName) ?? false;
        }
        
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
        /// 0. Config/TheSecondSeat/Prompts/{Persona}/{Language}/{file} (User Override - Persona & Language)
        /// 1. Config/TheSecondSeat/Prompts/{Persona}/{file} (User Override - Persona Global)
        /// 2. Config/TheSecondSeat/Prompts/{Language}/{file} (User Override - Specific Language)
        /// 3. Config/TheSecondSeat/Prompts/{file} (User Override - Global)
        /// 4. Mod/Languages/{Language}/Prompts/{file} (Mod Default)
        /// 5. Mod/Languages/English/Prompts/{file} (Mod Fallback)
        /// </summary>
        /// <param name="promptName">The name of the prompt file (without extension).</param>
        /// <param name="personaName">The persona name to look for specific overrides (optional).</param>
        /// <param name="silent">If true, don't log warning when file is not found. Default is false.</param>
        /// <returns>The content of the prompt file.</returns>
        public static string Load(string promptName, string personaName = null, bool silent = false)
        {
            // Check if disabled
            if (IsDisabled(promptName))
            {
                return "";
            }

            // ⭐ v2.9.6: 添加 null 检查
            if (LanguageDatabase.activeLanguage == null)
            {
                if (!silent) Log.Warning("[The Second Seat] LanguageDatabase.activeLanguage is null, cannot load prompt.");
                return $"[Error: Language not initialized]";
            }

            // ⭐ v1.7.0: 检查缓存
            string activeLangFolder = LanguageDatabase.activeLanguage.folderName;
            string cacheKey = $"{activeLangFolder}_{personaName ?? "Global"}_{promptName}";

            // 开发模式下禁用缓存，方便热重载
            if (!Prefs.DevMode && _promptCache.TryGetValue(cacheKey, out string cachedContent))
            {
                return cachedContent;
            }

            string fileName = promptName + ".txt";
            string configRoot = GenFilePaths.ConfigFolderPath;

            // --- User Overrides (Persona Specific) ---
            if (!string.IsNullOrEmpty(personaName))
            {
                // 0. User Override - Persona & Language
                string personaLangPath = Path.Combine(configRoot, "TheSecondSeat", PromptsFolderName, personaName, activeLangFolder, fileName);
                if (File.Exists(personaLangPath))
                {
                    if (Prefs.DevMode) Log.Message($"[The Second Seat] Loading prompt from User Override (Persona Lang): {personaLangPath}");
                    return CacheAndReturn(cacheKey, File.ReadAllText(personaLangPath));
                }

                // 1. User Override - Persona Global
                string personaGlobalPath = Path.Combine(configRoot, "TheSecondSeat", PromptsFolderName, personaName, fileName);
                if (File.Exists(personaGlobalPath))
                {
                    if (Prefs.DevMode) Log.Message($"[The Second Seat] Loading prompt from User Override (Persona Global): {personaGlobalPath}");
                    return CacheAndReturn(cacheKey, File.ReadAllText(personaGlobalPath));
                }
            }
            
            // --- User Overrides (General Config Folder) ---
            
            // 2. User Override - Specific Language
            string userLangPath = Path.Combine(configRoot, "TheSecondSeat", PromptsFolderName, activeLangFolder, fileName);
            if (File.Exists(userLangPath))
            {
                if (Prefs.DevMode) Log.Message($"[The Second Seat] Loading prompt from User Override (Lang): {userLangPath}");
                return CacheAndReturn(cacheKey, File.ReadAllText(userLangPath));
            }

            // 3. User Override - Global (Root of Prompts folder in Config)
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
                    else if (Prefs.DevMode)
                    {
                        Log.Message($"[The Second Seat] Mod Default file not found at: {activeLangPath}");
                    }
                }
                else if (Prefs.DevMode)
                {
                     Log.Message($"[The Second Seat] Mod Prompts Dir not found for language: {activeLangFolder}");
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
                        else if (Prefs.DevMode)
                        {
                            Log.Message($"[The Second Seat] Mod Fallback file not found at: {defaultPath}");
                        }
                    }
                }
            }
            else
            {
                if (Prefs.DevMode) Log.Warning("[The Second Seat] ModContent not found.");
            }

            // ⭐ v1.9.4: 支持静默模式，避免对动态标签文件输出警告
            if (!silent)
            {
                Log.Warning($"[The Second Seat] Prompt file not found: {promptName}.txt. Checked paths included Persona: {personaName}, Lang: {activeLangFolder}");
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
        /// Gets a list of all available prompt names (without extension) from all sources.
        /// </summary>
        public static List<string> GetAllPromptNames(string personaName = null)
        {
            HashSet<string> names = new HashSet<string>();

            // ⭐ v2.9.6: 添加 null 检查，避免在语言未初始化时崩溃
            if (LanguageDatabase.activeLanguage == null)
            {
                Log.Warning("[The Second Seat] LanguageDatabase.activeLanguage is null, returning empty list.");
                return new List<string>(names);
            }

            // 1. Mod Defaults
            var modContent = LoadedModManager.GetMod<TheSecondSeat.Settings.TheSecondSeatMod>()?.Content;
            if (modContent != null)
            {
                // Active Language
                string activeLangFolder = LanguageDatabase.activeLanguage.folderName;
                string modPromptsDir = GetModPromptsPath(modContent, activeLangFolder);
                if (modPromptsDir != null && Directory.Exists(modPromptsDir))
                {
                    foreach (var file in Directory.GetFiles(modPromptsDir, "*.txt"))
                        names.Add(Path.GetFileNameWithoutExtension(file));
                }

                // Fallback (English)
                if (activeLangFolder != DefaultLanguage)
                {
                    string defaultPromptsDir = GetModPromptsPath(modContent, DefaultLanguage);
                    if (defaultPromptsDir != null && Directory.Exists(defaultPromptsDir))
                    {
                        foreach (var file in Directory.GetFiles(defaultPromptsDir, "*.txt"))
                            names.Add(Path.GetFileNameWithoutExtension(file));
                    }
                }
            }

            // 2. Config Overrides
            string configRoot = Path.Combine(GenFilePaths.ConfigFolderPath, "TheSecondSeat", PromptsFolderName);
            if (Directory.Exists(configRoot))
            {
                // Global
                foreach (var file in Directory.GetFiles(configRoot, "*.txt"))
                    names.Add(Path.GetFileNameWithoutExtension(file));

                // Language Specific
                string activeLangFolder = LanguageDatabase.activeLanguage.folderName;
                string userLangPath = Path.Combine(configRoot, activeLangFolder);
                if (Directory.Exists(userLangPath))
                {
                    foreach (var file in Directory.GetFiles(userLangPath, "*.txt"))
                        names.Add(Path.GetFileNameWithoutExtension(file));
                }

                // Persona Specific
                if (!string.IsNullOrEmpty(personaName))
                {
                    // Persona Global
                    string personaGlobalPath = Path.Combine(configRoot, personaName);
                    if (Directory.Exists(personaGlobalPath))
                    {
                        foreach (var file in Directory.GetFiles(personaGlobalPath, "*.txt"))
                            names.Add(Path.GetFileNameWithoutExtension(file));
                    }
                    
                    // Persona Language
                    string personaLangPath = Path.Combine(configRoot, personaName, activeLangFolder);
                    if (Directory.Exists(personaLangPath))
                    {
                        foreach (var file in Directory.GetFiles(personaLangPath, "*.txt"))
                            names.Add(Path.GetFileNameWithoutExtension(file));
                    }
                }
            }

            return new List<string>(names);
        }

        /// <summary>
        /// Saves a user override for a prompt.
        /// Always saves to the language-specific config folder.
        /// </summary>
        public static void SaveUserOverride(string promptName, string content, string personaName = null)
        {
            EnsureConfigDirectory(personaName);
            string configPromptsPath = Path.Combine(GenFilePaths.ConfigFolderPath, "TheSecondSeat", PromptsFolderName);
            string activeLangFolder = LanguageDatabase.activeLanguage.folderName;
            
            string targetFolder;
            if (!string.IsNullOrEmpty(personaName))
            {
                targetFolder = Path.Combine(configPromptsPath, personaName, activeLangFolder);
            }
            else
            {
                targetFolder = Path.Combine(configPromptsPath, activeLangFolder);
            }
            
            if (!Directory.Exists(targetFolder)) Directory.CreateDirectory(targetFolder);
            
            string filePath = Path.Combine(targetFolder, promptName + ".txt");
            File.WriteAllText(filePath, content);
            
            // Update cache
            string cacheKey = $"{activeLangFolder}_{personaName ?? "Global"}_{promptName}";
            _promptCache[cacheKey] = content;
        }

        /// <summary>
        /// Deletes the user override for a prompt (reverting to default).
        /// </summary>
        public static void DeleteUserOverride(string promptName, string personaName = null)
        {
            string configPromptsPath = Path.Combine(GenFilePaths.ConfigFolderPath, "TheSecondSeat", PromptsFolderName);
            string activeLangFolder = LanguageDatabase.activeLanguage.folderName;
            
            string targetFolder;
            if (!string.IsNullOrEmpty(personaName))
            {
                targetFolder = Path.Combine(configPromptsPath, personaName, activeLangFolder);
            }
            else
            {
                targetFolder = Path.Combine(configPromptsPath, activeLangFolder);
            }
            
            // 1. Delete Language Specific
            string userLangPath = Path.Combine(targetFolder, promptName + ".txt");
            if (File.Exists(userLangPath)) File.Delete(userLangPath);
            
            // Clear cache to force reload
            _promptCache.Remove($"{activeLangFolder}_{personaName ?? "Global"}_{promptName}");
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
            
            if (Prefs.DevMode)
            {
                Log.Message($"[The Second Seat] Could not find prompts folder for language '{langFolder}' in '{languagesDir}'. Specific path checked: {specificPath}");
            }

            return null;
        }

        /// <summary>
        /// Ensures the user override directory exists and optionally dumps default prompts there.
        /// </summary>
        public static void EnsureConfigDirectory(string personaName = null)
        {
            string configPromptsPath = Path.Combine(GenFilePaths.ConfigFolderPath, "TheSecondSeat", PromptsFolderName);
            if (!Directory.Exists(configPromptsPath))
            {
                Directory.CreateDirectory(configPromptsPath);
            }
            
            // ⭐ v2.9.6: 添加 null 检查
            if (LanguageDatabase.activeLanguage == null)
            {
                Log.Warning("[The Second Seat] LanguageDatabase.activeLanguage is null, skipping language folder creation.");
                return;
            }
            
            string activeLangFolder = LanguageDatabase.activeLanguage.folderName;

            // Ensure language subfolders exist for current language (Global)
            string langPath = Path.Combine(configPromptsPath, activeLangFolder);
            if (!Directory.Exists(langPath))
            {
                Directory.CreateDirectory(langPath);
            }

            // Ensure Persona folder exists if specified
            if (!string.IsNullOrEmpty(personaName))
            {
                string personaPath = Path.Combine(configPromptsPath, personaName);
                if (!Directory.Exists(personaPath))
                {
                    Directory.CreateDirectory(personaPath);
                }

                string personaLangPath = Path.Combine(personaPath, activeLangFolder);
                if (!Directory.Exists(personaLangPath))
                {
                    Directory.CreateDirectory(personaLangPath);
                }
            }
        }

        /// <summary>
        /// Creates a README file explaining the prompt files.
        /// </summary>
        public static void CreateReadme(string personaName = null)
        {
            EnsureConfigDirectory(personaName);
            string configPromptsPath = Path.Combine(GenFilePaths.ConfigFolderPath, "TheSecondSeat", PromptsFolderName);
            
            // If personaName is provided, place README in persona folder too
            string targetFolder = configPromptsPath;
            if (!string.IsNullOrEmpty(personaName))
            {
                targetFolder = Path.Combine(configPromptsPath, personaName);
            }

            string readmePath = Path.Combine(targetFolder, "README.txt");

            string content;
            if (IsChinese)
            {
                content = @"
=== The Second Seat 自定义提示词指南 ===

要自定义 AI 的系统提示词，请在此文件夹中创建与内部提示词文件同名的 .txt 文件。
这些文件将覆盖模组的默认提示词。

[v3.0 新增] 自动加载新模块：
您可以创建新的 .txt 文件并添加 Metadata 头信息，系统会自动将其注册为新的技能模块，无需编写 XML。
格式示例：
---
defName: Module_NewSkill
priority: 500
intents: [MySkill, Action]
keywords: [关键词1, 关键词2]
---
## 技能内容...

您可以直接将文件放在此文件夹中（全局覆盖），或放在特定语言的子文件夹中（例如 'ChineseSimplified', 'English'）。
特定语言的覆盖优先于全局覆盖。

=== 文件列表与说明 ===

-- 核心 (高维TTRPG框架) --
Identity_Core.txt           : 定义元身份 (高维TTRPG框架, GMPC协议)
SystemPrompt_Master.txt     : 整合所有组件的主系统提示词
Language_Instruction.txt    : 关于使用哪种语言的关键指令

-- 行为规则 (双频道意识) --
BehaviorRules_Assistant.txt : 助理模式规则 (元频道 & IC频道)
BehaviorRules_Opponent.txt  : 对手模式规则 (挑战性, 控制欲)
BehaviorRules_Engineer.txt  : 工程师模式规则 (技术性, 诊断)
BehaviorRules_Universal.txt : 通用规则 (真实契约 Reality Pact)

-- 输出格式 --
OutputFormat_Structure.txt  : 定义 JSON/文本 响应的结构
OutputFormat_Fields.txt     : 解释输出中的特定字段
OutputFormat_Examples.txt   : 正确输出的示例

-- 恋爱系统 (好感度) --
Romantic_Intro.txt          : 恋爱互动指令
Romantic_Soulmate.txt       : 极高好感度 (90+) 时的行为
Romantic_Partner.txt        : 高好感度 (60-89) 时的行为
Romantic_Yandere.txt        : 病娇特质的特殊行为
Romantic_Tsundere.txt       : 傲娇特质的特殊行为

-- 诊断 --
LogDiagnosis.txt            : 分析游戏日志的指令

";
            }
            else
            {
                content = @"
=== The Second Seat Custom Prompts Guide ===

To customize the AI's system prompts, create .txt files with the same names as the internal prompt files in this folder.
These files will override the mod's default prompts.

[v3.0 NEW] Auto-Load New Modules:
You can create new .txt files with Metadata header, and the system will automatically register them as new skill modules without XML.
Example Format:
---
defName: Module_NewSkill
priority: 500
intents: [MySkill, Action]
keywords: [keyword1, keyword2]
---
## Skill Content...

You can place files directly in this folder (Global Override) or in language-specific subfolders (e.g., 'ChineseSimplified', 'English').
Language-specific overrides take precedence over global overrides.

=== File List & Descriptions ===

-- Core (Higher-dimensional TTRPG Framework) --
Identity_Core.txt           : Defines the meta-identity (Higher-dimensional TTRPG framework, GMPC protocol)
SystemPrompt_Master.txt     : Master system prompt integrating all components
Language_Instruction.txt    : Critical instructions about which language to speak.

-- Behavior Rules (Dual Channel Consciousness) --
BehaviorRules_Assistant.txt : Rules for Assistant Mode (Meta Channel & IC Channel)
BehaviorRules_Opponent.txt  : Rules for Opponent Mode (Challenging, controlling)
BehaviorRules_Engineer.txt  : Rules for Engineer Mode (Technical, diagnostic)
BehaviorRules_Universal.txt : Universal rules (Reality Pact)

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
            }
            File.WriteAllText(readmePath, content);
        }
        
        /// <summary>
        /// Opens the config folder in the OS file explorer.
        /// </summary>
        public static void OpenConfigFolder(string personaName = null)
        {
            EnsureConfigDirectory(personaName);
            string path = Path.Combine(GenFilePaths.ConfigFolderPath, "TheSecondSeat", PromptsFolderName);
            
            if (!string.IsNullOrEmpty(personaName))
            {
                path = Path.Combine(path, personaName);
            }

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
        public static void InitializeUserPrompts(string personaName = null)
        {
            EnsureConfigDirectory(personaName);
            CreateReadme(personaName);
            
            string configPromptsPath = Path.Combine(GenFilePaths.ConfigFolderPath, "TheSecondSeat", PromptsFolderName);
            string activeLangFolder = LanguageDatabase.activeLanguage.folderName;
            
            string targetFolder;
            if (!string.IsNullOrEmpty(personaName))
            {
                targetFolder = Path.Combine(configPromptsPath, personaName, activeLangFolder);
            }
            else
            {
                targetFolder = Path.Combine(configPromptsPath, activeLangFolder);
            }
            
            var modContent = LoadedModManager.GetMod<TheSecondSeat.Settings.TheSecondSeatMod>()?.Content;
            if (modContent == null) return;
            
            // Source path: Mod/Languages/{Language}/Prompts/
            Log.Message($"[The Second Seat] Initializing User Prompts. Active Language: {activeLangFolder} (Persona: {personaName ?? "Global"})");

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
                Messages.Message(IsChinese ? "提示词初始化完成" : "Prompt initialization complete", MessageTypeDefOf.PositiveEvent, false);
            }
            else
            {
                Log.Warning($"[The Second Seat] Could not find source prompts for language {activeLangFolder} or fallback.");
                Messages.Message(IsChinese ? "未找到源提示词文件" : "Source prompt files not found", MessageTypeDefOf.RejectInput, false);
            }
            
            // Clear cache to ensure new files are used
            ClearCache();
        }
    }
}
