using System;
using System.Collections.Generic;
using Verse;
using RimWorld;

namespace TheSecondSeat.Framework
{
    /// <summary>
    /// 叙事者事件定义 - TSS框架核心Def
    /// 
    /// 设计理念：
    /// 1. 数据驱动：所有事件逻辑通过XML定义
    /// 2. 解耦合：与原版IncidentWorker平行，独立运作
    /// 3. 可扩展：通过继承TSSTrigger和TSSAction实现新功能
    /// 
    /// 工作流程：
    /// 1. NarratorEventManager定期检查所有NarratorEventDef
    /// 2. 检查triggers是否全部满足
    /// 3. 检查chance和cooldown
    /// 4. 执行actions列表
    /// 5. 更新cooldown
    /// 
    /// XML配置示例：
    /// <![CDATA[
    /// <TheSecondSeat.Framework.NarratorEventDef>
    ///   <defName>HighAffinityReward</defName>
    ///   <label>高好感度奖励</label>
    ///   <description>当好感度达到80+时，叙事者提供额外帮助</description>
    ///   
    ///   <chance>0.5</chance>
    ///   <cooldownTicks>36000</cooldownTicks>
    ///   
    ///   <triggers>
    ///     <li Class="TheSecondSeat.Framework.Triggers.AffinityRangeTrigger">
    ///       <minAffinity>80</minAffinity>
    ///     </li>
    ///   </triggers>
    ///   
    ///   <actions>
    ///     <li Class="TheSecondSeat.Framework.Actions.ShowDialogueAction">
    ///       <dialogueText>看起来你做得不错！让我帮你一把~</dialogueText>
    ///     </li>
    ///     <li Class="TheSecondSeat.Framework.Actions.SpawnResourceAction">
    ///       <resourceType>Steel</resourceType>
    ///       <amount>100</amount>
    ///     </li>
    ///   </actions>
    /// </TheSecondSeat.Framework.NarratorEventDef>
    /// ]]>
    /// </summary>
    public class NarratorEventDef : Def
    {
        // ============================================
        // 基本信息
        // ============================================
        
        /// <summary>
        /// 事件显示名称（本地化键或直接文本）
        /// </summary>
        public string eventLabel = "";
        
        /// <summary>
        /// 事件描述（用于调试和编辑器显示）
        /// </summary>
        public string eventDescription = "";
        
        /// <summary>
        /// 事件类别（用于分组和过滤）
        /// 例如："Reward", "Punishment", "Dialogue", "Combat"
        /// </summary>
        public string category = "General";
        
        /// <summary>
        /// 事件优先级（数值越高越优先执行）
        /// </summary>
        public int priority = 0;
        
        // ============================================
        // 触发配置
        // ============================================
        
        /// <summary>
        /// 触发条件列表（所有条件必须满足才触发）
        /// </summary>
        public List<TSSTrigger> triggers = new List<TSSTrigger>();
        
        /// <summary>
        /// 触发概率（0.0-1.0）
        /// 1.0 = 100%触发，0.5 = 50%触发
        /// </summary>
        public float chance = 1.0f;
        
        /// <summary>
        /// 冷却时间（游戏Tick）
        /// 0 = 无冷却，3600 = 1分钟，216000 = 1游戏日
        /// </summary>
        public int cooldownTicks = 0;
        
        /// <summary>
        /// 是否只触发一次（触发后自动禁用）
        /// </summary>
        public bool triggerOnce = false;
        
        /// <summary>
        /// 最小触发间隔（游戏Tick）
        /// 防止同一事件频繁触发
        /// </summary>
        public int minIntervalTicks = 3600;
        
        // ============================================
        // 行动配置
        // ============================================
        
        /// <summary>
        /// 行动列表（按顺序执行）
        /// </summary>
        public List<TSSAction> actions = new List<TSSAction>();
        
        /// <summary>
        /// 是否并行执行所有action（默认串行）
        /// </summary>
        public bool parallelExecution = false;
        
        /// <summary>
        /// 执行失败时是否继续执行后续action
        /// </summary>
        public bool continueOnError = true;
        
        // ============================================
        // 条件配置
        // ============================================
        
        /// <summary>
        /// 需要的人格类型（留空表示所有人格都可触发）
        /// </summary>
        public List<string> requiredPersonas = new List<string>();
        
        /// <summary>
        /// 需要的AI模式（留空表示所有模式都可触发）
        /// </summary>
        public List<string> requiredDifficultyModes = new List<string>();
        
        /// <summary>
        /// 最小游戏时间（Tick）
        /// 0 = 无限制，216000 = 游戏开始1天后才能触发
        /// </summary>
        public int minGameTicks = 0;
        
        /// <summary>
        /// 最大游戏时间（Tick）
        /// 0 = 无限制
        /// </summary>
        public int maxGameTicks = 0;
        
        // ============================================
        // UI配置
        // ============================================
        
        /// <summary>
        /// 是否在触发时显示通知消息
        /// </summary>
        public bool showNotification = true;
        
        /// <summary>
        /// 通知消息类型
        /// ❌ 修复：不能在字段初始化时使用 DefOf
        /// ✅ 应该在 ResolveReferences() 中初始化
        /// </summary>
        public MessageTypeDef notificationMessageType;
        
        /// <summary>
        /// 自定义通知文本（留空使用label）
        /// </summary>
        public string customNotificationText = "";
        
        // ============================================
        // 运行时数据（不从XML加载）
        // ============================================
        
        /// <summary>
        /// 上次触发时间（游戏Tick）
        /// </summary>
        [Unsaved]
        private int lastTriggeredTick = 0;
        
        /// <summary>
        /// 已触发次数
        /// </summary>
        [Unsaved]
        private int triggeredCount = 0;
        
        /// <summary>
        /// 是否已被禁用（triggerOnce触发后会设为true）
        /// </summary>
        [Unsaved]
        private bool isDisabled = false;
        
        // ============================================
        // 公共方法
        // ============================================
        
        /// <summary>
        /// 检查事件是否可以触发
        /// </summary>
        public bool CanTrigger(Map map, Dictionary<string, object> context)
        {
            // 检查是否已禁用
            if (isDisabled)
            {
                return false;
            }
            
            // 检查冷却时间
            int currentTick = Find.TickManager.TicksGame;
            if (currentTick - lastTriggeredTick < cooldownTicks)
            {
                return false;
            }
            
            // 检查最小触发间隔
            if (currentTick - lastTriggeredTick < minIntervalTicks)
            {
                return false;
            }
            
            // 检查游戏时间限制
            if (minGameTicks > 0 && currentTick < minGameTicks)
            {
                return false;
            }
            
            if (maxGameTicks > 0 && currentTick > maxGameTicks)
            {
                return false;
            }
            
            // 检查人格限制
            if (requiredPersonas.Count > 0)
            {
                if (!context.TryGetValue("persona", out object personaObj))
                {
                    return false;
                }
                
                string currentPersona = personaObj?.ToString() ?? "";
                if (!requiredPersonas.Contains(currentPersona))
                {
                    return false;
                }
            }
            
            // 检查所有触发器
            if (triggers != null && triggers.Count > 0)
            {
                foreach (var trigger in triggers)
                {
                    if (!trigger.CheckSafe(map, context))
                    {
                        return false;
                    }
                }
            }
            
            // 检查概率
            if (chance < 1.0f && Rand.Value > chance)
            {
                if (Prefs.DevMode)
                {
                    Log.Message($"[NarratorEventDef] Event '{defName}' failed chance roll ({chance:P0})");
                }
                return false;
            }
            
            return true;
        }
        
        /// <summary>
        /// 触发事件（执行所有action）
        /// </summary>
        public void TriggerEvent(Map map, Dictionary<string, object> context)
        {
            try
            {
                if (Prefs.DevMode)
                {
                    Log.Message($"[NarratorEventDef] Triggering event: {defName} ({eventLabel})");
                }
                
                // ? 显示通知（增强null检查）
                if (showNotification)
                {
                    string notifText = !string.IsNullOrEmpty(customNotificationText) 
                        ? customNotificationText 
                        : eventLabel;
                    
                    // ? 确保 MessageType 不为 null，提供默认值
                    var messageType = notificationMessageType ?? MessageTypeDefOf.PositiveEvent;
                    
                    // ? 增加文本验证，避免空文本导致的问题
                    if (!string.IsNullOrWhiteSpace(notifText))
                    {
                        Messages.Message(notifText, messageType);
                    }
                }
                
                // 执行所有action
                if (actions != null && actions.Count > 0)
                {
                    if (parallelExecution)
                    {
                        // 并行执行（不等待延迟）
                        foreach (var action in actions)
                        {
                            ExecuteActionWithDelay(action, map, context);
                        }
                    }
                    else
                    {
                        // 串行执行
                        float totalDelay = 0f;
                        foreach (var action in actions)
                        {
                            totalDelay += action.delayTicks;
                            ExecuteActionWithDelay(action, map, context, totalDelay);
                        }
                    }
                }
                
                // 更新触发状态
                lastTriggeredTick = Find.TickManager.TicksGame;
                triggeredCount++;
                
                // 如果是一次性事件，禁用它
                if (triggerOnce)
                {
                    isDisabled = true;
                    
                    if (Prefs.DevMode)
                    {
                        Log.Message($"[NarratorEventDef] Event '{defName}' disabled after single trigger");
                    }
                }
                
                if (Prefs.DevMode)
                {
                    Log.Message($"[NarratorEventDef] Event '{defName}' executed successfully (total triggers: {triggeredCount})");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[NarratorEventDef] Event '{defName}' failed to trigger: {ex.Message}\n{ex.StackTrace}");
            }
        }
        
        /// <summary>
        /// 执行action（带延迟）
        /// </summary>
        private void ExecuteActionWithDelay(TSSAction action, Map map, Dictionary<string, object> context, float additionalDelay = 0f)
        {
            float totalDelay = action.delayTicks + additionalDelay;
            
            if (totalDelay <= 0f)
            {
                // 立即执行
                action.ExecuteSafe(map, context);
            }
            else
            {
                // 延迟执行
                Find.TickManager.CurTimeSpeed = TimeSpeed.Normal; // 确保时间在运行
                
                // 使用RimWorld的定时器系统
                LongEventHandler.QueueLongEvent(() =>
                {
                    action.ExecuteSafe(map, context);
                }, "ExecutingNarratorAction", false, null);
            }
        }
        
        /// <summary>
        /// 重置触发状态（用于调试或存档兼容）
        /// </summary>
        public void ResetState()
        {
            lastTriggeredTick = 0;
            triggeredCount = 0;
            isDisabled = false;
        }
        
        /// <summary>
        /// 获取下次可触发时间（秒）
        /// </summary>
        public int GetSecondsUntilNextTrigger()
        {
            int currentTick = Find.TickManager.TicksGame;
            int nextTriggerTick = lastTriggeredTick + Math.Max(cooldownTicks, minIntervalTicks);
            int remainingTicks = Math.Max(0, nextTriggerTick - currentTick);
            return remainingTicks / 60; // 转换为秒
        }
        
        /// <summary>
        /// Def加载后验证
        /// </summary>
        public override void ResolveReferences()
        {
            base.ResolveReferences();
            
            // ✅ 在这里初始化 DefOf，避免"未初始化的 DefOf"警告
            if (notificationMessageType == null)
            {
                notificationMessageType = MessageTypeDefOf.PositiveEvent;
            }
            
            // 验证配置
            if (string.IsNullOrEmpty(eventLabel))
            {
                eventLabel = defName;
            }
            
            if (triggers == null)
            {
                triggers = new List<TSSTrigger>();
            }
            
            if (actions == null)
            {
                actions = new List<TSSAction>();
            }
            
            if (requiredPersonas == null)
            {
                requiredPersonas = new List<string>();
            }
            
            if (requiredDifficultyModes == null)
            {
                requiredDifficultyModes = new List<string>();
            }
            
            // 验证所有触发器和行动
            foreach (var trigger in triggers)
            {
                if (!trigger.Validate(out string triggerError))
                {
                    Log.Error($"[NarratorEventDef] Trigger validation failed in '{defName}': {triggerError}");
                }
            }
            
            foreach (var action in actions)
            {
                if (!action.Validate(out string actionError))
                {
                    Log.Error($"[NarratorEventDef] Action validation failed in '{defName}': {actionError}");
                }
            }
            
            if (Prefs.DevMode)
            {
                Log.Message($"[NarratorEventDef] Loaded event: {defName} ({triggers.Count} triggers, {actions.Count} actions)");
            }
        }
    }
}
