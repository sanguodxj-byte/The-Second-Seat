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
                // ⭐ 增强逻辑：简单的否定词检查
                // 如果关键词前面紧跟着否定词，则忽略该匹配
                if (kvp.Value.Any(keyword =>
                {
                    int index = query.IndexOf(keyword);
                    if (index == -1) return false;

                    // 检查前面是否有否定词 (简单启发式)
                    string prefix = query.Substring(0, index).TrimEnd();
                    if (prefix.EndsWith("不") || prefix.EndsWith("不要") || prefix.EndsWith("别") ||
                        prefix.EndsWith("don't") || prefix.EndsWith("do not") || prefix.EndsWith("not"))
                    {
                        return false;
                    }
                    return true;
                }))
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
                // ⭐ 关键优化步骤 1: 提取 JSON 字符串
                string cleanJson = ExtractJsonString(llmJsonResponse);
                if (string.IsNullOrEmpty(cleanJson)) return null;

                // ⭐ 关键优化步骤 2: 尝试解析
                var json = JObject.Parse(cleanJson);
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
                    // 直接映射字典到 filters
                    parsed.parameters.filters = command.parameters;

                    // 手动提取已知字段
                    if (command.parameters.TryGetValue("scope", out var scopeObj) && scopeObj != null)
                    {
                        parsed.parameters.scope = scopeObj.ToString();
                    }

                    if (command.parameters.TryGetValue("target", out var targetObj) && targetObj != null)
                    {
                        parsed.parameters.target = targetObj.ToString();
                    }

                    if (command.parameters.TryGetValue("count", out var countObj) || command.parameters.TryGetValue("limit", out countObj))
                    {
                         if (countObj is int iVal) parsed.parameters.count = iVal;
                         else if (int.TryParse(countObj?.ToString(), out int pVal)) parsed.parameters.count = pVal;
                    }

                    if (command.parameters.TryGetValue("priority", out var priorityObj))
                    {
                        if (priorityObj is bool bVal) parsed.parameters.priority = bVal;
                        else if (bool.TryParse(priorityObj?.ToString(), out bool pbVal)) parsed.parameters.priority = pbVal;
                    }
                }

                return parsed;
            }
            catch (Exception ex)
            {
                // 记录警告而不是错误，因为对于弱模型，解析失败是常态，我们会回退到自然语言处理
                Log.Warning($"[NLParser] JSON 解析失败，尝试回退到 NLP 模式。原因: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// ⭐ 新增工具方法：从混合文本中提取 JSON
        /// </summary>
        private static string ExtractJsonString(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";

            // 1. 寻找第一个 '{' 和最后一个 '}'
            int startIndex = text.IndexOf('{');
            int endIndex = text.LastIndexOf('}');

            if (startIndex != -1 && endIndex != -1 && endIndex > startIndex)
            {
                // 截取中间部分
                string jsonCandidate = text.Substring(startIndex, endIndex - startIndex + 1);
                return jsonCandidate;
            }
            
            // 如果找不到大括号，可能模型完全忘了 JSON 格式
            return "";
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
        public static List<string> GenerateSuggestions(Monitoring.GameStateSnapshot gameState)
        {
            var suggestions = new List<string>();

            // 检查成熟作物
            // （需要访问实际的植物数据，这里仅作示意）
            if (gameState.colonists.Count > 0)
            {
                suggestions.Add("TSS_Suggest_Harvest".Translate());
            }

            // 检查低资源
            if (gameState.resources.food < 100)
            {
                suggestions.Add("TSS_Suggest_Food".Translate());
            }

            // 检查受损建筑
            suggestions.Add("TSS_Suggest_Repair".Translate());

            // 检查威胁
            if (gameState.threats.raidActive)
            {
                suggestions.Add("TSS_Suggest_Defense".Translate());
            }

            return suggestions;
        }
    }
}
