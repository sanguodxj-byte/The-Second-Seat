using HarmonyLib;
using RimWorld;
using Verse;

namespace TheSecondSeat.Patches
{
    /// <summary>
    /// Fix for Sideria Avatar being undraftable.
    /// Forces Pawn_DraftController existence for Sideria_DescentRace even though it's not Humanlike.
    /// </summary>
    [HarmonyPatch(typeof(PawnComponentsUtility), "AddAndRemoveDynamicComponents")]
    public static class PawnComponentsUtility_Patch
    {
        public static void Postfix(Pawn pawn)
        {
            // Only target our specific race
            if (pawn.def.defName == "Sideria_DescentRace" && pawn.Faction == Faction.OfPlayer)
            {
                // If drafter was removed or never added, add it back
                if (pawn.drafter == null)
                {
                    pawn.drafter = new Pawn_DraftController(pawn);
                }
            }
        }
    }
}