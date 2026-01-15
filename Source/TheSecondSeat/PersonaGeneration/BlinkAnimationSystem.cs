using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace TheSecondSeat.PersonaGeneration
{
    /// <summary>
    /// 眨眼动画系统 - 为分层立绘提供自然的眨眼效果
    /// ? v1.6.33: 修复眼睛命名 - 根据表情选择对应的eyes，闭眼统一使用closed_eyes
    /// </summary>
    public static class BlinkAnimationSystem
    {
        private static Dictionary<string, BlinkState> blinkStates = new Dictionary<string, BlinkState>();
        
        // 眨眼参数
        private const float MIN_BLINK_INTERVAL = 3.0f;  // 最小眨眼间隔（秒）
        private const float MAX_BLINK_INTERVAL = 6.0f;  // 最大眨眼间隔（秒）
        private const float BLINK_DURATION = 0.15f;     // 眨眼持续时间（秒）
        
        /// <summary>
        /// 眨眼状态数据
        /// </summary>
        private class BlinkState
        {
            public bool isBlinking;              // 是否正在眨眼
            public float blinkProgress;          // 眨眼进度（0-1）
            public float lastBlinkTime;          // 上次眨眼时间
            public float nextBlinkInterval;      // 下次眨眼间隔
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
                    isBlinking = false,
                    blinkProgress = 0f,
                    lastBlinkTime = Time.realtimeSinceStartup,
                    nextBlinkInterval = UnityEngine.Random.Range(MIN_BLINK_INTERVAL, MAX_BLINK_INTERVAL),
                    currentExpression = ExpressionType.Neutral
                };
                blinkStates[personaDefName] = state;
            }
            return state;
        }
        
        /// <summary>
        /// 获取眼睛层名称（根据眨眼状态返回）
        /// ? v1.6.33: 睁眼=表情对应eyes，闭眼=closed_eyes
        /// </summary>
        /// <param name="personaDefName">人格 DefName</param>
        /// <returns>眼睛层名称（表情_eyes 或 closed_eyes）</returns>
        public static string GetEyeLayerName(string personaDefName)
        {
            var state = GetOrCreateState(personaDefName);
            
            // 获取当前表情
            var expressionState = ExpressionSystem.GetExpressionState(personaDefName);
            if (expressionState.CurrentExpression != state.currentExpression)
            {
                state.currentExpression = expressionState.CurrentExpression;
            }
            
            float currentTime = Time.realtimeSinceStartup;
            float elapsed = currentTime - state.lastBlinkTime;
            
            // 检查是否应该开始眨眼
            if (!state.isBlinking && elapsed >= state.nextBlinkInterval)
            {
                state.isBlinking = true;
                state.blinkProgress = 0f;
                state.lastBlinkTime = currentTime;
                state.nextBlinkInterval = UnityEngine.Random.Range(MIN_BLINK_INTERVAL, MAX_BLINK_INTERVAL);
            }
            
            // 更新眨眼进度
            if (state.isBlinking)
            {
                state.blinkProgress += Time.deltaTime / BLINK_DURATION;
                
                if (state.blinkProgress >= 1f)
                {
                    // 眨眼完成
                    state.isBlinking = false;
                    state.blinkProgress = 0f;
                }
            }
            
            // ✅ 返回眼睛层名称
            if (state.isBlinking && state.blinkProgress > 0.3f && state.blinkProgress < 0.7f)
            {
                // 眨眼过程中：闭眼
                return "closed_eyes";
            }
            else
            {
                // ✅ v1.6.81: 睁眼时根据表情+变体选择对应的eyes
                return GetEyesForExpression(personaDefName, state.currentExpression);
            }
        }
        
        /// <summary>
        /// ✅ v1.6.81: 根据表情获取对应的眼睛层
        /// 支持变体：happy_eyes, happy1_eyes, happy2_eyes, happy3_eyes 等
        /// 添加空值防护和图层存在性检查
        /// </summary>
        private static string GetEyesForExpression(string personaDefName, ExpressionType expression)
        {
            // ✅ 空值防护
            if (string.IsNullOrEmpty(personaDefName))
            {
                return GetBaseEyesName(expression);
            }
            
            // 获取变体信息
            var expressionState = ExpressionSystem.GetExpressionState(personaDefName);
            int variant = 0;
            
            if (expressionState != null)
            {
                // 优先使用 Intensity，如果没有则使用 CurrentVariant
                variant = expressionState.Intensity > 0 ? expressionState.Intensity : expressionState.CurrentVariant;
            }
            
            // Neutral 表情不使用变体
            if (expression == ExpressionType.Neutral)
            {
                return null; // Neutral 表情使用 Base 图层
            }
            
            // 获取基础眼睛名称
            string baseName = GetBaseEyesName(expression);
            
            // 如果没有变体（0），返回基础名称
            if (variant <= 0)
            {
                return baseName;
            }
            
            // 构建变体名称：如 happy1_eyes, sad2_eyes
            string expressionName = expression.ToString().ToLower();
            return $"{expressionName}{variant}_eyes";
        }
        
        /// <summary>
        /// ✅ 获取基础眼睛层名称（不带变体编号）
        /// </summary>
        private static string GetBaseEyesName(ExpressionType expression)
        {
            return expression switch
            {
                ExpressionType.Neutral => null, // Neutral 表情使用 Base 图层
                ExpressionType.Happy => "happy_eyes",
                ExpressionType.Sad => "sad_eyes",
                ExpressionType.Angry => "angry_eyes",
                ExpressionType.Confused => "confused_eyes",
                ExpressionType.Shy => "shy_eyes",
                ExpressionType.Surprised => "surprised_eyes",
                ExpressionType.Smug => "smug_eyes",
                ExpressionType.Worried => "worried_eyes",
                ExpressionType.Disappointed => "disappointed_eyes",
                ExpressionType.Thoughtful => "thoughtful_eyes",
                ExpressionType.Annoyed => "annoyed_eyes",
                ExpressionType.Playful => "playful_eyes",
                _ => null  // 默认使用 Base 图层
            };
        }
        
        /// <summary>
        /// ? v1.6.30: 动态设置眨眼间隔（用于感情驱动动画）
        /// </summary>
        public static void SetBlinkInterval(string personaDefName, float minInterval, float maxInterval)
        {
            var state = GetOrCreateState(personaDefName);
            state.nextBlinkInterval = UnityEngine.Random.Range(minInterval, maxInterval);
            
            // 日志已静默：眨眼间隔调整
        }
        
        /// <summary>
        /// 强制触发眨眼
        /// </summary>
        public static void TriggerBlink(string personaDefName)
        {
            var state = GetOrCreateState(personaDefName);
            state.isBlinking = true;
            state.blinkProgress = 0f;
            state.lastBlinkTime = Time.realtimeSinceStartup;
        }
        
        /// <summary>
        /// 清除眨眼状态
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
