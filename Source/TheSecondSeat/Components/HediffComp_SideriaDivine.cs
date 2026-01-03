using System.Collections.Generic;
using System.Linq;
using Verse;
using RimWorld;

namespace TheSecondSeat.Components
{
    /// <summary>
    /// Sideria 神性躯体组件
    /// 1. 每 5 秒 (300 ticks) 清除有害 Hediff（除了自带的和部件损伤）
    /// 2. 免疫特定类型的伤害（通过 Harmony Patch 实现更彻底，这里做辅助检查）
    /// </summary>
    public class HediffComp_SideriaDivine : HediffComp
    {
        private int tickCounter = 0;
        private const int CHECK_INTERVAL = 300; // 5秒

        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);
            
            tickCounter++;
            if (tickCounter >= CHECK_INTERVAL)
            {
                tickCounter = 0;
                CleanseStatus();
            }
        }

        /// <summary>
        /// 清除有害状态
        /// </summary>
        private void CleanseStatus()
        {
            Pawn pawn = this.Pawn;
            if (pawn == null || pawn.Dead) return;

            // 收集需要移除的 Hediff
            List<Hediff> toRemove = new List<Hediff>();
            
            foreach (Hediff hediff in pawn.health.hediffSet.hediffs)
            {
                // 跳过自己
                if (hediff.def == this.Def) continue;
                
                // 跳过部件缺失/损伤 (Injury)
                if (hediff is Hediff_Injury || hediff is Hediff_MissingPart) continue;
                
                // 跳过 Sideria 专属状态 (防止移除 BloodBloom 等)
                if (hediff.def.defName.Contains("Sideria")) continue;

                // 移除所有其他不良状态 (疾病、中毒、体温过低/过高、成瘾、精神崩溃前兆等)
                // 只要是 bad 的 hediff，且不是伤口，都移除
                if (hediff.def.isBad)
                {
                    toRemove.Add(hediff);
                }
            }

            foreach (Hediff h in toRemove)
            {
                pawn.health.RemoveHediff(h);
            }
        }
    }
    
    public class HediffCompProperties_SideriaDivine : HediffCompProperties
    {
        public HediffCompProperties_SideriaDivine()
        {
            this.compClass = typeof(HediffComp_SideriaDivine);
        }
    }
}