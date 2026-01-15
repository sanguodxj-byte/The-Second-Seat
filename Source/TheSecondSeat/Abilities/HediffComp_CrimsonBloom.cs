using System;
using RimWorld;
using UnityEngine;
using Verse;

namespace TheSecondSeat
{
    /// <summary>
    /// 猩红绽放标记 HediffComp - 跟踪层数并在达到阈值时触发绽放
    /// </summary>
    public class HediffComp_CrimsonBloom : HediffComp
    {
        private Pawn applierPawn;
        private int currentStacks = 1;
        
        public HediffCompProperties_CrimsonBloom Props => 
            (HediffCompProperties_CrimsonBloom)props;

        public int CurrentStacks => currentStacks;
        
        public Pawn ApplierPawn => applierPawn;

        /// <summary>
        /// 增加层数
        /// </summary>
        public void AddStack(Pawn applier)
        {
            applierPawn = applier;
            currentStacks++;
            
            // 更新 Hediff 严重度来反映层数
            parent.Severity = currentStacks / 3f;
            
            if (currentStacks >= 3)
            {
                // 达到三层，触发绽放
                CompAbilityEffect_CrimsonBloom.CheckBloom(Pawn, applierPawn, currentStacks);
            }
            else
            {
                // 显示层数
                MoteMaker.ThrowText(Pawn.DrawPos, Pawn.Map,
                    "TSS_CrimsonBloom_MarkLabel".Translate(currentStacks), Color.red);
            }
        }

        public override void CompPostMake()
        {
            base.CompPostMake();
            currentStacks = 1;
        }

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look(ref currentStacks, "currentStacks", 1);
            Scribe_References.Look(ref applierPawn, "applierPawn");
        }

        public override string CompLabelPrefix
        {
            get
            {
                return $"x{currentStacks}";
            }
        }

        public override string CompTipStringExtra
        {
            get
            {
                return "TSS_CrimsonBloom_TipMain".Translate(currentStacks) + "\n" +
                       "TSS_CrimsonBloom_TipWarning".Translate();
            }
        }
    }

    /// <summary>
    /// 猩红绽放 HediffComp 属性
    /// </summary>
    public class HediffCompProperties_CrimsonBloom : HediffCompProperties
    {
        public HediffCompProperties_CrimsonBloom()
        {
            compClass = typeof(HediffComp_CrimsonBloom);
        }
    }
}