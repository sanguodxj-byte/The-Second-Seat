using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace TheSecondSeat
{
    public class JobDriver_CalamityThrow : JobDriver
    {
        private const TargetIndex DestinationInd = TargetIndex.A;
        private const TargetIndex VictimInd = TargetIndex.B;

        protected Pawn Victim => (Pawn)job.GetTarget(VictimInd).Thing;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.GetTarget(DestinationInd), job, 1, -1, null, errorOnFailed) && pawn.Reserve(Victim, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() =>
            {
                // 检查施法者是否还处于"持有"状态
                var holdingHediffDef = job.def.GetModExtension<DefModExtension_GrabJob>()?.holdingHediffDefName;
                if (string.IsNullOrEmpty(holdingHediffDef) || !pawn.health.hediffSet.HasHediff(DefDatabase<HediffDef>.GetNamed(holdingHediffDef)))
                {
                    return true;
                }
                return false;
            });
            
            // 直接执行投掷 - 施法者不需要移动，只是把受害者扔向目的地
            Toil throwToil = Toils_General.DoAtomic(delegate
            {
                Pawn caster = pawn;
                IntVec3 targetCell = job.GetTarget(DestinationInd).Cell;

                // 找到"持有中"的 Hediff 并获取目标
                Hediff hediff = caster.health.hediffSet.GetFirstHediffOfDef(
                    DefDatabase<HediffDef>.GetNamed(
                        job.def.GetModExtension<DefModExtension_GrabJob>().holdingHediffDefName));
                
                if (hediff is Hediff_CalamityHolding calamityHolding && calamityHolding.HeldTarget != null)
                {
                    // 使用 Hediff 中存储的 HeldTarget，而不是 Job 的 TargetB
                    // 这样即使 Job 参数有问题，也能正确获取被持有的目标
                    Pawn victim = calamityHolding.HeldTarget;

                    // 参考 GodHandMod 实现：手动创建 PawnFlyer，支持直接从持有状态投掷（无需先生成 Pawn，避免闪烁）
                    ThingDef flyerDef = DefDatabase<ThingDef>.GetNamed("TSS_PawnFlyer_Calamity");
                    PawnFlyer_CalamityThrow flyer = (PawnFlyer_CalamityThrow)ThingMaker.MakeThing(flyerDef);
                    
                    if (flyer != null)
                    {
                        flyer.SetLauncher(caster);
                        
                        // 初始化飞行参数
                        // 如果 victim 未生成（被持有），使用 caster.DrawPos 作为起点
                        Vector3 startPos = victim.Spawned ? victim.DrawPos : caster.DrawPos;
                        float distance = (targetCell.ToVector3Shifted() - startPos).magnitude;
                        flyer.InitializeFlightParams(startPos, targetCell, distance);
                        
                        // 将受害者转移到飞行器中
                        if (victim.Spawned)
                        {
                            victim.DeSpawn();
                        }
                        
                        // 如果在 Hediff 中，可能需要先移除 Hediff 释放 pawn?
                        // 不，直接 TryAdd 会尝试从当前持有者（Hediff/Caster）转移
                        if (flyer.GetDirectlyHeldThings().TryAdd(victim, true))
                        {
                            GenSpawn.Spawn(flyer, caster.Position, caster.Map, WipeMode.Vanish);
                        }
                        else
                        {
                            Log.Error("[TheSecondSeat] Failed to add victim to flyer container");
                            // Fallback: spawn victim normally
                            GenSpawn.Spawn(victim, targetCell, caster.Map);
                        }
                    }
                    
                    // 移除相关 hediffs
                    caster.health.RemoveHediff(hediff);
                    
                    var grabbedHediffDef = job.def.GetModExtension<DefModExtension_GrabJob>().GrabbedHediff;
                    if (grabbedHediffDef != null)
                    {
                        Hediff grabbedHediff = victim.health.hediffSet.GetFirstHediffOfDef(grabbedHediffDef);
                        if (grabbedHediff != null)
                        {
                            victim.health.RemoveHediff(grabbedHediff);
                        }
                    }
                }
            });

            yield return throwToil;
        }
    }
}
