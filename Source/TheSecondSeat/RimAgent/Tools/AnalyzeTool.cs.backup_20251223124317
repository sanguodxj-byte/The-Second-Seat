using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Verse;
using RimWorld;
using TheSecondSeat.Observer;

namespace TheSecondSeat.RimAgent.Tools
{
    /// <summary>
    /// 分析工具 - 分析殖民地状态
    /// </summary>
    public class AnalyzeTool : ITool
    {
        public string Name => "analyze";
        public string Description => "分析殖民地当前状态（人口、资源、威胁等）";
        
        public async Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
        {
            try
            {
                var snapshot = GameStateObserver.CaptureSnapshotSafe();
                
                var analysis = new Dictionary<string, object>
                {
                    ["colonist_count"] = snapshot.colonists?.Count ?? 0,
                    ["wealth"] = snapshot.colony?.wealth ?? 0,
                    ["food_level"] = snapshot.resources?.food ?? 0,
                    ["mood_average"] = snapshot.colonists?.Average(c => c.mood) ?? 50,
                    ["raid_active"] = snapshot.threats?.raidActive ?? false,
                    ["raid_strength"] = snapshot.threats?.raidStrength ?? 0
                };
                
                return new ToolResult
                {
                    Success = true,
                    Data = analysis
                };
            }
            catch (Exception ex)
            {
                return new ToolResult { Success = false, Error = ex.Message };
            }
        }
    }
}