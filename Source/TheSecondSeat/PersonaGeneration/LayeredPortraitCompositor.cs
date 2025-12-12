using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace TheSecondSeat.PersonaGeneration
{
    /// <summary>
    /// 分层立绘合成器
    /// 负责将多个图层纹理合成为最终立绘
    /// ? v1.6.20: 支持根据表情动态选择眼睛和嘴巴部件
    /// </summary>
    public static class LayeredPortraitCompositor
    {
        // 合成缓存（避免重复合成相同配置）
        private static Dictionary<string, Texture2D> compositeCache = new Dictionary<string, Texture2D>();
        
        // 基础纹理路径
        private const string LAYERED_BASE_PATH = "UI/Narrators/9x16/Layered/";

        /// <summary>
        /// 合成分层立绘
        /// </summary>
        /// <param name="config">分层配置</param>
        /// <param name="expression">当前表情</param>
        /// <param name="outfit">当前服装ID</param>
        /// <returns>合成后的纹理</returns>
        public static Texture2D CompositeLayers(
            LayeredPortraitConfig config, 
            ExpressionType expression = ExpressionType.Neutral, 
            string outfit = "default")
        {
            // 1. 生成缓存键
            string cacheKey = $"{config.PersonaDefName}_{expression}_{outfit}";
            
            // 2. 检查缓存
            if (compositeCache.TryGetValue(cacheKey, out Texture2D cachedTexture))
            {
                return cachedTexture;
            }

            try
            {
                // 3. 加载基础层
                List<Texture2D> layers = new List<Texture2D>();
                
                // 背景层（可选）
                var bgTexture = LoadLayerTexture(config.PersonaDefName, "background");
                if (bgTexture != null) layers.Add(bgTexture);
                
                // 身体层
                var bodyTexture = LoadLayerTexture(config.PersonaDefName, "body");
                if (bodyTexture != null) layers.Add(bodyTexture);
                
                // 头发层（背部，如果有）
                var hairBackTexture = LoadLayerTexture(config.PersonaDefName, "hair_back");
                if (hairBackTexture != null) layers.Add(hairBackTexture);
                
                // 4. ? 根据表情选择眼睛和嘴巴部件
                string eyesLayerName = GetEyesLayerName(expression);
                string mouthLayerName = GetMouthLayerName(expression);
                
                var eyesTexture = LoadLayerTexture(config.PersonaDefName, eyesLayerName);
                if (eyesTexture != null) layers.Add(eyesTexture);
                
                var mouthTexture = LoadLayerTexture(config.PersonaDefName, mouthLayerName);
                if (mouthTexture != null) layers.Add(mouthTexture);
                
                // 头发层（前部）
                var hairFrontTexture = LoadLayerTexture(config.PersonaDefName, "hair");
                if (hairFrontTexture != null) layers.Add(hairFrontTexture);
                
                // 服装层（可选）
                if (outfit != "default")
                {
                    var outfitTexture = LoadLayerTexture(config.PersonaDefName, $"outfit_{outfit}");
                    if (outfitTexture != null) layers.Add(outfitTexture);
                }
                
                // 5. 合成所有层
                if (layers.Count == 0)
                {
                    Log.Warning($"[LayeredPortraitCompositor] No layers found for {config.PersonaDefName}");
                    return null;
                }
                
                Texture2D composite = CompositeAllLayers(layers);
                
                // 6. 缓存结果
                compositeCache[cacheKey] = composite;
                
                if (Prefs.DevMode)
                {
                    Log.Message($"[LayeredPortraitCompositor] ? Composite created: {config.PersonaDefName} ({expression}, {layers.Count} layers)");
                }
                
                return composite;
            }
            catch (Exception ex)
            {
                Log.Error($"[LayeredPortraitCompositor] Composite failed: {ex}");
                return null;
            }
        }
        
        /// <summary>
        /// 根据表情类型获取眼睛层名称
        /// </summary>
        private static string GetEyesLayerName(ExpressionType expression)
        {
            return expression switch
            {
                ExpressionType.Neutral => "neutral_eyes",
                ExpressionType.Happy => "happy_eyes",
                ExpressionType.Sad => "sad_eyes",
                ExpressionType.Angry => "angry_eyes",
                ExpressionType.Surprised => "surprised_eyes",
                ExpressionType.Confused => "confused_eyes",
                ExpressionType.Smug => "smug_eyes",
                ExpressionType.Shy => "shy_eyes",
                _ => "neutral_eyes"
            };
        }

        /// <summary>
        /// 根据表情类型获取嘴巴层名称
        /// </summary>
        private static string GetMouthLayerName(ExpressionType expression)
        {
            // ? 使用 MouthAnimationSystem 动态获取嘴型
            // 这样可以支持说话时的嘴巴开合动画
            return expression switch
            {
                ExpressionType.Neutral => "neutral_mouth",
                ExpressionType.Happy => "happy_mouth",
                ExpressionType.Sad => "sad_mouth",
                ExpressionType.Angry => "angry_mouth",
                ExpressionType.Surprised => "surprised_mouth",
                ExpressionType.Confused => "confused_mouth",
                ExpressionType.Smug => "smug_mouth",
                ExpressionType.Shy => "shy_mouth",
                _ => "neutral_mouth"
            };
        }
        
        /// <summary>
        /// 加载单个图层纹理
        /// </summary>
        private static Texture2D LoadLayerTexture(string personaDefName, string layerName)
        {
            string texturePath = $"{LAYERED_BASE_PATH}{personaDefName}/{layerName}";
            var texture = ContentFinder<Texture2D>.Get(texturePath, false);
            
            if (texture != null && Prefs.DevMode)
            {
                Log.Message($"[LayeredPortraitCompositor] ? Loaded layer: {texturePath}");
            }
            
            return texture;
        }
        
        /// <summary>
        /// 合成所有图层
        /// </summary>
        private static Texture2D CompositeAllLayers(List<Texture2D> layers)
        {
            if (layers.Count == 0)
            {
                return null;
            }
            
            // 使用第一个图层作为基础
            Texture2D baseLayer = layers[0];
            int width = baseLayer.width;
            int height = baseLayer.height;
            
            // 创建最终纹理
            Texture2D result = new Texture2D(width, height, TextureFormat.RGBA32, false);
            
            // 初始化为透明
            Color[] pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = Color.clear;
            }
            
            // 逐层混合
            foreach (var layer in layers)
            {
                if (layer == null) continue;
                
                // 确保尺寸一致
                if (layer.width != width || layer.height != height)
                {
                    Log.Warning($"[LayeredPortraitCompositor] Layer size mismatch: {layer.width}x{layer.height} vs {width}x{height}");
                    continue;
                }
                
                try
                {
                    // 获取图层像素（使用 RenderTexture 避免 "not readable" 错误）
                    Color[] layerPixels = GetReadablePixels(layer);
                    
                    // Alpha 混合
                    for (int i = 0; i < pixels.Length; i++)
                    {
                        Color srcColor = pixels[i];
                        Color dstColor = layerPixels[i];
                        
                        // 标准 Alpha 混合公式
                        float alpha = dstColor.a;
                        pixels[i] = new Color(
                            srcColor.r * (1 - alpha) + dstColor.r * alpha,
                            srcColor.g * (1 - alpha) + dstColor.g * alpha,
                            srcColor.b * (1 - alpha) + dstColor.b * alpha,
                            Mathf.Max(srcColor.a, dstColor.a)
                        );
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[LayeredPortraitCompositor] Failed to blend layer: {ex}");
                }
            }
            
            // 应用最终像素
            result.SetPixels(pixels);
            result.Apply();
            
            return result;
        }
        
        /// <summary>
        /// 获取纹理的可读像素（避免 "not readable" 错误）
        /// </summary>
        private static Color[] GetReadablePixels(Texture2D texture)
        {
            // 尝试直接读取
            try
            {
                return texture.GetPixels();
            }
            catch
            {
                // 如果失败，使用 RenderTexture 转换
                RenderTexture renderTex = RenderTexture.GetTemporary(
                    texture.width, 
                    texture.height, 
                    0, 
                    RenderTextureFormat.Default, 
                    RenderTextureReadWrite.Linear
                );
                
                Graphics.Blit(texture, renderTex);
                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = renderTex;
                
                Texture2D readable = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
                readable.ReadPixels(new Rect(0, 0, renderTex.width, renderTex.height), 0, 0);
                readable.Apply();
                
                Color[] pixels = readable.GetPixels();
                
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(renderTex);
                
                return pixels;
            }
        }
        
        /// <summary>
        /// 清除特定人格和表情的缓存
        /// </summary>
        public static void ClearCache(string personaDefName, ExpressionType expression)
        {
            string cacheKey = $"{personaDefName}_{expression}_default";
            
            if (compositeCache.Remove(cacheKey))
            {
                if (Prefs.DevMode)
                {
                    Log.Message($"[LayeredPortraitCompositor] Cache cleared: {cacheKey}");
                }
            }
        }
        
        /// <summary>
        /// 清除所有缓存
        /// </summary>
        public static void ClearAllCache()
        {
            compositeCache.Clear();
            Log.Message("[LayeredPortraitCompositor] All cache cleared");
        }
    }
}
