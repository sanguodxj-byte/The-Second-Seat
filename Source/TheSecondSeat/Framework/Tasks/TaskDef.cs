using System;
using System.Collections.Generic;
using Verse;
using RimWorld;

namespace TheSecondSeat.Framework.Tasks
{
    /// <summary>
    /// 导演Agent任务定义
    /// 
    /// 类似于NarratorEventDef，但专门用于触发Agent工具执行
    /// 当条件满足时，向导演Agent发送任务指令
    /// </summary>
    public class TaskDef : Def
    {
        // ============================================
        // 基本信息
        // ============================================
        
        /// <summary>
        /// 任务标签（显示名称）
        /// </summary>
        public string taskLabel = "";
        
        /// <summary>
        /// 任务描述
        /// </summary>
        public string taskDescription = "";
        
        /// <summary>
        /// 任务类别（用于分组和过滤）
        /// </summary>
        public TaskCategory category = TaskCategory.General;
        
        /// <summary>
        /// 优先级（高优先级更频繁检查）
        /// </summary>
        public int priority = 50;
        
        // ============================================
        // 触发条件
        // ============================================
        
        /// <summary>
        /// 触发概率（0.0 - 1.0）
        /// </summary>
        public float chance = 1.0f;
        
        /// <summary>
        /// 冷却时间（Tick）
        /// </summary>
        public int cooldownTicks = 3600; // 默认1分钟
        
        /// <summary>
        /// 是否只触发一次
        /// </summary>
        public bool triggerOnce = false;
        
        /// <summary>
        /// 触发条件列表（AND关系）
        /// </summary>
        public List<TSSTrigger> triggers = new List<TSSTrigger>();
        
        // ============================================
        // 任务内容
        // ============================================
        
        /// <summary>
        /// 要执行的工具名称
        /// </summary>
        public string toolName = "";
        
        /// <summary>
        /// 工具参数（JSON格式字符串或key=value列表）
        /// </summary>
        public List<TaskParameter> parameters = new List<TaskParameter>();
        
        /// <summary>
        /// 任务提示词（发送给Agent的指令）
        /// 支持变量替换：{colonist_count}、{threat_level}、{affinity}等
        /// </summary>
        public string prompt = "";
        
        /// <summary>
        /// 成功后执行的动作
        /// </summary>
        public List<TSSAction> onSuccessActions = new List<TSSAction>();
        
        /// <summary>
        /// 失败后执行的动作
        /// </summary>
        public List<TSSAction> onFailureActions = new List<TSSAction>();
        
        // ============================================
        // 通知设置
        // ============================================
        
        /// <summary>
        /// 是否显示通知
        /// </summary>
        public bool showNotification = true;
        
        /// <summary>
        /// 自定义通知文本
        /// </summary>
        public string customNotificationText = "";
        
        /// <summary>
        /// 是否在任务开始时让叙事者说话
        /// </summary>
        public bool narratorSpeak = false;
        
        /// <summary>
        /// 叙事者说话内容
        /// </summary>
        public string narratorText = "";
        
        // ============================================
        // 运行时状态（不保存到Def）
        // ============================================
        
        [Unsaved]
        public int lastTriggeredTick = -99999;
        
        [Unsaved]
        public bool hasTriggered = false;
        
        /// <summary>
        /// 检查是否可以触发
        /// </summary>
        public bool CanTrigger(Map map, Dictionary<string, object> context)
        {
            // 检查是否已触发（仅触发一次的情况）
            if (triggerOnce && hasTriggered)
                return false;
            
            // 检查冷却
            int currentTick = Find.TickManager.TicksGame;
            if (currentTick - lastTriggeredTick < cooldownTicks)
                return false;
            
            // 检查概率
            if (chance < 1.0f && Rand.Value > chance)
                return false;
            
            // 检查所有触发条件
            foreach (var trigger in triggers)
            {
                if (!trigger.IsSatisfied(map, context))
                    return false;
            }
            
            return true;
        }
        
        /// <summary>
        /// 标记为已触发
        /// </summary>
        public void MarkTriggered()
        {
            lastTriggeredTick = Find.TickManager.TicksGame;
            hasTriggered = true;
        }
        
        /// <summary>
        /// 重置触发状态
        /// </summary>
        public void Reset()
        {
            lastTriggeredTick = -99999;
            hasTriggered = false;
        }
        
        /// <summary>
        /// 构建工具参数字典
        /// </summary>
        public Dictionary<string, object> BuildParameters(Dictionary<string, object> context)
        {
            var result = new Dictionary<string, object>();
            
            foreach (var param in parameters)
            {
                string value = param.value;
                
                // 变量替换
                if (!string.IsNullOrEmpty(value) && value.StartsWith("{") && value.EndsWith("}"))
                {
                    string key = value.Trim('{', '}');
                    if (context.TryGetValue(key, out var contextValue))
                    {
                        result[param.key] = contextValue;
                        continue;
                    }
                }
                
                result[param.key] = value;
            }
            
            return result;
        }
        
        /// <summary>
        /// 构建Prompt（变量替换）
        /// </summary>
        public string BuildPrompt(Dictionary<string, object> context)
        {
            if (string.IsNullOrEmpty(prompt))
                return "";
            
            string result = prompt;
            
            foreach (var kvp in context)
            {
                result = result.Replace("{" + kvp.Key + "}", kvp.Value?.ToString() ?? "");
            }
            
            return result;
        }
    }
    
    /// <summary>
    /// 任务类别
    /// </summary>
    public enum TaskCategory
    {
        General,        // 通用
        Combat,         // 战斗相关
        Resource,       // 资源管理
        Colonist,       // 殖民者相关
        Building,       // 建筑相关
        Event,          // 事件响应
        Maintenance,    // 日常维护
        Emergency       // 紧急情况
    }
    
    /// <summary>
    /// 任务参数
    /// </summary>
    public class TaskParameter
    {
        public string key = "";
        public string value = "";
    }
}
