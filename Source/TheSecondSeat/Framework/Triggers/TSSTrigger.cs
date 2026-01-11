using System;
using System.Collections.Generic;
using Verse;
using RimWorld;

namespace TheSecondSeat.Framework
{
    /// <summary>
    /// TSS触发器基类 - 定义"何时做"
    /// 
    /// 设计原则：
    /// 1. 单一职责：每个Trigger只检查一种条件
    /// 2. 可组合：多个Trigger可通过AND/OR逻辑组合
    /// 3. 高性能：IsSatisfied方法应尽可能快速
    /// 
    /// XML配置示例：
    /// <![CDATA[
    /// <triggers>
    ///   <li Class="TheSecondSeat.Framework.Triggers.AffinityRangeTrigger">
    ///     <minAffinity>60</minAffinity>
    ///     <maxAffinity>100</maxAffinity>
    ///   </li>
    ///   <li Class="TheSecondSeat.Framework.Triggers.ColonistCountTrigger">
    ///     <minCount>5</minCount>
    ///   </li>
    /// </triggers>
    /// ]]>
    /// </summary>
    public abstract class TSSTrigger
    {
        // ============================================
        // XML可配置字段
        // ============================================
        
        /// <summary>
        /// 触发器唯一标识（用于调试和日志）
        /// </summary>
        public string triggerId = "";
        
        /// <summary>
        /// 是否启用此触发器
        /// </summary>
        public bool enabled = true;
        
        /// <summary>
        /// 是否反转条件（NOT逻辑）
        /// </summary>
        public bool invert = false;
        
        /// <summary>
        /// 触发器权重（用于多触发器优先级排序）
        /// </summary>
        public float weight = 1.0f;
        
        // ============================================
        // 核心方法
        // ============================================
        
        /// <summary>
        /// 检查触发条件是否满足（抽象方法，由子类实现）
        /// </summary>
        /// <param name="map">目标地图</param>
        /// <param name="context">上下文数据</param>
        /// <returns>true = 条件满足，false = 条件不满足</returns>
        public abstract bool IsSatisfied(Map map, Dictionary<string, object> context);
        
        /// <summary>
        /// 安全检查包装器（带异常处理）
        /// 外部调用应使用此方法而非直接调用IsSatisfied
        /// </summary>
        public bool CheckSafe(Map map, Dictionary<string, object> context)
        {
            if (!enabled)
            {
                if (Prefs.DevMode)
                {
                    Log.Message($"[TSSTrigger] Trigger '{triggerId}' is disabled, returning false");
                }
                return false;
            }
            
            try
            {
                bool result = IsSatisfied(map, context);
                
                // 应用反转逻辑
                if (invert)
                {
                    result = !result;
                }
                
                if (Prefs.DevMode && result)
                {
                    Log.Message($"[TSSTrigger] Trigger '{triggerId}' ({GetType().Name}) satisfied");
                }
                
                return result;
            }
            catch (Exception ex)
            {
                Log.Error($"[TSSTrigger] Trigger '{triggerId}' ({GetType().Name}) check failed: {ex.Message}\n{ex.StackTrace}");
                return false; // 出错时默认返回false（安全失败）
            }
        }
        
        /// <summary>
        /// 获取触发器描述（用于UI显示和调试）
        /// </summary>
        public virtual string GetDescription()
        {
            string invertPrefix = invert ? "NOT " : "";
            return $"{invertPrefix}{GetType().Name} (ID: {triggerId})";
        }
        
        /// <summary>
        /// 验证配置是否有效（在加载时调用）
        /// </summary>
        public virtual bool Validate(out string error)
        {
            error = "";
            
            if (weight < 0)
            {
                error = $"weight cannot be negative: {weight}";
                return false;
            }
            
            return true;
        }
        
        /// <summary>
        /// 获取触发器依赖的上下文键列表（用于优化检查顺序）
        /// </summary>
        public virtual List<string> GetRequiredContextKeys()
        {
            return new List<string>();
        }
    }
    
    // ============================================
    // 触发器组合逻辑
    // ============================================
    
    /// <summary>
    /// 触发器组合模式
    /// </summary>
    public enum TriggerCombineMode
    {
        /// <summary>所有触发器都必须满足（AND）</summary>
        All,
        
        /// <summary>任意一个触发器满足即可（OR）</summary>
        Any,
        
        /// <summary>自定义逻辑（需子类实现）</summary>
        Custom
    }
    
    /// <summary>
    /// 触发器组合器 - 用于组合多个触发器
    /// 这是一个特殊的Trigger，包含子触发器列表
    /// </summary>
    public class CompositeTrigger : TSSTrigger
    {
        /// <summary>子触发器列表</summary>
        public List<TSSTrigger> subTriggers = new List<TSSTrigger>();
        
        /// <summary>组合模式</summary>
        public TriggerCombineMode combineMode = TriggerCombineMode.All;
        
        public override bool IsSatisfied(Map map, Dictionary<string, object> context)
        {
            if (subTriggers == null || subTriggers.Count == 0)
            {
                return false;
            }
            
            switch (combineMode)
            {
                case TriggerCombineMode.All:
                    // 所有子触发器都必须满足
                    foreach (var trigger in subTriggers)
                    {
                        if (!trigger.CheckSafe(map, context))
                        {
                            return false;
                        }
                    }
                    return true;
                    
                case TriggerCombineMode.Any:
                    // 任意一个子触发器满足即可
                    foreach (var trigger in subTriggers)
                    {
                        if (trigger.CheckSafe(map, context))
                        {
                            return true;
                        }
                    }
                    return false;
                    
                case TriggerCombineMode.Custom:
                    // 由子类实现自定义逻辑
                    return EvaluateCustomLogic(map, context);
                    
                default:
                    return false;
            }
        }
        
        /// <summary>
        /// 自定义组合逻辑（子类可重写）
        /// </summary>
        protected virtual bool EvaluateCustomLogic(Map map, Dictionary<string, object> context)
        {
            Log.Warning($"[CompositeTrigger] Custom logic not implemented for '{triggerId}', defaulting to ALL mode");
            return IsSatisfied(map, context);
        }
        
        public override string GetDescription()
        {
            string mode = combineMode == TriggerCombineMode.All ? "AND" : "OR";
            return $"Composite [{mode}] ({subTriggers.Count} triggers)";
        }
    }
}
