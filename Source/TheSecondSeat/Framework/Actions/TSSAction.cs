using System;
using System.Collections.Generic;
using Verse;
using RimWorld;

namespace TheSecondSeat.Framework
{
    /// <summary>
    /// TSS行动基类 - 定义"做什么"
    /// 
    /// 设计原则：
    /// 1. 原子性：每个Action只做一件事
    /// 2. 无状态：所有数据通过context传入
    /// 3. 可组合：多个Action可串联执行
    /// 
    /// XML配置示例：
    /// <![CDATA[
    /// <actions>
    ///   <li Class="TheSecondSeat.Framework.Actions.ModifyAffinityAction">
    ///     <delta>10</delta>
    ///     <reason>玩家完成了挑战</reason>
    ///   </li>
    /// </actions>
    /// ]]>
    /// </summary>
    public abstract class TSSAction
    {
        // ============================================
        // XML可配置字段
        // ============================================
        
        /// <summary>
        /// 执行延迟（游戏Tick）
        /// 0 = 立即执行，3600 = 1分钟后执行
        /// </summary>
        public float delayTicks = 0f;
        
        /// <summary>
        /// Action唯一标识（用于调试和日志）
        /// </summary>
        public string actionId = "";
        
        /// <summary>
        /// 是否启用此Action
        /// </summary>
        public bool enabled = true;
        
        /// <summary>
        /// 执行条件表达式（可选，留空表示无条件执行）
        /// 例如："context['affinity'] > 50"
        /// </summary>
        public string conditionExpression = "";
        
        // ============================================
        // 核心方法
        // ============================================
        
        /// <summary>
        /// 执行行动（抽象方法，由子类实现）
        /// </summary>
        /// <param name="map">目标地图</param>
        /// <param name="context">上下文数据（共享数据，如affinity、persona等）</param>
        public abstract void Execute(Map map, in NarratorContext context);
        
        /// <summary>
        /// 安全执行包装器（带异常处理）
        /// 外部调用应使用此方法而非直接调用Execute
        /// </summary>
        public void ExecuteSafe(Map map, in NarratorContext context)
        {
            if (!enabled)
            {
                if (Prefs.DevMode)
                {
                    Log.Message($"[TSSAction] Action '{actionId}' is disabled, skipping");
                }
                return;
            }
            
            try
            {
                // 检查执行条件
                if (!string.IsNullOrEmpty(conditionExpression))
                {
                    if (!EvaluateCondition(context))
                    {
                        if (Prefs.DevMode)
                        {
                            Log.Message($"[TSSAction] Action '{actionId}' condition not met: {conditionExpression}");
                        }
                        return;
                    }
                }
                
                // 执行Action
                if (Prefs.DevMode)
                {
                    Log.Message($"[TSSAction] Executing action: {actionId} ({GetType().Name})");
                }
                
                Execute(map, context);
                
                if (Prefs.DevMode)
                {
                    Log.Message($"[TSSAction] Action '{actionId}' executed successfully");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[TSSAction] Action '{actionId}' ({GetType().Name}) failed: {ex.Message}\n{ex.StackTrace}");
            }
        }
        
        /// <summary>
        /// 评估条件表达式（简单实现，可扩展）
        /// </summary>
        private bool EvaluateCondition(in NarratorContext context)
        {
            try
            {
                // TODO: 实现更复杂的表达式解析
                // 当前仅支持简单的键值检查
                return true;
            }
            catch (Exception ex)
            {
                Log.Warning($"[TSSAction] Condition evaluation failed for '{conditionExpression}': {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// 获取Action描述（用于UI显示和调试）
        /// </summary>
        public virtual string GetDescription()
        {
            return $"{GetType().Name} (ID: {actionId})";
        }
        
        /// <summary>
        /// 验证配置是否有效（在加载时调用）
        /// </summary>
        public virtual bool Validate(out string error)
        {
            error = "";
            
            if (delayTicks < 0)
            {
                error = $"delayTicks cannot be negative: {delayTicks}";
                return false;
            }
            
            return true;
        }
    }
}
