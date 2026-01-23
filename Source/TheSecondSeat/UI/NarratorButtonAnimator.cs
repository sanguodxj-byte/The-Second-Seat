using UnityEngine;
using Verse;

namespace TheSecondSeat.UI
{
    /// <summary>
    /// AI 叙事者按钮状态
    /// </summary>
    public enum NarratorButtonState
    {
        Ready,      // 就绪：连接正常，等待事件
        Processing, // 处理中：正在生成内容
        Error,      // 错误：连接失败或 API 错误
        Disabled    // 禁用：模组被关闭
    }

    /// <summary>
    /// AI 叙事者按钮动画系统
    /// </summary>
    [StaticConstructorOnStartup]  // ? 关键：添加此属性以在主线程加载纹理
    public static class NarratorButtonAnimator
    {
        // 动画计时器
        private static float animationTimer = 0f;
        
        // 动画速度配置
        private const float PulseSpeed = 2f;        // 脉冲速度
        private const float FlashSpeed = 4f;        // 闪烁速度
        private const float RotationSpeed = 30f;    // 旋转速度（度/秒）
        
        // 缩放范围
        private const float MinScale = 0.95f;
        private const float MaxScale = 1.05f;
        
        // 透明度范围
        private const float MinAlpha = 0.7f;
        private const float MaxAlpha = 1.0f;
        
        // 颜色配置
        private static readonly Color ReadyColor = new Color(0.8f, 0.8f, 0.8f, 1f);      // 灰白色
        private static readonly Color ProcessingColor = new Color(1f, 0.75f, 0.3f, 1f);  // 琥珀色
        private static readonly Color ErrorColor = new Color(1f, 0.2f, 0.2f, 1f);        // 红色
        private static readonly Color DisabledColor = new Color(0.5f, 0.5f, 0.5f, 0.5f); // 半透明灰色
        
        /// <summary>
        /// 更新动画计时器（每帧调用）
        /// </summary>
        public static void UpdateAnimation()
        {
            animationTimer = Time.realtimeSinceStartup;
        }
        
        /// <summary>
        /// 获取脉冲缩放值
        /// </summary>
        public static float GetPulsingScale(float speed = PulseSpeed)
        {
            float wave = Mathf.Sin(animationTimer * speed);
            return Mathf.Lerp(MinScale, MaxScale, (wave + 1f) / 2f);
        }
        
        /// <summary>
        /// 获取闪烁透明度
        /// </summary>
        public static float GetFlashingAlpha(float speed = FlashSpeed)
        {
            float wave = Mathf.Sin(animationTimer * speed);
            return Mathf.Lerp(MinAlpha, MaxAlpha, (wave + 1f) / 2f);
        }
        
        /// <summary>
        /// 获取旋转角度
        /// </summary>
        public static float GetRotationAngle(float speed = RotationSpeed)
        {
            return (animationTimer * speed) % 360f;
        }
        
        /// <summary>
        /// 根据状态获取颜色
        /// </summary>
        public static Color GetStateColor(NarratorButtonState state)
        {
            return state switch
            {
                NarratorButtonState.Ready => ReadyColor,
                NarratorButtonState.Processing => ProcessingColor,
                NarratorButtonState.Error => ErrorColor,
                NarratorButtonState.Disabled => DisabledColor,
                _ => ReadyColor
            };
        }
        
        /// <summary>
        /// 根据状态获取发光颜色（用于外发光效果）
        /// </summary>
        public static Color GetGlowColor(NarratorButtonState state)
        {
            Color baseColor = GetStateColor(state);
            float alpha = state == NarratorButtonState.Processing ? GetFlashingAlpha() : 0.5f;
            return new Color(baseColor.r, baseColor.g, baseColor.b, alpha);
        }
        
        /// <summary>
        /// 绘制带动画的图标
        /// </summary>
        /// <param name="rect">绘制区域</param>
        /// <param name="icon">图标纹理</param>
        /// <param name="state">当前状态</param>
        public static void DrawAnimatedIcon(Rect rect, Texture2D icon, NarratorButtonState state)
        {
            if (icon == null) return;
            
            // 根据状态应用不同的动画效果
            switch (state)
            {
                case NarratorButtonState.Ready:
                    // 就绪状态：无动画，直接绘制
                    DrawIcon(rect, icon, GetStateColor(state), 1f, 0f);
                    break;
                
                case NarratorButtonState.Processing:
                    // 处理中：琥珀色脉冲 + 缓慢旋转
                    float scale = GetPulsingScale(PulseSpeed);
                    float rotation = GetRotationAngle(RotationSpeed * 0.5f);
                    DrawIcon(rect, icon, GetStateColor(state), scale, rotation);
                    
                    // 绘制外发光
                    DrawGlow(rect, GetGlowColor(state));
                    break;
                
                case NarratorButtonState.Error:
                    // 错误状态：红色快速闪烁
                    float alpha = GetFlashingAlpha(FlashSpeed);
                    Color errorColor = new Color(ErrorColor.r, ErrorColor.g, ErrorColor.b, alpha);
                    DrawIcon(rect, icon, errorColor, 1f, 0f);
                    
                    // 绘制外发光
                    DrawGlow(rect, new Color(1f, 0f, 0f, alpha * 0.8f));
                    break;
                
                case NarratorButtonState.Disabled:
                    // 禁用状态：半透明灰色，无动画
                    DrawIcon(rect, icon, DisabledColor, 1f, 0f);
                    break;
            }
        }
        
        /// <summary>
        /// 绘制图标（支持缩放和旋转）
        /// ? 修改：图标内旋转，而不是整个按钮绕中心旋转
        /// </summary>
        private static void DrawIcon(Rect rect, Texture2D icon, Color color, float scale, float rotation)
        {
            // 保存当前矩阵
            Matrix4x4 matrix = GUI.matrix;
            
            // ? 关键修改：使用图标自己的中心作为旋转点
            Vector2 iconCenter = new Vector2(rect.x + rect.width / 2f, rect.y + rect.height / 2f);
            
            // 应用缩放
            Rect scaledRect = rect;
            if (scale != 1f)
            {
                float scaledWidth = rect.width * scale;
                float scaledHeight = rect.height * scale;
                scaledRect = new Rect(
                    iconCenter.x - scaledWidth / 2f,
                    iconCenter.y - scaledHeight / 2f,
                    scaledWidth,
                    scaledHeight
                );
            }
            
            // ? 应用旋转：绕图标自己的中心旋转（不是外部点）
            if (rotation != 0f)
            {
                // 使用 GUIUtility.RotateAroundPivot，pivot 点设置为图标中心
                GUIUtility.RotateAroundPivot(rotation, iconCenter);
            }
            
            // 绘制图标
            GUI.color = color;
            GUI.DrawTexture(scaledRect, icon);
            GUI.color = Color.white;
            
            // 恢复矩阵
            GUI.matrix = matrix;
        }
        
        /// <summary>
        /// 绘制外发光效果
        /// </summary>
        private static void DrawGlow(Rect rect, Color glowColor)
        {
            // 绘制多层渐变的方形发光
            for (int i = 1; i <= 3; i++)
            {
                float expansion = i * 4f;
                Rect glowRect = rect.ExpandedBy(expansion);
                float alpha = glowColor.a * (1f - i * 0.25f);
                
                GUI.color = new Color(glowColor.r, glowColor.g, glowColor.b, alpha);
                Widgets.DrawBoxSolid(glowRect, GUI.color);
            }
            
            GUI.color = Color.white;
        }
        
        /// <summary>
        /// 绘制指示灯（小圆点）
        /// </summary>
        /// <param name="rect">指示灯位置</param>
        /// <param name="state">当前状态</param>
        public static void DrawIndicatorLight(Rect rect, NarratorButtonState state)
        {
            Color lightColor = state switch
            {
                NarratorButtonState.Ready => new Color(0f, 1f, 0.5f, 1f),      // 绿色
                NarratorButtonState.Processing => new Color(1f, 0.75f, 0f, GetFlashingAlpha()), // 琥珀色闪烁
                NarratorButtonState.Error => new Color(1f, 0f, 0f, 1f),        // 红色
                NarratorButtonState.Disabled => new Color(0.5f, 0.5f, 0.5f, 0.5f), // 灰色
                _ => Color.white
            };
            
            // 创建圆形纹理（如果还没创建）
            Texture2D circleTexture = CreateCircleTexture(16);
            
            // 绘制外层发光（更大的半透明圆）
            GUI.color = new Color(lightColor.r, lightColor.g, lightColor.b, lightColor.a * 0.3f);
            Rect glowRect = rect.ExpandedBy(3f);
            GUI.DrawTexture(glowRect, circleTexture);
            
            // 绘制核心圆形
            GUI.color = lightColor;
            GUI.DrawTexture(rect, circleTexture);
            
            GUI.color = Color.white;
        }
        
        // 缓存圆形纹理
        private static Texture2D? cachedCircleTexture;
        
        /// <summary>
        /// 创建圆形纹理
        /// </summary>
        private static Texture2D CreateCircleTexture(int size)
        {
            if (cachedCircleTexture != null)
                return cachedCircleTexture;
            
            cachedCircleTexture = new Texture2D(size, size, TextureFormat.ARGB32, false);
            float center = size / 2f;
            float radius = center;
            
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Mathf.Sqrt((x - center) * (x - center) + (y - center) * (y - center));
                    float alpha = distance < radius ? 1f : 0f;
                    
                    // 边缘抗锯齿
                    if (distance >= radius - 1f && distance < radius)
                    {
                        alpha = radius - distance;
                    }
                    
                    cachedCircleTexture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
            
            cachedCircleTexture.Apply();
            return cachedCircleTexture;
        }
    }
}
