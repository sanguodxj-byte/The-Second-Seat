using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace TheSecondSeat.PersonaGeneration
{
    /// <summary>
    /// 头像加载器 - 专门用于UI按钮小头像
    /// 与 PortraitLoader（全身立绘）区分开
    /// ? 优化：静默回退机制，避免日志刷屏
    /// </summary>
    public static class AvatarLoader
    {
        private static Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();
        
        // 头像文件路径（512x512 头像资源）
        private const string AVATARS_PATH = "UI/Narrators/Avatars/";
        
        /// <summary>
        /// 加载头像（UI按钮专用）
        /// ? 优化：静默回退，不刷屏日志
        /// ? 支持表情变体系统
        /// </summary>
        /// <param name="def">人格定义</param>
        /// <param name="expression">表情类型</param>
        /// <returns>头像纹理</returns>
        public static Texture2D LoadAvatar(NarratorPersonaDef def, ExpressionType? expression = null)
        {
            if (def == null)
            {
                // 只在致命错误时输出
                if (Prefs.DevMode) Log.Warning("[AvatarLoader] PersonaDef is null");
                return GeneratePlaceholder(Color.gray);
            }
            
            // ? 确定表情后缀（支持变体选择）
            string expressionSuffix = "";
            if (expression.HasValue && expression.Value != ExpressionType.Neutral)
            {
                // ? 使用 ExpressionSystem 的回退机制
                expressionSuffix = ExpressionSystem.GetExpressionSuffix(def.defName, expression.Value);
            }
            
            // 缓存检查
            // ? v1.6.21: 添加 _avatar_ 标识，避免与 PortraitLoader 缓存冲突
            string cacheKey = $"{def.defName}_avatar_{expressionSuffix}";
            if (cache.TryGetValue(cacheKey, out Texture2D cached))
            {
                return cached;
            }
            
            Texture2D texture = null;
            
            // 1. 尝试加载表情头像文件
            string personaName = GetPersonaName(def);
            
            if (expression.HasValue && expression.Value != ExpressionType.Neutral)
            {
                // ? 从后缀中获取文件名（已经包含变体号）
                string expressionFileName = expressionSuffix.TrimStart('_').ToLower();
                
                string avatarPath = $"{AVATARS_PATH}{personaName}/{expressionFileName}";
                texture = ContentFinder<Texture2D>.Get(avatarPath, false);
                
                // ? 移除成功日志，只在DevMode下输出
                if (texture != null && Prefs.DevMode)
                {
                    Log.Message($"[AvatarLoader] ? 加载表情头像: {avatarPath}");
                }
            }
            
            // 2. 如果没有表情头像，尝试加载基础头像
            if (texture == null)
            {
                string[] baseFileNames = new[] { "base", "neutral", "Base", "Neutral", "default", "Default" };
                
                foreach (var baseName in baseFileNames)
                {
                    string baseAvatarPath = $"{AVATARS_PATH}{personaName}/{baseName}";
                    texture = ContentFinder<Texture2D>.Get(baseAvatarPath, false);
                    
                    if (texture != null)
                    {
                        // ? 移除成功日志，只在DevMode下输出
                        if (Prefs.DevMode)
                        {
                            Log.Message($"[AvatarLoader] ? 加载基础头像: {baseAvatarPath}");
                        }
                        SetTextureQualitySafe(texture);
                        break;
                    }
                }
            }
            
            // 3. 兜底：使用立绘裁剪
            if (texture == null)
            {
                var portrait = PortraitLoader.LoadPortrait(def, expression);
                if (portrait != null)
                {
                    // ? 移除日志，静默裁剪
                    texture = CropHeadFromPortraitSafe(portrait);
                }
            }
            
            // 4. 最终占位符
            if (texture == null)
            {
                // ? 只在完全失败时输出警告
                Log.Warning($"[AvatarLoader] ? 所有加载方式失败，使用占位符: {def.defName}{expressionSuffix}");
                texture = GeneratePlaceholder(def.primaryColor);
            }
            
            // 缓存
            cache[cacheKey] = texture;
            return texture;
        }
        
        /// <summary>
        /// ? 安全地从立绘裁剪头部区域（处理不可读纹理）
        /// </summary>
        private static Texture2D CropHeadFromPortraitSafe(Texture2D portrait)
        {
            try
            {
                // ? 先将纹理转换为可读格式
                Texture2D readable = MakeReadable(portrait);
                if (readable == null)
                {
                    Log.Warning("[AvatarLoader] 无法使纹理可读，直接返回原纹理");
                    return portrait;
                }
                
                // 裁剪上半部分（头部区域）
                int cropSize = Mathf.Min(512, Mathf.Min(readable.width, readable.height));
                int sourceWidth = readable.width;
                int sourceHeight = readable.height;
                
                // 从顶部裁剪区域（居中）
                int startX = (sourceWidth - cropSize) / 2;
                int startY = sourceHeight - cropSize; // Unity 纹理 Y 轴从底部开始
                
                // 确保不超出边界
                startX = Mathf.Clamp(startX, 0, Mathf.Max(0, sourceWidth - cropSize));
                startY = Mathf.Clamp(startY, 0, Mathf.Max(0, sourceHeight - cropSize));
                
                // 确保裁剪尺寸有效
                int actualCropWidth = Mathf.Min(cropSize, sourceWidth - startX);
                int actualCropHeight = Mathf.Min(cropSize, sourceHeight - startY);
                
                if (actualCropWidth <= 0 || actualCropHeight <= 0)
                {
                    Log.Warning("[AvatarLoader] 裁剪尺寸无效，返回原纹理");
                    return readable;
                }
                
                Texture2D cropped = new Texture2D(actualCropWidth, actualCropHeight, TextureFormat.RGBA32, false);
                Color[] pixels = readable.GetPixels(startX, startY, actualCropWidth, actualCropHeight);
                cropped.SetPixels(pixels);
                cropped.Apply();
                
                // 设置质量
                cropped.filterMode = FilterMode.Bilinear;
                cropped.anisoLevel = 4;
                
                return cropped;
            }
            catch (Exception ex)
            {
                Log.Error($"[AvatarLoader] 裁剪头像失败: {ex.Message}");
                return portrait;
            }
        }
        
        /// <summary>
        /// ? 将纹理转换为可读格式
        /// </summary>
        private static Texture2D MakeReadable(Texture2D source)
        {
            if (source == null) return null;
            
            // 先尝试直接读取
            try
            {
                source.GetPixel(0, 0);
                return source; // 已经可读
            }
            catch
            {
                // 需要转换
            }
            
            try
            {
                // 使用 RenderTexture 来复制纹理数据
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
            catch (Exception ex)
            {
                Log.Error($"[AvatarLoader] MakeReadable 失败: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// 获取人格名称
        /// </summary>
        private static string GetPersonaName(NarratorPersonaDef def)
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
        /// 生成占位符
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
        /// 清空缓存
        /// </summary>
        public static void ClearCache()
        {
            cache.Clear();
            Log.Message("[AvatarLoader] 头像缓存已清空");
        }
        
        /// <summary>
        /// ? v1.6.21: 清空所有缓存（用于模式切换）
        /// </summary>
        public static void ClearAllCache()
        {
            cache.Clear();
            Log.Message("[AvatarLoader] 所有头像缓存已清空");
        }

        /// <summary>
        /// ? 清除特定人格的特定表情头像缓存
        /// </summary>
        public static void ClearAvatarCache(string personaDefName, ExpressionType expression)
        {
            string expressionSuffix = "";
            if (expression != ExpressionType.Neutral)
            {
                expressionSuffix = ExpressionSystem.GetExpressionSuffix(personaDefName, expression);
            }
            
            string cacheKey = personaDefName + "_avatar" + expressionSuffix;
            
            if (cache.ContainsKey(cacheKey))
            {
                cache.Remove(cacheKey);
                Log.Message($"[AvatarLoader] 清除头像缓存: {cacheKey}");
            }
        }

        /// <summary>
        /// ? 设置纹理高质量参数（安全版本）
        /// </summary>
        private static void SetTextureQualitySafe(Texture2D texture)
        {
            if (texture == null) return;
            
            try
            {
                // 只设置过滤模式，不调用 Apply（避免不可读纹理错误）
                texture.filterMode = FilterMode.Bilinear;
                texture.anisoLevel = 4;
                // 注意：不调用 texture.Apply()
            }
            catch
            {
                // 静默忽略
            }
        }
    }
}
