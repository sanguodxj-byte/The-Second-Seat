using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace TheSecondSeat
{
    /// <summary>
    /// 灾厄摔掷技能属性 - 强化版 Tactical Throws
    /// 所有 Def 引用和数值通过 XML 配置，避免硬编码
    /// </summary>
    public class CompProperties_AbilityEffect_CalamityThrow : CompProperties_AbilityEffect
    {
        // === 数值配置（通过 XML 可调整）===
        
        /// <summary>伤害倍率</summary>
        public float damageMultiplier = 2.0f;
        
        /// <summary>跳过原版抓取判定</summary>
        public bool bypassGrappleCheck = true;
        
        /// <summary>最大可抓取体型（0 = 无限制）</summary>
        public float maxTargetBodySize = 3.5f;
        
        // === 通过 defName 配置的 Def 引用 ===
        
        /// <summary>持有 Job 的 defName</summary>
        public string holdJobDefName;
        
        /// <summary>伤害倍率 Hediff 的 defName</summary>
        public string damageMultiplierHediffDefName;

        // === 缓存的 Def 引用 ===
        private JobDef cachedHoldJobDef;
        private HediffDef cachedDamageMultiplierHediffDef;

        public JobDef HoldJobDef
        {
            get
            {
                if (cachedHoldJobDef == null && !string.IsNullOrEmpty(holdJobDefName))
                {
                    cachedHoldJobDef = DefDatabase<JobDef>.GetNamed(holdJobDefName, false);
                    if (cachedHoldJobDef == null)
                    {
                        Log.Error($"[CalamityThrow] JobDef '{holdJobDefName}' not found!");
                    }
                }
                return cachedHoldJobDef;
            }
        }

        public HediffDef DamageMultiplierHediffDef
        {
            get
            {
                if (cachedDamageMultiplierHediffDef == null && !string.IsNullOrEmpty(damageMultiplierHediffDefName))
                {
                    cachedDamageMultiplierHediffDef = DefDatabase<HediffDef>.GetNamed(damageMultiplierHediffDefName, false);
                    if (cachedDamageMultiplierHediffDef == null)
                    {
                        Log.Error($"[CalamityThrow] HediffDef '{damageMultiplierHediffDefName}' not found!");
                    }
                }
                return cachedDamageMultiplierHediffDef;
            }
        }

        public CompProperties_AbilityEffect_CalamityThrow()
        {
            compClass = typeof(CompAbilityEffect_CalamityThrow);
        }
    }

    /// <summary>
    /// 灾厄摔掷效果组件 - 直接抓取目标，无需判定
    /// 所有数值从 CompProperties 读取，支持 XML 配置
    /// </summary>
    public class CompAbilityEffect_CalamityThrow : CompAbilityEffect
    {
        public new CompProperties_AbilityEffect_CalamityThrow Props => 
            (CompProperties_AbilityEffect_CalamityThrow)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            Pawn caster = parent.pawn;
            Pawn targetPawn = target.Pawn;

            if (targetPawn == null || !targetPawn.Spawned || targetPawn.Dead)
            {
                Messages.Message("TSS_CalamityThrow_InvalidTarget".Translate(), MessageTypeDefOf.RejectInput, false);
                return;
            }

            // 检查目标体型（从 Props 获取限制值）
            if (Props.maxTargetBodySize > 0 && targetPawn.BodySize > Props.maxTargetBodySize)
            {
                Messages.Message("TSS_CalamityThrow_TargetTooLarge".Translate(Props.maxTargetBodySize), 
                    MessageTypeDefOf.RejectInput, false);
                return;
            }

            // 直接进入持有状态，跳过原版 Tactical Throws 的成功率判定
            StartCalamityHold(caster, targetPawn);
        }

        private void StartCalamityHold(Pawn caster, Pawn target)
        {
            // 从 CompProperties 获取配置的 JobDef
            JobDef holdJobDef = Props.HoldJobDef;
            if (holdJobDef == null)
            {
                Log.Error("[CalamityThrow] HoldJobDef not configured in CompProperties!");
                return;
            }

            Job holdJob = JobMaker.MakeJob(holdJobDef, target);
            holdJob.count = 1;
            
            // 存储伤害倍率到 Job 中供后续使用
            // 从 CompProperties 获取配置的 HediffDef
            HediffDef damageMultiplierHediffDef = Props.DamageMultiplierHediffDef;
            if (damageMultiplierHediffDef != null)
            {
                Hediff damageMultiplierHediff = HediffMaker.MakeHediff(damageMultiplierHediffDef, caster);
                damageMultiplierHediff.Severity = Props.damageMultiplier;
                caster.health.AddHediff(damageMultiplierHediff);
            }

            caster.jobs.StartJob(holdJob, JobCondition.InterruptForced);
        }

        public override bool CanApplyOn(LocalTargetInfo target, LocalTargetInfo dest)
        {
            if (!base.CanApplyOn(target, dest))
                return false;

            Pawn targetPawn = target.Pawn;
            if (targetPawn == null || !targetPawn.Spawned || targetPawn.Dead || targetPawn.Downed)
                return false;

            // 使用 Props 中的体型限制
            if (Props.maxTargetBodySize > 0 && targetPawn.BodySize > Props.maxTargetBodySize)
                return false;

            return true;
        }

        public override string ExtraLabelMouseAttachment(LocalTargetInfo target)
        {
            Pawn targetPawn = target.Pawn;
            if (targetPawn == null)
                return null;

            if (Props.maxTargetBodySize > 0 && targetPawn.BodySize > Props.maxTargetBodySize)
                return "TSS_CalamityThrow_TargetTooLarge".Translate(Props.maxTargetBodySize);

            return "TSS_CalamityThrow_GrabAction".Translate(Props.damageMultiplier);
        }
    }
}
