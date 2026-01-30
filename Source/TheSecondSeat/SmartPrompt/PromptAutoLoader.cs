using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Verse;
using TheSecondSeat.PersonaGeneration;

namespace TheSecondSeat.SmartPrompt
{
    /// <summary>
    /// 自动加载器
    /// 负责扫描 Prompts 目录下的 TXT 文件，解析 Metadata，动态生成 PromptModuleDef
    /// 从而消除手动维护 XML 的需求
    /// </summary>
    public static class PromptAutoLoader
    {
        private static readonly Regex FrontMatterRegex = new Regex(@"^---\s*\n([\s\S]*?)\n---\s*\n", RegexOptions.Multiline);
        
        /// <summary>
        /// ⭐ v3.1.0: 执行自动加载
        /// 集成 PromptLoader 的 disabledPrompts 设置，实现统一的提示词管理
        /// </summary>
        public static void AutoLoadDefs()
        {
            Log.Message("[SmartPrompt] Starting Auto-Loader for Prompt Modules...");
            
            // 获取所有可用的 Prompt 文件名（包括用户覆盖）
            var promptNames = PromptLoader.GetAllPromptNames();
            int loadedCount = 0;
            int skippedCount = 0;
            
            foreach (var name in promptNames)
            {
                // ⭐ v3.1.0: 检查是否在 PromptManagementWindow 中被禁用
                if (PromptLoader.IsDisabled(name))
                {
                    skippedCount++;
                    continue;
                }
                
                // 如果已经存在同名的 XML Def，则跳过（XML 优先级更高，或者视为已定义）
                if (DefDatabase<PromptModuleDef>.GetNamedSilentFail(name) != null)
                {
                    continue;
                }
                
                // 加载内容（通过 PromptLoader，支持用户覆盖）
                string fullContent = PromptLoader.Load(name, silent: true);
                if (string.IsNullOrEmpty(fullContent)) continue;
                
                // 解析 Front Matter
                var match = FrontMatterRegex.Match(fullContent);
                if (match.Success)
                {
                    try
                    {
                        string metadataYaml = match.Groups[1].Value;
                        string bodyContent = fullContent.Substring(match.Length);
                        
                        // 创建 Def
                        var def = ParseMetadataAndCreateDef(name, metadataYaml);
                        if (def != null)
                        {
                            // 直接设置 content，避免二次加载
                            def.content = bodyContent;
                            
                            // 注册 Def
                            DefDatabase<PromptModuleDef>.Add(def);
                            loadedCount++;
                            
                            if (Prefs.DevMode)
                            {
                                Log.Message($"[SmartPrompt] Auto-loaded module: {def.defName} ({def.moduleType})");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Warning($"[SmartPrompt] Failed to auto-load module {name}: {ex.Message}");
                    }
                }
                else
                {
                    // ⭐ v3.1.0: 对于没有 Front Matter 的 TXT 文件，也尝试创建默认模块
                    // 这样即使用户创建了简单的 TXT 文件，也能被 SmartPrompt 识别
                    try
                    {
                        var def = CreateDefaultModuleDef(name, fullContent);
                        if (def != null)
                        {
                            DefDatabase<PromptModuleDef>.Add(def);
                            loadedCount++;
                            
                            if (Prefs.DevMode)
                            {
                                Log.Message($"[SmartPrompt] Auto-loaded plain module: {def.defName}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Warning($"[SmartPrompt] Failed to create default module for {name}: {ex.Message}");
                    }
                }
            }
            
            if (loadedCount > 0)
            {
                Log.Message($"[SmartPrompt] Auto-loaded {loadedCount} modules from TXT files. (Skipped {skippedCount} disabled)");
            }
        }
        
        /// <summary>
        /// ⭐ v3.1.0: 为没有 Front Matter 的 TXT 文件创建默认模块
        /// </summary>
        private static PromptModuleDef CreateDefaultModuleDef(string name, string content)
        {
            // 如果内容为空或太短，跳过
            if (string.IsNullOrWhiteSpace(content) || content.Length < 10) return null;
            
            var def = new PromptModuleDef
            {
                defName = name,
                label = name,
                content = content,
                moduleType = InferModuleType(name),
                priority = InferPriority(name),
                // 根据文件名推断意图
                triggerIntents = InferIntents(name),
                // 从内容中提取关键词
                expandedKeywords = ExtractKeywords(name, content)
            };
            
            // ⭐ v3.2.0: 根据 moduleType 自动设置 alwaysActive
            // Core 和 Format 类型始终激活，其他类型按需加载
            if (def.moduleType == ModuleType.Core || def.moduleType == ModuleType.Format)
            {
                def.alwaysActive = true;
            }
            else
            {
                // Context, Skill, Memory, Extension 类型默认按需加载
                // 除非显式设置了 alwaysActive: true
                // 这里保持原有设置，不强制覆盖
            }
            
            return def;
        }
        
        /// <summary>
        /// 根据文件名推断模块类型
        /// </summary>
        private static ModuleType InferModuleType(string name)
        {
            string lower = name.ToLowerInvariant();
            
            if (lower.Contains("identity") || lower.Contains("core") || lower.Contains("system"))
                return ModuleType.Core;
            if (lower.Contains("format") || lower.Contains("output") || lower.Contains("structure"))
                return ModuleType.Format;
            if (lower.Contains("module_") || lower.Contains("skill"))
                return ModuleType.Skill;
            if (lower.Contains("context") || lower.Contains("behavior") || lower.Contains("rules"))
                return ModuleType.Context;
            if (lower.Contains("memory") || lower.Contains("history"))
                return ModuleType.Memory;
                
            return ModuleType.Extension;
        }
        
        /// <summary>
        /// 根据文件名推断优先级
        /// </summary>
        private static int InferPriority(string name)
        {
            string lower = name.ToLowerInvariant();
            
            if (lower.Contains("identity") || lower.Contains("core"))
                return 1000;
            if (lower.Contains("system") || lower.Contains("master"))
                return 900;
            if (lower.Contains("format") || lower.Contains("output"))
                return 800;
            if (lower.Contains("behavior") || lower.Contains("rules"))
                return 700;
            if (lower.Contains("romantic") || lower.Contains("affinity"))
                return 600;
            if (lower.Contains("module_"))
                return 500;
                
            return 100;
        }
        
        /// <summary>
        /// 根据文件名推断意图
        /// </summary>
        private static List<string> InferIntents(string name)
        {
            var intents = new List<string>();
            string lower = name.ToLowerInvariant();
            
            // 从文件名提取关键词作为意图
            if (lower.Contains("agriculture") || lower.Contains("harvest") || lower.Contains("farm"))
                intents.Add("Harvest");
            if (lower.Contains("combat") || lower.Contains("attack") || lower.Contains("fight"))
                intents.Add("Combat");
            if (lower.Contains("construction") || lower.Contains("build"))
                intents.Add("Build");
            if (lower.Contains("hunt"))
                intents.Add("Hunt");
            if (lower.Contains("medical") || lower.Contains("doctor"))
                intents.Add("Medical");
            if (lower.Contains("craft") || lower.Contains("make"))
                intents.Add("Craft");
            if (lower.Contains("research"))
                intents.Add("Research");
                
            return intents;
        }
        
        /// <summary>
        /// 从内容中提取关键词
        /// </summary>
        private static List<string> ExtractKeywords(string name, string content)
        {
            var keywords = new List<string>();
            
            // 从文件名提取
            var nameParts = name.Split(new[] { '_', '-', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in nameParts)
            {
                if (part.Length > 2 && !IsCommonWord(part))
                {
                    keywords.Add(part.ToLowerInvariant());
                }
            }
            
            // 从标题行提取 (## 或 ###)
            var lines = content.Split('\n');
            foreach (var line in lines)
            {
                if (line.TrimStart().StartsWith("#"))
                {
                    var headerText = line.TrimStart('#', ' ', '\t', '\r');
                    var words = headerText.Split(new[] { ' ', ':', '-', '（', '）', '(', ')' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var word in words)
                    {
                        if (word.Length > 1 && !IsCommonWord(word))
                        {
                            keywords.Add(word.ToLowerInvariant());
                        }
                    }
                }
            }
            
            return keywords;
        }
        
        /// <summary>
        /// 判断是否为常见的无意义词汇
        /// </summary>
        private static bool IsCommonWord(string word)
        {
            string[] commonWords = { "the", "a", "an", "and", "or", "is", "are", "to", "for", "of", "in", "on", "with", "module", "txt" };
            return Array.Exists(commonWords, w => w.Equals(word, StringComparison.OrdinalIgnoreCase));
        }
        
        /// <summary>
        /// 解析 Metadata 并创建 Def 对象
        /// 支持简单的键值对解析
        /// </summary>
        private static PromptModuleDef ParseMetadataAndCreateDef(string defName, string yaml)
        {
            var def = new PromptModuleDef();
            def.defName = defName;
            def.label = defName; // 默认 label
            
            using (var reader = new StringReader(yaml))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    line = line.Trim();
                    if (string.IsNullOrEmpty(line) || line.StartsWith("#")) continue;
                    
                    var parts = line.Split(new[] { ':' }, 2);
                    if (parts.Length != 2) continue;
                    
                    string key = parts[0].Trim().ToLowerInvariant();
                    string value = parts[1].Trim();
                    
                    switch (key)
                    {
                        case "defname":
                            def.defName = value;
                            break;
                        case "label":
                            def.label = value;
                            break;
                        case "description":
                            def.description = value;
                            break;
                        case "author":
                            def.author = value;
                            break;
                        case "moduletype":
                        case "type":
                            if (Enum.TryParse<ModuleType>(value, true, out var type))
                            {
                                def.moduleType = type;
                            }
                            break;
                        case "priority":
                            if (int.TryParse(value, out int p))
                            {
                                def.priority = p;
                            }
                            break;
                        case "alwaysactive":
                            if (bool.TryParse(value, out bool aa))
                            {
                                def.alwaysActive = aa;
                            }
                            break;
                        case "requirescombat":
                            if (bool.TryParse(value, out bool rc))
                            {
                                def.requiresCombat = rc;
                            }
                            break;
                        case "requirespeace":
                            if (bool.TryParse(value, out bool rp))
                            {
                                def.requiresPeace = rp;
                            }
                            break;
                        case "intents":
                        case "triggerintents":
                            def.triggerIntents = ParseList(value);
                            break;
                        case "keywords":
                        case "expandedkeywords":
                            def.expandedKeywords = ParseList(value);
                            break;
                        case "dependencies":
                            def.dependencies = ParseList(value);
                            break;
                    }
                }
            }
            
            return def;
        }
        
        /// <summary>
        /// 解析列表格式 [item1, item2]
        /// </summary>
        private static List<string> ParseList(string value)
        {
            var list = new List<string>();
            if (value.StartsWith("[") && value.EndsWith("]"))
            {
                var content = value.Substring(1, value.Length - 2);
                var items = content.Split(',');
                foreach (var item in items)
                {
                    var trimmed = item.Trim();
                    if (!string.IsNullOrEmpty(trimmed))
                    {
                        list.Add(trimmed);
                    }
                }
            }
            else
            {
                // 尝试直接解析逗号分隔
                var items = value.Split(',');
                foreach (var item in items)
                {
                    var trimmed = item.Trim();
                    if (!string.IsNullOrEmpty(trimmed))
                    {
                        list.Add(trimmed);
                    }
                }
            }
            return list;
        }
    }
}