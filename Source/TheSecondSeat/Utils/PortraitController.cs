using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using TheSecondSeat.PersonaGeneration;
using TheSecondSeat.TTS;

namespace TheSecondSeat.Utils
{
    /// <summary>
    /// 立绘控制器 - 管理整图切换动画系统
    /// 负责缓存纹理、眨眼逻辑、语音同步
    /// </summary>
    public class PortraitController
    {
        // ========== 单例模式 ==========
        private static PortraitController? instance;
        
        /// <summary>
        /// 单例实例
        /// </summary>
        public static PortraitController Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new PortraitController();
                    Log.Message("[PortraitController] Singleton instance created");
                }
                return instance;
            }
        }
        
        // ========== 纹理缓存 ==========
        /// <summary>
        /// 纹理缓存：key = "defName_type" (例如 "YourPersona_base", "YourPersona_blink")
        /// </summary>
        private Dictionary<string, Texture2D> textureCache = new Dictionary<string, Texture2D>();
        
        // ========== 眨眼状态 ==========
        /// <summary>
        /// 下次眨眼时间（游戏时间戳）
        /// </summary>
        private float nextBlinkTime = 0f;
        
        /// <summary>
        /// 是否正在眨眼
        /// </summary>
        private bool isBlinking = false;
        
        /// <summary>
        /// 眨眼开始时间
        /// </summary>
        private float blinkStartTime = 0f;
        
        /// <summary>
        /// 眨眼持续时间（秒）
        /// </summary>
        private const float BLINK_DURATION = 0.15f;
        
        /// <summary>
        /// 眨眼间隔范围（秒）
        /// </summary>
        private const float BLINK_INTERVAL_MIN = 3f;
        private const float BLINK_INTERVAL_MAX = 6f;
        
        // ========== 构造函数 ==========
        private PortraitController()
        {
            // 初始化下次眨眼时间
            ScheduleNextBlink();
        }
        
        // ========== 主要方法：获取当前立绘 ==========
        
        /// <summary>
        /// 获取当前应显示的立绘纹理
        /// 优先级：眨眼 > 说话 > 默认
        /// </summary>
        /// <param name="def">人格定义</param>
        /// <returns>当前应显示的纹理</returns>
        public Texture2D GetCurrentPortrait(NarratorPersonaDef def)
        {
            if (def == null)
            {
                Log.Warning("[PortraitController] NarratorPersonaDef is null");
                return GenerateFallbackTexture();
            }
            
            // 更新眨眼状态
            UpdateBlinkState();
            
            // Step 1: 解析并缓存纹理
            Texture2D baseTexture = GetOrLoadTexture(def.defName, "base", def.portraitPath);
            Texture2D blinkTexture = GetOrLoadTexture(def.defName, "blink", def.portraitPathBlink);
            Texture2D speakingTexture = GetOrLoadTexture(def.defName, "speaking", def.portraitPathSpeaking);
            
            // Step 2: 优先级逻辑
            
            // 优先级 1: 眨眼（最高优先级）
            if (isBlinking)
            {
                if (blinkTexture != null)
                {
                    if (Prefs.DevMode)
                    {
                        // Log.Message($"[PortraitController] {def.defName}: Showing BLINK texture");
                    }
                    return blinkTexture;
                }
                // 回退到基础纹理
                if (Prefs.DevMode)
                {
                    Log.Warning($"[PortraitController] {def.defName}: Blink texture missing, fallback to base");
                }
            }
            
            // 优先级 2: 说话
            if (TTSService.Instance != null && TTSService.Instance.IsSpeaking)
            {
                if (speakingTexture != null)
                {
                    if (Prefs.DevMode)
                    {
                        // Log.Message($"[PortraitController] {def.defName}: Showing SPEAKING texture");
                    }
                    return speakingTexture;
                }
                // 回退到基础纹理
                if (Prefs.DevMode)
                {
                    Log.Warning($"[PortraitController] {def.defName}: Speaking texture missing, fallback to base");
                }
            }
            
            // 优先级 3: 默认（基础纹理）
            if (baseTexture != null)
            {
                return baseTexture;
            }
            
            // 所有纹理都缺失，生成占位符
            Log.Warning($"[PortraitController] {def.defName}: All textures missing, using fallback");
            return GenerateFallbackTexture();
        }
        
        // ========== 纹理加载与缓存 ==========
        
        /// <summary>
        /// 获取或加载纹理（带缓存）
        /// </summary>
        /// <param name="defName">人格 defName</param>
        /// <param name="type">纹理类型（base, blink, speaking）</param>
        /// <param name="texturePath">纹理路径（来自 XML）</param>
        /// <returns>纹理对象，如果不存在返回 null</returns>
        private Texture2D? GetOrLoadTexture(string defName, string type, string texturePath)
        {
            // 检查路径是否为空
            if (string.IsNullOrEmpty(texturePath))
            {
                return null;
            }
            
            // 生成缓存键
            string cacheKey = $"{defName}_{type}";
            
            // 检查缓存
            if (textureCache.TryGetValue(cacheKey, out Texture2D cachedTexture))
            {
                return cachedTexture;
            }
            
            // 从磁盘加载
            try
            {
                Texture2D texture = ContentFinder<Texture2D>.Get(texturePath, false);
                
                if (texture != null)
                {
                    // 设置纹理质量
                    SetTextureQuality(texture);
                    
                    // 缓存
                    textureCache[cacheKey] = texture;
                    
                    if (Prefs.DevMode)
                    {
                        Log.Message($"[PortraitController] Loaded and cached texture: {cacheKey} from {texturePath}");
                    }
                    
                    return texture;
                }
                else
                {
                    if (Prefs.DevMode)
                    {
                        Log.Warning($"[PortraitController] Texture not found: {texturePath}");
                    }
                    return null;
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[PortraitController] Failed to load texture {texturePath}: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// 设置纹理质量参数
        /// </summary>
        private void SetTextureQuality(Texture2D texture)
        {
            if (texture == null) return;
            
            try
            {
                texture.filterMode = FilterMode.Bilinear;
                texture.anisoLevel = 4;
            }
            catch
            {
                // 静默忽略，纹理设置不是关键功能
            }
        }
        
        // ========== 眨眼逻辑 ==========
        
        /// <summary>
        /// 更新眨眼状态
        /// </summary>
        private void UpdateBlinkState()
        {
            float currentTime = Time.realtimeSinceStartup;
            
            if (isBlinking)
            {
                // 正在眨眼：检查是否结束
                float elapsedTime = currentTime - blinkStartTime;
                
                if (elapsedTime >= BLINK_DURATION)
                {
                    // 眨眼结束
                    isBlinking = false;
                    ScheduleNextBlink();
                    
                    if (Prefs.DevMode)
                    {
                        Log.Message($"[PortraitController] Blink ended, next blink at {nextBlinkTime:F2}");
                    }
                }
            }
            else
            {
                // 未眨眼：检查是否应该开始
                if (currentTime >= nextBlinkTime)
                {
                    StartBlink();
                }
            }
        }
        
        /// <summary>
        /// 开始眨眼
        /// </summary>
        private void StartBlink()
        {
            isBlinking = true;
            blinkStartTime = Time.realtimeSinceStartup;
            
            if (Prefs.DevMode)
            {
                Log.Message($"[PortraitController] Blink started at {blinkStartTime:F2}");
            }
        }
        
        /// <summary>
        /// 计划下次眨眼时间（随机3-6秒后）
        /// </summary>
        private void ScheduleNextBlink()
        {
            float interval = UnityEngine.Random.Range(BLINK_INTERVAL_MIN, BLINK_INTERVAL_MAX);
            nextBlinkTime = Time.realtimeSinceStartup + interval;
            
            if (Prefs.DevMode)
            {
                Log.Message($"[PortraitController] Next blink scheduled in {interval:F2}s (at {nextBlinkTime:F2})");
            }
        }
        
        // ========== 缓存管理 ==========
        
        /// <summary>
        /// 清空所有纹理缓存
        /// </summary>
        public void ClearCache()
        {
            textureCache.Clear();
            Log.Message("[PortraitController] Texture cache cleared");
        }
        
        /// <summary>
        /// 清除特定人格的纹理缓存
        /// </summary>
        /// <param name="defName">人格 defName</param>
        public void ClearCacheFor(string defName)
        {
            List<string> keysToRemove = new List<string>();
            
            foreach (var key in textureCache.Keys)
            {
                if (key.StartsWith(defName + "_"))
                {
                    keysToRemove.Add(key);
                }
            }
            
            foreach (var key in keysToRemove)
            {
                textureCache.Remove(key);
            }
            
            Log.Message($"[PortraitController] Cleared cache for {defName}: {keysToRemove.Count} textures removed");
        }
        
        /// <summary>
        /// 获取缓存统计信息
        /// </summary>
        public string GetCacheInfo()
        {
            return $"[PortraitController] Cached textures: {textureCache.Count}";
        }
        
        // ========== 占位符纹理 ==========
        
        /// <summary>
        /// 生成占位符纹理（当所有纹理都缺失时使用）
        /// </summary>
        private Texture2D GenerateFallbackTexture()
        {
            // 创建简单的灰色纹理
            Texture2D fallback = new Texture2D(512, 512, TextureFormat.RGBA32, false);
            Color gray = new Color(0.3f, 0.3f, 0.3f, 1f);
            
            for (int y = 0; y < 512; y++)
            {
                for (int x = 0; x < 512; x++)
                {
                    fallback.SetPixel(x, y, gray);
                }
            }
            
            fallback.Apply();
            return fallback;
        }
        
        // ========== 调试方法 ==========
        
        /// <summary>
        /// 强制触发眨眼（用于测试）
        /// </summary>
        public void ForceBlinkNow()
        {
            StartBlink();
            Log.Message("[PortraitController] Forced blink triggered");
        }
        
        /// <summary>
        /// 获取当前眨眼状态（用于调试）
        /// </summary>
        public string GetBlinkStatus()
        {
            float currentTime = Time.realtimeSinceStartup;
            
            if (isBlinking)
            {
                float elapsedTime = currentTime - blinkStartTime;
                float remainingTime = BLINK_DURATION - elapsedTime;
                return $"Blinking (remaining: {remainingTime:F2}s)";
            }
            else
            {
                float timeUntilBlink = nextBlinkTime - currentTime;
                return $"Idle (next blink in: {timeUntilBlink:F2}s)";
            }
        }
        
        /// <summary>
        /// 获取完整调试信息
        /// </summary>
        public string GetDebugInfo()
        {
            return $"[PortraitController Debug]\n" +
                   $"Cached textures: {textureCache.Count}\n" +
                   $"Blink status: {GetBlinkStatus()}\n" +
                   $"TTS Speaking: {(TTSService.Instance != null && TTSService.Instance.IsSpeaking ? "Yes" : "No")}";
        }
    }
}
