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
    /// </summary>
    public static class LayeredPortraitCompositor
    {
        // 合成缓存（避免重复合成相同配置）
        private static Dictionary<string, Texture2D> compositeCache = new Dictionary<string, Texture2D>();

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
            if (config == null)
            {
                Log.Error("[LayeredPortraitCompositor] Config is null");
                return GenerateFallbackTexture(config?.OutputSize ?? new Vector2Int(1024, 1572));
            }

            // 验证配置
            if (!config.Validate(out string error))
            {
                Log.Error($"[LayeredPortraitCompositor] Invalid config: {error}");
                return GenerateFallbackTexture(config.OutputSize);
            }

            // 生成缓存键
            string cacheKey = GenerateCacheKey(config.PersonaDefName, expression, outfit);

            // 检查缓存
            if (config.EnableCache && compositeCache.TryGetValue(cacheKey, out Texture2D cached))
            {
                if (Prefs.DevMode)
                {
                    Log.Message($"[LayeredPortraitCompositor] Using cached composite: {cacheKey}");
                }
                return cached;
            }

            // 获取当前条件下的活动图层
            var activeLayers = config.GetActiveLayersForCondition(expression, outfit);

            if (activeLayers.Count == 0)
            {
                Log.Warning($"[LayeredPortraitCompositor] No active layers for {config.PersonaDefName} ({expression}, {outfit})");
                return GenerateFallbackTexture(config.OutputSize);
            }

            // 加载所有图层纹理
            var layerTextures = new List<(LayerDefinition layer, Texture2D texture)>();

            foreach (var layer in activeLayers)
            {
                string texturePath = layer.ResolveTexturePath(
                    GetPersonaFolderName(config.PersonaDefName), 
                    expression, 
                    outfit
                );

                Texture2D texture = LoadLayerTexture(texturePath, layer.Required);

                if (texture != null)
                {
                    layerTextures.Add((layer, texture));
                }
                else if (layer.Required)
                {
                    Log.Error($"[LayeredPortraitCompositor] Required layer missing: {layer.Name} ({texturePath})");
                    return GenerateFallbackTexture(config.OutputSize);
                }
                else
                {
                    if (Prefs.DevMode)
                    {
                        Log.Message($"[LayeredPortraitCompositor] Optional layer not found: {layer.Name} ({texturePath})");
                    }
                }
            }

            if (layerTextures.Count == 0)
            {
                Log.Error($"[LayeredPortraitCompositor] No layers loaded for {config.PersonaDefName}");
                return GenerateFallbackTexture(config.OutputSize);
            }

            // 执行合成
            Texture2D result = PerformComposite(layerTextures, config.OutputSize);

            // 缓存结果
            if (config.EnableCache)
            {
                compositeCache[cacheKey] = result;
            }

            if (Prefs.DevMode)
            {
                Log.Message($"[LayeredPortraitCompositor] Composite created: {cacheKey} ({layerTextures.Count} layers)");
            }

            return result;
        }

        /// <summary>
        /// 执行实际的图层合成
        /// </summary>
        private static Texture2D PerformComposite(
            List<(LayerDefinition layer, Texture2D texture)> layerTextures,
            Vector2Int outputSize)
        {
            // 创建输出纹理
            Texture2D result = new Texture2D(outputSize.x, outputSize.y, TextureFormat.RGBA32, false);

            // 初始化为透明
            Color[] pixels = new Color[outputSize.x * outputSize.y];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = Color.clear;
            }
            result.SetPixels(pixels);

            // 按优先级顺序合成图层
            foreach (var (layer, texture) in layerTextures)
            {
                BlendLayer(result, texture, layer);
            }

            result.Apply();
            return result;
        }

        /// <summary>
        /// 将单个图层混合到结果纹理
        /// </summary>
        private static void BlendLayer(Texture2D result, Texture2D layerTexture, LayerDefinition layer)
        {
            // 确保纹理可读
            Texture2D readableLayer = MakeReadable(layerTexture);

            if (readableLayer == null)
            {
                Log.Warning($"[LayeredPortraitCompositor] Cannot make layer readable: {layer.Name}");
                return;
            }

            int width = result.width;
            int height = result.height;

            // 获取像素数据
            Color[] resultPixels = result.GetPixels();
            Color[] layerPixels = readableLayer.GetPixels();

            // 计算偏移
            int offsetX = Mathf.RoundToInt(layer.Offset.x);
            int offsetY = Mathf.RoundToInt(layer.Offset.y);

            // 逐像素混合
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    // 计算源纹理坐标（考虑缩放和偏移）
                    int srcX = Mathf.RoundToInt((x - offsetX) / layer.Scale);
                    int srcY = Mathf.RoundToInt((y - offsetY) / layer.Scale);

                    // 边界检查
                    if (srcX < 0 || srcX >= readableLayer.width || srcY < 0 || srcY >= readableLayer.height)
                    {
                        continue;
                    }

                    int resultIndex = y * width + x;
                    int layerIndex = srcY * readableLayer.width + srcX;

                    // 获取颜色
                    Color bgColor = resultPixels[resultIndex];
                    Color fgColor = layerPixels[layerIndex];

                    // 应用色调调整
                    fgColor = fgColor * layer.Tint;

                    // 应用不透明度
                    fgColor.a *= layer.Opacity;

                    // 执行混合
                    Color blended = BlendColors(bgColor, fgColor, layer.Blend);
                    resultPixels[resultIndex] = blended;
                }
            }

            result.SetPixels(resultPixels);
        }

        /// <summary>
        /// 混合两个颜色
        /// </summary>
        private static Color BlendColors(Color background, Color foreground, BlendMode mode)
        {
            float alpha = foreground.a;

            if (alpha <= 0)
            {
                return background;
            }

            switch (mode)
            {
                case BlendMode.Normal:
                    return BlendNormal(background, foreground);

                case BlendMode.Multiply:
                    return BlendMultiply(background, foreground, alpha);

                case BlendMode.Screen:
                    return BlendScreen(background, foreground, alpha);

                case BlendMode.Overlay:
                    return BlendOverlay(background, foreground, alpha);

                case BlendMode.Additive:
                    return BlendAdditive(background, foreground, alpha);

                default:
                    return BlendNormal(background, foreground);
            }
        }

        /// <summary>
        /// 标准 Alpha 混合
        /// </summary>
        private static Color BlendNormal(Color bg, Color fg)
        {
            float alpha = fg.a;
            return new Color(
                bg.r * (1 - alpha) + fg.r * alpha,
                bg.g * (1 - alpha) + fg.g * alpha,
                bg.b * (1 - alpha) + fg.b * alpha,
                Mathf.Max(bg.a, fg.a)
            );
        }

        /// <summary>
        /// 正片叠底混合
        /// </summary>
        private static Color BlendMultiply(Color bg, Color fg, float alpha)
        {
            Color result = new Color(
                bg.r * fg.r,
                bg.g * fg.g,
                bg.b * fg.b,
                bg.a
            );
            return Color.Lerp(bg, result, alpha);
        }

        /// <summary>
        /// 滤色混合
        /// </summary>
        private static Color BlendScreen(Color bg, Color fg, float alpha)
        {
            Color result = new Color(
                1 - (1 - bg.r) * (1 - fg.r),
                1 - (1 - bg.g) * (1 - fg.g),
                1 - (1 - bg.b) * (1 - fg.b),
                bg.a
            );
            return Color.Lerp(bg, result, alpha);
        }

        /// <summary>
        /// 叠加混合
        /// </summary>
        private static Color BlendOverlay(Color bg, Color fg, float alpha)
        {
            float r = bg.r < 0.5f ? 2 * bg.r * fg.r : 1 - 2 * (1 - bg.r) * (1 - fg.r);
            float g = bg.g < 0.5f ? 2 * bg.g * fg.g : 1 - 2 * (1 - bg.g) * (1 - fg.g);
            float b = bg.b < 0.5f ? 2 * bg.b * fg.b : 1 - 2 * (1 - bg.b) * (1 - fg.b);

            Color result = new Color(r, g, b, bg.a);
            return Color.Lerp(bg, result, alpha);
        }

        /// <summary>
        /// 加法混合（光效专用）
        /// </summary>
        private static Color BlendAdditive(Color bg, Color fg, float alpha)
        {
            return new Color(
                Mathf.Clamp01(bg.r + fg.r * alpha),
                Mathf.Clamp01(bg.g + fg.g * alpha),
                Mathf.Clamp01(bg.b + fg.b * alpha),
                Mathf.Max(bg.a, fg.a)
            );
        }

        /// <summary>
        /// 加载图层纹理
        /// </summary>
        private static Texture2D LoadLayerTexture(string path, bool required)
        {
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            Texture2D texture = ContentFinder<Texture2D>.Get(path, false);

            if (texture == null && required && Prefs.DevMode)
            {
                Log.Warning($"[LayeredPortraitCompositor] Required texture not found: {path}");
            }

            return texture;
        }

        /// <summary>
        /// 将纹理转换为可读格式
        /// </summary>
        private static Texture2D MakeReadable(Texture2D source)
        {
            // 尝试直接读取
            try
            {
                source.GetPixel(0, 0);
                return source;
            }
            catch
            {
                // 需要转换
            }

            // 使用 RenderTexture 转换
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

        /// <summary>
        /// 生成缓存键
        /// </summary>
        private static string GenerateCacheKey(string personaDefName, ExpressionType expression, string outfit)
        {
            return $"{personaDefName}_{expression}_{outfit}";
        }

        /// <summary>
        /// 生成回退纹理（占位符）
        /// </summary>
        private static Texture2D GenerateFallbackTexture(Vector2Int size)
        {
            Texture2D fallback = new Texture2D(size.x, size.y, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[size.x * size.y];

            // 生成渐变占位符
            for (int y = 0; y < size.y; y++)
            {
                float t = y / (float)size.y;
                Color gradientColor = Color.Lerp(new Color(0.2f, 0.2f, 0.3f), new Color(0.3f, 0.3f, 0.4f), t);

                for (int x = 0; x < size.x; x++)
                {
                    pixels[y * size.x + x] = gradientColor;
                }
            }

            fallback.SetPixels(pixels);
            fallback.Apply();

            return fallback;
        }

        /// <summary>
        /// 根据 defName 获取人格文件夹名称
        /// </summary>
        private static string GetPersonaFolderName(string defName)
        {
            // 移除常见后缀
            string[] suffixesToRemove = new[] { "_Default", "_Classic", "_Custom", "_Persona" };
            foreach (var suffix in suffixesToRemove)
            {
                if (defName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                {
                    return defName.Substring(0, defName.Length - suffix.Length);
                }
            }

            // 如果有下划线，取第一部分
            if (defName.Contains("_"))
            {
                return defName.Split('_')[0];
            }

            return defName;
        }

        /// <summary>
        /// 清除缓存
        /// </summary>
        public static void ClearCache()
        {
            compositeCache.Clear();
            Log.Message("[LayeredPortraitCompositor] Cache cleared");
        }

        /// <summary>
        /// 清除特定人格的缓存
        /// </summary>
        public static void ClearCache(string personaDefName)
        {
            var keysToRemove = compositeCache.Keys.Where(k => k.StartsWith(personaDefName + "_")).ToList();
            foreach (var key in keysToRemove)
            {
                compositeCache.Remove(key);
            }
            Log.Message($"[LayeredPortraitCompositor] Cache cleared for {personaDefName}");
        }

        /// <summary>
        /// 获取缓存统计
        /// </summary>
        public static string GetCacheStats()
        {
            return $"[LayeredPortraitCompositor] Cache: {compositeCache.Count} entries";
        }
    }
}
