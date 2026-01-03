using System;
using HarmonyLib;
using RimWorld;
using Verse;
using UnityEngine;

namespace TheSecondSeat.Descent
{
    /// <summary>
    /// Harmony patch to implement Sideria's specific damage immunities.
    /// She is immune to all damage except "Melee and Shooting" (Combat damage).
    /// </summary>
    [HarmonyPatch(typeof(Pawn_HealthTracker), "PreApplyDamage")]
    public static class SideriaDamagePatch
    {
        // Cache the HediffDef to avoid frequent lookups
        private static HediffDef divineBodyDef;

        public static bool Prefix(Pawn_HealthTracker __instance, ref DamageInfo dinfo, out bool absorbed, Pawn ___pawn)
        {
            absorbed = false;
            Pawn pawn = ___pawn;

            if (pawn == null) return true;

            // Lazy load Def
            if (divineBodyDef == null)
            {
                divineBodyDef = DefDatabase<HediffDef>.GetNamedSilentFail("Sideria_DivineBody");
            }

            // If Def not found or Pawn doesn't have the Hediff, execute original logic
            if (divineBodyDef == null || pawn.health?.hediffSet == null || !pawn.health.hediffSet.HasHediff(divineBodyDef))
            {
                return true;
            }

            // Check if damage is allowed
            bool isAllowed = IsDamageAllowed(dinfo);

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

        private static bool IsDamageAllowed(DamageInfo dinfo)
        {
            // 1. Damage from weapons (Melee or Ranged)
            if (dinfo.Weapon != null)
            {
                return true;
            }

            // 2. Special cases: Surgery and Execution must be allowed for gameplay management
            if (dinfo.Def == DamageDefOf.SurgicalCut || dinfo.Def == DamageDefOf.ExecutionCut)
            {
                return true;
            }

            // 3. Unarmed combat (from a Pawn, causing physical damage)
            // This covers fists, headbutts, etc., even without a weapon equipped
            if (dinfo.Instigator is Pawn)
            {
                // Check if damage type is physical (Sharp or Blunt)
                // Use defName comparison to avoid dependency on DamageArmorCategoryDefOf which might not contain Blunt in all versions
                if (dinfo.Def.armorCategory != null &&
                   (dinfo.Def.armorCategory == DamageArmorCategoryDefOf.Sharp ||
                    dinfo.Def.armorCategory.defName == "Blunt"))
                {
                    return true;
                }
            }

            // All other damage (Fire, Explosion from environment, Falling, Poison, Hypothermia, etc.) is ignored
            return false;
        }
    }
}