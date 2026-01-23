using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Verse;
using RimWorld;
using TheSecondSeat.Monitoring;

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
            Log.Message(string.Format("[AnalyzeTool] ExecuteAsync called with parameters: {0}", string.Join(", ", parameters.Keys)));
            
            var tcs = new TaskCompletionSource<ToolResult>();

            // ⭐ 修复线程安全：切换到主线程捕获快照
            Verse.LongEventHandler.ExecuteWhenFinished(() =>
            {
                try
                {
                    var snapshot = GameStateSnapshotUtility.CaptureSnapshotSafe();
                    
                    var analysis = new Dictionary<string, object>
                    {
                        ["colonist_count"] = snapshot.colonists?.Count ?? 0,
                        ["wealth"] = snapshot.colony?.wealth ?? 0,
                        ["food_level"] = snapshot.resources?.food ?? 0,
                        ["mood_average"] = snapshot.colonists != null && snapshot.colonists.Any()
                            ? snapshot.colonists.Average(c => c.mood)
                            : 50,
                        ["raid_active"] = snapshot.threats?.raidActive ?? false,
                        ["raid_strength"] = snapshot.threats?.raidStrength ?? 0
                    };
                    
                    tcs.SetResult(new ToolResult
                    {
                        Success = true,
                        Data = analysis
                    });
                }
                catch (Exception ex)
                {
                    tcs.SetResult(new ToolResult { Success = false, Error = ex.Message });
                }
            });

            // 等待主线程执行完成（带超时保护）
            var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(5000));
            
            if (completedTask == tcs.Task)
            {
                return await tcs.Task;
            }
            else
            {
                return new ToolResult { Success = false, Error = "Analyze timeout" };
            }
        }
    }
}
