using System;
using UnityEngine;
using Verse;
using TheSecondSeat.PersonaGeneration;

namespace TheSecondSeat.Core
{
    /// <summary>
    /// ⭐ v2.3.0: 叙事者空闲/打瞌睡系统
    /// ⭐ v2.4.0: 渐进式打瞌睡动画（眨眼频率逐渐降低 → 半闭眼 → 完全闭眼）
    /// 在睡眠时间（0:00-7:00）且5分钟内无事件/玩家操作时，叙事者会进入打瞌睡状态
    /// </summary>
    public static class NarratorIdleSystem
    {
        // === 状态 ===
        private static bool isDrowsy = false;              // 是否正在打瞌睡
        private static float idleStartTime = 0f;           // 开始空闲的时间戳
        private static float lastActivityTime = 0f;        // 上次活动时间（玩家操作/事件）
        
        // ⭐ v2.4.0: 渐进式打瞌睡状态
        private static DrowsyStage currentDrowsyStage = DrowsyStage.Awake;
        
        // === 配置 ===
        private const float DROWSY_THRESHOLD = 300f;       // 5分钟 = 300秒
        private const int SLEEP_HOUR_START = 0;            // 睡眠时间开始（0:00）
        private const int SLEEP_HOUR_END = 7;              // 睡眠时间结束（7:00）
        
        // ⭐ v2.4.0: 渐进阶段时间配置
        private const float STAGE_YAWNING_THRESHOLD = 120f;    // 2分钟后开始打哈欠
        private const float STAGE_DROWSY_THRESHOLD = 240f;     // 4分钟后进入困倦
        private const float STAGE_SLEEPING_THRESHOLD = 300f;   // 5分钟后完全入睡
        
        /// <summary>
        /// ⭐ v2.4.0: 打瞌睡阶段枚举
        /// </summary>
        public enum DrowsyStage
        {
            Awake,      // 清醒
            Yawning,    // 打哈欠（眨眼频率降低）
            Drowsy,     // 困倦（半闭眼）
            Sleeping    // 入睡（完全闭眼）
        }
        
        /// <summary>
        /// 是否正在打瞌睡
        /// </summary>
        public static bool IsDrowsy => isDrowsy;
        
        /// <summary>
        /// ⭐ v2.4.0: 获取当前打瞌睡阶段
        /// </summary>
        public static DrowsyStage CurrentStage => currentDrowsyStage;
        
        /// <summary>
        /// 获取空闲时长（秒）
        /// </summary>
        public static float IdleDuration => idleStartTime > 0 ? Time.realtimeSinceStartup - idleStartTime : 0f;
        
        /// <summary>
        /// 初始化系统（在游戏加载时调用）
        /// </summary>
        public static void Initialize()
        {
            isDrowsy = false;
            idleStartTime = 0f;
            lastActivityTime = Time.realtimeSinceStartup;
            currentDrowsyStage = DrowsyStage.Awake;
            Log.Message("[NarratorIdleSystem] 初始化完成");
        }
        
        /// <summary>
        /// 记录活动（玩家操作、AI响应、事件等）
        /// 调用此方法会重置空闲计时器
        /// </summary>
        public static void RecordActivity(string reason = "")
        {
            float now = Time.realtimeSinceStartup;
            lastActivityTime = now;
            
            // 如果正在打瞌睡，立即唤醒
            if (isDrowsy || currentDrowsyStage != DrowsyStage.Awake)
            {
                WakeUp(reason);
            }
            
            // 重置空闲开始时间
            idleStartTime = 0f;
        }
        
        /// <summary>
        /// 核心更新方法（每秒调用一次）
        /// ⭐ v2.4.0: 支持渐进式打瞌睡
        /// </summary>
        public static void Tick()
        {
            // 检查设置是否启用生物节律
            var settings = TheSecondSeat.Settings.TheSecondSeatMod.Settings;
            if (settings != null && !settings.enableBioRhythm) return;
            
            float now = Time.realtimeSinceStartup;
            
            // 检查所有条件
            bool shouldBeDrowsy = IsSleepingHours() && !HasRecentActivity();
            
            if (shouldBeDrowsy)
            {
                // 首次进入空闲状态，记录开始时间
                if (idleStartTime == 0f)
                {
                    idleStartTime = now;
                }
                
                // ⭐ v2.4.0: 渐进式阶段切换
                float idleDuration = now - idleStartTime;
                UpdateDrowsyStage(idleDuration);
            }
            else
            {
                // 条件不满足，重置状态
                if (isDrowsy || currentDrowsyStage != DrowsyStage.Awake)
                {
                    WakeUp("条件不满足");
                }
                idleStartTime = 0f;
            }
        }
        
        /// <summary>
        /// ⭐ v2.4.0: 更新打瞌睡阶段
        /// </summary>
        private static void UpdateDrowsyStage(float idleDuration)
        {
            string personaDefName = NarratorController.CurrentPersonaDefName;
            if (string.IsNullOrEmpty(personaDefName)) return;
            
            DrowsyStage newStage;
            
            if (idleDuration >= STAGE_SLEEPING_THRESHOLD)
            {
                newStage = DrowsyStage.Sleeping;
            }
            else if (idleDuration >= STAGE_DROWSY_THRESHOLD)
            {
                newStage = DrowsyStage.Drowsy;
            }
            else if (idleDuration >= STAGE_YAWNING_THRESHOLD)
            {
                newStage = DrowsyStage.Yawning;
            }
            else
            {
                newStage = DrowsyStage.Awake;
            }
            
            // 阶段变化时触发动画
            if (newStage != currentDrowsyStage)
            {
                TransitionToStage(personaDefName, newStage);
            }
        }
        
        /// <summary>
        /// ⭐ v2.4.0: 切换到指定阶段
        /// </summary>
        private static void TransitionToStage(string personaDefName, DrowsyStage newStage)
        {
            DrowsyStage oldStage = currentDrowsyStage;
            currentDrowsyStage = newStage;
            
            switch (newStage)
            {
                case DrowsyStage.Yawning:
                    // 阶段1：打哈欠 - 降低眨眼频率
                    BlinkAnimationSystem.SetBlinkInterval(personaDefName, 6.0f, 10.0f);
                    ExpressionSystem.SetExpression(personaDefName, ExpressionType.Tired, 
                        ExpressionTrigger.RandomVariation, 600, 0);
                    if (Prefs.DevMode)
                    {
                        Log.Message($"[NarratorIdleSystem] 阶段切换: {oldStage} → Yawning (空闲 {IdleDuration:F0} 秒)");
                    }
                    break;
                    
                case DrowsyStage.Drowsy:
                    // 阶段2：困倦 - 半闭眼状态
                    BlinkAnimationSystem.SetBlinkInterval(personaDefName, 10.0f, 15.0f);
                    // TODO: 可以添加半闭眼纹理支持
                    if (Prefs.DevMode)
                    {
                        Log.Message($"[NarratorIdleSystem] 阶段切换: {oldStage} → Drowsy (空闲 {IdleDuration:F0} 秒)");
                    }
                    break;
                    
                case DrowsyStage.Sleeping:
                    // 阶段3：入睡 - 完全闭眼
                    isDrowsy = true;
                    BlinkAnimationSystem.SetDrowsyMode(personaDefName, true);
                    ExpressionSystem.SetExpression(personaDefName, ExpressionType.Tired, 
                        ExpressionTrigger.RandomVariation, 36000, 0);
                    if (Prefs.DevMode)
                    {
                        Log.Message($"[NarratorIdleSystem] 阶段切换: {oldStage} → Sleeping (空闲 {IdleDuration:F0} 秒)");
                    }
                    break;
                    
                case DrowsyStage.Awake:
                    // 清醒状态
                    isDrowsy = false;
                    BlinkAnimationSystem.SetDrowsyMode(personaDefName, false);
                    BlinkAnimationSystem.SetBlinkInterval(personaDefName, 3.0f, 6.0f); // 恢复正常眨眼
                    ExpressionSystem.SetExpression(personaDefName, ExpressionType.Neutral, 
                        ExpressionTrigger.RandomVariation, 300, 0);
                    break;
            }
        }
        
        /// <summary>
        /// 是否处于睡眠时间（0:00 - 7:00）
        /// </summary>
        private static bool IsSleepingHours()
        {
            int hour = DateTime.Now.Hour;
            return hour >= SLEEP_HOUR_START && hour < SLEEP_HOUR_END;
        }
        
        /// <summary>
        /// 检测最近是否有活动（5分钟内）
        /// </summary>
        private static bool HasRecentActivity()
        {
            float now = Time.realtimeSinceStartup;
            return (now - lastActivityTime) < DROWSY_THRESHOLD;
        }
        
        /// <summary>
        /// 唤醒叙事者
        /// ⭐ v2.4.0: 支持从任意阶段唤醒
        /// </summary>
        private static void WakeUp(string reason = "")
        {
            if (!isDrowsy && currentDrowsyStage == DrowsyStage.Awake) return;
            
            string personaDefName = NarratorController.CurrentPersonaDefName;
            if (string.IsNullOrEmpty(personaDefName)) return;
            
            DrowsyStage oldStage = currentDrowsyStage;
            
            isDrowsy = false;
            idleStartTime = 0f;
            currentDrowsyStage = DrowsyStage.Awake;
            
            // 退出打瞌睡模式
            BlinkAnimationSystem.SetDrowsyMode(personaDefName, false);
            BlinkAnimationSystem.SetBlinkInterval(personaDefName, 3.0f, 6.0f); // 恢复正常眨眼
            
            // 恢复正常表情
            ExpressionSystem.SetExpression(personaDefName, ExpressionType.Neutral, 
                ExpressionTrigger.RandomVariation, 300, 0);
            
            if (Prefs.DevMode)
            {
                Log.Message($"[NarratorIdleSystem] 叙事者醒来了 (从 {oldStage}, 原因: {reason})");
            }
        }
        
        /// <summary>
        /// 强制唤醒（供外部调用）
        /// </summary>
        public static void ForceWakeUp(string reason = "外部调用")
        {
            RecordActivity(reason);
        }
    }
}
