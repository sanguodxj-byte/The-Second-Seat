using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace TheSecondSeat
{
    /// <summary>
    /// CompProperties for the calamity hold action gizmos.
    /// Configured via XML with JobDef references for throw/slam.
    /// </summary>
    public class CompProperties_CalamityHoldActions : CompProperties
    {
        public string throwJobDefName = "TSS_CalamityThrow";
        public string slamJobDefName = "TSS_CalamitySlam";
        
        public CompProperties_CalamityHoldActions()
        {
            compClass = typeof(Comp_CalamityHoldActions);
        }
    }

    /// <summary>
    /// Component that provides Throw and Slam gizmos when the pawn is holding a target.
    /// Dynamically attached to the pawn during CalamityHold job execution.
    /// </summary>
    public class Comp_CalamityHoldActions : ThingComp
    {
        private JobDef throwJobDef;
        private JobDef slamJobDef;
        
        public CompProperties_CalamityHoldActions Props => (CompProperties_CalamityHoldActions)props;
        
        /// <summary>
        /// Get the held target from Hediff_CalamityHolding.
        /// This ensures we always use the same target as the Hediff.
        /// </summary>
        private Pawn GetHeldTargetFromHediff()
        {
            Pawn pawn = parent as Pawn;
            if (pawn == null)
                return null;
            
            // Find any Hediff_CalamityHolding on this pawn
            foreach (Hediff hediff in pawn.health.hediffSet.hediffs)
            {
                if (hediff is Hediff_CalamityHolding holdingHediff && holdingHediff.HeldTarget != null)
                {
                    return holdingHediff.HeldTarget;
                }
            }
            return null;
        }
        
        /// <summary>
        /// Check if currently holding a target (via Hediff_CalamityHolding).
        /// </summary>
        public bool IsHoldingTarget
        {
            get
            {
                Pawn target = GetHeldTargetFromHediff();
                return target != null && !target.Dead && !target.Destroyed;
            }
        }
        
        private JobDef ThrowJobDef
        {
            get
            {
                if (throwJobDef == null && !string.IsNullOrEmpty(Props.throwJobDefName))
                {
                    throwJobDef = DefDatabase<JobDef>.GetNamed(Props.throwJobDefName, false);
                }
                return throwJobDef;
            }
        }
        
        private JobDef SlamJobDef
        {
            get
            {
                if (slamJobDef == null && !string.IsNullOrEmpty(Props.slamJobDefName))
                {
                    slamJobDef = DefDatabase<JobDef>.GetNamed(Props.slamJobDefName, false);
                }
                return slamJobDef;
            }
        }
        
        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            // Only show gizmos when holding a target
            if (!IsHoldingTarget)
                yield break;
            
            Pawn pawn = parent as Pawn;
            if (pawn == null || pawn.Dead || !pawn.Spawned)
                yield break;
            
            // Throw button - requires target selection
            if (ThrowJobDef != null)
            {
                Command_Target throwCommand = new Command_Target();
                throwCommand.defaultLabel = "TSS_CalamityThrow_Label".Translate();
                throwCommand.defaultDesc = "TSS_CalamityThrow_Desc".Translate();
                throwCommand.icon = ContentFinder<Texture2D>.Get("UI/Commands/Attack", false) ?? BaseContent.BadTex;
                throwCommand.targetingParams = new TargetingParameters
                {
                    canTargetLocations = true,
                    canTargetPawns = false,
                    canTargetBuildings = false
                };
                throwCommand.action = (LocalTargetInfo target) =>
                {
                    DoThrowAction(pawn, target);
                };
                throwCommand.hotKey = KeyBindingDefOf.Misc1;
                
                yield return throwCommand;
            }
            
            // Slam button - instant action
            if (SlamJobDef != null)
            {
                Command_Action slamCommand = new Command_Action();
                slamCommand.defaultLabel = "TSS_CalamitySlam_Label".Translate();
                slamCommand.defaultDesc = "TSS_CalamitySlam_Desc".Translate();
                slamCommand.icon = ContentFinder<Texture2D>.Get("UI/Commands/AttackMelee", false) ?? BaseContent.BadTex;
                slamCommand.action = () =>
                {
                    DoSlamAction(pawn);
                };
                slamCommand.hotKey = KeyBindingDefOf.Misc2;
                
                yield return slamCommand;
            }
        }
        
        private void DoThrowAction(Pawn pawn, LocalTargetInfo target)
        {
            Pawn heldTarget = GetHeldTargetFromHediff();
            if (heldTarget == null || ThrowJobDef == null)
                return;
            
            Log.Message($"[CalamityHoldActions] DoThrowAction: throwing {heldTarget.LabelShort} to {target}");
            
            // End the current hold job
            pawn.jobs.EndCurrentJob(JobCondition.InterruptForced, true);
            
            // Start the throw job
            Job throwJob = JobMaker.MakeJob(ThrowJobDef, heldTarget, target);
            pawn.jobs.StartJob(throwJob, JobCondition.InterruptForced);
        }
        
        private void DoSlamAction(Pawn pawn)
        {
            Pawn heldTarget = GetHeldTargetFromHediff();
            if (heldTarget == null || SlamJobDef == null)
                return;
            
            Log.Message($"[CalamityHoldActions] DoSlamAction: slamming {heldTarget.LabelShort}");
            
            // End the current hold job
            pawn.jobs.EndCurrentJob(JobCondition.InterruptForced, true);
            
            // Start the slam job
            Job slamJob = JobMaker.MakeJob(SlamJobDef, heldTarget);
            pawn.jobs.StartJob(slamJob, JobCondition.InterruptForced);
        }
    }
}