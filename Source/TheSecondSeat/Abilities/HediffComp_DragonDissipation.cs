using System;
using RimWorld;
using UnityEngine;
using Verse;

namespace TheSecondSeat
{
    /// <summary>
    /// 龙消散 HediffComp - 在持续时间结束后让召唤的龙消失
    /// </summary>
    public class HediffComp_DragonDissipation : HediffComp
    {
        private int ticksRemaining;
        private bool initialized = false;
        
        public HediffCompProperties_DragonDissipation Props => 
            (HediffCompProperties_DragonDissipation)props;

        public int TicksRemaining => ticksRemaining;

        public override void CompPostMake()
        {
            base.CompPostMake();
            if (!initialized)
            {
                ticksRemaining = Props.dissipationTicks;
                initialized = true;
            }
        }

        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);
            
            // 安全检查：龙死亡或销毁后停止执行
            if (Pawn == null || Pawn.Dead || Pawn.Destroyed)
            {
                return;
            }
            
            ticksRemaining--;
            
            // 更新严重度来反映剩余时间
            if (Props.dissipationTicks > 0)
            {
                parent.Severity = (float)ticksRemaining / Props.dissipationTicks;
            }
            
            if (ticksRemaining <= 0)
            {
                // 时间到，让龙消散
                DissipateTheDragon();
            }
        }

        private void DissipateTheDragon()
        {
            if (Pawn == null || Pawn.Map == null)
                return;
                
            // 创建消散效果
            FleckMaker.Static(Pawn.Position, Pawn.Map, FleckDefOf.PsycastAreaEffect, 3f);
            
            // 显示消息
            Messages.Message(
                "TSS_DragonDissipation_Start".Translate(Pawn.LabelCap),
                MessageTypeDefOf.NeutralEvent,
                historical: false);
            
            // 移除龙
            if (!Pawn.Destroyed)
            {
                Pawn.Destroy(DestroyMode.Vanish);
            }
        }

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look(ref ticksRemaining, "ticksRemaining", 0);
            Scribe_Values.Look(ref initialized, "initialized", false);
        }

        public override string CompTipStringExtra
        {
            get
            {
                // 安全检查：龙死亡后不显示提示
                if (Pawn == null || Pawn.Dead || Pawn.Destroyed)
                {
                    return null;
                }
                
                int ticks = ticksRemaining;
                int seconds = ticks / 60;
                return "TSS_DragonDissipation_Timer".Translate(seconds);
            }
        }
    }

    /// <summary>
    /// 龙消散 HediffComp 属性
    /// </summary>
    public class HediffCompProperties_DragonDissipation : HediffCompProperties
    {
        /// <summary>
        /// 消散前的持续时间（Ticks）
        /// </summary>
        public int dissipationTicks = 15000;
        
        public HediffCompProperties_DragonDissipation()
        {
            compClass = typeof(HediffComp_DragonDissipation);
        }
    }
}