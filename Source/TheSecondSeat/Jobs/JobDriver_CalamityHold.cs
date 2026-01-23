using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace TheSecondSeat
{
    public class JobDriver_CalamityHold : JobDriver
    {
        private const TargetIndex VictimInd = TargetIndex.A;

        protected Pawn Victim => (Pawn)job.GetTarget(VictimInd).Thing;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(Victim, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDestroyedOrNull(VictimInd);
            this.FailOnAggroMentalState(VictimInd);
            // 注意：不再检查 Victim.Downed，因为灾厄摔掷需要先让目标倒地再抓取
            this.FailOn(() => Victim.Dead);
            
            var extension = job.def.GetModExtension<DefModExtension_GrabJob>();
            if (extension == null)
            {
                Log.Error($"[CalamityHold] JobDef {job.def.defName} is missing DefModExtension_GrabJob.");
                yield return Toils_General.Wait(1); // Fail gracefully
                yield break;
            }

            // 1. 移动到目标
            yield return Toils_Goto.GotoThing(VictimInd, PathEndMode.Touch);

            // 2. 执行抓取
            Toil grab = Toils_General.DoAtomic(delegate
            {
                // 给目标添加“被抓取”状态
                if (extension.GrabbedHediff != null)
                {
                    Hediff hediff = HediffMaker.MakeHediff(extension.GrabbedHediff, Victim, null);
                    Victim.health.AddHediff(hediff);
                }

                // 给自己添加“持有中”状态
                if (extension.HoldingHediff != null)
                {
                    // 检查是否已经是持有状态，避免重复添加
                    if (!pawn.health.hediffSet.HasHediff(extension.HoldingHediff))
                    {
                        Log.Message($"[CalamityHold] Adding HoldingHediff ({extension.HoldingHediff.defName}) to {pawn.LabelShort}.");
                        Hediff_CalamityHolding holdingHediff = (Hediff_CalamityHolding)HediffMaker.MakeHediff(extension.HoldingHediff, pawn, null);
                        holdingHediff.HeldTarget = Victim; // 关键：将被抓取者与 Hediff 关联
                        pawn.health.AddHediff(holdingHediff);
                        Log.Message($"[CalamityHold] Hediff added. Pawn now has hediff? {pawn.health.hediffSet.HasHediff(extension.HoldingHediff)}");
                    }
                    else
                    {
                        // 如果已经存在，更新目标
                        Hediff_CalamityHolding existingHediff = (Hediff_CalamityHolding)pawn.health.hediffSet.GetFirstHediffOfDef(extension.HoldingHediff);
                        if (existingHediff != null)
                        {
                            existingHediff.HeldTarget = Victim;
                        }
                    }
                }
            });
            yield return grab;

            // 3. 等待玩家指令
            Toil wait = new Toil();
            wait.initAction = () =>
            {
                // 确保目标被正确地拉到施法者身边
                if (Victim.Spawned && pawn.Spawned && Victim.Map == pawn.Map && Victim.Position != pawn.Position)
                {
                    Victim.Position = pawn.Position;
                    Victim.pather.StopDead();
                }
                pawn.pather.StopDead();
            };
            wait.tickAction = () =>
            {
                // 持续将目标保持在施法者位置
                if (Victim != null && Victim.Spawned && pawn.Spawned && Victim.Position != pawn.Position)
                {
                    Victim.Position = pawn.Position;
                }
                
                // 如果“持有中”状态被任何原因移除了，则结束 Job
                if (!pawn.health.hediffSet.HasHediff(extension.HoldingHediff))
                {
                    Log.Message($"[CalamityHold] HoldingHediff is gone! Ending job.");
                    EndJobWith(JobCondition.Succeeded);
                }
            };
            wait.defaultCompleteMode = ToilCompleteMode.Never;
            wait.handlingFacing = true;
            yield return wait;
        }
    }
}