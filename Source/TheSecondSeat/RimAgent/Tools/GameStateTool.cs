using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TheSecondSeat.Monitoring;
using Verse;

namespace TheSecondSeat.RimAgent.Tools
{
    /// <summary>
    /// ⭐ v2.9.8: 游戏状态查询工具
    /// 供叙事者按需获取游戏状态（Pull 模式）
    /// 
    /// 用法：
    /// [ACTION]: get_game_state(scope="full")
    /// [ACTION]: get_game_state(scope="colonists")
    /// [ACTION]: get_game_state(scope="threats")
    /// [ACTION]: get_game_state(scope="resources")
    /// </summary>
    public class GameStateTool : ITool
    {
        public string Name => "get_game_state";
        
        public string Description => 
            "获取当前游戏状态。参数 scope: 'full'(完整状态), 'colonists'(殖民者状态), " +
            "'threats'(威胁信息), 'resources'(资源概况), 'summary'(简要摘要)。" +
            "建议先用 'summary' 快速了解情况，需要详情时再用具体 scope。";
        
        /// <summary>
        /// 缓存的游戏状态快照（由 NarratorUpdateService 在调用前设置）
        /// </summary>
        public static GameStateSnapshot CachedSnapshot { get; set; }
        
        public async Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
        {
            return await Task.Run(() =>
            {
                try
                {
                    // 获取 scope 参数
                    string scope = "summary";  // 默认使用简要摘要，减少 Token
                    if (parameters.TryGetValue("scope", out var scopeObj))
                    {
                        scope = scopeObj?.ToString()?.ToLower() ?? "summary";
                    }
                    
                    // 如果没有缓存的快照
                    if (CachedSnapshot == null)
                    {
                        Log.Warning("[GameStateTool] No cached snapshot available. Game state may be incomplete.");
                        return new ToolResult
                        {
                            Success = false,
                            Error = "游戏状态快照不可用，请稍后重试"
                        };
                    }
                    
                    // 根据 scope 返回不同级别的信息
                    string result = scope switch
                    {
                        "full" => GetFullState(),
                        "colonists" => GetColonistsState(),
                        "threats" => GetThreatsState(),
                        "resources" => GetResourcesState(),
                        "summary" => GetSummaryState(),
                        _ => GetSummaryState()
                    };
                    
                    return new ToolResult
                    {
                        Success = true,
                        Data = result
                    };
                }
                catch (System.Exception ex)
                {
                    Log.Error($"[GameStateTool] Error: {ex.Message}");
                    return new ToolResult
                    {
                        Success = false,
                        Error = ex.Message
                    };
                }
            });
        }
        
        /// <summary>
        /// 获取完整游戏状态（Token 较多，慎用）
        /// </summary>
        private string GetFullState()
        {
            if (CachedSnapshot == null) return "无可用状态";
            
            // 使用现有的序列化方法，但限制长度
            string json = GameStateSnapshotUtility.SnapshotToJson(CachedSnapshot);
            
            // 限制最大长度为 6000 字符（约 2000 tokens）
            const int maxLength = 6000;
            if (json.Length > maxLength)
            {
                json = json.Substring(0, maxLength) + "\n[...状态已截断，如需更多信息请使用具体 scope...]";
            }
            
            return json;
        }
        
        /// <summary>
        /// 获取殖民者状态
        /// </summary>
        private string GetColonistsState()
        {
            if (CachedSnapshot == null) return "无可用状态";
            
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("## 殖民者状态");
            sb.AppendLine($"殖民者数量: {CachedSnapshot.colonists?.Count ?? 0}");
            
            if (CachedSnapshot.colonists != null)
            {
                foreach (var colonist in CachedSnapshot.colonists)
                {
                    sb.AppendLine($"- {colonist.name}: 心情={colonist.mood}%, 健康={colonist.health}%");
                    if (!string.IsNullOrEmpty(colonist.currentJob))
                    {
                        sb.AppendLine($"  当前工作: {colonist.currentJob}");
                    }
                    if (colonist.majorInjuries?.Count > 0)
                    {
                        sb.AppendLine($"  伤病: {string.Join(", ", colonist.majorInjuries)}");
                    }
                }
            }
            
            return sb.ToString();
        }
        
        /// <summary>
        /// 获取威胁信息
        /// </summary>
        private string GetThreatsState()
        {
            if (CachedSnapshot == null) return "无可用状态";
            
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("## 威胁状态");
            
            if (CachedSnapshot.threats != null)
            {
                if (CachedSnapshot.threats.raidActive)
                {
                    sb.AppendLine($"⚠️ 袭击进行中！敌人数量: {CachedSnapshot.threats.raidStrength}");
                }
                else
                {
                    sb.AppendLine("当前无活跃威胁");
                }
                
                if (!string.IsNullOrEmpty(CachedSnapshot.threats.currentEvent))
                {
                    sb.AppendLine($"当前事件: {CachedSnapshot.threats.currentEvent}");
                }
            }
            
            // 威胁实体详情
            if (CachedSnapshot.threatEntities?.Count > 0)
            {
                sb.AppendLine($"\n敌对实体详情 ({CachedSnapshot.threatEntities.Count} 个):");
                foreach (var threat in CachedSnapshot.threatEntities.Take(10))
                {
                    sb.AppendLine($"- {threat.name} [{threat.threatType}] @ {threat.location?.direction}");
                    if (threat.isInCombat)
                    {
                        sb.AppendLine($"  正在战斗！");
                    }
                }
            }
            
            return sb.ToString();
        }
        
        /// <summary>
        /// 获取资源概况
        /// </summary>
        private string GetResourcesState()
        {
            if (CachedSnapshot == null) return "无可用状态";
            
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("## 资源概况");
            
            if (CachedSnapshot.resources != null)
            {
                sb.AppendLine($"食物: {CachedSnapshot.resources.food}");
                sb.AppendLine($"木材: {CachedSnapshot.resources.wood}");
                sb.AppendLine($"钢铁: {CachedSnapshot.resources.steel}");
                sb.AppendLine($"药品: {CachedSnapshot.resources.medicine}");
            }
            
            if (CachedSnapshot.colony != null)
            {
                sb.AppendLine($"\n殖民地财富: {CachedSnapshot.colony.wealth}");
            }
            
            return sb.ToString();
        }
        
        /// <summary>
        /// 获取简要摘要（推荐默认使用，Token 最少）
        /// </summary>
        private string GetSummaryState()
        {
            if (CachedSnapshot == null) return "无可用状态";
            
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("## 殖民地概况");
            sb.AppendLine($"- 殖民者: {CachedSnapshot.colonists?.Count ?? 0} 人");
            sb.AppendLine($"- 天数: 第 {CachedSnapshot.colony?.daysPassed ?? 0} 天");
            sb.AppendLine($"- 生态群落: {CachedSnapshot.colony?.biome ?? "未知"}");
            sb.AppendLine($"- 殖民地财富: {CachedSnapshot.colony?.wealth ?? 0}");
            
            // 资源摘要
            if (CachedSnapshot.resources != null)
            {
                sb.AppendLine($"- 食物储备: {CachedSnapshot.resources.food}");
                sb.AppendLine($"- 钢铁: {CachedSnapshot.resources.steel}");
            }
            
            // 威胁摘要
            if (CachedSnapshot.threats?.raidActive == true)
            {
                sb.AppendLine($"- ⚠️ 正在遭受袭击！敌人数量: {CachedSnapshot.threats.raidStrength}");
            }
            else
            {
                sb.AppendLine("- ✅ 无活跃威胁");
            }
            
            // 天气
            if (CachedSnapshot.weather != null)
            {
                sb.AppendLine($"- 天气: {CachedSnapshot.weather.current}, 温度: {CachedSnapshot.weather.temperature:F0}°C");
            }
            
            // 心情摘要
            if (CachedSnapshot.colonists?.Count > 0)
            {
                double avgMood = CachedSnapshot.colonists.Average(c => c.mood);
                string moodDesc = avgMood > 70 ? "良好" : avgMood > 40 ? "一般" : "低落";
                sb.AppendLine($"- 平均心情: {moodDesc} ({avgMood:F0}%)");
            }
            
            sb.AppendLine("\n(如需详情，可使用 scope='colonists'/'threats'/'resources'/'full')");
            
            return sb.ToString();
        }
    }
}
