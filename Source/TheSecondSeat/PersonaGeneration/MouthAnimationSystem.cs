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
    /// ? v1.6.31: 三态随机切换（opened_mouth, medium_mouth, small_mouth）
    /// </summary>
    public static class MouthAnimationSystem
    {
        private static Dictionary<string, MouthState> mouthStates = new Dictionary<string, MouthState>();
        
        // 张嘴参数
        private const float MOUTH_SWITCH_INTERVAL = 0.15f; // ? 缩短到0.15秒，更自然
        
        // ? v1.6.31: 三个说话嘴型
        private static readonly string[] SPEAKING_MOUTHS = new string[]
        {
            "opened_mouth",   // 闭嘴/微张
            "medium_mouth",   // 中等张开（新增）
            "small_mouth"     // 小张嘴
        };
        
        /// <summary>
        /// 嘴巴状态数据
        /// ? v1.6.31: 支持三态随机切换
        /// </summary>
        private class MouthState
        {
            public bool isSpeaking;                 // 是否正在说话
            public float lastSwitchTime;            // 上次切换时间
            public int currentMouthIndex;           // ? 当前嘴型索引（0-2）
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
                    currentMouthIndex = 0,
                    currentExpression = ExpressionType.Neutral
                };
                mouthStates[personaDefName] = state;
            }
            return state;
        }
        
        /// <summary>
        /// 获取嘴巴层名称（根据表情和开合程度返回对应的嘴型）
        /// ? v1.6.31: 三态随机切换 - 每0.15秒在3个嘴型之间随机切换
        /// </summary>
        /// <param name="personaDefName">人格 DefName</param>
        /// <returns>嘴巴层名称（opened_mouth, medium_mouth, small_mouth, 或表情对应的嘴型）</returns>
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
                // ? 正在说话：每 0.15 秒随机切换嘴型
                state.isSpeaking = true;
                
                float currentTime = Time.realtimeSinceStartup;
                float elapsed = currentTime - state.lastSwitchTime;
                
                // 每 0.15 秒切换
                if (elapsed >= MOUTH_SWITCH_INTERVAL)
                {
                    // ? 随机选择下一个嘴型（避免连续重复）
                    int nextIndex;
                    do
                    {
                        nextIndex = UnityEngine.Random.Range(0, SPEAKING_MOUTHS.Length);
                    }
                    while (nextIndex == state.currentMouthIndex && SPEAKING_MOUTHS.Length > 1);
                    
                    state.currentMouthIndex = nextIndex;
                    state.lastSwitchTime = currentTime;
                    
                    if (Prefs.DevMode)
                    {
                        Log.Message($"[MouthAnimationSystem] {personaDefName} 切换嘴型: {SPEAKING_MOUTHS[nextIndex]}");
                    }
                }
                
                // 返回当前嘴型
                return SPEAKING_MOUTHS[state.currentMouthIndex];
            }
            else
            {
                // ? 不在说话：根据表情决定嘴型
                if (state.isSpeaking)
                {
                    // 刚停止说话，重置状态
                    state.isSpeaking = false;
                    state.currentMouthIndex = 0;
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
