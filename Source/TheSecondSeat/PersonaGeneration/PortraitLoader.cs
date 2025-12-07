using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Verse;
using RimWorld;
using TheSecondSeat.Storyteller;

namespace TheSecondSeat.PersonaGeneration
{
    /// <summary>
    /// ������غ͹�����
    /// </summary>
    public static class PortraitLoader
    {
        private static Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();
        
        // ? ��������·��
        private const string BASE_PATH_9x16 = "UI/Narrators/9x16/";
        
        // ? �����ļ�·����������
        private const string EXPRESSIONS_PATH = "UI/Narrators/9x16/Expressions/";
        
        /// <summary>
        /// 加载立绘（支持 Mod资源、外部文件、占位符）
        /// ✅ 支持动态表情切换
        /// ✅ 支持表情叠加合成模式
        /// ✅ 支持表情文件夹新结构
        /// ✅ 支持服装叠加系统
        /// ✅ 支持自动裁剪合成（兼容性降级
        /// </summary>
        public static Texture2D LoadPortrait(NarratorPersonaDef def, ExpressionType? expression = null)
        {
            if (def == null)
            {
                Log.Warning("[PortraitLoader] PersonaDef is null");
                return GeneratePlaceholder(Color.gray);
            }
            
            // 确定表情后缀
            string expressionSuffix = "";
            if (expression.HasValue && expression.Value != ExpressionType.Neutral)
            {
                expressionSuffix = ExpressionSystem.GetExpressionSuffix(def.defName, expression.Value);
            }
            
            // 1. 检查缓存（包含表情后缀）
            string cacheKey = def.defName + expressionSuffix;
            if (cache.TryGetValue(cacheKey, out Texture2D cached))
            {
                return cached;
            }
            
            Texture2D texture = null;
            
            // 2. ✅ 修复：使用统一的加载方法，避免重复加载
            texture = LoadExpressionOrBase(def, expression);
            
            // 3. 如果还是没有，生成占位符
            if (texture == null)
            {
                Log.Warning($"[PortraitLoader] ✖ 所有加载方式失败，使用占位符: {def.defName}{expressionSuffix}");
                texture = GeneratePlaceholder(def.primaryColor);
            }
            
            // 缓存
            cache[cacheKey] = texture;
            return texture;
        }
        
        /// <summary>
        /// ✅ 统一的表情/基础立绘加载方法（避免重复加载）
        /// </summary>
        private static Texture2D LoadExpressionOrBase(NarratorPersonaDef def, ExpressionType? expression)
        {
            string personaName = GetPersonaFolderName(def);
            
            // 1. 如果有表情需求，先尝试加载表情立绘
            if (expression.HasValue && expression.Value != ExpressionType.Neutral)
            {
                string expressionSuffix = ExpressionSystem.GetExpressionSuffix(def.defName, expression.Value);
                string expressionFileName = expressionSuffix.TrimStart('_').ToLower();
                string expressionPath = $"{EXPRESSIONS_PATH}{personaName}/{expressionFileName}";
                
                var expressionTexture = ContentFinder<Texture2D>.Get(expressionPath, false);
                
                if (expressionTexture != null)
                {
                    SetTextureQuality(expressionTexture);
                    return expressionTexture;
                }
                
                // 尝试面部覆盖模式
                var overlayTexture = LoadWithFaceOverlay(def, expression.Value);
                if (overlayTexture != null)
                {
                    return overlayTexture;
                }
            }
            
            // 2. 没有表情或表情文件不存在，加载基础立绘
            return LoadBasePortrait(def);
        }
        
        /// <summary>
        /// ? �������� defName ��ȡ�˸��ļ�������
        /// ���磺Sideria_Default �� Sideria
        /// </summary>
        private static string GetPersonaFolderName(NarratorPersonaDef def)
        {
            // 1. 如果 narratorName 存在且不为空，使用它（去除空格和特殊字符）
            if (!string.IsNullOrEmpty(def.narratorName))
            {
                // 取第一个单词（如 "Cassandra Classic" → "Cassandra"）
                string name = def.narratorName.Split(' ')[0].Trim();
                if (!string.IsNullOrEmpty(name))
                {
                    return name;
                }
            }
            
            // 2. 否则从 defName 解析（去掉常见后缀）
            string defName = def.defName;
            
            // 去掉常见后缀
            string[] suffixesToRemove = new[] { "_Default", "_Classic", "_Custom", "_Persona", "_Chillax", "_Random", "_Invader", "_Protector" };
            foreach (var suffix in suffixesToRemove)
            {
                if (defName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                {
                    return defName.Substring(0, defName.Length - suffix.Length);
                }
            }
            
            // 3. 如果没有后缀，尝试用下划线分割取第一部分
            if (defName.Contains("_"))
            {
                return defName.Split('_')[0];
            }
            
            // 4. 直接返回 defName
            return defName;
        }
        
        /// <summary>
        /// ? ��������δ��棨�� Expressions �ļ��У�
        /// </summary>
        private static Texture2D LoadFullExpressionPortrait(NarratorPersonaDef def, string expressionSuffix)
        {
            if (string.IsNullOrEmpty(expressionSuffix))
            {
                return null;
            }
            
            // ? �޸�������ȷ���ļ��нṹ���ر���
            // ��ʽ��UI/Narrators/9x16/Expressions/{PersonaName}/{expression}.png
            string expressionFileName = expressionSuffix.TrimStart('_').ToLower(); // �Ƴ�ǰ׺ "_" ��تСд
            
            // ? �޸����� defName ��ȡ�˸�����
            string personaName = GetPersonaFolderName(def);
            string expressionPath = $"{EXPRESSIONS_PATH}{personaName}/{expressionFileName}";
            
            var texture = ContentFinder<Texture2D>.Get(expressionPath, false);
            
            if (texture != null)
            {
                Log.Message($"[PortraitLoader] ? �� Expressions ������ͼ����: {expressionPath}");
                return texture;
            }
            
            // ? ���������Ծ�·���������ԣ�- ��ʽ��UI/Narrators/9x16/Sideria_Happy
            if (!string.IsNullOrEmpty(def.portraitPath))
            {
                string oldPath = def.portraitPath + expressionSuffix;
                texture = ContentFinder<Texture2D>.Get(oldPath, false);
                
                if (texture != null)
                {
                    Log.Warning($"[PortraitLoader] ?? �Ӿ�·�����ر��飨��Ǩ�Ƶ� Expressions �ļ��У�: {oldPath}");
                    return texture;
                }
            }
            
            // ? �����������Զ���·��
            if (def.useCustomPortrait && !string.IsNullOrEmpty(def.customPortraitPath))
            {
                string customPath = GetExpressionPath(def.customPortraitPath, expressionSuffix);
                texture = LoadFromExternalFile(customPath);
                
                if (texture != null)
                {
                    Log.Message($"[PortraitLoader] ���Զ���·�����ر���: {customPath}");
                    return texture;
                }
            }
            
            Log.Warning($"[PortraitLoader] ? δ�ҵ������ļ�: {expressionPath}");
            return null;
        }
        
        /// <summary>
        /// ? �����沿���ӱ��飨�� Expressions �ļ��У�
        /// ? ����֧���沿���ǲ�ģʽ
        /// </summary>
        private static Texture2D LoadWithFaceOverlay(NarratorPersonaDef def, ExpressionType expression)
        {
            try
            {
                // 1. 加载基础立绘
                Texture2D baseTexture = LoadBasePortrait(def);
                if (baseTexture == null)
                {
                    return null;
                }
                
                // 2. ✅ 从 Expressions 文件夹加载面部叠加层（文件后缀 _face）
                string faceSuffix = ExpressionSystem.GetExpressionSuffix(def.defName, expression) + "_face";
                
                // ✅ 修复：使用正确的人格文件夹名称
                string personaName = GetPersonaFolderName(def);
                string facePath = $"{EXPRESSIONS_PATH}{personaName}/{faceSuffix}";
                
                Texture2D faceTexture = ContentFinder<Texture2D>.Get(facePath, false);
                
                // ✅ 尝试旧的直接路径
                if (faceTexture == null && !string.IsNullOrEmpty(def.portraitPath))
                {
                    string oldFacePath = def.portraitPath + faceSuffix;
                    faceTexture = ContentFinder<Texture2D>.Get(oldFacePath, false);
                    
                    if (faceTexture != null)
                    {
                        Log.Warning($"[PortraitLoader] ✅ 从旧路径加载面部叠加: {oldFacePath}");
                    }
                }
                
                // ✅ 尝试自定义路径
                if (faceTexture == null && def.useCustomPortrait && !string.IsNullOrEmpty(def.customPortraitPath))
                {
                    string customFacePath = GetExpressionPath(def.customPortraitPath, faceSuffix);
                    faceTexture = LoadFromExternalFile(customFacePath);
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
                
                Log.Message($"[PortraitLoader] ✅ 使用面部叠加模式: {personaName} + {faceSuffix}");
                return composite;
            }
            catch (Exception ex)
            {
                Log.Error($"[PortraitLoader] 面部叠合成失败: {ex}");
                return null;
            }
        }
        
        /// <summary>
        /// 加载基础立绘（无表情）
        /// ✅ 从人格文件夹的 base.png 加载
        /// ✅ 优化：设置高质量纹理过滤
        /// </summary>
        private static Texture2D LoadBasePortrait(NarratorPersonaDef def)
        {
            string personaName = GetPersonaFolderName(def);
            
            // ✅ 添加详细诊断日志
            Log.Message($"[PortraitLoader] ========== 立绘加载诊断 ==========");
            Log.Message($"[PortraitLoader] defName: {def.defName}");
            Log.Message($"[PortraitLoader] narratorName: {def.narratorName}");
            Log.Message($"[PortraitLoader] portraitPath: {def.portraitPath}");
            Log.Message($"[PortraitLoader] 解析的文件夹名: {personaName}");
            
            // ✅ 尝试路径1：9x16文件夹的 base.png
            string basePath = $"{BASE_PATH_9x16}{personaName}/base";
            Log.Message($"[PortraitLoader] 尝试路径1: {basePath}");
            
            var texture = ContentFinder<Texture2D>.Get(basePath, false);
            
            if (texture != null)
            {
                Log.Message($"[PortraitLoader] ✓ 路径1成功: {basePath}");
                SetTextureQuality(texture);
                return texture;
            }
            Log.Message($"[PortraitLoader] ✗ 路径1失败");
            
            // ✅ 尝试路径2：直接用 personaName（不加 /base）
            string path2 = $"{BASE_PATH_9x16}{personaName}";
            Log.Message($"[PortraitLoader] 尝试路径2: {path2}");
            
            texture = ContentFinder<Texture2D>.Get(path2, false);
            if (texture != null)
            {
                Log.Message($"[PortraitLoader] ✓ 路径2成功: {path2}");
                SetTextureQuality(texture);
                return texture;
            }
            Log.Message($"[PortraitLoader] ✗ 路径2失败");
            
            // ✅ 尝试路径3：使用 portraitPath
            if (!string.IsNullOrEmpty(def.portraitPath))
            {
                Log.Message($"[PortraitLoader] 尝试路径3 (portraitPath): {def.portraitPath}");
                texture = ContentFinder<Texture2D>.Get(def.portraitPath, false);
                if (texture != null)
                {
                    Log.Message($"[PortraitLoader] ✓ 路径3成功: {def.portraitPath}");
                    SetTextureQuality(texture);
                    return texture;
                }
                Log.Message($"[PortraitLoader] ✗ 路径3失败");
            }
            
            // ✅ 尝试路径4：自定义路径
            if (def.useCustomPortrait && !string.IsNullOrEmpty(def.customPortraitPath))
            {
                Log.Message($"[PortraitLoader] 尝试路径4 (customPortraitPath): {def.customPortraitPath}");
                texture = LoadFromExternalFile(def.customPortraitPath);
                if (texture != null)
                {
                    Log.Message($"[PortraitLoader] ✓ 路径4成功");
                    return texture;
                }
                Log.Message($"[PortraitLoader] ✗ 路径4失败");
            }
            
            // ✅ 尝试路径5：原版叙事者路径 UI/HeroArt/{Name}
            string heroArtPath = $"UI/HeroArt/{personaName}";
            Log.Message($"[PortraitLoader] 尝试路径5 (HeroArt): {heroArtPath}");
            texture = ContentFinder<Texture2D>.Get(heroArtPath, false);
            if (texture != null)
            {
                Log.Message($"[PortraitLoader] ✓ 路径5成功: {heroArtPath}");
                SetTextureQuality(texture);
                return texture;
            }
            Log.Message($"[PortraitLoader] ✗ 路径5失败");
            
            Log.Warning($"[PortraitLoader] ========== 所有路径都失败 ==========");
            Log.Warning($"[PortraitLoader] 请确保以下路径之一存在纹理文件:");
            Log.Warning($"[PortraitLoader]   - Textures/{basePath}.png");
            Log.Warning($"[PortraitLoader]   - Textures/{path2}.png");
            if (!string.IsNullOrEmpty(def.portraitPath))
                Log.Warning($"[PortraitLoader]   - Textures/{def.portraitPath}.png");
            Log.Warning($"[PortraitLoader]   - Textures/{heroArtPath}.png");
            
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
                    Log.Warning($"[PortraitLoader] 文件不存在: {filePath}");
                    return null;
                }
                
                byte[] fileData = File.ReadAllBytes(filePath);
                Texture2D loadedTexture = new Texture2D(2, 2);
                
                if (!loadedTexture.LoadImage(fileData))
                {
                    Log.Error($"[PortraitLoader] 无法加载图片: {filePath}");
                    return null;
                }
                
                // ✅ 设置高质量过滤模式
                SetTextureQuality(loadedTexture);
                
                return loadedTexture;
            }
            catch (Exception ex)
            {
                Log.Error($"[PortraitLoader] 加载失败: {filePath}\n{ex}");
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
        /// ���ɸĽ���ռλ������ - ��������˸��ʶ
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
            Log.Message("[PortraitLoader] ���滺�������");
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
        /// ��ȡ Mod ����Ŀ¼
        /// ? ���������� Mod ��������Ŀ¼
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
        /// ? ����ԭ�й��ܣ���Ҹ��Ի�ʹ�ã�
        /// </summary>
        public static void OpenUserPortraitsDirectory()
        {
            string path = GetUserPortraitsDirectory();
            
            try
            {
                Application.OpenURL("file://" + path);
                Messages.Message($"�Ѵ��û�����Ŀ¼:\n{path}\n\n�뽫 PNG �� JPG �ļ����Ƶ���Ŀ¼", MessageTypeDefOf.NeutralEvent);
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
        /// ��ȡ���פ��õ������ļ��б�
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
                        
                        Log.Message($"[PortraitLoader] �ҵ�ԭ������: {storyteller}");
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
                // ȷ���ߴ�һ��
                if (bottom.width != top.width || bottom.height != top.height)
                {
                    Log.Warning($"[PortraitLoader] ����ߴ粻һ��: {bottom.width}x{bottom.height} vs {top.width}x{top.height}");
                    return bottom;
                }
                
                // �����������
                Texture2D result = new Texture2D(bottom.width, bottom.height, TextureFormat.RGBA32, false);
                
                // ��ȡ��������
                Color[] bottomPixels = MakeReadable(bottom).GetPixels();
                Color[] topPixels = MakeReadable(top).GetPixels();
                Color[] resultPixels = new Color[bottomPixels.Length];
                
                // Alpha ���
                for (int i = 0; i < bottomPixels.Length; i++)
                {
                    Color bottomColor = bottomPixels[i];
                    Color topColor = topPixels[i];
                    
                    // Alpha ��Ϲ�ʽ
                    float alpha = topColor.a;
                    resultPixels[i] = new Color(
                        bottomColor.r * (1 - alpha) + topColor.r * alpha,
                        bottomColor.g * (1 - alpha) + topColor.g * alpha,
                        bottomColor.b * (1 - alpha) + topColor.b * alpha,
                        Mathf.Max(bottomColor.a, topColor.a)
                    );
                }
                
                result.SetPixels(resultPixels);
                result.Apply();
                
                return result;
            }
            catch (Exception ex)
            {
                Log.Error($"[PortraitLoader] ����ϳ�ʧ��: {ex}");
                return bottom;
            }
        }
        
        /// <summary>
        /// ������ت��Ϊ�ɶ���ʽ�������Ҫ��
        /// </summary>
        private static Texture2D MakeReadable(Texture2D source)
        {
            // ��������Ѿ��ɶ���ֱ�ӷ���
            try
            {
                source.GetPixel(0, 0);
                return source;
            }
            catch
            {
                // ��Ҫت��
            }
            
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
    }
    
    /// <summary>
    /// �����ļ���Ϣ
    /// ? ������������Դ�����ơ�·������ϸ��Ϣ
    /// </summary>
    public class PortraitFileInfo
    {
        public string Name { get; set; } = "";
        public string Path { get; set; } = "";
        public PortraitSource Source { get; set; }
        public string ModName { get; set; } = "";
        public Texture2D? Texture { get; set; }
    }
    
    /// <summary>
    /// ������Դ
    /// </summary>
    public enum PortraitSource
    {
        Vanilla,      // ԭ�� RimWorld
        OtherMod,     // ���� Mod
        ThisMod,      // �� Mod
        User          // �û��Զ���
    }
}
