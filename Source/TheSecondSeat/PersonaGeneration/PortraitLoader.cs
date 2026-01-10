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
        
        private const string BASE_PATH_9x16 = "UI/Narrators/9x16";
        private const string EXPRESSIONS_PATH = "UI/Narrators/9x16/Expressions";
        
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
                if (Prefs.DevMode)
                {
                    Log.Warning("[PortraitLoader] PersonaDef is null");
                }
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
                // ✅ 只在DevMode下输出警告
                if (Prefs.DevMode)
                {
                    Log.Warning($"[PortraitLoader] Portrait not found for {def.defName}{expressionSuffix}, using placeholder");
                }
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
                    if (Prefs.DevMode)
                    {
                        Log.Warning($"[PortraitLoader] Layered config is null for {def.defName}");
                    }
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
                    // ✅ 只在DevMode下输出错误
                    if (Prefs.DevMode)
                    {
                        Log.Error($"[PortraitLoader] Layered composite failed for {def.defName}");
                    }
                    return GeneratePlaceholder(def.primaryColor);
                }
                
                // ✅ v1.6.27: 完全移除成功日志，避免连续输出
                
                return composite;
            }
            catch (Exception ex)
            {
                // ✅ 只在DevMode下输出异常
                if (Prefs.DevMode)
                {
                    Log.Error($"[PortraitLoader] Layered portrait loading failed: {ex}");
                }
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
                        if (Prefs.DevMode)
                        {
                            Log.Message($"[PortraitLoader] 表情回退: {fileName} -> {fallbackName}");
                        }
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
                
                // ✅ 移除合成成功日志，只在DevMode下输出
                if (composite != null && Prefs.DevMode)
                {
                    Log.Message($"[PortraitLoader] ✓ 面部叠加合成: {personaName}");
                }
                return composite;
            }
            catch (Exception ex)
            {
                // ✅ 保留异常日志（这是真正的错误）
                Log.Error($"[PortraitLoader] 面部叠加合成失败: {ex}");
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
            
            // ✅ 尝试路径4：自定义路径
            if (def.useCustomPortrait && !string.IsNullOrEmpty(def.customPortraitPath))
            {
                texture = LoadFromExternalFile(def.customPortraitPath);
                if (texture != null)
                {
                    // 判断是否为拥有的资源（非 ContentFinder 加载）
                    bool isOwned = !def.customPortraitPath.StartsWith("UI/");
                    
                    cache[baseCacheKey] = new CacheEntry
                    {
                        Texture = texture,
                        LastAccessTick = Find.TickManager.TicksGame,
                        IsOwned = isOwned
                    };
                    return (texture, isOwned);
                }
            }
            
            // ✅ 尝试路径5：原版叙事者路径 UI/HeroArt/{Name}
            string heroArtPath = $"UI/HeroArt/{personaName}";
            texture = ContentFinder<Texture2D>.Get(heroArtPath, false);
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
            
            // ✅ 所有路径都失败，只在DevMode下输出警告
            if (Prefs.DevMode)
            {
                Log.Warning($"[PortraitLoader] Portrait not found for {personaName}");
                Log.Warning($"[PortraitLoader] Tried paths:");
                Log.Warning($"  - Textures/{basePath}.png");
                Log.Warning($"  - Textures/{path2}.png");
                if (!string.IsNullOrEmpty(def.portraitPath))
                    Log.Warning($"  - Textures/{def.portraitPath}.png");
                Log.Warning($"  - Textures/{heroArtPath}.png");
            }
            
            return (null, false);
        }
        
        /// <summary>
        /// 获取表情后缀的路径
        /// </summary>
        private static string GetExpressionPath(string basePath, string expressionSuffix)
        {
            if (string.IsNullOrEmpty(expressionSuffix))
            {
                return basePath;
            }
            
            // 分离文件名和扩展名
            string directory = Path.GetDirectoryName(basePath) ?? "";
            string fileNameWithoutExt = Path.GetFileNameWithoutExtension(basePath);
            string extension = Path.GetExtension(basePath);
            
            // 拼接新的文件路径
            string newFileName = fileNameWithoutExt + expressionSuffix + extension;
            
            if (string.IsNullOrEmpty(directory))
            {
                return newFileName;
            }
            
            return Path.Combine(directory, newFileName);
        }
        
        /// <summary>
        /// 从外部文件加载纹理
        /// 支持文件路径和ContentFinder路径
        /// ✅ v1.6.27: 静默处理文件不存在的情况
        /// </summary>
        public static Texture2D? LoadFromExternalFile(string filePath)
        {
            try
            {
                if (string.IsNullOrEmpty(filePath))
                {
                    return null;
                }
                
                // ✅ 如果是ContentFinder路径（UI/开头），直接用ContentFinder
                if (filePath.StartsWith("UI/"))
                {
                    var tex = ContentFinder<Texture2D>.Get(filePath, false);
                    if (tex != null)
                    {
                        // ✅ 设置高质量过滤模式
                        SetTextureQuality(tex);
                    }
                    return tex;
                }
                
                // 否则作为文件路径处理
                if (!File.Exists(filePath))
                {
                    // ✅ 只在DevMode下输出警告
                    if (Prefs.DevMode)
                    {
                        Log.Warning($"[PortraitLoader] 文件不存在: {filePath}");
                    }
                    return null;
                }
                
                byte[] fileData = File.ReadAllBytes(filePath);
                Texture2D loadedTexture = new Texture2D(2, 2);
                
                if (!loadedTexture.LoadImage(fileData))
                {
                    // ✅ 只在DevMode下输出错误
                    if (Prefs.DevMode)
                    {
                        Log.Error($"[PortraitLoader] 无法加载图片: {filePath}");
                    }
                    return null;
                }
                
                // ✅ 设置高质量过滤模式
                SetTextureQuality(loadedTexture);
                
                return loadedTexture;
            }
            catch (Exception ex)
            {
                // ✅ 只在DevMode下输出异常
                if (Prefs.DevMode)
                {
                    Log.Error($"[PortraitLoader] 加载失败: {filePath}\n{ex}");
                }
                return null;
            }
        }
        
        /// <summary>
        /// ✅ 设置纹理高质量参数（安全版本）
        /// </summary>
        private static void SetTextureQuality(Texture2D texture)
        {
            if (texture == null) return;
            
            try
            {
                // ✅ 只设置过滤模式，不调用 Apply（避免不可读纹理错误）
                texture.filterMode = FilterMode.Bilinear;
                texture.anisoLevel = 4;
                
                // ? [核心修复] 2. 循环模式设为 Clamp (钳制)
                // 这行代码是消除边缘黑线/杂色的关键！
                // 它告诉 GPU：不要去采样对面的像素，边缘是什么就是什么。
                texture.wrapMode = TextureWrapMode.Clamp;
                
                // 注意：不调用 texture.Apply()，因为 ContentFinder 加载的纹理是只读的
                // Apply 会触发 "Texture not readable" 错误
            }
            catch
            {
                // 静默忽略，纹理设置不是关键功能
            }
        }
        
        /// <summary>
        /// 生成改良版占位符纹理 - 带不同颜色标识
        /// 用不同颜色的占位符纹理来区分
        /// </summary>
        private static Texture2D GeneratePlaceholder(Color color)
        {
            int size = 512;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            
            // 1. 绘制渐变背景（深色到浅色）
            Color darkColor = color * 0.3f;
            Color lightColor = color * 1.2f;
            
            for (int y = 0; y < size; y++)
            {
                float t = y / (float)size;
                Color gradientColor = Color.Lerp(darkColor, lightColor, t);
                
                for (int x = 0; x < size; x++)
                {
                    // 添加径向渐变效果
                    float distFromCenter = Vector2.Distance(
                        new Vector2(x, y), 
                        new Vector2(size / 2f, size / 2f)
                    ) / (size / 2f);
                    
                    Color finalColor = Color.Lerp(gradientColor, darkColor, distFromCenter * 0.3f);
                    texture.SetPixel(x, y, finalColor);
                }
            }
            
            // 2. 绘制两个圆形装饰
            DrawCircle(texture, size / 2, size / 2, size / 3, Color.white * 0.1f, 4);
            DrawCircle(texture, size / 2, size / 2, size / 4, Color.white * 0.15f, 3);
            
            // 3. 绘制角落装饰线（科技感）
            DrawCornerLines(texture, color * 1.5f);
            
            texture.Apply();
            return texture;
        }
        
        /// <summary>
        /// 在纹理上画一个圆环
        /// </summary>
        private static void DrawCircle(Texture2D texture, int centerX, int centerY, int radius, Color color, int thickness)
        {
            for (int angle = 0; angle < 360; angle += 2)
            {
                float rad = angle * Mathf.Deg2Rad;
                
                for (int t = 0; t < thickness; t++)
                {
                    int r = radius + t - thickness / 2;
                    int x = centerX + Mathf.RoundToInt(r * Mathf.Cos(rad));
                    int y = centerY + Mathf.RoundToInt(r * Mathf.Sin(rad));
                    
                    if (x >= 0 && x < texture.width && y >= 0 && y < texture.height)
                    {
                        texture.SetPixel(x, y, color);
                    }
                }
            }
        }
        
        /// <summary>
        /// 绘制角落装饰线（科技感）
        /// </summary>
        private static void DrawCornerLines(Texture2D texture, Color color)
        {
            int size = texture.width;
            int lineLength = size / 8;
            int thickness = 3;
            
            // 四个角落的 L 形装饰
            Color lineColor = new Color(color.r, color.g, color.b, 0.3f);
            
            // 左上角
            DrawLine(texture, 20, 20, 20 + lineLength, 20, lineColor, thickness);
            DrawLine(texture, 20, 20, 20, 20 + lineLength, lineColor, thickness);
            
            // 右上角
            DrawLine(texture, size - 20, 20, size - 20 - lineLength, 20, lineColor, thickness);
            DrawLine(texture, size - 20, 20, size - 20, 20 + lineLength, lineColor, thickness);
            
            // 左下角
            DrawLine(texture, 20, size - 20, 20 + lineLength, size - 20, lineColor, thickness);
            DrawLine(texture, 20, size - 20, 20, size - 20 - lineLength, lineColor, thickness);
            
            // 右下角
            DrawLine(texture, size - 20, size - 20, size - 20 - lineLength, size - 20, lineColor, thickness);
            DrawLine(texture, size - 20, size - 20, size - 20, size - 20 - lineLength, lineColor, thickness);
        }
        
        /// <summary>
        /// 绘制直线
        /// </summary>
        private static void DrawLine(Texture2D texture, int x1, int y1, int x2, int y2, Color color, int thickness)
        {
            int dx = Mathf.Abs(x2 - x1);
            int dy = Mathf.Abs(y2 - y1);
            int sx = x1 < x2 ? 1 : -1;
            int sy = y1 < y2 ? 1 : -1;
            int err = dx - dy;
            
            while (true)
            {
                // 绘制粗线
                for (int tx = -thickness / 2; tx <= thickness / 2; tx++)
                {
                    for (int ty = -thickness / 2; ty <= thickness / 2; ty++)
                    {
                        int px = x1 + tx;
                        int py = y1 + ty;
                        
                        if (px >= 0 && px < texture.width && py >= 0 && py < texture.height)
                        {
                            texture.SetPixel(px, py, color);
                        }
                    }
                }
                
                if (x1 == x2 && y1 == y2) break;
                
                int e2 = 2 * err;
                if (e2 > -dy)
                {
                    err -= dy;
                    x1 += sx;
                }
                if (e2 < dx)
                {
                    err += dx;
                    y1 += sy;
                }
            }
        }
        
        /// <summary>
        /// ✅ 新增：清理旧缓存（LRU机制）
        /// ⭐ v1.6.92: 跳过关键图层（base_body, body, base）避免分层立绘丢失
        /// </summary>
        public static void CleanOldCache()
        {
            int currentTick = Find.TickManager.TicksGame;
            
            // ⭐ 关键图层名称（不应被自动清理）
            var criticalLayers = new HashSet<string> { "base_body", "body", "base", "base_", "_base" };
            
            var oldEntries = cache
                .Where(kv => currentTick - kv.Value.LastAccessTick > 36000) // 10分钟未访问
                // ⭐ 跳过关键图层
                .Where(kv => !criticalLayers.Any(cl => kv.Key.Contains(cl)))
                .OrderBy(kv => kv.Value.LastAccessTick)
                .Take(10) // 每次最多清理10个
                .ToList();
            
            foreach (var entry in oldEntries)
            {
                // ✅ 修复：不再销毁纹理，防止误删 ContentFinder 管理的共享资源
                // 即使是 IsOwned，为了安全起见（防止第二次读档丢失），也建议由 Unity 自动回收
                cache.Remove(entry.Key);
            }
            
            // 日志已静默
        }
        
        /// <summary>
        /// 清空缓存
        /// </summary>
        public static void ClearCache()
        {
            // ✅ 修复：不再销毁纹理
            cache.Clear();
            if (Prefs.DevMode)
            {
                Log.Message("[PortraitLoader] 立绘缓存已清空");
            }
        }
        
        /// <summary>
        /// 清空所有缓存（用于模式切换）
        /// </summary>
        public static void ClearAllCache()
        {
            // ✅ 修复：不再销毁纹理
            cache.Clear();
            if (Prefs.DevMode)
            {
                Log.Message("[PortraitLoader] 所有立绘缓存已清空");
            }
        }
        
        /// <summary>
        /// 清除特定人格特定表情的缓存
        /// </summary>
        public static void ClearPortraitCache(string personaDefName, ExpressionType expression)
        {
            string expressionSuffix = ExpressionSystem.GetExpressionSuffix(personaDefName, expression);
            string cacheKey = $"{personaDefName}_portrait_{expressionSuffix}";
            
            if (cache.TryGetValue(cacheKey, out CacheEntry entry))
            {
                // ✅ 修复：不再销毁纹理
                cache.Remove(cacheKey);
                if (Prefs.DevMode)
                {
                    Log.Message($"[PortraitLoader] 清除缓存: {cacheKey}");
                }
            }
        }
        
        /// <summary>
        /// 获取Mod立绘目录路径
        /// </summary>
        public static string GetModPortraitsDirectory()
        {
            // 获取 Mod 的目录
            var modContentPack = LoadedModManager.RunningModsListForReading
                .FirstOrDefault(mod => mod.PackageId.ToLower().Contains("thesecondseat") || 
                                      mod.Name.Contains("Second Seat"));
            
            if (modContentPack != null)
            {
                string path = Path.Combine(modContentPack.RootDir, "Textures", "UI", "Narrators");
                
                try
                {
                    if (!Directory.Exists(path))
                    {
                        Directory.CreateDirectory(path);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[PortraitLoader] 无法创建 Mod 目录: {path}\n{ex}");
                }
                
                return path;
            }
            
            // 备用路径（如果找不到 Mod）
            return Path.Combine(GenFilePaths.SaveDataFolderPath, "TheSecondSeat", "Portraits");
        }
        
        /// <summary>
        /// 打开 Mod 立绘目录
        /// 引导用户将立绘文件放到该目录
        /// </summary>
        public static void OpenModPortraitsDirectory()
        {
            string path = GetModPortraitsDirectory();
            
            try
            {
                Application.OpenURL("file://" + path);
                Messages.Message($"已打开 Mod 立绘目录:\n{path}\n\n请将 PNG 文件放到该目录", MessageTypeDefOf.NeutralEvent);
            }
            catch (Exception ex)
            {
                Log.Error($"[PortraitLoader] 无法打开目录: {ex}");
                Messages.Message($"请手动打开目录:\n{path}", MessageTypeDefOf.RejectInput);
            }
        }
        
        /// <summary>
        /// 获取推荐的用户立绘目录
        /// 保留原有功能，更加容易使用
        /// </summary>
        public static string GetUserPortraitsDirectory()
        {
            string path = Path.Combine(GenFilePaths.SaveDataFolderPath, "TheSecondSeat", "Portraits");
            
            try
            {
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[PortraitLoader] 无法创建目录: {path}\n{ex}");
            }
            
            return path;
        }
        
        /// <summary>
        /// 打开用户立绘目录
        /// 保留原有功能，更加容易使用
        /// </summary>
        public static void OpenUserPortraitsDirectory()
        {
            string path = GetUserPortraitsDirectory();
            
            try
            {
                Application.OpenURL("file://" + path);
                Messages.Message($"已打开用户立绘目录:\n{path}\n\n请将立绘 PNG 或 JPG 文件复制到该目录", MessageTypeDefOf.NeutralEvent);
            }
            catch (Exception ex)
            {
                Log.Error($"[PortraitLoader] 无法打开目录: {ex}");
                Messages.Message($"请手动打开目录:\n{path}", MessageTypeDefOf.RejectInput);
            }
        }
        
        /// <summary>
        /// 获取 Mod 立绘文件列表
        /// 获取所有可用的 Mod 目录中的立绘文件
        /// </summary>
        public static List<string> GetModPortraitFiles()
        {
            List<string> files = new List<string>();
            
            try
            {
                string modDir = GetModPortraitsDirectory();
                
                if (Directory.Exists(modDir))
                {
                    // PNG 文件
                    files.AddRange(Directory.GetFiles(modDir, "*.png"));
                    // JPG 文件
                    files.AddRange(Directory.GetFiles(modDir, "*.jpg"));
                    files.AddRange(Directory.GetFiles(modDir, "*.jpeg"));
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[PortraitLoader] 获取 Mod 立绘失败: {ex}");
            }
            
            return files;
        }
        
        /// <summary>
        /// 获取用户立绘文件列表
        /// </summary>
        public static List<string> GetUserPortraitFiles()
        {
            List<string> files = new List<string>();
            
            try
            {
                string userDir = GetUserPortraitsDirectory();
                
                if (Directory.Exists(userDir))
                {
                    // PNG 文件
                    files.AddRange(Directory.GetFiles(userDir, "*.png"));
                    // JPG 文件
                    files.AddRange(Directory.GetFiles(userDir, "*.jpg"));
                    files.AddRange(Directory.GetFiles(userDir, "*.jpeg"));
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[PortraitLoader] 获取用户立绘失败: {ex}");
            }
            
            return files;
        }
        
        /// <summary>
        /// 获取所有可用的立绘列表（多源）
        /// 包含所有来源：原版叙事者立绘 + Mod立绘 + 用户立绘
        /// </summary>
        public static List<PortraitFileInfo> GetAllAvailablePortraits()
        {
            var portraits = new List<PortraitFileInfo>();
            
            try
            {
                // 1. 添加原版 RimWorld 叙事者立绘
                portraits.AddRange(GetVanillaStorytellerPortraits());
                
                // 2. 添加其他 Mod 的叙事者立绘
                portraits.AddRange(GetModStorytellerPortraits());
                
                // 3. 添加本 Mod 自带立绘
                portraits.AddRange(GetModPortraitFilesWithInfo());
                
                // 4. 添加用户自定义立绘
                portraits.AddRange(GetUserPortraitFilesWithInfo());
            }
            catch (Exception ex)
            {
                Log.Error($"[PortraitLoader] 获取可用立绘列表失败: {ex}");
            }
            
            return portraits;
        }
        
        /// <summary>
        /// 获取原版 RimWorld 叙事者立绘
        /// Cassandra, Phoebe, Randy 等
        /// </summary>
        private static List<PortraitFileInfo> GetVanillaStorytellerPortraits()
        {
            var portraits = new List<PortraitFileInfo>();
            
            try
            {
                // 原版叙事者列表
                var vanillaStorytellers = new[]
                {
                    "Cassandra",
                    "Phoebe", 
                    "Randy",
                    "Igor"  // DLC 叙事者
                };
                
                foreach (var storyteller in vanillaStorytellers)
                {
                    // RimWorld 原版立绘路径：UI/HeroArt/{StorytellerName}
                    string texturePath = $"UI/HeroArt/{storyteller}";
                    var texture = ContentFinder<Texture2D>.Get(texturePath, false);
                    
                    if (texture != null)
                    {
                        portraits.Add(new PortraitFileInfo
                        {
                            Name = $"{storyteller} (原版)",
                            Path = texturePath,
                            Source = PortraitSource.Vanilla,
                            Texture = texture
                        });
                        
                        // ✅ v1.6.61: 关闭调试日志
                        // Log.Message($"[PortraitLoader] 找到并添加原版立绘: {storyteller}");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[PortraitLoader] 获取原版立绘失败: {ex.Message}");
            }
            
            return portraits;
        }
        
        /// <summary>
        /// 获取其他 Mod 的叙事者立绘
        /// 自动检测其他 Mod 的 StorytellerDefs
        /// </summary>
        private static List<PortraitFileInfo> GetModStorytellerPortraits()
        {
            var portraits = new List<PortraitFileInfo>();
            
            try
            {
                // 获取所有叙事者定义（包括 Mod 添加的）
                var allStorytellers = DefDatabase<StorytellerDef>.AllDefsListForReading;
                
                foreach (var storyteller in allStorytellers)
                {
                    // 跳过原版叙事者，已在前面处理
                    if (storyteller.defName == "Cassandra" || 
                        storyteller.defName == "Phoebe" || 
                        storyteller.defName == "Randy" ||
                        storyteller.defName == "Igor")
                    {
                        continue;
                    }
                    
                    // 修改：StorytellerDef.portraitLargeTex 是 Texture2D，而非 string
                    // 所以需要通过 defName 构建路径
                    var texture = storyteller.portraitLargeTex;
                    
                    if (texture != null)
                    {
                        // 获取 Mod 名称
                        string modName = storyteller.modContentPack?.Name ?? "未知Mod";
                        
                        // 立绘纹理路径（通常是 UI/HeroArt/{DefName}）
                        string portraitPath = $"UI/HeroArt/{storyteller.defName}";
                        
                        var portraitInfo = new PortraitFileInfo();
                        portraitInfo.Name = $"{storyteller.LabelCap} ({modName})";
                        portraitInfo.Path = portraitPath;
                        portraitInfo.Source = PortraitSource.OtherMod;
                        portraitInfo.Texture = texture;
                        portraitInfo.ModName = modName;
                        
                        portraits.Add(portraitInfo);
                        
                        // ✅ v1.6.61: 关闭调试日志
                        // Log.Message($"[PortraitLoader] 找到Mod立绘: {storyteller.LabelCap} from {modName}");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[PortraitLoader] 获取Mod立绘失败: {ex.Message}");
            }
            
            return portraits;
        }
        
        /// <summary>
        /// 获取本 Mod 立绘文件（含详细信息）
        /// </summary>
        private static List<PortraitFileInfo> GetModPortraitFilesWithInfo()
        {
            var portraits = new List<PortraitFileInfo>();
            
            try
            {
                var files = GetModPortraitFiles();
                
                foreach (var file in files)
                {
                    portraits.Add(new PortraitFileInfo
                    {
                        Name = Path.GetFileNameWithoutExtension(file),
                        Path = file,
                        Source = PortraitSource.ThisMod,
                        ModName = "The Second Seat"
                    });
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[PortraitLoader] 获取本Mod立绘失败: {ex.Message}");
            }
            
            return portraits;
        }
        
        /// <summary>
        /// 获取用户自定义立绘文件信息
        /// ? 逐个文件检查并构建信息
        /// </summary>
        private static List<PortraitFileInfo> GetUserPortraitFilesWithInfo()
        {
            var portraits = new List<PortraitFileInfo>();
            
            try
            {
                var files = GetUserPortraitFiles();
                
                foreach (var file in files)
                {
                    portraits.Add(new PortraitFileInfo
                    {
                        Name = Path.GetFileNameWithoutExtension(file) + " (用户)",
                        Path = file,
                        Source = PortraitSource.User
                    });
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[PortraitLoader] 获取用户立绘失败: {ex.Message}");
            }
            
            return portraits;
        }
        
        /// <summary>
        /// 获取调试信息
        /// </summary>
        public static string GetDebugInfo()
        {
            var allPortraits = GetAllAvailablePortraits();
            
            var vanillaCount = allPortraits.Count(p => p.Source == PortraitSource.Vanilla);
            var modCount = allPortraits.Count(p => p.Source == PortraitSource.OtherMod);
            var thisModCount = allPortraits.Count(p => p.Source == PortraitSource.ThisMod);
            var userCount = allPortraits.Count(p => p.Source == PortraitSource.User);
            
            return $"[PortraitLoader] 缓存数量: {cache.Count}\n" +
                   $"可用立绘总数: {allPortraits.Count}\n" +
                   $"  - 原版立绘: {vanillaCount}\n" +
                   $"  - 其他Mod立绘: {modCount}\n" +
                   $"  - 本Mod立绘: {thisModCount}\n" +
                   $"  - 用户立绘: {userCount}\n" +
                   $"Mod 立绘目录: {GetModPortraitsDirectory()}\n" +
                   $"用户立绘目录: {GetUserPortraitsDirectory()}";
        }
        
        /// <summary>
        /// 应用服装差分
        /// </summary>
        private static Texture2D ApplyOutfit(Texture2D baseTexture, string personaDefName)
        {
            // ✅ 直接返回基础纹理，不进行服装合成
            // 原因：ContentFinder加载的纹理默认不可读，无法调用GetPixel()
            // 如果需要服装系统，应该预先合成好完整立绘，而不是运行时合成
            return baseTexture;
        }
        
        /// <summary>
        /// 合成多层纹理（基础立绘 + 服装/差分）
        /// </summary>
        private static Texture2D CompositeTextures(Texture2D bottom, Texture2D top)
        {
            try
            {
                // 确保纹理尺寸一致
                int width = Mathf.Min(bottom.width, top.width);
                int height = Mathf.Min(bottom.height, top.height);
                
                Texture2D result = new Texture2D(width, height, TextureFormat.RGBA32, false);
                
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        Color bottomColor = bottom.GetPixel(x, y);
                        Color topColor = top.GetPixel(x, y);
                        
                        // Alpha blending
                        Color blended = Color.Lerp(bottomColor, topColor, topColor.a);
                        result.SetPixel(x, y, blended);
                    }
                }
                
                result.Apply();
                return result;
            }
            catch (Exception ex)
            {
                Log.Error($"[PortraitLoader] 合成纹理失败: {ex}");
                return bottom;
            }
        }
        
        /// <summary>
        /// 获取人格文件夹名称
        /// ✅ v1.6.82: 使用 GetResourceName() 方法，支持本地化独立的资源路径
        /// </summary>
        private static string GetPersonaFolderName(NarratorPersonaDef def)
        {
            return def.GetResourceName();
        }
        
        /// <summary>
        /// ✅ v1.6.34: 获取单个图层纹理（支持子 Mod 路径）
        /// ⭐ v1.6.74 更新：支持扁平结构的子 Mod 路径
        /// ⭐ 路径回退机制：
        ///   1. 主 Mod 路径: UI/Narrators/9x16/Layered/{PersonaName}/{layerName}
        ///   2. 子 Mod 路径（带子文件夹）: Narrators/Layered/{PersonaName}/{layerName}
        ///   3. ⭐ 子 Mod 路径（无子文件夹）: Narrators/Layered/{layerName} - 适配扁平结构
        ///   4. 降临姿态路径: UI/Narrators/Descent/Postures/{PersonaName}/{layerName}
        ///   5. 降临特效路径: UI/Narrators/Descent/Effects/{PersonaName}/{layerName}
        /// </summary>
        /// <param name="def">人格定义</param>
        /// <param name="layerName">图层名称（如 "base_body", "happy_eyes", "descent_pose"）</param>
        /// <param name="suppressWarning">是否抑制警告日志（用于尝试加载可能不存在的图层）</param>
        /// <returns>图层纹理，如果未找到则返回null</returns>
        public static Texture2D GetLayerTexture(NarratorPersonaDef def, string layerName, bool suppressWarning = false)
        {
            if (def == null || string.IsNullOrEmpty(layerName))
            {
                return null;
            }
            
            // 1. 生成缓存键
            string personaName = GetPersonaFolderName(def);
            string cacheKey = $"{personaName}_layer_{layerName}";
            
            // 2. 检查缓存
            if (cache.TryGetValue(cacheKey, out CacheEntry cachedTexture))
            {
                cachedTexture.LastAccessTick = Find.TickManager.TicksGame;
                return cachedTexture.Texture as Texture2D;
            }
            
            // 3. ⭐ v1.6.74: 多路径回退机制（支持扁平化无子文件夹结构）
            Texture2D texture = null;
            bool isOwned = false; // 图层通常是从 ContentFinder 加载的，不属于我们
            string foundPath = ""; // 记录找到的路径用于调试

            // ⭐ 路径优先级调整：如果定义了 portraitPath，优先尝试它
            // 这对于子 Mod (如 Sideria) 至关重要，避免回退到错误的默认路径
            if (!string.IsNullOrEmpty(def.portraitPath))
            {
                // ⭐ 路径0：使用 portraitPath 前缀作为基础路径（支持子 Mod 独立路径，如 Sideria/Narrators/Layered/）
                string basePath = def.portraitPath;
                
                // 如果 portraitPath 包含完整路径（如 "Sideria/Narrators/Layered/base"）
                // 提取目录部分，拼接图层名称
                int lastSlashIndex = basePath.LastIndexOf('/');
                if (lastSlashIndex >= 0)
                {
                    string baseDir = basePath.Substring(0, lastSlashIndex + 1);
                    string layerPath0 = $"{baseDir}{layerName}";
                    texture = ContentFinder<Texture2D>.Get(layerPath0, false);
                    if (texture != null) foundPath = layerPath0;
                }
            }
            
            // 路径1 & 2 (主 Mod 默认路径) 已移除，不再支持主 Mod 的旧版路径结构
            
            if (texture == null)
            {
                // ⭐ 路径3：子 Mod 分层路径（无子文件夹）- 适配扁平结构
                string layerPath3 = $"Narrators/Layered/{layerName}";
                texture = ContentFinder<Texture2D>.Get(layerPath3, false);
                if (texture != null) foundPath = layerPath3;
            }
            
            if (texture == null && (layerName.Contains("descent") || layerName.Contains("pose")))
            {
                // 路径4：降临姿态路径（主 Mod）
                string descentPath1 = $"UI/Narrators/Descent/Postures/{personaName}/{layerName}";
                texture = ContentFinder<Texture2D>.Get(descentPath1, false);
                
                if (texture == null)
                {
                    // 路径5：降临姿态路径（子 Mod，无子文件夹）
                    string descentPath2 = $"Narrators/Descent/Postures/{layerName}";
                    texture = ContentFinder<Texture2D>.Get(descentPath2, false);
                }
            }
            
            if (texture == null && (layerName.Contains("effect") || layerName.Contains("assist") || layerName.Contains("attack")))
            {
                // 路径6：降临特效路径（主 Mod）
                string effectPath1 = $"UI/Narrators/Descent/Effects/{personaName}/{layerName}";
                texture = ContentFinder<Texture2D>.Get(effectPath1, false);
                
                if (texture == null)
                {
                    // 路径7：降临特效路径（子 Mod，无子文件夹）
                    string effectPath2 = $"Narrators/Descent/Effects/{layerName}";
                    texture = ContentFinder<Texture2D>.Get(effectPath2, false);
                }
            }
            
            // 4. 设置纹理质量并缓存
            if (texture != null)
            {
                SetTextureQuality(texture);
                cache[cacheKey] = new CacheEntry
                {
                    Texture = texture,
                    LastAccessTick = Find.TickManager.TicksGame,
                    IsOwned = isOwned
                };
                
                if (Prefs.DevMode)
                {
                    Log.Message($"[PortraitLoader] ✅ 加载图层: {layerName} from {foundPath} ({texture.width}x{texture.height})");
                }
            }
            else if (Prefs.DevMode && !suppressWarning)
            {
                // ⭐ v1.8.3: 诊断日志 - 显示尝试的所有路径
                Log.Warning($"[PortraitLoader] ⚠️ 图层加载失败: {layerName}");
                Log.Warning($"[PortraitLoader]   persona: {def.defName}, portraitPath: {def.portraitPath}");
                if (!string.IsNullOrEmpty(def.portraitPath))
                {
                    int lastSlashIndex = def.portraitPath.LastIndexOf('/');
                    if (lastSlashIndex >= 0)
                    {
                        string baseDir = def.portraitPath.Substring(0, lastSlashIndex + 1);
                        Log.Warning($"[PortraitLoader]   尝试的路径: {baseDir}{layerName}");
                    }
                }
                Log.Warning($"[PortraitLoader]   尝试的路径: Narrators/Layered/{layerName}");
            }
            // else if (Prefs.DevMode && !suppressWarning)
            // {
            //     // 用户请求关闭此类报错日志
            //     // Log.Warning($"[PortraitLoader] ⚠️ 图层未找到: {layerName} (persona: {personaName})");
            //     // Log.Warning($"[PortraitLoader]   尝试的路径:");
            //     // Log.Warning($"[PortraitLoader]     • UI/Narrators/9x16/Layered/{personaName}/{layerName}");
            //     // Log.Warning($"[PortraitLoader]     • Narrators/Layered/{personaName}/{layerName}");
            //     // Log.Warning($"[PortraitLoader]     • Narrators/Layered/{layerName}");
            // }
            
            return texture;
        }

    }
}
