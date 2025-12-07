using System;
using UnityEngine;
using Verse;

namespace TheSecondSeat.PersonaGeneration
{
    /// <summary>
    /// 立绘分割配置
    /// 定义如何从特定位置分割立绘（如从脖子分割头部和身体）
    /// </summary>
    public class PortraitSplitConfig
    {
        /// <summary>
        /// 人格名称
        /// </summary>
        public string personaName;
        
        /// <summary>
        /// 分割类型
        /// </summary>
        public SplitType splitType;
        
        /// <summary>
        /// 水平分割线位置（Y坐标，从顶部开始，0-1范围）
        /// 例如：0.3 表示从顶部30%的位置分割
        /// </summary>
        public float horizontalSplitY = 0.3f;
        
        /// <summary>
        /// 垂直分割线位置（X坐标，从左边开始，0-1范围）
        /// </summary>
        public float verticalSplitX = 0.5f;
        
        /// <summary>
        /// 羽化边缘宽度（像素）
        /// 用于平滑过渡，避免生硬的切割线
        /// </summary>
        public int featherWidth = 5;
        
        /// <summary>
        /// 是否启用羽化
        /// </summary>
        public bool enableFeathering = true;
    }
    
    /// <summary>
    /// 分割类型
    /// </summary>
    public enum SplitType
    {
        Horizontal,     // 水平分割（用于分离头部和身体）
        Vertical,       // 垂直分割（用于分离左右半身）
        Custom          // 自定义分割线
    }
    
    /// <summary>
    /// 立绘自动分割和组合系统
    /// 
    /// 核心功能：
    /// 1. 从脖子位置自动分割头部和身体
    /// 2. 头部区域使用表情差分
    /// 3. 身体区域使用服装差分
    /// 4. 自动合成最终立绘
    /// 
    /// 使用场景：
    /// - base.png（完整立绘）
    /// - expression_happy.png（完整立绘，只有头部表情不同）
    /// - outfit_warm.png（完整立绘，只有服装不同）
    /// 
    /// 最终效果：
    /// base.png 的身体 + expression_happy.png 的头部 + outfit_warm.png 的服装
    /// </summary>
    public static class PortraitSplitter
    {
        /// <summary>
        /// 默认分割配置
        /// 头部范围：顶部0% - 30%（脖子位置）
        /// </summary>
        private static PortraitSplitConfig GetDefaultConfig(string personaName)
        {
            return new PortraitSplitConfig
            {
                personaName = personaName,
                splitType = SplitType.Horizontal,
                horizontalSplitY = 0.30f,  // 从顶部30%的位置分割（脖子）
                featherWidth = 10,         // 10像素羽化
                enableFeathering = true
            };
        }
        
        /// <summary>
        /// 自定义分割配置（每个角色可能不同）
        /// </summary>
        private static PortraitSplitConfig GetPersonaConfig(string personaName)
        {
            // 可以为不同角色定义不同的分割位置
            switch (personaName)
            {
                case "Cassandra_Classic":
                    return new PortraitSplitConfig
                    {
                        personaName = personaName,
                        splitType = SplitType.Horizontal,
                        horizontalSplitY = 0.28f,  // Cassandra 脖子位置稍高
                        featherWidth = 12,
                        enableFeathering = true
                    };
                    
                case "Phoebe_Chillax":
                    return new PortraitSplitConfig
                    {
                        personaName = personaName,
                        splitType = SplitType.Horizontal,
                        horizontalSplitY = 0.32f,  // Phoebe 脖子位置稍低
                        featherWidth = 10,
                        enableFeathering = true
                    };
                    
                default:
                    return GetDefaultConfig(personaName);
            }
        }
        
        /// <summary>
        /// 组合完整立绘：基础立绘 + 表情差分 + 服装差分
        /// 
        /// 流程：
        /// 1. 从 base.png 提取身体（脖子以下）
        /// 2. 从 expression.png 提取头部（脖子以上）
        /// 3. 从 outfit.png 提取服装（脖子以下，覆盖身体）
        /// 4. 合成：身体 + 服装 + 头部
        /// </summary>
        public static Texture2D ComposePortrait(
            Texture2D baseTexture,          // 基础立绘（完整）
            Texture2D expressionTexture,    // 表情差分（完整，头部不同）
            Texture2D outfitTexture,        // 服装差分（完整，服装不同）
            string personaName)
        {
            try
            {
                var config = GetPersonaConfig(personaName);
                
                // 确保所有纹理尺寸一致
                if (!ValidateTextureSizes(baseTexture, expressionTexture, outfitTexture))
                {
                    Log.Error("[PortraitSplitter] 纹理尺寸不一致");
                    return baseTexture;
                }
                
                int width = baseTexture.width;
                int height = baseTexture.height;
                
                // 计算分割线位置（像素）
                int splitLineY = Mathf.RoundToInt(height * config.horizontalSplitY);
                
                // 创建结果纹理
                Texture2D result = new Texture2D(width, height, TextureFormat.RGBA32, false);
                
                // 获取像素数据
                Color[] basePixels = MakeReadable(baseTexture).GetPixels();
                Color[] expressionPixels = expressionTexture != null ? MakeReadable(expressionTexture).GetPixels() : null;
                Color[] outfitPixels = outfitTexture != null ? MakeReadable(outfitTexture).GetPixels() : null;
                Color[] resultPixels = new Color[basePixels.Length];
                
                // 逐像素合成
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        int index = y * width + x;
                        
                        if (y < splitLineY)
                        {
                            // 头部区域：使用表情差分
                            if (expressionPixels != null)
                            {
                                resultPixels[index] = BlendWithFeathering(
                                    basePixels[index],
                                    expressionPixels[index],
                                    y,
                                    splitLineY,
                                    config,
                                    isHead: true
                                );
                            }
                            else
                            {
                                resultPixels[index] = basePixels[index];
                            }
                        }
                        else
                        {
                            // 身体区域：先用基础，再叠加服装
                            Color bodyColor = basePixels[index];
                            
                            if (outfitPixels != null)
                            {
                                // 叠加服装（使用 Alpha 混合）
                                bodyColor = BlendWithFeathering(
                                    bodyColor,
                                    outfitPixels[index],
                                    y,
                                    splitLineY,
                                    config,
                                    isHead: false
                                );
                            }
                            
                            resultPixels[index] = bodyColor;
                        }
                    }
                }
                
                result.SetPixels(resultPixels);
                result.Apply();
                
                Log.Message($"[PortraitSplitter] ? 合成完成: {personaName} (分割线: {splitLineY}px)");
                
                return result;
            }
            catch (Exception ex)
            {
                Log.Error($"[PortraitSplitter] 合成失败: {ex}");
                return baseTexture;
            }
        }
        
        /// <summary>
        /// 带羽化的混合
        /// 在分割线附近进行平滑过渡，避免生硬的切割线
        /// </summary>
        private static Color BlendWithFeathering(
            Color bottom,
            Color top,
            int currentY,
            int splitLineY,
            PortraitSplitConfig config,
            bool isHead)
        {
            if (!config.enableFeathering)
            {
                // 不启用羽化，直接使用顶层颜色
                return top;
            }
            
            // 计算距离分割线的距离
            int distanceFromSplit = Mathf.Abs(currentY - splitLineY);
            
            if (distanceFromSplit >= config.featherWidth)
            {
                // 远离分割线，直接使用对应区域的颜色
                return top;
            }
            
            // 在羽化范围内，进行平滑混合
            float blendFactor = (float)distanceFromSplit / config.featherWidth;
            
            if (isHead)
            {
                // 头部区域：距离分割线越近，越多使用基础纹理
                blendFactor = 1f - blendFactor;
            }
            
            // Alpha 混合
            return Color.Lerp(bottom, top, blendFactor);
        }
        
        /// <summary>
        /// 验证纹理尺寸是否一致
        /// </summary>
        private static bool ValidateTextureSizes(params Texture2D[] textures)
        {
            if (textures == null || textures.Length == 0)
                return false;
                
            int width = textures[0].width;
            int height = textures[0].height;
            
            foreach (var tex in textures)
            {
                if (tex == null)
                    continue;
                    
                if (tex.width != width || tex.height != height)
                {
                    Log.Warning($"[PortraitSplitter] 纹理尺寸不一致: {tex.width}x{tex.height} vs {width}x{height}");
                    return false;
                }
            }
            
            return true;
        }
        
        /// <summary>
        /// 将纹理转换为可读格式
        /// </summary>
        private static Texture2D MakeReadable(Texture2D source)
        {
            // 如果纹理已经可读，直接返回
            try
            {
                source.GetPixel(0, 0);
                return source;
            }
            catch
            {
                // 需要转换
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
        
        /// <summary>
        /// 调试：保存分割预览（用于调试分割线位置）
        /// </summary>
        public static void SaveSplitPreview(Texture2D texture, string personaName)
        {
            var config = GetPersonaConfig(personaName);
            int splitLineY = Mathf.RoundToInt(texture.height * config.horizontalSplitY);
            
            var preview = MakeReadable(texture);
            var pixels = preview.GetPixels();
            
            // 在分割线位置画一条红线
            for (int x = 0; x < preview.width; x++)
            {
                for (int offset = -2; offset <= 2; offset++)
                {
                    int y = splitLineY + offset;
                    if (y >= 0 && y < preview.height)
                    {
                        int index = y * preview.width + x;
                        pixels[index] = Color.red;
                    }
                }
            }
            
            preview.SetPixels(pixels);
            preview.Apply();
            
            // 保存到临时文件
            byte[] bytes = preview.EncodeToPNG();
            string path = System.IO.Path.Combine(
                Verse.GenFilePaths.SaveDataFolderPath,
                "TheSecondSeat",
                $"{personaName}_split_preview.png"
            );
            
            System.IO.File.WriteAllBytes(path, bytes);
            Log.Message($"[PortraitSplitter] 分割预览已保存: {path}");
        }
    }
}
