using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace TheSecondSeat.PersonaGeneration
{
    /// <summary>
    /// 分层立绘合成器
    /// 负责将多个图层纹理合成为最终立绘
    /// ? v1.6.27: 使用base_body.png作为底图，其他部件覆盖
    /// </summary>
    public static class LayeredPortraitCompositor
    {
        // 合成缓存（避免重复合成相同配置）
        private static Dictionary<string, Texture2D> compositeCache = new Dictionary<string, Texture2D>();
        
        // 基础纹理路径
        private const string LAYERED_BASE_PATH = "UI/Narrators/9x16/Layered/";

        /// <summary>
        /// 合成分层立绘（异步版本）
        /// ? v1.6.27: 使用base_body作为底图，其他部件覆盖；异步合成避免卡顿
        /// ? v1.6.29: Neutral表情直接使用base_body（底图已包含睁眼闭嘴）
        /// </summary>
        public static async Task<Texture2D> CompositeLayersAsync(
            LayeredPortraitConfig config, 
            ExpressionType expression = ExpressionType.Neutral, 
            string outfit = "default")
        {
            // 1. 生成缓存键
            string cacheKey = $"{config.PersonaDefName}_{expression}_default";
            
            // 2. 检查缓存
            if (compositeCache.TryGetValue(cacheKey, out Texture2D cachedTexture))
            {
                return cachedTexture;
            }

            try
            {
                // ? v1.6.27: 使用 PersonaName 而不是 PersonaDefName
                string personaName = config.PersonaName;
                
                // 3. ? 加载base_body作为底图
                var baseBodyTexture = LoadLayerTexture(personaName, "base_body");
                
                if (baseBodyTexture == null)
                {
                    // ? 只在DevMode下输出警告
                    if (Prefs.DevMode)
                    {
                        Log.Warning($"[LayeredPortraitCompositor] base_body.png not found for {personaName}");
                    }
                    return null;
                }

                // ? v1.6.29: 如果是Neutral表情，直接返回底图（底图已包含睁眼闭嘴）
                if (expression == ExpressionType.Neutral)
                {
                    compositeCache[cacheKey] = baseBodyTexture;
                    return baseBodyTexture;
                }

                // 创建图层列表，base_body作为第一层
                List<Texture2D> layers = new List<Texture2D>();
                layers.Add(baseBodyTexture);
                
                // 4. ? 根据表情选择需要覆盖的部件（眼睛和嘴巴）
                string eyesLayerName = GetEyesLayerName(expression);
                string mouthLayerName = GetMouthLayerName(expression);
                
                // ? 只加载非默认的部件（避免加载opened_eyes和opened_mouth）
                if (eyesLayerName != "opened_eyes")
                {
                    var eyesTexture = LoadLayerTexture(personaName, eyesLayerName);
                    if (eyesTexture != null) 
                    {
                        layers.Add(eyesTexture);
                    }
                }
                
                if (mouthLayerName != "opened_mouth")
                {
                    var mouthTexture = LoadLayerTexture(personaName, mouthLayerName);
                    if (mouthTexture != null) 
                    {
                        layers.Add(mouthTexture);
                    }
                }
                
                // 5. ? 可选：腮红/特效层
                var flushTexture = LoadLayerTexture(personaName, $"{GetExpressionPrefix(expression)}_flush");
                if (flushTexture != null)
                {
                    layers.Add(flushTexture);
                }
                
                // 6. ? 异步合成所有层（在后台线程）
                Texture2D composite = await Task.Run(() => CompositeAllLayers(layers));
                
                // 7. 缓存结果
                if (composite != null)
                {
                    compositeCache[cacheKey] = composite;
                }
                
                return composite;
            }
            catch (Exception ex)
            {
                // ? 只在DevMode下输出错误
                if (Prefs.DevMode)
                {
                    Log.Error($"[LayeredPortraitCompositor] Composite failed: {ex}");
                }
                return null;
            }
        }
        
        /// <summary>
        /// 合成分层立绘（同步版本，已废弃）
        /// ?? 警告：此方法会卡主线程，请使用 CompositeLayersAsync
        /// ? v1.6.29: Neutral表情直接使用base_body（底图已包含睁眼闭嘴）
        /// </summary>
        [Obsolete("Use CompositeLayersAsync instead to avoid blocking the main thread")]
        public static Texture2D CompositeLayers(
            LayeredPortraitConfig config, 
            ExpressionType expression = ExpressionType.Neutral, 
            string outfit = "default")
        {
            // 1. 生成缓存键
            string cacheKey = $"{config.PersonaDefName}_{expression}_default";
            
            // 2. 检查缓存
            if (compositeCache.TryGetValue(cacheKey, out Texture2D cachedTexture))
            {
                return cachedTexture;
            }

            try
            {
                // ? v1.6.27: 使用 PersonaName 而不是 PersonaDefName
                string personaName = config.PersonaName;
                
                // 3. ? 加载base_body作为底图
                var baseBodyTexture = LoadLayerTexture(personaName, "base_body");
                
                if (baseBodyTexture == null)
                {
                    // ? 只在DevMode下输出警告
                    if (Prefs.DevMode)
                    {
                        Log.Warning($"[LayeredPortraitCompositor] base_body.png not found for {personaName}");
                    }
                    return null;
                }

                // ? v1.6.29: 如果是Neutral表情，直接返回底图（底图已包含睁眼闭嘴）
                if (expression == ExpressionType.Neutral)
                {
                    compositeCache[cacheKey] = baseBodyTexture;
                    return baseBodyTexture;
                }
                
                // 创建图层列表，base_body作为第一层
                List<Texture2D> layers = new List<Texture2D>();
                layers.Add(baseBodyTexture);
                
                // 4. ? 根据表情选择需要覆盖的部件（眼睛和嘴巴）
                string eyesLayerName = GetEyesLayerName(expression);
                string mouthLayerName = GetMouthLayerName(expression);
                
                // ? 只加载非默认的部件（避免加载opened_eyes和opened_mouth）
                if (eyesLayerName != "opened_eyes")
                {
                    var eyesTexture = LoadLayerTexture(personaName, eyesLayerName);
                    if (eyesTexture != null) 
                    {
                        layers.Add(eyesTexture);
                    }
                }
                
                if (mouthLayerName != "opened_mouth")
                {
                    var mouthTexture = LoadLayerTexture(personaName, mouthLayerName);
                    if (mouthTexture != null) 
                    {
                        layers.Add(mouthTexture);
                    }
                }
                
                // 5. ? 可选：腮红/特效层
                var flushTexture = LoadLayerTexture(personaName, $"{GetExpressionPrefix(expression)}_flush");
                if (flushTexture != null)
                {
                    layers.Add(flushTexture);
                }
                
                // 6. 合成所有层
                Texture2D composite = CompositeAllLayers(layers);
                
                // 7. 缓存结果
                if (composite != null)
                {
                    compositeCache[cacheKey] = composite;
                }
                
                return composite;
            }
            catch (Exception ex)
            {
                // ? 只在DevMode下输出错误
                if (Prefs.DevMode)
                {
                    Log.Error($"[LayeredPortraitCompositor] Composite failed: {ex}");
                }
                return null;
            }
        }
        
        /// <summary>
        /// ? 根据表情类型获取眼睛层名称
        /// ? v1.6.29: 修复Neutral使用opened_eyes（睁眼）
        /// </summary>
        private static string GetEyesLayerName(ExpressionType expression)
        {
            return expression switch
            {
                ExpressionType.Neutral => "opened_eyes",  // ? 修复：中性表情应该睁眼
                ExpressionType.Happy => "happy_eyes",     // ? 如果没有则回退到opened_eyes
                ExpressionType.Sad => "sad_eyes",
                ExpressionType.Angry => "angry_eyes",
                ExpressionType.Surprised => "opened_eyes", // 惊讶睁大眼睛
                ExpressionType.Confused => "opened_eyes",  // 困惑睁眼
                ExpressionType.Smug => "opened_eyes",      // 得意睁眼
                ExpressionType.Shy => "opened_eyes",       // 害羞睁眼
                _ => "opened_eyes"
            };
        }

        /// <summary>
        /// ? 根据表情类型获取嘴巴层名称
        /// ? v1.6.29: 修复Neutral使用opened_mouth（闭嘴）
        /// </summary>
        private static string GetMouthLayerName(ExpressionType expression)
        {
            return expression switch
            {
                ExpressionType.Neutral => "opened_mouth",   // ? 修复：中性表情应该闭嘴
                ExpressionType.Happy => "larger_mouth",     // ? 开心用大嘴
                ExpressionType.Sad => "sad_mouth",
                ExpressionType.Angry => "angry_mouth",
                ExpressionType.Surprised => "larger_mouth", // 惊讶张大嘴
                ExpressionType.Confused => "opened_mouth",  // 困惑闭嘴
                ExpressionType.Smug => "small1_mouth",      // 使用小嘴变体
                ExpressionType.Shy => "opened_mouth",       // 害羞闭嘴
                _ => "opened_mouth"
            };
        }
        
        /// <summary>
        /// ? 获取表情前缀（用于查找特效层）
        /// </summary>
        private static string GetExpressionPrefix(ExpressionType expression)
        {
            return expression switch
            {
                ExpressionType.Angry => "angry",
                ExpressionType.Shy => "shy",
                _ => ""
            };
        }
        
        /// <summary>
        /// 加载单个图层纹理
        /// ? v1.6.27: 完全静默，不输出任何日志
        /// ? v1.6.27: 直接使用 personaName（如 "Sideria"）
        /// </summary>
        private static Texture2D LoadLayerTexture(string personaName, string layerName)
        {
            string texturePath = $"{LAYERED_BASE_PATH}{personaName}/{layerName}";
            var texture = ContentFinder<Texture2D>.Get(texturePath, false);
            
            // ? v1.6.27: 完全静默，成功和失败都不输出
            
            return texture;
        }
        
        /// <summary>
        /// ? v1.6.27: 从 defName 提取人格文件夹名称
        /// 例如：Sideria_Default → Sideria, Cassandra_Classic → Cassandra
        /// </summary>
        private static string GetPersonaFolderName(string defName)
        {
            if (string.IsNullOrEmpty(defName))
            {
                return defName;
            }
            
            // 移除常见后缀
            string[] suffixesToRemove = new[] { "_Default", "_Classic", "_Custom", "_Persona", "_Chillax", "_Random", "_Invader", "_Protector" };
            
            foreach (var suffix in suffixesToRemove)
            {
                if (defName.EndsWith(suffix))
                {
                    return defName.Substring(0, defName.Length - suffix.Length);
                }
            }
            
            return defName;
        }
        
        /// <summary>
        /// 合成所有图层
        /// ? v1.6.27: 静默处理尺寸不匹配
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
                    // ? 只在DevMode下输出警告
                    if (Prefs.DevMode)
                    {
                        Log.Warning($"[LayeredPortraitCompositor] Layer size mismatch: {layer.width}x{layer.height} vs {width}x{height}");
                    }
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
                    // ? 只在DevMode下输出错误
                    if (Prefs.DevMode)
                    {
                        Log.Error($"[LayeredPortraitCompositor] Failed to blend layer: {ex}");
                    }
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
        /// ? v1.6.27: 预加载所有表情到缓存（加载存档时调用）
        /// </summary>
        public static void PreloadAllExpressions(LayeredPortraitConfig config)
        {
            if (config == null) return;
            
            var allExpressions = System.Enum.GetValues(typeof(ExpressionType)).Cast<ExpressionType>();
            int loadedCount = 0;
            
            foreach (var expression in allExpressions)
            {
                string cacheKey = $"{config.PersonaDefName}_{expression}_default";
                
                // 如果已缓存，跳过
                if (compositeCache.ContainsKey(cacheKey))
                {
                    continue;
                }
                
                try
                {
                    // 同步合成并缓存
                    #pragma warning disable CS0618  // 允许使用已废弃的方法（内部调用）
                    var composite = CompositeLayers(config, expression, "default");
                    #pragma warning restore CS0618
                    
                    if (composite != null)
                    {
                        loadedCount++;
                    }
                }
                catch (Exception ex)
                {
                    if (Prefs.DevMode)
                    {
                        Log.Warning($"[LayeredPortraitCompositor] Failed to preload {expression}: {ex.Message}");
                    }
                }
            }
            
            if (Prefs.DevMode && loadedCount > 0)
            {
                Log.Message($"[LayeredPortraitCompositor] Preloaded {loadedCount} expressions for {config.PersonaName}");
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
