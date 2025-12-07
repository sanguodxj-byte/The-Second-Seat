using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Verse;

namespace TheSecondSeat.NaturalLanguage
{
    /// <summary>
    /// 高级命令参数
    /// </summary>
    [Serializable]
    public class AdvancedCommandParams
    {
        public string? target { get; set; }          // 目标类型（如 "Blighted", "All", "Damaged"）
        public string? scope { get; set; }           // 范围（"Map", "Home", "Selected"）
        public Dictionary<string, object>? filters { get; set; }  // 过滤条件
        public int? count { get; set; }              // 数量限制
        public bool? priority { get; set; }          // 是否优先
    }

    /// <summary>
    /// 自然语言命令解析结果
    /// </summary>
    public class ParsedCommand
    {
        public string action = "";
        public AdvancedCommandParams parameters = new AdvancedCommandParams();
        public float confidence = 0f;  // 解析置信度
        public string originalQuery = "";
    }

    /// <summary>
    /// 自然语言命令解析器 - 增强版
    /// </summary>
    public static class NaturalLanguageParser
    {
        // 命令映射表
        private static readonly Dictionary<string, string[]> ActionKeywords = new Dictionary<string, string[]>
        {
            { "BatchHarvest", new[] { "收获", "收割", "采集", "摘", "harvest", "cut" } },
            { "BatchEquip", new[] { "装备", "武装", "穿戴", "equip", "arm" } },
            { "BatchCapture", new[] { "俘虏", "捕获", "抓捕", "capture", "arrest", "imprison" } },
            { "BatchMine", new[] { "采矿", "挖矿", "开采", "mine", "dig", "excavate" } },
            { "PriorityRepair", new[] { "修复", "维修", "修理", "repair", "fix" } },
            { "EmergencyRetreat", new[] { "撤退", "逃跑", "征召", "retreat", "draft" } },
            { "ChangePolicy", new[] { "政策", "限制", "规则", "policy", "restriction" } },
            { "DesignatePlantCut", new[] { "砍树", "砍植物", "清理植物", "cut plant", "chop" } },
            { "DesignateConstruction", new[] { "建造", "建筑", "construct", "build" } },
            { "AssignWork", new[] { "分配工作", "指派", "assign work" } },
            { "ForbidItems", new[] { "禁止", "锁定", "forbid" } },
            { "AllowItems", new[] { "允许", "解锁", "allow", "permit" } }
        };

        // 目标类型关键词
        private static readonly Dictionary<string, string[]> TargetKeywords = new Dictionary<string, string[]>
        {
            { "Blighted", new[] { "枯萎", "病害", "坏掉", "blighted" } },
            { "All", new[] { "所有", "全部", "全图", "all", "everything" } },
            { "Damaged", new[] { "受损", "破损", "损坏", "damaged", "broken" } },
            { "Mature", new[] { "成熟", "可收获", "mature", "ready" } },
            { "Weapon", new[] { "武器", "weapon", "gun" } },
            { "Armor", new[] { "护甲", "盔甲", "armor" } }
        };

        /// <summary>
        /// 解析自然语言命令
        /// </summary>
        public static ParsedCommand? Parse(string naturalLanguageQuery)
        {
            if (string.IsNullOrWhiteSpace(naturalLanguageQuery))
                return null;

            string query = naturalLanguageQuery.ToLower().Trim();

            // 1. 识别动作
            string? action = IdentifyAction(query);
            if (action == null)
            {
                Log.Warning($"[NLParser] 无法识别动作: {query}");
                return null;
            }

            // 2. 识别目标
            string? target = IdentifyTarget(query);

            // 3. 识别范围
            string? scope = IdentifyScope(query);

            // 4. 提取数量
            int? count = ExtractCount(query);

            // 5. 判断是否优先
            bool? priority = query.Contains("优先") || query.Contains("紧急") || query.Contains("priority");

            // 6. 计算置信度
            float confidence = CalculateConfidence(query, action, target, scope);

            var parsed = new ParsedCommand
            {
                action = action,
                parameters = new AdvancedCommandParams
                {
                    target = target,
                    scope = scope ?? "Map",
                    count = count,
                    priority = priority
                },
                confidence = confidence,
                originalQuery = naturalLanguageQuery
            };

            Log.Message($"[NLParser] 解析结果: Action={action}, Target={target}, Scope={scope}, Confidence={confidence:P0}");

            return parsed;
        }

        private static string? IdentifyAction(string query)
        {
            foreach (var kvp in ActionKeywords)
            {
                if (kvp.Value.Any(keyword => query.Contains(keyword)))
                {
                    return kvp.Key;
                }
            }
            return null;
        }

        private static string? IdentifyTarget(string query)
        {
            foreach (var kvp in TargetKeywords)
            {
                if (kvp.Value.Any(keyword => query.Contains(keyword)))
                {
                    return kvp.Key;
                }
            }
            return "All"; // 默认目标
        }

        private static string? IdentifyScope(string query)
        {
            if (query.Contains("地图") || query.Contains("全图") || query.Contains("map"))
                return "Map";
            if (query.Contains("家园") || query.Contains("殖民地") || query.Contains("home"))
                return "Home";
            if (query.Contains("选中") || query.Contains("当前") || query.Contains("selected"))
                return "Selected";
            
            return null;
        }

        private static int? ExtractCount(string query)
        {
            // 简单数字提取
            var words = query.Split(' ');
            foreach (var word in words)
            {
                if (int.TryParse(word, out int num))
                {
                    return num;
                }
            }

            // 中文数字识别
            if (query.Contains("十个")) return 10;
            if (query.Contains("五个")) return 5;
            
            return null;
        }

        private static float CalculateConfidence(string query, string? action, string? target, string? scope)
        {
            float confidence = 0.5f;

            if (action != null) confidence += 0.3f;
            if (target != null) confidence += 0.15f;
            if (scope != null) confidence += 0.05f;

            return Math.Min(1f, confidence);
        }

        /// <summary>
        /// 从LLM JSON响应中解析命令
        /// </summary>
        public static ParsedCommand? ParseFromLLMResponse(string llmJsonResponse)
        {
            try
            {
                var json = JObject.Parse(llmJsonResponse);
                var commandToken = json["command"];

                if (commandToken == null || commandToken.Type == JTokenType.Null)
                {
                    return null;
                }

                var command = commandToken.ToObject<LLM.LLMCommand>();
                if (command == null || string.IsNullOrEmpty(command.action))
                {
                    return null;
                }

                // 转换为ParsedCommand
                var parsed = new ParsedCommand
                {
                    action = command.action,
                    parameters = new AdvancedCommandParams
                    {
                        target = command.target,
                        scope = "Map"
                    },
                    confidence = 1f, // LLM解析的置信度默认为1
                    originalQuery = ""
                };

                // 尝试解析parameters
                if (command.parameters != null)
                {
                    var paramsJson = JsonConvert.SerializeObject(command.parameters);
                    var advancedParams = JsonConvert.DeserializeObject<AdvancedCommandParams>(paramsJson);
                    if (advancedParams != null)
                    {
                        parsed.parameters = advancedParams;
                    }
                }

                return parsed;
            }
            catch (Exception ex)
            {
                Log.Error($"[NLParser] 解析LLM响应失败: {ex.Message}");
                return null;
            }
        }
    }

    /// <summary>
    /// 命令执行建议生成器
    /// </summary>
    public static class CommandSuggestionGenerator
    {
        /// <summary>
        /// 根据游戏状态生成命令建议
        /// </summary>
        public static List<string> GenerateSuggestions(Observer.GameStateSnapshot gameState)
        {
            var suggestions = new List<string>();

            // 检查成熟作物
            // （需要访问实际的植物数据，这里仅作示意）
            if (gameState.colonists.Count > 0)
            {
                suggestions.Add("如果有成熟作物，可以说：'帮我收获所有成熟的作物'");
            }

            // 检查低资源
            if (gameState.resources.food < 100)
            {
                suggestions.Add("资源不足时可以说：'给我一些食物补给'");
            }

            // 检查受损建筑
            suggestions.Add("建筑受损时可以说：'优先修复所有受损的建筑'");

            // 检查威胁
            if (gameState.threats.raidActive)
            {
                suggestions.Add("遭遇袭击时可以说：'紧急武装所有殖民者'");
            }

            return suggestions;
        }
    }
}
