using HarmonyLib;
using RimWorld;
using Verse;
using System;
using System.Reflection;
using System.Collections.Generic;
using TheSecondSeat.Components;

namespace TheSecondSeat.Patches
{
    /// <summary>
    /// Patch FloatMenuOptionProvider_DraftedAttack to handle Sideria pawns without drafter
    /// This prevents NullReferenceException when the game tries to access drafter on Sideria
    ///
    /// 由于 FloatMenuOptionProvider_DraftedAttack 在检查 pawn.Drafted 时会访问 pawn.drafter，
    /// 而降临体没有 drafter 组件会导致空引用错误。
    /// 这个类的 GetFloatMenuOptions 方法会在右键菜单时被调用。
    /// </summary>
    [StaticConstructorOnStartup]
    public static class FloatMenuOptionProvider_DraftedAttack_Patch
    {
        static FloatMenuOptionProvider_DraftedAttack_Patch()
        {
            ApplyPatchesInternal();
        }
        
        private static bool isPatched = false;
        
        public static void ApplyPatches(Harmony harmony)
        {
            // 已在静态构造函数中应用，这里仅用于兼容旧调用
        }
        
        private static void ApplyPatchesInternal()
        {
            if (isPatched) return;
            
            try
            {
                var harmony = new Harmony("thesecondseat.floatmenufix");
                
                // Find the FloatMenuOptionProvider_DraftedAttack type
                Type providerType = AccessTools.TypeByName("RimWorld.FloatMenuOptionProvider_DraftedAttack");
                if (providerType == null)
                {
                    Log.Warning("[TSS] Could not find FloatMenuOptionProvider_DraftedAttack type");
                    return;
                }
                
                // Patch GetFloatMenuOptions method specifically
                var getOptionsMethod = AccessTools.Method(providerType, "GetFloatMenuOptions");
                if (getOptionsMethod != null)
                {
                    harmony.Patch(getOptionsMethod,
                        prefix: new HarmonyMethod(typeof(FloatMenuOptionProvider_DraftedAttack_Patch), nameof(GetFloatMenuOptionsPrefix)));
                    Log.Message("[TSS] Patched FloatMenuOptionProvider_DraftedAttack.GetFloatMenuOptions");
                }
                else
                {
                    Log.Warning("[TSS] Could not find GetFloatMenuOptions method");
                }
                
                isPatched = true;
                Log.Message("[TSS] FloatMenuOptionProvider_DraftedAttack patching complete");
            }
            catch (Exception e)
            {
                Log.Error($"[TSS] Error patching FloatMenuOptionProvider_DraftedAttack: {e}");
            }
        }
        
        /// <summary>
        /// Prefix for GetFloatMenuOptions - skip for Sideria pawns
        /// 方法签名: IEnumerable<FloatMenuOption> GetFloatMenuOptions(Pawn clickedPawn, Pawn selPawn)
        /// </summary>
        public static bool GetFloatMenuOptionsPrefix(Pawn clickedPawn, Pawn selPawn, ref IEnumerable<FloatMenuOption> __result)
        {
            try
            {
                // 检查 selPawn（选中的 pawn）是否是降临体
                if (selPawn != null)
                {
                    var comp = selPawn.GetComp<CompDraftableAnimal>();
                    if (comp != null)
                    {
                        // 是降临体，返回空列表
                        __result = new List<FloatMenuOption>();
                        return false; // Skip original
                    }
                }
                
                // 也检查 clickedPawn
                if (clickedPawn != null)
                {
                    var comp = clickedPawn.GetComp<CompDraftableAnimal>();
                    if (comp != null)
                    {
                        // 点击的是降临体，返回空列表
                        __result = new List<FloatMenuOption>();
                        return false; // Skip original
                    }
                }
            }
            catch (Exception)
            {
                // 静默处理错误，运行原方法
            }
            
            return true;
        }
    }
}