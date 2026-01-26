using System.Collections.Generic;
using RimWorld;
using Verse;

namespace TheSecondSeat.Hediffs
{
    /// <summary>
    /// 猩红绽放光环组件属性
    /// 持续给范围内敌人叠加猩红绽放标记
    /// </summary>
    public class HediffCompProperties_CrimsonBloomAura : HediffCompProperties
    {
        // === 可配置参数 ===
        
        /// <summary>光环半径</summary>
        public float radius = 9.9f;
        
        /// <summary>检测间隔（Ticks）</summary>
        public int checkInterval = 60;
        
        /// <summary>标记 Hediff（通过 defName 配置）</summary>
        public string markHediffDefName;
        
        /// <summary>是否影响敌人</summary>
        public bool affectEnemies = true;
        
        /// <summary>是否影响盟友</summary>
        public bool affectAllies = false;
        
        /// <summary>激活时的特效</summary>
        public EffecterDef activeEffect;
        
        // === 缓存的 Def 引用 ===
        private HediffDef cachedMarkHediffDef;
        
        public HediffDef MarkHediffDef
        {
            get
            {
                if (cachedMarkHediffDef == null && !string.IsNullOrEmpty(markHediffDefName))
                {
                    cachedMarkHediffDef = DefDatabase<HediffDef>.GetNamed(markHediffDefName, false);
                    if (cachedMarkHediffDef == null)
                    {
                        Log.Error($"[CrimsonBloomAura] HediffDef '{markHediffDefName}' not found!");
                    }
                }
                return cachedMarkHediffDef;
            }
        }
        
        public HediffCompProperties_CrimsonBloomAura()
        {
            this.compClass = typeof(HediffComp_CrimsonBloomAura);
        }
    }

    /// <summary>
    /// 猩红绽放光环组件
    /// 每秒给范围内敌人叠加一层猩红绽放标记
    /// 优化：使用 Pawn 列表遍历替代范围扫描，提升性能
    /// </summary>
    public class HediffComp_CrimsonBloomAura : HediffComp
    {
        public HediffCompProperties_CrimsonBloomAura Props => (HediffCompProperties_CrimsonBloomAura)props;
        
        private Effecter effecter;
        
        // 缓存半径的平方，避免每次计算时开方
        private float radiusSquaredCache = -1f;
        private float RadiusSquared
        {
            get
            {
                if (radiusSquaredCache < 0)
                {
                    radiusSquaredCache = Props.radius * Props.radius;
                }
                return radiusSquaredCache;
            }
        }

        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);
            
            if (Pawn.IsHashIntervalTick(Props.checkInterval))
            {
                ApplyAuraOptimized();
            }
            
            // Handle continuous visual effect
            if (Props.activeEffect != null)
            {
                if (effecter == null)
                {
                    effecter = Props.activeEffect.Spawn();
                    effecter.Trigger(Pawn, TargetInfo.Invalid);
                }
                effecter.EffectTick(Pawn, TargetInfo.Invalid);
            }
        }

        /// <summary>
        /// 优化的光环应用方法
        /// 使用 Pawn 列表遍历 + 距离平方判断，替代 GenRadial.RadialDistinctThingsAround
        /// 性能提升：O(n) 而非 O(r²)，其中 n 是地图上的 Pawn 数量，r 是半径格子数
        /// </summary>
        private void ApplyAuraOptimized()
        {
            if (Pawn.Map == null) return;
            
            HediffDef markDef = Props.MarkHediffDef;
            if (markDef == null) return;
            
            Map map = Pawn.Map;
            IntVec3 myPos = Pawn.Position;
            float radiusSq = RadiusSquared;
            
            // 使用 AllPawnsSpawned 遍历，比范围扫描更高效
            // 特别是在大半径的情况下
            var allPawns = map.mapPawns.AllPawnsSpawned;
            
            for (int i = 0; i < allPawns.Count; i++)
            {
                Pawn target = allPawns[i];
                
                // 排除自己、死亡、倒地的 Pawn
                if (target == Pawn || target.Dead || target.Downed)
                {
                    continue;
                }
                
                // 使用距离平方判断，避免开方运算
                float distSq = (target.Position - myPos).LengthHorizontalSquared;
                if (distSq > radiusSq)
                {
                    continue;
                }
                
                // 检查是否应该影响此目标
                bool isEnemy = target.HostileTo(Pawn);
                if (!((Props.affectEnemies && isEnemy) || (Props.affectAllies && !isEnemy)))
                {
                    continue;
                }
                
                // 应用标记
                ApplyCrimsonBloomMark(target, markDef);
            }
        }

        /// <summary>
        /// 应用猩红绽放标记
        /// </summary>
        private void ApplyCrimsonBloomMark(Pawn target, HediffDef markDef)
        {
            Hediff existing = target.health.hediffSet.GetFirstHediffOfDef(markDef);
            
            if (existing != null)
            {
                // 找到现有的 HediffComp_CrimsonBloom 并调用 AddStack
                var bloomComp = existing.TryGetComp<HediffComp_CrimsonBloom>();
                if (bloomComp != null)
                {
                    bloomComp.AddStack(Pawn);
                }
            }
            else
            {
                // 添加新的标记
                Hediff hediff = HediffMaker.MakeHediff(markDef, target);
                hediff.Severity = 0.33f; // 初始1层
                target.health.AddHediff(hediff, null, null);
            }
        }
        
        public override void CompPostPostRemoved()
        {
            base.CompPostPostRemoved();
            if (effecter != null)
            {
                effecter.Cleanup();
                effecter = null;
            }
        }
    }
}
