using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace TheSecondSeat
{
    public class JobDriver_CalamitySlam : JobDriver
    {
        private const TargetIndex VictimInd = TargetIndex.A;
        protected Pawn Victim => (Pawn)job.GetTarget(VictimInd).Thing;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(Victim, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() =>
            {
                // 检查施法者是否还处于“持有”状态
                var holdingHediffDefName = job.def.GetModExtension<DefModExtension_GrabJob>()?.holdingHediffDefName;
                if (string.IsNullOrEmpty(holdingHediffDefName)) return true;
                
                var holdingHediffDef = DefDatabase<HediffDef>.GetNamed(holdingHediffDefName, false);
                return holdingHediffDef == null || !pawn.health.hediffSet.HasHediff(holdingHediffDef);
            });

            Toil slamToil = Toils_General.DoAtomic(delegate
            {
                Pawn caster = pawn;
                
                // 找到“持有中”的 Hediff 并获取目标
                var holdingHediffDefName = job.def.GetModExtension<DefModExtension_GrabJob>().holdingHediffDefName;
                Hediff hediff = caster.health.hediffSet.GetFirstHediffOfDef(DefDatabase<HediffDef>.GetNamed(holdingHediffDefName));

                if (hediff is Hediff_CalamityHolding calamityHolding && calamityHolding.HeldTarget != null)
                {
                    Pawn victim = Victim; // Get victim from job target

                    // 计算伤害
                    float damageAmount = 80f; // 基础伤害
                    // 从伤害倍率 Hediff 获取倍率
                    Hediff bonusHediff = caster.health.hediffSet.GetFirstHediffOfDef(DefDatabase<HediffDef>.GetNamed("Sideria_CalamityThrowBonus", false));
                    if (bonusHediff != null)
                    {
                        damageAmount *= bonusHediff.Severity;
                        caster.health.RemoveHediff(bonusHediff); // 用完即删
                    }
                    
                    // 造成伤害
                    DamageInfo dinfo = new DamageInfo(DamageDefOf.Blunt, damageAmount, 999f, -1, caster, victim.health.hediffSet.GetBrain(), null, DamageInfo.SourceCategory.ThingOrUnknown, null, true, true);
                    victim.TakeDamage(dinfo);

                    // 视觉和声音效果
                    GenExplosion.DoExplosion(
                        center: victim.Position,
                        map: caster.Map,
                        radius: 1.5f,
                        damType: DamageDefOf.Blunt,
                        instigator: caster,
                        damAmount: (int)damageAmount / 2, // 爆炸伤害减半
                        armorPenetration: 0.5f,
                        explosionSound: SoundDefOf.Pawn_Melee_Punch_HitPawn
                    );
                    
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

            yield return slamToil;
        }
    }
}