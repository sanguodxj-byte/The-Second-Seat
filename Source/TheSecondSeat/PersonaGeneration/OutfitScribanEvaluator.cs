using System;
using System.Collections.Generic;
using Scriban;
using Scriban.Runtime;
using Verse;

namespace TheSecondSeat.PersonaGeneration
{
    // =========================================================================
    // OutfitScribanEvaluator v1.0.0 - Scriban 表达式评估器
    // =========================================================================
    //
    // 功能说明：
    // 使用 Scriban 模板引擎评估服装切换条件表达式，
    // 支持在 XML 中编写灵活的条件逻辑。
    //
    // 可用变量：
    // - hour: 现实时间（小时，0-23）
    // - game_hour: 游戏内时间（小时，0-23）
    // - affinity: 好感度（-100 到 100）
    // - activity: 活动状态（字符串）
    // - weather: 天气（字符串）
    // - season: 季节（字符串）
    // - mood: 心情（0-100）
    // - energy: 精力（0-100）
    //
    // 表达式示例：
    // - {{ hour >= 22 || hour < 7 }}                    睡眠时间
    // - {{ affinity >= 60 && hour >= 20 }}              高好感度且晚上
    // - {{ activity == "Resting" || energy < 30 }}      休息中或精力不足
    // - {{ season == "Winter" && weather == "Snow" }}   冬季下雪
    //
    // =========================================================================

    /// <summary>
    /// Scriban 表达式评估器
    /// 用于评估服装切换的自定义条件
    /// </summary>
    public static class OutfitScribanEvaluator
    {
        private static Dictionary<string, Template> templateCache = new Dictionary<string, Template>();
        private const int MaxCacheSize = 100;

        /// <summary>
        /// 评估 Scriban 表达式
        /// </summary>
        /// <param name="expression">Scriban 表达式</param>
        /// <param name="context">评估上下文</param>
        /// <returns>表达式结果（布尔值）</returns>
        public static bool Evaluate(string expression, OutfitEvaluationContext context)
        {
            if (string.IsNullOrEmpty(expression)) return false;

            try
            {
                // 获取或解析模板
                var template = GetOrParseTemplate(expression);
                if (template == null || template.HasErrors)
                {
                    if (Prefs.DevMode)
                    {
                        Log.Warning($"[OutfitScribanEvaluator] Template parse error: {expression}");
                    }
                    return false;
                }

                // 创建脚本对象并设置变量
                var scriptObject = CreateScriptObject(context);
                var templateContext = new TemplateContext();
                templateContext.PushGlobal(scriptObject);

                // 渲染模板
                var result = template.Render(templateContext);
                
                // 解析结果
                return ParseResult(result);
            }
            catch (Exception ex)
            {
                if (Prefs.DevMode)
                {
                    Log.Warning($"[OutfitScribanEvaluator] Evaluation error: {ex.Message}");
                }
                return false;
            }
        }

        /// <summary>
        /// 评估 Scriban 表达式并返回字符串结果
        /// </summary>
        public static string EvaluateString(string expression, OutfitEvaluationContext context)
        {
            if (string.IsNullOrEmpty(expression)) return "";

            try
            {
                var template = GetOrParseTemplate(expression);
                if (template == null || template.HasErrors)
                {
                    return "";
                }

                var scriptObject = CreateScriptObject(context);
                var templateContext = new TemplateContext();
                templateContext.PushGlobal(scriptObject);

                return template.Render(templateContext).Trim();
            }
            catch (Exception ex)
            {
                if (Prefs.DevMode)
                {
                    Log.Warning($"[OutfitScribanEvaluator] String evaluation error: {ex.Message}");
                }
                return "";
            }
        }

        /// <summary>
        /// 获取或解析模板（带缓存）
        /// </summary>
        private static Template GetOrParseTemplate(string expression)
        {
            if (templateCache.TryGetValue(expression, out var cached))
            {
                return cached;
            }

            // 限制缓存大小
            if (templateCache.Count >= MaxCacheSize)
            {
                templateCache.Clear();
            }

            // 解析模板
            var template = Template.Parse(expression);
            templateCache[expression] = template;

            return template;
        }

        /// <summary>
        /// 创建 Scriban 脚本对象
        /// </summary>
        private static ScriptObject CreateScriptObject(OutfitEvaluationContext context)
        {
            var scriptObject = new ScriptObject();

            // 时间变量
            scriptObject["hour"] = context.RealHour;
            scriptObject["game_hour"] = context.GameHour;
            scriptObject["gamehour"] = context.GameHour; // 别名

            // 好感度
            scriptObject["affinity"] = context.Affinity;

            // 活动状态
            scriptObject["activity"] = context.Activity ?? "";

            // 天气和季节
            scriptObject["weather"] = context.Weather ?? "";
            scriptObject["season"] = context.Season ?? "";

            // 心情和精力
            scriptObject["mood"] = context.Mood;
            scriptObject["energy"] = context.Energy;

            // 人格信息
            scriptObject["persona"] = context.PersonaDefName ?? "";

            // 辅助函数
            scriptObject.Import("is_sleeping_time", new Func<bool>(() => 
                context.RealHour >= 22 || context.RealHour < 7));
            
            scriptObject.Import("is_work_time", new Func<bool>(() => 
                context.RealHour >= 9 && context.RealHour < 18));
            
            scriptObject.Import("is_meal_time", new Func<bool>(() => 
                (context.RealHour >= 7 && context.RealHour < 9) ||
                (context.RealHour >= 12 && context.RealHour < 14) ||
                (context.RealHour >= 18 && context.RealHour < 20)));

            scriptObject.Import("is_high_affinity", new Func<bool>(() => 
                context.Affinity >= 60));

            scriptObject.Import("is_low_energy", new Func<bool>(() => 
                context.Energy < 30));

            scriptObject.Import("is_resting", new Func<bool>(() => 
                context.Activity == "Resting" || context.Activity == "Sleeping"));

            return scriptObject;
        }

        /// <summary>
        /// 解析表达式结果为布尔值
        /// </summary>
        private static bool ParseResult(string result)
        {
            if (string.IsNullOrWhiteSpace(result)) return false;

            result = result.Trim().ToLower();

            // 直接匹配 true/false
            if (result == "true" || result == "1" || result == "yes")
                return true;
            if (result == "false" || result == "0" || result == "no" || result == "")
                return false;

            // 尝试解析为数字
            if (float.TryParse(result, out float num))
            {
                return num > 0;
            }

            // 非空字符串视为 true
            return !string.IsNullOrEmpty(result);
        }

        /// <summary>
        /// 清除模板缓存
        /// </summary>
        public static void ClearCache()
        {
            templateCache.Clear();
        }

        /// <summary>
        /// 验证表达式语法
        /// </summary>
        public static bool ValidateExpression(string expression, out string error)
        {
            error = null;

            if (string.IsNullOrEmpty(expression))
            {
                error = "Expression is empty";
                return false;
            }

            try
            {
                var template = Template.Parse(expression);
                if (template.HasErrors)
                {
                    error = string.Join("; ", template.Messages);
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }
    }
}
