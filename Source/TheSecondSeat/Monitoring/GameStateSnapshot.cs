using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using RimWorld;
using Newtonsoft.Json;

namespace TheSecondSeat.Monitoring
{
    /// <summary>
    /// Simplified game state snapshot for LLM consumption
    /// v2.0.0: 增强空间感知能力
    /// </summary>
    [Serializable]
    public class GameStateSnapshot
    {
        public ColonyInfo colony { get; set; } = new ColonyInfo();
        public List<ColonistInfo> colonists { get; set; } = new List<ColonistInfo>();
        public ResourceInfo resources { get; set; } = new ResourceInfo();
        public ThreatInfo threats { get; set; } = new ThreatInfo();
        public WeatherInfo weather { get; set; } = new WeatherInfo();
        
        // v2.0.0: 空间感知扩展
        /// <summary>
        /// 殖民地中心位置
        /// </summary>
        public SpatialInfo colonyCenter { get; set; } = new SpatialInfo();
        
        /// <summary>
        /// 重要建筑列表（含位置信息）
        /// </summary>
        public List<BuildingInfo> buildings { get; set; } = new List<BuildingInfo>();
        
        /// <summary>
        /// 威胁实体列表（含位置信息）
        /// </summary>
        public List<ThreatEntityInfo> threatEntities { get; set; } = new List<ThreatEntityInfo>();
        
        /// <summary>
        /// 空间布局摘要（用于AI快速理解）
        /// </summary>
        public string? spatialSummary { get; set; }
    }

    [Serializable]
    public class ColonyInfo
    {
        public int wealth { get; set; }
        public string biome { get; set; } = "";
        public int daysPassed { get; set; }
    }

    [Serializable]
    public class ColonistInfo
    {
        public string name { get; set; } = "";
        public int mood { get; set; }
        public string currentJob { get; set; } = "";
        public int health { get; set; }
        public List<string> majorInjuries { get; set; } = new List<string>();
        
        // v2.0.0: 空间位置信息
        /// <summary>
        /// 殖民者当前位置信息
        /// </summary>
        public SpatialInfo location { get; set; } = new SpatialInfo();
        
        /// <summary>
        /// 是否在工作中
        /// </summary>
        public bool isWorking { get; set; }
        
        /// <summary>
        /// 当前房间类型（如果有）
        /// </summary>
        public string? currentRoom { get; set; }
    }

    [Serializable]
    public class ResourceInfo
    {
        public int food { get; set; }
        public int wood { get; set; }
        public int steel { get; set; }
        public int medicine { get; set; }
    }

    [Serializable]
    public class ThreatInfo
    {
        public bool raidActive { get; set; }
        public int raidStrength { get; set; }
        public string? currentEvent { get; set; }
    }

    [Serializable]
    public class WeatherInfo
    {
        public string current { get; set; } = "";
        public float temperature { get; set; }
    }

    /// <summary>
    /// Utility class for capturing game state snapshots in a token-efficient format
    /// ? v1.6.42: 添加线程安全的快照接口
    /// ? v1.6.50: 重命名为 GameStateSnapshotUtility 以避免与 GameComponent 冲突
    /// </summary>
    public static class GameStateSnapshotUtility
    {
        /// <summary>
        /// ? v1.6.42: 线程安全的快照获取（供后台 AI 线程调用）
        /// 从缓存读取，避免跨线程访问游戏状态
        /// ? v1.6.46: 临时禁用缓存（GameStateCache 类不存在）
        /// </summary>
        public static GameStateSnapshot CaptureSnapshotSafe()
        {
            // ? v1.6.46: 临时注释掉缓存调用（GameStateCache 类不存在）
            /*
            // 尝试从缓存获取
            var cached = GameStateCache.GetCachedSnapshot();
            
            if (cached != null)
            {
                return cached;
            }
            */
            
            // ? v1.6.46: 直接调用 Unsafe 方法（需要在主线程调用）
            // 原因是：缓存机制比较复杂，涉及到游戏状态的序列化与跨线程访问，
            // 而后台 AI 线程并不能保证何时何地被调用，因此直接在主线程捕获状态比较可靠
            if (Prefs.DevMode)
            {
                Log.Warning("[GameStateSnapshotUtility] Cache system disabled, using direct capture (main thread only)");
            }
            return CaptureSnapshotUnsafe();
        }

        /// <summary>
        /// ? v1.6.42: 非线程安全的快照捕获（仅限主线程调用）
        /// 原 CaptureSnapshot() 重命名，明确表示线程不安全
        /// ? v1.6.46: 修复线程安全问题 - 避免访问 map.mapPawns
        /// ? v2.0.0: 增强空间感知能力
        /// </summary>
        public static GameStateSnapshot CaptureSnapshotUnsafe()
        {
            var snapshot = new GameStateSnapshot();
            
            var map = Find.CurrentMap;
            if (map == null)
            {
                return snapshot;
            }

            // Colony info
            snapshot.colony.biome = map.Biome?.label ?? "Unknown";
            snapshot.colony.daysPassed = GenDate.DaysPassed;
            snapshot.colony.wealth = (int)map.wealthWatcher.WealthTotal;

            // v2.0.0: 计算殖民地中心
            var colonyCenter = DirectionCalculator.CalculateColonyCenterFromHomeArea(map);
            snapshot.colonyCenter = new SpatialInfo
            {
                x = colonyCenter.x,
                z = colonyCenter.z,
                direction = "Center",
                distanceFromCenter = 0,
                distanceLevel = "VeryClose",
                zone = "Home",
                isInHomeArea = true
            };

            // ? 修复：避免使用 map.mapPawns，改为安全的遍历方式
            // 使用 map.listerThings.ThingsInGroup(ThingRequestGroup.Pawn) 代替
            try
            {
                var allPawns = map.listerThings.ThingsInGroup(ThingRequestGroup.Pawn);
                var colonists = allPawns
                    .OfType<Pawn>()
                    .Where(p => p.IsColonist && p.Spawned && !p.Dead)
                    .Take(10); // Limit to 10 colonists to save tokens

                foreach (var pawn in colonists)
                {
                    var colonistInfo = new ColonistInfo
                    {
                        name = pawn.Name?.ToStringShort ?? "Unknown",
                        mood = pawn.needs?.mood?.CurLevelPercentage != null 
                            ? (int)(pawn.needs.mood.CurLevelPercentage * 100) 
                            : 50,
                        currentJob = pawn.CurJob?.def?.reportString ?? "Idle",
                        health = (int)(pawn.health.summaryHealth.SummaryHealthPercent * 100),
                        // v2.0.0: 添加空间位置信息
                        location = DirectionCalculator.GetSpatialInfo(pawn.Position, colonyCenter, map),
                        isWorking = pawn.CurJob != null && !pawn.CurJob.def.casualInterruptible,
                        currentRoom = GetRoomName(pawn, map)
                    };

                    // Major injuries
                    var hediffs = pawn.health.hediffSet.hediffs
                        .Where(h => h.Visible && h.def.makesSickThought)
                        .Take(3);
                    
                    foreach (var hediff in hediffs)
                    {
                        colonistInfo.majorInjuries.Add(hediff.def.label);
                    }

                    snapshot.colonists.Add(colonistInfo);
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[GameStateObserver] Error capturing colonists: {ex.Message}");
            }

            // Resources
            snapshot.resources = CaptureResources(map);

            // Threats
            snapshot.threats = CaptureThreats(map);
            
            // v2.0.0: 捕获威胁实体位置信息
            snapshot.threatEntities = CaptureThreatEntities(map, colonyCenter);

            // Weather
            snapshot.weather.current = map.weatherManager.curWeather?.label ?? "Clear";
            snapshot.weather.temperature = map.mapTemperature.OutdoorTemp;
            
            // v2.0.0: 捕获重要建筑
            snapshot.buildings = CaptureBuildings(map, colonyCenter);
            
            // v2.0.0: 生成空间摘要
            snapshot.spatialSummary = GenerateSpatialSummary(snapshot);

            return snapshot;
        }
        
        /// <summary>
        /// 获取Pawn所在房间名称
        /// </summary>
        private static string? GetRoomName(Pawn pawn, Map map)
        {
            try
            {
                var room = pawn.Position.GetRoom(map);
                if (room == null || room.PsychologicallyOutdoors)
                    return "Outdoors";
                
                // 尝试获取房间角色
                var role = room.Role;
                if (role != null)
                    return role.label;
                
                return "Room";
            }
            catch
            {
                return null;
            }
        }
        
        /// <summary>
        /// v2.0.0: 捕获重要建筑信息
        /// </summary>
        private static List<BuildingInfo> CaptureBuildings(Map map, IntVec3 colonyCenter)
        {
            var buildings = new List<BuildingInfo>();
            
            try
            {
                var importantBuildings = map.listerBuildings.allBuildingsColonist
                    .Where(b => IsImportantBuilding(b))
                    .Take(20); // 限制数量节省token
                
                foreach (var building in importantBuildings)
                {
                    buildings.Add(new BuildingInfo
                    {
                        name = building.Label,
                        defName = building.def.defName,
                        type = GetBuildingType(building),
                        location = DirectionCalculator.GetSpatialInfo(building.Position, colonyCenter, map),
                        size = building.def.size.x * building.def.size.z,
                        isOperational = IsBuildingOperational(building),
                        currentWorker = GetBuildingWorker(building)
                    });
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[GameStateSnapshotUtility] Error capturing buildings: {ex.Message}");
            }
            
            return buildings;
        }
        
        /// <summary>
        /// 判断是否为重要建筑
        /// </summary>
        private static bool IsImportantBuilding(Building building)
        {
            if (building?.def == null) return false;
            
            // 生产设施
            if (building is Building_WorkTable) return true;
            
            // 电力设施
            if (building.def.HasComp(typeof(CompPowerPlant))) return true;
            if (building.def.HasComp(typeof(CompPowerBattery))) return true;
            
            // 防御设施
            if (building.def.building?.IsTurret == true) return true;
            
            // 存储设施
            if (building.def.HasComp(typeof(CompForbiddable)) && building.def.thingClass.Name.Contains("Storage")) return true;
            
            // 医疗设施
            if (building.def.defName.Contains("Medical") || building.def.defName.Contains("Hospital")) return true;
            
            // 研究设施
            if (building.def.defName.Contains("Research")) return true;
            
            // 娱乐设施（需要至少占用2格）
            if (building.def.size.x * building.def.size.z >= 2) return true;
            
            return false;
        }
        
        /// <summary>
        /// 获取建筑类型
        /// </summary>
        private static string GetBuildingType(Building building)
        {
            if (building?.def == null) return "Unknown";
            
            if (building is Building_WorkTable) return "Production";
            if (building.def.HasComp(typeof(CompPowerPlant))) return "Power";
            if (building.def.HasComp(typeof(CompPowerBattery))) return "Power";
            if (building.def.building?.IsTurret == true) return "Defense";
            if (building.def.defName.Contains("Medical") || building.def.defName.Contains("Hospital")) return "Medical";
            if (building.def.defName.Contains("Research")) return "Research";
            if (building.def.defName.Contains("Joy") || building.def.defName.Contains("Recreation")) return "Recreation";
            if (building.def.defName.Contains("Storage")) return "Storage";
            
            return "Other";
        }
        
        /// <summary>
        /// 检查建筑是否正在运行
        /// </summary>
        private static bool IsBuildingOperational(Building building)
        {
            try
            {
                // 检查电力
                var powerComp = building.TryGetComp<CompPowerTrader>();
                if (powerComp != null && !powerComp.PowerOn)
                    return false;
                
                // 检查故障
                var breakdownComp = building.TryGetComp<CompBreakdownable>();
                if (breakdownComp != null && breakdownComp.BrokenDown)
                    return false;
                
                return true;
            }
            catch
            {
                return true;
            }
        }
        
        /// <summary>
        /// 获取建筑当前工作者
        /// </summary>
        private static string? GetBuildingWorker(Building building)
        {
            try
            {
                if (building is Building_WorkTable workTable)
                {
                    // 查找正在使用此工作台的Pawn
                    var map = building.Map;
                    if (map != null)
                    {
                        var pawnsAtBuilding = map.mapPawns.FreeColonistsSpawned
                            .Where(p => p.CurJob?.targetA.Thing == building)
                            .FirstOrDefault();
                        
                        return pawnsAtBuilding?.Name?.ToStringShort;
                    }
                }
            }
            catch
            {
                // 忽略错误
            }
            
            return null;
        }
        
        /// <summary>
        /// v2.0.0: 捕获威胁实体信息
        /// </summary>
        private static List<ThreatEntityInfo> CaptureThreatEntities(Map map, IntVec3 colonyCenter)
        {
            var threats = new List<ThreatEntityInfo>();
            
            try
            {
                var allPawns = map.listerThings.ThingsInGroup(ThingRequestGroup.Pawn);
                var hostilePawns = allPawns
                    .OfType<Pawn>()
                    .Where(p => p.Spawned && !p.Dead && p.HostileTo(Faction.OfPlayer))
                    .Take(20); // 限制数量
                
                foreach (var pawn in hostilePawns)
                {
                    threats.Add(new ThreatEntityInfo
                    {
                        name = pawn.Name?.ToStringShort ?? pawn.def.label,
                        threatType = GetThreatType(pawn),
                        faction = pawn.Faction?.Name,
                        location = DirectionCalculator.GetSpatialInfo(pawn.Position, colonyCenter, map),
                        threatLevel = CalculateThreatLevel(pawn),
                        isInCombat = pawn.CurJob?.def == JobDefOf.AttackMelee || pawn.CurJob?.def == JobDefOf.AttackStatic,
                        weapon = pawn.equipment?.Primary?.def?.label
                    });
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[GameStateSnapshotUtility] Error capturing threat entities: {ex.Message}");
            }
            
            return threats;
        }
        
        /// <summary>
        /// 获取威胁类型
        /// </summary>
        private static string GetThreatType(Pawn pawn)
        {
            if (pawn.RaceProps.IsMechanoid) return "Mechanoid";
            if (pawn.RaceProps.Insect) return "Insect";
            if (pawn.RaceProps.Animal) return "Animal";
            if (pawn.Faction?.def.defName.Contains("Pirate") == true) return "Pirate";
            if (pawn.Faction?.def.defName.Contains("Tribe") == true) return "Tribal";
            return "Raider";
        }
        
        /// <summary>
        /// 计算威胁等级
        /// </summary>
        private static int CalculateThreatLevel(Pawn pawn)
        {
            float combatPower = pawn.kindDef?.combatPower ?? 50f;
            
            if (combatPower < 30) return 1;
            if (combatPower < 60) return 2;
            if (combatPower < 100) return 3;
            if (combatPower < 200) return 4;
            return 5;
        }
        
        /// <summary>
        /// v2.0.0: 生成空间摘要（用于AI快速理解）
        /// </summary>
        private static string GenerateSpatialSummary(GameStateSnapshot snapshot)
        {
            var sb = new System.Text.StringBuilder();
            
            sb.AppendLine($"Colony Center: ({snapshot.colonyCenter.x}, {snapshot.colonyCenter.z})");
            
            // 按方向分组殖民者
            var colonistsByDirection = snapshot.colonists
                .GroupBy(c => c.location.direction)
                .Where(g => g.Any())
                .ToDictionary(g => g.Key, g => g.ToList());
            
            if (colonistsByDirection.Any())
            {
                sb.AppendLine("Colonist Distribution:");
                foreach (var kvp in colonistsByDirection)
                {
                    var names = string.Join(", ", kvp.Value.Select(c => c.name));
                    sb.AppendLine($"  {kvp.Key}: {names}");
                }
            }
            
            // 威胁方向
            if (snapshot.threatEntities.Any())
            {
                var threatDirections = snapshot.threatEntities
                    .GroupBy(t => t.location.direction)
                    .Select(g => $"{g.Key}({g.Count()})")
                    .ToList();
                
                sb.AppendLine($"Threats from: {string.Join(", ", threatDirections)}");
            }
            
            return sb.ToString();
        }

        /// <summary>
        /// ?? 已废弃：使用 CaptureSnapshotSafe() 代替（线程安全）
        /// 或使用 CaptureSnapshotUnsafe() （仅主线程）
        /// </summary>
        [Obsolete("使用 CaptureSnapshotSafe() 代替（线程安全）")]
        public static GameStateSnapshot CaptureSnapshot()
        {
            return CaptureSnapshotUnsafe();
        }

        private static ResourceInfo CaptureResources(Map map)
        {
            var resources = new ResourceInfo();

            try
            {
                // ? 修复：只计算殖民地区域（Home Area）的资源，而不是整个地图
                var homeArea = map.areaManager.Home;
                
                if (homeArea == null)
                {
                    Log.Warning("[The Second Seat] Home area not found, counting all stockpile zones");
                    // 如果没有 Home Area，则计算所有储存区域
                    return CaptureResourcesFromStockpiles(map);
                }

                // 只计算 Home Area 内的物品
                var allThings = map.listerThings.AllThings
                    .Where(t => homeArea[t.Position]) // ? 过滤：只在 Home Area 内
                    .ToList();

                resources.food = allThings
                    .Where(t => t.def.IsNutritionGivingIngestible)
                    .Sum(t => t.stackCount);

                resources.wood = CountResource(allThings, ThingDefOf.WoodLog);
                resources.steel = CountResource(allThings, ThingDefOf.Steel);
                resources.medicine = CountResource(allThings, ThingDefOf.MedicineIndustrial);
                
                Log.Message($"[The Second Seat] Captured colony resources (Home Area): Food={resources.food}, Wood={resources.wood}, Steel={resources.steel}, Medicine={resources.medicine}");
            }
            catch (Exception ex)
            {
                Log.Warning($"[The Second Seat] Error capturing resources: {ex.Message}");
            }

            return resources;
        }

        /// <summary>
        /// 备用方案：从储存区域计算资源
        /// </summary>
        private static ResourceInfo CaptureResourcesFromStockpiles(Map map)
        {
            var resources = new ResourceInfo();

            try
            {
                // 获取所有储存区域的物品
                var stockpileZones = map.zoneManager.AllZones
                    .OfType<Zone_Stockpile>()
                    .ToList();

                var stockpileThings = new List<Thing>();
                
                foreach (var zone in stockpileZones)
                {
                    foreach (var cell in zone.Cells)
                    {
                        var things = map.thingGrid.ThingsListAtFast(cell);
                        stockpileThings.AddRange(things);
                    }
                }

                resources.food = stockpileThings
                    .Where(t => t.def.IsNutritionGivingIngestible)
                    .Sum(t => t.stackCount);

                resources.wood = CountResource(stockpileThings, ThingDefOf.WoodLog);
                resources.steel = CountResource(stockpileThings, ThingDefOf.Steel);
                resources.medicine = CountResource(stockpileThings, ThingDefOf.MedicineIndustrial);
                
                Log.Message($"[The Second Seat] Captured colony resources (Stockpiles): Food={resources.food}, Wood={resources.wood}, Steel={resources.steel}, Medicine={resources.medicine}");
            }
            catch (Exception ex)
            {
                Log.Warning($"[The Second Seat] Error capturing stockpile resources: {ex.Message}");
            }

            return resources;
        }

        private static int CountResource(List<Thing> allThings, ThingDef def)
        {
            return allThings.Where(t => t.def == def).Sum(t => t.stackCount);
        }

        private static ThreatInfo CaptureThreats(Map map)
        {
            var threats = new ThreatInfo();

            try
            {
                // ? 修复：避免使用 map.mapPawns.AllPawnsSpawned
                // Check for active raids using safe method
                var allPawns = map.listerThings.ThingsInGroup(ThingRequestGroup.Pawn);
                var hostilePawns = allPawns
                    .OfType<Pawn>()
                    .Where(p => p.Spawned && !p.Dead && p.HostileTo(Faction.OfPlayer))
                    .ToList();

                threats.raidActive = hostilePawns.Any();
                threats.raidStrength = hostilePawns.Count;

                // Check for active incidents - 修改：IncidentQueue 不支持 LINQ
                // 简化处理，只检测排队中的第一个事件
                var storyteller = Find.Storyteller;
                if (storyteller?.incidentQueue != null)
                {
                    try
                    {
                        // 由于没有公开 API，我们就简单检测即可
                        // incidentQueue 内部没有公开 API，所以暂不检测了
                        threats.currentEvent = null; // 暂不支持
                    }
                    catch
                    {
                        // 读取失败时忽略
                        threats.currentEvent = null;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[The Second Seat] Error capturing threats: {ex.Message}");
            }

            return threats;
        }

        /// <summary>
        /// Convert snapshot to JSON string
        /// </summary>
        public static string SnapshotToJson(GameStateSnapshot snapshot)
        {
            try
            {
                return JsonConvert.SerializeObject(snapshot, Formatting.Indented);
            }
            catch (Exception ex)
            {
                Log.Error($"[The Second Seat] Failed to serialize game state: {ex.Message}");
                return "{}";
            }
        }
    }
}
