using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace TheSecondSeat
{
    // =============================================================
    // 血棘龙技能：猩红狂怒
    // =============================================================
    public class HediffCompProperties_BloodThornRage : HediffCompProperties
    {
        public int maxStacks = 10;
        public float cooldownReductionPerStack = 0.05f;
        public int stackDuration = 600;

        public HediffCompProperties_BloodThornRage()
        {
            compClass = typeof(HediffComp_BloodThornRage);
        }
    }

    public class HediffComp_BloodThornRage : HediffComp
    {
        public HediffCompProperties_BloodThornRage Props => (HediffCompProperties_BloodThornRage)props;

        public override void Notify_PawnUsedVerb(Verb verb, LocalTargetInfo target)
        {
            base.Notify_PawnUsedVerb(verb, target);
            
            if (!verb.IsMeleeAttack || Pawn.Map == null) return;

            // 应用或刷新狂怒层数
            HediffDef rageStackDef = DefDatabase<HediffDef>.GetNamed("Sideria_BloodThorn_RageStack", false);
            if (rageStackDef == null) return;

            Hediff rageHediff = Pawn.health.hediffSet.GetFirstHediffOfDef(rageStackDef);
            if (rageHediff == null)
            {
                rageHediff = Pawn.health.AddHediff(rageStackDef);
                rageHediff.Severity = 1.0f;
            }
            else
            {
                if (rageHediff.Severity < Props.maxStacks)
                {
                    rageHediff.Severity += 1.0f;
                }
            }
            
            // 播放特效
            FleckMaker.ThrowDustPuff(Pawn.Position, Pawn.Map, 1.0f);
        }
    }

    // =============================================================
    // 煌耀龙技能：神圣重生
    // =============================================================
    public class HediffCompProperties_RadiantRebirth : HediffCompProperties
    {
        public int cooldownTicks = 60000; // 1天冷却
        public float triggerHealthPct = 0.3f; // 30% 血量触发
        public float healPct = 0.5f; // 重生时恢复 50% 血量
        public int vanishDuration = 120; // 2秒
        public float explosionRadius = 5.9f;
        public int explosionDamage = 50;

        public HediffCompProperties_RadiantRebirth()
        {
            compClass = typeof(HediffComp_RadiantRebirth);
        }
    }

    public class HediffComp_RadiantRebirth : HediffComp
    {
        public HediffCompProperties_RadiantRebirth Props => (HediffCompProperties_RadiantRebirth)props;

        private int ticksUntilReady = 0;

        public override void CompPostPostAdd(DamageInfo? dinfo)
        {
            base.CompPostPostAdd(dinfo);
            ticksUntilReady = 0;
        }

        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);
            if (ticksUntilReady > 0)
            {
                ticksUntilReady--;
            }
        }

        public override void Notify_PawnPostApplyDamage(DamageInfo dinfo, float totalDamageDealt)
        {
            base.Notify_PawnPostApplyDamage(dinfo, totalDamageDealt);

            // 允许在倒地状态下触发，但不能在死亡状态下触发（除非添加复活逻辑）
            if (Pawn.Map == null || Pawn.Dead) return;

            // 检查冷却
            if (ticksUntilReady > 0) return;

            // 检查血量阈值
            if (Pawn.HealthScale > 0 && Pawn.health.summaryHealth.SummaryHealthPercent < Props.triggerHealthPct)
            {
                TriggerRebirth();
            }
        }

        private void TriggerRebirth()
        {
            if (Pawn.Map == null) return;

            Map map = Pawn.Map;
            IntVec3 pos = Pawn.Position;
            
            // 播放消失特效
            FleckMaker.Static(pos, map, FleckDefOf.PsycastSkipFlashEntry, 3f);
            
            // 生成隐藏点
            ThingDef hidingSpotDef = DefDatabase<ThingDef>.GetNamed("Sideria_DragonHidingSpot", false);
            if (hidingSpotDef != null)
            {
                DragonHidingSpot hidingSpot = (DragonHidingSpot)GenSpawn.Spawn(hidingSpotDef, pos, map);
                // 传递治疗参数
                hidingSpot.Setup(Pawn, Props.vanishDuration, Props.explosionRadius, Props.explosionDamage, Props.healPct);
            }
            
            // 设置冷却
            ticksUntilReady = Props.cooldownTicks;
            
            Messages.Message("TSS_RadiantRebirth_Triggered".Translate(Pawn.LabelShort), MessageTypeDefOf.PositiveEvent);
        }

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look(ref ticksUntilReady, "ticksUntilReady", 0);
        }
    }

    // =============================================================
    // 机铠龙技能：全弹发射
    // =============================================================
    public class HediffCompProperties_MechaOmniFire : HediffCompProperties
    {
        public IntRange fireIntervalRange = new IntRange(400, 800);
        public int burstCount = 16;
        public float range = 25.9f;
        public ThingDef projectileDef;

        public HediffCompProperties_MechaOmniFire()
        {
            compClass = typeof(HediffComp_MechaOmniFire);
        }
    }

    public class HediffComp_MechaOmniFire : HediffComp
    {
        public HediffCompProperties_MechaOmniFire Props => (HediffCompProperties_MechaOmniFire)props;

        private int ticksUntilFire;
        private int burstShotsLeft;
        private int burstCooldown;
        private Thing currentTarget;

        public override void CompPostPostAdd(DamageInfo? dinfo)
        {
            base.CompPostPostAdd(dinfo);
            ticksUntilFire = Props.fireIntervalRange.RandomInRange;
        }

        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);

            // 处理连射
            if (burstShotsLeft > 0)
            {
                if (burstCooldown > 0)
                {
                    burstCooldown--;
                }
                else
                {
                    FireSingleShot();
                    burstCooldown = 4; // 每4 ticks (约0.06秒) 发射一发
                }
                return; // 连射期间暂停主冷却
            }

            ticksUntilFire--;
            if (ticksUntilFire <= 0)
            {
                TryStartBurst();
                ticksUntilFire = Props.fireIntervalRange.RandomInRange;
            }
        }

        private void TryStartBurst()
        {
            if (Pawn.Map == null || Pawn.Downed || Pawn.Dead) return;

            // 寻找目标
            currentTarget = FindTarget();
            if (currentTarget == null) return;

            burstShotsLeft = Props.burstCount;
            burstCooldown = 0;
        }

        private void FireSingleShot()
        {
            if (Pawn.Map == null || Pawn.Downed || Pawn.Dead)
            {
                burstShotsLeft = 0;
                return;
            }

            // 如果目标丢失或死亡，尝试重新寻找，如果找不到则停止
            if (currentTarget == null || currentTarget.Destroyed || (currentTarget is Pawn p && p.Dead))
            {
                currentTarget = FindTarget();
                if (currentTarget == null)
                {
                    burstShotsLeft = 0;
                    return;
                }
            }

            LaunchProjectile(currentTarget);
            burstShotsLeft--;

            // 播放音效
            SoundDef sound = DefDatabase<SoundDef>.GetNamed("Shot_ChargeBlaster", false);
            if (sound != null)
            {
                SoundStarter.PlayOneShot(sound, Pawn);
            }
        }

        private Thing FindTarget()
        {
            // 优先攻击当前攻击目标
            if (Pawn.TargetCurrentlyAimingAt.HasThing)
            {
                return Pawn.TargetCurrentlyAimingAt.Thing;
            }
            
            // 否则寻找最近的敌对 Pawn
            return GenClosest.ClosestThingReachable(
                Pawn.Position,
                Pawn.Map,
                ThingRequest.ForGroup(ThingRequestGroup.Pawn),
                PathEndMode.Touch,
                TraverseParms.For(Pawn),
                Props.range,
                validator: (t) => t is Pawn p && p.HostileTo(Pawn) && !p.Downed && !p.Dead
            );
        }

        private void LaunchProjectile(Thing target)
        {
            IntVec3 sourceCell = Pawn.Position;
            IntVec3 targetCell = target.Position;
            
            Projectile projectile = (Projectile)GenSpawn.Spawn(Props.projectileDef, sourceCell, Pawn.Map);
            
            // 稍微分散一点落点 (增加散布范围)
            targetCell += new IntVec3(Rand.Range(-2, 3), 0, Rand.Range(-2, 3));
            
            projectile.Launch(Pawn, targetCell, targetCell, ProjectileHitFlags.All, false, null);
        }

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look(ref ticksUntilFire, "ticksUntilFire", 0);
            Scribe_Values.Look(ref burstShotsLeft, "burstShotsLeft", 0);
            Scribe_Values.Look(ref burstCooldown, "burstCooldown", 0);
            Scribe_References.Look(ref currentTarget, "currentTarget");
        }
    }
}
