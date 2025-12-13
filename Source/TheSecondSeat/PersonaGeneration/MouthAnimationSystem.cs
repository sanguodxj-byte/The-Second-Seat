using System;
using System.Collections.Generic;
using Verse;
using UnityEngine;

namespace TheSecondSeat.PersonaGeneration
{
    /// <summary>
    /// ? v1.6.18: 张嘴动画系统（口型同步）
    /// ? v1.6.36: 集成 TTSAudioPlayer 状态，实现真正的口型同步
    /// 
    /// 功能：
    /// - TTS播放时自动张嘴
    /// - 3种嘴型随机切换（small_mouth, medium_mouth, larger_mouth）
    /// - 避免连续重复相同嘴型
    /// 
    /// 使用：
    /// 1. Update() 检测 TTSAudioPlayer.IsSpeaking(defName)
    /// 2. GetMouthLayerName(defName) 返回当前嘴型图层名
    /// </summary>
    public static class MouthAnimationSystem
    {
        // ===== 配置参数 =====
        private const float MOUTH_CHANGE_INTERVAL = 0.15f;  // 每0.15秒切换一次嘴型
        
        // ===== 说话状态数据 =====
        private class SpeakingState
        {
            public bool IsSpeaking;                      // 是否正在说话
            public string CurrentMouthLayer;             // 当前嘴型层名称
            public string LastMouthLayer;                // 上一次的嘴型层名称（避免重复）
            public float LastChangeTime;                 // 上次切换时间
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
        /// ? v1.6.36: 每帧更新（检测TTS播放状态并触发张嘴动画）
        /// </summary>
        /// <param name="deltaTime">增量时间</param>
        public static void Update(float deltaTime)
        {
            // ? 遍历所有人格，检测TTS播放状态
            var allPersonas = DefDatabase<NarratorPersonaDef>.AllDefsListForReading;
            
            foreach (var persona in allPersonas)
            {
                if (persona == null || string.IsNullOrEmpty(persona.defName))
                {
                    continue;
                }
                
                // ? 检测TTS是否正在播放该人格的音频
                bool isCurrentlySpeaking = TTS.TTSAudioPlayer.IsSpeaking(persona.defName);
                
                // 获取或创建状态
                if (!speakingStates.TryGetValue(persona.defName, out var state))
                {
                    state = new SpeakingState
                    {
                        IsSpeaking = false,
                        CurrentMouthLayer = null,
                        LastMouthLayer = null,
                        LastChangeTime = 0f
                    };
                    speakingStates[persona.defName] = state;
                }
                
                // ? 状态变化检测
                if (isCurrentlySpeaking && !state.IsSpeaking)
                {
                    // ? 开始说话 → 启动张嘴动画
                    StartSpeaking(persona.defName);
                }
                else if (!isCurrentlySpeaking && state.IsSpeaking)
                {
                    // ? 停止说话 → 停止张嘴动画
                    StopSpeaking(persona.defName);
                }
                else if (isCurrentlySpeaking)
                {
                    // ? 正在说话 → 更新嘴型动画
                    UpdateMouthAnimation(persona.defName, deltaTime);
                }
            }
        }
        
        /// <summary>
        /// ? 启动说话状态（TTS播放开始时自动调用）
        /// </summary>
        private static void StartSpeaking(string defName)
        {
            if (!speakingStates.TryGetValue(defName, out var state))
            {
                state = new SpeakingState();
                speakingStates[defName] = state;
            }
            
            state.IsSpeaking = true;
            state.CurrentMouthLayer = GetRandomMouthLayer(state.LastMouthLayer);
            state.LastChangeTime = Time.realtimeSinceStartup;
            
            if (Prefs.DevMode)
            {
                Log.Message($"[MouthAnimationSystem] {defName} 开始说话: {state.CurrentMouthLayer}");
            }
        }
        
        /// <summary>
        /// ? 停止说话状态（TTS播放结束时自动调用）
        /// </summary>
        private static void StopSpeaking(string defName)
        {
            if (speakingStates.TryGetValue(defName, out var state))
            {
                state.IsSpeaking = false;
                state.CurrentMouthLayer = null;
                state.LastMouthLayer = null;
                
                if (Prefs.DevMode)
                {
                    Log.Message($"[MouthAnimationSystem] {defName} 停止说话");
                }
            }
        }
        
        /// <summary>
        /// ? 更新嘴型动画（说话过程中每帧调用）
        /// </summary>
        private static void UpdateMouthAnimation(string defName, float deltaTime)
        {
            if (!speakingStates.TryGetValue(defName, out var state))
            {
                return;
            }
            
            float currentTime = Time.realtimeSinceStartup;
            float elapsed = currentTime - state.LastChangeTime;
            
            // ? 每 MOUTH_CHANGE_INTERVAL 切换一次嘴型
            if (elapsed >= MOUTH_CHANGE_INTERVAL)
            {
                state.LastMouthLayer = state.CurrentMouthLayer;
                state.CurrentMouthLayer = GetRandomMouthLayer(state.LastMouthLayer);
                state.LastChangeTime = currentTime;
                
                if (Prefs.DevMode)
                {
                    Log.Message($"[MouthAnimationSystem] {defName} 切换嘴型: {state.CurrentMouthLayer}");
                }
            }
        }
        
        /// <summary>
        /// ? 获取当前嘴巴图层名称（供 LayeredPortraitCompositor 调用）
        /// </summary>
        /// <param name="defName">人格 DefName</param>
        /// <returns>嘴巴图层名称，如果不在说话则返回 null</returns>
        public static string GetMouthLayerName(string defName)
        {
            if (string.IsNullOrEmpty(defName))
            {
                return null;
            }
            
            if (speakingStates.TryGetValue(defName, out var state) && state.IsSpeaking)
            {
                return state.CurrentMouthLayer;
            }
            
            // ? 不在说话时返回 null（使用 base_body 的闭嘴）
            return null;
        }
        
        /// <summary>
        /// ? 获取随机嘴型（避免连续重复）
        /// </summary>
        private static string GetRandomMouthLayer(string lastLayer)
        {
            if (mouthLayers.Length == 1)
            {
                return mouthLayers[0];
            }
            
            string newLayer;
            int attempts = 0;
            const int maxAttempts = 10;
            
            do
            {
                newLayer = mouthLayers[UnityEngine.Random.Range(0, mouthLayers.Length)];
                attempts++;
            }
            while (newLayer == lastLayer && attempts < maxAttempts);
            
            return newLayer;
        }
        
        /// <summary>
        /// ? 清除指定人格的状态
        /// </summary>
        public static void ClearState(string defName)
        {
            speakingStates.Remove(defName);
        }
        
        /// <summary>
        /// ? 清除所有状态
        /// </summary>
        public static void ClearAllStates()
        {
            speakingStates.Clear();
        }
    }
}
