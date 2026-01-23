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
        /// ⭐ v1.12.1: 降低平滑因子以提高响应速度 (0.15f -> 0.05f)
        /// </summary>
        private const float SMOOTHING_FACTOR = 0.05f;
        
        /// <summary>
        /// ✅ v1.6.74: 最小保持时间（秒）
        /// ⭐ v1.12.1: 缩短保持时间以允许更快的口型变化 (0.12f -> 0.05f)
        /// </summary>
        private const float MIN_HOLD_TIME = 0.05f;
        
        /// <summary>
        /// ✅ v1.6.74: Viseme 切换间隔（秒）
        /// ⭐ v1.12.1: 缩短切换间隔 (0.1f -> 0.05f)
        /// </summary>
        private const float VISEME_CHANGE_INTERVAL = 0.05f;
        
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
            public bool usePhonemeMode;                  // ⭐ v1.8.6: 当前会话是否使用音素模式
            
            // ⭐ v1.9.0: 直接开合度支持 (Azure BlendShapes / 0-1 Value)
            public float externalOpenness;               // 外部输入的开合度
            public float lastExternalOpennessTime;       // 上次接收外部开合度的时间
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
                    visemeTransitionProgress = 1f,
                    usePhonemeMode = false,
                    externalOpenness = 0f,
                    lastExternalOpennessTime = 0f
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
        /// ⭐ v1.13.0: 添加详细诊断日志
        /// </summary>
        /// <param name="defName">人格 DefName</param>
        /// <returns>嘴部图层名称（如果不需要则返回 null）</returns>
        public static string GetMouthLayerName(string defName)
        {
            if (string.IsNullOrEmpty(defName))
            {
                return null;
            }
            
            // ⭐ v1.13.0: 诊断日志（每 120 帧输出一次）
            bool shouldLog = Prefs.DevMode && Time.frameCount % 120 == 0;
            
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
                    visemeTransitionProgress = 1f,
                    usePhonemeMode = false,
                    externalOpenness = 0f,
                    lastExternalOpennessTime = 0f
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
                
                // ⭐ v1.13.0: 检查是否有任何人在说话（用于诊断）
                bool anyoneSpeaking = TTS.TTSAudioPlayer.IsAnyoneSpeaking();
                string currentSpeaker = TTS.TTSAudioPlayer.GetCurrentSpeaker();
                
                if (shouldLog)
                {
                    // Log.Message($"[MouthAnim-DEBUG] defName={defName}, isPlayingTTS={isPlayingTTS}, anyoneSpeaking={anyoneSpeaking}, currentSpeaker={currentSpeaker}");
                }
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
            // ⭐ v1.8.6: 优先使用实例级别的标志，其次是全局标志
            string result;
            if (state.usePhonemeMode || EnablePhonemeMode)
            {
                // 音素模式：使用 TTS 音素数据（精确口型）
                result = GetMouthLayerNameFromPhoneme(state, defName, isPlayingTTS);
            }
            else
            {
                // 模拟模式：使用正弦波开合度（旧版逻辑）
                result = GetMouthLayerNameFromOpenness(state, defName, isPlayingTTS);
            }
            
            // ⭐ v1.13.0: 记录最终返回的嘴型
            if (shouldLog)
            {
                // Log.Message($"[MouthAnim-DEBUG] → returning: {result ?? "null"} (openness={state.currentOpenness:F2})");
            }
            
            return result;
        }
        
        /// <summary>
        /// ⭐ v1.6.74: 音素模式 - 从 TTS 音素数据获取嘴型
        /// ⭐ v1.7.6: 添加 visemeQueue null 安全检查
        /// ⭐ v1.11.0: 支持 RenderTreeDef 配置化纹理映射
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
                    state.usePhonemeMode = false; // ⭐ 重置模式
                    
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
            
            // ⭐ v1.11.0: 优先使用 RenderTreeDef 配置化映射
            string layerName;
            var renderTree = RenderTreeDefManager.GetRenderTree(defName);
            if (renderTree != null)
            {
                layerName = renderTree.GetVisemeTextureName(state.currentViseme);
            }
            else
            {
                // 回退到默认配置
                layerName = RenderTreeDefManager.GetDefault().GetVisemeTextureName(state.currentViseme);
            }
            
            if (layerName != state.lockedMouthLayer)
            {
                state.lockedMouthLayer = layerName;
                // ⭐ v1.8.3: 添加诊断日志
                // if (Prefs.DevMode)
                // {
                //     Log.Message($"[MouthAnimationSystem] 口型切换 (音素模式): {defName} → {layerName} (Viseme: {state.currentViseme})");
                // }
            }
            
            return layerName;
        }
        
        /// <summary>
        /// ⭐ v1.6.74: 模拟模式 - 使用音频频谱分析（FFT）计算开合度
        /// ⭐ v1.12.0: 简化 RMS 处理，降低阈值，确保嘴型能够响应
        /// </summary>
        private static string GetMouthLayerNameFromOpenness(SpeakingState state, string defName, bool isPlayingTTS)
        {
            // 计算目标嘴部开合度
            float targetOpenness = 0f;
            
            if (isPlayingTTS)
            {
                state.isSpeaking = true;
                state.speakingTime += Time.deltaTime;
                
                // ⭐ v1.9.0: 检查是否有外部输入的开合度 (Azure 0-1)
                if (Time.time - state.lastExternalOpennessTime < 0.2f)
                {
                    targetOpenness = state.externalOpenness;
                    state.targetRawOpenness = targetOpenness;
                }
                else
                {
                    // ⭐ v1.12.0: 获取音频 RMS
                    float rms = TTS.TTSAudioPlayer.GetAudioRMS(defName);
                    
                    // ⭐ v1.12.2: 移除 RMS 模拟，使用真实值
                    // 直接线性放大：RMS * 5 映射到开合度
                    float amplifiedRMS = rms * 5f;
                    targetOpenness = Mathf.Clamp01(amplifiedRMS);
                    
                    // 添加微小的随机扰动 (仅在有声音时)
                    if (targetOpenness > 0.01f)
                    {
                        float noise = (Mathf.PerlinNoise(Time.time * 20f, 0f) - 0.5f) * 0.1f;
                        targetOpenness = Mathf.Clamp01(targetOpenness + noise);
                    }

                    // 诊断日志 (当 RMS 异常低时警告)
                    if (rms < 0.0001f && state.speakingTime > 0.5f && Time.frameCount % 60 == 0)
                    {
                        Log.Warning($"[MouthAnimationSystem] RMS is effectively zero ({rms}) while speaking. AudioSource data might be missing.");
                    }
                    
                    state.targetRawOpenness = targetOpenness;
                }
                
                // ⭐ v1.12.0: 诊断日志
                if (Prefs.DevMode && Time.frameCount % 60 == 0)
                {
                    Log.Message($"[MouthAnim] {defName}: TTS=true, targetOpenness={targetOpenness:F3}, currentOpenness={state.currentOpenness:F3}");
                }
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
                
                // ⭐ v1.11.0: TTS停止时返回null，交由PortraitDrawer使用静态表情嘴型
                return null;
            }
            
            // 平滑阻尼算法
            // 检测突变（大声说话时）
            float opennessDelta = Mathf.Abs(state.targetRawOpenness - state.currentOpenness);
            // ⭐ v1.12.1: 降低突变判定阈值，提高响应灵敏度 (0.5f -> 0.2f)
            bool isSuddenChange = opennessDelta > 0.2f;
            
            if (isSuddenChange)
            {
                // 突变时加快响应 (20f -> 25f)
                state.currentOpenness = Mathf.Lerp(state.currentOpenness, state.targetRawOpenness, Time.deltaTime * 25f);
            }
            else
            {
                // 正常情况平滑过渡
                state.currentOpenness = Mathf.Lerp(state.currentOpenness, state.targetRawOpenness, Time.deltaTime / SMOOTHING_FACTOR);
            }
            
            // 应用闭嘴阈值（过滤噪音）
            if (state.currentOpenness < 0.05f)
            {
                state.currentOpenness = 0f;
            }
            
            // ⭐ v1.6.74: 从开合度转换为 Viseme 编码
            // ⭐ v1.11.0: 优先使用 RenderTreeDef 配置化映射
            VisemeCode viseme;
            string layerName;
            var renderTree = RenderTreeDefManager.GetRenderTree(defName);
            if (renderTree != null)
            {
                viseme = renderTree.GetVisemeFromOpenness(state.currentOpenness);
                layerName = renderTree.GetVisemeTextureName(viseme);
            }
            else
            {
                // 回退到默认配置
                var defaultDef = RenderTreeDefManager.GetDefault();
                viseme = defaultDef.GetVisemeFromOpenness(state.currentOpenness);
                layerName = defaultDef.GetVisemeTextureName(viseme);
            }
            
            // 最小保持时间机制
            float currentTime = Time.time;
            bool canChange = (currentTime - state.lastStateChangeTime) >= MIN_HOLD_TIME;
            
            if (!canChange && state.lockedMouthLayer != null)
            {
                // 除非是突变，否则保持当前嘴型
                float currentDelta = Mathf.Abs(state.targetRawOpenness - state.currentOpenness);
                if (currentDelta < 0.5f)
                {
                    layerName = state.lockedMouthLayer;
                }
            }
            
            // 如果嘴型发生变化，更新锁定状态
            if (layerName != state.lockedMouthLayer)
            {
                state.lockedMouthLayer = layerName;
                state.lastStateChangeTime = currentTime;
                // ⭐ v1.8.3: 添加诊断日志
                // if (Prefs.DevMode)
                // {
                //     Log.Message($"[MouthAnimationSystem] 口型切换 (模拟模式): {defName} → {layerName} (开合度: {state.currentOpenness:F2}, Viseme: {viseme})");
                // }
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
                // 如果状态不存在，尝试创建它（确保首次调用也能生效）
                state = new SpeakingState
                {
                    IsSpeaking = false,
                    visemeQueue = new Queue<VisemeCode>(),
                    currentViseme = VisemeCode.Closed,
                    targetViseme = VisemeCode.Closed
                };
                speakingStates[defName] = state;
            }
            
            // ⭐ v1.8.4: 收到 Viseme 数据时，自动启用音素模式
            // EnablePhonemeMode = true; // 不再修改全局标志
            state.usePhonemeMode = true; // ⭐ 仅修改当前实例
            
            state.visemeQueue.Clear();
            foreach (var viseme in visemes)
            {
                state.visemeQueue.Enqueue(viseme);
            }
            
            if (Prefs.DevMode)
            {
                Log.Message($"[MouthAnimationSystem] Received {visemes.Count} visemes for {defName}. EnablePhonemeMode set to true.");
            }
        }
        
        /// <summary>
        /// ⭐ v1.9.0: 【新增】推送直接开合度数据 (0-1)
        /// 用于 Azure BlendShapes 或其他精确的口型数据源
        /// </summary>
        /// <param name="defName">人格 DefName</param>
        /// <param name="openness">开合度 (0.0 - 1.0)</param>
        public static void PushOpenness(string defName, float openness)
        {
            if (!speakingStates.TryGetValue(defName, out var state))
            {
                state = new SpeakingState
                {
                    IsSpeaking = false,
                    visemeQueue = new Queue<VisemeCode>(),
                    currentViseme = VisemeCode.Closed,
                    targetViseme = VisemeCode.Closed,
                    usePhonemeMode = false // 开合度模式不使用音素逻辑
                };
                speakingStates[defName] = state;
            }
            
            state.externalOpenness = Mathf.Clamp01(openness);
            state.lastExternalOpennessTime = Time.time;
            
            // 确保不处于音素模式，以便 GetMouthLayerNameFromOpenness 被调用
            state.usePhonemeMode = false;
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
        /// ⭐ v1.9.8: 获取静态表情嘴部图层名（支持变体）
        /// 用于 TTS 未播放时，返回带变体的表情嘴型
        /// </summary>
        /// <param name="defName">人格 DefName</param>
        /// <returns>嘴部图层名称（如 happy1_mouth, sad2_mouth）</returns>
        public static string GetStaticMouthLayerName(string defName)
        {
            if (string.IsNullOrEmpty(defName)) return "Closed_mouth";
            
            var expressionState = ExpressionSystem.GetExpressionState(defName);
            if (expressionState == null) return "Closed_mouth";
            
            // 获取变体编号
            int variant = expressionState.Intensity > 0 ? expressionState.Intensity : expressionState.CurrentVariant;
            
            return GetStaticMouthLayerName(defName, expressionState.CurrentExpression, variant);
        }

        /// <summary>
        /// ⭐ v1.9.9: 获取指定表情的静态嘴型（重载）
        /// </summary>
        public static string GetStaticMouthLayerName(string defName, ExpressionType expression, int variant)
        {
            if (string.IsNullOrEmpty(defName)) return "Closed_mouth";
            
            // ⭐ v1.14.0: 移除 Neutral 强制闭嘴的限制，允许返回 neutral_mouth
            // 这样 PortraitDrawer 可以尝试加载 neutral_mouth，如果不存在则会自动回退到 Closed_mouth
            // if (expression == ExpressionType.Neutral)
            // {
            //     return "Closed_mouth";
            // }
            
            string exprName = expression.ToString().ToLower();
            
            if (variant <= 0)
            {
                // 基础嘴型：happy_mouth, sad_mouth 等
                return $"{exprName}_mouth";
            }
            
            // 变体嘴型：happy1_mouth, sad2_mouth 等
            return $"{exprName}{variant}_mouth";
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
                state.usePhonemeMode = false; // ⭐ 重置模式
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
