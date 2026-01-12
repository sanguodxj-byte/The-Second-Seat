using HarmonyLib;
using RimWorld;
using Verse;
using TheSecondSeat.Components;

namespace TheSecondSeat.Patches
{
    /// <summary>
    /// Harmony patch for MapPawns.IsValidColonyPawn to include Descent entities.
    /// This is the core method RimWorld uses to determine if a pawn counts as "our pawn".
    /// By patching this, Descent entities (identified by CompDraftableAnimal) will be
    /// properly recognized as colony pawns throughout the game's systems.
    /// </summary>
    [HarmonyPatch(typeof(MapPawns), "IsValidColonyPawn")]
    public static class MapPawns_IsValidColonyPawn_Patch
    {
        /// <summary>
        /// Postfix: If the original method returned false, check if this is a Descent entity.
        /// If so, override the result to true.
        /// </summary>
        /// <param name="pawn">The pawn being checked</param>
        /// <param name="__result">The result from the original method (can be modified)</param>
        public static void Postfix(Pawn pawn, ref bool __result)
        {
            // If already recognized as valid colony pawn, no need to modify
            if (__result)
                return;
            
            // Skip null checks
            if (pawn == null)
                return;
            
            // Check if this is a Descent entity (has CompDraftableAnimal component)
            var draftComp = pawn.GetComp<CompDraftableAnimal>();
            if (draftComp == null)
                return;
            
            // Additional validation: must belong to player faction and not be dead
            // (unless they have death refusal or are resurrecting, matching original logic)
            if (pawn.Faction != Faction.OfPlayer)
                return;
            
            if (pawn.Dead && !pawn.HasDeathRefusalOrResurrecting)
                return;
            
            // This is a valid Descent entity - mark as valid colony pawn
            __result = true;
            
            // Optional: Log for debugging (can be removed in production)
            if (Prefs.DevMode)
            {
                Log.Message($"[TSS] MapPawns.IsValidColonyPawn: Descent entity '{pawn.LabelShort}' recognized as valid colony pawn.");
            }
        }
    }
}