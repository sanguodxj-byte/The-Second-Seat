using HarmonyLib;
using Verse;
using RimWorld;
using TheSecondSeat.Descent;

namespace TheSecondSeat.Patches
{
    /// <summary>
    /// Patch Pawn.Drafted getter
    /// 原版 Pawn.Drafted 检查 RaceProps.intelligence >= Intelligence.ToolUser
    /// 对于降临体（动物类型），即使设置了 ToolUser 智力，也可能被动物 ThinkTree 覆盖
    /// 这个 patch 确保对降临体返回正确的 Drafted 状态
    /// </summary>
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.Drafted), MethodType.Getter)]
    public static class Pawn_Drafted_Patch
    {
        public static void Postfix(Pawn __instance, ref bool __result)
        {
            // 如果已经是 true，不需要处理
            if (__result)
                return;
            
            // 只处理降临体（通过注册系统判断）
            if (!DescentEntityRegistry.IsDescentEntity(__instance))
                return;
            
            // 检查是否有 drafter 且已征召
            if (__instance.drafter != null && __instance.drafter.Drafted)
            {
                __result = true;
                // Log.Message($"[TSS-Debug] Pawn_Drafted_Patch: Overriding Drafted=true for {__instance.LabelShort}");
            }
        }
    }
}