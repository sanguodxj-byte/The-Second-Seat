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
    /// </summary>
    public static class GameStateObserver
    {
        /// <summary>
        /// Capture a snapshot of the current game state
        /// </summary>
        public static GameStateSnapshot CaptureSnapshot()
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

            // Colonists
            var colonists = map.mapPawns.FreeColonistsSpawned;
            foreach (var pawn in colonists.Take(10)) // Limit to 10 colonists to save tokens
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

            // Resources
            snapshot.resources = CaptureResources(map);

            // Threats
            snapshot.threats = CaptureThreats(map);

            // Weather
            snapshot.weather.current = map.weatherManager.curWeather?.label ?? "Clear";
            snapshot.weather.temperature = map.mapTemperature.OutdoorTemp;

            return snapshot;
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
                // Check for active raids
                var hostilePawns = map.mapPawns.AllPawnsSpawned
                    .Where(p => p.HostileTo(Faction.OfPlayer) && !p.Dead)
                    .ToList();

                threats.raidActive = hostilePawns.Any();
                threats.raidStrength = hostilePawns.Count;

                // Check for active incidents - 修复：IncidentQueue 不支持 LINQ
                // 简化处理：只检查队列中的第一个事件
                var storyteller = Find.Storyteller;
                if (storyteller?.incidentQueue != null)
                {
                    try
                    {
                        // 尝试通过反射或其他方式访问（如果不行就跳过）
                        // incidentQueue 可能没有公共 API，所以我们简单忽略这个功能
                        threats.currentEvent = null; // 暂时不支持
                    }
                    catch
                    {
                        // 访问失败时忽略
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
