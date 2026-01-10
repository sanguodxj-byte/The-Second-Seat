using System;
using System.Linq;
using RimWorld;
using Verse;

namespace TheSecondSeat.Components
{
    public class CompAbilityEffect_DestructiveRend : CompAbilityEffect
    {
        public new CompProperties_AbilityEffect Props => (CompProperties_AbilityEffect)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            Pawn targetPawn = target.Pawn;
            if (targetPawn == null) return;

            // 寻找已受伤的身体部位
            BodyPartRecord injuredPart = null;
            
            // 优先找损伤最严重的部位
            var injuredParts = targetPawn.health.hediffSet.hediffs
                .Where(h => h is Hediff_Injury && h.Part != null)
                .Select(h => h.Part)
                .Distinct();

            if (injuredParts.Any())
            {
                // 简单的策略：取第一个找到的受伤部位。
                // 也可以改进为找当前生命值最低的部位。
                injuredPart = injuredParts.FirstOrDefault();
            }

            // 如果没有受伤部位，随机选择一个部位（或者躯干）
            if (injuredPart == null)
            {
                injuredPart = targetPawn.health.hediffSet.GetNotMissingParts()
                    .FirstOrDefault(p => p.def == BodyPartDefOf.Torso || p.def == BodyPartDefOf.Head);
            }
            
            if (injuredPart == null) return; // 极罕见情况

            // 造成 1000 点伤害
            DamageInfo dinfo = new DamageInfo(
                DamageDefOf.Cut,
                1000f,
                999f, // Armor Penetration
                -1f,
                parent.pawn,
                injuredPart,
                parent.pawn.equipment?.Primary?.def
            );

            targetPawn.TakeDamage(dinfo);
            
            // 特效
            FleckMaker.ThrowMicroSparks(targetPawn.DrawPos, targetPawn.Map);
            FleckMaker.Static(targetPawn.DrawPos, targetPawn.Map, FleckDefOf.ExplosionFlash, 5f);
            
            Messages.Message($"{parent.pawn.LabelShort} used Destructive Rend on {targetPawn.LabelShort}!", targetPawn, MessageTypeDefOf.PositiveEvent);
        }
    }
}
