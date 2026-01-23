using HarmonyLib;
using RimWorld;
using Verse;
using TheSecondSeat.Components;

namespace TheSecondSeat.Patches
{
    // Patch Pawn.Tick to freeze non-casters
    [HarmonyPatch(typeof(Pawn), "Tick")]
    public static class Pawn_Tick_Patch
    {
        public static bool Prefix(Pawn __instance)
        {
            if (GameComponent_TimeStop.Instance != null && 
                !GameComponent_TimeStop.Instance.CanTick(__instance))
            {
                return false; // Skip original method
            }
            return true;
        }
    }

    // Patch Projectile.Tick to freeze projectiles
    [HarmonyPatch(typeof(Projectile), "Tick")]
    public static class Projectile_Tick_Patch
    {
        public static bool Prefix(Projectile __instance)
        {
            if (GameComponent_TimeStop.Instance != null && 
                !GameComponent_TimeStop.Instance.CanTick(__instance))
            {
                return false; // Skip original method
            }
            return true;
        }
    }

    // Patch Fire.Tick to freeze fire spreading
    [HarmonyPatch(typeof(Fire), "Tick")]
    public static class Fire_Tick_Patch
    {
        public static bool Prefix(Fire __instance)
        {
            if (GameComponent_TimeStop.Instance != null && 
                !GameComponent_TimeStop.Instance.CanTick(__instance))
            {
                return false; // Skip original method
            }
            return true;
        }
    }

    // Patch Mote.Tick to freeze visual effects (smoke, blood, etc)
    [HarmonyPatch(typeof(Mote), "Tick")]
    public static class Mote_Tick_Patch
    {
        public static bool Prefix(Mote __instance)
        {
            if (GameComponent_TimeStop.Instance != null && 
                !GameComponent_TimeStop.Instance.CanTick(__instance))
            {
                return false; // Skip original method
            }
            return true;
        }
    }
}
