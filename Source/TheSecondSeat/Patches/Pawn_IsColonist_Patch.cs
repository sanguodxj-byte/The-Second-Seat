using HarmonyLib;
using Verse;
using RimWorld;
using TheSecondSeat.Components;

namespace TheSecondSeat.Patches
{
    /// <summary>
    /// Patch Pawn.IsColonist 属性 getter
    /// 让降临体（带有 CompDraftableAnimal 的 Pawn）被系统识别为殖民者
    /// 这样可以让降临体拥有殖民者的右键菜单（移动、攻击等）
    /// </summary>
    [HarmonyPatch(typeof(Pawn), "get_IsColonist")]
    public static class Pawn_IsColonist_Patch
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
            
            // 活着或有复活能力
            if (__instance.Dead && !__instance.HasDeathRefusalOrResurrecting) return;
            
            // 让降临体被识别为殖民者
            __result = true;
        }
    }
}