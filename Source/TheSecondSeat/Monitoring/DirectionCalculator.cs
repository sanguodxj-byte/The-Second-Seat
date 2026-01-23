using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using RimWorld;

namespace TheSecondSeat.Monitoring
{
    /// <summary>
    /// 方位计算工具类 - 提供空间方位计算功能
    /// v2.0.0: 空间感知系统核心计算组件
    /// </summary>
    public static class DirectionCalculator
    {
        /// <summary>
        /// 8个方向的枚举
        /// </summary>
        public enum CardinalDirection
        {
            Center,
            North,
            South,
            East,
            West,
            Northeast,
            Northwest,
            Southeast,
            Southwest
        }
        
        /// <summary>
        /// 计算从点A到点B的方向（8方向）
        /// </summary>
        /// <param name="from">起始点（通常是殖民地中心）</param>
        /// <param name="to">目标点</param>
        /// <returns>方向字符串</returns>
        public static string GetDirection(IntVec3 from, IntVec3 to)
        {
            int dx = to.x - from.x;
            int dz = to.z - from.z;
            
            // 如果距离很近，认为是中心
            if (Math.Abs(dx) < 5 && Math.Abs(dz) < 5)
                return "Center";
            
            // 计算角度（弧度转度数）
            // 注意：RimWorld的坐标系中，z轴向上是北，x轴向右是东
            double angle = Math.Atan2(dz, dx) * 180 / Math.PI;
            
            // 根据角度判断方向（8等分，每45度一个方向）
            // East = 0°, North = 90°, West = 180°/-180°, South = -90°
            if (angle >= -22.5 && angle < 22.5) return "East";
            if (angle >= 22.5 && angle < 67.5) return "Northeast";
            if (angle >= 67.5 && angle < 112.5) return "North";
            if (angle >= 112.5 && angle < 157.5) return "Northwest";
            if (angle >= 157.5 || angle < -157.5) return "West";
            if (angle >= -157.5 && angle < -112.5) return "Southwest";
            if (angle >= -112.5 && angle < -67.5) return "South";
            if (angle >= -67.5 && angle < -22.5) return "Southeast";
            
            return "Unknown";
        }
        
        /// <summary>
        /// 获取方向枚举
        /// </summary>
        public static CardinalDirection GetDirectionEnum(IntVec3 from, IntVec3 to)
        {
            string dir = GetDirection(from, to);
            return Enum.TryParse<CardinalDirection>(dir, out var result) ? result : CardinalDirection.Center;
        }
        
        /// <summary>
        /// 计算两点之间的距离（格子数）
        /// </summary>
        public static int GetDistance(IntVec3 from, IntVec3 to)
        {
            return (int)Math.Sqrt(
                Math.Pow(to.x - from.x, 2) + 
                Math.Pow(to.z - from.z, 2)
            );
        }
        
        /// <summary>
        /// 计算距离等级
        /// </summary>
        /// <param name="distance">格子距离</param>
        /// <returns>距离等级字符串</returns>
        public static string GetDistanceLevel(int distance)
        {
            if (distance < 10) return "VeryClose";
            if (distance < 30) return "Close";
            if (distance < 60) return "Medium";
            if (distance < 100) return "Far";
            return "VeryFar";
        }
        
        /// <summary>
        /// 计算距离等级（带自定义阈值）
        /// </summary>
        public static string GetDistanceLevel(int distance, int veryCloseThreshold, int closeThreshold, int mediumThreshold, int farThreshold)
        {
            if (distance < veryCloseThreshold) return "VeryClose";
            if (distance < closeThreshold) return "Close";
            if (distance < mediumThreshold) return "Medium";
            if (distance < farThreshold) return "Far";
            return "VeryFar";
        }
        
        /// <summary>
        /// 计算殖民地中心（所有殖民者的平均位置）
        /// </summary>
        /// <param name="colonists">殖民者列表</param>
        /// <returns>殖民地中心坐标</returns>
        public static IntVec3 CalculateColonyCenter(IEnumerable<Pawn> colonists)
        {
            var colonistList = colonists.Where(p => p != null && p.Spawned && !p.Dead).ToList();
            
            if (!colonistList.Any())
                return IntVec3.Zero;
            
            int avgX = (int)colonistList.Average(p => p.Position.x);
            int avgZ = (int)colonistList.Average(p => p.Position.z);
            
            return new IntVec3(avgX, 0, avgZ);
        }
        
        /// <summary>
        /// 计算殖民地中心（基于家园区）
        /// </summary>
        public static IntVec3 CalculateColonyCenterFromHomeArea(Map map)
        {
            if (map == null) return IntVec3.Zero;
            
            var homeArea = map.areaManager?.Home;
            if (homeArea == null) return CalculateColonyCenterFromBuildings(map);
            
            int totalX = 0, totalZ = 0, count = 0;
            
            foreach (var cell in homeArea.ActiveCells)
            {
                totalX += cell.x;
                totalZ += cell.z;
                count++;
            }
            
            if (count == 0) return CalculateColonyCenterFromBuildings(map);
            
            return new IntVec3(totalX / count, 0, totalZ / count);
        }
        
        /// <summary>
        /// 计算殖民地中心（基于建筑）
        /// </summary>
        public static IntVec3 CalculateColonyCenterFromBuildings(Map map)
        {
            if (map?.listerBuildings?.allBuildingsColonist == null)
                return IntVec3.Zero;
            
            var buildings = map.listerBuildings.allBuildingsColonist.ToList();
            if (!buildings.Any()) return IntVec3.Zero;
            
            int avgX = (int)buildings.Average(b => b.Position.x);
            int avgZ = (int)buildings.Average(b => b.Position.z);
            
            return new IntVec3(avgX, 0, avgZ);
        }
        
        /// <summary>
        /// 获取完整的空间信息
        /// </summary>
        public static SpatialInfo GetSpatialInfo(IntVec3 position, IntVec3 colonyCenter, Map map)
        {
            var info = new SpatialInfo
            {
                x = position.x,
                z = position.z,
                direction = GetDirection(colonyCenter, position),
                distanceFromCenter = GetDistance(colonyCenter, position)
            };
            
            info.distanceLevel = GetDistanceLevel(info.distanceFromCenter);
            info.zone = GetZoneName(map, position);
            info.isInHomeArea = IsInHomeArea(map, position);
            info.isIndoors = IsIndoors(map, position);
            
            return info;
        }
        
        /// <summary>
        /// 获取区域名称
        /// </summary>
        public static string GetZoneName(Map map, IntVec3 pos)
        {
            if (map == null) return "Unknown";
            
            try
            {
                // 首先检查是否有具体的区域
                var zone = map.zoneManager?.ZoneAt(pos);
                if (zone != null)
                {
                    // 返回区域类型名称
                    if (zone is Zone_Stockpile) return "Stockpile";
                    if (zone is Zone_Growing) return "GrowingZone";
                    return zone.label ?? "Zone";
                }
                
                // 检查是否在家园区
                if (map.areaManager?.Home?[pos] == true)
                    return "Home";
                
                // 检查是否有建筑
                var building = map.edificeGrid?[pos];
                if (building != null)
                {
                    if (building.def?.building?.isNaturalRock == true)
                        return "Mountain";
                    return "Building";
                }
                
                // 检查地形
                var terrain = map.terrainGrid?.TerrainAt(pos);
                if (terrain != null)
                {
                    if (terrain.IsWater) return "Water";
                    if (terrain.defName?.Contains("Floor") == true) return "Floor";
                }
                
                return "Wilderness";
            }
            catch (Exception ex)
            {
                Log.Warning($"[DirectionCalculator] GetZoneName error: {ex.Message}");
                return "Unknown";
            }
        }
        
        /// <summary>
        /// 检查是否在家园区内
        /// </summary>
        public static bool IsInHomeArea(Map map, IntVec3 pos)
        {
            if (map?.areaManager?.Home == null) return false;
            
            try
            {
                return map.areaManager.Home[pos];
            }
            catch
            {
                return false;
            }
        }
        
        /// <summary>
        /// 检查是否在室内
        /// </summary>
        public static bool IsIndoors(Map map, IntVec3 pos)
        {
            if (map == null) return false;
            
            try
            {
                var room = pos.GetRoom(map);
                if (room == null) return false;
                
                // 如果是室外或非常大的区域，认为是室外
                return !room.PsychologicallyOutdoors;
            }
            catch
            {
                return false;
            }
        }
        
        /// <summary>
        /// 获取方向的中文名称
        /// </summary>
        public static string GetDirectionChinese(string direction)
        {
            return direction switch
            {
                "North" => "北方",
                "South" => "南方",
                "East" => "东方",
                "West" => "西方",
                "Northeast" => "东北方",
                "Northwest" => "西北方",
                "Southeast" => "东南方",
                "Southwest" => "西南方",
                "Center" => "中心",
                _ => "未知"
            };
        }
        
        /// <summary>
        /// 获取距离等级的中文名称
        /// </summary>
        public static string GetDistanceLevelChinese(string level)
        {
            return level switch
            {
                "VeryClose" => "非常近",
                "Close" => "近",
                "Medium" => "中等",
                "Far" => "远",
                "VeryFar" => "非常远",
                _ => "未知"
            };
        }
        
        /// <summary>
        /// 按方向对实体进行分组
        /// </summary>
        public static Dictionary<string, List<T>> GroupByDirection<T>(
            IEnumerable<T> entities, 
            Func<T, IntVec3> positionSelector, 
            IntVec3 center)
        {
            var result = new Dictionary<string, List<T>>
            {
                ["North"] = new List<T>(),
                ["South"] = new List<T>(),
                ["East"] = new List<T>(),
                ["West"] = new List<T>(),
                ["Northeast"] = new List<T>(),
                ["Northwest"] = new List<T>(),
                ["Southeast"] = new List<T>(),
                ["Southwest"] = new List<T>(),
                ["Center"] = new List<T>()
            };
            
            foreach (var entity in entities)
            {
                var pos = positionSelector(entity);
                var direction = GetDirection(center, pos);
                
                if (result.ContainsKey(direction))
                {
                    result[direction].Add(entity);
                }
                else
                {
                    result["Center"].Add(entity);
                }
            }
            
            return result;
        }
        
        /// <summary>
        /// 生成空间摘要（用于AI Prompt）
        /// </summary>
        public static string GenerateSpatialSummary(Map map)
        {
            if (map == null) return "No map available.";
            
            try
            {
                var center = CalculateColonyCenterFromHomeArea(map);
                var summary = new System.Text.StringBuilder();
                
                summary.AppendLine("=== Colony Spatial Layout ===");
                summary.AppendLine($"Colony Center: ({center.x}, {center.z})");
                
                // 统计各方向的殖民者分布
                var colonists = map.mapPawns?.FreeColonistsSpawned;
                if (colonists != null && colonists.Any())
                {
                    var grouped = GroupByDirection(colonists, p => p.Position, center);
                    
                    summary.AppendLine("\nColonist Distribution:");
                    foreach (var kvp in grouped.Where(g => g.Value.Any()))
                    {
                        summary.AppendLine($"  {kvp.Key}: {kvp.Value.Count} colonist(s)");
                    }
                }
                
                // 统计各方向的建筑分布
                var buildings = map.listerBuildings?.allBuildingsColonist;
                if (buildings != null && buildings.Any())
                {
                    var grouped = GroupByDirection(buildings, b => b.Position, center);
                    
                    summary.AppendLine("\nBuilding Distribution:");
                    foreach (var kvp in grouped.Where(g => g.Value.Any()))
                    {
                        summary.AppendLine($"  {kvp.Key}: {kvp.Value.Count} building(s)");
                    }
                }
                
                return summary.ToString();
            }
            catch (Exception ex)
            {
                return $"Error generating spatial summary: {ex.Message}";
            }
        }
    }
}
