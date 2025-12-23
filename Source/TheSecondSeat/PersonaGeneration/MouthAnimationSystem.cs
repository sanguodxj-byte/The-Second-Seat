using System;
using System.Collections.Generic;
using Verse;
using UnityEngine;

namespace TheSecondSeat.PersonaGeneration
{
    /// <summary>
    /// 📌 v1.6.18: 张嘴动画系统（口型同步）
    /// 📌 v1.6.36: 集成 TTSAudioPlayer 状态，实现真正的口型同步
    /// ✅ v1.6.60: 优化平滑过渡，解决"速度过快/抽搐"问题
    /// 
    /// 功能：
    /// - TTS播放时自动张嘴
    /// - 3种嘴型平滑切换（small_mouth, medium_mouth, larger_mouth）
    /// - 平滑阻尼算法（避免机械跳动）
    /// - 最小保持时间（防止高频闪烁）
    /// - 调整阈值（过滤噪音）
    /// 
    /// 使用：
    /// 1. Update() 检测 TTSAudioPlayer.IsSpeaking(defName)
    /// 2. GetMouthLayerName(defName) 返回当前嘴型图层名
    /// </summary>
    public static class MouthAnimationSystem
    {
        // ===== 配置参数 =====
        
        /// <summary>
        /// ✅ v1.6.65: 进一步降低动画速度，提升自然度
        /// 平滑因子（数值越小越慢，越平滑）
        /// v1.6.60: 0.15 → v1.6.65: 0.08 (降低 47%)
        /// </summary>
        private const float SMOOTHING_FACTOR = 0.08f;
        
        /// <summary>
        /// 最小保持时间（秒，避免高频闪烁）
        /// v1.6.60: 0.05s → v1.6.65: 0.15s (增加 200%)
        /// </summary>
        private const float MIN_HOLD_TIME = 0.15f;
        
        /// <summary>
        /// 突变阈值（大于此值立即切换）
        /// </summary>
        private const float SUDDEN_CHANGE_THRESHOLD = 0.5f;
        
        /// <summary>
        /// 闭嘴阈值（提高以过滤噪音）
        /// </summary>
        private const float SILENCE_THRESHOLD = 0.05f;
        
        /// <summary>
        /// 嘴型切换间隔（秒）
        /// v1.6.60: 0.15s → v1.6.65: 0.2s (增加 33%)
        /// </summary>
        private const float MOUTH_CHANGE_INTERVAL = 0.2f;
        
        // ===== 说话状态数据 =====
        private class SpeakingState
        {
            public bool IsSpeaking;                      // 是否正在说话
            public string CurrentMouthLayer;             // 当前嘴型层名称
            public string LastMouthLayer;                // 上一次的嘴型层名称（避免重复）
            public float LastChangeTime;                 // 上次切换时间
            
            // 📌 v1.6.44: 新增字段
            public ExpressionType currentExpression;     // 当前表情
            public float currentOpenness;                // ✅ 当前平滑后的开合度（0-1）
            public float targetRawOpenness;              // ✅ 目标原始开合度（来自正弦波）
            public float speakingTime;                   // 说话累计时间
            public bool isSpeaking;                      // 是否正在说话（新状态标志）
            
            // ✅ v1.6.60: 新增字段（最小保持时间）
            public float lastStateChangeTime;            // 上次状态变化时间
            public string lockedMouthLayer;              // 锁定的嘴型（保持期间）
        }
        
        // ===== 数据存储 =====
        private static readonly Dictionary<string, SpeakingState> speakingStates = new Dictionary<string, SpeakingState>();
        
        // ===== 嘴型列表 =====
        private static readonly string[] mouthLayers = new[]
        {
            "small_mouth",
            "medium_mouth",
            "larger_mouth"
        };
        
        /// <summary>
        /// 📌 v1.6.36: 每帧更新（检测TTS播放状态并触发张嘴动画）
        /// 📌 v1.6.44: 简化为仅更新状态，实际嘴型由 GetMouthLayerName 计算
        /// </summary>
        /// <param name="deltaTime">增量时间</param>
        public static void Update(float deltaTime)
        {
            // 📌 遍历所有人格，检测TTS播放状态
            var allPersonas = DefDatabase<NarratorPersonaDef>.AllDefsListForReading;
            
            foreach (var persona in allPersonas)
            {
                if (persona == null || string.IsNullOrEmpty(persona.defName))
                {
                    continue;
                }
                
                // 📌 检测TTS是否正在播放该人格的音频
                bool isCurrentlySpeaking = TTS.TTSAudioPlayer.IsSpeaking(persona.defName);
                
                // 获取或创建状态
                if (!speakingStates.TryGetValue(persona.defName, out var state))
                {
                    state = new SpeakingState
                    {
                        IsSpeaking = false,
                        CurrentMouthLayer = null,
                        LastMouthLayer = null,
                        LastChangeTime = 0f,
                        currentExpression = ExpressionType.Neutral,
                        currentOpenness = 0f,
                        targetRawOpenness = 0f,
                        speakingTime = 0f,
                        isSpeaking = false,
                        lastStateChangeTime = 0f,
                        lockedMouthLayer = null
                    };
                    speakingStates[persona.defName] = state;
                }
                
                // 📌 更新状态（实际嘴型由 GetMouthLayerName 计算）
                state.IsSpeaking = isCurrentlySpeaking;
                
                if (Prefs.DevMode && isCurrentlySpeaking && !state.isSpeaking)
                {
                    Log.Message($"[MouthAnimationSystem] {persona.defName} 开始说话");
                }
                else if (Prefs.DevMode && !isCurrentlySpeaking && state.isSpeaking)
                {
                    Log.Message($"[MouthAnimationSystem] {persona.defName} 停止说话");
                }
            }
        }
        
        /// <summary>
        /// ✅ v1.6.60: 获取当前嘴巴图层名称（供 LayeredPortraitCompositor 调用）
        /// 优化：
        /// - 平滑阻尼算法（避免机械跳动）
        /// - 最小保持时间（防止高频闪烁）
        /// - 调整阈值（过滤噪音）
        /// - 突变检测（大声说话时立即切换）
        /// </summary>
        /// <param name="defName">人格 DefName</param>
        /// <returns>嘴部图层名称（如果不需要则返回 null）</returns>
        public static string GetMouthLayerName(string defName)
        {
            if (string.IsNullOrEmpty(defName))
            {
                return null;
            }
            
            // 📌 1. 获取或创建状态
            if (!speakingStates.TryGetValue(defName, out var state))
            {
                state = new SpeakingState
                {
                    IsSpeaking = false,
                    CurrentMouthLayer = null,
                    LastMouthLayer = null,
                    LastChangeTime = 0f,
                    currentExpression = ExpressionType.Neutral,
                    currentOpenness = 0f,
                    targetRawOpenness = 0f,
                    speakingTime = 0f,
                    isSpeaking = false,
                    lastStateChangeTime = 0f,
                    lockedMouthLayer = null
                };
                speakingStates[defName] = state;
            }
            
            // 📌 2. 同步当前表情（从 ExpressionSystem）
            var expressionState = ExpressionSystem.GetExpressionState(defName);
            state.currentExpression = expressionState.CurrentExpression;
            
            // 📌 3. 检查 TTS 播放状态（修改：使用 try-catch 避免崩溃）
            bool isPlayingTTS = false;
            try
            {
                isPlayingTTS = TTS.TTSAudioPlayer.IsSpeaking(defName);
            }
            catch (System.Exception ex)
            {
                if (Prefs.DevMode)
                {
                    Log.Warning($"[MouthAnimationSystem] 检测 TTS 状态失败: {ex.Message}");
                }
                isPlayingTTS = false;
            }
            
            // 📌 4. 计算目标嘴部开合度
            float targetOpenness = 0f;
            
            if (isPlayingTTS)
            {
                // 📌 TTS 播放中：动态张嘴（使用正弦波模拟说话）
                state.isSpeaking = true;
                state.speakingTime += Time.deltaTime;
                
                // 使用正弦波在 0 到 0.8 的开合度
                float sineWave = Mathf.Sin(state.speakingTime * 10f); // 10Hz 频率
                targetOpenness = Mathf.Lerp(0f, 0.8f, (sineWave + 1f) * 0.5f);
                
                // ✅ 保存原始目标值（用于突变检测）
                state.targetRawOpenness = targetOpenness;
                
                // 📌 调试日志：TTS 播放时输出
                if (Prefs.DevMode && state.speakingTime % 1f < 0.1f) // 每秒输出一次
                {
                    Log.Message($"[MouthAnimationSystem] {defName} TTS播放中 - 原始开合度: {targetOpenness:F2}, 平滑后: {state.currentOpenness:F2}");
                }
            }
            else
            {
                // ✅ v1.6.60: TTS 停止后立即重置开合度（不使用平滑过渡）
                if (state.isSpeaking)
                {
                    // 刚刚停止说话，立即重置
                    state.isSpeaking = false;
                    state.speakingTime = 0f;
                    state.currentOpenness = 0f;  // ✅ 关键修复：立即重置为 0
                    state.targetRawOpenness = 0f;
                    state.lockedMouthLayer = null; // ✅ 清除锁定
                    
                    if (Prefs.DevMode)
                    {
                        Log.Message($"[MouthAnimationSystem] {defName} TTS停止 - 立即闭嘴");
                    }
                }
                
                // 根据表情获取静态开合度
                targetOpenness = GetMouthOpennessForExpression(state.currentExpression);
                state.targetRawOpenness = targetOpenness;
            }
            
            // ✅ 5. 平滑阻尼算法（Smoothing Damping）
            if (isPlayingTTS)
            {
                // ✅ 检测突变（大声说话时）
                float opennessDelta = Mathf.Abs(state.targetRawOpenness - state.currentOpenness);
                bool isSuddenChange = opennessDelta > SUDDEN_CHANGE_THRESHOLD;
                
                if (isSuddenChange)
                {
                    // 突变：快速跟随（用于捕捉大声说话的瞬间）
                    state.currentOpenness = Mathf.Lerp(state.currentOpenness, state.targetRawOpenness, SMOOTHING_FACTOR * 3f);
                }
                else
                {
                    // 正常平滑过渡（避免机械跳动）
                    state.currentOpenness = Mathf.Lerp(state.currentOpenness, state.targetRawOpenness, SMOOTHING_FACTOR);
                }
            }
            else
            {
                // TTS 停止后：直接设置为目标值（避免延迟）
                state.currentOpenness = targetOpenness;
            }
            
            // ✅ 6. 应用闭嘴阈值（过滤噪音）
            if (state.currentOpenness < SILENCE_THRESHOLD)
            {
                state.currentOpenness = 0f; // 强制闭嘴
            }
            
            // ✅ 7. 根据开合度返回对应的嘴部图层名称
            string layerName = GetMouthShapeLayerName(state.currentExpression, state.currentOpenness);
            
            // ✅ 8. 最小保持时间机制（防止高频闪烁）
            float currentTime = Time.time;
            bool canChange = (currentTime - state.lastStateChangeTime) >= MIN_HOLD_TIME;
            
            // 如果在保持期间且不是突变，继续使用锁定的嘴型
            if (!canChange && state.lockedMouthLayer != null)
            {
                // 除非是突变（大声说话），否则保持当前嘴型
                float opennessDelta = Mathf.Abs(state.targetRawOpenness - state.currentOpenness);
                if (opennessDelta < SUDDEN_CHANGE_THRESHOLD)
                {
                    layerName = state.lockedMouthLayer;
                }
            }
            
            // 如果嘴型发生变化，更新锁定状态
            if (layerName != state.lockedMouthLayer)
            {
                state.lockedMouthLayer = layerName;
                state.lastStateChangeTime = currentTime;
                
                // 📌 调试日志：每次切换嘴型时输出
                if (Prefs.DevMode)
                {
                    Log.Message($"[MouthAnimationSystem] {defName} 嘴型切换: {layerName ?? "null"} (表情={state.currentExpression}, 开合度={state.currentOpenness:F2}, TTS={isPlayingTTS})");
                }
            }
            
            return layerName;
        }
        
        /// <summary>
        /// 📌 v1.6.44: 根据表情获取静态嘴型开合度
        /// </summary>
        private static float GetMouthOpennessForExpression(ExpressionType expression)
        {
            return expression switch
            {
                ExpressionType.Happy => 0.5f,      // 微笑：中等开合
                ExpressionType.Surprised => 0.8f,  // 惊讶：大张
                ExpressionType.Smug => 0.3f,       // 得意：略微上扬
                ExpressionType.Angry => 0.4f,      // 生气：紧绷
                ExpressionType.Sad => 0.2f,        // 悲伤：微微下撇
                ExpressionType.Confused => 0.2f,   // 困惑：小开口
                ExpressionType.Shy => 0.1f,        // 害羞：几乎闭嘴
                ExpressionType.Neutral => 0f,      // 平静：闭嘴
                _ => 0f
            };
        }
        
        /// <summary>
        /// 📌 v1.6.44: 根据表情和开合度返回嘴型图层名称
        /// </summary>
        private static string GetMouthShapeLayerName(ExpressionType expression, float openness)
        {
            // ✅ 使用更高的阈值（过滤噪音）
            if (openness < SILENCE_THRESHOLD)
            {
                return null; // 闭嘴（使用 base_body 的默认嘴型）
            }
            
            // 根据开合度选择嘴型
            if (openness < 0.3f)
            {
                return "small_mouth";
            }
            else if (openness < 0.6f)
            {
                return "medium_mouth";
            }
            else
            {
                return "larger_mouth";
            }
        }
        
        /// <summary>
        /// 📌 清除指定人格的状态
        /// </summary>
        public static void ClearState(string defName)
        {
            speakingStates.Remove(defName);
        }
        
        /// <summary>
        /// 📌 清除所有状态
        /// </summary>
        public static void ClearAllStates()
        {
            speakingStates.Clear();
        }
        
        /// <summary>
        /// ✅ v1.6.65: 立即停止所有嘴部动画
        /// </summary>
        public static void StopAnimation()
        {
            // 强制所有人格立即闭嘴
            foreach (var state in speakingStates.Values)
            {
                state.isSpeaking = false;
                state.speakingTime = 0f;
                state.currentOpenness = 0f;
                state.targetRawOpenness = 0f;
                state.lockedMouthLayer = null;
            }
            
            if (Prefs.DevMode)
            {
                Log.Message("[MouthAnimationSystem] All mouth animations stopped");
            }
        }
    }
}
