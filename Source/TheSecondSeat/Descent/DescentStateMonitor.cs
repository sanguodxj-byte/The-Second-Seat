using System;
using Verse;
using RimWorld;

namespace TheSecondSeat.Descent
{
    /// <summary>
    /// 降临状态监控器 - 负责检查实体状态并处理销毁
    /// </summary>
    public static class DescentStateMonitor
    {
        // 好感度惩罚参数
        private const float AFFINITY_PENALTY_COMBAT = 0.10f;
        private const float AFFINITY_PENALTY_NON_COMBAT = 0.50f;

        /// <summary>
        /// 检查是否应该强制销毁降临实体
        /// </summary>
        public static bool ShouldForceDestroy(Pawn mainPawn, Pawn companionPawn, bool wasInCombat, out bool isCombatReason)
        {
            isCombatReason = false;
            
            // 1. 检查主体状态
            if (mainPawn != null && CheckPawnStatus(mainPawn, wasInCombat, ref isCombatReason, "主体"))
            {
                return true;
            }

            // 2. 检查伴随生物状态 (如果存在)
            if (companionPawn != null && companionPawn.Spawned)
            {
                if (CheckPawnStatus(companionPawn, wasInCombat, ref isCombatReason, "伴随生物"))
                {
                    return true;
                }
            }
            
            return false;
        }

        /// <summary>
        /// 通用实体状态检查
        /// </summary>
        public static bool CheckPawnStatus(Pawn pawn, bool wasInCombat, ref bool isCombatReason, string label)
        {
            if (pawn == null || !pawn.Spawned) return false;

            // 检查死亡
            if (pawn.Dead)
            {
                isCombatReason = wasInCombat;
                Log.Message($"[DescentStateMonitor] {label}死亡，战斗状态: {wasInCombat}");
                return true;
            }
            
            // 检查睡眠
            if (pawn.CurJob?.def == JobDefOf.LayDown ||
                pawn.jobs?.curDriver?.asleep == true)
            {
                // 伴随生物睡觉不触发回归
                if (label == "伴随生物") return false;

                isCombatReason = false;
                Log.Message($"[DescentStateMonitor] {label}陷入睡眠状态");
                return true;
            }
            
            // 检查昏迷（心智状态）
            if (pawn.InMentalState || pawn.health?.Downed == true)
            {
                isCombatReason = wasInCombat;
                Log.Message($"[DescentStateMonitor] {label}昏迷/倒地，战斗状态: {wasInCombat}");
                return true;
            }
            
            // 检查束缚
            if (pawn.IsPrisoner || pawn.guest?.IsPrisoner == true)
            {
                isCombatReason = false;
                Log.Message($"[DescentStateMonitor] {label}被束缚/囚禁");
                return true;
            }
            
            // 检查无法行动
            if (!pawn.health?.capacities?.CapableOf(PawnCapacityDefOf.Moving) == true)
            {
                isCombatReason = wasInCombat;
                Log.Message($"[DescentStateMonitor] {label}无法移动，战斗状态: {wasInCombat}");
                return true;
            }

            return false;
        }

        /// <summary>
        /// 更新战斗状态追踪
        /// </summary>
        public static bool UpdateCombatStatus(Pawn pawn, bool currentStatus)
        {
            if (pawn == null || !pawn.Spawned)
            {
                return currentStatus;
            }
            
            // 检查是否正在战斗
            bool currentlyInCombat =
                pawn.CurJob?.def == JobDefOf.AttackMelee ||
                pawn.CurJob?.def == JobDefOf.AttackStatic ||
                pawn.mindState?.enemyTarget != null ||
                pawn.stances?.curStance is Stance_Warmup;
            
            // 一旦进入战斗，保持标记
            return currentStatus || currentlyInCombat;
        }

        /// <summary>
        /// 获取惩罚率
        /// </summary>
        public static float GetPenaltyRate(bool isCombatReason)
        {
            return isCombatReason ? AFFINITY_PENALTY_COMBAT : AFFINITY_PENALTY_NON_COMBAT;
        }
    }
}