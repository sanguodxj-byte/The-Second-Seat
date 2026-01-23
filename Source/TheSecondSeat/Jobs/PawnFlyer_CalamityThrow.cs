using RimWorld;
using UnityEngine;
using Verse;
using System.Reflection;

namespace TheSecondSeat
{
    public class PawnFlyer_CalamityThrow : PawnFlyer
    {
        private Thing launcher; // 存储发射者
        // 使用反射访问 PawnFlyer 的私有字段，参考 GodHandMod 实现
        private static FieldInfo destCellField = typeof(PawnFlyer).GetField("destinationCell", BindingFlags.Instance | BindingFlags.NonPublic);
        private static FieldInfo flightDistanceField = typeof(PawnFlyer).GetField("flightDistance", BindingFlags.Instance | BindingFlags.NonPublic);

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref launcher, "launcher");
        }

        public void SetLauncher(Thing launcher)
        {
            this.launcher = launcher;
        }

        /// <summary>
        /// 手动初始化飞行参数，绕过 PawnFlyer.MakeFlyer 的限制（如 Pawn 必须 Spawned）
        /// </summary>
        public void InitializeFlightParams(Vector3 startPosition, IntVec3 destination, float distance)
        {
            this.startVec = startPosition;
            if (destCellField != null) destCellField.SetValue(this, destination);
            if (flightDistanceField != null) flightDistanceField.SetValue(this, distance);
            
            // 确保 flightDistance 被正确设置，PawnFlyer.Tick 需要它
            // 同时设置 ticksFlightTime 以防止 DrawAt 中的除零错误或 NRE
            // 注意：PawnFlyerProperties 可能不包含 FlightSpeed，使用硬编码默认值或反射获取
            float speed = 12f; // 默认速度
            if (this.def?.pawnFlyer != null)
            {
                // 尝试通过反射获取 flightSpeed，如果无法获取则使用默认值
                // 1.5 中通常是 flightSpeed 字段
                var speedField = typeof(PawnFlyerProperties).GetField("flightSpeed", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (speedField != null)
                {
                    speed = (float)speedField.GetValue(this.def.pawnFlyer);
                }
            }
            this.ticksFlightTime = Mathf.Max(1, Mathf.RoundToInt(distance / speed));
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            // 增加安全检查，防止 NRE
            if (this.FlyingPawn == null)
            {
                return;
            }
            
            // 如果 ticksFlightTime 无效，可能会导致计算错误
            if (this.ticksFlightTime <= 0)
            {
                this.ticksFlightTime = 1;
            }

            try
            {
                base.DrawAt(drawLoc, flip);
            }
            catch (System.Exception ex)
            {
                // 仅记录一次错误，避免日志刷屏
                Log.ErrorOnce($"[TheSecondSeat] Exception drawing PawnFlyer_CalamityThrow: {ex}", this.thingIDNumber ^ 0x1234);
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