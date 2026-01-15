using System;
using System.Collections.Generic;
using System.Reflection;
using RimWorld;
using Verse;

namespace TheSecondSeat.Hediffs
{
    /// <summary>
    /// 通用光环应用组件属性
    /// 可以通过 XML 配置各种光环效果，包括叠层效果
    /// </summary>
    public class HediffCompProperties_ApplyAura : HediffCompProperties
    {
        /// <summary>光环半径</summary>
        public float radius = 9.9f;
        
        /// <summary>检查间隔（ticks），60 = 1秒</summary>
        public int checkInterval = 60;
        
        /// <summary>要应用的 Hediff</summary>
        public HediffDef hediffToApply;
        
        /// <summary>是否影响敌人</summary>
        public bool affectEnemies = true;
        
        /// <summary>是否影响友军</summary>
        public bool affectAllies = false;
        
        /// <summary>是否影响自己</summary>
        public bool affectSelf = false;
        
        /// <summary>
        /// 叠层模式：
        /// - None: 只应用一次，不叠加
        /// - Severity: 每次增加 severity
        /// - CustomMethod: 调用自定义叠层方法
        /// </summary>
        public StackMode stackMode = StackMode.None;
        
        /// <summary>Severity 增量（stackMode = Severity 时使用）</summary>
        public float severityIncrease = 0.33f;
        
        /// <summary>最大 severity（0 表示无限制）</summary>
        public float maxSeverity = 0f;
        
        /// <summary>
        /// 自定义叠层方法名（stackMode = CustomMethod 时使用）
        /// 方法签名应为：void MethodName(Pawn applier)
        /// 会在目标 Hediff 的所有 HediffComp 中查找此方法
        /// </summary>
        public string stackMethodName = "AddStack";
        
        /// <summary>初始 severity（新添加 Hediff 时）</summary>
        public float initialSeverity = 0.33f;
        
        /// <summary>持续视觉效果</summary>
        public EffecterDef activeEffect;
        
        /// <summary>每次应用时的闪烁效果</summary>
        public FleckDef hitFleck;
        
        /// <summary>是否只影响可见目标</summary>
        public bool requireLineOfSight = false;
        
        public HediffCompProperties_ApplyAura()
        {
            this.compClass = typeof(HediffComp_ApplyAura);
        }
    }

    /// <summary>
    /// 叠层模式枚举
    /// </summary>
    public enum StackMode
    {
        /// <summary>不叠加，只应用一次</summary>
        None,
        
        /// <summary>通过增加 severity 来叠加</summary>
        Severity,
        
        /// <summary>调用自定义方法来叠加</summary>
        CustomMethod
    }

    /// <summary>
    /// 通用光环应用组件
    /// 定期给范围内目标应用 Hediff，支持多种叠层模式
    /// 
    /// XML 配置示例：
    /// <comps>
    ///   <li Class="TheSecondSeat.Hediffs.HediffCompProperties_ApplyAura">
    ///     <radius>9.9</radius>
    ///     <checkInterval>60</checkInterval>
    ///     <hediffToApply>Sideria_CrimsonBloomMark</hediffToApply>
    ///     <affectEnemies>true</affectEnemies>
    ///     <stackMode>CustomMethod</stackMode>
    ///     <stackMethodName>AddStack</stackMethodName>
    ///     <initialSeverity>0.33</initialSeverity>
    ///   </li>
    /// </comps>
    /// </summary>
    public class HediffComp_ApplyAura : HediffComp
    {
        public HediffCompProperties_ApplyAura Props => (HediffCompProperties_ApplyAura)props;
        
        private Effecter effecter;
        
        // 缓存反射方法信息，避免每次都查找
        private Dictionary<Type, MethodInfo> methodCache = new Dictionary<Type, MethodInfo>();

        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);
            
            if (Pawn.IsHashIntervalTick(Props.checkInterval))
            {
                ApplyAura();
            }
            
            // 处理持续视觉效果
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

        private void ApplyAura()
        {
            if (Pawn.Map == null || Props.hediffToApply == null) return;
            
            var targets = GenRadial.RadialDistinctThingsAround(Pawn.Position, Pawn.Map, Props.radius, true);
            
            foreach (var thing in targets)
            {
                if (thing is Pawn target && !target.Dead)
                {
                    if (ShouldAffect(target))
                    {
                        ApplyHediffToTarget(target);
                    }
                }
            }
        }

        private bool ShouldAffect(Pawn target)
        {
            // 检查是否是自己
            if (target == Pawn)
            {
                return Props.affectSelf;
            }
            
            // 检查敌友关系
            bool isEnemy = target.HostileTo(Pawn);
            
            if (isEnemy && !Props.affectEnemies) return false;
            if (!isEnemy && !Props.affectAllies) return false;
            
            // 检查视线（如果需要）
            if (Props.requireLineOfSight)
            {
                if (!GenSight.LineOfSight(Pawn.Position, target.Position, Pawn.Map))
                {
                    return false;
                }
            }
            
            return true;
        }

        private void ApplyHediffToTarget(Pawn target)
        {
            Hediff existing = target.health.hediffSet.GetFirstHediffOfDef(Props.hediffToApply);
            
            if (existing != null)
            {
                // 已有 Hediff，根据模式处理叠层
                HandleStacking(existing, target);
            }
            else
            {
                // 新添加 Hediff
                AddNewHediff(target);
            }
            
            // 显示闪烁效果
            if (Props.hitFleck != null && target.Map != null)
            {
                FleckMaker.AttachedOverlay(target, Props.hitFleck, UnityEngine.Vector3.zero);
            }
        }

        private void HandleStacking(Hediff existing, Pawn target)
        {
            switch (Props.stackMode)
            {
                case StackMode.None:
                    // 不叠加，什么都不做
                    break;
                    
                case StackMode.Severity:
                    // 通过增加 severity 叠加
                    float newSeverity = existing.Severity + Props.severityIncrease;
                    if (Props.maxSeverity > 0)
                    {
                        newSeverity = System.Math.Min(newSeverity, Props.maxSeverity);
                    }
                    existing.Severity = newSeverity;
                    break;
                    
                case StackMode.CustomMethod:
                    // 调用自定义方法
                    CallStackMethod(existing);
                    break;
            }
        }

        private void CallStackMethod(Hediff hediff)
        {
            if (string.IsNullOrEmpty(Props.stackMethodName)) return;
            
            // 需要转型为 HediffWithComps 才能访问 comps
            if (!(hediff is HediffWithComps hediffWithComps)) return;
            
            // 在所有 HediffComp 中查找方法
            var comps = hediffWithComps.comps;
            if (comps == null) return;
            
            foreach (var comp in comps)
            {
                Type compType = comp.GetType();
                
                // 尝试从缓存获取方法
                if (!methodCache.TryGetValue(compType, out MethodInfo method))
                {
                    // 查找方法：void MethodName(Pawn applier)
                    method = compType.GetMethod(Props.stackMethodName, 
                        BindingFlags.Public | BindingFlags.Instance,
                        null,
                        new Type[] { typeof(Pawn) },
                        null);
                    
                    // 缓存结果（包括 null）
                    methodCache[compType] = method;
                }
                
                if (method != null)
                {
                    try
                    {
                        method.Invoke(comp, new object[] { Pawn });
                        return; // 找到并调用了，退出
                    }
                    catch (Exception ex)
                    {
                        Log.Warning($"[TSS] ApplyAura: Failed to call {Props.stackMethodName} on {compType.Name}: {ex.Message}");
                    }
                }
            }
        }

        private void AddNewHediff(Pawn target)
        {
            Hediff hediff = HediffMaker.MakeHediff(Props.hediffToApply, target);
            hediff.Severity = Props.initialSeverity;
            target.health.AddHediff(hediff, null, null);
        }
        
        public override void CompPostPostRemoved()
        {
            base.CompPostPostRemoved();
            if (effecter != null)
            {
                effecter.Cleanup();
                effecter = null;
            }
            methodCache.Clear();
        }
        
        public override string CompTipStringExtra
        {
            get
            {
                return "TSS_ApplyAura_Tip".Translate(Props.radius.ToString("F1"));
            }
        }
    }
}