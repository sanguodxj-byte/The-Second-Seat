using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;
using RimWorld;
using UnityEngine;
using TheSecondSeat.NaturalLanguage;

namespace TheSecondSeat.Commands.Implementations
{
    /// <summary>
    /// Toggle draft status for a colonist
    /// </summary>
    public class DraftPawnCommand : BaseAICommand
    {
        public override string ActionName => "DraftPawn";

        public override string GetDescription()
        {
            return "Toggle draft status for a specific colonist";
        }

        public override bool Execute(string? target = null, object? parameters = null)
        {
            if (string.IsNullOrEmpty(target))
            {
                LogError("Target colonist name is required");
                return false;
            }

            var pawn = Find.CurrentMap.mapPawns.FreeColonists
                .FirstOrDefault(p => p.Name.ToStringFull.Contains(target) || 
                                   p.LabelShort.Equals(target, StringComparison.OrdinalIgnoreCase));

            if (pawn == null)
            {
                LogError($"Colonist '{target}' not found");
                return false;
            }

            if (pawn.drafter == null)
            {
                LogError($"Colonist '{target}' cannot be drafted");
                return false;
            }

            // Toggle draft status
            bool newDraftStatus = !pawn.drafter.Drafted;
            pawn.drafter.Drafted = newDraftStatus;

            LogExecution($"Draft status for {pawn.LabelShort} set to {newDraftStatus}");
            return true;
        }
    }

    /// <summary>
    /// Order a drafted colonist to move to a location
    /// </summary>
    public class MovePawnCommand : BaseAICommand
    {
        public override string ActionName => "MovePawn";

        public override string GetDescription()
        {
            return "Order a drafted colonist to move to a specific location (x, z)";
        }

        public override bool Execute(string? target = null, object? parameters = null)
        {
            if (string.IsNullOrEmpty(target))
            {
                LogError("Target colonist name is required");
                return false;
            }

            var pawn = Find.CurrentMap.mapPawns.FreeColonists
                .FirstOrDefault(p => p.Name.ToStringFull.Contains(target) || 
                                   p.LabelShort.Equals(target, StringComparison.OrdinalIgnoreCase));

            if (pawn == null)
            {
                LogError($"Colonist '{target}' not found");
                return false;
            }

            if (!pawn.Drafted)
            {
                LogError($"Colonist '{target}' must be drafted to move");
                return false;
            }

            if (parameters is Dictionary<string, object> locationData &&
                locationData.TryGetValue("x", out var xObj) &&
                locationData.TryGetValue("z", out var zObj))
            {
                int x = Convert.ToInt32(xObj);
                int z = Convert.ToInt32(zObj);
                IntVec3 dest = new IntVec3(x, 0, z);

                if (!dest.InBounds(pawn.Map))
                {
                    LogError($"Destination {dest} is out of bounds");
                    return false;
                }

                Job job = JobMaker.MakeJob(JobDefOf.Goto, dest);
                pawn.jobs.TryTakeOrderedJob(job);
                
                // ? Visual feedback
                FleckMaker.ThrowMetaIcon(dest, pawn.Map, FleckDefOf.FeedbackGoto);
                
                LogExecution($"Ordered {pawn.LabelShort} to move to {dest}");
                return true;
            }

            LogError("Invalid location parameters");
            return false;
        }
    }

    /// <summary>
    /// Attempt to heal a pawn (using debug/god mode or assigning doctor)
    /// Currently implements a cheat heal for testing/god mode purposes as requested
    /// </summary>
    public class HealPawnCommand : BaseAICommand
    {
        public override string ActionName => "HealPawn";

        public override string GetDescription()
        {
            return "Heal a specific pawn (God Mode)";
        }

        public override bool Execute(string? target = null, object? parameters = null)
        {
            if (string.IsNullOrEmpty(target))
            {
                LogError("Target pawn name is required");
                return false;
            }

            var pawn = Find.CurrentMap.mapPawns.AllPawns
                .FirstOrDefault(p => p.Name.ToStringFull.Contains(target) || 
                                   p.LabelShort.Equals(target, StringComparison.OrdinalIgnoreCase));

            if (pawn == null)
            {
                LogError($"Pawn '{target}' not found");
                return false;
            }

            // Restore body parts and heal injuries
            var injuries = pawn.health.hediffSet.hediffs
                .Where(h => h is Hediff_Injury || h is Hediff_MissingPart)
                .ToList();

            foreach (var injury in injuries)
            {
                pawn.health.RemoveHediff(injury);
            }

            LogExecution($"Healed {pawn.LabelShort}");
            return true;
        }
    }

    /// <summary>
    /// Set work priorities for a colonist
    /// </summary>
    public class SetWorkPriorityCommand : BaseAICommand
    {
        public override string ActionName => "SetWorkPriority";

        public override string GetDescription()
        {
            return "Set work priority for a colonist. Parameters: workType=<string>, priority=<1-4>";
        }

        public override bool Execute(string? target = null, object? parameters = null)
        {
            if (string.IsNullOrEmpty(target))
            {
                LogError("Target colonist name is required");
                return false;
            }

            var pawn = Find.CurrentMap.mapPawns.FreeColonists
                .FirstOrDefault(p => p.Name.ToStringFull.Contains(target) || 
                                   p.LabelShort.Equals(target, StringComparison.OrdinalIgnoreCase));

            if (pawn == null)
            {
                LogError($"Colonist '{target}' not found");
                return false;
            }

            if (parameters is Dictionary<string, object> paramsDict &&
                paramsDict.TryGetValue("workType", out var workTypeObj) &&
                paramsDict.TryGetValue("priority", out var priorityObj))
            {
                string workTypeStr = workTypeObj.ToString();
                int priority = Convert.ToInt32(priorityObj);

                var workDef = DefDatabase<WorkTypeDef>.AllDefs
                    .FirstOrDefault(w => w.defName.Equals(workTypeStr, StringComparison.OrdinalIgnoreCase) ||
                                       w.labelShort.Equals(workTypeStr, StringComparison.OrdinalIgnoreCase));

                if (workDef == null)
                {
                    LogError($"Work type '{workTypeStr}' not found");
                    return false;
                }

                if (pawn.workSettings == null)
                {
                    LogError($"Colonist '{target}' cannot work");
                    return false;
                }

                pawn.workSettings.SetPriority(workDef, priority);
                LogExecution($"Set {workDef.labelShort} priority to {priority} for {pawn.LabelShort}");
                return true;
            }

            LogError("Invalid parameters. Required: workType, priority");
            return false;
        }
    }

    /// <summary>
    /// Order a colonist to equip a specific weapon
    /// </summary>
    public class EquipWeaponCommand : BaseAICommand
    {
        public override string ActionName => "EquipWeapon";

        public override string GetDescription()
        {
            return "Order a colonist to equip a specific weapon";
        }

        public override bool Execute(string? target = null, object? parameters = null)
        {
            if (string.IsNullOrEmpty(target))
            {
                LogError("Target colonist name is required");
                return false;
            }

            var pawn = Find.CurrentMap.mapPawns.FreeColonists
                .FirstOrDefault(p => p.Name.ToStringFull.Contains(target) || 
                                   p.LabelShort.Equals(target, StringComparison.OrdinalIgnoreCase));

            if (pawn == null)
            {
                LogError($"Colonist '{target}' not found");
                return false;
            }

            string weaponName = "";
            if (parameters is Dictionary<string, object> paramsDict &&
                paramsDict.TryGetValue("weapon", out var weaponObj))
            {
                weaponName = weaponObj.ToString();
            }
            else
            {
                LogError("Weapon name is required in parameters");
                return false;
            }

            var weapon = Find.CurrentMap.listerThings.ThingsInGroup(ThingRequestGroup.Weapon)
                .Where(w => !w.IsForbidden(Faction.OfPlayer) && 
                           (w.Label.Contains(weaponName) || w.def.defName.Contains(weaponName)))
                .OrderBy(w => w.Position.DistanceTo(pawn.Position))
                .FirstOrDefault();

            if (weapon == null)
            {
                LogError($"Weapon '{weaponName}' not found or forbidden");
                return false;
            }

            if (!pawn.CanReserveAndReach(weapon, PathEndMode.ClosestTouch, Danger.None))
            {
                LogError($"Colonist cannot reach weapon '{weapon.Label}'");
                return false;
            }

            var job = JobMaker.MakeJob(JobDefOf.Equip, weapon);
            pawn.jobs.TryTakeOrderedJob(job);
            
            LogExecution($"Ordered {pawn.LabelShort} to equip {weapon.Label}");
            return true;
        }
    }
}