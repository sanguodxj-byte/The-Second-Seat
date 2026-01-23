using System;
using System.Collections.Generic;
using HarmonyLib;
using Verse;
using Verse.AI;
using RimWorld;

namespace TheSecondSeat.Monitoring
{
    /// <summary>
    /// 游戏事件 Harmony Patches
    /// 自动捕获游戏中的关键事件并发送到 EventAggregator
    /// 
    /// 设计原则：
    /// - 只捕获关键事件，避免性能影响
    /// - 使用 Postfix 避免影响原有逻辑
    /// - 事件描述简短，减少 Token 消耗
    /// </summary>
    [HarmonyPatch]
    public static class GameEventPatches
    {
        private static EventAggregator GetAggregator()
        {
            return Current.Game?.GetComponent<EventAggregator>();
        }
        
        // ========== 战斗事件 ==========
        
        /// <summary>
        /// 捕获 Pawn 死亡事件
        /// </summary>
        [HarmonyPatch(typeof(Pawn), nameof(Pawn.Kill))]
        [HarmonyPostfix]
        public static void Pawn_Kill_Postfix(Pawn __instance, DamageInfo? dinfo)
        {
            try
            {
                var aggregator = GetAggregator();
                if (aggregator == null) return;
                
                string pawnName = __instance.Name?.ToStringShort ?? __instance.LabelShort;
                bool isColonist = __instance.Faction?.IsPlayer == true && __instance.RaceProps?.Humanlike == true;
                
                if (isColonist)
                {
                    // 殖民者死亡 - 关键事件
                    aggregator.RecordEvent(new GameEvent
                    {
                        Description = $"殖民者 {pawnName} 死亡",
                        Category = EventCategory.Health,
                        Priority = EventPriority.Critical,
                        Timestamp = Find.TickManager.TicksGame,
                        Tags = new List<string> { "death", "colonist" },
                        PawnId = __instance.ThingID
                    });
                }
                else if (__instance.Faction?.HostileTo(Faction.OfPlayer) == true)
                {
                    // 敌人死亡 - 低优先级
                    aggregator.RecordEvent(new GameEvent
                    {
                        Description = $"击杀敌人 {pawnName}",
                        Category = EventCategory.Combat,
                        Priority = EventPriority.Low,
                        Timestamp = Find.TickManager.TicksGame,
                        Tags = new List<string> { "kill", "enemy" }
                    });
                }
            }
            catch { /* 静默处理 */ }
        }
        
        /// <summary>
        /// 捕获袭击事件
        /// </summary>
        [HarmonyPatch(typeof(IncidentWorker_RaidEnemy), "TryExecuteWorker")]
        [HarmonyPostfix]
        public static void RaidEnemy_Postfix(bool __result, IncidentParms parms)
        {
            if (!__result) return;
            
            try
            {
                var aggregator = GetAggregator();
                if (aggregator == null) return;
                
                string factionName = parms.faction?.Name ?? "未知势力";
                int points = (int)parms.points;
                
                aggregator.RecordEvent(new GameEvent
                {
                    Description = $"{factionName} 发起袭击 (威胁点数: {points})",
                    Category = EventCategory.Combat,
                    Priority = EventPriority.Critical,
                    Timestamp = Find.TickManager.TicksGame,
                    Tags = new List<string> { "raid", "threat" }
                });
            }
            catch { /* 静默处理 */ }
        }
        
        // ========== 健康事件 ==========
        
        /// <summary>
        /// 捕获疾病事件
        /// </summary>
        [HarmonyPatch(typeof(HediffSet), nameof(HediffSet.AddDirect))]
        [HarmonyPostfix]
        public static void HediffSet_AddDirect_Postfix(HediffSet __instance, Hediff hediff)
        {
            try
            {
                var aggregator = GetAggregator();
                if (aggregator == null) return;
                
                Pawn pawn = __instance.pawn;
                if (pawn?.Faction?.IsPlayer != true || pawn.RaceProps?.Humanlike != true) return;
                
                // 只关注疾病和严重伤害
                bool isDisease = hediff.def?.makesSickThought == true;
                bool isSeriousInjury = hediff is Hediff_Injury injury && injury.Severity > 10;
                
                if (isDisease)
                {
                    string pawnName = pawn.Name?.ToStringShort ?? pawn.LabelShort;
                    aggregator.RecordEvent(new GameEvent
                    {
                        Description = $"{pawnName} 患上 {hediff.Label}",
                        Category = EventCategory.Health,
                        Priority = EventPriority.High,
                        Timestamp = Find.TickManager.TicksGame,
                        Tags = new List<string> { "disease" },
                        PawnId = pawn.ThingID
                    });
                }
                else if (isSeriousInjury)
                {
                    string pawnName = pawn.Name?.ToStringShort ?? pawn.LabelShort;
                    aggregator.RecordEvent(new GameEvent
                    {
                        Description = $"{pawnName} 受到严重伤害",
                        Category = EventCategory.Health,
                        Priority = EventPriority.Normal,
                        Timestamp = Find.TickManager.TicksGame,
                        Tags = new List<string> { "injury" },
                        PawnId = pawn.ThingID
                    });
                }
            }
            catch { /* 静默处理 */ }
        }
        
        // ========== 心情事件 ==========
        
        /// <summary>
        /// 捕获精神崩溃事件
        /// </summary>
        [HarmonyPatch(typeof(MentalStateHandler), nameof(MentalStateHandler.TryStartMentalState))]
        [HarmonyPostfix]
        public static void MentalState_Postfix(bool __result, MentalStateHandler __instance, MentalStateDef stateDef)
        {
            if (!__result) return;
            
            try
            {
                var aggregator = GetAggregator();
                if (aggregator == null) return;
                
                // 使用 Traverse 获取 pawn 字段 (防止访问级别问题)
                Pawn pawn = Traverse.Create(__instance).Field("pawn").GetValue<Pawn>();
                if (pawn?.Faction?.IsPlayer != true) return;
                
                string pawnName = pawn.Name?.ToStringShort ?? pawn.LabelShort;
                string stateName = stateDef?.label ?? "精神崩溃";
                
                aggregator.RecordEvent(new GameEvent
                {
                    Description = $"{pawnName} 进入 {stateName} 状态",
                    Category = EventCategory.Mood,
                    Priority = EventPriority.High,
                    Timestamp = Find.TickManager.TicksGame,
                    Tags = new List<string> { "breakdown", "mental" },
                    PawnId = pawn.ThingID
                });
            }
            catch { /* 静默处理 */ }
        }
        
        /// <summary>
        /// 捕获灵感事件
        /// </summary>
        [HarmonyPatch(typeof(InspirationHandler), nameof(InspirationHandler.TryStartInspiration))]
        [HarmonyPostfix]
        public static void Inspiration_Postfix(bool __result, InspirationHandler __instance, InspirationDef def)
        {
            if (!__result) return;
            
            try
            {
                var aggregator = GetAggregator();
                if (aggregator == null) return;
                
                Pawn pawn = __instance.pawn;
                if (pawn?.Faction?.IsPlayer != true) return;
                
                string pawnName = pawn.Name?.ToStringShort ?? pawn.LabelShort;
                
                aggregator.RecordEvent(new GameEvent
                {
                    Description = $"{pawnName} 获得灵感",
                    Category = EventCategory.Mood,
                    Priority = EventPriority.Low,
                    Timestamp = Find.TickManager.TicksGame,
                    Tags = new List<string> { "inspiration" },
                    PawnId = pawn.ThingID
                });
            }
            catch { /* 静默处理 */ }
        }
        
        // ========== 社交事件 ==========
        
        /// <summary>
        /// 捕获社交互动事件（只捕获重要的）
        /// </summary>
        [HarmonyPatch(typeof(InteractionWorker_MarriageProposal), nameof(InteractionWorker_MarriageProposal.Interacted))]
        [HarmonyPostfix]
        public static void Marriage_Postfix(Pawn initiator, Pawn recipient)
        {
            try
            {
                var aggregator = GetAggregator();
                if (aggregator == null) return;
                
                if (initiator?.Faction?.IsPlayer != true && recipient?.Faction?.IsPlayer != true) return;
                
                string name1 = initiator?.Name?.ToStringShort ?? "某人";
                string name2 = recipient?.Name?.ToStringShort ?? "某人";
                
                aggregator.RecordEvent(new GameEvent
                {
                    Description = $"{name1} 向 {name2} 求婚",
                    Category = EventCategory.Social,
                    Priority = EventPriority.High,
                    Timestamp = Find.TickManager.TicksGame,
                    Tags = new List<string> { "positive", "marriage" }
                });
            }
            catch { /* 静默处理 */ }
        }
        
        // ========== 资源事件 ==========
        
        /// <summary>
        /// 捕获资源短缺警报
        /// </summary>
        [HarmonyPatch(typeof(Alert_StarvationColonists), nameof(Alert_StarvationColonists.GetReport))]
        [HarmonyPostfix]
        public static void Starvation_Postfix(Alert_StarvationColonists __instance, AlertReport __result)
        {
            try
            {
                if (!__result.active) return;
                
                var aggregator = GetAggregator();
                if (aggregator == null) return;
                
                // 使用冷却机制避免重复记录
                aggregator.RecordEvent(new GameEvent
                {
                    Description = "殖民者正在挨饿",
                    Category = EventCategory.Resource,
                    Priority = EventPriority.Critical,
                    Timestamp = Find.TickManager.TicksGame,
                    Tags = new List<string> { "shortage", "food" }
                });
            }
            catch { /* 静默处理 */ }
        }
        
        // ========== 建筑事件 ==========
        
        /// <summary>
        /// 捕获建筑完成事件
        /// </summary>
        [HarmonyPatch(typeof(Frame), nameof(Frame.CompleteConstruction))]
        [HarmonyPostfix]
        public static void Construction_Postfix(Frame __instance, Pawn worker)
        {
            try
            {
                var aggregator = GetAggregator();
                if (aggregator == null) return;
                
                // 只记录重要建筑
                string defName = __instance.def?.entityDefToBuild?.defName ?? "";
                bool isImportant = defName.Contains("Turret") || 
                                   defName.Contains("Generator") || 
                                   defName.Contains("Research") ||
                                   defName.Contains("Hospital");
                
                if (isImportant)
                {
                    aggregator.RecordEvent(new GameEvent
                    {
                        Description = $"完成建造 {__instance.def?.entityDefToBuild?.label ?? "建筑"}",
                        Category = EventCategory.Construction,
                        Priority = EventPriority.Low,
                        Timestamp = Find.TickManager.TicksGame,
                        Tags = new List<string> { "completed" }
                    });
                }
            }
            catch { /* 静默处理 */ }
        }
        
        // ========== 贸易事件 ==========
        
        /// <summary>
        /// 捕获商队到达事件
        /// </summary>
        [HarmonyPatch(typeof(IncidentWorker_TraderCaravanArrival), "TryExecuteWorker")]
        [HarmonyPostfix]
        public static void TraderArrival_Postfix(bool __result, IncidentParms parms)
        {
            if (!__result) return;
            
            try
            {
                var aggregator = GetAggregator();
                if (aggregator == null) return;
                
                string factionName = parms.faction?.Name ?? "商队";
                
                aggregator.RecordEvent(new GameEvent
                {
                    Description = $"{factionName} 商队到达",
                    Category = EventCategory.Trade,
                    Priority = EventPriority.Normal,
                    Timestamp = Find.TickManager.TicksGame,
                    Tags = new List<string> { "trader", "arrival" }
                });
            }
            catch { /* 静默处理 */ }
        }
    }
}
