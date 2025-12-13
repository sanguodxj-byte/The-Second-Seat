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
        /// ? v1.6.30: 简化版口型同步
        /// </summary>
        private class MouthState
        {
            public bool isSpeaking;                 // 是否正在说话
            public float lastSwitchTime;            // 上次切换时间
            public bool isSmallMouth;               // 当前是否为small_mouth（false=opened_mouth）
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
                    isSpeaking = false,
                    lastSwitchTime = Time.realtimeSinceStartup,
                    isSmallMouth = false,
                    currentExpression = ExpressionType.Neutral
                };
                mouthStates[personaDefName] = state;
            }
            return state;
        }
        
        /// <summary>
        /// 获取嘴巴层名称（根据表情和开合程度返回对应的嘴型）
        /// ? v1.6.30: 简化版口型同步 - 每0.2秒在opened_mouth和small_mouth之间切换
        /// </summary>
        /// <param name="personaDefName">人格 DefName</param>
        /// <returns>嘴巴层名称（opened_mouth, small_mouth, 或表情对应的嘴型）</returns>
        public static string GetMouthLayerName(string personaDefName)
        {
            var state = GetOrCreateState(personaDefName);
            
            // 获取当前表情
            var expressionState = ExpressionSystem.GetExpressionState(personaDefName);
            if (expressionState.CurrentExpression != state.currentExpression)
            {
                state.currentExpression = expressionState.CurrentExpression;
            }
            
            // ? v1.6.30: 检查是否正在播放 TTS（口型同步系统已集成）
            bool isPlayingTTS = TTS.TTSAudioPlayer.IsSpeaking(personaDefName);
            
            if (isPlayingTTS)
            {
                // ? 正在说话：每 0.2 秒切换一次嘴型
                state.isSpeaking = true;
                
                float currentTime = Time.realtimeSinceStartup;
                float elapsed = currentTime - state.lastSwitchTime;
                
                // 每 0.2 秒切换
                if (elapsed >= 0.2f)
                {
                    state.isSmallMouth = !state.isSmallMouth;
                    state.lastSwitchTime = currentTime;
                }
                
                // 返回对应的嘴型
                return state.isSmallMouth ? "small_mouth" : "opened_mouth";
            }
            else
            {
                // ? 不在说话：根据表情决定嘴型
                if (state.isSpeaking)
                {
                    // 刚停止说话，重置状态
                    state.isSpeaking = false;
                    state.isSmallMouth = false;
                }
                
                // 根据表情返回对应的嘴型
                return GetMouthShapeForExpression(state.currentExpression);
            }
        }
        
        /// <summary>
        /// 根据表情获取嘴型层名称
        /// ? v1.6.30: 简化版 - 直接返回 LayeredPortraitCompositor 使用的名称
        /// </summary>
        private static string GetMouthShapeForExpression(ExpressionType expression)
        {
            return expression switch
            {
                ExpressionType.Happy => "larger_mouth",      // 开心用大嘴
                ExpressionType.Surprised => "larger_mouth",  // 惊讶张大嘴
                ExpressionType.Sad => "sad_mouth",
                ExpressionType.Angry => "angry_mouth",
                ExpressionType.Smug => "small1_mouth",       // 得意用小嘴变体
                _ => "opened_mouth"                          // 默认闭嘴
            };
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
