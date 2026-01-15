using RimWorld;
using UnityEngine;
using Verse;
using System.Reflection;

namespace TheSecondSeat
{
    public class PawnFlyer_CalamityThrow : PawnFlyer
    {
        private Thing launcher; // 存储发射者
        private static readonly FieldInfo DestinationCellField = typeof(PawnFlyer).GetField("destinationCell", BindingFlags.Instance | BindingFlags.NonPublic);

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref launcher, "launcher");
        }

        public void Initialize(Thing launcher, IntVec3 destination, Pawn thrownPawn)
        {
            this.launcher = launcher;
            this.startVec = thrownPawn.DrawPos;

            // 使用反射设置私有字段
            if (DestinationCellField != null)
            {
                DestinationCellField.SetValue(this, destination);
            }
            else
            {
                Log.Error("[CalamityThrow] Could not find 'destinationCell' field in PawnFlyer via reflection.");
            }
            
            this.GetDirectlyHeldThings().TryAdd(thrownPawn, true);

            if (thrownPawn.Spawned)
            {
                thrownPawn.DeSpawn(DestroyMode.Vanish);
            }
        }
        
        protected override void RespawnPawn()
        {
            Pawn flyingPawn = this.FlyingPawn;
            base.RespawnPawn();

            if (flyingPawn == null) return;
            
            // 计算伤害
            float damageAmount = 40f; // 基础伤害
            if (this.launcher is Pawn caster) // 使用存储的 launcher
            {
                Hediff bonusHediff = caster.health.hediffSet.GetFirstHediffOfDef(DefDatabase<HediffDef>.GetNamed("Sideria_CalamityThrowBonus", false));
                if (bonusHediff != null)
                {
                    damageAmount *= bonusHediff.Severity;
                    caster.health.RemoveHediff(bonusHediff);
                }
            }

            // 对区域内的敌人造成爆炸伤害
            GenExplosion.DoExplosion(
                center: this.Position,
                map: this.Map,
                radius: 3.5f,
                damType: DamageDefOf.Blunt,
                instigator: launcher,
                damAmount: (int)damageAmount,
                armorPenetration: 999f,
                explosionSound: SoundDefOf.Pawn_Melee_Punch_HitPawn,
                applyDamageToExplosionCellsNeighbors: true
            );

            // 对被投掷的 Pawn 自己也造成伤害
            DamageInfo dinfo = new DamageInfo(DamageDefOf.Blunt, damageAmount, 999f, -1, launcher, null, null, DamageInfo.SourceCategory.ThingOrUnknown, null, true, true);
            flyingPawn.TakeDamage(dinfo);
        }
    }
}