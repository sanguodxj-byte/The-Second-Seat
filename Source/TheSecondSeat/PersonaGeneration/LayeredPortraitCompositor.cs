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
        private class CacheEntry
        {
            public Texture Texture;
            public bool IsOwned; // ✅ 标识是否为合成产生的纹理（需要销毁）
        }

        // ? v1.7.0: 改为 Texture 以支持 RenderTexture
        private static Dictionary<string, CacheEntry> compositeCache = new Dictionary<string, CacheEntry>();
        private static List<Texture> _staleTextures = new List<Texture>(); // 待销毁的旧纹理
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
        public static Task<Texture> CompositeLayersAsync(
            LayeredPortraitConfig config,
            ExpressionType expression = ExpressionType.Neutral,
            string outfit = "default")
        {
            // 1. 生成缓存键
            string cacheKey = $"{config.PersonaDefName}_{expression}_default";
            
            // 2. 检查缓存
            if (compositeCache.TryGetValue(cacheKey, out CacheEntry cachedEntry))
            {
                return Task.FromResult(cachedEntry.Texture);
            }

            try
            {
                // ? v1.6.27: 使用 PersonaName 而不是 PersonaDefName
                string personaName = config.PersonaName;
                
                // ⚠️ v1.6.80: 所有纹理加载必须在主线程完成
                // 3. ? 加载base_body作为底图
                var baseBodyTexture = LoadLayerTexture(config, "base_body");
                
                if (baseBodyTexture == null)
                {
                    // ? 只在DevMode下输出警告
                    if (Prefs.DevMode)
                    {
                        Log.Warning($"[LayeredPortraitCompositor] base_body.png not found for {personaName}");
                    }
                    return Task.FromResult<Texture>(null);
                }

                // ? v1.6.29: 如果是Neutral表情，直接返回底图（底图已包含睁眼闭嘴）
                // 注意：这里返回的是 Texture2D (资源)，不需要 Release
                if (expression == ExpressionType.Neutral)
                {
                    compositeCache[cacheKey] = new CacheEntry
                    {
                        Texture = baseBodyTexture,
                        IsOwned = false // ✅ 共享资源，不可销毁
                    };
                    return Task.FromResult<Texture>(baseBodyTexture);
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
                    var eyesTexture = LoadLayerTexture(config, eyesLayerName);
                    if (eyesTexture != null)
                    {
                        layers.Add(eyesTexture);
                    }
                }
                
                if (mouthLayerName != "opened_mouth")
                {
                    var mouthTexture = LoadLayerTexture(config, mouthLayerName);
                    if (mouthTexture != null)
                    {
                        layers.Add(mouthTexture);
                    }
                }
                
                // 5. ? 可选：腮红/特效层
                var flushTexture = LoadLayerTexture(config, $"{GetExpressionPrefix(expression)}_flush");
                if (flushTexture != null)
                {
                    layers.Add(flushTexture);
                }
                
                // ⚠️ v1.7.0: 使用 GPU 渲染系统
                // 返回的是 RenderTexture
                Texture composite = PortraitRenderSystem.CompositeLayers(layers);
                
                // 7. ✅ 修复：替换前先销毁旧纹理，限制缓存大小
                if (composite != null)
                {
                    // 销毁旧纹理（如果存在）
                    if (compositeCache.TryGetValue(cacheKey, out var oldEntry))
                    {
                        if (oldEntry.IsOwned)
                        {
                            ReleaseTexture(oldEntry.Texture);
                        }
                    }
                    
                    // 限制缓存大小
                    if (compositeCache.Count >= MaxCacheSize)
                    {
                        var firstKey = compositeCache.Keys.First();
                        if (compositeCache.TryGetValue(firstKey, out var oldestEntry))
                        {
                            if (oldestEntry.IsOwned)
                            {
                                ReleaseTexture(oldestEntry.Texture);
                            }
                        }
                        compositeCache.Remove(firstKey);
                    }
                    
                    compositeCache[cacheKey] = new CacheEntry
                    {
                        Texture = composite,
                        IsOwned = true // ✅ 合成纹理，属于我们，需要销毁
                    };

                    // 清理待销毁的旧纹理 (在生成新纹理后销毁，避免闪烁)
                    if (_staleTextures.Count > 0)
                    {
                        for (int i = _staleTextures.Count - 1; i >= 0; i--)
                        {
                            ReleaseTexture(_staleTextures[i]);
                        }
                        _staleTextures.Clear();
                    }
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
                return Task.FromResult<Texture>(null);
            }
        }
        
        /// <summary>
        /// 合成分层立绘（同步版本，已废弃）
        /// ?? 警告：此方法会卡主线程，请使用 CompositeLayersAsync
        /// ? v1.6.29: Neutral表情直接使用base_body（底图已包含睁眼闭嘴）
        /// </summary>
        [Obsolete("Use CompositeLayersAsync instead to avoid blocking the main thread")]
        public static Texture CompositeLayers(
            LayeredPortraitConfig config,
            ExpressionType expression = ExpressionType.Neutral,
            string outfit = "default")
        {
            // 1. 生成缓存键
            string cacheKey = $"{config.PersonaDefName}_{expression}_default";
            
            // 2. 检查缓存
            if (compositeCache.TryGetValue(cacheKey, out CacheEntry cachedEntry))
            {
                return cachedEntry.Texture;
            }

            try
            {
                // ? v1.6.27: 使用 PersonaName 而不是 PersonaDefName
                string personaName = config.PersonaName;
                
                // 3. ? 加载base_body作为底图
                var baseBodyTexture = LoadLayerTexture(config, "base_body");
                
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
                    compositeCache[cacheKey] = new CacheEntry
                    {
                        Texture = baseBodyTexture,
                        IsOwned = false // ✅ 共享资源
                    };
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
                    var eyesTexture = LoadLayerTexture(config, eyesLayerName);
                    if (eyesTexture != null)
                    {
                        layers.Add(eyesTexture);
                    }
                }
                
                if (mouthLayerName != "opened_mouth")
                {
                    var mouthTexture = LoadLayerTexture(config, mouthLayerName);
                    if (mouthTexture != null)
                    {
                        layers.Add(mouthTexture);
                    }
                }
                
                // 5. ? 可选：腮红/特效层
                var flushTexture = LoadLayerTexture(config, $"{GetExpressionPrefix(expression)}_flush");
                if (flushTexture != null)
                {
                    layers.Add(flushTexture);
                }
                
                // 6. 合成所有层 (GPU)
                Texture composite = PortraitRenderSystem.CompositeLayers(layers);
                
                // 7. ✅ 修复：替换前先销毁旧纹理，限制缓存大小
                if (composite != null)
                {
                    // 销毁旧纹理（如果存在）
                    if (compositeCache.TryGetValue(cacheKey, out var oldEntry))
                    {
                        if (oldEntry.IsOwned)
                        {
                            ReleaseTexture(oldEntry.Texture);
                        }
                    }
                    
                    // 限制缓存大小
                    if (compositeCache.Count >= MaxCacheSize)
                    {
                        var firstKey = compositeCache.Keys.First();
                        if (compositeCache.TryGetValue(firstKey, out var oldestEntry))
                        {
                            if (oldestEntry.IsOwned)
                            {
                                ReleaseTexture(oldestEntry.Texture);
                            }
                        }
                        compositeCache.Remove(firstKey);
                    }
                    
                    compositeCache[cacheKey] = new CacheEntry
                    {
                        Texture = composite,
                        IsOwned = true // ✅ 合成纹理
                    };
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
        /// ⭐ 根据表情类型获取眼睛层名称
        /// ⭐ v1.8.1: 修复 Confused 使用 confused_eyes，支持更多眼睛纹理
        /// </summary>
        private static string GetEyesLayerName(ExpressionType expression)
        {
            return expression switch
            {
                ExpressionType.Neutral => "opened_eyes",   // 中性表情睁眼（使用 base_body 默认）
                ExpressionType.Happy => "happy_eyes",      // 开心眼
                ExpressionType.Sad => "sad_eyes",          // 悲伤眼
                ExpressionType.Angry => "angry_eyes",      // 愤怒眼
                ExpressionType.Surprised => "opened_eyes", // 惊讶睁大眼睛
                ExpressionType.Confused => "confused_eyes",// ⭐ 修复：困惑眼
                ExpressionType.Smug => "happy_eyes",       // 得意用开心眼
                ExpressionType.Shy => "closed_eyes",       // ⭐ 害羞闭眼
                _ => "opened_eyes"
            };
        }

        /// <summary>
        /// ⭐ 根据表情类型获取嘴巴层名称
        /// ⭐ v1.8.1: 修复映射，与 Sideria 纹理文件名对齐
        /// </summary>
        private static string GetMouthLayerName(ExpressionType expression)
        {
            return expression switch
            {
                ExpressionType.Neutral => "Closed_mouth",   // ⭐ 修复：中性表情闭嘴
                ExpressionType.Happy => "happy_mouth",      // ⭐ 修复：开心用 happy_mouth
                ExpressionType.Sad => "sad_mouth",          // 悲伤嘴
                ExpressionType.Angry => "angry_mouth",      // 愤怒嘴
                ExpressionType.Surprised => "larger_mouth", // 惊讶张大嘴
                ExpressionType.Confused => "Neutral_mouth", // ⭐ 修复：困惑用微张嘴
                ExpressionType.Smug => "Neutral_mouth",     // ⭐ 修复：得意用微张嘴
                ExpressionType.Shy => "Closed_mouth",       // ⭐ 修复：害羞闭嘴
                _ => "Closed_mouth"
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
        /// ✅ v1.7.1: 优先使用 PortraitLoader.GetLayerTexture 以支持 portraitPath
        /// ? v1.6.27: 完全静默，不输出任何日志
        /// </summary>
        private static Texture2D LoadLayerTexture(LayeredPortraitConfig config, string layerName)
        {
            // 1. 尝试通过 Def 获取（支持 portraitPath）
            if (!string.IsNullOrEmpty(config.PersonaDefName))
            {
                var def = DefDatabase<NarratorPersonaDef>.GetNamedSilentFail(config.PersonaDefName);
                if (def != null)
                {
                    // 使用 PortraitLoader 的高级路径查找逻辑
                    return PortraitLoader.GetLayerTexture(def, layerName);
                }
            }

            // 2. 回退到基于名称的查找（旧逻辑）
            string personaName = config.PersonaName;
            
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
        /// 释放纹理资源
        /// </summary>
        private static void ReleaseTexture(Texture texture)
        {
            if (texture == null) return;

            // 如果是 RenderTexture，使用专用释放方法
            if (texture is RenderTexture rt)
            {
                PortraitRenderSystem.Release(rt);
            }
            // 如果是 Texture2D，且不是资源（例如旧的合成结果），可能需要 Destroy
            // 但在新管线中，Texture2D 通常是资源，不应 Destroy
            // 为了安全起见，这里不 Destroy Texture2D，依靠 Unity 资源管理
        }
        
        /// <summary>
        /// ⭐ v1.6.27: 预加载所有表情到缓存（加载存档时调用）
        /// ⭐ v1.8.2: 新增口型图层预加载（A_mouth, E_mouth, O_mouth, U_mouth）
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
            
            // ⭐ v1.8.2: 预加载口型同步专用图层（TTS 唇形同步用）
            PreloadVisemeLayers(config);
            
            if (Prefs.DevMode && loadedCount > 0)
            {
                Log.Message($"[LayeredPortraitCompositor] Preloaded {loadedCount} expressions for {config.PersonaName}");
            }
        }
        
        /// <summary>
        /// ⭐ v1.8.2: 预加载口型图层（TTS 唇形同步用）
        /// 口型图层：A_mouth, E_mouth, O_mouth, U_mouth, Closed_mouth, Neutral_mouth
        /// </summary>
        public static void PreloadVisemeLayers(LayeredPortraitConfig config)
        {
            if (config == null) return;
            
            // 口型图层列表（与 VisemeHelper.VisemeToTextureName 对应）
            string[] visemeLayers = new[]
            {
                "A_mouth",        // Large - 大张嘴
                "E_mouth",        // Smile - 咧嘴
                "O_mouth",        // OShape - 圆嘴
                "U_mouth",        // Medium - 嘟嘴
                "Closed_mouth",   // Closed - 闭嘴
                "Neutral_mouth"   // Small - 微张
            };
            
            int loadedCount = 0;
            
            foreach (var layerName in visemeLayers)
            {
                try
                {
                    var texture = LoadLayerTexture(config, layerName);
                    if (texture != null)
                    {
                        loadedCount++;
                        if (Prefs.DevMode)
                        {
                            Log.Message($"[LayeredPortraitCompositor] ✅ 预加载口型图层: {layerName} ({texture.width}x{texture.height})");
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (Prefs.DevMode)
                    {
                        Log.Warning($"[LayeredPortraitCompositor] 口型图层加载失败: {layerName} - {ex.Message}");
                    }
                }
            }
            
            if (Prefs.DevMode && loadedCount > 0)
            {
                Log.Message($"[LayeredPortraitCompositor] 预加载 {loadedCount} 个口型图层 for {config.PersonaName}");
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
            
            if (compositeCache.TryGetValue(cacheKey, out var entry))
            {
                // ✅ 修复：不要立即销毁，而是加入待销毁列表，等待新纹理生成后再销毁
                // UnityEngine.Object.Destroy(texture);
                if (entry.Texture != null && entry.IsOwned)
                {
                    _staleTextures.Add(entry.Texture);
                }
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
            foreach (var entry in compositeCache.Values)
            {
                if (entry.Texture != null && entry.IsOwned)
                {
                    ReleaseTexture(entry.Texture);
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
