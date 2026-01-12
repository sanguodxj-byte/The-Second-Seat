using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace TheSecondSeat.Components
{
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
            if (parent.Faction != Faction.OfPlayer) yield break;

            // 降临体使用自定义征召系统
            // Pawn.Drafted getter patch 会读取我们的 isDrafted 状态，使右键菜单正常工作
            // 注意：降临体作为动物没有原版 drafter 组件，我们提供替代的征召按钮
            
            // 使用原版征召/解除征召的标签
            string draftLabel = isDrafted ? "CommandUndraft".Translate() : "CommandDraft".Translate();
            string draftDesc = isDrafted ? "CommandUndraftDesc".Translate() : "CommandDraftDesc".Translate();
            
            Command_Toggle cmd = new Command_Toggle
            {
                defaultLabel = draftLabel,
                defaultDesc = draftDesc,
                icon = TexCommand.Draft,
                isActive = () => isDrafted,
                toggleAction = delegate
                {
                    isDrafted = !isDrafted;
                    if (isDrafted)
                    {
                        // 进入征召状态: 打断当前工作，进入待命
                        Pawn.jobs.EndCurrentJob(JobCondition.InterruptForced, true);
                        var newJob = JobMaker.MakeJob(JobDefOf.Wait_Combat, 2500);
                        newJob.playerForced = true;
                        Pawn.jobs.TryTakeOrderedJob(newJob, JobTag.DraftedOrder);
                    }
                    else
                    {
                        // 退出征召状态: 允许AI自由活动
                        Pawn.jobs.EndCurrentJob(JobCondition.InterruptForced, true);
                    }
                },
                hotKey = KeyBindingDefOf.Command_ColonistDraft
            };
            yield return cmd;
        }
    }

    public class CompProperties_DraftableAnimal : CompProperties
    {
        public CompProperties_DraftableAnimal()
        {
            compClass = typeof(CompDraftableAnimal);
        }
    }

    [HarmonyPatch]
    public static class DraftableAnimalHarmonyPatches
    {
        public static bool isPatched = false;

        public static void ApplyPatches(Harmony harmony)
        {
            if (isPatched) return;
            
            var targetMethod = AccessTools.GetDeclaredMethods(typeof(FloatMenuMakerMap))
                .FirstOrDefault(m => m.Name == "GetOptions" && m.GetParameters().Length >= 2 && m.GetParameters()[0].ParameterType == typeof(List<Pawn>));

            if (targetMethod != null)
            {
                harmony.Patch(targetMethod, postfix: new HarmonyMethod(typeof(DraftableAnimalHarmonyPatches), nameof(GetOptions_Postfix)));
                Log.Message("[TSS] Successfully patched FloatMenuMakerMap.GetOptions for custom drafting.");
            }
            else
            {
                Log.Warning("[TSS] FAILED to find target method for right-click menu patch.");
            }

            isPatched = true;
        }

        public static void GetOptions_Postfix(ref List<FloatMenuOption> __result, Vector3 clickPos, List<Pawn> selectedPawns)
        {
            if (selectedPawns == null || selectedPawns.Count != 1) return;
            var pawn = selectedPawns[0];

            if (pawn == null || pawn.Faction != Faction.OfPlayer) return;
            
            var comp = pawn.GetComp<CompDraftableAnimal>();
            if (comp == null || !comp.isDrafted) return;

            IntVec3 clickCell = IntVec3.FromVector3(clickPos);
            Map map = pawn.Map;
            if (map == null) return;

            // 移动命令
            if (clickCell.Standable(map) && pawn.CanReach(clickCell, PathEndMode.OnCell, Danger.Deadly))
            {
                if (!__result.Any(o => o.Label.Contains("GoHere".Translate())))
                {
                    Action action = delegate
                    {
                        Job job = JobMaker.MakeJob(JobDefOf.Goto, clickCell);
                        pawn.jobs.TryTakeOrderedJob(job, JobTag.DraftedOrder);
                    };
                    __result.Add(new FloatMenuOption("GoHere".Translate(), action, MenuOptionPriority.GoHere));
                }
            }

            // 攻击命令 - 允许攻击任何目标（敌对或友好）
            foreach (Thing t in clickCell.GetThingList(map))
            {
                // 近战攻击目标
                if (t is Pawn target && target != pawn && !target.Downed)
                {
                    string label = "Melee".Translate() + " " + target.LabelCap;
                    if (!__result.Any(o => o.Label == label))
                    {
                        Action action = delegate
                        {
                            Job job = JobMaker.MakeJob(JobDefOf.AttackMelee, target);
                            job.playerForced = true;
                            pawn.jobs.TryTakeOrderedJob(job, JobTag.DraftedOrder);
                        };
                        __result.Add(FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption(label, action, MenuOptionPriority.AttackEnemy, null, target), pawn, target));
                    }
                }
            }
        }
    }
}