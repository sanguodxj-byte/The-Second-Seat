using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using RimWorld;
using UnityEngine;
using TheSecondSeat.NaturalLanguage;
using TheSecondSeat.Descent;

namespace TheSecondSeat.Commands.Implementations
{
    /// <summary>
    /// Trigger a specific game event/incident
    /// </summary>
    public class TriggerEventCommand : BaseAICommand
    {
        public override string ActionName => "TriggerEvent";

        public override string GetDescription()
        {
            return "Trigger a game event. Target: IncidentDefName. Parameters: points=<int>";
        }

        public override bool Execute(string? target = null, object? parameters = null)
        {
            if (string.IsNullOrEmpty(target))
            {
                LogError("Event name (IncidentDef) is required");
                return false;
            }

            var incidentDef = DefDatabase<IncidentDef>.GetNamedSilentFail(target);
            if (incidentDef == null)
            {
                LogError($"Incident '{target}' not found");
                return false;
            }

            var map = Find.CurrentMap;
            if (map == null)
            {
                LogError("No active map");
                return false;
            }

            float points = StorytellerUtility.DefaultThreatPointsNow(map);
            if (parameters is Dictionary<string, object> paramsDict &&
                paramsDict.TryGetValue("points", out var pointsObj))
            {
                points = Convert.ToSingle(pointsObj);
            }

            IncidentParms parms = StorytellerUtility.DefaultParmsNow(incidentDef.category, map);
            parms.points = points;

            if (incidentDef.Worker.TryExecute(parms))
            {
                LogExecution($"Triggered event '{target}' with {points} points");
                return true;
            }

            LogError($"Failed to trigger event '{target}'");
            return false;
        }
    }

    /// <summary>
    /// Schedule an event for the future
    /// </summary>
    public class ScheduleEventCommand : BaseAICommand
    {
        public override string ActionName => "ScheduleEvent";

        public override string GetDescription()
        {
            return "Schedule an event. Target: IncidentDefName. Parameters: delayHours=<int>";
        }

        public override bool Execute(string? target = null, object? parameters = null)
        {
            if (string.IsNullOrEmpty(target))
            {
                LogError("Event name is required");
                return false;
            }

            int delayHours = 24;
            if (parameters is Dictionary<string, object> paramsDict &&
                paramsDict.TryGetValue("delayHours", out var delayObj))
            {
                delayHours = Convert.ToInt32(delayObj);
            }

            // In a real implementation, this would add to a queue
            // For now, we'll just log it as "Scheduled"
            LogExecution($"Scheduled event '{target}' in {delayHours} hours");
            return true;
        }
    }

    /// <summary>
    /// Trigger the narrator descent sequence
    /// </summary>
    public class DescentCommand : BaseAICommand
    {
        public override string ActionName => "Descent";

        public override string GetDescription()
        {
            return "Trigger narrator descent sequence. Target: NarratorDefName (e.g. TSS_Narrator_Sideria)";
        }

        public override bool Execute(string? target = null, object? parameters = null)
        {
            var map = Find.CurrentMap;
            if (map == null)
            {
                LogError("No active map");
                return false;
            }

            // Check if descent system is available
            var descentSystem = NarratorDescentSystem.Instance;
            if (descentSystem == null)
            {
                LogError("NarratorDescentSystem not found");
                return false;
            }

            // Parse parameters
            bool isHostile = false; // Default to assist/friendly
            
            if (parameters is Dictionary<string, object> paramsDict)
            {
                // Support "mode=attack" or "hostile=true"
                if (paramsDict.TryGetValue("mode", out var modeObj) &&
                    modeObj.ToString().ToLower() == "attack")
                {
                    isHostile = true;
                }
                else if (paramsDict.TryGetValue("hostile", out var hostileObj) &&
                         bool.TryParse(hostileObj.ToString(), out bool parsedHostile))
                {
                    isHostile = parsedHostile;
                }
            }

            // Trigger the descent (uses current persona)
            if (descentSystem.TriggerDescent(isHostile))
            {
                LogExecution($"Triggered descent sequence (Hostile: {isHostile})");
                return true;
            }

            LogError("Failed to trigger descent (see log for details)");
            return false;
        }
    }
}