using System;
using System.Collections.Generic;
using Verse;
using RimWorld;

namespace TheSecondSeat.Framework.Triggers
{
    /// <summary>
    /// 好感度范围触发器
    /// 检查当前好感度是否在指定范围内
    /// 
    /// XML示例：
    /// <![CDATA[
    /// <li Class="TheSecondSeat.Framework.Triggers.AffinityRangeTrigger">
    ///   <minAffinity>60</minAffinity>
    ///   <maxAffinity>100</maxAffinity>
    /// </li>
    /// ]]>
    /// </summary>
    public class AffinityRangeTrigger : TSSTrigger
    {
        public float minAffinity = float.MinValue;
        public float maxAffinity = float.MaxValue;
        
        public override bool IsSatisfied(Map map, Dictionary<string, object> context)
        {
            if (!context.TryGetValue("affinity", out object affinityObj))
            {
                return false;
            }
            
            float affinity = Convert.ToSingle(affinityObj);
            return affinity >= minAffinity && affinity <= maxAffinity;
        }
        
        public override string GetDescription()
        {
            return $"Affinity [{minAffinity}, {maxAffinity}]";
        }
    }
    
    /// <summary>
    /// 殖民者数量触发器
    /// 检查殖民者数量是否满足条件
    /// </summary>
    public class ColonistCountTrigger : TSSTrigger
    {
        public int minCount = 0;
        public int maxCount = int.MaxValue;
        
        public override bool IsSatisfied(Map map, Dictionary<string, object> context)
        {
            if (!context.TryGetValue("colonistCount", out object countObj))
            {
                if (map != null)
                {
                    int count = map.mapPawns.FreeColonistsCount;
                    return count >= minCount && count <= maxCount;
                }
                return false;
            }
            
            int colonistCount = Convert.ToInt32(countObj);
            return colonistCount >= minCount && colonistCount <= maxCount;
        }
        
        public override string GetDescription()
        {
            return $"Colonists [{minCount}, {maxCount}]";
        }
    }
    
    /// <summary>
    /// 财富范围触发器
    /// </summary>
    public class WealthRangeTrigger : TSSTrigger
    {
        public float minWealth = 0f;
        public float maxWealth = float.MaxValue;
        public bool checkTotal = true;
        public bool checkBuildings = false;
        public bool checkItems = false;
        
        public override bool IsSatisfied(Map map, Dictionary<string, object> context)
        {
            float wealth = 0f;
            
            if (checkTotal && context.TryGetValue("wealthTotal", out object wealthTotalObj))
            {
                wealth = Convert.ToSingle(wealthTotalObj);
            }
            else if (checkBuildings && context.TryGetValue("wealthBuildings", out object wealthBuildingsObj))
            {
                wealth = Convert.ToSingle(wealthBuildingsObj);
            }
            else if (checkItems && context.TryGetValue("wealthItems", out object wealthItemsObj))
            {
                wealth = Convert.ToSingle(wealthItemsObj);
            }
            else if (map != null && map.wealthWatcher != null)
            {
                wealth = map.wealthWatcher.WealthTotal;
            }
            
            return wealth >= minWealth && wealth <= maxWealth;
        }
        
        public override string GetDescription()
        {
            string type = checkBuildings ? "Buildings" : checkItems ? "Items" : "Total";
            return $"Wealth {type} [{minWealth}, {maxWealth}]";
        }
    }
    
    /// <summary>
    /// 季节触发器
    /// </summary>
    public class SeasonTrigger : TSSTrigger
    {
        public List<Season> allowedSeasons = new List<Season>();
        
        public override bool IsSatisfied(Map map, Dictionary<string, object> context)
        {
            if (allowedSeasons == null || allowedSeasons.Count == 0)
            {
                return true;
            }
            
            if (context.TryGetValue("gameSeason", out object seasonObj))
            {
                Season currentSeason = (Season)seasonObj;
                return allowedSeasons.Contains(currentSeason);
            }
            
            if (map != null)
            {
                Season currentSeason = GenDate.Season(Find.TickManager.TicksAbs, Find.WorldGrid.LongLatOf(map.Tile));
                return allowedSeasons.Contains(currentSeason);
            }
            
            return false;
        }
        
        public override string GetDescription()
        {
            return $"Season: {string.Join(", ", allowedSeasons)}";
        }
    }
    
    /// <summary>
    /// 时间范围触发器（游戏内时间）
    /// </summary>
    public class TimeRangeTrigger : TSSTrigger
    {
        public int minHour = 0;
        public int maxHour = 24;
        
        public override bool IsSatisfied(Map map, Dictionary<string, object> context)
        {
            int currentHour = GenLocalDate.HourOfDay(map);
            return currentHour >= minHour && currentHour < maxHour;
        }
        
        public override string GetDescription()
        {
            return $"Time [{minHour}:00, {maxHour}:00)";
        }
    }
    
    /// <summary>
    /// 随机概率触发器（额外的概率检查）
    /// </summary>
    public class RandomChanceTrigger : TSSTrigger
    {
        public float chance = 0.5f;
        
        public override bool IsSatisfied(Map map, Dictionary<string, object> context)
        {
            return Rand.Value <= chance;
        }
        
        public override string GetDescription()
        {
            return $"Random Chance {chance:P0}";
        }
    }
    
    /// <summary>
    /// 心情状态触发器
    /// </summary>
    public class MoodStateTrigger : TSSTrigger
    {
        public List<string> allowedMoods = new List<string>();
        
        public override bool IsSatisfied(Map map, Dictionary<string, object> context)
        {
            if (allowedMoods == null || allowedMoods.Count == 0)
            {
                return true;
            }
            
            if (!context.TryGetValue("mood", out object moodObj))
            {
                return false;
            }
            
            string currentMood = moodObj.ToString();
            return allowedMoods.Contains(currentMood);
        }
        
        public override string GetDescription()
        {
            return $"Mood: {string.Join(", ", allowedMoods)}";
        }
    }
}
