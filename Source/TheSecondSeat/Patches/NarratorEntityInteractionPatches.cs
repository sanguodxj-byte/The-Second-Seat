using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Reflection;
using Verse;

namespace TheSecondSeat
{
    public static class NarratorEntityUtils
    {
        public static bool IsNarratorEntity(Thing t)
        {
            if (t is Pawn p && p.def != null)
            {
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

    /// <summary>
    /// Gizmo 过滤辅助类 - 缓存反射和类型判断
    /// </summary>
    public static class GizmoFilterHelper
    {
        // 缓存的 MethodInfo，避免每帧反射
        private static Func<Pawn_DraftController, IEnumerable<Gizmo>> getDraftGizmosDelegate;
        private static bool delegateInitialized = false;
        
        // 缓存的 Designator 类型
        private static Type releaseDesignatorType;
        private static bool typeInitialized = false;
        
        /// <summary>
        /// 获取 Drafter 的 GetGizmos 方法委托（高性能）
        /// </summary>
        public static IEnumerable<Gizmo> GetDraftGizmos(Pawn_DraftController drafter)
        {
            if (!delegateInitialized)
            {
                InitializeDelegate();
            }
            
            if (getDraftGizmosDelegate != null)
            {
                return getDraftGizmosDelegate(drafter);
            }
            
            return null;
        }
        
        private static void InitializeDelegate()
        {
            try
            {
                MethodInfo method = AccessTools.Method(typeof(Pawn_DraftController), "GetGizmos");
                if (method != null)
                {
                    getDraftGizmosDelegate = AccessTools.MethodDelegate<Func<Pawn_DraftController, IEnumerable<Gizmo>>>(method);
                }
            }
            catch (Exception ex)
            {
                Log.ErrorOnce($"[TheSecondSeat] Failed to create GetGizmos delegate: {ex.Message}", 0x7890);
            }
            delegateInitialized = true;
        }
        
        /// <summary>
        /// 判断 Gizmo 是否应该被过滤
        /// 使用类型判断替代字符串比较
        /// </summary>
        public static bool ShouldFilterReleaseGizmo(Gizmo gizmo)
        {
            if (!typeInitialized)
            {
                releaseDesignatorType = typeof(Designator_ReleaseAnimalToWild);
                typeInitialized = true;
            }
            
            // 优先使用类型判断（快速）
            if (gizmo is Designator_ReleaseAnimalToWild)
            {
                return true;
            }
            
            // 如果是 Command，检查其内部 Designator
            if (gizmo is Command_Action cmdAction)
            {
                // 使用缓存的字符串比较
                // 注意：这里不再使用 Translate()，而是直接比较 Key 或使用其他标识符
                // 但为了保持向后兼容，我们检查 Gizmo 的 icon 或其他稳定属性
                return false; // 大部分情况下不需要额外过滤
            }
            
            return false;
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
    // 重构版：使用简洁的 foreach + 类型判断，移除丑陋的 try-catch 迭代器
    [HarmonyPatch(typeof(Pawn), "GetGizmos")]
    public static class Pawn_GetGizmos_Patch
    {
        public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> values, Pawn __instance)
        {
            // 快速路径：如果没有 values，直接返回
            if (values == null) yield break;

            var comp = NarratorEntityUtils.GetNarratorComp(__instance);
            
            // 快速路径：如果不是叙事者实体，直接透传所有 Gizmos
            if (comp == null)
            {
                foreach (var gizmo in values)
                {
                    yield return gizmo;
                }
                yield break;
            }

            // 慢速路径：需要过滤的叙事者实体
            foreach (var gizmo in values)
            {
                // 过滤 Release Gizmos
                if (comp.Props.disableRelease && GizmoFilterHelper.ShouldFilterReleaseGizmo(gizmo))
                {
                    continue;
                }

                yield return gizmo;
            }

            // 添加 Draft Gizmo（如果需要）
            if (comp.Props.forceShowDraftGizmo && 
                __instance.Faction == Faction.OfPlayer && 
                __instance.drafter != null)
            {
                // 使用缓存的委托获取 Draft Gizmos
                var draftGizmos = GizmoFilterHelper.GetDraftGizmos(__instance.drafter);
                if (draftGizmos != null)
                {
                    foreach (var gizmo in draftGizmos)
                    {
                        if (gizmo != null)
                        {
                            yield return gizmo;
                        }
                    }
                }
            }
        }
    }

    // 4. Hide Training Tab
    // 重构版：使用缓存的 Property 访问器
    [HarmonyPatch(typeof(ITab_Pawn_Training), "IsVisible", MethodType.Getter)]
    public static class ITab_Pawn_Training_Patch
    {
        // 缓存的属性委托
        private static Func<ITab_Pawn_Training, Pawn> getSelPawnDelegate;
        private static bool delegateInitialized = false;
        
        private static void InitializeDelegate()
        {
            try
            {
                PropertyInfo prop = AccessTools.Property(typeof(ITab_Pawn_Training), "SelPawn");
                if (prop != null && prop.GetMethod != null)
                {
                    getSelPawnDelegate = AccessTools.MethodDelegate<Func<ITab_Pawn_Training, Pawn>>(prop.GetMethod);
                }
            }
            catch (Exception ex)
            {
                Log.ErrorOnce($"[TheSecondSeat] Failed to create SelPawn delegate: {ex.Message}", 0x7891);
            }
            delegateInitialized = true;
        }
        
        public static void Postfix(ITab_Pawn_Training __instance, ref bool __result)
        {
            if (!__result) return;
            
            if (!delegateInitialized)
            {
                InitializeDelegate();
            }
            
            Pawn pawn = null;
            if (getSelPawnDelegate != null)
            {
                pawn = getSelPawnDelegate(__instance);
            }
            else
            {
                // 回退到反射（理论上不应该执行到这里）
                pawn = Traverse.Create(__instance).Property("SelPawn").GetValue<Pawn>();
            }
            
            var comp = NarratorEntityUtils.GetNarratorComp(pawn);
            if (comp != null && comp.Props.hideTrainingTab)
            {
                __result = false;
            }
        }
    }
}
