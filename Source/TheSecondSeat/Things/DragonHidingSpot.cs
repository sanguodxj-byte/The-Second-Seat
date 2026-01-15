using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace TheSecondSeat
{
    public class DragonHidingSpot : ThingWithComps, IThingHolder
    {
        private ThingOwner innerContainer;
        private int ticksToReappear;
        private float explosionRadius;
        private int explosionDamage;
        private float healPct;

        public DragonHidingSpot()
        {
            innerContainer = new ThingOwner<Thing>(this, false, LookMode.Deep);
        }

        public void Setup(Pawn dragon, int duration, float radius, int damage, float healPercentage)
        {
            if (dragon.Spawned)
            {
                dragon.DeSpawn(DestroyMode.Vanish);
            }
            innerContainer.TryAdd(dragon);
            ticksToReappear = duration;
            explosionRadius = radius;
            explosionDamage = damage;
            healPct = healPercentage;
        }

        protected override void Tick()
        {
            base.Tick();
            ticksToReappear--;
            if (ticksToReappear <= 0)
            {
                Reappear();
            }
            else
            {
                // 播放持续特效
                if (ticksToReappear % 10 == 0)
                {
                    FleckMaker.ThrowLightningGlow(Position.ToVector3Shifted(), Map, 1.0f);
                }
            }
        }

        private void Reappear()
        {
            if (innerContainer.Count > 0)
            {
                Thing thing = innerContainer[0];
                
                // 寻找新位置
                IntVec3 newPos = CellFinder.RandomClosewalkCellNear(Position, Map, 5);
                
                // 重新生成
                GenPlace.TryPlaceThing(thing, newPos, Map, ThingPlaceMode.Near);
                
                if (thing is Pawn dragon)
                {
                    // 治疗
                    if (healPct > 0)
                    {
                        List<Hediff_Injury> injuries = new List<Hediff_Injury>();
                        dragon.health.hediffSet.GetHediffs(ref injuries);
                        
                        foreach (var injury in injuries)
                        {
                            // 治疗每个伤口的一定比例
                            float amountToHeal = injury.Severity * healPct;
                            injury.Heal(amountToHeal);
                        }
                    }
                    
                    Messages.Message("TSS_RadiantRebirth".Translate(dragon.LabelShort), dragon, MessageTypeDefOf.PositiveEvent);
                }

                // 爆炸
                GenExplosion.DoExplosion(
                    center: newPos,
                    map: Map,
                    radius: explosionRadius,
                    damType: DamageDefOf.Flame,
                    instigator: thing,
                    damAmount: explosionDamage,
                    armorPenetration: 0.5f,
                    explosionSound: null,
                    weapon: null,
                    projectile: null,
                    intendedTarget: null,
                    postExplosionSpawnThingDef: null,
                    postExplosionSpawnChance: 0f,
                    postExplosionSpawnThingCount: 1,
                    postExplosionGasType: null,
                    applyDamageToExplosionCellsNeighbors: true,
                    preExplosionSpawnThingDef: null,
                    preExplosionSpawnChance: 0f,
                    preExplosionSpawnThingCount: 1,
                    chanceToStartFire: 0.5f,
                    damageFalloff: true
                );
            }
            
            Destroy();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Deep.Look(ref innerContainer, "innerContainer", this);
            Scribe_Values.Look(ref ticksToReappear, "ticksToReappear", 0);
            Scribe_Values.Look(ref explosionRadius, "explosionRadius", 5.9f);
            Scribe_Values.Look(ref explosionDamage, "explosionDamage", 50);
            Scribe_Values.Look(ref healPct, "healPct", 0f);
        }

        public void GetChildHolders(List<IThingHolder> outChildren)
        {
            ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, innerContainer);
        }

        public ThingOwner GetDirectlyHeldThings()
        {
            return innerContainer;
        }
    }
}
