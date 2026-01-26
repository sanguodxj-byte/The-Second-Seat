using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using RimWorld;
using TheSecondSeat.Narrator;
using TheSecondSeat.Integration;
using UnityEngine;

namespace TheSecondSeat.Monitoring
{
    /// <summary>
    /// 殖民地状态监控器 - 负责监控游戏状态变化并触发叙事者反应
    /// v1.6.42: 初始实现
    /// v2.0.0: 恢复功能，使用 GameStateSnapshotUtility 替代缺失的 Cache
    /// </summary>
    public class ColonyStateMonitor : GameComponent
    {
        private int ticksSinceLastCheck = 0;
        private const int CheckInterval = 6000; // 约1.5分钟 (游戏内时间)
        
        // 状态追踪变量
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

        /// <summary>
        /// 检查殖民地状态变化
        /// </summary>
        private void CheckColonyState()
        {
            // 使用 Unsafe 捕获（因为我们在主线程中，这是安全的）
            var snapshot = GameStateSnapshotUtility.CaptureSnapshotUnsafe();
            if (snapshot == null) return;

            var narrator = Current.Game?.GetComponent<NarratorManager>();
            if (narrator == null) return;

            CheckColonistChanges(narrator, snapshot);
            CheckWealthGrowth(narrator, snapshot);
            CheckResourceStatus(narrator, snapshot);
            CheckCombatStatus(narrator, snapshot);
            CheckColonyMood(narrator, snapshot);
        }

        private void CheckColonistChanges(NarratorManager narrator, GameStateSnapshot snapshot)
        {
            int currentCount = snapshot.colonists.Count;
            // 初始化
            if (lastColonistCount == 0 && currentCount > 0)
            {
                lastColonistCount = currentCount;
                return;
            }

            if (currentCount > lastColonistCount)
            {
                narrator.ModifyFavorability(2f, "新殖民者加入");
            }
            else if (currentCount < lastColonistCount)
            {
                narrator.ModifyFavorability(-5f, "失去殖民者");
            }
            lastColonistCount = currentCount;
        }

        private void CheckWealthGrowth(NarratorManager narrator, GameStateSnapshot snapshot)
        {
            float currentWealth = snapshot.colony.wealth;
            if (lastWealth <= 0.1f)
            {
                lastWealth = currentWealth;
                return;
            }

            // 显著增长 (20%)
            if (currentWealth > lastWealth * 1.2f)
            {
                narrator.ModifyFavorability(1f, "殖民地繁荣发展");
                lastWealth = currentWealth;
            }
            else
            {
                // 平滑更新基准值，用于长期趋势追踪
                lastWealth = Mathf.Lerp(lastWealth, currentWealth, 0.05f);
            }
        }

        private void CheckResourceStatus(NarratorManager narrator, GameStateSnapshot snapshot)
        {
            int currentFood = snapshot.resources.food;
            
            // 简单的低食物预警
            if (currentFood < 50 && lastFoodAmount >= 50 && snapshot.colonists.Count > 0)
            {
                narrator.ModifyFavorability(-1f, "食物短缺危机");
            }
            
            lastFoodAmount = currentFood;
        }

        private void CheckCombatStatus(NarratorManager narrator, GameStateSnapshot snapshot)
        {
            bool currentCombat = snapshot.threats.raidActive;
            
            if (!currentCombat && lastInCombat)
            {
                // 战斗刚刚结束
                narrator.ModifyFavorability(1f, "成功度过威胁");
            }
            
            lastInCombat = currentCombat;
        }

        private void CheckColonyMood(NarratorManager narrator, GameStateSnapshot snapshot)
        {
            if (snapshot.colonists.Count == 0) return;
            
            float avgMood = 0f;
            foreach(var c in snapshot.colonists) avgMood += c.mood;
            avgMood /= snapshot.colonists.Count;
            
            // Mood is 0-100 in snapshot
            if (avgMood > 80)
            {
                consecutiveGoodDays++;
                consecutiveBadDays = 0;
                
                // 约每24小时 (15次检查)
                if (consecutiveGoodDays % 15 == 0) 
                {
                     narrator.ModifyFavorability(0.5f, "殖民地士气高昂");
                }
            }
            else if (avgMood < 30)
            {
                consecutiveBadDays++;
                consecutiveGoodDays = 0;
                
                if (consecutiveBadDays % 5 == 0)
                {
                    narrator.ModifyFavorability(-0.5f, "殖民地士气低落");
                }
            }
            else
            {
                consecutiveGoodDays = 0;
                consecutiveBadDays = 0;
            }
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
    /// 玩家互动监控器 - 监控玩家与叙事者的交互频率
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
        /// 记录一次对话
        /// </summary>
        public void RecordConversation(bool hasUserMessage)
        {
            totalConversations++;
            
            var narrator = Current.Game?.GetComponent<NarratorManager>();
            if (narrator == null) return;

            // 每积极互动10次增加好感
            if (totalConversations % 10 == 0 && hasUserMessage)
            {
                narrator.ModifyFavorability(1f, "频繁的友好交流");
            }

            // 检查是否很久没有互动
            int ticksSinceLastConversation = Find.TickManager.TicksGame - lastConversationTick;
            if (ticksSinceLastConversation > 360000) // >6小时 (游戏时间约10天)
            {
                narrator.ModifyFavorability(-1f, "被长期冷落");
            }

            lastConversationTick = Find.TickManager.TicksGame;
        }

        /// <summary>
        /// 记录一次忽略建议
        /// </summary>
        public void RecordIgnoredSuggestion()
        {
            ignoredSuggestions++;
            
            var narrator = Current.Game?.GetComponent<NarratorManager>();
            if (narrator == null) return;

            // 连续忽略建议
            if (ignoredSuggestions >= 5)
            {
                narrator.ModifyFavorability(-3f, "建议屡次被无视");
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
