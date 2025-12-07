using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace TheSecondSeat.PersonaGeneration
{
    /// <summary>
    /// 智能裁剪系统 - 自动从完整立绘裁剪出头像和表情区域
    /// 功能：
    /// 1. 头像裁剪 - 提取上半身作为聊天窗口头像
    /// 2. 表情差分裁剪 - 提取面部区域减少内存占用
    /// 3. 智能定位 - 自动识别最佳裁剪区域
    /// 4. 缓存优化 - 避免重复裁剪
    /// </summary>
    public static class SmartCropper
    {
        // 裁剪缓存
        private static Dictionary<string, Texture2D> cropCache = new Dictionary<string, Texture2D>();
        
        /// <summary>
        /// 裁剪类型
        /// </summary>
        public enum CropType
        {
            Avatar,      // 头像（聊天窗口用）
            Expression,  // 表情差分（动态表情用）
            Portrait     // 完整立绘（不裁剪）
        }
        
        /// <summary>
        /// 从完整立绘裁剪指定区域
        /// </summary>
        /// <param name="sourceTexture">源纹理（完整立绘）</param>
        /// <param name="cropType">裁剪类型</param>
        /// <param name="customRect">自定义裁剪区域（可选）</param>
        /// <returns>裁剪后的纹理</returns>
        public static Texture2D CropTexture(Texture2D sourceTexture, CropType cropType, Rect? customRect = null)
        {
            if (sourceTexture == null)
            {
                Log.Warning("[SmartCropper] 源纹理为空，无法裁剪");
                return null;
            }
            
            // 生成缓存键
            string cacheKey = $"{sourceTexture.name}_{cropType}_{customRect?.ToString() ?? "auto"}";
            
            // 检查缓存
            if (cropCache.TryGetValue(cacheKey, out Texture2D cached))
            {
                return cached;
            }
            
            try
            {
                // 确定裁剪区域
                Rect cropRect = customRect ?? GetDefaultCropRect(cropType, sourceTexture);
                
                // 执行裁剪
                Texture2D croppedTexture = PerformCrop(sourceTexture, cropRect);
                
                if (croppedTexture != null)
                {
                    croppedTexture.name = $"{sourceTexture.name}_{cropType}";
                    
                    // 缓存结果
                    cropCache[cacheKey] = croppedTexture;
                    
                    Log.Message($"[SmartCropper] 成功裁剪纹理: {sourceTexture.name} -> {cropType} ({cropRect})");
                }
                
                return croppedTexture;
            }
            catch (Exception ex)
            {
                Log.Error($"[SmartCropper] 裁剪失败: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// 获取默认裁剪区域（归一化坐标 0-1）
        /// </summary>
        private static Rect GetDefaultCropRect(CropType cropType, Texture2D texture)
        {
            switch (cropType)
            {
                case CropType.Avatar:
                    // 头像：取上方 40%，水平居中 80%
                    return new Rect(0.1f, 0.6f, 0.8f, 0.4f);
                
                case CropType.Expression:
                    // 表情差分：取中上部 50%，水平居中 70%
                    // 这个区域覆盖面部和上半身
                    return new Rect(0.15f, 0.45f, 0.7f, 0.5f);
                
                case CropType.Portrait:
                default:
                    // 完整立绘：不裁剪
                    return new Rect(0f, 0f, 1f, 1f);
            }
        }
        
        /// <summary>
        /// 执行实际的像素裁剪
        /// </summary>
        private static Texture2D PerformCrop(Texture2D source, Rect normalizedRect)
        {
            // 转换为像素坐标
            int x = Mathf.RoundToInt(normalizedRect.x * source.width);
            int y = Mathf.RoundToInt(normalizedRect.y * source.height);
            int width = Mathf.RoundToInt(normalizedRect.width * source.width);
            int height = Mathf.RoundToInt(normalizedRect.height * source.height);
            
            // 边界检查
            x = Mathf.Clamp(x, 0, source.width - 1);
            y = Mathf.Clamp(y, 0, source.height - 1);
            width = Mathf.Clamp(width, 1, source.width - x);
            height = Mathf.Clamp(height, 1, source.height - y);
            
            // 读取像素（确保纹理可读）
            Color[] pixels;
            try
            {
                pixels = source.GetPixels(x, y, width, height);
            }
            catch (UnityException)
            {
                // 纹理不可读，尝试创建可读副本
                Log.Warning($"[SmartCropper] 纹理 {source.name} 不可读，尝试创建副本");
                Texture2D readable = MakeTextureReadable(source);
                if (readable == null)
                {
                    return null;
                }
                pixels = readable.GetPixels(x, y, width, height);
            }
            
            // 创建新纹理
            Texture2D croppedTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            croppedTexture.SetPixels(pixels);
            croppedTexture.Apply();
            
            return croppedTexture;
        }
        
        /// <summary>
        /// 将不可读纹理转换为可读纹理
        /// </summary>
        private static Texture2D MakeTextureReadable(Texture2D source)
        {
            try
            {
                // 创建 RenderTexture
                RenderTexture rt = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32);
                RenderTexture.active = rt;
                
                // 渲染到 RenderTexture
                Graphics.Blit(source, rt);
                
                // 读取像素
                Texture2D readable = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
                readable.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
                readable.Apply();
                
                // 清理
                RenderTexture.active = null;
                RenderTexture.ReleaseTemporary(rt);
                
                return readable;
            }
            catch (Exception ex)
            {
                Log.Error($"[SmartCropper] 创建可读纹理失败: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// 智能分析图像，自动定位最佳裁剪区域（高级功能）
        /// </summary>
        /// <param name="texture">要分析的纹理</param>
        /// <param name="cropType">裁剪类型</param>
        /// <returns>建议的裁剪区域</returns>
        public static Rect AnalyzeOptimalCropRect(Texture2D texture, CropType cropType)
        {
            try
            {
                // 简化实现：基于亮度分布定位主体
                Color[] pixels = texture.GetPixels();
                
                // 计算每行和每列的平均亮度
                float[] rowBrightness = new float[texture.height];
                float[] colBrightness = new float[texture.width];
                
                for (int y = 0; y < texture.height; y++)
                {
                    float rowSum = 0f;
                    for (int x = 0; x < texture.width; x++)
                    {
                        Color pixel = pixels[y * texture.width + x];
                        float brightness = (pixel.r + pixel.g + pixel.b) / 3f;
                        rowSum += brightness;
                        colBrightness[x] += brightness;
                    }
                    rowBrightness[y] = rowSum / texture.width;
                }
                
                // 归一化
                for (int x = 0; x < texture.width; x++)
                {
                    colBrightness[x] /= texture.height;
                }
                
                // 找到亮度集中区域（假设人物主体较亮）
                int topY = FindContentBoundary(rowBrightness, true);
                int bottomY = FindContentBoundary(rowBrightness, false);
                int leftX = FindContentBoundary(colBrightness, true);
                int rightX = FindContentBoundary(colBrightness, false);
                
                // 根据裁剪类型调整区域
                if (cropType == CropType.Avatar)
                {
                    // 头像：取上半部分
                    bottomY = Mathf.Min(bottomY, topY + (bottomY - topY) / 2);
                }
                else if (cropType == CropType.Expression)
                {
                    // 表情：聚焦中上部
                    int centerY = (topY + bottomY) / 2;
                    topY = Mathf.Max(topY, centerY - (bottomY - topY) / 3);
                }
                
                // 转换为归一化坐标
                return new Rect(
                    (float)leftX / texture.width,
                    (float)topY / texture.height,
                    (float)(rightX - leftX) / texture.width,
                    (float)(bottomY - topY) / texture.height
                );
            }
            catch (Exception ex)
            {
                Log.Warning($"[SmartCropper] 智能分析失败，使用默认区域: {ex.Message}");
                return GetDefaultCropRect(cropType, texture);
            }
        }
        
        /// <summary>
        /// 查找内容边界
        /// </summary>
        private static int FindContentBoundary(float[] brightness, bool fromStart)
        {
            float threshold = 0.1f; // 亮度阈值
            
            if (fromStart)
            {
                for (int i = 0; i < brightness.Length; i++)
                {
                    if (brightness[i] > threshold)
                        return i;
                }
                return 0;
            }
            else
            {
                for (int i = brightness.Length - 1; i >= 0; i--)
                {
                    if (brightness[i] > threshold)
                        return i;
                }
                return brightness.Length - 1;
            }
        }
        
        /// <summary>
        /// 清理缓存
        /// </summary>
        public static void ClearCache()
        {
            foreach (var tex in cropCache.Values)
            {
                if (tex != null)
                {
                    UnityEngine.Object.Destroy(tex);
                }
            }
            cropCache.Clear();
            Log.Message("[SmartCropper] 缓存已清理");
        }
        
        /// <summary>
        /// 获取缓存统计
        /// </summary>
        public static string GetCacheStats()
        {
            return $"[SmartCropper] 缓存项: {cropCache.Count}, 内存估算: {EstimateCacheMemoryMB():F2} MB";
        }
        
        /// <summary>
        /// 估算缓存占用内存
        /// </summary>
        private static float EstimateCacheMemoryMB()
        {
            long totalBytes = 0;
            foreach (var tex in cropCache.Values)
            {
                if (tex != null)
                {
                    // RGBA32 = 4 bytes per pixel
                    totalBytes += tex.width * tex.height * 4;
                }
            }
            return totalBytes / (1024f * 1024f);
        }
    }
}
