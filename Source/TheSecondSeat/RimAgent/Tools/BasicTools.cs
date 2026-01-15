using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using RimWorld;
using Verse;
using TheSecondSeat.Monitoring;

namespace TheSecondSeat.RimAgent.Tools
{
    /// <summary>
    /// 获取殖民地状态工具
    /// </summary>
    public class GetColonyStateTool : ITool
    {
        public string Name => "get_colony_state";
        public string Description => "获取殖民地基本状态（殖民者数量、心情、战斗状态、食物）";
        
        public Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
        {
            try
            {
                string result = "";
                var observer = Current.Game?.GetComponent<GameStateObserver>();
                if (observer != null)
                {
                    result = observer.GetCompactStateJson();
                }
                else
                {
                    // 降级：直接查询
                    var map = Find.CurrentMap;
                    if (map == null) result = "{\"error\":\"no_map\"}";
                    else
                    {
                        int colonists = map.mapPawns.FreeColonistsSpawnedCount;
                        result = $"{{\"colonists\":{colonists}}}";
                    }
                }
                return Task.FromResult(new ToolResult { Success = true, Data = result });
            }
            catch (Exception ex)
            {
                return Task.FromResult(new ToolResult { Success = false, Error = ex.Message });
            }
        }
    }
    
    /// <summary>
    /// 获取库存工具
    /// </summary>
    public class GetInventoryTool : ITool
    {
        public string Name => "get_inventory";
        public string Description => "获取关键物资库存（食物、药品、钢铁等）";
        
        public Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
        {
            try
            {
                var map = Find.CurrentMap;
                if (map == null) return Task.FromResult(new ToolResult { Success = false, Error = "no_map" });
                
                // 简化查询，只返回关键数据
                int food = map.resourceCounter.GetCount(RimWorld.ThingDefOf.MealSimple) +
                           map.resourceCounter.GetCount(RimWorld.ThingDefOf.MealFine);
                int medicine = map.resourceCounter.GetCount(RimWorld.ThingDefOf.MedicineHerbal) +
                               map.resourceCounter.GetCount(RimWorld.ThingDefOf.MedicineIndustrial);
                int steel = map.resourceCounter.GetCount(RimWorld.ThingDefOf.Steel);
                
                string json = $"{{\"food\":{food},\"medicine\":{medicine},\"steel\":{steel}}}";
                return Task.FromResult(new ToolResult { Success = true, Data = json });
            }
            catch (Exception ex)
            {
                return Task.FromResult(new ToolResult { Success = false, Error = ex.Message });
            }
        }
    }
    
    /// <summary>
    /// 获取殖民者列表工具
    /// </summary>
    public class GetColonistsTool : ITool
    {
        public string Name => "get_colonists";
        public string Description => "获取殖民者列表及状态";
        
        public Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
        {
            try
            {
                var map = Find.CurrentMap;
                if (map == null) return Task.FromResult(new ToolResult { Success = true, Data = "[]" });
                
                var sb = new System.Text.StringBuilder("[");
                bool first = true;
                
                foreach (var pawn in map.mapPawns.FreeColonistsSpawned)
                {
                    if (!first) sb.Append(",");
                    first = false;
                    
                    float mood = pawn.needs?.mood?.CurLevelPercentage ?? 0;
                    float health = pawn.health?.summaryHealth?.SummaryHealthPercent ?? 1;
                    string job = pawn.CurJob?.def?.defName ?? "Idle";
                    
                    sb.Append($"{{\"name\":\"{pawn.LabelShort}\",\"mood\":{mood:F2},\"health\":{health:F2},\"job\":\"{job}\"}}");
                }
                
                sb.Append("]");
                return Task.FromResult(new ToolResult { Success = true, Data = sb.ToString() });
            }
            catch (Exception ex)
            {
                return Task.FromResult(new ToolResult { Success = false, Error = ex.Message });
            }
        }
    }
    
    /// <summary>
    /// 检查威胁工具
    /// </summary>
    public class CheckThreatsTool : ITool
    {
        public string Name => "check_threats";
        public string Description => "检查当前威胁（敌人、火灾等）";
        
        public Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
        {
            try
            {
                var map = Find.CurrentMap;
                if (map == null) return Task.FromResult(new ToolResult { Success = true, Data = "{\"threats\":[]}" });
                
                var threats = new List<string>();
                
                // 检查敌人
                int enemies = 0;
                foreach (var p in map.mapPawns.AllPawnsSpawned)
                {
                    if (p.HostileTo(Faction.OfPlayer)) enemies++;
                }
                if (enemies > 0)
                {
                    threats.Add($"敌人:{enemies}");
                }
                
                // 检查火灾
                int fires = map.listerThings.ThingsOfDef(RimWorld.ThingDefOf.Fire).Count;
                if (fires > 0)
                {
                    threats.Add($"火灾:{fires}");
                }
                
                string json = $"{{\"active\":{(threats.Count > 0 ? "true" : "false")},\"threats\":[\"{string.Join("\",\"", threats)}\"]}}";
                return Task.FromResult(new ToolResult { Success = true, Data = json });
            }
            catch (Exception ex)
            {
                return Task.FromResult(new ToolResult { Success = false, Error = ex.Message });
            }
        }
    }
}
