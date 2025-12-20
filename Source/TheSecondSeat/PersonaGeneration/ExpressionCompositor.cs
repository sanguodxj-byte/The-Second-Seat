using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace TheSecondSeat.PersonaGeneration
{
    /// <summary>
    /// 面部区域定义
    /// ? 定义表情覆盖的面部区域范围
    /// </summary>
    public class FaceRegion
    {
        // 面部区域在完整立绘中的位置（归一化坐标 0-1）
        public float CenterX { get; set; } = 0.5f;      // 水平中心（0.5 = 正中间）
        public float CenterY { get; set; } = 0.35f;     // 垂直中心（0.35 = 略偏上，适合大多数立绘）
        public float Width { get; set; } = 0.4f;        // 宽度占比（0.4 = 40%）
        public float Height { get; set; } = 0.3f;       // 高度占比（0.3 = 30%）
        
        // 羽化边缘（避免硬边界）
        public float FeatherRadius { get; set; } = 0.05f;  // 5%的羽化范围
        
        /// <summary>
        /// 获取面部区域的像素范围
        /// </summary>
        public Rect GetPixelRect(int textureWidth, int textureHeight)
        {
            float pixelWidth = Width * textureWidth;
            float pixelHeight = Height * textureHeight;
            float pixelX = (CenterX * textureWidth) - (pixelWidth / 2f);
            float pixelY = (CenterY * textureHeight) - (pixelHeight / 2f);
            
            return new Rect(pixelX, pixelY, pixelWidth, pixelHeight);
        }
    }
    
    /// <summary>
    /// 表情纹理合成器
    /// ? 将面部表情纹理叠加到基础立绘上
    /// </summary>
    public static class ExpressionCompositor
    {
        // 缓存已合成的表情纹理
        private static Dictionary<string, Texture2D> compositeCache = new Dictionary<string, Texture2D>();
        
        /// <summary>
        /// 合成表情（基础立绘 + 脸部差分）
        /// ? 新增：支持智能裁剪，减少内存占用
        /// </summary>
        /// <param name="baseTexture">基础立绘（完整身体、衣服等）</param>
        /// <param name="faceTexture">脸部差分（或完整表情立绘）</param>
        /// <param name="faceRegion">脸部区域</param>
        /// <param name="cacheKey">缓存键（用于避免重复合成）</param>
        /// <param name="autoCrop">是否自动裁剪表情差分（如果是完整立绘）</param>
        /// <returns>合成后的立绘纹理</returns>
        public static Texture2D CompositeExpression(
            Texture2D baseTexture, 
            Texture2D faceTexture, 
            FaceRegion faceRegion,
            string cacheKey = null,
            bool autoCrop = true)
        {
            // 检查缓存
            if (!string.IsNullOrEmpty(cacheKey) && compositeCache.TryGetValue(cacheKey, out Texture2D cached))
            {
                return cached;
            }
            
            try
            {
                // ? 智能裁剪：如果表情差分是完整立绘，自动裁剪出面部区域
                Texture2D processedFace = faceTexture;
                if (autoCrop && IsFullPortrait(faceTexture, baseTexture))
                {
                    Log.Message($"[ExpressionCompositor] 检测到完整立绘表情，执行智能裁剪");
                    processedFace = SmartCropper.CropTexture(faceTexture, SmartCropper.CropType.Expression);
                    
                    if (processedFace == null)
                    {
                        Log.Warning("[ExpressionCompositor] 裁剪失败，使用原始纹理");
                        processedFace = faceTexture;
                    }
                }
                
                // 创建可读版本纹理
                Texture2D readableBase = MakeReadable(baseTexture);
                Texture2D readableFace = MakeReadable(processedFace);
                
                // 创建结果纹理
                Texture2D result = new Texture2D(readableBase.width, readableBase.height, TextureFormat.RGBA32, false);
                
                // 复制基础像素
                Color[] basePixels = readableBase.GetPixels();
                result.SetPixels(basePixels);
                
                // 获取脸部区域
                Rect faceRect = faceRegion.GetPixelRect(readableBase.width, readableBase.height);
                
                // 混合脸部差分
                BlendFaceRegion(result, readableFace, faceRect, faceRegion.FeatherRadius);
                
                result.Apply();
                
                // 保存缓存
                if (!string.IsNullOrEmpty(cacheKey))
                {
                    compositeCache[cacheKey] = result;
                }
                
                // 清理临时纹理
                if (readableBase != baseTexture)
                    UnityEngine.Object.Destroy(readableBase);
                if (readableFace != faceTexture && readableFace != processedFace)
                    UnityEngine.Object.Destroy(readableFace);
                
                Log.Message($"[ExpressionCompositor] 合成表情成功: {cacheKey}");
                return result;
            }
            catch (Exception ex)
            {
                Log.Error($"[ExpressionCompositor] 合成表情失败: {ex}");
                return baseTexture; // 失败时返回基础立绘
            }
        }
        
        /// <summary>
        /// ? 检测是否为完整立绘（而非已裁剪的表情差分）
        /// </summary>
        private static bool IsFullPortrait(Texture2D texture, Texture2D referenceTexture)
        {
            // 简单启发式：如果尺寸与基础立绘相同或接近，认为是完整立绘
            float sizeRatio = (float)(texture.width * texture.height) / (referenceTexture.width * referenceTexture.height);
            return sizeRatio > 0.7f; // 面积超过 70% 认为是完整立绘
        }
        
        /// <summary>
        /// 将纹理转换为可读格式
        /// </summary>
        private static Texture2D MakeReadable(Texture2D source)
        {
            RenderTexture renderTex = RenderTexture.GetTemporary(
                source.width, 
                source.height, 
                0, 
                RenderTextureFormat.Default, 
                RenderTextureReadWrite.Linear
            );
            
            Graphics.Blit(source, renderTex);
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = renderTex;
            
            Texture2D readable = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
            readable.ReadPixels(new Rect(0, 0, renderTex.width, renderTex.height), 0, 0);
            readable.Apply();
            
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(renderTex);
            
            return readable;
        }
        
        /// <summary>
        /// 混合面部区域（带羽化边缘）
        /// </summary>
        private static void BlendFaceRegion(
            Texture2D target, 
            Texture2D face, 
            Rect faceRect, 
            float featherRadius)
        {
            int startX = Mathf.Max(0, Mathf.FloorToInt(faceRect.x));
            int startY = Mathf.Max(0, Mathf.FloorToInt(faceRect.y));
            int endX = Mathf.Min(target.width, Mathf.CeilToInt(faceRect.xMax));
            int endY = Mathf.Min(target.height, Mathf.CeilToInt(faceRect.yMax));
            
            float featherPixels = featherRadius * target.width;
            
            for (int y = startY; y < endY; y++)
            {
                for (int x = startX; x < endX; x++)
                {
                    // 计算在面部纹理中的归一化坐标
                    float u = (x - faceRect.x) / faceRect.width;
                    float v = (y - faceRect.y) / faceRect.height;
                    
                    // 边界检查
                    if (u < 0 || u > 1 || v < 0 || v > 1) continue;
                    
                    // 从面部纹理采样
                    int faceX = Mathf.FloorToInt(u * face.width);
                    int faceY = Mathf.FloorToInt(v * face.height);
                    
                    if (faceX < 0 || faceX >= face.width || faceY < 0 || faceY >= face.height) continue;
                    
                    Color faceColor = face.GetPixel(faceX, faceY);
                    
                    // 如果面部像素是透明的，跳过
                    if (faceColor.a < 0.01f) continue;
                    
                    // 计算羽化权重（距离边缘越近越透明）
                    float distToEdge = GetDistanceToEdge(u, v);
                    float featherWeight = Mathf.Clamp01(distToEdge / featherRadius);
                    
                    // 混合颜色
                    Color baseColor = target.GetPixel(x, y);
                    float alpha = faceColor.a * featherWeight;
                    Color blended = Color.Lerp(baseColor, faceColor, alpha);
                    
                    target.SetPixel(x, y, blended);
                }
            }
        }
        
        /// <summary>
        /// 计算点到矩形边缘的最小距离（归一化）
        /// </summary>
        private static float GetDistanceToEdge(float u, float v)
        {
            float distX = Mathf.Min(u, 1f - u);
            float distY = Mathf.Min(v, 1f - v);
            return Mathf.Min(distX, distY);
        }
        
        /// <summary>
        /// 清除合成缓存
        /// </summary>
        public static void ClearCache()
        {
            foreach (var texture in compositeCache.Values)
            {
                if (texture != null)
                {
                    UnityEngine.Object.Destroy(texture);
                }
            }
            compositeCache.Clear();
            Log.Message("[ExpressionCompositor] 表情合成缓存已清除");
        }
        
        /// <summary>
        /// 获取调试信息
        /// </summary>
        public static string GetDebugInfo()
        {
            return $"[ExpressionCompositor] 缓存数量: {compositeCache.Count}";
        }
    }
    
    /// <summary>
    /// 人格面部区域配置
    /// ? 为不同人格定义面部区域（因为立绘构图可能不同）
    /// </summary>
    public static class PersonaFaceRegions
    {
        private static Dictionary<string, FaceRegion> regions = new Dictionary<string, FaceRegion>();
        
        static PersonaFaceRegions()
        {
            // 默认面部区域（适合大多数立绘）
            regions["Default"] = new FaceRegion
            {
                CenterX = 0.5f,   // 水平居中
                CenterY = 0.35f,  // 略偏上（头部通常在上1/3处）
                Width = 0.4f,     // 宽度40%
                Height = 0.3f,    // 高度30%
                FeatherRadius = 0.05f
            };
            
            // 示例：为特定人格自定义区域
            regions["Cassandra_Classic"] = new FaceRegion
            {
                CenterX = 0.5f,
                CenterY = 0.32f,  // Cassandra的脸稍微靠上
                Width = 0.38f,
                Height = 0.28f,
                FeatherRadius = 0.06f
            };
            
            regions["Phoebe_Friendly"] = new FaceRegion
            {
                CenterX = 0.5f,
                CenterY = 0.37f,  // Phoebe的脸稍微靠下
                Width = 0.42f,
                Height = 0.32f,
                FeatherRadius = 0.05f
            };
        }
        
        /// <summary>
        /// 获取人格的面部区域
        /// </summary>
        public static FaceRegion GetFaceRegion(string personaDefName)
        {
            if (regions.TryGetValue(personaDefName, out FaceRegion region))
            {
                return region;
            }
            
            // 回退到默认区域
            return regions["Default"];
        }
        
        /// <summary>
        /// 设置人格的面部区域
        /// </summary>
        public static void SetFaceRegion(string personaDefName, FaceRegion region)
        {
            regions[personaDefName] = region;
            Log.Message($"[PersonaFaceRegions] 已设置 {personaDefName} 的面部区域");
        }
    }
}
