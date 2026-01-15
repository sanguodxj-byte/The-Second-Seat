using HarmonyLib;
using Verse;
using RimWorld;
using TheSecondSeat.Descent;

namespace TheSecondSeat.Patches
{
    /// <summary>
    /// Patch Pawn.IsColonistPlayerControlled 属性 getter
    /// 原版的征召按钮显示逻辑检查这个属性
    /// 让降临体也能显示征召按钮
    /// </summary>
    [HarmonyPatch(typeof(Pawn), "get_IsColonistPlayerControlled")]
    public static class Pawn_IsColonistPlayerControlled_Patch
    {
        public static void Postfix(Pawn __instance, ref bool __result)
        {
            // 如果原版已经返回 true，不需要干预
            if (__result) return;
            
            if (__instance == null) return;
            
            // 检查是否是降临体（通过注册系统判断）
            if (!DescentEntityRegistry.IsDescentEntity(__instance)) return;
            
            // 必须属于玩家派系
            if (__instance.Faction != Faction.OfPlayer) return;
            
            // 必须在地图上
            if (!__instance.Spawned) return;
            
            // 不能是心智失常状态
            if (__instance.InMentalState) return;
            
            // 让降临体被识别为可控制的殖民者
            __result = true;
            
            // Log.Message($"[TSS-Debug] Pawn_IsColonistPlayerControlled_Patch: {__instance.LabelShort} -> true");
        }
    }
}