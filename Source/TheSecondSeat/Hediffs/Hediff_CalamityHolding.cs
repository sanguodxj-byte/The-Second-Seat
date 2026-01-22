using RimWorld;
using Verse;

namespace TheSecondSeat
{
    /// <summary>
    /// 自定义 Hediff，用于在施法者身上标记“持有”状态，并存储被持有的目标。
    /// </summary>
    public class Hediff_CalamityHolding : HediffWithComps
    {
        public Pawn HeldTarget;

        public override string LabelInBrackets => HeldTarget?.LabelShort ?? base.LabelInBrackets;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref HeldTarget, "HeldTarget");
        }

        public override void Tick()
        {
            base.Tick();

            // 如果施法者死亡/倒地或目标死亡/销毁，则移除此 hediff
            // 注意1：检查 pawn.Downed，因为施法者倒地无法维持持有
            // 注意2：不检查 HeldTarget.Downed，因为允许持有倒地目标
            // 注意3：不检查 HeldTarget.Spawned，因为被持有的目标可能处于 DeSpawned 状态
            if (pawn == null || pawn.Dead || pawn.Downed || HeldTarget == null || HeldTarget.Dead || HeldTarget.Destroyed)
            {
                // 仅在调试模式或特定情况下记录，避免刷屏
                // Log.Message($"[CalamityHolding Hediff] Removing self because of invalid state...");
                pawn.health.RemoveHediff(this);
            }
        }
        
        public override void PostRemoved()
        {
            base.PostRemoved();
            Log.Message($"[CalamityHolding Hediff] PostRemoved called for {pawn?.LabelShort}.");
            // 确保在移除时，如果目标仍然被标记为“被抓取”，则清除该状态
            // JobDrivers 和 HediffComp 会处理这个逻辑，这里只负责停止当前job
            if (pawn.CurJob != null && pawn.CurJob.def.GetModExtension<DefModExtension_GrabJob>() != null)
            {
                pawn.jobs.EndCurrentJob(Verse.AI.JobCondition.InterruptForced, true);
            }
        }
    }
}
