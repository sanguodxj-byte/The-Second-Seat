using HarmonyLib;
using Verse;
using RimWorld;
using TheSecondSeat.Components;

namespace TheSecondSeat.Patches
{
    /// <summary>
    /// Patch Pawn.Drafted 属性 getter
    /// 让降临体的 CompDraftableAnimal.isDrafted 状态映射到系统的 Drafted 状态
    /// 这样降临体在征召状态下可以执行移动、攻击等殖民者命令
    /// </summary>
    [HarmonyPatch(typeof(Pawn), "get_Drafted")]
    public static class Pawn_Drafted_Getter_Patch
    {
        public static void Postfix(Pawn __instance, ref bool __result)
        {
            // 如果原版已经返回 true，不需要干预
            if (__result) return;
            
            if (__instance == null) return;
            
            // 检查是否是降临体
            var draftComp = __instance.GetComp<CompDraftableAnimal>();
            if (draftComp == null) return;
            
            // 必须属于玩家派系
            if (__instance.Faction != Faction.OfPlayer) return;
            
            // 检查 CompDraftableAnimal 的 isDrafted 状态
            if (draftComp.isDrafted)
            {
                __result = true;
            }
        }
    }
    
    // 注意：Pawn.Drafted 只有 getter 没有 setter
    // 征召状态通过 CompDraftableAnimal 的自定义按钮来控制
    // 不需要 setter patch
}