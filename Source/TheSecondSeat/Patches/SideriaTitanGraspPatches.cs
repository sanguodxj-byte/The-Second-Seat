using System;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using UnityEngine;

namespace TheSecondSeat.Patches
{
    /// <summary>
    /// ⭐ v1.8.7: Sideria 强化版战术投掷补丁
    /// 依赖: Tactical Throws (TitanGrasp) mod
    /// 效果: Sideria 使用擒拿时必定成功 + 所有伤害翻倍
    /// </summary>
    [StaticConstructorOnStartup]
    public static class SideriaTitanGraspPatches
    {
        // Sideria 伤害倍率
        public const float SIDERIA_DAMAGE_MULTIPLIER = 2.0f;
        
        // Sideria ThingDef 名称
        public const string SIDERIA_RACE_DEF = "Sideria_DescentRace";
        
        static SideriaTitanGraspPatches()
        {
            try
            {
                // 检查 TitanGrasp mod 是否加载
                var titanGraspAssembly = AccessTools.TypeByName("TitanGrasp.CompAbilityEffect_Grapple");
                if (titanGraspAssembly == null)
                {
                    if (Prefs.DevMode)
                        Log.Message("[The Second Seat] TitanGrasp mod 未加载，跳过擒拿补丁");
                    return;
                }
                
                var harmony = new Harmony("thesecondseat.sideria.titangrasp");
                
                // 补丁1: Apply 方法 - 必定成功
                var originalApply = AccessTools.Method(titanGraspAssembly, "Apply");
                if (originalApply != null)
                {
                    harmony.Patch(originalApply, 
                        prefix: new HarmonyMethod(typeof(SideriaTitanGraspPatches), nameof(Apply_Prefix)));
                }
                
                // 补丁2: Slam伤害计算
                var slamType = AccessTools.TypeByName("TitanGrasp.JobDriver_Slam");
                if (slamType != null)
                {
                    var slamMethod = AccessTools.Method(slamType, "MakeNewToils");
                    if (slamMethod != null)
                    {
                        // 使用Postfix拦截伤害
                        harmony.Patch(slamMethod,
                            postfix: new HarmonyMethod(typeof(SideriaTitanGraspPatches), nameof(Slam_Postfix)));
                    }
                }
                
                // 补丁3: 投掷伤害
                var projectileType = AccessTools.TypeByName("TitanGrasp.Projectile_LivingPawn");
                if (projectileType != null)
                {
                    var impactMethod = AccessTools.Method(projectileType, "Impact");
                    if (impactMethod != null)
                    {
                        harmony.Patch(impactMethod,
                            prefix: new HarmonyMethod(typeof(SideriaTitanGraspPatches), nameof(Impact_Prefix)));
                    }
                }
                
                Log.Message("[The Second Seat] ⭐ Sideria神威擒拿补丁已加载");
            }
            catch (Exception ex)
            {
                Log.Warning($"[The Second Seat] TitanGrasp补丁失败（可能mod未加载）: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 检查Pawn是否是Sideria
        /// </summary>
        public static bool IsSideria(Pawn pawn)
        {
            if (pawn == null) return false;
            return pawn.def.defName == SIDERIA_RACE_DEF;
        }
        
        /// <summary>
        /// 补丁: 擒拿Apply - Sideria必定成功
        /// </summary>
        public static bool Apply_Prefix(object __instance, LocalTargetInfo target, LocalTargetInfo dest)
        {
            try
            {
                // 获取 parent.pawn
                var parentField = AccessTools.Field(__instance.GetType().BaseType, "parent");
                if (parentField == null) return true;
                
                var parent = parentField.GetValue(__instance);
                if (parent == null) return true;
                
                var pawnProp = AccessTools.Property(parent.GetType(), "pawn");
                if (pawnProp == null) return true;
                
                var caster = pawnProp.GetValue(parent) as Pawn;
                if (caster == null || !IsSideria(caster)) return true;
                
                // Sideria: 跳过原有判定逻辑，直接成功
                Pawn victim = target.Pawn;
                if (victim == null) return true;
                
                // 直接开始擒拿Job（必定成功）
                var titanDefOf = AccessTools.TypeByName("TitanGrasp.TitanDefOf");
                if (titanDefOf == null) return true;
                
                var grappleHoldDef = AccessTools.Field(titanDefOf, "Titan_GrappleHold")?.GetValue(null) as JobDef;
                if (grappleHoldDef == null) return true;
                
                Job job = JobMaker.MakeJob(grappleHoldDef, victim);
                job.count = 1;
                caster.jobs.TryTakeOrderedJob(job, JobTag.Misc);
                
                Messages.Message($"Sideria 神威擒拿成功！", caster, MessageTypeDefOf.PositiveEvent);
                MoteMaker.ThrowText(caster.DrawPos, caster.Map, "神威擒拿！", Color.cyan);
                
                return false; // 跳过原方法
            }
            catch (Exception ex)
            {
                Log.Warning($"[Sideria] Apply_Prefix 异常: {ex.Message}");
                return true; // 出错时执行原方法
            }
        }
        
        /// <summary>
        /// 临时存储Sideria伤害倍率
        /// </summary>
        [ThreadStatic]
        private static float currentDamageMultiplier = 1f;
        
        /// <summary>
        /// 补丁: Slam伤害翻倍
        /// </summary>
        public static void Slam_Postfix(object __instance)
        {
            // 通过Traverse获取pawn
            try
            {
                var pawn = Traverse.Create(__instance).Field("pawn").GetValue<Pawn>();
                if (pawn != null && IsSideria(pawn))
                {
                    currentDamageMultiplier = SIDERIA_DAMAGE_MULTIPLIER;
                }
                else
                {
                    currentDamageMultiplier = 1f;
                }
            }
            catch { currentDamageMultiplier = 1f; }
        }
        
        /// <summary>
        /// 补丁: 投掷撞击伤害翻倍
        /// </summary>
        public static void Impact_Prefix(object __instance)
        {
            try
            {
                var launcher = Traverse.Create(__instance).Field("launcher").GetValue<Thing>();
                if (launcher is Pawn caster && IsSideria(caster))
                {
                    currentDamageMultiplier = SIDERIA_DAMAGE_MULTIPLIER;
                }
                else
                {
                    currentDamageMultiplier = 1f;
                }
            }
            catch { currentDamageMultiplier = 1f; }
        }
        
        /// <summary>
        /// 获取当前Sideria伤害倍率（供其他补丁使用）
        /// </summary>
        public static float GetCurrentDamageMultiplier()
        {
            return currentDamageMultiplier;
        }
    }
    
    /// <summary>
    /// 补丁: 拦截TakeDamage来应用Sideria伤害加成
    /// </summary>
    [HarmonyPatch(typeof(Thing), "TakeDamage")]
    public static class Thing_TakeDamage_SideriaPatch
    {
        public static void Prefix(ref DamageInfo dinfo)
        {
            try
            {
                // 检查攻击者是否是Sideria
                if (dinfo.Instigator is Pawn attacker && 
                    SideriaTitanGraspPatches.IsSideria(attacker))
                {
                    // 检查是否在擒拿动作中
                    if (attacker.CurJobDef?.defName?.StartsWith("Titan_") == true)
                    {
                        // 伤害翻倍
                        float newDamage = dinfo.Amount * SideriaTitanGraspPatches.SIDERIA_DAMAGE_MULTIPLIER;
                        dinfo.SetAmount(newDamage);
                    }
                }
            }
            catch { }
        }
    }
}