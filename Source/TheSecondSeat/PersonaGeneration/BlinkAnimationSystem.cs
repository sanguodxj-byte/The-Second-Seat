using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace TheSecondSeat.PersonaGeneration
{
    /// <summary>
    /// 眨眼动画系统 - 为分层立绘提供自然的眨眼效果
    /// ? 支持随机眨眼周期
    /// ? 支持表情关联（不同表情有不同眨眼频率）
    /// ? 平滑的闭眼/睁眼过渡
    /// </summary>
    public static class BlinkAnimationSystem
    {
        private static Dictionary<string, BlinkState> blinkStates = new Dictionary<string, BlinkState>();
        
        // 眨眼参数
        private const float BLINK_CYCLE_MIN = 3.0f;      // 最小眨眼周期（秒）
        private const float BLINK_CYCLE_MAX = 6.0f;      // 最大眨眼周期（秒）
        private const float BLINK_DURATION = 0.15f;      // 闭眼持续时间（秒）
        private const float BLINK_TRANSITION = 0.05f;    // 过渡时间（秒）
        
        /// <summary>
        /// 眨眼状态数据
        /// </summary>
        private class BlinkState
        {
            public float lastBlinkTime;
            public float nextBlinkInterval;
            public bool isBlinking;
            public float blinkProgress;
            public ExpressionType currentExpression;
        }
        
        /// <summary>
        /// 获取或创建眨眼状态
        /// </summary>
        private static BlinkState GetOrCreateState(string personaDefName)
        {
            if (!blinkStates.TryGetValue(personaDefName, out BlinkState state))
            {
                state = new BlinkState
                {
                    lastBlinkTime = Time.realtimeSinceStartup,
                    nextBlinkInterval = UnityEngine.Random.Range(BLINK_CYCLE_MIN, BLINK_CYCLE_MAX),
                    isBlinking = false,
                    blinkProgress = 0f,
                    currentExpression = ExpressionType.Neutral
                };
                blinkStates[personaDefName] = state;
            }
            return state;
        }
        
        /// <summary>
        /// 获取眨眼层名称（根据进度返回对应的眨眼帧）
        /// </summary>
        /// <param name="personaDefName">人格 DefName</param>
        /// <returns>眨眼层名称（eyes_open, eyes_half, eyes_closed）</returns>
        public static string GetBlinkLayerName(string personaDefName)
        {
            var state = GetOrCreateState(personaDefName);
            
            // 获取当前表情（用于调整眨眼频率）
            var expressionState = ExpressionSystem.GetExpressionState(personaDefName);
            if (expressionState.CurrentExpression != state.currentExpression)
            {
                state.currentExpression = expressionState.CurrentExpression;
                // 表情变化时，重置眨眼周期
                state.nextBlinkInterval = GetBlinkIntervalForExpression(state.currentExpression);
            }
            
            float currentTime = Time.realtimeSinceStartup;
            float elapsed = currentTime - state.lastBlinkTime;
            
            // 判断是否到达眨眼时间
            if (!state.isBlinking && elapsed >= state.nextBlinkInterval)
            {
                // 开始眨眼
                state.isBlinking = true;
                state.blinkProgress = 0f;
                state.lastBlinkTime = currentTime;
            }
            
            // 眨眼动画逻辑
            if (state.isBlinking)
            {
                state.blinkProgress += Time.deltaTime;
                
                // 眨眼完成
                if (state.blinkProgress >= BLINK_DURATION)
                {
                    state.isBlinking = false;
                    state.blinkProgress = 0f;
                    state.nextBlinkInterval = UnityEngine.Random.Range(BLINK_CYCLE_MIN, BLINK_CYCLE_MAX);
                    return "eyes_open";
                }
                
                // 眨眼阶段判断
                float progress = state.blinkProgress / BLINK_DURATION;
                
                if (progress < 0.33f)
                {
                    // 第1阶段：睁眼 → 半闭
                    return "eyes_half";
                }
                else if (progress < 0.67f)
                {
                    // 第2阶段：半闭 → 闭眼
                    return "eyes_closed";
                }
                else
                {
                    // 第3阶段：闭眼 → 半闭
                    return "eyes_half";
                }
            }
            
            // 默认：睁眼
            return "eyes_open";
        }
        
        /// <summary>
        /// 获取眨眼层透明度（用于更平滑的过渡）
        /// </summary>
        /// <param name="personaDefName">人格 DefName</param>
        /// <returns>透明度（0~1）</returns>
        public static float GetBlinkLayerAlpha(string personaDefName)
        {
            var state = GetOrCreateState(personaDefName);
            
            if (!state.isBlinking)
            {
                return 1f; // 完全显示睁眼层
            }
            
            float progress = state.blinkProgress / BLINK_DURATION;
            
            // 平滑的透明度过渡（使用正弦波）
            return Mathf.Sin(progress * Mathf.PI);
        }
        
        /// <summary>
        /// 根据表情获取眨眼频率
        /// </summary>
        private static float GetBlinkIntervalForExpression(ExpressionType expression)
        {
            return expression switch
            {
                ExpressionType.Surprised => UnityEngine.Random.Range(1.5f, 3.0f),  // 惊讶时眨眼频繁
                ExpressionType.Confused => UnityEngine.Random.Range(2.0f, 4.0f),   // 疑惑时眨眼较频繁
                ExpressionType.Sad => UnityEngine.Random.Range(4.0f, 7.0f),        // 悲伤时眨眼较慢
                ExpressionType.Angry => UnityEngine.Random.Range(4.0f, 8.0f),      // 生气时眨眼较慢
                ExpressionType.Happy => UnityEngine.Random.Range(2.5f, 5.0f),      // 开心时正常
                _ => UnityEngine.Random.Range(BLINK_CYCLE_MIN, BLINK_CYCLE_MAX)   // 默认
            };
        }

        /// <summary>
        /// ? v1.6.30: 动态设置眨眼间隔（用于感情驱动动画）
        /// </summary>
        /// <param name="personaDefName">人格 DefName</param>
        /// <param name="minInterval">最小间隔（秒）</param>
        /// <param name="maxInterval">最大间隔（秒）</param>
        public static void SetBlinkInterval(string personaDefName, float minInterval, float maxInterval)
        {
            var state = GetOrCreateState(personaDefName);
            
            // 更新下次眨眼间隔
            state.nextBlinkInterval = UnityEngine.Random.Range(minInterval, maxInterval);
            
            if (Prefs.DevMode)
            {
                Log.Message($"[BlinkAnimationSystem] 眨眼间隔调整: {personaDefName} ({minInterval}-{maxInterval}秒)");
            }
        }
        
        /// <summary>
        /// 强制触发眨眼（用于特殊事件）
        /// </summary>
        public static void TriggerBlink(string personaDefName)
        {
            var state = GetOrCreateState(personaDefName);
            state.isBlinking = true;
            state.blinkProgress = 0f;
            state.lastBlinkTime = Time.realtimeSinceStartup;
        }
        
        /// <summary>
        /// 清除眨眼状态（用于人格切换）
        /// </summary>
        public static void ClearState(string personaDefName)
        {
            blinkStates.Remove(personaDefName);
        }
        
        /// <summary>
        /// 清除所有眨眼状态
        /// </summary>
        public static void ClearAllStates()
        {
            blinkStates.Clear();
        }
    }
}
