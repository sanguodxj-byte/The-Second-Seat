using RimWorld;
using Verse;
using UnityEngine;

namespace TheSecondSeat.Components
{
    public class CompAbilityEffect_GoldenBeam : CompAbilityEffect
    {
        public new CompProperties_AbilityEffect Props => (CompProperties_AbilityEffect)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            
            // 获取目标位置
            IntVec3 cell = target.Cell;
            Map map = parent.pawn.Map;

            if (cell.IsValid && map != null)
            {
                // 定义光束属性
                // 注意：我们在XML中定义了 Sideria_GoldenPowerBeam 作为 ThingDef
                ThingDef beamDef = DefDatabase<ThingDef>.GetNamed("Sideria_GoldenPowerBeam", false);
                
                if (beamDef == null)
                {
                    // 如果找不到自定义光束，回退到原版 PowerBeam
                    beamDef = ThingDefOf.PowerBeam;
                    Log.Warning("[TheSecondSeat] Sideria_GoldenPowerBeam not found, falling back to PowerBeam.");
                }

                // 创建光束
                OrbitalStrike beam = (OrbitalStrike)GenSpawn.Spawn(beamDef, cell, map);
                
                // 设置光束持续时间和伤害
                // 这里的 duration 是 ticks。原版 PowerBeam 是 600 ticks (10秒)
                beam.duration = 600; 
                
                // 设置本能者，这样伤害会归因于龙
                beam.instigator = parent.pawn;
                
                // 开始打击
                beam.StartStrike();

                // 播放音效（如果 Def 中未处理）
                // SoundDefOf.OrbitalBeam.PlayOneShot(new TargetInfo(cell, map));
            }
        }

        public override bool CanApplyOn(LocalTargetInfo target, LocalTargetInfo dest)
        {
            return target.IsValid;
        }
    }
}
