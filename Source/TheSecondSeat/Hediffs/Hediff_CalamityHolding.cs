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

            // 如果施法者死亡或目标死亡/消失，则移除此 hediff
            // 注意：不再检查 pawn.Downed 或 HeldTarget.Downed，因为灾厄摔掷需要持有倒地目标
            if (pawn == null || pawn.Dead || HeldTarget == null || HeldTarget.Dead || !HeldTarget.Spawned)
            {
                Log.Message($"[CalamityHolding Hediff] Removing self because of invalid state. Pawn null? {pawn == null}, Pawn dead? {pawn?.Dead}, HeldTarget null? {HeldTarget == null}, HeldTarget dead? {HeldTarget?.Dead}");
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