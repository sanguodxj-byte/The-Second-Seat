using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;
using RimWorld;

namespace TheSecondSeat
{
    public class CompProperties_AutoAbility : CompProperties
    {
        public List<string> abilityDefs = new List<string>();
        public int checkInterval = 180;
        public float searchRadius = 25f;
        public int maxSummonCount = 2;
        public string summonLimitDefName = ""; // e.g. "TSS_SpiritDragon"

        public CompProperties_AutoAbility()
        {
            this.compClass = typeof(CompAutoAbility);
        }
    }

    public class CompAutoAbility : ThingComp
    {
        public CompProperties_AutoAbility Props => (CompProperties_AutoAbility)props;

        public override void CompTick()
        {
            base.CompTick();
            
            Pawn pawn = parent as Pawn;
            if (pawn == null || !pawn.Spawned || pawn.Dead || pawn.Downed) return;

            // Only effective for non-player factions (Visitors/Aid)
            if (pawn.Faction == Faction.OfPlayer) return;

            if (!pawn.IsHashIntervalTick(Props.checkInterval)) return;

            TryUseAbilities(pawn);
        }

        private void TryUseAbilities(Pawn pawn)
        {
            if (pawn.abilities == null) return;

            // Find target
            Thing target = (pawn.TargetCurrentlyAimingAt.IsValid ? pawn.TargetCurrentlyAimingAt.Thing : null);
            if (target == null) target = pawn.mindState?.enemyTarget;
            
            if (target == null && pawn.Map != null)
            {
                target = GenClosest.ClosestThingReachable(
                    pawn.Position,
                    pawn.Map,
                    ThingRequest.ForGroup(ThingRequestGroup.Pawn),
                    PathEndMode.Touch,
                    TraverseParms.For(pawn),
                    Props.searchRadius,
                    t => t is Pawn p && p.HostileTo(pawn) && !p.Downed
                );
            }

            if (target == null) return;

            foreach (var ability in pawn.abilities.abilities)
            {
                if (!ability.CanCast) continue;
                if (!Props.abilityDefs.Contains(ability.def.defName)) continue;

                // Special handling for Summon abilities (DragonGate logic generalized)
                // If the ability spawns things and we have a limit
                if (!string.IsNullOrEmpty(Props.summonLimitDefName) && 
                    (ability.def.defName.Contains("Summon") || ability.def.defName.Contains("Gate")))
                {
                    if (HasTooManySummons(pawn.Map, Props.summonLimitDefName, Props.maxSummonCount))
                    {
                        continue;
                    }
                    
                    // Summon abilities usually target a cell
                    IntVec3 cell = CellFinder.RandomClosewalkCellNear(pawn.Position, pawn.Map, 3, null);
                    if (cell.IsValid)
                    {
                        ability.verb.TryStartCastOn(new LocalTargetInfo(cell));
                        return;
                    }
                }
                
                // General target ability
                if (ability.verb.CanHitTarget(target))
                {
                    ability.verb.TryStartCastOn(target);
                    return;
                }
            }
        }

        private bool HasTooManySummons(Map map, string defNamePart, int limit)
        {
            if (map == null) return true;
            int count = 0;
            foreach (Pawn p in map.mapPawns.AllPawnsSpawned)
            {
                if (p.def.defName.Contains(defNamePart) && !p.Dead)
                {
                    count++;
                }
            }
            return count >= limit;
        }
    }
}