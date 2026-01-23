using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using Verse;
using System.Linq;

namespace TheSecondSeat
{
    public static class NarratorEntityUtils
    {
        public static bool IsNarratorEntity(Thing t)
        {
            if (t is Pawn p && p.def != null)
            {
                // Check if the pawn has CompNarratorEntity
                var comp = p.GetComp<CompNarratorEntity>();
                return comp != null;
            }
            return false;
        }
        
        public static CompNarratorEntity GetNarratorComp(Thing t)
        {
            if (t is Pawn p)
            {
                return p.GetComp<CompNarratorEntity>();
            }
            return null;
        }
    }

    // 1. Forbidden Slaughter
    [HarmonyPatch(typeof(Designator_Slaughter), "CanDesignateThing")]
    public static class Designator_Slaughter_Patch
    {
        public static void Postfix(Thing t, ref AcceptanceReport __result)
        {
            if (__result.Accepted)
            {
                var comp = NarratorEntityUtils.GetNarratorComp(t);
                if (comp != null && comp.Props.disableSlaughter)
                {
                    __result = false;
                }
            }
        }
    }

    // 2. Forbidden Release to Wild
    [HarmonyPatch(typeof(Designator_ReleaseAnimalToWild), "CanDesignateThing")]
    public static class Designator_ReleaseAnimalToWild_Patch
    {
        public static void Postfix(Thing t, ref AcceptanceReport __result)
        {
            if (__result.Accepted)
            {
                var comp = NarratorEntityUtils.GetNarratorComp(t);
                if (comp != null && comp.Props.disableRelease)
                {
                    __result = false;
                }
            }
        }
    }

    // 3. Remove Release Gizmo & Add Draft Gizmo
    [HarmonyPatch(typeof(Pawn), "GetGizmos")]
    public static class Pawn_GetGizmos_Patch
    {
        public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> values, Pawn __instance)
        {
            if (values == null) yield break;

            var comp = NarratorEntityUtils.GetNarratorComp(__instance);
            if (comp != null)
            {
                // Use try-catch to prevent crashes from other mods
                var enumerator = values.GetEnumerator();
                while (true)
                {
                    Gizmo gizmo = null;
                    try
                    {
                        if (!enumerator.MoveNext()) break;
                        gizmo = enumerator.Current;
                    }
                    catch (System.Exception ex)
                    {
                        Log.ErrorOnce($"[TheSecondSeat] Error iterating gizmos for {__instance}: {ex}", __instance.thingIDNumber ^ 0x1234);
                        break;
                    }

                    if (gizmo == null) continue;

                    // Remove Release Gizmos if disabled
                    if (comp.Props.disableRelease)
                    {
                        if (gizmo is Designator_ReleaseAnimalToWild) continue;
                        
                        if (gizmo is Command cmd)
                        {
                            if (cmd.defaultLabel == "ReleaseToWild".Translate() ||
                                cmd.defaultLabel == "DesignatorReleaseAnimalToWild".Translate())
                            {
                                 continue;
                            }
                        }
                    }

                    yield return gizmo;
                }

                // Add Draft Gizmo if forced
                if (comp.Props.forceShowDraftGizmo && __instance.Faction == Faction.OfPlayer && __instance.drafter != null)
                {
                    var draftGizmos = Traverse.Create(__instance.drafter).Method("GetGizmos").GetValue<IEnumerable<Gizmo>>();
                    if (draftGizmos != null)
                    {
                        var draftEnumerator = draftGizmos.GetEnumerator();
                        while (true)
                        {
                            Gizmo gizmo = null;
                            try
                            {
                                if (!draftEnumerator.MoveNext()) break;
                                gizmo = draftEnumerator.Current;
                            }
                            catch (System.Exception ex)
                            {
                                Log.ErrorOnce($"[TheSecondSeat] Error iterating draft gizmos for {__instance}: {ex}", __instance.thingIDNumber ^ 0x5678);
                                break;
                            }
                            
                            if (gizmo != null)
                            {
                                yield return gizmo;
                            }
                        }
                    }
                }
            }
            else
            {
                foreach (var gizmo in values)
                {
                    yield return gizmo;
                }
            }
        }
    }

    // 4. Hide Training Tab
    [HarmonyPatch(typeof(ITab_Pawn_Training), "IsVisible", MethodType.Getter)]
    public static class ITab_Pawn_Training_Patch
    {
        public static void Postfix(ITab_Pawn_Training __instance, ref bool __result)
        {
            if (__result)
            {
                Pawn pawn = Traverse.Create(__instance).Property("SelPawn").GetValue<Pawn>();
                var comp = NarratorEntityUtils.GetNarratorComp(pawn);
                if (comp != null && comp.Props.hideTrainingTab)
                {
                    __result = false;
                }
            }
        }
    }
}