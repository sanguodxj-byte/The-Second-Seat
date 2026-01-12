using HarmonyLib;
using Verse;
using RimWorld;
using System.Collections.Generic;
using TheSecondSeat.Components;

namespace TheSecondSeat.Patches
{
    /// <summary>
    /// Patch Pawn_DraftController.GetGizmos
    /// 阻止原版征召 gizmo 显示给带有 CompDraftableAnimal 的 pawn（降临体）
    /// 因为降临体已经通过 CompDraftableAnimal.CompGetGizmosExtra() 提供自己的征召按钮
    /// 这样可以避免出现双征召按钮的问题
    /// </summary>
    [HarmonyPatch(typeof(Pawn_DraftController), "GetGizmos")]
    public static class Pawn_DraftController_GetGizmos_Patch
    {
        public static bool Prefix(Pawn_DraftController __instance, ref IEnumerable<Gizmo> __result)
        {
            if (__instance == null) return true;
            
            Pawn pawn = __instance.pawn;
            if (pawn == null) return true;
            
            // 检查是否是降临体（带有 CompDraftableAnimal）
            var draftComp = pawn.GetComp<CompDraftableAnimal>();
            if (draftComp == null) return true;
            
            // 降临体使用 CompDraftableAnimal 提供的自定义征召按钮
            // 跳过原版的征召 gizmo，返回空列表
            __result = new List<Gizmo>();
            return false;
        }
    }
}