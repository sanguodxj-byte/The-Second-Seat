using System;
using HarmonyLib;
using RimWorld;
using Verse;
using UnityEngine;

namespace TheSecondSeat.Descent
{
    /// <summary>
    /// Harmony patch to implement generic Divine Body damage immunities.
    /// Pawns with any HediffDef that has DefModExtension_DivineBody are immune to non-combat damage.
    /// This is a generic implementation - sub-mods define the HediffDef with the extension in XML.
    ///
    /// The DefModExtension_DivineBody is defined in TheSecondSeat.DefModExtensions.cs
    /// </summary>
    [HarmonyPatch(typeof(Pawn_HealthTracker), "PreApplyDamage")]
    public static class DivineBodyDamagePatch
    {
        public static bool Prefix(Pawn_HealthTracker __instance, ref DamageInfo dinfo, out bool absorbed, Pawn ___pawn)
        {
            absorbed = false;
            Pawn pawn = ___pawn;

            if (pawn == null || pawn.health?.hediffSet == null) return true;

            // Find any hediff with DefModExtension_DivineBody
            DefModExtension_DivineBody divineBodyExt = null;
            foreach (Hediff hediff in pawn.health.hediffSet.hediffs)
            {
                var ext = hediff.def.GetModExtension<DefModExtension_DivineBody>();
                if (ext != null)
                {
                    divineBodyExt = ext;
                    break;
                }
            }

            // If no divine body hediff found, execute original logic
            if (divineBodyExt == null)
            {
                return true;
            }

            // Check if damage is allowed
            bool isAllowed = IsDamageAllowed(dinfo, divineBodyExt);

            if (!isAllowed)
            {
                absorbed = true;
                
                // Show "Immune" text if spawned and not a silent damage type
                if (pawn.Spawned && dinfo.Def.isExplosive == false)
                {
                    MoteMaker.ThrowText(pawn.DrawPos + new Vector3(0, 0, 0.5f), pawn.Map, "Immune", Color.cyan);
                }
                
                return false; // Block original method
            }

            return true;
        }

        private static bool IsDamageAllowed(DamageInfo dinfo, DefModExtension_DivineBody ext)
        {
            // 1. Damage from weapons (Melee or Ranged)
            if (ext.allowWeaponDamage && dinfo.Weapon != null)
            {
                return true;
            }

            // 2. Special cases: Surgery and Execution must be allowed for gameplay management
            if (ext.allowMedicalDamage &&
                (dinfo.Def == DamageDefOf.SurgicalCut || dinfo.Def == DamageDefOf.ExecutionCut))
            {
                return true;
            }

            // 3. Unarmed combat (from a Pawn, causing physical damage)
            if (ext.allowPawnInstigatorDamage && dinfo.Instigator is Pawn)
            {
                // Check if damage type is in the allowed categories
                if (dinfo.Def.armorCategory != null &&
                    ext.allowedDamageCategories.Contains(dinfo.Def.armorCategory.defName))
                {
                    return true;
                }
            }

            // 4. Check damage category directly (for weapon damage)
            if (dinfo.Def.armorCategory != null &&
                ext.allowedDamageCategories.Contains(dinfo.Def.armorCategory.defName))
            {
                // Only allow if it's from combat (has an instigator pawn or weapon)
                if (dinfo.Instigator is Pawn || dinfo.Weapon != null)
                {
                    return true;
                }
            }

            // All other damage (Fire, Explosion from environment, Falling, Poison, Hypothermia, etc.) is ignored
            return false;
        }
    }
}