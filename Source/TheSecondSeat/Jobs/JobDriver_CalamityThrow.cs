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

        // 缓存 ModExtension 避免重复获取
        private DefModExtension_GrabJob cachedExtension;
        private DefModExtension_GrabJob GrabJobExtension
        {
            get
            {
                if (cachedExtension == null)
                {
                    cachedExtension = job?.def?.GetModExtension<DefModExtension_GrabJob>();
                }
                return cachedExtension;
            }
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            // ⭐ v2.5.0: 首先检查 extension 是否存在
            var ext = GrabJobExtension;
            if (ext == null)
            {
                Log.Error("[CalamityThrow] DefModExtension_GrabJob is null! Check JobDef configuration.");
                yield break;
            }
            
            this.FailOn(() =>
            {
                // 检查施法者是否还处于"持有"状态
                var extension = GrabJobExtension;
                if (extension == null) return true;
                
                string holdingHediffDefName = extension.holdingHediffDefName;
                if (string.IsNullOrEmpty(holdingHediffDefName)) return true;
                
                HediffDef hediffDef = DefDatabase<HediffDef>.GetNamed(holdingHediffDefName, false);
                if (hediffDef == null) return true;
                
                return !pawn.health.hediffSet.HasHediff(hediffDef);
            });
            
            // 直接执行投掷 - 施法者不需要移动，只是把受害者扔向目的地
            Toil throwToil = Toils_General.DoAtomic(delegate
            {
                var extension = GrabJobExtension;
                if (extension == null)
                {
                    Log.Error("[CalamityThrow] Extension is null in throwToil!");
                    return;
                }
                
                Pawn caster = pawn;
                IntVec3 targetCell = job.GetTarget(DestinationInd).Cell;

                // 找到"持有中"的 Hediff 并获取目标
                string holdingHediffDefName = extension.holdingHediffDefName;
                if (string.IsNullOrEmpty(holdingHediffDefName))
                {
                    Log.Error("[CalamityThrow] holdingHediffDefName is null or empty!");
                    return;
                }
                
                HediffDef holdingHediffDef = DefDatabase<HediffDef>.GetNamed(holdingHediffDefName, false);
                if (holdingHediffDef == null)
                {
                    Log.Error($"[CalamityThrow] HediffDef '{holdingHediffDefName}' not found!");
                    return;
                }
                
                Hediff hediff = caster.health.hediffSet.GetFirstHediffOfDef(holdingHediffDef);
                
                if (hediff is Hediff_CalamityHolding calamityHolding && calamityHolding.HeldTarget != null)
                {
                    // 使用 Hediff 中存储的 HeldTarget，而不是 Job 的 TargetB
                    // 这样即使 Job 参数有问题，也能正确获取被持有的目标
                    Pawn victim = calamityHolding.HeldTarget;

                    // 参考 GodHandMod 实现：使用工厂方法创建 PawnFlyer
                    ThingDef flyerDef = DefDatabase<ThingDef>.GetNamed("TSS_PawnFlyer_Calamity", false);
                    if (flyerDef == null)
                    {
                        Log.Error("[CalamityThrow] TSS_PawnFlyer_Calamity ThingDef not found!");
                        return;
                    }
                    
                    // 确保 victim 被正确放置在地图上以便 MakeFlyer 工作
                    if (!victim.Spawned && caster.Map != null)
                    {
                        GenSpawn.Spawn(victim, caster.Position, caster.Map);
                    }

                    try
                    {
                        PawnFlyer_CalamityThrow flyer = PawnFlyer_CalamityThrow.MakeCalamityFlyer(
                            flyerDef,
                            victim,
                            targetCell,
                            caster,
                            null,
                            null
                        );
                        
                        if (flyer != null)
                        {
                            GenSpawn.Spawn(flyer, caster.Position, caster.Map, WipeMode.Vanish);
                        }
                        else
                        {
                            Log.Warning("[CalamityThrow] MakeCalamityFlyer returned null");
                        }
                    }
                    catch (System.Exception ex)
                    {
                        Log.Error($"[CalamityThrow] Exception creating flyer: {ex}");
                    }
                    
                    // 移除相关 hediffs
                    caster.health.RemoveHediff(hediff);
                    
                    HediffDef grabbedHediffDef = extension.GrabbedHediff;
                    if (grabbedHediffDef != null)
                    {
                        Hediff grabbedHediff = victim.health.hediffSet.GetFirstHediffOfDef(grabbedHediffDef);
                        if (grabbedHediff != null)
                        {
                            victim.health.RemoveHediff(grabbedHediff);
                        }
                    }
                }
                else
                {
                    Log.Warning("[CalamityThrow] No valid HeldTarget found in CalamityHolding hediff");
                }
            });

            yield return throwToil;
        }
    }
}
