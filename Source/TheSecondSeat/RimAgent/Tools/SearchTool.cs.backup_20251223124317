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
            try
            {
                if (!parameters.TryGetValue("query", out var queryObj))
                {
                    return new ToolResult { Success = false, Error = "Missing parameter: query" };
                }
                
                string query = queryObj.ToString().ToLower();
                var results = new List<string>();
                
                // 搜索殖民者
                var pawns = Find.CurrentMap?.mapPawns.FreeColonists;
                if (pawns != null)
                {
                    var matchedPawns = pawns.Where(p => p.Name.ToStringShort.ToLower().Contains(query));
                    results.AddRange(matchedPawns.Select(p => $"殖民者: {p.Name.ToStringShort}"));
                }
                
                // 搜索物品
                var things = Find.CurrentMap?.listerThings.AllThings;
                if (things != null)
                {
                    var matchedThings = things.Where(t => t.Label.ToLower().Contains(query)).Take(10);
                    results.AddRange(matchedThings.Select(t => $"物品: {t.Label}"));
                }
                
                return new ToolResult
                {
                    Success = true,
                    Data = results.Count > 0 ? string.Join(", ", results) : "未找到匹配项"
                };
            }
            catch (Exception ex)
            {
                return new ToolResult { Success = false, Error = ex.Message };
            }
        }
    }
}