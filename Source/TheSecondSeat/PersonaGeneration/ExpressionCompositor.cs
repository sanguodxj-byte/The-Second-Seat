using System;
using System.Collections.Generic;
using System.Linq;
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
        // 坐标系：Top-Down (0,0 在左上角)
        public float CenterX { get; set; } = 0.5f;      // 水平中心（0.5 = 正中间）
        public float CenterY { get; set; } = 0.35f;     // 垂直中心（0.35 = 略偏上，适合大多数立绘）
        public float Width { get; set; } = 0.4f;        // 宽度占比（0.4 = 40%）
        public float Height { get; set; } = 0.3f;       // 高度占比（0.3 = 30%）
        
        // 羽化边缘（避免硬边界）
        // 注意：GPU 版本暂不支持自动羽化，依赖素材本身的 Alpha 通道
        public float FeatherRadius { get; set; } = 0.05f;  
        
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
    /// 表情纹理合成器 (GPU Accelerated)
    /// ? 将面部表情纹理叠加到基础立绘上
    /// ? v2.0: 使用 RenderTexture 替代 CPU SetPixels，大幅提升性能
    /// </summary>
    public static class ExpressionCompositor
    {
        // 缓存已合成的表情纹理
        private static Dictionary<string, Texture> compositeCache = new Dictionary<string, Texture>();
        private const int MaxCacheSize = 20; // 限制缓存大小，避免显存占用过高
        
        /// <summary>
        /// 合成表情（基础立绘 + 脸部差分）
        /// </summary>
        /// <param name="baseTexture">基础立绘（完整身体、衣服等）</param>
        /// <param name="faceTexture">脸部差分（或完整表情立绘）</param>
        /// <param name="faceRegion">脸部区域</param>
        /// <param name="cacheKey">缓存键（用于避免重复合成）</param>
        /// <param name="autoCrop">是否自动裁剪表情差分（如果是完整立绘且为 Texture2D）</param>
        /// <returns>合成后的立绘纹理 (RenderTexture)</returns>
        public static Texture CompositeExpression(
            Texture baseTexture, 
            Texture faceTexture, 
            FaceRegion faceRegion,
            string cacheKey = null,
            bool autoCrop = true)
        {
            // 检查缓存
            if (!string.IsNullOrEmpty(cacheKey) && compositeCache.TryGetValue(cacheKey, out Texture cached))
            {
                return cached;
            }
            
            if (baseTexture == null) return null;
            
            // 如果没有表情贴图，直接返回底图
            if (faceTexture == null) return baseTexture;

            Texture processedFace = faceTexture;

            // ? 智能裁剪逻辑
            // 仅当输入是 Texture2D 时尝试裁剪 (SmartCropper 需要读取像素)
            // 如果是 RenderTexture，假设已经处理好或者是作为整体覆盖
            if (autoCrop && faceTexture is Texture2D faceTex2D && baseTexture is Texture2D baseTex2D)
            {
                if (IsFullPortrait(faceTex2D, baseTex2D))
                {
                    // Log.Message($"[ExpressionCompositor] 检测到完整立绘表情，执行智能裁剪");
                    var cropped = SmartCropper.CropTexture(faceTex2D, SmartCropper.CropType.Expression);
                    if (cropped != null)
                    {
                        processedFace = cropped;
                    }
                }
            }
            
            // 创建目标 RT
            RenderTexture targetRT = PortraitRenderSystem.CreateRenderTexture(baseTexture.width, baseTexture.height);
            
            RenderTexture previousRT = RenderTexture.active;

            try
            {
                RenderTexture.active = targetRT;
                
                // 1. 清除背景
                GL.Clear(true, true, Color.clear);
                
                // 2. 设置 Top-Down 坐标系 (与 GUI 和 FaceRegion 定义一致)
                GL.PushMatrix();
                GL.LoadPixelMatrix(0, baseTexture.width, baseTexture.height, 0);
                
                // 3. 绘制底图 (铺满)
                Graphics.DrawTexture(new Rect(0, 0, baseTexture.width, baseTexture.height), baseTexture);
                
                // 4. 计算面部位置并绘制
                Rect faceRect = faceRegion.GetPixelRect(baseTexture.width, baseTexture.height);
                Graphics.DrawTexture(faceRect, processedFace);
                
                GL.PopMatrix();
                
                // 缓存结果
                if (!string.IsNullOrEmpty(cacheKey))
                {
                    AddToCache(cacheKey, targetRT);
                }
                
                return targetRT;
            }
            catch (Exception ex)
            {
                Log.Error($"[ExpressionCompositor] GPU Composite failed: {ex}");
                PortraitRenderSystem.Release(targetRT);
                return baseTexture;
            }
            finally
            {
                RenderTexture.active = previousRT;
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
        /// 添加到缓存并管理容量
        /// </summary>
        private static void AddToCache(string key, Texture texture)
        {
            if (compositeCache.ContainsKey(key))
            {
                ReleaseTexture(compositeCache[key]);
                compositeCache[key] = texture;
                return;
            }

            if (compositeCache.Count >= MaxCacheSize)
            {
                var firstKey = compositeCache.Keys.First();
                ReleaseTexture(compositeCache[firstKey]);
                compositeCache.Remove(firstKey);
            }
            
            compositeCache[key] = texture;
        }

        private static void ReleaseTexture(Texture texture)
        {
            if (texture is RenderTexture rt)
            {
                PortraitRenderSystem.Release(rt);
            }
            else
            {
                UnityEngine.Object.Destroy(texture);
            }
        }
        
        /// <summary>
        /// 清除合成缓存
        /// </summary>
        public static void ClearCache()
        {
            foreach (var texture in compositeCache.Values)
            {
                ReleaseTexture(texture);
            }
            compositeCache.Clear();
            if (Prefs.DevMode)
            {
                Log.Message("[ExpressionCompositor] 表情合成缓存已清除 (GPU)");
            }
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
