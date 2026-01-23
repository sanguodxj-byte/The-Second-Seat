using System.Collections.Generic;
using RimWorld;
using Verse;

namespace TheSecondSeat.Hediffs
{
    /// <summary>
    /// 猩红绽放光环组件属性
    /// 持续给范围内敌人叠加猩红绽放标记
    /// </summary>
    public class HediffCompProperties_CrimsonBloomAura : HediffCompProperties
    {
        public float radius = 9.9f;
        public int checkInterval = 60; // Check every 60 ticks (1 second)
        public HediffDef markHediff; // TSS_CrimsonBloomMark
        public bool affectEnemies = true;
        public bool affectAllies = false;
        public EffecterDef activeEffect;
        
        public HediffCompProperties_CrimsonBloomAura()
        {
            this.compClass = typeof(HediffComp_CrimsonBloomAura);
        }
    }

    /// <summary>
    /// 猩红绽放光环组件
    /// 每秒给范围内敌人叠加一层猩红绽放标记
    /// 与 HediffComp_CrimsonBloom.AddStack() 正确集成
    /// </summary>
    public class HediffComp_CrimsonBloomAura : HediffComp
    {
        public HediffCompProperties_CrimsonBloomAura Props => (HediffCompProperties_CrimsonBloomAura)props;
        
        private Effecter effecter;

        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);
            
            if (Pawn.IsHashIntervalTick(Props.checkInterval))
            {
                ApplyAura();
            }
            
            // Handle continuous visual effect
            if (Props.activeEffect != null)
            {
                if (effecter == null)
                {
                    effecter = Props.activeEffect.Spawn();
                    effecter.Trigger(Pawn, TargetInfo.Invalid);
                }
                effecter.EffectTick(Pawn, TargetInfo.Invalid);
            }
        }

        private void ApplyAura()
        {
            if (Pawn.Map == null || Props.markHediff == null) return;
            
            float radius = Props.radius;
            var targets = GenRadial.RadialDistinctThingsAround(Pawn.Position, Pawn.Map, radius, true);
            
            foreach (var thing in targets)
            {
                if (thing is Pawn target && target != Pawn && !target.Dead && !target.Downed)
                {
                    bool isEnemy = target.HostileTo(Pawn);
                    if ((Props.affectEnemies && isEnemy) || (Props.affectAllies && !isEnemy))
                    {
                        ApplyCrimsonBloomMark(target);
                    }
                }
            }
        }

        private void ApplyCrimsonBloomMark(Pawn target)
        {
            Hediff existing = target.health.hediffSet.GetFirstHediffOfDef(Props.markHediff);
            
            if (existing != null)
            {
                // 找到现有的 HediffComp_CrimsonBloom 并调用 AddStack
                var bloomComp = existing.TryGetComp<HediffComp_CrimsonBloom>();
                if (bloomComp != null)
                {
                    bloomComp.AddStack(Pawn);
                }
            }
            else
            {
                // 添加新的标记
                Hediff hediff = HediffMaker.MakeHediff(Props.markHediff, target);
                hediff.Severity = 0.33f; // 初始1层
                target.health.AddHediff(hediff, null, null);
                
                // 设置施放者
                var bloomComp = hediff.TryGetComp<HediffComp_CrimsonBloom>();
                if (bloomComp != null)
                {
                    // 使用反射设置 applierPawn，或者在 AddStack 中处理
                    // AddStack 会同时设置 applierPawn
                }
            }
        }
        
        public override void CompPostPostRemoved()
        {
            base.CompPostPostRemoved();
            if (effecter != null)
            {
                effecter.Cleanup();
                effecter = null;
            }
        }
    }
}