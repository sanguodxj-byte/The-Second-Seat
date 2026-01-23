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
    /// 空间查询工具 - 为叙事者提供空间感知能力
    /// v2.0.0: 新增工具，支持方位、距离、区域查询
    /// </summary>
    public class SpatialQueryTool : ITool
    {
        public string Name => "spatial_query";
        public string Description => @"查询殖民地空间布局和实体位置。
可用的查询类型（query_type）：
- 'overview': 获取殖民地空间概览（默认）
- 'colonists': 查询殖民者分布和位置
- 'buildings': 查询建筑分布和位置
- 'threats': 查询威胁实体位置
- 'direction': 查询特定方向的实体

可选的方向过滤（direction）：
North, South, East, West, Northeast, Northwest, Southeast, Southwest, Center

示例:
- 查询概览: { ""query_type"": ""overview"" }
- 查询北方殖民者: { ""query_type"": ""colonists"", ""direction"": ""North"" }
- 查询所有威胁: { ""query_type"": ""threats"" }";

        public async Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
        {
            Log.Message($"[SpatialQueryTool] ExecuteAsync called with {parameters.Count} parameters");
            
            var tcs = new TaskCompletionSource<ToolResult>();
            
            // 切换到主线程执行
            Verse.LongEventHandler.ExecuteWhenFinished(() =>
            {
                try
                {
                    var queryType = GetParameter<string>(parameters, "query_type", "overview");
                    var direction = GetParameter<string>(parameters, "direction", null);
                    
                    var result = ExecuteQuery(queryType, direction);
                    tcs.SetResult(result);
                }
                catch (Exception ex)
                {
                    Log.Error($"[SpatialQueryTool] Error: {ex.Message}");
                    tcs.SetResult(new ToolResult 
                    { 
                        Success = false, 
                        Error = ex.Message 
                    });
                }
            });
            
            // 等待执行完成（带超时）
            var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(5000));
            
            if (completedTask == tcs.Task)
            {
                return await tcs.Task;
            }
            else
            {
                return new ToolResult 
                { 
                    Success = false, 
                    Error = "Spatial query timeout after 5 seconds" 
                };
            }
        }
        
        /// <summary>
        /// 执行空间查询
        /// </summary>
        private ToolResult ExecuteQuery(string queryType, string? direction)
        {
            var map = Find.CurrentMap;
            if (map == null)
            {
                return new ToolResult 
                { 
                    Success = false, 
                    Error = "No active map" 
                };
            }
            
            // 获取完整快照
            var snapshot = GameStateSnapshotUtility.CaptureSnapshotSafe();
            
            var result = new Dictionary<string, object>();
            
            switch (queryType.ToLower())
            {
                case "overview":
                    result = BuildOverviewResult(snapshot, map);
                    break;
                    
                case "colonists":
                    result = BuildColonistsResult(snapshot, direction);
                    break;
                    
                case "buildings":
                    result = BuildBuildingsResult(snapshot, direction);
                    break;
                    
                case "threats":
                    result = BuildThreatsResult(snapshot, direction);
                    break;
                    
                case "direction":
                    if (string.IsNullOrEmpty(direction))
                    {
                        return new ToolResult
                        {
                            Success = false,
                            Error = "Direction parameter required for 'direction' query type"
                        };
                    }
                    result = BuildDirectionResult(snapshot, direction);
                    break;
                    
                default:
                    return new ToolResult
                    {
                        Success = false,
                        Error = $"Unknown query type: {queryType}. Valid types: overview, colonists, buildings, threats, direction"
                    };
            }
            
            return new ToolResult
            {
                Success = true,
                Data = result
            };
        }
        
        /// <summary>
        /// 构建概览结果
        /// </summary>
        private Dictionary<string, object> BuildOverviewResult(GameStateSnapshot snapshot, Map map)
        {
            var result = new Dictionary<string, object>
            {
                ["colony_center"] = new
                {
                    x = snapshot.colonyCenter.x,
                    z = snapshot.colonyCenter.z,
                    description = "The center of your colony (based on home area)"
                },
                ["total_colonists"] = snapshot.colonists.Count,
                ["total_buildings"] = snapshot.buildings.Count,
                ["active_threats"] = snapshot.threatEntities.Count,
                ["spatial_summary"] = snapshot.spatialSummary
            };
            
            // 添加方向分布摘要
            var colonistDistribution = snapshot.colonists
                .GroupBy(c => c.location.direction)
                .ToDictionary(g => g.Key, g => g.Count());
            
            var buildingDistribution = snapshot.buildings
                .GroupBy(b => b.location.direction)
                .ToDictionary(g => g.Key, g => g.Count());
            
            result["colonist_distribution"] = colonistDistribution;
            result["building_distribution"] = buildingDistribution;
            
            // 添加威胁警报
            if (snapshot.threatEntities.Any())
            {
                var threatDirections = snapshot.threatEntities
                    .GroupBy(t => t.location.direction)
                    .Select(g => new
                    {
                        direction = g.Key,
                        count = g.Count(),
                        distance = g.Min(t => t.location.distanceFromCenter),
                        threat_level = g.Max(t => t.threatLevel)
                    })
                    .OrderByDescending(x => x.threat_level)
                    .ToList();
                
                result["threat_alert"] = threatDirections;
            }
            
            return result;
        }
        
        /// <summary>
        /// 构建殖民者结果
        /// </summary>
        private Dictionary<string, object> BuildColonistsResult(GameStateSnapshot snapshot, string? direction)
        {
            var colonists = snapshot.colonists.AsEnumerable();
            
            // 如果指定了方向，进行过滤
            if (!string.IsNullOrEmpty(direction))
            {
                colonists = colonists.Where(c => 
                    c.location.direction.Equals(direction, StringComparison.OrdinalIgnoreCase));
            }
            
            var colonistList = colonists.Select(c => new
            {
                name = c.name,
                location = new
                {
                    direction = c.location.direction,
                    direction_cn = DirectionCalculator.GetDirectionChinese(c.location.direction),
                    distance = c.location.distanceFromCenter,
                    distance_level = c.location.distanceLevel,
                    zone = c.location.zone,
                    is_indoors = c.location.isIndoors,
                    coordinates = $"({c.location.x}, {c.location.z})"
                },
                status = new
                {
                    mood = c.mood,
                    health = c.health,
                    current_job = c.currentJob,
                    is_working = c.isWorking,
                    current_room = c.currentRoom
                }
            }).ToList();
            
            return new Dictionary<string, object>
            {
                ["filter_direction"] = direction ?? "All",
                ["count"] = colonistList.Count,
                ["colonists"] = colonistList
            };
        }
        
        /// <summary>
        /// 构建建筑结果
        /// </summary>
        private Dictionary<string, object> BuildBuildingsResult(GameStateSnapshot snapshot, string? direction)
        {
            var buildings = snapshot.buildings.AsEnumerable();
            
            if (!string.IsNullOrEmpty(direction))
            {
                buildings = buildings.Where(b => 
                    b.location.direction.Equals(direction, StringComparison.OrdinalIgnoreCase));
            }
            
            // 按类型分组
            var buildingsByType = buildings
                .GroupBy(b => b.type)
                .ToDictionary(g => g.Key, g => g.Select(b => new
                {
                    name = b.name,
                    defName = b.defName,
                    direction = b.location.direction,
                    distance = b.location.distanceFromCenter,
                    zone = b.location.zone,
                    is_operational = b.isOperational,
                    current_worker = b.currentWorker,
                    coordinates = $"({b.location.x}, {b.location.z})"
                }).ToList());
            
            return new Dictionary<string, object>
            {
                ["filter_direction"] = direction ?? "All",
                ["total_count"] = buildings.Count(),
                ["buildings_by_type"] = buildingsByType
            };
        }
        
        /// <summary>
        /// 构建威胁结果
        /// </summary>
        private Dictionary<string, object> BuildThreatsResult(GameStateSnapshot snapshot, string? direction)
        {
            var threats = snapshot.threatEntities.AsEnumerable();
            
            if (!string.IsNullOrEmpty(direction))
            {
                threats = threats.Where(t => 
                    t.location.direction.Equals(direction, StringComparison.OrdinalIgnoreCase));
            }
            
            var threatList = threats.Select(t => new
            {
                name = t.name,
                type = t.threatType,
                faction = t.faction,
                threat_level = t.threatLevel,
                weapon = t.weapon,
                is_in_combat = t.isInCombat,
                location = new
                {
                    direction = t.location.direction,
                    direction_cn = DirectionCalculator.GetDirectionChinese(t.location.direction),
                    distance = t.location.distanceFromCenter,
                    distance_level = t.location.distanceLevel,
                    coordinates = $"({t.location.x}, {t.location.z})"
                }
            })
            .OrderBy(t => t.location.distance) // 按距离排序，最近的优先
            .ToList();
            
            // 生成威胁摘要
            string threatSummary = "";
            if (threatList.Any())
            {
                var nearestThreat = threatList.First();
                var directionCounts = threatList
                    .GroupBy(t => t.location.direction)
                    .Select(g => $"{g.Count()} from {g.Key}")
                    .ToList();
                
                threatSummary = $"WARNING: {threatList.Count} hostile(s) detected! " +
                    $"Nearest: {nearestThreat.name} ({nearestThreat.type}) at {nearestThreat.location.distance} tiles to the {nearestThreat.location.direction}. " +
                    $"Distribution: {string.Join(", ", directionCounts)}.";
            }
            else
            {
                threatSummary = "No hostile threats detected. Colony is currently safe.";
            }
            
            return new Dictionary<string, object>
            {
                ["filter_direction"] = direction ?? "All",
                ["threat_count"] = threatList.Count,
                ["summary"] = threatSummary,
                ["threats"] = threatList
            };
        }
        
        /// <summary>
        /// 构建特定方向结果
        /// </summary>
        private Dictionary<string, object> BuildDirectionResult(GameStateSnapshot snapshot, string direction)
        {
            var colonistsInDirection = snapshot.colonists
                .Where(c => c.location.direction.Equals(direction, StringComparison.OrdinalIgnoreCase))
                .Select(c => new { name = c.name, job = c.currentJob, distance = c.location.distanceFromCenter })
                .ToList();
            
            var buildingsInDirection = snapshot.buildings
                .Where(b => b.location.direction.Equals(direction, StringComparison.OrdinalIgnoreCase))
                .Select(b => new { name = b.name, type = b.type, distance = b.location.distanceFromCenter })
                .ToList();
            
            var threatsInDirection = snapshot.threatEntities
                .Where(t => t.location.direction.Equals(direction, StringComparison.OrdinalIgnoreCase))
                .Select(t => new { name = t.name, type = t.threatType, distance = t.location.distanceFromCenter, threat_level = t.threatLevel })
                .ToList();
            
            // 生成自然语言描述
            var description = new System.Text.StringBuilder();
            description.AppendLine($"=== {DirectionCalculator.GetDirectionChinese(direction)} ({direction}) Area ===");
            
            if (colonistsInDirection.Any())
            {
                var names = string.Join(", ", colonistsInDirection.Select(c => c.name));
                description.AppendLine($"Colonists: {names}");
            }
            else
            {
                description.AppendLine("Colonists: None");
            }
            
            if (buildingsInDirection.Any())
            {
                var types = buildingsInDirection.GroupBy(b => b.type).Select(g => $"{g.Count()} {g.Key}");
                description.AppendLine($"Buildings: {string.Join(", ", types)}");
            }
            else
            {
                description.AppendLine("Buildings: None");
            }
            
            if (threatsInDirection.Any())
            {
                description.AppendLine($"⚠️ THREATS: {threatsInDirection.Count} hostile(s)!");
            }
            else
            {
                description.AppendLine("Threats: None (safe)");
            }
            
            return new Dictionary<string, object>
            {
                ["direction"] = direction,
                ["direction_cn"] = DirectionCalculator.GetDirectionChinese(direction),
                ["description"] = description.ToString(),
                ["colonists"] = colonistsInDirection,
                ["buildings"] = buildingsInDirection,
                ["threats"] = threatsInDirection
            };
        }
        
        /// <summary>
        /// 获取参数值
        /// </summary>
        private T GetParameter<T>(Dictionary<string, object> parameters, string key, T defaultValue)
        {
            if (parameters.TryGetValue(key, out var value))
            {
                if (value is T typedValue)
                    return typedValue;
                
                try
                {
                    return (T)Convert.ChangeType(value, typeof(T));
                }
                catch
                {
                    return defaultValue;
                }
            }
            return defaultValue;
        }
    }
}
