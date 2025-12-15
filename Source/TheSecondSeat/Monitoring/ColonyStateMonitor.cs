using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using RimWorld;
using TheSecondSeat.Narrator;
using TheSecondSeat.Integration;
// ? v1.6.46: 临时注释掉 GameStateSnapshot 引用（该类可能不存在或在其他命名空间）
/// using TheSecondSeat.Core;  

namespace TheSecondSeat.Monitoring
{
    /// <summary>
    /// ??????????? - ???????仯????ж?
    /// ? v1.6.42: ?????????????????????????????
    /// ? v1.6.46: 临时禁用（GameStateSnapshot 类不存在）
    /// </summary>
    public class ColonyStateMonitor : GameComponent
    {
        private int ticksSinceLastCheck = 0;
        private const int CheckInterval = 6000; // ?100???????
        
        // ??μ??????
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
                // ? v1.6.46: 临时禁用（GameStateSnapshot 不可用）
                // CheckColonyState();
            }
        }

        /// <summary>
        /// ? v1.6.42: ?????????????
        /// ? v1.6.46: 临时禁用
        /// </summary>
        private void CheckColonyState()
        {
            /*
            // ? ???????????????????
            var snapshot = GameStateCache.GetCachedSnapshot();
            if (snapshot == null)
            {
                Log.Warning("[ColonyStateMonitor] ??????????????????");
                return;
            }

            var narrator = Current.Game?.GetComponent<NarratorManager>();
            if (narrator == null) return;

            // ??????????????????
            CheckColonistChanges(narrator, snapshot);
            CheckWealthGrowth(narrator, snapshot);
            CheckResourceStatus(narrator, snapshot);
            CheckCombatStatus(narrator, snapshot);
            CheckColonyMood(narrator, snapshot);
            CheckConsecutiveDays(narrator);
            */
        }

        // ? v1.6.46: 临时注释掉所有依赖 GameStateSnapshot 的方法

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
    /// ??????????? - ?????????????ж?
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
        /// ??????
        /// </summary>
        public void RecordConversation(bool hasUserMessage)
        {
            totalConversations++;
            
            var narrator = Current.Game?.GetComponent<NarratorManager>();
            if (narrator == null) return;

            // ??????????????10?ζ????
            if (totalConversations % 10 == 0 && hasUserMessage)
            {
                narrator.ModifyFavorability(1f, "???????????");
            }

            // ?????δ????????????
            int ticksSinceLastConversation = Find.TickManager.TicksGame - lastConversationTick;
            if (ticksSinceLastConversation > 360000) // >6С?
            {
                narrator.ModifyFavorability(-1f, "???????????");
            }

            lastConversationTick = Find.TickManager.TicksGame;
        }

        /// <summary>
        /// ??????????
        /// </summary>
        public void RecordIgnoredSuggestion()
        {
            ignoredSuggestions++;
            
            var narrator = Current.Game?.GetComponent<NarratorManager>();
            if (narrator == null) return;

            // ????????
            if (ignoredSuggestions >= 5)
            {
                narrator.ModifyFavorability(-3f, "??κ?????????");
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
