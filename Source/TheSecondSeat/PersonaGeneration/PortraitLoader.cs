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
        public Texture2D Texture { get; set; }
        public string ModName { get; set; }
    }
    
    /// <summary>
    /// 立绘加载管理器
    /// ✅ v1.6.27: 消除未找到立绘时的报错日志
    /// </summary>
    public static class PortraitLoader
    {
        private static Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();
        
        private const string BASE_PATH_9x16 = "UI/Narrators/9x16";
        private const string EXPRESSIONS_PATH = "UI/Narrators/9x16/Expressions";
        
        /// <summary>
        /// 加载立绘（支持 Mod资源、外部文件、占位符）
        /// ✅ 消除未找到立绘时的报错日志
        /// ✅ v1.6.28: 分层立绘直接调用LayeredPortraitCompositor，不单独缓存
        /// </summary>
        public static Texture2D LoadPortrait(NarratorPersonaDef def, ExpressionType? expression = null)
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
            if (cache.TryGetValue(cacheKey, out Texture2D cached))
            {
                return cached;
            }
            
            Texture2D texture = null;
            
            // 2. ✅ 修复：使用统一的加载方法，静默处理失败
            texture = LoadExpressionOrBase(def, expression);
            
            // 3. 如果还是没有，生成占位符
            if (texture == null)
            {
                // ✅ 只在DevMode下输出警告
                if (Prefs.DevMode)
                {
                    Log.Warning($"[PortraitLoader] Portrait not found for {def.defName}{expressionSuffix}, using placeholder");
                }
                texture = GeneratePlaceholder(def.primaryColor);
            }
            
            // 缓存
            cache[cacheKey] = texture;
            return texture;
        }

        /// <summary>
        /// ✅ 加载分层立绘（新增）
        /// ✅ v1.6.27: 静默处理失败，完全移除成功日志
        /// ✅ v1.6.28: 修复缓存键不一致问题，复用LayeredPortraitCompositor的缓存
        /// </summary>
        private static Texture2D LoadLayeredPortrait(NarratorPersonaDef def, ExpressionType? expression)
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
                ExpressionType currentExpression = expression ?? ExpressionSystem.GetExpressionState(def.defName).CurrentExpression;
                string currentOutfit = "default";
                
                // ✅ v1.6.28: 不再在PortraitLoader中维护缓存，直接调用LayeredPortraitCompositor
                // LayeredPortraitCompositor内部已经有完整的缓存机制
                Texture2D composite = LayeredPortraitCompositor.CompositeLayers(
                    config, 
                    currentExpression, 
                    currentOutfit
                );
                
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
        /// </summary>
        private static Texture2D LoadExpressionOrBase(NarratorPersonaDef def, ExpressionType? expression)
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
                    return expressionTexture;
                }
                
                // ✅ 尝试面部覆盖模式（静默）
                var overlayTexture = LoadWithFaceOverlayAndFallback(def, expression.Value);
                if (overlayTexture != null)
                {
                    return overlayTexture;
                }
                
                // ✅ 移除回退警告，静默继续
            }
            
            // 2. 没有表情或表情文件不存在，加载基础立绘
            return LoadBasePortrait(def);
        }
        
        /// <summary>
        /// ✅ 带回退机制的纹理加载方法
        /// 回退顺序：具体变体 → 通用表情
        /// 例如：_happy3 → _happy
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
                // ✅ 移除成功日志，静默返回
                return texture;
            }
            
            // ✅ 第2层：回退到通用表情（去掉变体编号）
            string genericFileName = StripVariantNumber(fileName);
            
            if (genericFileName != fileName)  // 确实有变体编号被去掉了
            {
                string genericPath = $"{EXPRESSIONS_PATH}{personaName}/{genericFileName}";
                texture = ContentFinder<Texture2D>.Get(genericPath, false);
                
                if (texture != null)
                {
                    // ✅ 移除回退成功日志，静默返回
                    return texture;
                }
            }
            
            // ✅ 两层都失败，静默返回 null（不输出警告，由上层统一处理）
            return null;
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
        /// </summary>
        private static Texture2D LoadWithFaceOverlayAndFallback(NarratorPersonaDef def, ExpressionType expression)
        {
            try
            {
                // 1. 加载基础立绘
                Texture2D baseTexture = LoadBasePortrait(def);
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
                var composite = ExpressionCompositor.CompositeExpression(
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
        /// </summary>
        private static Texture2D LoadBasePortrait(NarratorPersonaDef def)
        {
            string personaName = GetPersonaFolderName(def);
            
            // ✅ 尝试路径1：9x16文件夹的 base.png
            string basePath = $"{BASE_PATH_9x16}{personaName}/base";
            var texture = ContentFinder<Texture2D>.Get(basePath, false);
            
            if (texture != null)
            {
                SetTextureQuality(texture);
                return texture;
            }
            
            // ✅ 尝试路径2：直接用 personaName（不加 /base）
            string path2 = $"{BASE_PATH_9x16}{personaName}";
            texture = ContentFinder<Texture2D>.Get(path2, false);
            if (texture != null)
            {
                SetTextureQuality(texture);
                return texture;
            }
            
            // ✅ 尝试路径3：使用 portraitPath
            if (!string.IsNullOrEmpty(def.portraitPath))
            {
                texture = ContentFinder<Texture2D>.Get(def.portraitPath, false);
                if (texture != null)
                {
                    SetTextureQuality(texture);
                    return texture;
                }
            }
            
            // ✅ 尝试路径4：自定义路径
            if (def.useCustomPortrait && !string.IsNullOrEmpty(def.customPortraitPath))
            {
                texture = LoadFromExternalFile(def.customPortraitPath);
                if (texture != null)
                {
                    return texture;
                }
            }
            
            // ✅ 尝试路径5：原版叙事者路径 UI/HeroArt/{Name}
            string heroArtPath = $"UI/HeroArt/{personaName}";
            texture = ContentFinder<Texture2D>.Get(heroArtPath, false);
            if (texture != null)
            {
                SetTextureQuality(texture);
                return texture;
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
            
            return null;
        }
        
        /// <summary>
        /// ��ȡ�������׺��·��
        /// </summary>
        private static string GetExpressionPath(string basePath, string expressionSuffix)
        {
            if (string.IsNullOrEmpty(expressionSuffix))
            {
                return basePath;
            }
            
            // �����ļ�������չ��
            string directory = Path.GetDirectoryName(basePath) ?? "";
            string fileNameWithoutExt = Path.GetFileNameWithoutExtension(basePath);
            string extension = Path.GetExtension(basePath);
            
            // �����������·��
            string newFileName = fileNameWithoutExt + expressionSuffix + extension;
            
            if (string.IsNullOrEmpty(directory))
            {
                return newFileName;
            }
            
            return Path.Combine(directory, newFileName);
        }
        
        /// <summary>
        /// ���ⲿ�ļ���������
        /// ? ֧���ļ�·����ContentFinder·��
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
                
                // 注意：不调用 texture.Apply()，因为 ContentFinder 加载的纹理是只读的
                // Apply 会触发 "Texture not readable" 错误
            }
            catch
            {
                // 静默忽略，纹理设置不是关键功能
            }
        }
        
        /// <summary>
        /// ���ɸĽ���ռλ����� - ��������˸��ʶ
        /// ? �ò�ͬ�˸��ռλ������������
        /// </summary>
        private static Texture2D GeneratePlaceholder(Color color)
        {
            int size = 512;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            
            // 1. �������䱳��������ɫ����ɫ��
            Color darkColor = color * 0.3f;
            Color lightColor = color * 1.2f;
            
            for (int y = 0; y < size; y++)
            {
                float t = y / (float)size;
                Color gradientColor = Color.Lerp(darkColor, lightColor, t);
                
                for (int x = 0; x < size; x++)
                {
                    // ��Ӿ��򽥱�Ч��
                    float distFromCenter = Vector2.Distance(
                        new Vector2(x, y), 
                        new Vector2(size / 2f, size / 2f)
                    ) / (size / 2f);
                    
                    Color finalColor = Color.Lerp(gradientColor, darkColor, distFromCenter * 0.3f);
                    texture.SetPixel(x, y, finalColor);
                }
            }
            
            // 2. ��������Բ����װ�Σ�
            DrawCircle(texture, size / 2, size / 2, size / 3, Color.white * 0.1f, 4);
            DrawCircle(texture, size / 2, size / 2, size / 4, Color.white * 0.15f, 3);
            
            // 3. ���ƽ���װ�Σ��Ƽ��У�
            DrawCornerLines(texture, color * 1.5f);
            
            texture.Apply();
            return texture;
        }
        
        /// <summary>
        /// �������ϻ���Բ��
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
        /// ���ƽ���װ���ߣ��Ƽ��У�
        /// </summary>
        private static void DrawCornerLines(Texture2D texture, Color color)
        {
            int size = texture.width;
            int lineLength = size / 8;
            int thickness = 3;
            
            // �ĸ�������� L ��װ��
            Color lineColor = new Color(color.r, color.g, color.b, 0.3f);
            
            // ���Ͻ�
            DrawLine(texture, 20, 20, 20 + lineLength, 20, lineColor, thickness);
            DrawLine(texture, 20, 20, 20, 20 + lineLength, lineColor, thickness);
            
            // ���Ͻ�
            DrawLine(texture, size - 20, 20, size - 20 - lineLength, 20, lineColor, thickness);
            DrawLine(texture, size - 20, 20, size - 20, 20 + lineLength, lineColor, thickness);
            
            // ���½�
            DrawLine(texture, 20, size - 20, 20 + lineLength, size - 20, lineColor, thickness);
            DrawLine(texture, 20, size - 20, 20, size - 20 - lineLength, lineColor, thickness);
            
            // ���½�
            DrawLine(texture, size - 20, size - 20, size - 20 - lineLength, size - 20, lineColor, thickness);
            DrawLine(texture, size - 20, size - 20, size - 20, size - 20 - lineLength, lineColor, thickness);
        }
        
        /// <summary>
        /// ����ֱ��
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
                // ���ƴ���
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
        /// ��ջ���
        /// </summary>
        public static void ClearCache()
        {
            cache.Clear();
            Log.Message("[PortraitLoader] 立绘缓存已清空");
        }
        
        /// <summary>
        /// ✅ v1.6.21: 清空所有缓存（用于模式切换）
        /// </summary>
        public static void ClearAllCache()
        {
            cache.Clear();
            Log.Message("[PortraitLoader] 所有立绘缓存已清空");
        }
        
        /// <summary>
        /// ? ����ض��˸���ض����黺��
        /// </summary>
        public static void ClearPortraitCache(string personaDefName, ExpressionType expression)
        {
            string expressionSuffix = ExpressionSystem.GetExpressionSuffix(personaDefName, expression);  // ✅ 添加 personaDefName 参数
            string cacheKey = personaDefName + expressionSuffix;
            
            if (cache.ContainsKey(cacheKey))
            {
                cache.Remove(cacheKey);
                Log.Message($"[PortraitLoader] �������: {cacheKey}");
            }
        }
        
        /// <summary>
        /// ��ȡ�������׺��·��
        /// </summary>
        public static string GetModPortraitsDirectory()
        {
            // ��ȡ Mod ��Ŀ¼
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
                        Log.Message($"[PortraitLoader] ���� Mod ����Ŀ¼: {path}");
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[PortraitLoader] �޷����� Mod Ŀ¼: {path}\n{ex}");
                }
                
                return path;
            }
            
            // ����·��������Ҳ��� Mod��
            return Path.Combine(GenFilePaths.SaveDataFolderPath, "TheSecondSeat", "Portraits");
        }
        
        /// <summary>
        /// �� Mod ����Ŀ¼
        /// ? �������� Mod ��������Ŀ¼��������ʹ�ã�
        /// </summary>
        public static void OpenModPortraitsDirectory()
        {
            string path = GetModPortraitsDirectory();
            
            try
            {
                Application.OpenURL("file://" + path);
                Messages.Message($"�Ѵ� Mod ����Ŀ¼:\n{path}\n\n�뽫 PNG �ļ������Ŀ¼", MessageTypeDefOf.NeutralEvent);
            }
            catch (Exception ex)
            {
                Log.Error($"[PortraitLoader] �޷���Ŀ¼: {ex}");
                Messages.Message($"���ֶ���Ŀ¼:\n{path}", MessageTypeDefOf.RejectInput);
            }
        }
        
        /// <summary>
        /// ��ȡ�Ƽ����û�����Ŀ¼
        /// ? ����ԭ�й��ܣ���Ҹ��İ�ʹ�ã�
        /// </summary>
        public static string GetUserPortraitsDirectory()
        {
            string path = Path.Combine(GenFilePaths.SaveDataFolderPath, "TheSecondSeat", "Portraits");
            
            try
            {
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                    Log.Message($"[PortraitLoader] �����û�����Ŀ¼: {path}");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[PortraitLoader] �޷�����Ŀ¼: {path}\n{ex}");
            }
            
            return path;
        }
        
        /// <summary>
        /// ���û�����Ŀ¼
        /// ? ����ԭ�й��ܣ���Ҹ��İ�ʹ�ã�
        /// </summary>
        public static void OpenUserPortraitsDirectory()
        {
            string path = GetUserPortraitsDirectory();
            
            try
            {
                Application.OpenURL("file://" + path);
                Messages.Message($"�Ѵ��û�����Ŀ¼:\n{path}\n\n�žؼ PNG �� JPG �ļ����Ƶ���Ŀ¼", MessageTypeDefOf.NeutralEvent);
            }
            catch (Exception ex)
            {
                Log.Error($"[PortraitLoader] �o����Ŀ¼: {ex}");
                Messages.Message($"���ֶ���Ŀ¼:\n{path}", MessageTypeDefOf.RejectInput);
            }
        }
        
        /// <summary>
        /// ��ȡ Mod �����ļ��б�
        /// ? ��������ȡ Mod Ŀ¼�е������ļ�
        /// </summary>
        public static List<string> GetModPortraitFiles()
        {
            List<string> files = new List<string>();
            
            try
            {
                string modDir = GetModPortraitsDirectory();
                
                if (Directory.Exists(modDir))
                {
                    // PNG �ļ�
                    files.AddRange(Directory.GetFiles(modDir, "*.png"));
                    // JPG �ļ�
                    files.AddRange(Directory.GetFiles(modDir, "*.jpg"));
                    files.AddRange(Directory.GetFiles(modDir, "*.jpeg"));
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[PortraitLoader] ��ȡ Mod ����ʧ��: {ex}");
            }
            
            return files;
        }
        
        /// <summary>
        /// ��ȡ�û������ļ��б�
        /// </summary>
        public static List<string> GetUserPortraitFiles()
        {
            List<string> files = new List<string>();
            
            try
            {
                string userDir = GetUserPortraitsDirectory();
                
                if (Directory.Exists(userDir))
                {
                    // PNG �ļ�
                    files.AddRange(Directory.GetFiles(userDir, "*.png"));
                    // JPG �ļ�
                    files.AddRange(Directory.GetFiles(userDir, "*.jpg"));
                    files.AddRange(Directory.GetFiles(userDir, "*.jpeg"));
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[PortraitLoader] ��ȡ�û�����ʧ��: {ex}");
            }
            
            return files;
        }
        
        /// <summary>
        /// ��ȡ���פ��õ������ľ���
        /// ? ����������ԭ������������ + Mod���� + �û�����
        /// </summary>
        public static List<PortraitFileInfo> GetAllAvailablePortraits()
        {
            var portraits = new List<PortraitFileInfo>();
            
            try
            {
                // 1. ���ԭ�� RimWorld ����������
                portraits.AddRange(GetVanillaStorytellerPortraits());
                
                // 2. ������� Mod ������������
                portraits.AddRange(GetModStorytellerPortraits());
                
                // 3. ��ӱ� Mod �Դ�����
                portraits.AddRange(GetModPortraitFilesWithInfo());
                
                // 4. ����û��Զ�������
                portraits.AddRange(GetUserPortraitFilesWithInfo());
            }
            catch (Exception ex)
            {
                Log.Error($"[PortraitLoader] ��ȡ���������б�ʧ��: {ex}");
            }
            
            return portraits;
        }
        
        /// <summary>
        /// ��ȡԭ�� RimWorld ����������
        /// ? Cassandra, Phoebe, Randy ��
        /// </summary>
        private static List<PortraitFileInfo> GetVanillaStorytellerPortraits()
        {
            var portraits = new List<PortraitFileInfo>();
            
            try
            {
                // ԭ���������б�
                var vanillaStorytellers = new[]
                {
                    "Cassandra",
                    "Phoebe", 
                    "Randy",
                    "Igor"  // DLC ������
                };
                
                foreach (var storyteller in vanillaStorytellers)
                {
                    // RimWorld ԭ������·����UI/HeroArt/{StorytellerName}
                    string texturePath = $"UI/HeroArt/{storyteller}";
                    var texture = ContentFinder<Texture2D>.Get(texturePath, false);
                    
                    if (texture != null)
                    {
                        portraits.Add(new PortraitFileInfo
                        {
                            Name = $"{storyteller} (ԭ��)",
                            Path = texturePath,  // ? �޸�������Ӧ����string·��
                            Source = PortraitSource.Vanilla,
                            Texture = texture  // ? Texture������ֵ
                        });
                        
                        Log.Message($"[PortraitLoader] �ڵ��Ǵ�ԭ������: {storyteller}");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[PortraitLoader] ��ȡԭ������ʧ��: {ex.Message}");
            }
            
            return portraits;
        }
        
        /// <summary>
        /// ��ȡ���� Mod ������������
        /// ? �Զ��������� Mod �� StorytellerDefs
        /// </summary>
        private static List<PortraitFileInfo> GetModStorytellerPortraits()
        {
            var portraits = new List<PortraitFileInfo>();
            
            try
            {
                // ��ȡ���������߶��壨���� Mod ��ӵģ�
                var allStorytellers = DefDatabase<StorytellerDef>.AllDefsListForReading;
                
                foreach (var storyteller in allStorytellers)
                {
                    // ����ԭ�������ߣ��������洦���
                    if (storyteller.defName == "Cassandra" || 
                        storyteller.defName == "Phoebe" || 
                        storyteller.defName == "Randy" ||
                        storyteller.defName == "Igor")
                    {
                        continue;
                    }
                    
                    // ? �޸���StorytellerDef.portraitLargeTex �� Texture2D������ string
                    // ������Ҫͨ�� defName ����·��
                    var texture = storyteller.portraitLargeTex;
                    
                    if (texture != null)
                    {
                        // ��ȡ Mod ����
                        string modName = storyteller.modContentPack?.Name ?? "δ֪Mod";
                        
                        // ��������·����ͨ���� UI/HeroArt/{DefName}��
                        string portraitPath = $"UI/HeroArt/{storyteller.defName}";
                        
                        var portraitInfo = new PortraitFileInfo();
                        portraitInfo.Name = $"{storyteller.LabelCap} ({modName})";
                        portraitInfo.Path = portraitPath;  // ? ʹ�ù�����·��
                        portraitInfo.Source = PortraitSource.OtherMod;
                        portraitInfo.Texture = texture;  // ? ֱ��ʹ���Ѽ��ص�Texture
                        portraitInfo.ModName = modName;
                        
                        portraits.Add(portraitInfo);
                        
                        Log.Message($"[PortraitLoader] �ҵ�Mod����: {storyteller.LabelCap} from {modName}");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[PortraitLoader] ��ȡMod����ʧ��: {ex.Message}");
            }
            
            return portraits;
        }
        
        /// <summary>
        /// ��ȡ�� Mod �����ļ�������ϸ��Ϣ��
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
                Log.Warning($"[PortraitLoader] ��ȡ��Mod����ʧ��: {ex.Message}");
            }
            
            return portraits;
        }
        
        /// <summary>
        /// ��ȡ�û������ļ�������ϸ��Ϣ��
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
                        Name = Path.GetFileNameWithoutExtension(file) + " (�û�)",
                        Path = file,
                        Source = PortraitSource.User
                    });
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[PortraitLoader] ��ȡ�û�����ʧ��: {ex.Message}");
            }
            
            return portraits;
        }
        
        /// <summary>
        /// ��ȡ������Ϣ
        /// </summary>
        public static string GetDebugInfo()
        {
            var allPortraits = GetAllAvailablePortraits();
            
            var vanillaCount = allPortraits.Count(p => p.Source == PortraitSource.Vanilla);
            var modCount = allPortraits.Count(p => p.Source == PortraitSource.OtherMod);
            var thisModCount = allPortraits.Count(p => p.Source == PortraitSource.ThisMod);
            var userCount = allPortraits.Count(p => p.Source == PortraitSource.User);
            
            return $"[PortraitLoader] ��������: {cache.Count}\n" +
                   $"������������: {allPortraits.Count}\n" +
                   $"  - ԭ������: {vanillaCount}\n" +
                   $"  - ����Mod����: {modCount}\n" +
                   $"  - ��Mod����: {thisModCount}\n" +
                   $"  - �û�����: {userCount}\n" +
                   $"Mod ����Ŀ¼: {GetModPortraitsDirectory()}\n" +
                   $"�û�����Ŀ¼: {GetUserPortraitsDirectory()}";
        }
        
        /// <summary>
        /// ? ���ӷ�װ���
        /// </summary>
        private static Texture2D ApplyOutfit(Texture2D baseTexture, string personaDefName)
        {
            // ✅ 直接返回基础纹理，不进行服装合成
            // 原因：ContentFinder加载的纹理默认不可读，无法调用GetPixel()
            // 如果需要服装系统，应该预先合成好完整立绘，而不是运行时合成
            return baseTexture;
            
            /*
            // 以下代码保留作为参考，但禁用以避免错误
            string outfitPath = OutfitSystem.GetCurrentOutfitPath(personaDefName);
            
            if (string.IsNullOrEmpty(outfitPath))
            {
                return baseTexture;
            }
            
            var outfitTexture = ContentFinder<Texture2D>.Get(outfitPath, false);
            
            if (outfitTexture == null)
            {
                return baseTexture;
            }
            
            return CompositeTextures(baseTexture, outfitTexture);
            */
        }
        
        /// <summary>
        /// ? �ϳ������������������ + ��װ/���飩
        /// </summary>
        private static Texture2D CompositeTextures(Texture2D bottom, Texture2D top)
        {
            try
            {
                // ȷ����

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
        /// ✅ v1.6.34: 获取单个图层纹理（用于运行时分层绘制）
        /// </summary>
        /// <param name="def">人格定义</param>
        /// <param name="layerName">图层名称（如 "base_body", "happy_eyes", "small_mouth"）</param>
        /// <returns>图层纹理，如果未找到则返回null</returns>
        public static Texture2D GetLayerTexture(NarratorPersonaDef def, string layerName)
        {
            if (def == null || string.IsNullOrEmpty(layerName))
            {
                return null;
            }
            
            // 1. 生成缓存键
            string personaName = GetPersonaFolderName(def);
            string cacheKey = $"{personaName}_layer_{layerName}";
            
            // 2. 检查缓存
            if (cache.TryGetValue(cacheKey, out Texture2D cachedTexture))
            {
                return cachedTexture;
            }
            
            // 3. 构建图层路径
            string layerPath = $"UI/Narrators/9x16/Layered/{personaName}/{layerName}";
            
            // 4. 加载纹理
            Texture2D texture = ContentFinder<Texture2D>.Get(layerPath, false);
            
            if (texture != null)
            {
                // 设置纹理过滤模式为双线性（更平滑）
                texture.filterMode = FilterMode.Bilinear;
                texture.anisoLevel = 4; // 各向异性过滤（提升斜角质量）
                
                // 缓存纹理
                cache[cacheKey] = texture;
                
                if (Prefs.DevMode)
                {
                    Log.Message($"[PortraitLoader] Loaded layer: {layerPath} ({texture.width}x{texture.height})");
                }
            }
            else if (Prefs.DevMode)
            {
                Log.Warning($"[PortraitLoader] Layer not found: {layerPath}");
            }
            
            return texture;
        }
        
        /// <summary>
        /// ✅ v1.6.34: 批量获取多个图层纹理
        /// </summary>
        /// <param name="def">人格定义</param>
        /// <param name="layerNames">图层名称列表</param>
        /// <returns>图层名称→纹理的字典</returns>
        public static Dictionary<string, Texture2D> GetLayerTextures(NarratorPersonaDef def, params string[] layerNames)
        {
            var result = new Dictionary<string, Texture2D>();
            
            foreach (var layerName in layerNames)
            {
                var texture = GetLayerTexture(def, layerName);
                if (texture != null)
                {
                    result[layerName] = texture;
                }
            }
            
            return result;
        }
        
        /// <summary>
        /// ✅ v1.6.34: 清除特定图层的缓存
        /// </summary>
        public static void ClearLayerCache(NarratorPersonaDef def, string layerName)
        {
            string personaName = GetPersonaFolderName(def);
            string cacheKey = $"{personaName}_layer_{layerName}";
            cache.Remove(cacheKey);
        }
    }
}
