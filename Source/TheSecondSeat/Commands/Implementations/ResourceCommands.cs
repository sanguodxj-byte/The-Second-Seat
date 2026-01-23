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
    /// Forbid items in a specific area or of a specific type
    /// </summary>
    public class ForbidItemsCommand : BaseAICommand
    {
        public override string ActionName => "ForbidItems";

        public override string GetDescription()
        {
            return "Forbid items. Parameters: target=<itemName/all>, x=<int>, z=<int>, radius=<int>";
        }

        public override bool Execute(string? target = null, object? parameters = null)
        {
            var map = Find.CurrentMap;
            if (map == null)
            {
                LogError("No active map");
                return false;
            }

            string itemTarget = target ?? "all";
            IntVec3 center = IntVec3.Invalid;
            int radius = -1;

            if (parameters is Dictionary<string, object> paramsDict)
            {
                if (paramsDict.TryGetValue("x", out var xObj) && paramsDict.TryGetValue("z", out var zObj))
                {
                    center = new IntVec3(Convert.ToInt32(xObj), 0, Convert.ToInt32(zObj));
                }
                if (paramsDict.TryGetValue("radius", out var rObj))
                {
                    radius = Convert.ToInt32(rObj);
                }
            }

            var items = map.listerThings.AllThings.Where(t => t.def.category == ThingCategory.Item).ToList();
            int count = 0;

            foreach (var item in items)
            {
                if (item.IsForbidden(Faction.OfPlayer)) continue;

                bool matchesTarget = itemTarget == "all" || 
                                   item.Label.Contains(itemTarget) || 
                                   item.def.defName.Contains(itemTarget);

                bool inRange = true;
                if (center.IsValid && radius > 0)
                {
                    inRange = item.Position.DistanceTo(center) <= radius;
                }

                if (matchesTarget && inRange)
                {
                    item.SetForbidden(true);
                    count++;
                }
            }

            LogExecution($"Forbidden {count} items matching '{itemTarget}'");
            return true;
        }
    }

    /// <summary>
    /// Allow items in a specific area or of a specific type
    /// </summary>
    public class AllowItemsCommand : BaseAICommand
    {
        public override string ActionName => "AllowItems";

        public override string GetDescription()
        {
            return "Allow items. Parameters: target=<itemName/all>, x=<int>, z=<int>, radius=<int>";
        }

        public override bool Execute(string? target = null, object? parameters = null)
        {
            var map = Find.CurrentMap;
            if (map == null)
            {
                LogError("No active map");
                return false;
            }

            string itemTarget = target ?? "all";
            IntVec3 center = IntVec3.Invalid;
            int radius = -1;

            if (parameters is Dictionary<string, object> paramsDict)
            {
                if (paramsDict.TryGetValue("x", out var xObj) && paramsDict.TryGetValue("z", out var zObj))
                {
                    center = new IntVec3(Convert.ToInt32(xObj), 0, Convert.ToInt32(zObj));
                }
                if (paramsDict.TryGetValue("radius", out var rObj))
                {
                    radius = Convert.ToInt32(rObj);
                }
            }

            var items = map.listerThings.AllThings.Where(t => t.def.category == ThingCategory.Item).ToList();
            int count = 0;

            foreach (var item in items)
            {
                if (!item.IsForbidden(Faction.OfPlayer)) continue;

                bool matchesTarget = itemTarget == "all" || 
                                   item.Label.Contains(itemTarget) || 
                                   item.def.defName.Contains(itemTarget);

                bool inRange = true;
                if (center.IsValid && radius > 0)
                {
                    inRange = item.Position.DistanceTo(center) <= radius;
                }

                if (matchesTarget && inRange)
                {
                    item.SetForbidden(false);
                    count++;
                }
            }

            LogExecution($"Allowed {count} items matching '{itemTarget}'");
            return true;
        }
    }

    /// <summary>
    /// Change colony policies (food, drug, clothing restrictions)
    /// </summary>
    public class ChangePolicyCommand : BaseAICommand
    {
        public override string ActionName => "ChangePolicy";

        public override string GetDescription()
        {
            return "Change colony policy. Target: Food/Drug/Clothing. Parameters: policyName=<string>";
        }

        public override bool Execute(string? target = null, object? parameters = null)
        {
            if (string.IsNullOrEmpty(target))
            {
                LogError("Policy type (Food/Drug/Clothing) is required");
                return false;
            }

            string policyName = "";
            if (parameters is Dictionary<string, object> paramsDict &&
                paramsDict.TryGetValue("policyName", out var policyObj))
            {
                policyName = policyObj.ToString();
            }
            else
            {
                LogError("Policy name is required");
                return false;
            }

            var colonists = Find.CurrentMap.mapPawns.FreeColonists;
            int updated = 0;

            foreach (var colonist in colonists)
            {
                if (target.Equals("Food", StringComparison.OrdinalIgnoreCase))
                {
                    var policy = Current.Game.foodRestrictionDatabase.AllFoodRestrictions
                        .FirstOrDefault(p => p.label.Equals(policyName, StringComparison.OrdinalIgnoreCase));
                    if (policy != null)
                    {
                        colonist.foodRestriction.CurrentFoodPolicy = policy;
                        updated++;
                    }
                }
                else if (target.Equals("Drug", StringComparison.OrdinalIgnoreCase))
                {
                    var policy = Current.Game.drugPolicyDatabase.AllPolicies
                        .FirstOrDefault(p => p.label.Equals(policyName, StringComparison.OrdinalIgnoreCase));
                    if (policy != null)
                    {
                        colonist.drugs.CurrentPolicy = policy;
                        updated++;
                    }
                }
                else if (target.Equals("Clothing", StringComparison.OrdinalIgnoreCase))
                {
                    var policy = Current.Game.outfitDatabase.AllOutfits
                        .FirstOrDefault(p => p.label.Equals(policyName, StringComparison.OrdinalIgnoreCase));
                    if (policy != null)
                    {
                        colonist.outfits.CurrentApparelPolicy = policy;
                        updated++;
                    }
                }
            }

            if (updated > 0)
            {
                LogExecution($"Updated {target} policy to '{policyName}' for {updated} colonists");
                return true;
            }

            LogError($"Failed to update policy or policy '{policyName}' not found");
            return false;
        }
    }

    /// <summary>
    /// Change colony policies - New Implementation
    /// </summary>
    public class ChangePolicyCommand_New : BaseAICommand
    {
        public override string ActionName => "ChangePolicy_New";

        public override string GetDescription()
        {
            return "Change colony policy. Target: Food/Drug/Clothing. Parameters: policyName=<string>";
        }

        public override bool Execute(string? target = null, object? parameters = null)
        {
            if (string.IsNullOrEmpty(target))
            {
                LogError("Policy type (Food/Drug/Clothing) is required");
                return false;
            }

            string policyName = "";
            if (parameters is Dictionary<string, object> paramsDict &&
                paramsDict.TryGetValue("policyName", out var policyObj))
            {
                policyName = policyObj.ToString();
            }
            else
            {
                LogError("Policy name is required");
                return false;
            }

            var colonists = Find.CurrentMap.mapPawns.FreeColonists;
            int updated = 0;

            foreach (var colonist in colonists)
            {
                if (target.Equals("Food", StringComparison.OrdinalIgnoreCase))
                {
                    var policy = Current.Game.foodRestrictionDatabase.AllFoodRestrictions
                        .FirstOrDefault(p => p.label.Equals(policyName, StringComparison.OrdinalIgnoreCase));
                    if (policy != null)
                    {
                        colonist.foodRestriction.CurrentFoodPolicy = policy;
                        updated++;
                    }
                }
                else if (target.Equals("Drug", StringComparison.OrdinalIgnoreCase))
                {
                    var policy = Current.Game.drugPolicyDatabase.AllPolicies
                        .FirstOrDefault(p => p.label.Equals(policyName, StringComparison.OrdinalIgnoreCase));
                    if (policy != null)
                    {
                        colonist.drugs.CurrentPolicy = policy;
                        updated++;
                    }
                }
                else if (target.Equals("Clothing", StringComparison.OrdinalIgnoreCase))
                {
                    var policy = Current.Game.outfitDatabase.AllOutfits
                        .FirstOrDefault(p => p.label.Equals(policyName, StringComparison.OrdinalIgnoreCase));
                    if (policy != null)
                    {
                        colonist.outfits.CurrentApparelPolicy = policy;
                        updated++;
                    }
                }
            }

            if (updated > 0)
            {
                LogExecution($"Updated {target} policy to '{policyName}' for {updated} colonists");
                return true;
            }

            LogError($"Failed to update policy or policy '{policyName}' not found");
            return false;
        }
    }
}