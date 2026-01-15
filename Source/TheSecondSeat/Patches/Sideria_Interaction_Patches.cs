using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using Verse;
using System.Linq;

namespace TheSecondSeat
{
    public static class SideriaInteractionUtils
    {
        public static bool IsSideriaOrDragon(Thing t)
        {
            if (t is Pawn p)
            {
                if (p.def.defName == "Sideria_DescentRace") return true;
                if (p.def.defName.StartsWith("Sideria_SpiritDragon_")) return true;
            }
            return false;
        }
    }

    // 1. 禁止被宰杀
    [HarmonyPatch(typeof(Designator_Slaughter), "CanDesignateThing")]
    public static class Designator_Slaughter_Patch
    {
        public static void Postfix(Thing t, ref AcceptanceReport __result)
        {
            if (__result.Accepted && SideriaInteractionUtils.IsSideriaOrDragon(t))
            {
                __result = false;
            }
        }
    }

    // 2. 禁止被放生 (Designator 层面)
    [HarmonyPatch(typeof(Designator_ReleaseAnimalToWild), "CanDesignateThing")]
    public static class Designator_ReleaseAnimalToWild_Patch
    {
        public static void Postfix(Thing t, ref AcceptanceReport __result)
        {
            if (__result.Accepted && SideriaInteractionUtils.IsSideriaOrDragon(t))
            {
                __result = false;
            }
        }
    }

    // 3. 从 Gizmo 中移除放生按钮，并手动添加征召按钮（因为现在它们是动物智商）
    [HarmonyPatch(typeof(Pawn), "GetGizmos")]
    public static class Pawn_GetGizmos_Patch
    {
        public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> values, Pawn __instance)
        {
            if (values == null) yield break;

            if (SideriaInteractionUtils.IsSideriaOrDragon(__instance))
            {
                foreach (var gizmo in values)
                {
                    if (gizmo == null) continue;

                    // 移除 Designator_ReleaseAnimalToWild
                    if (gizmo is Designator_ReleaseAnimalToWild)
                    {
                        continue;
                    }
                    
                    // 移除可能的 Command_ReleaseToWild (如果存在) 或其他相关命令
                    // 通过 Label 检查作为额外保障
                    if (gizmo is Command cmd)
                    {
                        if (cmd.defaultLabel == "ReleaseToWild".Translate() || 
                            cmd.defaultLabel == "DesignatorReleaseAnimalToWild".Translate()) 
                        {
                             continue;
                        }
                    }

                    yield return gizmo;
                }

                // 手动添加征召按钮
                // 因为我们将智商设置为了 Animal，原版 Pawn.GetGizmos 不会为它们添加征召按钮
                if (__instance.Faction == Faction.OfPlayer && __instance.drafter != null)
                {
                    // GetGizmos 是 internal 的，需要使用 Traverse 访问
                    var draftGizmos = Traverse.Create(__instance.drafter).Method("GetGizmos").GetValue<IEnumerable<Gizmo>>();
                    if (draftGizmos != null)
                    {
                        foreach (var gizmo in draftGizmos)
                        {
                            yield return gizmo;
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

    // 4. 隐藏训练标签页
    [HarmonyPatch(typeof(ITab_Pawn_Training), "IsVisible", MethodType.Getter)]
    public static class ITab_Pawn_Training_Patch
    {
        public static void Postfix(ITab_Pawn_Training __instance, ref bool __result)
        {
            if (__result)
            {
                // 使用 Traverse 访问受保护的 SelPawn 属性
                Pawn pawn = Traverse.Create(__instance).Property("SelPawn").GetValue<Pawn>();
                if (pawn != null && SideriaInteractionUtils.IsSideriaOrDragon(pawn))
                {
                    __result = false;
                }
            }
        }
    }
}
