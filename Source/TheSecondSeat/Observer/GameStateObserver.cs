using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using RimWorld;
using Newtonsoft.Json;

namespace TheSecondSeat.Observer
{
    /// <summary>
    /// Simplified game state snapshot for LLM consumption
    /// </summary>
    [Serializable]
    public class GameStateSnapshot
    {
        public ColonyInfo colony { get; set; } = new ColonyInfo();
        public List<ColonistInfo> colonists { get; set; } = new List<ColonistInfo>();
        public ResourceInfo resources { get; set; } = new ResourceInfo();
        public ThreatInfo threats { get; set; } = new ThreatInfo();
        public WeatherInfo weather { get; set; } = new WeatherInfo();
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
    /// Observes and captures the current game state in a token-efficient format
    /// ? v1.6.42: 添加线程安全的快照接口
    /// </summary>
    public static class GameStateObserver
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
                Log.Warning("[GameStateObserver] Cache system disabled, using direct capture (main thread only)");
            }
            return CaptureSnapshotUnsafe();
        }

        /// <summary>
        /// ? v1.6.42: 非线程安全的快照捕获（仅限主线程调用）
        /// 原 CaptureSnapshot() 重命名，明确表示线程不安全
        /// ? v1.6.46: 修复线程安全问题 - 避免访问 map.mapPawns
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
                        health = (int)(pawn.health.summaryHealth.SummaryHealthPercent * 100)
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

            // Weather
            snapshot.weather.current = map.weatherManager.curWeather?.label ?? "Clear";
            snapshot.weather.temperature = map.mapTemperature.OutdoorTemp;

            return snapshot;
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
