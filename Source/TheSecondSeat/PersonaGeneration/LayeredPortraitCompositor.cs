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
        // ✅ 修复内存泄漏：添加缓存大小限制
        private static Dictionary<string, Texture2D> compositeCache = new Dictionary<string, Texture2D>();
        private const int MaxCacheSize = 30; // 最大缓存数量
        
        // 基础纹理路径
        private const string LAYERED_BASE_PATH = "UI/Narrators/9x16/Layered/";

        /// <summary>
        /// 合成分层立绘（异步版本）
        /// ⚠️ v1.6.80: 修复线程安全问题 - 纹理加载必须在主线程
        /// ⚠️ v1.6.81: 修复CS1998警告 - 移除async关键字，使用Task.FromResult包装
        /// ? v1.6.27: 使用base_body作为底图，其他部件覆盖；异步合成避免卡顿
        /// ? v1.6.29: Neutral表情直接使用base_body（底图已包含睁眼闭嘴）
        /// </summary>
        public static Task<Texture2D> CompositeLayersAsync(
            LayeredPortraitConfig config,
            ExpressionType expression = ExpressionType.Neutral,
            string outfit = "default")
        {
            // 1. 生成缓存键
            string cacheKey = $"{config.PersonaDefName}_{expression}_default";
            
            // 2. 检查缓存
            if (compositeCache.TryGetValue(cacheKey, out Texture2D cachedTexture))
            {
                return Task.FromResult(cachedTexture);
            }

            try
            {
                // ? v1.6.27: 使用 PersonaName 而不是 PersonaDefName
                string personaName = config.PersonaName;
                
                // ⚠️ v1.6.80: 所有纹理加载必须在主线程完成
                // 3. ? 加载base_body作为底图
                var baseBodyTexture = LoadLayerTexture(personaName, "base_body");
                
                if (baseBodyTexture == null)
                {
                    // ? 只在DevMode下输出警告
                    if (Prefs.DevMode)
                    {
                        Log.Warning($"[LayeredPortraitCompositor] base_body.png not found for {personaName}");
                    }
                    return Task.FromResult<Texture2D>(null);
                }

                // ? v1.6.29: 如果是Neutral表情，直接返回底图（底图已包含睁眼闭嘴）
                if (expression == ExpressionType.Neutral)
                {
                    compositeCache[cacheKey] = baseBodyTexture;
                    return Task.FromResult(baseBodyTexture);
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
                
                // ⚠️ v1.6.80: 像素合成可以同步完成，避免跨线程问题
                // 因为纹理已在主线程加载，像素操作也必须在主线程
                Texture2D composite = CompositeAllLayers(layers);
                
                // 7. ✅ 修复：替换前先销毁旧纹理，限制缓存大小
                if (composite != null)
                {
                    // 销毁旧纹理（如果存在）
                    if (compositeCache.TryGetValue(cacheKey, out var oldTexture))
                    {
                        UnityEngine.Object.Destroy(oldTexture);
                    }
                    
                    // 限制缓存大小
                    if (compositeCache.Count >= MaxCacheSize)
                    {
                        var firstKey = compositeCache.Keys.First();
                        if (compositeCache.TryGetValue(firstKey, out var oldestTexture))
                        {
                            UnityEngine.Object.Destroy(oldestTexture);
                        }
                        compositeCache.Remove(firstKey);
                    }
                    
                    compositeCache[cacheKey] = composite;
                }
                
                return Task.FromResult(composite);
            }
            catch (Exception ex)
            {
                // ? 只在DevMode下输出错误
                if (Prefs.DevMode)
                {
                    Log.Error($"[LayeredPortraitCompositor] Composite failed: {ex}");
                }
                return Task.FromResult<Texture2D>(null);
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
                
                // 7. ✅ 修复：替换前先销毁旧纹理，限制缓存大小
                if (composite != null)
                {
                    // 销毁旧纹理（如果存在）
                    if (compositeCache.TryGetValue(cacheKey, out var oldTexture))
                    {
                        UnityEngine.Object.Destroy(oldTexture);
                    }
                    
                    // 限制缓存大小
                    if (compositeCache.Count >= MaxCacheSize)
                    {
                        var firstKey = compositeCache.Keys.First();
                        if (compositeCache.TryGetValue(firstKey, out var oldestTexture))
                        {
                            UnityEngine.Object.Destroy(oldestTexture);
                        }
                        compositeCache.Remove(firstKey);
                    }
                    
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
        /// ⭐ v1.6.74: 支持多路径回退（主 Mod 和子 Mod 路径）
        /// ? v1.6.27: 完全静默，不输出任何日志
        /// </summary>
        private static Texture2D LoadLayerTexture(string personaName, string layerName)
        {
            // ⭐ v1.6.74: 尝试多个路径（按优先级）
            string[] pathsToTry = new[]
            {
                // 路径 1: 主 Mod 路径（UI/Narrators/9x16/Layered/PersonaName/）
                $"{LAYERED_BASE_PATH}{personaName}/{layerName}",
                
                // 路径 2: 子 Mod 路径（Narrators/Layered/）- 适配扁平结构
                $"Narrators/Layered/{layerName}",
                
                // 路径 3: 旧版路径（向后兼容）
                $"UI/Narrators/Layered/{personaName}/{layerName}"
            };
            
            foreach (var texturePath in pathsToTry)
            {
                var texture = ContentFinder<Texture2D>.Get(texturePath, false);
                
                if (texture != null)
                {
                    // ⭐ 只在 DevMode 下输出成功路径（方便调试）
                    if (Prefs.DevMode)
                    {
                        Log.Message($"[LayeredPortraitCompositor] ✅ Loaded: {texturePath}");
                    }
                    return texture;
                }
            }
            
            // ⭐ 所有路径都失败，只在 DevMode 下输出警告
            if (Prefs.DevMode)
            {
                Log.Warning($"[LayeredPortraitCompositor] ❌ Not found: {layerName} for {personaName}");
                Log.Warning($"[LayeredPortraitCompositor]   Tried paths:");
                foreach (var path in pathsToTry)
                {
                    Log.Warning($"[LayeredPortraitCompositor]     • {path}");
                }
            }
            
            return null;
        }
        
        /// <summary>
        /// ? v1.6.27: 从 defName 提取人格文件夹名称
        /// 例如：YourPersona_Default → YourPersona, Cassandra_Classic → Cassandra
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
                
                // ? v1.6.83: 自动处理尺寸不匹配（通过 GetReadablePixels 缩放）
                if (layer.width != width || layer.height != height)
                {
                    if (Prefs.DevMode)
                    {
                        Log.Warning($"[LayeredPortraitCompositor] Resizing layer {layer.name}: {layer.width}x{layer.height} -> {width}x{height}");
                    }
                }
                
                try
                {
                    // 获取图层像素（使用 RenderTexture 避免 "not readable" 错误，并自动缩放）
                    Color[] layerPixels = GetReadablePixels(layer, width, height);
                    
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
        /// 获取纹理的可读像素（避免 "not readable" 错误，并支持自动缩放）
        /// </summary>
        private static Color[] GetReadablePixels(Texture2D texture, int targetWidth, int targetHeight)
        {
            // 如果尺寸匹配且可读，尝试直接读取（性能优化）
            if (texture.width == targetWidth && texture.height == targetHeight)
            {
                try
                {
                    return texture.GetPixels();
                }
                catch
                {
                    // 如果不可读，回退到 RenderTexture 方法
                }
            }

            // 使用 RenderTexture 转换（支持缩放和不可读纹理）
            RenderTexture renderTex = RenderTexture.GetTemporary(
                targetWidth,
                targetHeight,
                0,
                RenderTextureFormat.Default,
                RenderTextureReadWrite.Linear
            );
            
            // Blit 会自动处理缩放
            Graphics.Blit(texture, renderTex);
            
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = renderTex;
            
            Texture2D readable = new Texture2D(targetWidth, targetHeight, TextureFormat.RGBA32, false);
            readable.ReadPixels(new Rect(0, 0, targetWidth, targetHeight), 0, 0);
            readable.Apply();
            
            Color[] pixels = readable.GetPixels();
            
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(renderTex);
            
            return pixels;
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
        /// ✅ 修复：销毁纹理防止内存泄漏
        /// ⭐ v1.6.92: 跳过 Neutral 表情的缓存（base_body 不应被清除）
        /// </summary>
        public static void ClearCache(string personaDefName, ExpressionType expression)
        {
            // ⭐ v1.6.92: 跳过 Neutral 表情（base_body 是分层立绘的基础，不应被频繁清除）
            if (expression == ExpressionType.Neutral)
            {
                return;
            }
            
            string cacheKey = $"{personaDefName}_{expression}_default";
            
            if (compositeCache.TryGetValue(cacheKey, out var texture))
            {
                UnityEngine.Object.Destroy(texture);
                compositeCache.Remove(cacheKey);
                
                // 日志已静默
            }
        }
        
        /// <summary>
        /// 清除所有缓存
        /// ✅ 修复：销毁所有纹理防止内存泄漏
        /// </summary>
        public static void ClearAllCache()
        {
            foreach (var texture in compositeCache.Values)
            {
                if (texture != null)
                {
                    UnityEngine.Object.Destroy(texture);
                }
            }
            compositeCache.Clear();
            if (Prefs.DevMode)
            {
                Log.Message("[LayeredPortraitCompositor] All cache cleared");
            }
        }
    }
}
