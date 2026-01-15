using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace TheSecondSeat.Hediffs
{
    public class HediffCompProperties_Aura : HediffCompProperties
    {
        public float radius = 9.9f;
        public int checkInterval = 60; // Check every 60 ticks (1 second)
        public HediffDef effectHediff;
        
        // Target selection
        public bool affectEnemies = true;
        public bool affectAllies = false;
        public bool affectSelf = false;
        
        // Stacking behavior
        public bool addsStacks = false; // If true, adds to severity. If false, refreshes duration.
        public float severityAmount = 1.0f; // Amount to add if stacking, or initial severity
        public float maxSeverity = float.MaxValue; // Cap for stacking
        
        // Visuals
        public EffecterDef activeEffect;
        
        public HediffCompProperties_Aura()
        {
            this.compClass = typeof(HediffComp_Aura);
        }
    }

    public class HediffComp_Aura : HediffComp
    {
        public HediffCompProperties_Aura Props => (HediffCompProperties_Aura)props;
        
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
            if (Pawn.Map == null) return;
            
            // Self
            if (Props.affectSelf)
            {
                ApplyTo(Pawn);
            }

            // Area
            float radius = Props.radius;
            // Use RadialDistinctThingsAround for efficient spatial query
            var targets = GenRadial.RadialDistinctThingsAround(Pawn.Position, Pawn.Map, radius, true);
            
            foreach (var thing in targets)
            {
                if (thing is Pawn target && target != Pawn && !target.Dead && !target.Downed)
                {
                    bool isEnemy = target.HostileTo(Pawn);
                    if ((Props.affectEnemies && isEnemy) || (Props.affectAllies && !isEnemy))
                    {
                        ApplyTo(target);
                    }
                }
            }
        }

        private void ApplyTo(Pawn target)
        {
            if (Props.effectHediff == null) return;

            Hediff existing = target.health.hediffSet.GetFirstHediffOfDef(Props.effectHediff);
            
            if (existing != null)
            {
                if (Props.addsStacks)
                {
                    if (existing.Severity < Props.maxSeverity)
                    {
                        existing.Severity += Props.severityAmount;
                    }
                }
                else
                {
                    // Refresh duration logic: 
                    // Re-adding the hediff usually resets the CompDisappears timer in vanilla logic
                    target.health.AddHediff(Props.effectHediff, null, null);
                }
            }
            else
            {
                Hediff hediff = HediffMaker.MakeHediff(Props.effectHediff, target);
                hediff.Severity = Props.severityAmount;
                target.health.AddHediff(hediff, null, null);
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
