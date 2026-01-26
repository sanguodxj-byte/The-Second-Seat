using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace TheSecondSeat
{
    /// <summary>
    /// 猩红绽放技能属性
    /// 所有 Def 引用和数值通过 XML 配置，避免硬编码
    /// </summary>
    public class CompProperties_AbilityCrimsonBloom : CompProperties_AbilityEffect
    {
        // === 数值配置 ===
        
        /// <summary>触发绽放所需层数</summary>
        public int stacksToBloom = 3;
        
        /// <summary>标记持续时间（秒）</summary>
        public float markDuration = 30f;
        
        /// <summary>后备伤害（无有效部件时使用）</summary>
        public float fallbackDamage = 200f;
        
        /// <summary>初始标记严重度</summary>
        public float initialSeverity = 1f;
        
        // === 通过 defName 配置的 Def 引用 ===
        
        /// <summary>猩红标记 HediffDef 的 defName</summary>
        public string markHediffDefName;
        
        /// <summary>优先查找的身体部位 defName 列表（按优先级排序）</summary>
        public List<string> priorityBodyPartDefNames = new List<string> { "Heart", "Brain", "Neck", "Torso" };
        
        /// <summary>优先查找的 BodyPartTag defName 列表（用于异种族兼容）</summary>
        public List<string> priorityBodyPartTagDefNames = new List<string> { "BloodPumpingSource", "ConsciousnessSource", "BreathingSource" };

        // === 缓存的 Def 引用 ===
        private HediffDef cachedMarkHediffDef;
        private List<BodyPartTagDef> cachedBodyPartTagDefs;

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

        public List<BodyPartTagDef> PriorityBodyPartTagDefs
        {
            get
            {
                if (cachedBodyPartTagDefs == null)
                {
                    cachedBodyPartTagDefs = new List<BodyPartTagDef>();
                    foreach (string tagName in priorityBodyPartTagDefNames)
                    {
                        BodyPartTagDef tag = DefDatabase<BodyPartTagDef>.GetNamed(tagName, false);
                        if (tag != null)
                        {
                            cachedBodyPartTagDefs.Add(tag);
                        }
                    }
                }
                return cachedBodyPartTagDefs;
            }
        }

        public CompProperties_AbilityCrimsonBloom()
        {
            compClass = typeof(CompAbilityEffect_CrimsonBloom);
        }
    }

    /// <summary>
    /// 猩红绽放技能效果组件
    /// 每次攻击时积累层数，达到层数后直接摧毁对方核心身体部件
    /// 使用 BodyPartTagDef 查找身体部位，兼容异种族 Mod
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
                    CheckBloom(target, caster, (int)existingMark.Severity, Props.stacksToBloom, Props.fallbackDamage, Props);
                }
            }
            else
            {
                // 添加新标记
                Hediff newMark = HediffMaker.MakeHediff(markDef, target);
                newMark.Severity = Props.initialSeverity;
                target.health.AddHediff(newMark);

                // 视觉效果
                FleckMaker.Static(target.DrawPos, target.Map, FleckDefOf.PsycastAreaEffect, 0.5f);
                MoteMaker.ThrowText(target.DrawPos, target.Map, "TSS_CrimsonBloom_MarkLabel".Translate(1), Color.red);
            }
        }

        /// <summary>
        /// 检查是否达到绽放条件
        /// 重载1：使用 CompProperties（从 Ability 调用时使用）
        /// </summary>
        public static void CheckBloom(Pawn target, Pawn caster, int stacks, CompProperties_AbilityCrimsonBloom props)
        {
            int requiredStacks = props?.stacksToBloom ?? 3;
            float fallbackDamage = props?.fallbackDamage ?? 200f;
            
            CheckBloom(target, caster, stacks, requiredStacks, fallbackDamage, props);
        }

        /// <summary>
        /// 检查是否达到绽放条件
        /// 重载2：直接传递参数（从 HediffComp 调用时使用）
        /// </summary>
        public static void CheckBloom(Pawn target, Pawn caster, int stacks, int requiredStacks, float fallbackDamage, CompProperties_AbilityCrimsonBloom props)
        {
            if (stacks >= requiredStacks)
            {
                // 达到层数，摧毁核心部件！
                ExecuteCrimsonBloom(target, caster, fallbackDamage, props);
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
        private static void ExecuteCrimsonBloom(Pawn target, Pawn caster, float fallbackDamage, CompProperties_AbilityCrimsonBloom propsForTags)
        {
            if (target == null || target.Dead || target.health == null)
            {
                return;
            }

            Map map = target.Map;
            Vector3 pos = target.DrawPos;

            // 猩红绽放特效
            FleckMaker.Static(pos, map, FleckDefOf.PsycastAreaEffect, 3f);
            
            // 尝试摧毁核心部件（使用 Tag 查找，兼容异种族）
            BodyPartRecord corePartToDestroy = FindCoreBodyPart(target, propsForTags);

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
                // 没有可摧毁的核心部件，造成后备伤害
                float finalFallbackDamage = fallbackDamage > 0 ? fallbackDamage : 200f;
                
                DamageInfo fallbackDamageInfo = new DamageInfo(
                    DamageDefOf.ExecutionCut,
                    finalFallbackDamage,
                    armorPenetration: 1f,
                    instigator: caster);

                target.TakeDamage(fallbackDamageInfo);
                
                MoteMaker.ThrowText(pos, map, "TSS_CrimsonBloom_Detonate".Translate(), Color.magenta);
            }

            // 移除标记
            RemoveCrimsonBloomMark(target);
        }

        /// <summary>
        /// 移除猩红绽放标记
        /// </summary>
        private static void RemoveCrimsonBloomMark(Pawn target)
        {
            if (target.health?.hediffSet == null) return;
            
            // 查找所有带有 HediffComp_CrimsonBloom 的 Hediff
            Hediff mark = target.health.hediffSet.hediffs.FirstOrDefault(h =>
                h.TryGetComp<HediffComp_CrimsonBloom>() != null);
            
            if (mark != null)
            {
                target.health.RemoveHediff(mark);
            }
        }

        /// <summary>
        /// 找到可摧毁的核心身体部件
        /// 优先使用 BodyPartTag 查找（兼容异种族），回退到 defName 查找
        /// </summary>
        private static BodyPartRecord FindCoreBodyPart(Pawn pawn, CompProperties_AbilityCrimsonBloom props)
        {
            if (pawn?.health?.hediffSet == null)
            {
                return null;
            }

            var notMissingParts = pawn.health.hediffSet.GetNotMissingParts().ToList();
            if (notMissingParts.Count == 0) return null;

            // === 第一优先级：使用 BodyPartTag 查找（兼容异种族）===
            if (props?.PriorityBodyPartTagDefs != null)
            {
                foreach (BodyPartTagDef tag in props.PriorityBodyPartTagDefs)
                {
                    BodyPartRecord part = notMissingParts.FirstOrDefault(p => 
                        p.def.tags != null && p.def.tags.Contains(tag));
                    
                    if (part != null)
                    {
                        return part;
                    }
                }
            }

            // === 第二优先级：使用 defName 查找（原版生物）===
            if (props?.priorityBodyPartDefNames != null)
            {
                foreach (string partDefName in props.priorityBodyPartDefNames)
                {
                    BodyPartRecord part = notMissingParts.FirstOrDefault(p => 
                        p.def.defName == partDefName);

                    if (part != null)
                    {
                        return part;
                    }
                }
            }

            // === 第三优先级：使用默认的原版 Tag（最终回退）===
            BodyPartRecord vitalPart = notMissingParts.FirstOrDefault(p => 
                p.def.tags?.Contains(BodyPartTagDefOf.BloodPumpingSource) == true);

            if (vitalPart != null) return vitalPart;

            vitalPart = notMissingParts.FirstOrDefault(p => 
                p.def.tags?.Contains(BodyPartTagDefOf.ConsciousnessSource) == true);

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
}
