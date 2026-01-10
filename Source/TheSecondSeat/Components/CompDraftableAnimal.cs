using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;

namespace TheSecondSeat.Components
{
    /// <summary>
    /// CompDraftableAnimal - 最终修正版
    /// 逻辑回归：不修改 Pawn.Drafted，只通过组件控制 UI 和行为。
    /// 这是最稳定、最符合 Koelime 原理的写法。
    /// </summary>
    public class CompDraftableAnimal : ThingComp
    {
        public bool isDrafted = false;
        public Pawn Pawn => parent as Pawn;

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref isDrafted, "isDrafted", false);
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            // 只有属于玩家阵营才显示
            if (parent.Faction != Faction.OfPlayer) yield break;

            // 征召开关
            Command_Toggle cmd = new Command_Toggle();
            cmd.defaultLabel = "CommandDraft".Translate();
            cmd.defaultDesc = "CommandDraftDesc".Translate();
            cmd.icon = TexCommand.Draft;
            cmd.isActive = () => isDrafted;
            cmd.toggleAction = delegate ()
            {
                isDrafted = !isDrafted;
                // 切换状态时打断当前发呆/乱跑
                Pawn p = parent as Pawn;
                if (p != null && p.jobs != null)
                {
                    p.jobs.EndCurrentJob(JobCondition.InterruptForced);
                }
            };
            cmd.hotKey = KeyBindingDefOf.Command_ColonistDraft;
            yield return cmd;

            // 技能按钮
            if (Pawn != null && Pawn.abilities != null)
            {
                foreach (Ability ability in Pawn.abilities.abilities)
                {
                    foreach (Gizmo gizmo in ability.GetGizmos()) yield return gizmo;
                }
            }
        }
    }

    public class CompProperties_DraftableAnimal : CompProperties
    {
        public CompProperties_DraftableAnimal()
        {
            this.compClass = typeof(CompDraftableAnimal);
        }
    }

    // ==================== Harmony Patches ====================
    public static class DraftableAnimalHarmonyPatches
    {
        private static bool patched = false;

        public static void ApplyPatches(Harmony harmony)
        {
            if (patched) return;

            try
            {
                // Patch 1: 过滤原版动物按钮 (比如"屠宰")
                var getGizmosMethod = AccessTools.Method(typeof(Pawn), "GetGizmos");
                if (getGizmosMethod != null)
                {
                    harmony.Patch(getGizmosMethod, postfix: new HarmonyMethod(typeof(DraftableAnimalHarmonyPatches), nameof(Pawn_GetGizmos_Postfix)));
                    Log.Message("[TSS] Patched Pawn.GetGizmos");
                }
                else
                {
                    Log.Warning("[TSS] Could not find Pawn.GetGizmos method");
                }

                // Patch 2: 注入右键菜单 (核心逻辑)
                // 使用精确的方法签名查找
                var choicesAtForMethod = AccessTools.Method(
                    typeof(FloatMenuMakerMap),
                    "ChoicesAtFor",
                    new Type[] { typeof(Vector3), typeof(Pawn), typeof(bool) }
                );
                
                if (choicesAtForMethod != null)
                {
                    harmony.Patch(choicesAtForMethod, postfix: new HarmonyMethod(typeof(DraftableAnimalHarmonyPatches), nameof(ChoicesAtFor_Postfix)));
                    Log.Message("[TSS] Patched FloatMenuMakerMap.ChoicesAtFor");
                }
                else
                {
                    Log.Warning("[TSS] Could not find FloatMenuMakerMap.ChoicesAtFor method, trying alternative...");
                    // 尝试不带参数的查找
                    var altMethod = AccessTools.Method(typeof(FloatMenuMakerMap), "ChoicesAtFor");
                    if (altMethod != null)
                    {
                        harmony.Patch(altMethod, postfix: new HarmonyMethod(typeof(DraftableAnimalHarmonyPatches), nameof(ChoicesAtFor_Postfix)));
                        Log.Message("[TSS] Patched FloatMenuMakerMap.ChoicesAtFor (alt)");
                    }
                    else
                    {
                        Log.Warning("[TSS] FloatMenuMakerMap.ChoicesAtFor not found - right-click menu won't work");
                    }
                }

                // ⚠️ 删除了 Pawn.Drafted 和 AddHumanlikeOrders 的所有 Patch
                // 只要不乱改 Drafted 属性，就不会导致生成时崩溃。
                
                patched = true;
                Log.Message("[TSS] DraftableAnimal patches applied (Clean Strategy).");
            }
            catch (Exception ex)
            {
                Log.Error($"[TSS] Patch Failed: {ex}");
            }
        }

        // 过滤不需要的动物按钮
        public static IEnumerable<Gizmo> Pawn_GetGizmos_Postfix(IEnumerable<Gizmo> __result, Pawn __instance)
        {
            if (__instance.GetComp<CompDraftableAnimal>() == null)
            {
                foreach (var g in __result) yield return g;
                yield break;
            }
            foreach (var g in __result)
            {
                if (g is Command cmd && cmd.defaultLabel != null)
                {
                    string label = cmd.defaultLabel.ToString().ToLower();
                    if (label.Contains("slaughter") || label.Contains("release") || label.Contains("train")) continue;
                }
                yield return g;
            }
        }

        // 核心：手动添加移动和攻击指令
        // 原理：因为 Pawn.Drafted 是 false，原版只会生成基本交互（如救援）。
        // 我们在这里追加"伪征召"的战斗指令。
        public static void ChoicesAtFor_Postfix(ref List<FloatMenuOption> __result, Vector3 clickPos, Pawn pawn, bool suppressAutoTakeableGoto = false)
        {
            if (pawn == null || pawn.Faction != Faction.OfPlayer) return;
            
            var comp = pawn.GetComp<CompDraftableAnimal>();
            // 只有当组件存在 且 处于我们自定义的征召状态时
            if (comp == null || !comp.isDrafted) return;

            IntVec3 clickCell = IntVec3.FromVector3(clickPos);
            Map map = pawn.Map;
            if (map == null) return;

            // 1. 移动命令 (Go Here)
            if (clickCell.Standable(map) && pawn.CanReach(clickCell, PathEndMode.OnCell, Danger.Deadly))
            {
                // 检查是否重复
                if (!__result.Any(o => o.Label.Contains("GoHere".Translate()) || o.Label.Contains("Go here")))
                {
                    Pawn p = pawn;
                    IntVec3 dest = clickCell;
                    Map destMap = map;
                    __result.Add(new FloatMenuOption("GoHere".Translate(), delegate()
                    {
                        Job job = JobMaker.MakeJob(JobDefOf.Goto, dest);
                        job.playerForced = true; // 强制执行，打断发呆
                        p.jobs.TryTakeOrderedJob(job, JobTag.Misc);
                    }, MenuOptionPriority.GoHere));
                }
            }

            // 2. 攻击命令 (Attack)
            foreach (Thing t in clickCell.GetThingList(map))
            {
                if (t is Pawn target && target != pawn && target.HostileTo(pawn))
                {
                    Pawn p = pawn;
                    string label = "MeleeAttack".Translate(target.LabelShort, target);
                    
                    if (!__result.Any(o => o.Label == label))
                    {
                        Pawn attackTarget = target;
                        // 插入到最前面，方便操作
                        __result.Insert(0, new FloatMenuOption(label, delegate()
                        {
                            Job job = JobMaker.MakeJob(JobDefOf.AttackMelee, attackTarget);
                            job.playerForced = true;
                            p.jobs.TryTakeOrderedJob(job, JobTag.Misc);
                        }, MenuOptionPriority.AttackEnemy));
                    }
                }
            }
        }
    }
}
