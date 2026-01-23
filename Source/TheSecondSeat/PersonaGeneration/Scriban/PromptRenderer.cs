using System;
using System.Collections.Generic;
using System.Linq;
using Scriban;
using Scriban.Runtime;
using RimWorld;
using Verse;

namespace TheSecondSeat.PersonaGeneration.Scriban
{
    /// <summary>
    /// Scriban 模板渲染器
    /// 封装 Scriban 引擎的调用逻辑，配置 MemberRenamer 和 TemplateLoader
    /// ⭐ v2.0.0: 添加模板编译缓存，减少 Template.Parse() 的 CPU 开销（90%+）
    /// ⭐ v2.0.0: 添加自定义 Scriban 函数，允许模板直接访问游戏数据
    /// </summary>
    public static class PromptRenderer
    {
        private static readonly ModPromptTemplateLoader _loader = new ModPromptTemplateLoader();
        
        // ⭐ v2.0.0: 模板编译缓存
        private static readonly Dictionary<string, Template> _templateCache = new Dictionary<string, Template>();
        private static readonly Dictionary<string, int> _contentHashCache = new Dictionary<string, int>();
        
        // ⭐ 统计信息（用于调试）
        private static int _cacheHits = 0;
        private static int _cacheMisses = 0;

        static PromptRenderer()
        {
            // 输出 Scriban 版本以验证升级是否生效
            var scribanVersion = typeof(Template).Assembly.GetName().Version;
            Log.Message($"[The Second Seat] Initialized Scriban Renderer v2.0.0. Version: {scribanVersion} (ILRepack Integrated)");
            Log.Message($"[The Second Seat] Template compilation cache enabled. Dev mode auto-recompile: {Prefs.DevMode}");
        }

        /// <summary>
        /// 手动初始化以触发静态构造函数
        /// </summary>
        public static void Init() { }
        
        /// <summary>
        /// ⭐ v2.0.0: 清除模板编译缓存（用于热重载）
        /// </summary>
        public static void ClearTemplateCache()
        {
            int count = _templateCache.Count;
            _templateCache.Clear();
            _contentHashCache.Clear();
            PromptLoader.ClearCache(); // 同时清除文件内容缓存
            
            Log.Message($"[The Second Seat] Template cache cleared. {count} templates removed. Stats before clear: Hits={_cacheHits}, Misses={_cacheMisses}");
            _cacheHits = 0;
            _cacheMisses = 0;
        }
        
        /// <summary>
        /// ⭐ v2.0.0: 获取缓存统计信息
        /// </summary>
        public static string GetCacheStats()
        {
            float hitRate = (_cacheHits + _cacheMisses) > 0 
                ? (float)_cacheHits / (_cacheHits + _cacheMisses) * 100f 
                : 0f;
            return $"Templates: {_templateCache.Count}, Hits: {_cacheHits}, Misses: {_cacheMisses}, Hit Rate: {hitRate:F1}%";
        }

        /// <summary>
        /// 渲染指定的模板文件
        /// </summary>
        /// <param name="templateName">模板名称（不含 .txt 后缀），如 "SystemPrompt_Master"</param>
        /// <param name="context">数据上下文</param>
        /// <returns>渲染后的字符串</returns>
        public static string Render(string templateName, PromptContext context)
        {
            try
            {
                // 1. 加载模板内容（已被 PromptLoader 缓存）
                string templateContent = PromptLoader.Load(templateName);
                if (string.IsNullOrEmpty(templateContent) || templateContent.StartsWith("[Error:"))
                {
                    Log.Error($"[The Second Seat] Failed to load template: {templateName}");
                    return $"Error: Template {templateName} missing.";
                }

                // 2. ⭐ v2.0.0: 检查编译缓存
                Template template = GetOrCompileTemplate(templateName, templateContent);
                if (template == null)
                {
                    return $"Error: Template {templateName} has syntax errors.";
                }

                // 3. ⭐ v2.0.0: 创建渲染上下文（包含自定义函数）
                var templateContext = CreateTemplateContext(context);

                // 4. 渲染（极快，因为模板已编译）
                return template.Render(templateContext);
            }
            catch (Exception ex)
            {
                Log.Error($"[The Second Seat] Render Error ({templateName}): {ex}");
                return $"Error: Render failed for {templateName}. {ex.Message}";
            }
        }
        
        /// <summary>
        /// ⭐ v2.0.0: 获取或编译模板（带缓存）
        /// </summary>
        private static Template GetOrCompileTemplate(string templateName, string templateContent)
        {
            int currentHash = templateContent.GetHashCode();
            
            // 检查缓存
            if (_templateCache.TryGetValue(templateName, out Template cachedTemplate))
            {
                // 开发模式下检查内容是否变化
                if (Prefs.DevMode)
                {
                    if (_contentHashCache.TryGetValue(templateName, out int cachedHash) && cachedHash == currentHash)
                    {
                        _cacheHits++;
                        return cachedTemplate;
                    }
                    // 内容已变化，需要重新编译
                    Log.Message($"[The Second Seat] Template '{templateName}' content changed, recompiling...");
                }
                else
                {
                    _cacheHits++;
                    return cachedTemplate;
                }
            }
            
            // 编译模板
            _cacheMisses++;
            var template = Template.Parse(templateContent, templateName);
            
            if (template.HasErrors)
            {
                foreach (var error in template.Messages)
                {
                    Log.Error($"[The Second Seat] Template Parse Error ({templateName}): {error}");
                }
                return null;
            }
            
            // 存入缓存
            _templateCache[templateName] = template;
            _contentHashCache[templateName] = currentHash;
            
            if (Prefs.DevMode)
            {
                Log.Message($"[The Second Seat] Template '{templateName}' compiled and cached.");
            }
            
            return template;
        }
        
        /// <summary>
        /// ⭐ v2.0.0: 创建渲染上下文，包含自定义函数
        /// </summary>
        private static TemplateContext CreateTemplateContext(PromptContext context)
        {
            var scriptObject = new ScriptObject();
            
            // 导入 PromptContext 数据
            scriptObject.Import(context);
            
            // ⭐ 注入自定义 Scriban 函数（允许模板直接访问游戏数据）
            RegisterCustomFunctions(scriptObject);
            
            var templateContext = new TemplateContext();
            templateContext.TemplateLoader = _loader;
            templateContext.PushGlobal(scriptObject);
            
            return templateContext;
        }
        
        /// <summary>
        /// ⭐ v2.0.0: 注册自定义 Scriban 函数
        /// 模板中可以使用 {{ get_weather() }}、{{ get_colonist_count() }} 等
        /// </summary>
        private static void RegisterCustomFunctions(ScriptObject scriptObject)
        {
            // 获取当前天气
            scriptObject.Import("get_weather", new Func<string>(() =>
            {
                try
                {
                    var map = Find.CurrentMap;
                    return map?.weatherManager?.curWeather?.label ?? "Unknown";
                }
                catch { return "Unknown"; }
            }));
            
            // 获取殖民者数量
            scriptObject.Import("get_colonist_count", new Func<int>(() =>
            {
                try
                {
                    var map = Find.CurrentMap;
                    return map?.mapPawns?.FreeColonistsCount ?? 0;
                }
                catch { return 0; }
            }));
            
            // 获取殖民地财富
            scriptObject.Import("get_total_wealth", new Func<float>(() =>
            {
                try
                {
                    var map = Find.CurrentMap;
                    return map?.wealthWatcher?.WealthTotal ?? 0f;
                }
                catch { return 0f; }
            }));
            
            // 获取当前季节
            scriptObject.Import("get_season", new Func<string>(() =>
            {
                try
                {
                    var map = Find.CurrentMap;
                    if (map == null) return "Unknown";
                    return GenDate.Season(Find.TickManager.TicksAbs, Find.WorldGrid.LongLatOf(map.Tile)).Label();
                }
                catch { return "Unknown"; }
            }));
            
            // 获取当前时间段（早上/下午/晚上/深夜）
            scriptObject.Import("get_time_of_day", new Func<string>(() =>
            {
                try
                {
                    int hour = GenLocalDate.HourOfDay(Find.CurrentMap);
                    if (hour >= 5 && hour < 12) return "morning";
                    if (hour >= 12 && hour < 17) return "afternoon";
                    if (hour >= 17 && hour < 21) return "evening";
                    return "night";
                }
                catch { return "day"; }
            }));
            
            // 翻译游戏内文本
            scriptObject.Import("translate", new Func<string, string>((key) =>
            {
                try
                {
                    return key.Translate();
                }
                catch { return key; }
            }));
            
            // 获取游戏内天数
            scriptObject.Import("get_game_days", new Func<int>(() =>
            {
                try
                {
                    return GenDate.DaysPassed;
                }
                catch { return 0; }
            }));
            
            // 获取当前故事讲述者名称
            scriptObject.Import("get_storyteller", new Func<string>(() =>
            {
                try
                {
                    return Find.Storyteller?.def?.label ?? "Unknown";
                }
                catch { return "Unknown"; }
            }));
            
            // 获取难度等级
            scriptObject.Import("get_difficulty", new Func<string>(() =>
            {
                try
                {
                    return Find.Storyteller?.difficultyDef?.label ?? "Unknown";
                }
                catch { return "Unknown"; }
            }));
            
            // 检查是否有敌人袭击
            scriptObject.Import("is_under_attack", new Func<bool>(() =>
            {
                try
                {
                    var map = Find.CurrentMap;
                    if (map == null) return false;
                    return map.attackTargetsCache.TargetsHostileToColony.Any();
                }
                catch { return false; }
            }));
            
            // 获取活着的殖民者名单（逗号分隔）
            scriptObject.Import("get_colonist_names", new Func<string>(() =>
            {
                try
                {
                    var map = Find.CurrentMap;
                    if (map == null) return "";
                    var names = map.mapPawns.FreeColonists.Select(p => p.Name?.ToStringShort ?? "Unknown");
                    return string.Join(", ", names);
                }
                catch { return ""; }
            }));
        }
        
        /// <summary>
        /// 渲染 System Prompt（便捷方法，自动拼接 Scriban 后缀）
        /// </summary>
        /// <param name="promptType">提示词类型："Master" 或 "EventDirector"</param>
        /// <param name="context">数据上下文</param>
        /// <returns>渲染后的 System Prompt</returns>
        public static string RenderSystemPrompt(string promptType, PromptContext context)
        {
            string templateName = $"SystemPrompt_{promptType}_Scriban";
            return Render(templateName, context);
        }
    }
}
