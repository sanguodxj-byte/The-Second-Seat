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
        /// 执行自动加载
        /// </summary>
        public static void AutoLoadDefs()
        {
            Log.Message("[SmartPrompt] Starting Auto-Loader for Prompt Modules...");
            
            // 获取所有可用的 Prompt 文件名
            var promptNames = PromptLoader.GetAllPromptNames();
            int loadedCount = 0;
            
            foreach (var name in promptNames)
            {
                // 如果已经存在同名的 XML Def，则跳过（XML 优先级更高，或者视为已定义）
                if (DefDatabase<PromptModuleDef>.GetNamedSilentFail(name) != null)
                {
                    continue;
                }
                
                // 加载内容
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
                            // 注入数据库
                            // 注意：由于 PromptLoader.Load 已经返回了内容，我们不需要再设置 contentPath
                            // 但为了保持一致性，如果使用了 contentPath 机制，这里可以设置。
                            // 实际上，我们直接把处理后的 bodyContent 赋值给 def.content 会更高效，
                            // 避免二次加载和再次解析 Front Matter。
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
            }
            
            if (loadedCount > 0)
            {
                Log.Message($"[SmartPrompt] Auto-loaded {loadedCount} new modules from TXT files.");
            }
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