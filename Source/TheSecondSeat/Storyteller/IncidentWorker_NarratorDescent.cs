using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using RimWorld;
using TheSecondSeat.Descent;

namespace TheSecondSeat.Storyteller
{
    /// <summary>
    /// Worker for triggering Narrator Descent via Incident system.
    /// This allows the descent event to be triggered by Storytellers, Quests, or Debug actions.
    /// </summary>
    public class IncidentWorker_NarratorDescent : IncidentWorker
    {
        protected override bool CanFireNowSub(IncidentParms parms)
        {
            // Check if descent system is ready
            var descentSystem = NarratorDescentSystem.Instance;
            if (descentSystem == null) return false;

            // Check if we can descend now
            if (!descentSystem.CanDescendNow(out string reason))
            {
                return false;
            }

            return base.CanFireNowSub(parms);
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            var descentSystem = NarratorDescentSystem.Instance;
            if (descentSystem == null)
            {
                Log.Error("[TSS] NarratorDescentSystem not found during incident execution.");
                return false;
            }

            // Determine if hostile based on IncidentDef or Parms
            bool isHostile = false;

            // 1. Check CustomDef properties if available (requires custom IncidentDef subclass, skipping for now to keep it simple)
            // 2. Check Faction in parms (if Faction is hostile, descent is hostile)
            if (parms.faction != null && parms.faction.HostileTo(Faction.OfPlayer))
            {
                isHostile = true;
            }
            
            // 3. Check forced hostile flag in parms.customArgs (if used)
            // Note: IncidentParms doesn't have a generic dictionary, but we can infer from other properties or context if needed.
            // For now, we rely on the Faction or the Def itself being configured as a threat.
            
            // If the IncidentDef is a "ThreatBig" or "ThreatSmall", default to hostile if not specified
            if (def.category == IncidentCategoryDefOf.ThreatBig || def.category == IncidentCategoryDefOf.ThreatSmall)
            {
                isHostile = true;
            }

            // Execute Descent
            // We use the spawn center from parms if valid, otherwise system picks location
            IntVec3? targetLoc = null;
            if (parms.spawnCenter.IsValid)
            {
                targetLoc = parms.spawnCenter;
            }

            return descentSystem.TriggerDescent(isHostile, targetLoc);
        }
    }
}
