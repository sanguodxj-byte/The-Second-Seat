using HarmonyLib;
using RimWorld;
using Verse;
using TheSecondSeat.Descent;

namespace TheSecondSeat.Patches
{
    [HarmonyPatch(typeof(PawnGenerator), "GeneratePawn", new[] { typeof(PawnGenerationRequest) })]
    public static class PawnGenerator_GeneratePawn_Patch
    {
        /// <summary>
        /// 在 Pawn 生成后，检查其是否为降临实体，并赋予其在 NarratorPersonaDef 中定义的 Hediffs 和技能。
        /// 这是连接 XML 配置和实际游戏效果的核心环节。
        /// </summary>
        [HarmonyPostfix]
        public static void Postfix(Pawn __result)
        {
            // 确保生成的 Pawn 不为空
            if (__result == null) return;

            // 检查 Pawn 是否为我们系统中的降临实体
            if (DescentEntityRegistry.IsDescentEntity(__result))
            {
                // 安全获取 pawn 名称，防止空引用
                string pawnName = __result.Name?.ToStringShort ?? __result.LabelShort ?? __result.def?.defName ?? "Unknown";
                string raceName = __result.def?.defName ?? "Unknown";
                
                Log.Message($"[TSS-Patch] Detected descent entity '{pawnName}' of race '{raceName}'. Applying properties.");

                // 赋予 Hediffs
                var hediffsToGrant = DescentEntityRegistry.GetHediffsToGrant(__result);
                if (!hediffsToGrant.NullOrEmpty())
                {
                    foreach (var hediffDefName in hediffsToGrant)
                    {
                        var hediffDef = DefDatabase<HediffDef>.GetNamed(hediffDefName, false);
                        if (hediffDef != null)
                        {
                            __result.health?.AddHediff(hediffDef);
                            Log.Message($"[TSS-Patch] Applied hediff '{hediffDefName}' to '{pawnName}'.");
                        }
                        else
                        {
                            Log.Warning($"[TSS-Patch] Could not find HediffDef named '{hediffDefName}' to apply.");
                        }
                    }
                }

                // 赋予技能
                // 注意：动物类型的 Pawn 默认没有 abilities tracker
                // 技能系统需要通过 Hediff (如 HediffComp_DivineBody) 或其他方式赋予
                var abilitiesToGrant = DescentEntityRegistry.GetAbilitiesToGrant(__result);
                if (!abilitiesToGrant.NullOrEmpty())
                {
                    if (__result.abilities == null)
                    {
                        // 动物没有 abilities 组件是正常的，技能将通过 Hediff 系统赋予
                        // 不再报错，改为 DevMode 下的信息提示
                        if (Prefs.DevMode)
                        {
                            Log.Message($"[TSS-Patch] Pawn '{pawnName}' has no abilities tracker. Abilities will be granted via Hediff system (HediffComp_DivineBody).");
                        }
                    }
                    else
                    {
                        foreach (var abilityDefName in abilitiesToGrant)
                        {
                            var abilityDef = DefDatabase<AbilityDef>.GetNamed(abilityDefName, false);
                            if (abilityDef != null)
                            {
                                __result.abilities.GainAbility(abilityDef);
                                Log.Message($"[TSS-Patch] Applied ability '{abilityDefName}' to '{pawnName}'.");
                            }
                            else
                            {
                                Log.Warning($"[TSS-Patch] Could not find AbilityDef named '{abilityDefName}' to apply.");
                            }
                        }
                    }
                }
            }
        }
    }
}
