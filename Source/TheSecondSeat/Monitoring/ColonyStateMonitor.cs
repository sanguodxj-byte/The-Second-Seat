using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using RimWorld;
using TheSecondSeat.Narrator;
using TheSecondSeat.Integration;

namespace TheSecondSeat.Monitoring
{
    /// <summary>
    /// 殖民地状态监控器 - 根据殖民地变化影响好感度
    /// </summary>
    public class ColonyStateMonitor : GameComponent
    {
        private int ticksSinceLastCheck = 0;
        private const int CheckInterval = 6000; // 每100秒检查一次
        
        // 上次记录的状态
        private int lastColonistCount = 0;
        private float lastWealth = 0f;
        private int lastFoodAmount = 0;
        private bool lastInCombat = false;
        private int consecutiveGoodDays = 0;
        private int consecutiveBadDays = 0;

        public ColonyStateMonitor(Game game) : base()
        {
        }

        public override void GameComponentTick()
        {
            base.GameComponentTick();

            ticksSinceLastCheck++;

            if (ticksSinceLastCheck >= CheckInterval)
            {
                ticksSinceLastCheck = 0;
                CheckColonyState();
            }
        }

        private void CheckColonyState()
        {
            var map = Find.CurrentMap;
            if (map == null) return;

            var narrator = Current.Game?.GetComponent<NarratorManager>();
            if (narrator == null) return;

            // 检查各项指标
            CheckColonistChanges(narrator);
            CheckWealthGrowth(narrator);
            CheckResourceStatus(narrator);
            CheckCombatStatus(narrator);
            CheckColonyMood(narrator);
            CheckConsecutiveDays(narrator);
        }

        /// <summary>
        /// 检查殖民者数量变化
        /// </summary>
        private void CheckColonistChanges(NarratorManager narrator)
        {
            var map = Find.CurrentMap;
            int currentCount = map.mapPawns.FreeColonistsCount;

            if (lastColonistCount == 0)
            {
                lastColonistCount = currentCount;
                return;
            }

            // 殖民者死亡
            if (currentCount < lastColonistCount)
            {
                int deaths = lastColonistCount - currentCount;
                float penalty = deaths * -5f; // 每人死亡 -5
                narrator.ModifyFavorability(penalty, $"{deaths}名殖民者死亡");
                
                MemoryContextBuilder.RecordEvent(
                    $"悲剧：{deaths}名殖民者死亡",
                    MemoryImportance.Critical
                );
                
                consecutiveBadDays++;
                consecutiveGoodDays = 0;
            }
            // 新殖民者加入
            else if (currentCount > lastColonistCount)
            {
                int newColonists = currentCount - lastColonistCount;
                float bonus = newColonists * 3f; // 每人加入 +3
                narrator.ModifyFavorability(bonus, $"{newColonists}名新殖民者加入");
                
                MemoryContextBuilder.RecordEvent(
                    $"好消息：{newColonists}名新成员加入殖民地",
                    MemoryImportance.High
                );
                
                consecutiveGoodDays++;
                consecutiveBadDays = 0;
            }

            lastColonistCount = currentCount;
        }

        /// <summary>
        /// 检查财富增长
        /// </summary>
        private void CheckWealthGrowth(NarratorManager narrator)
        {
            var map = Find.CurrentMap;
            float currentWealth = map.wealthWatcher.WealthTotal;

            if (lastWealth == 0f)
            {
                lastWealth = currentWealth;
                return;
            }

            float growthRatio = currentWealth / lastWealth;

            // 财富大幅增长（>20%）
            if (growthRatio > 1.2f)
            {
                narrator.ModifyFavorability(5f, $"殖民地财富增长{(growthRatio - 1f) * 100:F0}%");
                consecutiveGoodDays++;
            }
            // 财富大幅下降（>20%）
            else if (growthRatio < 0.8f)
            {
                narrator.ModifyFavorability(-3f, $"殖民地财富下降{(1f - growthRatio) * 100:F0}%");
                consecutiveBadDays++;
            }

            lastWealth = currentWealth;
        }

        /// <summary>
        /// 检查资源状态
        /// </summary>
        private void CheckResourceStatus(NarratorManager narrator)
        {
            var map = Find.CurrentMap;
            int currentFood = GetTotalFood(map);

            if (lastFoodAmount == 0)
            {
                lastFoodAmount = currentFood;
                return;
            }

            // 食物严重短缺（<50）且没有改善
            if (currentFood < 50 && currentFood <= lastFoodAmount)
            {
                narrator.ModifyFavorability(-0.5f, "食物持续短缺但未采取行动");
            }
            // 食物从短缺恢复
            else if (lastFoodAmount < 100 && currentFood > 200)
            {
                narrator.ModifyFavorability(2f, "成功解决食物短缺问题");
            }

            lastFoodAmount = currentFood;
        }

        /// <summary>
        /// 检查战斗状态
        /// </summary>
        private void CheckCombatStatus(NarratorManager narrator)
        {
            var map = Find.CurrentMap;
            bool inCombat = map.lordManager.lords.Any(lord => 
                lord.faction != null && lord.faction.HostileTo(Faction.OfPlayer));

            // 成功击退袭击
            if (lastInCombat && !inCombat)
            {
                // 统计殖民者伤亡
                var colonists = map.mapPawns.FreeColonists;
                // 修复：直接检查受伤的殖民者数量
                int woundedCount = colonists.Count(p => p.health != null && p.health.hediffSet != null && 
                    (p.health.hediffSet.HasNaturallyHealingInjury() || p.health.hediffSet.PainTotal > 0.1f));
                
                if (woundedCount == 0)
                {
                    narrator.ModifyFavorability(8f, "完美防御，无人受伤");
                    MemoryContextBuilder.RecordEvent("完美防御战", MemoryImportance.High);
                }
                else if (woundedCount < colonists.Count() / 2)
                {
                    narrator.ModifyFavorability(5f, "成功击退袭击");
                    MemoryContextBuilder.RecordEvent("成功防御", MemoryImportance.Medium);
                }
                else
                {
                    narrator.ModifyFavorability(2f, "艰难击退袭击，伤亡惨重");
                }
                
                consecutiveGoodDays++;
            }

            lastInCombat = inCombat;
        }

        /// <summary>
        /// 检查殖民者整体心情
        /// </summary>
        private void CheckColonyMood(NarratorManager narrator)
        {
            var map = Find.CurrentMap;
            var colonists = map.mapPawns.FreeColonists;
            
            if (colonists.Count() == 0) return;

            float avgMood = colonists.Average(p => 
                p.needs?.mood?.CurLevelPercentage ?? 0.5f) * 100f;

            // 整体心情非常好（>80%）
            if (avgMood > 80f)
            {
                narrator.ModifyFavorability(1f, "殖民者心情极佳");
                consecutiveGoodDays++;
            }
            // 整体心情很差（<30%）
            else if (avgMood < 30f)
            {
                narrator.ModifyFavorability(-1f, "殖民者心情低落");
                consecutiveBadDays++;
            }
        }

        /// <summary>
        /// 检查连续好/坏日子
        /// </summary>
        private void CheckConsecutiveDays(NarratorManager narrator)
        {
            // 连续7天好日子
            if (consecutiveGoodDays >= 7)
            {
                narrator.ModifyFavorability(10f, "连续一周殖民地繁荣发展");
                MemoryContextBuilder.RecordEvent("黄金一周", MemoryImportance.Critical);
                consecutiveGoodDays = 0;
            }
            // 连续7天坏日子
            else if (consecutiveBadDays >= 7)
            {
                narrator.ModifyFavorability(-5f, "连续一周灾难不断");
                MemoryContextBuilder.RecordEvent("黑暗一周", MemoryImportance.Critical);
                consecutiveBadDays = 0;
            }
        }

        /// <summary>
        /// 获取总食物量
        /// </summary>
        private int GetTotalFood(Map map)
        {
            return map.resourceCounter.GetCount(ThingDefOf.MealSimple) +
                   map.resourceCounter.GetCount(ThingDefOf.MealFine) +
                   map.resourceCounter.GetCount(ThingDefOf.MealSurvivalPack);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref lastColonistCount, "lastColonistCount", 0);
            Scribe_Values.Look(ref lastWealth, "lastWealth", 0f);
            Scribe_Values.Look(ref lastFoodAmount, "lastFoodAmount", 0);
            Scribe_Values.Look(ref lastInCombat, "lastInCombat", false);
            Scribe_Values.Look(ref consecutiveGoodDays, "consecutiveGoodDays", 0);
            Scribe_Values.Look(ref consecutiveBadDays, "consecutiveBadDays", 0);
        }
    }

    /// <summary>
    /// 玩家互动监控器 - 根据玩家行为影响好感度
    /// </summary>
    public class PlayerInteractionMonitor : GameComponent
    {
        private int totalConversations = 0;
        private int lastConversationTick = 0;
        private int ignoredSuggestions = 0;

        public PlayerInteractionMonitor(Game game) : base()
        {
        }

        /// <summary>
        /// 记录对话
        /// </summary>
        public void RecordConversation(bool hasUserMessage)
        {
            totalConversations++;
            
            var narrator = Current.Game?.GetComponent<NarratorManager>();
            if (narrator == null) return;

            // 频繁互动奖励（每10次对话）
            if (totalConversations % 10 == 0 && hasUserMessage)
            {
                narrator.ModifyFavorability(1f, "经常与我交流");
            }

            // 长时间未互动后重新对话
            int ticksSinceLastConversation = Find.TickManager.TicksGame - lastConversationTick;
            if (ticksSinceLastConversation > 360000) // >6小时
            {
                narrator.ModifyFavorability(-1f, "长时间冷落我");
            }

            lastConversationTick = Find.TickManager.TicksGame;
        }

        /// <summary>
        /// 记录忽略建议
        /// </summary>
        public void RecordIgnoredSuggestion()
        {
            ignoredSuggestions++;
            
            var narrator = Current.Game?.GetComponent<NarratorManager>();
            if (narrator == null) return;

            // 累计忽略惩罚
            if (ignoredSuggestions >= 5)
            {
                narrator.ModifyFavorability(-3f, "多次忽略我的建议");
                ignoredSuggestions = 0;
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref totalConversations, "totalConversations", 0);
            Scribe_Values.Look(ref lastConversationTick, "lastConversationTick", 0);
            Scribe_Values.Look(ref ignoredSuggestions, "ignoredSuggestions", 0);
        }
    }
}
