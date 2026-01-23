using HarmonyLib;
using Verse;
using RimWorld;
using TheSecondSeat.Descent;

namespace TheSecondSeat.Patches
{
    /// <summary>
    /// Patch Pawn.SpawnSetup 方法
    /// 在降临体生成时注入原版 Pawn_DraftController 组件和 Pawn_AbilityTracker 组件
    /// 这样降临体就可以使用原版的征召系统，包括：
    /// - 原版征召按钮
    /// - 原版右键移动/攻击菜单
    /// - 原版所有与征召相关的功能
    /// 并根据 NarratorPersonaDef 中配置的 abilitiesToGrant 赋予技能
    /// </summary>
    [HarmonyPatch(typeof(Pawn), "SpawnSetup")]
    public static class Pawn_SpawnSetup_Patch
    {
        public static void Postfix(Pawn __instance, bool respawningAfterLoad)
        {
            // 只处理降临体（通过注册系统判断）
            if (!DescentEntityRegistry.IsDescentEntity(__instance))
                return;
            
            Log.Message($"[TSS-Debug] SpawnSetup for DescentEntity: {__instance.LabelShort}, respawning={respawningAfterLoad}, faction={__instance.Faction?.Name ?? "null"}, drafter={__instance.drafter != null}");
            
            // 必须属于玩家派系
            if (__instance.Faction != Faction.OfPlayer)
            {
                Log.Message($"[TSS-Debug] DescentEntity {__instance.LabelShort} not player faction, skipping drafter injection");
                return;
            }
            
            // 总是确保有 drafter（即使是从存档加载）
            if (__instance.drafter == null)
            {
                __instance.drafter = new Pawn_DraftController(__instance);
                Log.Message($"[TSS] Injected NEW Pawn_DraftController for DescentEntity: {__instance.LabelShort}");
            }
            else
            {
                Log.Message($"[TSS-Debug] DescentEntity {__instance.LabelShort} already has drafter, Drafted={__instance.drafter.Drafted}");
            }

            // 总是确保有 equipment（即使是 Animal 类型的降临体），否则 Pawn_DraftController.GetGizmos 会因为空引用报错
            if (__instance.equipment == null)
            {
                __instance.equipment = new Pawn_EquipmentTracker(__instance);
                Log.Message($"[TSS] Injected NEW Pawn_EquipmentTracker for DescentEntity: {__instance.LabelShort}");
            }
            
            // 注入 abilities tracker（如果没有）
            if (__instance.abilities == null)
            {
                __instance.abilities = new Pawn_AbilityTracker(__instance);
                Log.Message($"[TSS] Injected NEW Pawn_AbilityTracker for DescentEntity: {__instance.LabelShort}");
            }
            
            // 根据 NarratorPersonaDef 配置赋予技能
            GrantDescentAbilities(__instance);
            
            // 根据 NarratorPersonaDef 配置添加 Hediffs
            ApplyDescentHediffs(__instance);
            
            // 调试：检查智力级别
            Log.Message($"[TSS-Debug] DescentEntity {__instance.LabelShort} intelligence={__instance.RaceProps?.intelligence}, Drafted property={__instance.Drafted}");
        }

        /// <summary>
        /// 根据 NarratorPersonaDef 配置赋予降临体技能
        /// </summary>
        private static void GrantDescentAbilities(Pawn pawn)
        {
            if (pawn.abilities == null)
            {
                Log.Warning($"[TSS] Cannot grant abilities to {pawn.LabelShort}: no ability tracker");
                return;
            }

            // 从注册系统获取应赋予的技能列表
            var abilityDefNames = DescentEntityRegistry.GetAbilitiesToGrant(pawn);
            
            foreach (string abilityDefName in abilityDefNames)
            {
                if (string.IsNullOrEmpty(abilityDefName)) continue;
                
                AbilityDef abilityDef = DefDatabase<AbilityDef>.GetNamedSilentFail(abilityDefName);
                
                if (abilityDef == null)
                {
                    Log.Warning($"[TSS] AbilityDef '{abilityDefName}' not found for DescentEntity {pawn.LabelShort}");
                    continue;
                }
                
                if (!HasAbility(pawn, abilityDef))
                {
                    pawn.abilities.GainAbility(abilityDef);
                    Log.Message($"[TSS] Granted ability '{abilityDef.label}' to {pawn.LabelShort}");
                }
            }
        }

        /// <summary>
        /// 根据 NarratorPersonaDef 配置添加 Hediffs
        /// </summary>
        private static void ApplyDescentHediffs(Pawn pawn)
        {
            if (pawn.health?.hediffSet == null) return;
            
            // 从注册系统获取应添加的 Hediff 列表
            var hediffDefNames = DescentEntityRegistry.GetHediffsToGrant(pawn);
            
            foreach (string hediffDefName in hediffDefNames)
            {
                if (string.IsNullOrEmpty(hediffDefName)) continue;
                
                HediffDef hediffDef = DefDatabase<HediffDef>.GetNamedSilentFail(hediffDefName);
                
                if (hediffDef == null)
                {
                    Log.Warning($"[TSS] HediffDef '{hediffDefName}' not found for DescentEntity {pawn.LabelShort}");
                    continue;
                }
                
                // 检查是否已有此 Hediff
                if (pawn.health.hediffSet.HasHediff(hediffDef)) continue;
                
                Hediff hediff = HediffMaker.MakeHediff(hediffDef, pawn);
                pawn.health.AddHediff(hediff);
                Log.Message($"[TSS] Applied hediff '{hediffDef.label}' to {pawn.LabelShort}");
            }
        }

        /// <summary>
        /// 检查 Pawn 是否已拥有某个技能
        /// </summary>
        private static bool HasAbility(Pawn pawn, AbilityDef abilityDef)
        {
            if (pawn.abilities?.abilities == null) return false;
            
            foreach (Ability ability in pawn.abilities.abilities)
            {
                if (ability.def == abilityDef) return true;
            }
            return false;
        }
    }
}