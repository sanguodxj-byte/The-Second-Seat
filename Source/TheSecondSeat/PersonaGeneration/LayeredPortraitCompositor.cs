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
            strin
