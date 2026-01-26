using RimWorld;
using UnityEngine;
using Verse;
using System.Linq;

namespace TheSecondSeat
{
    /// <summary>
    /// PawnFlyer 的 ModExtension，用于配置飞行投掷的参数
    /// 通过 XML 配置，避免硬编码
    /// </summary>
    public class CalamityThrowExtension : DefModExtension
    {
        // 基础伤害
        public float baseDamage = 40f;
        
        // 爆炸半径
        public float explosionRadius = 3.5f;
        
        // 护甲穿透
        public float armorPenetration = 999f;
        
        // 默认飞行速度（如果 PawnFlyerProperties 没有配置）
        public float defaultFlightSpeed = 12f;
        
        // 伤害类型 defName（可选，默认使用 Blunt）
        public string damageDefName = "Blunt";
        
        // 爆炸音效 defName（可选）
        public string explosionSoundDefName;
        
        // 投掷者伤害加成 Hediff defName
        public string damageMultiplierHediffDefName = "TSS_CalamityThrowBonus";
        
        // 缓存的 DamageDef
        private DamageDef cachedDamageDef;
        public DamageDef DamageDef
        {
            get
            {
                if (cachedDamageDef == null && !string.IsNullOrEmpty(damageDefName))
                {
                    cachedDamageDef = DefDatabase<DamageDef>.GetNamed(damageDefName, false);
                }
                return cachedDamageDef ?? DamageDefOf.Blunt;
            }
        }
    }

    /// <summary>
    /// 灾厄投掷的 PawnFlyer 实现
    /// 继承 PawnFlyer 并使用公开的 API，避免反射
    /// 通过 DefModExtension 配置参数，避免硬编码
    /// </summary>
    public class PawnFlyer_CalamityThrow : PawnFlyer
    {
        // 存储发射者
        private Thing launcher;
        
        // 缓存的配置扩展
        private CalamityThrowExtension extensionCache;
        private CalamityThrowExtension Extension
        {
            get
            {
                if (extensionCache == null && def != null)
                {
                    extensionCache = def.GetModExtension<CalamityThrowExtension>();
                }
                // 如果没有配置扩展，返回默认值
                return extensionCache ?? new CalamityThrowExtension();
            }
        }

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
        /// 使用 PawnFlyer.MakeFlyer 的静态工厂方法创建 Flyer
        /// 这是 RimWorld 官方推荐的方式
        /// </summary>
        public static PawnFlyer_CalamityThrow MakeCalamityFlyer(
            ThingDef flyerDef,
            Pawn pawn,
            IntVec3 destCell,
            Thing launcher,
            EffecterDef flightEffecterDef = null,
            SoundDef landingSound = null,
            bool flyWithCarriedThing = false)
        {
            // 使用基类的 MakeFlyer 方法
            PawnFlyer_CalamityThrow flyer = (PawnFlyer_CalamityThrow)PawnFlyer.MakeFlyer(
                flyerDef, pawn, destCell, flightEffecterDef, landingSound, flyWithCarriedThing);
            
            if (flyer != null)
            {
                flyer.SetLauncher(launcher);
            }
            
            return flyer;
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

            if (flyingPawn == null || this.Map == null) return;
            
            // 从 ModExtension 获取配置
            var ext = Extension;
            float damageAmount = ext.baseDamage;
            
            // 检查伤害加成 Hediff
            if (this.launcher is Pawn caster && !string.IsNullOrEmpty(ext.damageMultiplierHediffDefName))
            {
                HediffDef bonusDef = DefDatabase<HediffDef>.GetNamed(ext.damageMultiplierHediffDefName, false);
                if (bonusDef != null)
                {
                    Hediff bonusHediff = caster.health.hediffSet.GetFirstHediffOfDef(bonusDef);
                    if (bonusHediff != null)
                    {
                        damageAmount *= bonusHediff.Severity;
                        caster.health.RemoveHediff(bonusHediff);
                    }
                }
            }

            // 获取爆炸音效
            SoundDef explosionSound = null;
            if (!string.IsNullOrEmpty(ext.explosionSoundDefName))
            {
                explosionSound = DefDatabase<SoundDef>.GetNamed(ext.explosionSoundDefName, false);
            }

            // 对区域内的敌人造成爆炸伤害
            GenExplosion.DoExplosion(
                center: this.Position,
                map: this.Map,
                radius: ext.explosionRadius,
                damType: ext.DamageDef,
                instigator: launcher,
                damAmount: (int)damageAmount,
                armorPenetration: ext.armorPenetration,
                explosionSound: explosionSound ?? SoundDefOf.Pawn_Melee_Punch_HitPawn,
                applyDamageToExplosionCellsNeighbors: true
            );

            // 对被投掷的 Pawn 自己也造成伤害
            DamageInfo dinfo = new DamageInfo(
                ext.DamageDef, 
                damageAmount, 
                ext.armorPenetration, 
                -1, 
                launcher, 
                null, 
                null, 
                DamageInfo.SourceCategory.ThingOrUnknown, 
                null, 
                true, 
                true);
            flyingPawn.TakeDamage(dinfo);
        }
    }
}
