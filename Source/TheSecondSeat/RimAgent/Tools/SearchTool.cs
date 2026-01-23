using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Verse;
using RimWorld;

namespace TheSecondSeat.RimAgent.Tools
{
    /// <summary>
    /// 搜索工具 - 搜索游戏数据
    /// </summary>
    public class SearchTool : ITool
    {
        public string Name => "search";
        public string Description => "搜索游戏中的 Pawn、物品、建筑等数据";
        
        public async Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
        {
            Log.Message(string.Format("[SearchTool] ExecuteAsync called with parameters: {0}", string.Join(", ", parameters.Keys)));
            try
            {
                if (!parameters.TryGetValue("query", out var queryObj))
                {
                    return new ToolResult { Success = false, Error = "Missing parameter: query" };
                }
                
                string query = queryObj.ToString().ToLower();
                
                // ✅ 修复：在主线程捕获游戏数据
                List<string> pawnNames = null;
                List<string> thingLabels = null;
                
                // 使用 TaskCompletionSource 在主线程执行数据捕获
                var tcs = new TaskCompletionSource<bool>();
                
                Verse.LongEventHandler.ExecuteWhenFinished(() =>
                {
                    try
                    {
                        var map = Find.CurrentMap;
                        if (map != null)
                        {
                            // 捕获殖民者名称
                            var pawns = map.mapPawns?.FreeColonists;
                            if (pawns != null)
                            {
                                pawnNames = pawns.Select(p => p.Name.ToStringShort).ToList();
                            }
                            
                            // 捕获物品标签
                            var things = map.listerThings?.AllThings;
                            if (things != null)
                            {
                                thingLabels = things.Select(t => t.Label).ToList();
                            }
                        }
                        
                        tcs.SetResult(true);
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"[SearchTool] Error capturing game data: {ex.Message}");
                        tcs.SetException(ex);
                    }
                });
                
                // 等待主线程数据捕获完成
                await tcs.Task;
                
                // ✅ 现在在后台线程处理捕获的数据（线程安全）
                var results = new List<string>();
                
                // 搜索殖民者
                if (pawnNames != null)
                {
                    var matchedPawns = pawnNames.Where(name => name.ToLower().Contains(query));
                    results.AddRange(matchedPawns.Select(name => $"殖民者: {name}"));
                }
                
                // 搜索物品
                if (thingLabels != null)
                {
                    var matchedThings = thingLabels.Where(label => label.ToLower().Contains(query)).Take(10);
                    results.AddRange(matchedThings.Select(label => $"物品: {label}"));
                }
                
                return new ToolResult
                {
                    Success = true,
                    Data = results.Count > 0 ? string.Join(", ", results) : "未找到匹配项"
                };
            }
            catch (Exception ex)
            {
                Log.Error($"[SearchTool] ExecuteAsync failed: {ex.Message}");
                return new ToolResult { Success = false, Error = ex.Message };
            }
        }
    }
}