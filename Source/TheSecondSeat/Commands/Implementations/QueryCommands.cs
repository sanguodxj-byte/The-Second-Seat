using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using RimWorld;
using UnityEngine;
using TheSecondSeat.NaturalLanguage;

namespace TheSecondSeat.Commands.Implementations
{
    /// <summary>
    /// Get coordinates for a specific map location/building
    /// </summary>
    public class GetMapLocationCommand : BaseAICommand
    {
        public override string ActionName => "GetMapLocation";

        public override string GetDescription()
        {
            return "Get coordinates of a location/building";
        }

        public override bool Execute(string? target = null, object? parameters = null)
        {
            if (string.IsNullOrEmpty(target))
            {
                LogError("Target location/building name is required");
                return false;
            }

            var map = Find.CurrentMap;
            if (map == null)
            {
                LogError("No active map");
                return false;
            }

            // Search for things matching the target name
            var thing = map.listerThings.AllThings
                .FirstOrDefault(t => t.Label.Contains(target) || 
                                   t.def.defName.Contains(target));

            if (thing != null)
            {
                LogExecution($"Found '{target}' at ({thing.Position.x}, {thing.Position.z})");
                return true;
            }

            // Check specific areas/zones
            var zone = map.zoneManager.AllZones
                .FirstOrDefault(z => z.label.Contains(target));

            if (zone != null)
            {
                IntVec3 center = zone.Cells[0]; // Just take first cell for now
                LogExecution($"Found zone '{target}' at ({center.x}, {center.z})");
                return true;
            }

            LogError($"Location '{target}' not found");
            return false;
        }
    }

    /// <summary>
    /// Scan map for resources/enemies/etc
    /// </summary>
    public class ScanMapCommand : BaseAICommand
    {
        public override string ActionName => "ScanMap";

        public override string GetDescription()
        {
            return "Scan map for specific entities. Target: Resources/Enemies/Colonists";
        }

        public override bool Execute(string? target = null, object? parameters = null)
        {
            var map = Find.CurrentMap;
            if (map == null)
            {
                LogError("No active map");
                return false;
            }

            string scanType = target?.ToLower() ?? "all";
            int count = 0;
            string details = "";

            switch (scanType)
            {
                case "enemies":
                    var enemies = map.mapPawns.AllPawnsSpawned
                        .Where(p => p.HostileTo(Faction.OfPlayer) && !p.Dead && !p.Downed)
                        .ToList();
                    count = enemies.Count;
                    details = string.Join(", ", enemies.Select(e => $"{e.LabelShort} at {e.Position}"));
                    break;

                case "resources":
                    var resources = map.listerThings.AllThings
                        .Where(t => t.def.category == ThingCategory.Item && !t.IsForbidden(Faction.OfPlayer))
                        .GroupBy(t => t.def.label)
                        .Select(g => $"{g.Key}: {g.Sum(t => t.stackCount)}")
                        .ToList();
                    count = resources.Count;
                    details = string.Join(", ", resources);
                    break;

                case "colonists":
                    var colonists = map.mapPawns.FreeColonists;
                    count = colonists.Count;
                    details = string.Join(", ", colonists.Select(c => 
                        $"{c.LabelShort} ({c.CurJob?.def.defName ?? "Idle"})"));
                    break;

                default:
                    LogError($"Unknown scan type: {scanType}");
                    return false;
            }

            LogExecution($"Scan complete. Found {count} {scanType}. Details: {details}");
            return true;
        }
    }
}