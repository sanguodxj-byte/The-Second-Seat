using UnityEngine;
using Verse;

namespace TheSecondSeat.Descent
{
    /// <summary>
    /// ? v1.6.73: 单帧特效动画控制器
    /// 
    /// 功能：通过动态调整亮度、对比度和透明度来创建流畅动画
    /// 
    /// 动画阶段：
    /// 1. 淡入（0-15%）：透明度从 0 渐变到 1
    /// 2. 亮度脉冲（15-50%）：亮度从 1.0 → 1.5 → 1.0，模拟能量聚集
    /// 3. 对比度爆发（50-60%）：对比度从 1.0 → 2.0，模拟冲击波
    /// 4. 稳定期（60-85%）：亮度对比度正常，透明度保持 1.0
    /// 5. 淡出（85-100%）：透明度从 1.0 渐变到 0
    /// </summary>
    public static class EffectAnimationController
    {
        // ==================== 动画参数配置 ====================
        
        private const float FADE_IN_END = 0.15f;        // 淡入结束时间点（15%）
        private const float BRIGHTNESS_PULSE_END = 0.50f; // 亮度脉冲结束（50%）
        private const float CONTRAST_BURST_START = 0.50f; // 对比度爆发开始（50%）
        private const float CONTRAST_BURST_END = 0.60f;   // 对比度爆发结束（60%）
        private const float STABLE_END = 0.85f;          // 稳定期结束（85%）
        
        private const float MAX_BRIGHTNESS = 1.5f;       // 最大亮度倍率
        private const float MAX_CONTRAST = 2.0f;         // 最大对比度倍率
        
        // ==================== 公共方法 ====================
        
        /// <summary>
        /// 计算当前帧的特效参数
        /// </summary>
        /// <param name="progress">动画进度（0.0 - 1.0）</param>
        /// <param name="alpha">输出：透明度</param>
        /// <param name="brightness">输出：亮度倍率</param>
        /// <param name="contrast">输出：对比度倍率</param>
        public static void CalculateEffectParams(float progress, out float alpha, out float brightness, out float contrast)
        {
            progress = Mathf.Clamp01(progress);
            
            // 默认值
            alpha = 1.0f;
            brightness = 1.0f;
            contrast = 1.0f;
            
            // ==================== 阶段 1: 淡入 ====================
            if (progress < FADE_IN_END)
            {
                float t = progress / FADE_IN_END;
                alpha = EaseInOut(t);
                brightness = 1.0f;
                contrast = 1.0f;
            }
            // ==================== 阶段 2: 亮度脉冲 ====================
            else if (progress < BRIGHTNESS_PULSE_END)
            {
                float t = (progress - FADE_IN_END) / (BRIGHTNESS_PULSE_END - FADE_IN_END);
                alpha = 1.0f;
                
                // 亮度从 1.0 → 1.5 → 1.0（正弦波）
                brightness = 1.0f + (MAX_BRIGHTNESS - 1.0f) * Mathf.Sin(t * Mathf.PI);
                contrast = 1.0f;
            }
            // ==================== 阶段 3: 对比度爆发 ====================
            else if (progress < CONTRAST_BURST_END)
            {
                float t = (progress - CONTRAST_BURST_START) / (CONTRAST_BURST_END - CONTRAST_BURST_START);
                alpha = 1.0f;
                brightness = 1.0f;
                
                // 对比度从 1.0 → 2.0 → 1.0（正弦波）
                contrast = 1.0f + (MAX_CONTRAST - 1.0f) * Mathf.Sin(t * Mathf.PI);
            }
            // ==================== 阶段 4: 稳定期 ====================
            else if (progress < STABLE_END)
            {
                alpha = 1.0f;
                brightness = 1.0f;
                contrast = 1.0f;
            }
            // ==================== 阶段 5: 淡出 ====================
            else
            {
                float t = (progress - STABLE_END) / (1.0f - STABLE_END);
                alpha = 1.0f - EaseInOut(t);
                brightness = 1.0f;
                contrast = 1.0f;
            }
        }
        
        /// <summary>
        /// 应用特效参数到 GUI.color（用于 Unity 绘制）
        /// </summary>
        /// <param name="baseAlpha">基础透明度（来自动画系统）</param>
        /// <param name="effectAlpha">特效透明度</param>
        /// <param name="brightness">亮度倍率</param>
        /// <param name="contrast">对比度倍率</param>
        public static void ApplyEffectToGUIColor(float baseAlpha, float effectAlpha, float brightness, float contrast)
        {
            // 计算最终 RGB 值（亮度和对比度）
            float r = ApplyBrightnessContrast(1f, brightness, contrast);
            float g = ApplyBrightnessContrast(1f, brightness, contrast);
            float b = ApplyBrightnessContrast(1f, brightness, contrast);
            
            // 计算最终透明度（基础透明度 × 特效透明度）
            float finalAlpha = baseAlpha * effectAlpha;
            
            // 应用到 GUI.color
            GUI.color = new Color(r, g, b, finalAlpha);
        }
        
        /// <summary>
        /// 绘制带动画参数的特效纹理
        /// </summary>
        /// <param name="rect">绘制区域</param>
        /// <param name="texture">特效纹理</param>
        /// <param name="progress">动画进度（0.0 - 1.0）</param>
        /// <param name="baseAlpha">基础透明度</param>
        public static void DrawAnimatedEffect(Rect rect, Texture2D texture, float progress, float baseAlpha)
        {
            if (texture == null) return;
            
            // 计算特效参数
            CalculateEffectParams(progress, out float alpha, out float brightness, out float contrast);
            
            // 保存原始颜色
            Color originalColor = GUI.color;
            
            // 应用特效参数
            ApplyEffectToGUIColor(baseAlpha, alpha, brightness, contrast);
            
            // 绘制纹理
            Widgets.DrawTextureFitted(rect, texture, 1.0f);
            
            // 恢复原始颜色
            GUI.color = originalColor;
        }
        
        // ==================== 私有辅助方法 ====================
        
        /// <summary>
        /// 平滑过渡函数（EaseInOut）
        /// </summary>
        private static float EaseInOut(float t)
        {
            return t < 0.5f 
                ? 2f * t * t 
                : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;
        }
        
        /// <summary>
        /// 应用亮度和对比度到颜色分量
        /// </summary>
        /// <param name="value">原始颜色值（0-1）</param>
        /// <param name="brightness">亮度倍率</param>
        /// <param name="contrast">对比度倍率</param>
        /// <returns>调整后的颜色值</returns>
        private static float ApplyBrightnessContrast(float value, float brightness, float contrast)
        {
            // 先应用亮度
            value *= brightness;
            
            // 再应用对比度（中心点为 0.5）
            value = (value - 0.5f) * contrast + 0.5f;
            
            // 限制范围
            return Mathf.Clamp01(value);
        }
        
        /// <summary>
        /// 获取动画阶段名称（用于调试）
        /// </summary>
        public static string GetAnimationStageName(float progress)
        {
            if (progress < FADE_IN_END)
                return "淡入";
            else if (progress < BRIGHTNESS_PULSE_END)
                return "亮度脉冲";
            else if (progress < CONTRAST_BURST_END)
                return "对比度爆发";
            else if (progress < STABLE_END)
                return "稳定期";
            else
                return "淡出";
        }
    }
}
