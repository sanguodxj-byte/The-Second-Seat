using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace TheSecondSeat
{
    /// <summary>
    /// HediffCompProperties for the calamity hold action gizmos.
    /// Configured via XML with JobDef references for throw/slam.
    /// </summary>
    public class HediffCompProperties_CalamityHoldActions : HediffCompProperties
    {
        public string throwJobDefName = "TSS_CalamityThrow";
        public string slamJobDefName = "TSS_CalamitySlam";
        
        public HediffCompProperties_CalamityHoldActions()
        {
            compClass = typeof(HediffComp_CalamityHoldActions);
        }
    }

    /// <summary>
    /// HediffComp that provides Throw and Slam gizmos when the pawn is holding a target.
    /// Attached via Hediff_CalamityHolding during CalamityHold job execution.
    /// Using HediffComp instead of ThingComp because Hediffs can be dynamically added/removed
    /// and their Gizmos will be properly refreshed.
    /// </summary>
    public class HediffComp_CalamityHoldActions : HediffComp
    {
        private JobDef throwJobDef;
        private JobDef slamJobDef;
        
        public HediffCompProperties_CalamityHoldActions Props => 
            (HediffCompProperties_CalamityHoldActions)props;
        
        /// <summary>
        /// Get the held target from the parent hediff.
        /// </summary>
        public Pawn HeldTarget
        {
            get
            {
                if (this.parent is Hediff_CalamityHolding holdingHediff)
                {
                    return holdingHediff.HeldTarget;
                }
                return null;
            }
        }
        
        /// <summary>
        /// Check if currently holding a valid target.
        /// </summary>
        public bool IsHoldingTarget => HeldTarget != null && !HeldTarget.Dead && !HeldTarget.Destroyed;
        
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
        
        public override IEnumerable<Gizmo> CompGetGizmos()
        {
            // Debug: Log when this method is called
            Log.Message($"[TSS HediffComp_CalamityHoldActions] CompGetGizmos called for {Pawn?.LabelShort ?? "null"}");
            
            // Only show gizmos when holding a target
            if (!IsHoldingTarget)
            {
                // 深入调试 IsHoldingTarget
                if (HeldTarget == null)
                {
                    Log.Error("[TSS HediffComp_CalamityHoldActions] HeldTarget is NULL.");
                }
                else
                {
                    Log.Error($"[TSS HediffComp_CalamityHoldActions] HeldTarget ({HeldTarget.LabelShort}) state is invalid. Dead: {HeldTarget.Dead}, Destroyed: {HeldTarget.Destroyed}");
                }
                yield break;
            }
            
            Pawn pawn = Pawn;
            if (pawn == null || pawn.Dead || !pawn.Spawned)
            {
                Log.Message($"[TSS HediffComp_CalamityHoldActions] Pawn invalid: null={pawn == null}, Dead={pawn?.Dead}, Spawned={pawn?.Spawned}");
                yield break;
            }
            
            // Only show for player faction pawns (降临体可能不是标准殖民者，但仍属于玩家阵营)
            if (pawn.Faction == null || !pawn.Faction.IsPlayer)
            {
                Log.Message($"[TSS HediffComp_CalamityHoldActions] Not player faction: {pawn.Faction?.Name ?? "null"}");
                yield break;
            }
            
            Log.Message($"[TSS HediffComp_CalamityHoldActions] Showing gizmos for {pawn.LabelShort}, HeldTarget: {HeldTarget?.LabelShort}");
            
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
            Pawn heldTarget = HeldTarget;
            if (heldTarget == null || ThrowJobDef == null)
                return;
            
            // End the current hold job
            pawn.jobs.EndCurrentJob(JobCondition.InterruptForced, true);
            
            // Start the throw job with the destination as TargetA and held target as TargetB
            Job throwJob = JobMaker.MakeJob(ThrowJobDef, target, heldTarget);
            pawn.jobs.StartJob(throwJob, JobCondition.InterruptForced);
        }
        
        private void DoSlamAction(Pawn pawn)
        {
            Pawn heldTarget = HeldTarget;
            if (heldTarget == null || SlamJobDef == null)
                return;
            
            // End the current hold job
            pawn.jobs.EndCurrentJob(JobCondition.InterruptForced, true);
            
            // Start the slam job with the held target
            Job slamJob = JobMaker.MakeJob(SlamJobDef, heldTarget);
            pawn.jobs.StartJob(slamJob, JobCondition.InterruptForced);
        }
    }
}