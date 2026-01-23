using System;
using UnityEngine;
using Verse;

namespace TheSecondSeat.PersonaGeneration
{
    /// <summary>
    /// GPU 立绘渲染系统
    /// 负责使用 GPU (RenderTexture) 高效合成多层立绘，替代昂贵的 CPU GetPixels/SetPixels 操作
    /// </summary>
    public static class PortraitRenderSystem
    {
        // 默认渲染分辨率
        private const int DEFAULT_WIDTH = 512;
        private const int DEFAULT_HEIGHT = 512;

        /// <summary>
        /// 创建一个新的 RenderTexture
        /// </summary>
        public static RenderTexture CreateRenderTexture(int width, int height)
        {
            var rt = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32);
            rt.useMipMap = false;
            rt.filterMode = FilterMode.Bilinear;
            rt.anisoLevel = 0;
            rt.wrapMode = TextureWrapMode.Clamp; // 避免边缘杂色
            rt.Create();
            return rt;
        }

        /// <summary>
        /// 使用 GPU 合成图层列表到 RenderTexture
        /// </summary>
        /// <param name="layers">按顺序排列的图层列表（底部图层在前）</param>
        /// <param name="targetRT">目标 RenderTexture，如果为 null 则自动创建</param>
        /// <returns>包含合成结果的 RenderTexture</returns>
        public static RenderTexture CompositeLayers(System.Collections.Generic.List<Texture2D> layers, RenderTexture targetRT = null)
        {
            if (layers == null || layers.Count == 0)
                return null;

            // 确定目标尺寸（以第一层或默认尺寸为准）
            int width = layers[0]?.width ?? DEFAULT_WIDTH;
            int height = layers[0]?.height ?? DEFAULT_HEIGHT;

            // 确保目标 RT 存在且有效
            if (targetRT == null)
            {
                targetRT = CreateRenderTexture(width, height);
            }
            else if (targetRT.width != width || targetRT.height != height)
            {
                // 如果尺寸不匹配，释放旧的并重新创建
                if (targetRT.IsCreated()) targetRT.Release();
                targetRT.width = width;
                targetRT.height = height;
                targetRT.Create();
            }

            // 保存当前的 RT 状态
            RenderTexture previousRT = RenderTexture.active;

            try
            {
                // 激活目标 RT
                RenderTexture.active = targetRT;

                // 1. 清除背景（全透明）
                GL.Clear(true, true, Color.clear);

                // 2. 设置像素矩阵，确保坐标系正确（左上角为原点，与 GUI 一致）
                GL.PushMatrix();
                GL.LoadPixelMatrix(0, width, height, 0);

                // 3. 逐层绘制
                foreach (var layer in layers)
                {
                    if (layer == null) continue;

                    // Graphics.DrawTexture 支持 Alpha 混合，且自动处理缩放
                    // 绘制铺满整个 RT
                    Graphics.DrawTexture(new Rect(0, 0, width, height), layer);
                }

                // 恢复矩阵
                GL.PopMatrix();
            }
            catch (Exception ex)
            {
                Log.Error($"[PortraitRenderSystem] GPU Composite failed: {ex}");
            }
            finally
            {
                // 恢复之前的 RT
                RenderTexture.active = previousRT;
            }

            return targetRT;
        }

        /// <summary>
        /// 释放 RenderTexture
        /// </summary>
        public static void Release(RenderTexture rt)
        {
            if (rt != null)
            {
                rt.Release();
                UnityEngine.Object.Destroy(rt);
            }
        }
    }
}