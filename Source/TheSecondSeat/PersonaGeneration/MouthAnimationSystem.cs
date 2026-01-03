using System;
using System.Collections.Generic;
using Verse;
using UnityEngine;

namespace TheSecondSeat.PersonaGeneration
{
    /// <summary>
    /// 📌 v1.6.74: 张嘴动画系统（真实音素口型同步）
    /// ⭐ 新功能：
    /// - 支持音素到 Viseme 的精确映射（IPA、ARPABET、中文拼音）
    /// - 集成 Azure TTS Viseme 事件（如果可用）
    /// - 平滑的 Viseme 过渡动画
    /// 
    /// 功能：
    /// - TTS播放时自动张嘴
    /// - 6种口型精确切换（Closed, Small, Medium, Large, Smile, OShape）
    /// - 平滑阻尼算法（避免机械跳动）
    /// - 最小保持时间（防止高频闪烁）
    /// 
    /// 使用：
    /// 1. Update() 检测 TTSAudioPlayer.IsSpeaking(defName)
    /// 2. GetMouthLayerName(defName) 返回当前嘴型图层名
    /// </summary>
    public static class MouthAnimationSystem
    {
        // ===== 配置参数 =====
        
        /// <summary>
        /// ✅ v1.6.74: 平滑因子（Viseme 过渡速度）
        /// </summary>
        private const float SMOOTHING_FACTOR = 0.15f;
        
        /// <summary>
        /// ✅ v1.6.74: 最小保持时间（秒）
        /// </summary>
        private const float MIN_HOLD_TIME = 0.12f;
        
        /// <summary>
        /// ✅ v1.6.74: Viseme 切换间隔（秒）
        /// </summary>
        private const float VISEME_CHANGE_INTERVAL = 0.1f;
        
        /// <summary>
        /// ⭐ v1.6.74: 是否启用音素映射模式（默认 false，使用开合度模拟）
        /// TODO: 集成 TTS 音素数据后设为 true
        /// </summary>
        public static bool EnablePhonemeMode { get; set; } = false;
        
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
            
            // ⭐ v1.6.74: 新增音素相关字段
            public VisemeCode currentViseme;             // 当前 Viseme 编码
            public VisemeCode targetViseme;              // 目标 Viseme 编码
            public Queue<VisemeCode> visemeQueue;        // Viseme 序列队列（来自 TTS）
            public float visemeTransitionProgress;       // Viseme 过渡进度（0-1）
        }
        
        // ===== 数据存储 =====
        private static readonly Dictionary<string, SpeakingState> speakingStates = new Dictionary<string, SpeakingState>();
        
        /// <summary>
        /// 📌 v1.6.36: 每帧更新（检测TTS播放状态并触发张嘴动画）
        /// </summary>
        /// <param name="deltaTime">增量时间</param>
        public static void Update(float deltaTime)
        {
            // 遍历所有人格，检测TTS播放状态
            var allPersonas = DefDatabase<NarratorPersonaDef>.AllDefsListForReading;
            
            foreach (var persona in allPersonas)
            {
                if (persona == null || string.IsNullOrEmpty(persona.defName))
                {
                    continue;
                }
                
                // 检测TTS是否正在播放该人格的音频
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
                        lockedMouthLayer = null,
                        currentViseme = VisemeCode.Closed,
                        targetViseme = VisemeCode.Closed,
                        visemeQueue = new Queue<VisemeCode>(),
                        visemeTransitionProgress = 1f
                    };
                    speakingStates[persona.defName] = state;
                }
                
                // 更新状态
                state.IsSpeaking = isCurrentlySpeaking;
                
                // 日志已静默：TTS说话状态变化
            }
        }
        
        /// <summary>
        /// ⭐ v1.6.74: 获取当前嘴巴图层名称（供 LayeredPortraitCompositor 调用）
        /// 支持两种模式：
        /// 1. 音素模式（EnablePhonemeMode=true）：使用 TTS 音素数据
        /// 2. 模拟模式（EnablePhonemeMode=false）：使用正弦波开合度
        /// </summary>
        /// <param name="defName">人格 DefName</param>
        /// <returns>嘴部图层名称（如果不需要则返回 null）</returns>
        public static string GetMouthLayerName(string defName)
        {
            if (string.IsNullOrEmpty(defName))
            {
                return null;
            }
            
            // 获取或创建状态
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
                    lockedMouthLayer = null,
                    currentViseme = VisemeCode.Closed,
                    targetViseme = VisemeCode.Closed,
                    visemeQueue = new Queue<VisemeCode>(),
                    visemeTransitionProgress = 1f
                };
                speakingStates[defName] = state;
            }
            
            // 同步当前表情（从 ExpressionSystem）
            var expressionState = ExpressionSystem.GetExpressionState(defName);
            state.currentExpression = expressionState.CurrentExpression;
            
            // 检查 TTS 播放状态
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
            
            // ⭐ v1.6.74: 根据模式选择处理方式
            if (EnablePhonemeMode)
            {
                // 音素模式：使用 TTS 音素数据（精确口型）
                return GetMouthLayerNameFromPhoneme(state, defName, isPlayingTTS);
            }
            else
            {
                // 模拟模式：使用正弦波开合度（旧版逻辑）
                return GetMouthLayerNameFromOpenness(state, defName, isPlayingTTS);
            }
        }
        
        /// <summary>
        /// ⭐ v1.6.74: 音素模式 - 从 TTS 音素数据获取嘴型
        /// ⭐ v1.7.6: 添加 visemeQueue null 安全检查
        /// </summary>
        private static string GetMouthLayerNameFromPhoneme(SpeakingState state, string defName, bool isPlayingTTS)
        {
            // ⭐ v1.7.6: 确保 visemeQueue 不为 null
            if (state.visemeQueue == null)
            {
                state.visemeQueue = new Queue<VisemeCode>();
            }
            
            if (!isPlayingTTS)
            {
                // TTS 停止后立即闭嘴
                if (state.isSpeaking)
                {
                    state.isSpeaking = false;
                    state.currentViseme = VisemeCode.Closed;
                    state.targetViseme = VisemeCode.Closed;
                    state.visemeQueue.Clear();
                    state.lockedMouthLayer = null;
                    
                    // 日志已静默：TTS停止（音素模式）
                }
                
                return null; // 闭嘴
            }
            
            // TTS 播放中
            state.isSpeaking = true;
            
            // TODO: 从 TTSAudioPlayer 获取当前音素
            // 当前占位符：使用队列中的下一个 Viseme
            if (state.visemeQueue.Count > 0 && Time.time - state.lastStateChangeTime >= VISEME_CHANGE_INTERVAL)
            {
                state.targetViseme = state.visemeQueue.Dequeue();
                state.visemeTransitionProgress = 0f;
                state.lastStateChangeTime = Time.time;
            }
            
            // 平滑过渡到目标 Viseme
            if (state.currentViseme != state.targetViseme)
            {
                state.visemeTransitionProgress += Time.deltaTime / 0.05f; // 50ms 过渡时间
                state.visemeTransitionProgress = Mathf.Clamp01(state.visemeTransitionProgress);
                
                if (state.visemeTransitionProgress >= 1f)
                {
                    state.currentViseme = state.targetViseme;
                }
            }
            
            // 返回对应的纹理名称
            string layerName = VisemeHelper.VisemeToTextureName(state.currentViseme);
            
            if (layerName != state.lockedMouthLayer)
            {
                state.lockedMouthLayer = layerName;
                // 日志已静默：Viseme切换
            }
            
            return layerName;
        }
        
        /// <summary>
        /// ⭐ v1.6.74: 模拟模式 - 使用正弦波开合度（旧版逻辑，保留为备用）
        /// </summary>
        private static string GetMouthLayerNameFromOpenness(SpeakingState state, string defName, bool isPlayingTTS)
        {
            // 计算目标嘴部开合度
            float targetOpenness = 0f;
            
            if (isPlayingTTS)
            {
                // TTS 播放中：动态张嘴（使用正弦波模拟说话）
                state.isSpeaking = true;
                state.speakingTime += Time.deltaTime;
                
                // 使用正弦波在 0 到 0.8 的开合度
                float sineWave = Mathf.Sin(state.speakingTime * 10f); // 10Hz 频率
                targetOpenness = Mathf.Lerp(0f, 0.8f, (sineWave + 1f) * 0.5f);
                
                state.targetRawOpenness = targetOpenness;
                
                // 日志已静默：TTS播放时的开合度
            }
            else
            {
                // TTS 停止后立即重置开合度
                if (state.isSpeaking)
                {
                    state.isSpeaking = false;
                    state.speakingTime = 0f;
                    state.currentOpenness = 0f;
                    state.targetRawOpenness = 0f;
                    state.lockedMouthLayer = null;
                    
                    // 日志已静默：TTS停止（模拟模式）
                }
                
                // 根据表情获取静态开合度
                targetOpenness = GetMouthOpennessForExpression(state.currentExpression);
                state.targetRawOpenness = targetOpenness;
            }
            
            // 平滑阻尼算法
            if (isPlayingTTS)
            {
                // 检测突变（大声说话时）
                float opennessDelta = Mathf.Abs(state.targetRawOpenness - state.currentOpenness);
                bool isSuddenChange = opennessDelta > 0.5f;
                
                if (isSuddenChange)
                {
                    // 突变：快速跟随
                    state.currentOpenness = Mathf.Lerp(state.currentOpenness, state.targetRawOpenness, SMOOTHING_FACTOR * 3f);
                }
                else
                {
                    // 正常平滑过渡
                    state.currentOpenness = Mathf.Lerp(state.currentOpenness, state.targetRawOpenness, SMOOTHING_FACTOR);
                }
            }
            else
            {
                // TTS 停止后：直接设置为目标值
                state.currentOpenness = targetOpenness;
            }
            
            // 应用闭嘴阈值（过滤噪音）
            if (state.currentOpenness < 0.05f)
            {
                state.currentOpenness = 0f;
            }
            
            // ⭐ v1.6.74: 从开合度转换为 Viseme 编码
            VisemeCode viseme = VisemeHelper.OpennessToViseme(state.currentOpenness);
            
            // 根据 Viseme 返回对应的嘴部图层名称
            string layerName = VisemeHelper.VisemeToTextureName(viseme);
            
            // 最小保持时间机制
            float currentTime = Time.time;
            bool canChange = (currentTime - state.lastStateChangeTime) >= MIN_HOLD_TIME;
            
            if (!canChange && state.lockedMouthLayer != null)
            {
                // 除非是突变，否则保持当前嘴型
                float opennessDelta = Mathf.Abs(state.targetRawOpenness - state.currentOpenness);
                if (opennessDelta < 0.5f)
                {
                    layerName = state.lockedMouthLayer;
                }
            }
            
            // 如果嘴型发生变化，更新锁定状态
            if (layerName != state.lockedMouthLayer)
            {
                state.lockedMouthLayer = layerName;
                state.lastStateChangeTime = currentTime;
                // 日志已静默：嘴型切换
            }
            
            return layerName;
        }
        
        /// <summary>
        /// ⭐ v1.6.74: 【新增】从 TTS 推送 Viseme 序列
        /// 用于集成 Azure TTS Viseme 事件
        /// </summary>
        /// <param name="defName">人格 DefName</param>
        /// <param name="visemes">Viseme 序列（按时间顺序）</param>
        public static void PushVisemeSequence(string defName, List<VisemeCode> visemes)
        {
            if (!speakingStates.TryGetValue(defName, out var state))
            {
                return;
            }
            
            state.visemeQueue.Clear();
            foreach (var viseme in visemes)
            {
                state.visemeQueue.Enqueue(viseme);
            }
            
            // 日志已静默：Viseme序列接收
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
        /// ⭐ v1.7.6: 添加 visemeQueue null 安全检查
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
                state.currentViseme = VisemeCode.Closed;
                state.targetViseme = VisemeCode.Closed;
                // ⭐ v1.7.6: null 安全检查
                if (state.visemeQueue != null)
                {
                    state.visemeQueue.Clear();
                }
                else
                {
                    state.visemeQueue = new Queue<VisemeCode>();
                }
            }
            
            // 日志已静默：所有嘴部动画停止
        }
    }
}
