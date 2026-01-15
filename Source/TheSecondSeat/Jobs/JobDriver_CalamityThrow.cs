using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace TheSecondSeat
{
    public class JobDriver_CalamityThrow : JobDriver
    {
        private const TargetIndex DestinationInd = TargetIndex.A;
        private const TargetIndex VictimInd = TargetIndex.B;

        protected Pawn Victim => (Pawn)job.GetTarget(VictimInd).Thing;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.GetTarget(DestinationInd), job, 1, -1, null, errorOnFailed) && pawn.Reserve(Victim, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() =>
            {
                // 检查施法者是否还处于"持有"状态
                var holdingHediffDef = job.def.GetModExtension<DefModExtension_GrabJob>()?.holdingHediffDefName;
                if (string.IsNullOrEmpty(holdingHediffDef) || !pawn.health.hediffSet.HasHediff(DefDatabase<HediffDef>.GetNamed(holdingHediffDef)))
                {
                    return true;
                }
                return false;
            });
            
            // 直接执行投掷 - 施法者不需要移动，只是把受害者扔向目的地
            Toil throwToil = Toils_General.DoAtomic(delegate
            {
                Pawn caster = pawn;
                IntVec3 targetCell = job.GetTarget(DestinationInd).Cell;

                // 找到“持有中”的 Hediff 并获取目标
                Hediff hediff = caster.health.hediffSet.GetFirstHediffOfDef(
                    DefDatabase<HediffDef>.GetNamed(
                        job.def.GetModExtension<DefModExtension_GrabJob>().holdingHediffDefName));
                
                if (hediff is Hediff_CalamityHolding calamityHolding && calamityHolding.HeldTarget != null)
                {
                    Pawn victim = Victim; // Get victim from job target

                    // 创建 PawnFlyer
                    ThingDef flyerDef = DefDatabase<ThingDef>.GetNamed("Sideria_PawnFlyer_Calamity");
                    PawnFlyer_CalamityThrow flyer = (PawnFlyer_CalamityThrow)ThingMaker.MakeThing(flyerDef);
                    flyer.Initialize(caster, targetCell, victim);
                    GenSpawn.Spawn(flyer, victim.Position, caster.Map, WipeMode.Vanish);
                    
                    // 移除相关 hediffs
                    caster.health.RemoveHediff(hediff);
                    
                    var grabbedHediffDef = job.def.GetModExtension<DefModExtension_GrabJob>().GrabbedHediff;
                    if (grabbedHediffDef != null)
                    {
                        Hediff grabbedHediff = victim.health.hediffSet.GetFirstHediffOfDef(grabbedHediffDef);
                        if (grabbedHediff != null)
                        {
                            victim.health.RemoveHediff(grabbedHediff);
                        }
                    }
                }
            });

            yield return throwToil;
        }
    }
}
