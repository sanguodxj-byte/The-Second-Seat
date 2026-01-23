using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Verse;
using RimWorld;
using TheSecondSeat.Storyteller;

namespace TheSecondSeat.PersonaGeneration
{
    /// <summary>
    /// 立绘来源枚举
    /// </summary>
    public enum PortraitSource
    {
        Vanilla,    // 原版游戏
        ThisMod,    // 本 Mod
        OtherMod,   // 其他 Mod
        User        // 用户自定义
    }
    
    /// <summary>
    /// 立绘文件信息
    /// </summary>
    public class PortraitFileInfo
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public PortraitSource Source { get; set; }
        public Texture Texture { get; set; }
        public string ModName { get; set; }
    }
    
    /// <summary>
    /// 立绘加载管理器
    /// ✅ v1.6.27: 消除未找到立绘时的报错日志
    /// ✅ v1.12.0: 添加全局空值防护和失败计数，失败3次后退回默认纹理
    /// </summary>
    public static class PortraitLoader
    {
        // ✅ 修复内存泄漏：添加缓存条目类和大小限制
        private class CacheEntry
        {
            public Texture Texture;
            public int LastAccessTick;
            public bool IsOwned; // ✅ 标识是否为该类拥有的资源（需要销毁）
        }
        
        private static Dictionary<string, CacheEntry> cache = new Dictionary<string, CacheEntry>();
        private const int MaxCacheSize = 50; // 最大缓存数量
        
        // ⭐ v1.12.0: 全局失败计数器 - 跟踪每个纹理路径的失败次数
        private static Dictionary<string, int> failureCounter = new Dictionary<string, int>();
        private const int MaxFailureCount = 3; // 失败阈值：3次后退回默认
        private static Texture2D _fallbackPlaceholder = null; // 全局默认占位符（复用）
        
        private const string BASE_PATH_9x16 = "UI/Narrators/9x16/";
        private const string EXPRESSIONS_PATH = "UI/Narrators/9x16/Expressions/";
        
        /// <summary>
        /// 加载立绘（支持 Mod资源、外部文件、占位符）
        /// ✅ 消除未找到立绘时的报错日志
        /// ✅ v1.6.28: 分层立绘直接调用LayeredPortraitCompositor，不单独缓存
        /// ? v1.7.0: 返回类型改为 Texture 以支持 RenderTexture
        /// </summary>
        public static Texture LoadPortrait(NarratorPersonaDef def, ExpressionType? expression = null)
        {
            if (def == null)
            {
                return GeneratePlaceholder(Color.gray);
            }
            
            // ✅ 新增：如果启用了分层立绘系统，直接调用LayeredPortraitCompositor
            // LayeredPortraitCompositor 内部有完整的预加载和缓存机制
            if (def.useLayeredPortrait)
            {
                return LoadLayeredPortrait(def, expression);
            }
            
            // 确定表情后缀
            string expressionSuffix = "";
            if (expression.HasValue && expression.Value != ExpressionType.Neutral)
            {
                expressionSuffix = ExpressionSystem.GetExpressionSuffix(def.defName, expression.Value);
            }
            
            // 1. 检查缓存（包含表情后缀）
            string cacheKey = $"{def.defName}_portrait_{expressionSuffix}";
            if (cache.TryGetValue(cacheKey, out CacheEntry cached))
            {
                // ✅ 更新访问时间
                cached.LastAccessTick = Find.TickManager.TicksGame;
                return cached.Texture;
            }
            
            Texture texture = null;
            bool isOwned = false;
            
            // 2. ✅ 修复：使用统一的加载方法，静默处理失败
            var result = LoadExpressionOrBase(def, expression);
            texture = result.Item1;
            isOwned = result.Item2;
            
            // 3. 如果还是没有，生成占位符
            if (texture == null)
            {
                // ✅ 静默处理：未找到立绘时直接使用占位符，不报错
                texture = GeneratePlaceholder(def.primaryColor);
                isOwned = true; // 占位符是新创建的纹理，需要销毁
            }
            
            // ✅ 修复：检查缓存大小，必要时清理
            if (cache.Count >= MaxCacheSize)
            {
                CleanOldCache();
            }
            
            // 缓存
            cache[cacheKey] = new CacheEntry
            {
                Texture = texture,
                LastAccessTick = Find.TickManager.TicksGame,
                IsOwned = isOwned
            };
            return texture;
        }

        /// <summary>
        /// ✅ 加载分层立绘（新增）
        /// ✅ v1.6.27: 静默处理失败，完全移除成功日志
        /// ✅ v1.6.28: 修复缓存键不一致问题，复用LayeredPortraitCompositor的缓存
        /// ? v1.7.0: 返回类型改为 Texture 以支持 RenderTexture
        /// </summary>
        private static Texture LoadLayeredPortrait(NarratorPersonaDef def, ExpressionType? expression)
        {
            try
            {
                // 获取分层配置
                var config = def.GetLayeredConfig();
                if (config == null)
                {
                    return GeneratePlaceholder(def.primaryColor);
                }
                
                // ✅ v1.6.20: 优先从 ExpressionSystem 获取当前表情
                // ⭐ v1.6.93: 增加空引用检查，防止 ExpressionSystem 未初始化时崩溃
                var exprState = ExpressionSystem.GetExpressionState(def.defName);
                ExpressionType currentExpression = expression ?? exprState?.CurrentExpression ?? ExpressionType.Neutral;
                string currentOutfit = "default";
                
                // ✅ v1.6.28: 不再在PortraitLoader中维护缓存，直接调用LayeredPortraitCompositor
                // LayeredPortraitCompositor内部已经有完整的缓存机制
                // ✅ v1.6.81: 使用 #pragma warning 抑制CS0618警告（同步方法内部调用已废弃方法）
                #pragma warning disable CS0618
                Texture composite = LayeredPortraitCompositor.CompositeLayers(
                    config,
                    currentExpression,
                    currentOutfit
                );
                #pragma warning restore CS0618
                
                if (composite == null)
                {
                    // ✅ 静默处理合成失败
                    return GeneratePlaceholder(def.primaryColor);
                }
                
                // ✅ v1.6.27: 完全移除成功日志，避免连续输出
                
                return composite;
            }
            catch (Exception)
            {
                // ✅ 静默处理异常
                return GeneratePlaceholder(def.primaryColor);
            }
        }
        
        /// <summary>
        /// ✅ 统一的表情/基础立绘加载方法（带3层回退机制）
        /// 回退顺序：
        /// 1. 尝试具体变体（如 _happy3）
        /// 2. 回退到通用表情（如 _happy）
        /// 3. 回退到面部覆盖模式（同样支持变体回退）
        /// 4. 加载基础立绘
        /// ✅ 优化：静默回退，不刷屏日志
        /// ? v1.7.0: 返回类型改为 Texture
        /// </summary>
        private static (Texture, bool) LoadExpressionOrBase(NarratorPersonaDef def, ExpressionType? expression)
        {
            string personaName = GetPersonaFolderName(def);
            
            // 1. 如果有表情需求，先尝试加载表情立绘
            if (expression.HasValue && expression.Value != ExpressionType.Neutral)
            {
                string expressionSuffix = ExpressionSystem.GetExpressionSuffix(def.defName, expression.Value);
                
                // ✅ 使用带回退的加载方法（静默）
                var expressionTexture = TryLoadTextureWithFallback(personaName, expressionSuffix);
                
                if (expressionTexture != null)
                {
                    SetTextureQuality(expressionTexture);
                    // ✅ 移除成功日志，静默返回
                    // ContentFinder 加载的纹理不属于我们，不需要销毁
                    return (expressionTexture, false);
                }
                
                // ✅ 尝试面部覆盖模式（静默）
                var overlayTexture = LoadWithFaceOverlayAndFallback(def, expression.Value);
                if (overlayTexture != null)
                {
                    // 合成后的纹理属于我们，需要销毁
                    return (overlayTexture, true);
                }
                
                // ✅ 移除回退警告，静默继续
            }
            
            // 2. 没有表情或表情文件不存在，加载基础立绘
            var baseResult = LoadBasePortrait(def);
            return (baseResult.Item1, baseResult.Item2);
        }
        
        /// <summary>
        /// ✅ 带回退机制的纹理加载方法
        /// 回退顺序：具体变体 → 递减变体 → 通用表情
        /// 例如：_happy3 → _happy2 → _happy1 → _happy
        /// ✅ 优化：静默回退，不输出中间日志
        /// </summary>
        /// <param name="personaName">人格文件夹名称</param>
        /// <param name="suffix">表情后缀（可能包含变体编号，如 _happy3）</param>
        /// <returns>加载的纹理，如果全部失败则返回 null</returns>
        private static Texture2D TryLoadTextureWithFallback(string personaName, string suffix)
        {
            if (string.IsNullOrEmpty(suffix))
            {
                return null;
            }
            
            // 转换后缀为文件名（去掉前缀 _，转小写）
            string fileName = suffix.TrimStart('_').ToLower();
            
            // ✅ 第1层：尝试加载具体变体（如 happy3）
            string specificPath = $"{EXPRESSIONS_PATH}{personaName}/{fileName}";
            var texture = ContentFinder<Texture2D>.Get(specificPath, false);
            
            if (texture != null)
            {
                return texture;
            }

            // ✅ 第1.5层：尝试递减变体（如 happy3 -> happy2 -> happy1）
            // 提取基础名称和变体编号
            string baseName = StripVariantNumber(fileName);
            int variantNum = GetVariantNumber(fileName);
            
            if (variantNum > 1)
            {
                // 从当前变体编号递减尝试
                for (int i = variantNum - 1; i >= 1; i--)
                {
                    string fallbackName = $"{baseName}{i}";
                    string fallbackPath = $"{EXPRESSIONS_PATH}{personaName}/{fallbackName}";
                    texture = ContentFinder<Texture2D>.Get(fallbackPath, false);
                    
                    if (texture != null)
                    {
                        return texture;
                    }
                }
            }
            
            // ✅ 第2层：回退到通用表情（去掉变体编号）
            if (baseName != fileName)  // 确实有变体编号被去掉了
            {
                string genericPath = $"{EXPRESSIONS_PATH}{personaName}/{baseName}";
                texture = ContentFinder<Texture2D>.Get(genericPath, false);
                
                if (texture != null)
                {
                    return texture;
                }
            }
            
            // ✅ 全部失败，静默返回 null
            return null;
        }

        /// <summary>
        /// 从文件名中提取变体编号
        /// </summary>
        private static int GetVariantNumber(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return 0;
            
            int lastIndex = fileName.Length - 1;
            string numStr = "";
            
            while (lastIndex >= 0 && char.IsDigit(fileName[lastIndex]))
            {
                numStr = fileName[lastIndex] + numStr;
                lastIndex--;
            }
            
            if (int.TryParse(numStr, out int result))
            {
                return result;
            }
            return 0;
        }
        
        /// <summary>
        /// ✅ 去掉表情文件名末尾的变体编号
        /// 例如：happy3 → happy, sad1 → sad, angry → angry
        /// </summary>
        private static string StripVariantNumber(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                return fileName;
            }
            
            // 从末尾查找数字并去掉
            int lastIndex = fileName.Length - 1;
            
            while (lastIndex >= 0 && char.IsDigit(fileName[lastIndex]))
            {
                lastIndex--;
            }
            
            // 如果整个字符串都是数字，或者没有数字，返回原字符串
            if (lastIndex < 0 || lastIndex == fileName.Length - 1)
            {
                return fileName;
            }
            
            return fileName.Substring(0, lastIndex + 1);
        }
        
        /// <summary>
        /// ✅ 带回退机制的面部覆盖加载（支持变体回退）
        /// 回退顺序：_happy3_face → _happy_face
        /// ✅ 优化：静默回退，不刷屏日志
        /// ? v1.7.0: 返回类型改为 Texture
        /// </summary>
        private static Texture LoadWithFaceOverlayAndFallback(NarratorPersonaDef def, ExpressionType expression)
        {
            try
            {
                // 1. 加载基础立绘
                var (baseTexture, _) = LoadBasePortrait(def);
                if (baseTexture == null)
                {
                    return null;
                }
                
                // 2. 获取面部后缀
                string expressionSuffix = ExpressionSystem.GetExpressionSuffix(def.defName, expression);
                string personaName = GetPersonaFolderName(def);
                
                // ✅ 构建面部覆盖后缀（带变体）
                string faceSuffix = expressionSuffix + "_face";
                string faceFileName = faceSuffix.TrimStart('_').ToLower();
                
                // ✅ 第1层：尝试具体变体面部（如 happy3_face）
                string specificFacePath = $"{EXPRESSIONS_PATH}{personaName}/{faceFileName}";
                Texture2D faceTexture = ContentFinder<Texture2D>.Get(specificFacePath, false);
                
                // ✅ 第2层：回退到通用面部（如 happy_face）
                if (faceTexture == null)
                {
                    string genericFaceFileName = StripVariantNumber(faceFileName.Replace("_face", "")) + "_face";
                    
                    if (genericFaceFileName != faceFileName)
                    {
                        string genericFacePath = $"{EXPRESSIONS_PATH}{personaName}/{genericFaceFileName}";
                        faceTexture = ContentFinder<Texture2D>.Get(genericFacePath, false);
                        
                        // ✅ 移除回退成功日志，静默返回
                    }
                }
                
                // 如果没有面部叠加层，返回null
                if (faceTexture == null)
                {
                    return null;
                }
                
                // 3. 获取面部区域坐标
                var faceRegion = PersonaFaceRegions.GetFaceRegion(def.defName);
                
                // 4. 合成表情
                string cacheKey = def.defName + faceSuffix + "_composite";
                // ? v1.7.0: ExpressionCompositor 现在返回 Texture (可能是 RenderTexture)
                Texture composite = ExpressionCompositor.CompositeExpression(
                    baseTexture,
                    faceTexture,
                    faceRegion,
                    cacheKey
                );
                
                // ✅ 移除合成成功日志
                return composite;
            }
            catch (Exception)
            {
                // ✅ 静默处理合成失败
                return null;
            }
        }
        
        /// <summary>
        /// 加载基础立绘（无表情）
        /// ✅ v1.6.27: 静默处理失败，只在DevMode下输出
        /// ✅ v1.6.55: 增强缓存检查，避免重复加载
        /// </summary>
        private static (Texture2D, bool) LoadBasePortrait(NarratorPersonaDef def)
        {
            string personaName = GetPersonaFolderName(def);
            
            // ✅ 在尝试加载前，先检查是否已在缓存中
            string baseCacheKey = $"{personaName}_base";
            if (cache.TryGetValue(baseCacheKey, out CacheEntry cachedBase))
            {
                cachedBase.LastAccessTick = Find.TickManager.TicksGame;
                return (cachedBase.Texture as Texture2D, cachedBase.IsOwned);
            }
            
            // ✅ 尝试路径1：9x16文件夹的 base.png
            string basePath = $"{BASE_PATH_9x16}{personaName}/base";
            var texture = ContentFinder<Texture2D>.Get(basePath, false);
            
            if (texture != null)
            {
                SetTextureQuality(texture);
                // ✅ 缓存基础纹理 (ContentFinder 资源，不拥有)
                cache[baseCacheKey] = new CacheEntry
                {
                    Texture = texture,
                    LastAccessTick = Find.TickManager.TicksGame,
                    IsOwned = false
                };
                return (texture, false);
            }
            
            // ✅ 尝试路径2：直接用 personaName（不加 /base）
            string path2 = $"{BASE_PATH_9x16}{personaName}";
            texture = ContentFinder<Texture2D>.Get(path2, false);
            if (texture != null)
            {
                SetTextureQuality(texture);
                cache[baseCacheKey] = new CacheEntry
                {
                    Texture = texture,
                    LastAccessTick = Find.TickManager.TicksGame,
                    IsOwned = false
                };
                return (texture, false);
            }
            
            // ✅ 尝试路径3：使用 portraitPath
            if (!string.IsNullOrEmpty(def.portraitPath))
            {
                texture = ContentFinder<Texture2D>.Get(def.portraitPath, false);
                if (texture != null)
                {
                    SetTextureQuality(texture);
                    cache[baseCacheKey] = new CacheEntry
                    {
                        Texture = texture,
                        LastAccessTick = Find.TickManager.TicksGame,
                        IsOwned = false
                    };
                    return (texture, false);
                }
            }
            
            // 未找到任何立绘
            return (null, false);
        }
        
        /// <summary>
        /// 获取人格文件夹名称
        /// </summary>
        private static string GetPersonaFolderName(NarratorPersonaDef def)
        {
            if (!string.IsNullOrEmpty(def.narratorName))
            {
                // 取第一个单词（如 "Cassandra Classic" → "Cassandra"）
                return def.narratorName.Split(' ')[0].Trim();
            }
            
            string defName = def.defName;
            string[] suffixesToRemove = new[] { "_Default", "_Classic", "_Custom", "_Persona" };
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
        /// 设置纹理高质量参数
        /// </summary>
        private static void SetTextureQuality(Texture2D texture)
        {
            if (texture == null) return;
            
            try
            {
                texture.filterMode = FilterMode.Bilinear;
                texture.anisoLevel = 4;
            }
            catch
            {
                // 静默忽略
            }
        }
        
        /// <summary>
        /// 生成占位符纹理
        /// </summary>
        private static Texture2D GeneratePlaceholder(Color color)
        {
            int size = 512;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            
            // 简单渐变
            Color darkColor = color * 0.3f;
            Color lightColor = color * 1.2f;
            
            for (int y = 0; y < size; y++)
            {
                float t = y / (float)size;
                Color gradientColor = Color.Lerp(darkColor, lightColor, t);
                
                for (int x = 0; x < size; x++)
                {
                    texture.SetPixel(x, y, gradientColor);
                }
            }
            
            texture.Apply();
            return texture;
        }
        
        /// <summary>
        /// ⭐ v1.12.0: 获取全局默认占位符（复用单例，避免重复创建）
        /// </summary>
        public static Texture2D GetFallbackPlaceholder()
        {
            if (_fallbackPlaceholder == null)
            {
                _fallbackPlaceholder = GeneratePlaceholder(new Color(0.3f, 0.3f, 0.4f)); // 深灰蓝色
            }
            return _fallbackPlaceholder;
        }
        
        /// <summary>
        /// ⭐ v1.12.0: 带失败计数的纹理加载方法（核心防护）
        /// 失败3次后直接返回默认占位符，不再尝试加载
        /// </summary>
        /// <param name="texturePath">纹理路径</param>
        /// <param name="fallbackColor">失败时占位符颜色（可选）</param>
        /// <returns>加载的纹理或占位符</returns>
        public static Texture2D TryLoadTextureWithFailureCount(string texturePath, Color? fallbackColor = null)
        {
            if (string.IsNullOrEmpty(texturePath))
            {
                return GetFallbackPlaceholder();
            }
            
            // ⭐ 检查失败计数：如果已经失败3次，直接返回默认
            if (failureCounter.TryGetValue(texturePath, out int count) && count >= MaxFailureCount)
            {
                // 已达到失败阈值，静默返回占位符
                return GetFallbackPlaceholder();
            }
            
            // 尝试加载
            var texture = ContentFinder<Texture2D>.Get(texturePath, false);
            
            if (texture != null)
            {
                // 成功加载，清除失败计数
                failureCounter.Remove(texturePath);
                return texture;
            }
            
            // 加载失败，增加计数
            if (failureCounter.ContainsKey(texturePath))
            {
                failureCounter[texturePath]++;
            }
            else
            {
                failureCounter[texturePath] = 1;
            }
            
            // 只在首次失败时输出警告（DevMode）
            if (failureCounter[texturePath] == 1 && Prefs.DevMode)
            {
                Log.Warning($"[PortraitLoader] 纹理加载失败 (1/{MaxFailureCount}): {texturePath}");
            }
            else if (failureCounter[texturePath] == MaxFailureCount && Prefs.DevMode)
            {
                Log.Warning($"[PortraitLoader] 纹理加载失败已达阈值，将使用默认占位符: {texturePath}");
            }
            
            // 返回占位符
            return fallbackColor.HasValue 
                ? GeneratePlaceholder(fallbackColor.Value) 
                : GetFallbackPlaceholder();
        }
        
        /// <summary>
        /// ⭐ v1.12.0: 重置特定路径的失败计数（例如用户修复了纹理文件后）
        /// </summary>
        public static void ResetFailureCount(string texturePath)
        {
            if (!string.IsNullOrEmpty(texturePath))
            {
                failureCounter.Remove(texturePath);
            }
        }
        
        /// <summary>
        /// ⭐ v1.12.0: 重置所有失败计数
        /// </summary>
        public static void ResetAllFailureCounts()
        {
            failureCounter.Clear();
            if (Prefs.DevMode)
            {
                Log.Message("[PortraitLoader] 所有纹理失败计数已重置");
            }
        }
        
        /// <summary>
        /// 清空缓存
        /// ✅ 修复：显式销毁拥有的纹理资源，防止内存泄漏
        /// </summary>
        public static void ClearCache()
        {
            foreach (var entry in cache.Values)
            {
                if (entry.IsOwned && entry.Texture != null)
                {
                    UnityEngine.Object.Destroy(entry.Texture);
                }
            }
            cache.Clear();
            
            if (Prefs.DevMode)
            {
                Log.Message("[PortraitLoader] 立绘缓存已清空 (资源已释放)");
            }
        }
        
        /// <summary>
        /// ⭐ 清除特定人格和表情的缓存
        /// ⭐ v1.6.92: 支持按人格+表情清除特定缓存条目
        /// </summary>
        public static void ClearPortraitCache(string personaDefName, ExpressionType expression)
        {
            if (string.IsNullOrEmpty(personaDefName)) return;
            
            string expressionSuffix = ExpressionSystem.GetExpressionSuffix(personaDefName, expression);
            string cacheKey = $"{personaDefName}_portrait_{expressionSuffix}";
            
            if (cache.TryGetValue(cacheKey, out CacheEntry entry))
            {
                if (entry.IsOwned && entry.Texture != null)
                {
                    UnityEngine.Object.Destroy(entry.Texture);
                }
                cache.Remove(cacheKey);
            }
        }
        
        /// <summary>
        /// ⭐ 清理过期缓存（公共方法）
        /// ⭐ v1.6.76: 从 private 改为 public 以供 NarratorManager 调用
        /// </summary>
        public static void CleanOldCache()
        {
            if (cache.Count < MaxCacheSize / 2)
            {
                return; // 缓存未满一半，不需要清理
            }
            
            // 找出最久未访问的条目
            var entriesToRemove = new List<string>();
            int currentTick = Find.TickManager?.TicksGame ?? 0;
            int maxAge = 60000 * 5; // 5 天游戏时间
            
            foreach (var kvp in cache)
            {
                if (currentTick - kvp.Value.LastAccessTick > maxAge)
                {
                    entriesToRemove.Add(kvp.Key);
                }
            }
            
            // 如果按时间清理后仍然过多，按访问时间排序后清理
            if (cache.Count - entriesToRemove.Count > MaxCacheSize)
            {
                var sortedEntries = new List<KeyValuePair<string, CacheEntry>>(cache);
                sortedEntries.Sort((a, b) => a.Value.LastAccessTick.CompareTo(b.Value.LastAccessTick));
                
                int toRemove = cache.Count - MaxCacheSize / 2;
                for (int i = 0; i < toRemove && i < sortedEntries.Count; i++)
                {
                    if (!entriesToRemove.Contains(sortedEntries[i].Key))
                    {
                        entriesToRemove.Add(sortedEntries[i].Key);
                    }
                }
            }
            
            // 执行清理
            foreach (var key in entriesToRemove)
            {
                if (cache.TryGetValue(key, out CacheEntry entry))
                {
                    // 只销毁我们拥有的资源
                    if (entry.IsOwned && entry.Texture != null)
                    {
                        UnityEngine.Object.Destroy(entry.Texture);
                    }
                    cache.Remove(key);
                }
            }
            
            if (Prefs.DevMode && entriesToRemove.Count > 0)
            {
                Log.Message($"[PortraitLoader] 清理了 {entriesToRemove.Count} 个过期缓存条目");
            }
        }
        
        /// <summary>
        /// ⭐ 获取图层纹理（用于分层立绘系统）
        /// ⭐ v1.6.74: 支持多路径查找和 portraitPath
        /// ✅ v1.12.0: 添加失败计数机制，失败3次后返回默认占位符
        /// </summary>
        /// <param name="persona">人格定义</param>
        /// <param name="layerName">图层名称（如 base_body, opened_eyes, closed_mouth）</param>
        /// <param name="suppressWarning">是否抑制警告日志</param>
        /// <returns>纹理，如果未找到且失败次数未达阈值返回 null，达到阈值后返回占位符</returns>
        public static Texture2D GetLayerTexture(NarratorPersonaDef persona, string layerName, bool suppressWarning = false)
        {
            if (persona == null || string.IsNullOrEmpty(layerName)) return GetFallbackPlaceholder();
            
            string personaName = GetPersonaFolderName(persona);
            
            // 缓存键：包含人格名和图层名
            string cacheKey = $"layer_{personaName}_{layerName}";
            
            // ✅ v1.12.0: 检查失败计数阈值
            if (failureCounter.TryGetValue(cacheKey, out int count) && count >= MaxFailureCount)
            {
                // 已达到失败阈值，静默返回 null (避免紫色色块)
                return null;
            }
            
            if (cache.TryGetValue(cacheKey, out CacheEntry cached))
            {
                cached.LastAccessTick = Find.TickManager?.TicksGame ?? 0;
                return cached.Texture as Texture2D;
            }
            
            // 多路径查找
            string[] pathsToTry = new[]
            {
                // 路径 1: Layered 文件夹（主 Mod）
                $"UI/Narrators/9x16/Layered/{personaName}/{layerName}",
                
                // ⭐ 路径 2: 子 Mod 路径（Sideria 格式）
                $"{personaName}/Narrators/Layered/{layerName}",
                
                // 路径 3: 子 Mod 路径（扁平结构）
                $"Narrators/Layered/{layerName}",
                
                // 路径 4: 使用 portraitPath 的基础目录
                !string.IsNullOrEmpty(persona.portraitPath)
                    ? $"{Path.GetDirectoryName(persona.portraitPath)?.Replace("\\", "/")}/{layerName}"
                    : null,
                
                // 路径 5: 通用回退
                $"UI/Narrators/Layered/{personaName}/{layerName}"
            };
            
            foreach (var path in pathsToTry)
            {
                if (string.IsNullOrEmpty(path)) continue;
                
                var texture = ContentFinder<Texture2D>.Get(path, false);
                if (texture != null)
                {
                    // ✅ 成功加载，清除失败计数
                    failureCounter.Remove(cacheKey);
                    
                    // 缓存并返回
                    cache[cacheKey] = new CacheEntry
                    {
                        Texture = texture,
                        LastAccessTick = Find.TickManager?.TicksGame ?? 0,
                        IsOwned = false // ContentFinder 加载的不属于我们
                    };
                    return texture;
                }
            }
            
            // ✅ v1.12.0: 所有路径都失败，增加失败计数
            if (failureCounter.ContainsKey(cacheKey))
            {
                failureCounter[cacheKey]++;
            }
            else
            {
                failureCounter[cacheKey] = 1;
            }
            
            // 只在首次失败时输出警告
            if (!suppressWarning && Prefs.DevMode && failureCounter[cacheKey] == 1)
            {
                Log.Warning($"[PortraitLoader] Layer texture not found (1/{MaxFailureCount}): {layerName} for {personaName}");
            }
            else if (Prefs.DevMode && failureCounter[cacheKey] == MaxFailureCount)
            {
                Log.Warning($"[PortraitLoader] Layer texture failed {MaxFailureCount} times, using fallback: {layerName} for {personaName}");
            }
            
            // ✅ v1.12.0: 达到阈值后返回 null (避免紫色色块)
            if (failureCounter[cacheKey] >= MaxFailureCount)
            {
                return null;
            }
            
            return null;
        }
        
        /// <summary>
        /// ⭐ 获取所有可用立绘列表
        /// 扫描原版叙事者、其他Mod、本Mod和用户自定义立绘
        /// </summary>
        public static List<PortraitFileInfo> GetAllAvailablePortraits()
        {
            var result = new List<PortraitFileInfo>();
            
            try
            {
                // 1. 扫描原版叙事者立绘
                string[] vanillaNarrators = { "Cassandra", "Phoebe", "Randy" };
                foreach (var narrator in vanillaNarrators)
                {
                    string path = $"UI/HeroArt/{narrator}";
                    var texture = ContentFinder<Texture2D>.Get(path, false);
                    if (texture != null)
                    {
                        result.Add(new PortraitFileInfo
                        {
                            Name = narrator,
                            Path = path,
                            Source = PortraitSource.Vanilla,
                            Texture = texture,
                            ModName = "Core"
                        });
                    }
                }
                
                // 2. 扫描本 Mod 的立绘（UI/Narrators/9x16/）
                string thisModPath = "UI/Narrators/9x16/";
                foreach (var def in DefDatabase<NarratorPersonaDef>.AllDefsListForReading)
                {
                    string personaName = GetPersonaFolderName(def);
                    string basePath = $"{thisModPath}{personaName}/base";
                    var texture = ContentFinder<Texture2D>.Get(basePath, false);
                    
                    if (texture != null)
                    {
                        result.Add(new PortraitFileInfo
                        {
                            Name = def.narratorName ?? personaName,
                            Path = basePath,
                            Source = PortraitSource.ThisMod,
                            Texture = texture,
                            ModName = "The Second Seat"
                        });
                    }
                }
                
                // 3. 扫描用户自定义立绘目录
                string userDir = GetUserPortraitsDirectory();
                if (Directory.Exists(userDir))
                {
                    foreach (var file in Directory.GetFiles(userDir, "*.png"))
                    {
                        result.Add(new PortraitFileInfo
                        {
                            Name = Path.GetFileNameWithoutExtension(file),
                            Path = file,
                            Source = PortraitSource.User,
                            Texture = null, // 延迟加载
                            ModName = "User"
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[PortraitLoader] Error scanning portraits: {ex.Message}");
            }
            
            return result;
        }
        
        /// <summary>
        /// ⭐ 获取 Mod 立绘目录路径
        /// </summary>
        public static string GetModPortraitsDirectory()
        {
            // 查找本 Mod 的路径
            var thisMod = LoadedModManager.RunningMods
                .FirstOrDefault(m => m.Name.Contains("The Second Seat") || m.PackageId.Contains("thesecondseat"));
            
            if (thisMod != null)
            {
                return Path.Combine(thisMod.RootDir, "Textures", "UI", "Narrators", "9x16");
            }
            
            // 回退到配置目录
            return Path.Combine(GenFilePaths.ConfigFolderPath, "TheSecondSeat", "Portraits");
        }
        
        /// <summary>
        /// ⭐ 获取用户立绘目录路径
        /// </summary>
        private static string GetUserPortraitsDirectory()
        {
            string path = Path.Combine(GenFilePaths.ConfigFolderPath, "TheSecondSeat", "UserPortraits");
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
            return path;
        }
        
        /// <summary>
        /// ⭐ 打开 Mod 立绘目录（使用系统文件浏览器）
        /// </summary>
        public static void OpenModPortraitsDirectory()
        {
            string path = GetModPortraitsDirectory();
            
            if (!Directory.Exists(path))
            {
                try
                {
                    Directory.CreateDirectory(path);
                }
                catch (Exception ex)
                {
                    Log.Warning($"[PortraitLoader] Failed to create directory: {ex.Message}");
                }
            }
            
            try
            {
                Application.OpenURL("file://" + path.Replace("\\", "/"));
            }
            catch (Exception ex)
            {
                Log.Warning($"[PortraitLoader] Failed to open directory: {ex.Message}");
                Messages.Message($"无法打开目录：{path}", MessageTypeDefOf.RejectInput);
            }
        }
        
        /// <summary>
        /// ⭐ 打开用户立绘目录（使用系统文件浏览器）
        /// </summary>
        public static void OpenUserPortraitsDirectory()
        {
            string path = GetUserPortraitsDirectory();
            
            try
            {
                Application.OpenURL("file://" + path.Replace("\\", "/"));
            }
            catch (Exception ex)
            {
                Log.Warning($"[PortraitLoader] Failed to open user directory: {ex.Message}");
                Messages.Message($"无法打开目录：{path}", MessageTypeDefOf.RejectInput);
            }
        }
        
        /// <summary>
        /// ⭐ 从外部文件加载纹理
        /// </summary>
        /// <param name="filePath">文件完整路径</param>
        /// <returns>加载的纹理，失败返回 null</returns>
        public static Texture2D LoadFromExternalFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                return null;
            }
            
            try
            {
                byte[] fileData = File.ReadAllBytes(filePath);
                Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                
                if (texture.LoadImage(fileData))
                {
                    texture.filterMode = FilterMode.Bilinear;
                    texture.anisoLevel = 4;
                    return texture;
                }
                else
                {
                    UnityEngine.Object.Destroy(texture);
                    return null;
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[PortraitLoader] Failed to load external file: {filePath} - {ex.Message}");
                return null;
            }
        }
    }
}
