using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using TheSecondSeat.TTS;

namespace TheSecondSeat.PersonaGeneration
{
    /// <summary>
    /// 张嘴动画系统 - 为分层立绘提供说话时的嘴巴开合效果
    /// ? 支持 TTS 播放同步
    /// ? 支持表情关联（不同表情有不同嘴型）
    /// ? 平滑的开合过渡
    /// </summary>
    public static class MouthAnimationSystem
    {
        private static Dictionary<string, MouthState> mouthStates = new Dictionary<string, MouthState>();
        
        // 张嘴参数
        private const float MOUTH_OPEN_SPEED = 8.0f;      // 嘴巴开合速度
        private const float MOUTH_CLOSE_SPEED = 6.0f;     // 嘴巴闭合速度
        private const float SPEAKING_FREQUENCY = 8.0f;    // 说话频率（Hz）
        
        /// <summary>
        /// 嘴型类型
        /// </summary>
        public enum MouthShape
        {
            Closed,      // 闭嘴
            Smile,       // 微笑
            OpenSmall,   // 小张嘴
            OpenWide,    // 大张嘴
            Frown        // 皱眉
        }
        
        /// <summary>
        /// 嘴巴状态数据
        /// </summary>
        private class MouthState
        {
            public float targetOpenness;      // 目标开合程度（0~1）
            public float currentOpenness;     // 当前开合程度（0~1）
            public bool isSpeaking;           // 是否正在说话
            public float speakingTime;        // 说话持续时间
            public ExpressionType currentExpression;
        }
        
        /// <summary>
        /// 获取或创建嘴巴状态
        /// </summary>
        private static MouthState GetOrCreateState(string personaDefName)
        {
            if (!mouthStates.TryGetValue(personaDefName, out MouthState state))
            {
                state = new MouthState
                {
                    targetOpenness = 0f,
                    currentOpenness = 0f,
                    isSpeaking = false,
                    speakingTime = 0f,
                    currentExpression = ExpressionType.Neutral
                };
                mouthStates[personaDefName] = state;
            }
            return state;
        }
        
        /// <summary>
        /// 获取嘴巴层名称（根据表情和开合程度返回对应的嘴型）
        /// </summary>
        /// <param name="personaDefName">人格 DefName</param>
        /// <returns>嘴巴层名称（mouth_closed, mouth_smile, mouth_open_small, mouth_open_wide）</returns>
        public static string GetMouthLayerName(string personaDefName)
        {
            var state = GetOrCreateState(personaDefName);
            
            // 获取当前表情
            var expressionState = ExpressionSystem.GetExpressionState(personaDefName);
            if (expressionState.CurrentExpression != state.currentExpression)
            {
                state.currentExpression = expressionState.CurrentExpression;
            }
            
            // 检查是否正在播放 TTS
            // ? 修复：TTSAudioPlayer 没有 IsPlaying 方法，暂时禁用此功能
            bool isPlayingTTS = false;  // TODO: 实现 TTS 播放状态检测
            // bool isPlayingTTS = TTSAudioPlayer.IsPlaying(personaDefName);
            
            if (isPlayingTTS)
            {
                // 正在说话：模拟嘴巴开合
                state.isSpeaking = true;
                state.speakingTime += Time.deltaTime;
                
                // 使用正弦波模拟说话时的嘴巴开合
                float wave = Mathf.Sin(state.speakingTime * SPEAKING_FREQUENCY * 2f * Mathf.PI);
                state.targetOpenness = Mathf.Clamp01((wave + 1f) * 0.5f * 0.7f); // 0~0.7 范围
            }
            else
            {
                // 不在说话：根据表情决定嘴型
                if (state.isSpeaking)
                {
                    state.isSpeaking = false;
                    state.speakingTime = 0f;
                }
                
                state.targetOpenness = GetMouthOpennessForExpression(state.currentExpression);
            }
            
            // 平滑过渡到目标开合程度
            float speed = state.targetOpenness > state.currentOpenness ? MOUTH_OPEN_SPEED : MOUTH_CLOSE_SPEED;
            state.currentOpenness = Mathf.Lerp(state.currentOpenness, state.targetOpenness, Time.deltaTime * speed);
            
            // 根据开合程度返回嘴型
            return GetMouthShapeLayerName(state.currentExpression, state.currentOpenness);
        }
        
        /// <summary>
        /// 根据表情和开合程度获取嘴型层名称
        /// </summary>
        private static string GetMouthShapeLayerName(ExpressionType expression, float openness)
        {
            // 根据表情决定基础嘴型
            MouthShape baseShape = GetMouthShapeForExpression(expression);
            
            // 如果在说话，根据开合程度覆盖嘴型
            if (openness > 0.4f)
            {
                return "mouth_open_wide";
            }
            else if (openness > 0.2f)
            {
                return "mouth_open_small";
            }
            else if (openness > 0.05f)
            {
                // 微微张嘴
                return "mouth_open_small";
            }
            
            // 根据表情返回对应的嘴型
            return baseShape switch
            {
                MouthShape.Smile => "mouth_smile",
                MouthShape.OpenSmall => "mouth_open_small",
                MouthShape.OpenWide => "mouth_open_wide",
                MouthShape.Frown => "mouth_frown",
                _ => "mouth_closed"
            };
        }
        
        /// <summary>
        /// 根据表情获取基础嘴型
        /// </summary>
        private static MouthShape GetMouthShapeForExpression(ExpressionType expression)
        {
            return expression switch
            {
                ExpressionType.Happy => MouthShape.Smile,
                ExpressionType.Surprised => MouthShape.OpenWide,
                ExpressionType.Sad => MouthShape.Frown,
                ExpressionType.Angry => MouthShape.Closed,
                ExpressionType.Confused => MouthShape.OpenSmall,
                ExpressionType.Smug => MouthShape.Smile,
                ExpressionType.Shy => MouthShape.Closed,
                _ => MouthShape.Closed
            };
        }
        
        /// <summary>
        /// 根据表情获取嘴巴开合程度（不在说话时）
        /// </summary>
        private static float GetMouthOpennessForExpression(ExpressionType expression)
        {
            return expression switch
            {
                ExpressionType.Surprised => 0.6f,  // 惊讶时嘴巴微张
                ExpressionType.Confused => 0.2f,   // 疑惑时嘴巴微开
                _ => 0f                             // 其他表情默认闭嘴
            };
        }
        
        /// <summary>
        /// 获取嘴巴层透明度（用于更平滑的过渡）
        /// </summary>
        /// <param name="personaDefName">人格 DefName</param>
        /// <returns>透明度（0~1）</returns>
        public static float GetMouthLayerAlpha(string personaDefName)
        {
            var state = GetOrCreateState(personaDefName);
            
            // 如果在说话，透明度根据开合程度调整
            if (state.isSpeaking)
            {
                return Mathf.Clamp01(state.currentOpenness * 1.2f);
            }
            
            // 不在说话时，根据表情决定透明度
            return state.currentOpenness > 0.05f ? 1f : 0f;
        }
        
        /// <summary>
        /// 强制设置嘴型（用于特殊事件）
        /// </summary>
        public static void SetMouthShape(string personaDefName, MouthShape shape, float openness)
        {
            var state = GetOrCreateState(personaDefName);
            state.targetOpenness = openness;
        }
        
        /// <summary>
        /// 清除嘴巴状态（用于人格切换）
        /// </summary>
        public static void ClearState(string personaDefName)
        {
            mouthStates.Remove(personaDefName);
        }
        
        /// <summary>
        /// 清除所有嘴巴状态
        /// </summary>
        public static void ClearAllStates()
        {
            mouthStates.Clear();
        }
    }
}
