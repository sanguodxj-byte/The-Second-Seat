using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace TheSecondSeat
{
    /// <summary>
    /// 猩红绽放技能效果组件
    /// 每次攻击时积累层数，三层后直接摧毁对方核心身体部件
    /// </summary>
    public class CompAbilityEffect_CrimsonBloom : CompAbilityEffect
    {
        public new CompProperties_AbilityCrimsonBloom Props => (CompProperties_AbilityCrimsonBloom)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            Pawn caster = parent.pawn;
            Pawn targetPawn = target.Pawn;

            if (targetPawn == null || targetPawn.Dead)
            {
                return;
            }

            // 应用或增加猩红绽放标记
            ApplyOrStackMark(targetPawn, caster);
        }

        /// <summary>
        /// 应用或堆叠猩红绽放标记
        /// </summary>
        private void ApplyOrStackMark(Pawn target, Pawn caster)
        {
            // 从 CompProperties 获取配置的 HediffDef
            HediffDef markDef = Props.MarkHediffDef;
            if (markDef == null)
            {
                Log.Error("[CrimsonBloom] MarkHediffDef not configured in CompProperties!");
                return;
            }

            // 检查是否已有标记
            Hediff existingMark = target.health?.hediffSet?.GetFirstHediffOfDef(markDef);

            if (existingMark != null)
            {
                // 增加层数
                HediffComp_CrimsonBloom comp = existingMark.TryGetComp<HediffComp_CrimsonBloom>();
                if (comp != null)
                {
                    comp.AddStack(caster);
                }
                else
                {
                    // 如果没有 comp，直接增加严重度
                    existingMark.Severity += 1f;
                    CheckBloom(target, caster, (int)existingMark.Severity);
                }
            }
            else
            {
                // 添加新标记
                Hediff newMark = HediffMaker.MakeHediff(markDef, target);
                newMark.Severity = 1f;
                target.health.AddHediff(newMark);

                // 视觉效果
                FleckMaker.Static(target.DrawPos, target.Map, FleckDefOf.PsycastAreaEffect, 0.5f);
                MoteMaker.ThrowText(target.DrawPos, target.Map, "TSS_CrimsonBloom_MarkLabel".Translate(1), Color.red);
            }
        }

        /// <summary>
        /// 检查是否达到绽放条件
        /// </summary>
        public static void CheckBloom(Pawn target, Pawn caster, int stacks)
        {
            if (stacks >= 3)
            {
                // 达到三层，摧毁核心部件！
                ExecuteCrimsonBloom(target, caster);
            }
            else
            {
                // 显示当前层数
                MoteMaker.ThrowText(target.DrawPos, target.Map,
                    "TSS_CrimsonBloom_MarkLabel".Translate(stacks), Color.red);
            }
        }

        /// <summary>
        /// 执行猩红绽放 - 摧毁核心身体部件
        /// </summary>
        private static void ExecuteCrimsonBloom(Pawn target, Pawn caster)
        {
            if (target == null || target.Dead || target.health == null)
            {
                return;
            }

            Map map = target.Map;
            Vector3 pos = target.DrawPos;

            // 猩红绽放特效
            FleckMaker.Static(pos, map, FleckDefOf.PsycastAreaEffect, 3f);
            
            // 尝试摧毁核心部件
            BodyPartRecord corePartToDestroy = FindCoreBodyPart(target);

            if (corePartToDestroy != null)
            {
                // 造成足以摧毁该部件的伤害
                DamageInfo destroyDamage = new DamageInfo(
                    DamageDefOf.ExecutionCut,
                    9999f,
                    armorPenetration: 999f,
                    angle: -1f,
                    instigator: caster,
                    hitPart: corePartToDestroy,
                    weapon: null,
                    category: DamageInfo.SourceCategory.ThingOrUnknown,
                    intendedTarget: target);

                target.TakeDamage(destroyDamage);

                // 显示效果
                MoteMaker.ThrowText(pos, map,
                    "TSS_CrimsonBloom_OrganDestroyed".Translate(target.LabelShort, corePartToDestroy.Label),
                    Color.magenta);

                Log.Message($"[CrimsonBloom] {caster.LabelShort} destroyed {target.LabelShort}'s {corePartToDestroy.Label}!");
            }
            else
            {
                // 没有可摧毁的核心部件，造成大量伤害
                DamageInfo fallbackDamage = new DamageInfo(
                    DamageDefOf.ExecutionCut,
                    200f,
                    armorPenetration: 1f,
                    instigator: caster);

                target.TakeDamage(fallbackDamage);
                
                MoteMaker.ThrowText(pos, map, "TSS_CrimsonBloom_Detonate".Translate(), Color.magenta);
            }

            // 移除标记 - 使用传入的 caster 获取 CompProperties
            // 注意：这是静态方法，需要额外获取 markDef
            // 通过目标已有的标记来查找并移除
            if (target.health?.hediffSet != null)
            {
                // 查找所有可能是猩红标记的 Hediff（通过 HediffComp_CrimsonBloom）
                Hediff mark = target.health.hediffSet.hediffs.FirstOrDefault(h =>
                    h.TryGetComp<HediffComp_CrimsonBloom>() != null);
                if (mark != null)
                {
                    target.health.RemoveHediff(mark);
                }
            }
        }

        /// <summary>
        /// 找到可摧毁的核心身体部件
        /// 优先级：心脏 > 大脑 > 颈部 > 躯干
        /// </summary>
        private static BodyPartRecord FindCoreBodyPart(Pawn pawn)
        {
            if (pawn?.health?.hediffSet == null)
            {
                return null;
            }

            var body = pawn.RaceProps?.body;
            if (body == null)
            {
                return null;
            }

            // 按优先级查找核心部件
            List<string> coreParts = new List<string>
            {
                "Heart",
                "Brain", 
                "Neck",
                "Torso"
            };

            foreach (string partDefName in coreParts)
            {
                BodyPartRecord part = pawn.health.hediffSet.GetNotMissingParts()
                    .FirstOrDefault(p => p.def.defName == partDefName);

                if (part != null)
                {
                    return part;
                }
            }

            // 如果找不到标准部件，尝试找任何致命部件
            BodyPartRecord vitalPart = pawn.health.hediffSet.GetNotMissingParts()
                .FirstOrDefault(p => p.def.tags?.Contains(BodyPartTagDefOf.BloodPumpingSource) == true);

            if (vitalPart != null)
            {
                return vitalPart;
            }

            vitalPart = pawn.health.hediffSet.GetNotMissingParts()
                .FirstOrDefault(p => p.def.tags?.Contains(BodyPartTagDefOf.ConsciousnessSource) == true);

            return vitalPart;
        }

        public override bool CanApplyOn(LocalTargetInfo target, LocalTargetInfo dest)
        {
            if (!base.CanApplyOn(target, dest))
            {
                return false;
            }

            Pawn targetPawn = target.Pawn;
            if (targetPawn == null || targetPawn.Dead)
            {
                return false;
            }

            // 不能对友方使用
            if (targetPawn.Faction == parent.pawn.Faction)
            {
                return false;
            }

            return true;
        }

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            if (!base.Valid(target, throwMessages))
            {
                return false;
            }

            Pawn targetPawn = target.Pawn;
            if (targetPawn == null)
            {
                if (throwMessages)
                {
                    Messages.Message("TSS_CrimsonBloom_NoTargets".Translate(), MessageTypeDefOf.RejectInput, false);
                }
                return false;
            }

            if (targetPawn.Dead)
            {
                if (throwMessages)
                {
                    Messages.Message("TSS_CrimsonBloom_TargetDead".Translate(), MessageTypeDefOf.RejectInput, false);
                }
                return false;
            }

            return true;
        }
    }

    /// <summary>
    /// 猩红绽放技能属性
    /// 所有 Def 引用通过 defName 配置，子模组通过 XML 指定
    /// </summary>
    public class CompProperties_AbilityCrimsonBloom : CompProperties_AbilityEffect
    {
        public int stacksToBloom = 3;
        public float markDuration = 30f; // 标记持续时间（秒）
        
        // 通过 defName 配置的 Def 引用
        public string markHediffDefName; // 猩红标记 HediffDef 的 defName

        // 缓存的 Def 引用
        private HediffDef cachedMarkHediffDef;

        public HediffDef MarkHediffDef
        {
            get
            {
                if (cachedMarkHediffDef == null && !string.IsNullOrEmpty(markHediffDefName))
                {
                    cachedMarkHediffDef = DefDatabase<HediffDef>.GetNamed(markHediffDefName, false);
                    if (cachedMarkHediffDef == null)
                    {
                        Log.Error($"[CrimsonBloom] HediffDef '{markHediffDefName}' not found!");
                    }
                }
                return cachedMarkHediffDef;
            }
        }

        public CompProperties_AbilityCrimsonBloom()
        {
            compClass = typeof(CompAbilityEffect_CrimsonBloom);
        }
    }
}
