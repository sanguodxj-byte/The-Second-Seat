using System;
using UnityEngine;
using Verse;
using RimWorld;
using TheSecondSeat.PersonaGeneration;

namespace TheSecondSeat.Descent
{
    /// <summary>
    /// ⭐ v1.6.80: 实体阴影渲染器
    ///
    /// 功能：
    /// - 在地图上渲染移动的实体投影
    /// - 使用透明黑色投影纹理
    /// - 从地图底部快速飞向顶部
    /// </summary>
    [StaticConstructorOnStartup]
    public class DragonShadowRenderer : MapComponent
    {
        // ==================== 静态资源 ====================
        
        /// <summary>实体阴影纹理（透明背景纯黑）</summary>
        private static Texture2D dragonShadowTexture;
        
        /// <summary>阴影材质（使用透明混合）</summary>
        private static Material shadowMaterial;
        
        // ==================== 动画状态 ====================
        
        private bool isAnimating = false;
        private float animationProgress = 0f;      // 0-1 动画进度
        private float animationDuration = 1.5f;    // 动画持续时间（秒）
        private IntVec3 targetLocation;            // 降临目标位置
        
        // ==================== 渲染参数 ====================
        
        private const float SHADOW_SIZE = 40f;     // 阴影大小（地图格子）
        private const float SHADOW_ALPHA = 0.6f;   // 阴影透明度
        private const float SPEED_MULTIPLIER = 1.5f; // 飞行速度倍率
        
        // ==================== 纹理路径 ====================
        
        // ⭐ v1.6.81: 不再硬编码默认路径，纹理由子Mod提供
        // 子Mod需要在 NarratorPersonaDef.dragonShadowTexturePath 中配置路径
        private const string DEFAULT_SHADOW_PATH = "";
        
        // ==================== 构造函数 ====================
        
        static DragonShadowRenderer()
        {
            LongEventHandler.ExecuteWhenFinished(() =>
            {
                LoadResources();
            });
        }
        
        public DragonShadowRenderer(Map map) : base(map)
        {
        }
        
        // ==================== 资源加载 ====================
        
        private static void LoadResources()
        {
            try
            {
                // ⭐ v1.6.81: 默认不加载任何纹理
                // 纹理由子Mod通过 SetCustomTexture() 或 NarratorPersonaDef.dragonShadowTexturePath 提供
                dragonShadowTexture = null;
                
                // 创建阴影材质（使用透明混合）
                shadowMaterial = new Material(ShaderDatabase.MetaOverlay);
                shadowMaterial.color = new Color(0f, 0f, 0f, SHADOW_ALPHA);
                
                if (Prefs.DevMode)
                {
                    Log.Message("[DragonShadowRenderer] 初始化完成，等待子Mod提供纹理");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[DragonShadowRenderer] 初始化失败: {ex}");
            }
        }
        
        /// <summary>
        /// 创建占位符纹理（简单的椭圆形）
        /// </summary>
        private static Texture2D CreatePlaceholderTexture()
        {
            int size = 128;
            Texture2D tex = new Texture2D(size, size, TextureFormat.ARGB32, false);
            
            Color transparent = new Color(0, 0, 0, 0);
            Color black = new Color(0, 0, 0, 1);
            
            // 填充透明
            for (int x = 0; x < size; x++)
            {
                for (int y = 0; y < size; y++)
                {
                    tex.SetPixel(x, y, transparent);
                }
            }
            
            // 绘制椭圆形阴影（模拟实体轮廓）
            int centerX = size / 2;
            int centerY = size / 2;
            int radiusX = size / 3;
            int radiusY = size / 5;
            
            for (int x = 0; x < size; x++)
            {
                for (int y = 0; y < size; y++)
                {
                    float dx = (float)(x - centerX) / radiusX;
                    float dy = (float)(y - centerY) / radiusY;
                    
                    if (dx * dx + dy * dy <= 1)
                    {
                        tex.SetPixel(x, y, black);
                    }
                }
            }
            
            tex.Apply();
            return tex;
        }
        
        // ==================== 公共API ====================
        
        /// <summary>
        /// 开始播放阴影飞掠动画
        /// </summary>
        public void StartAnimation(IntVec3 target, float duration = 1.5f)
        {
            if (isAnimating)
            {
                Log.Warning("[DragonShadowRenderer] 动画正在播放中");
                return;
            }
            
            targetLocation = target;
            animationDuration = duration;
            animationProgress = 0f;
            isAnimating = true;
            
            Log.Message($"[DragonShadowRenderer] 开始实体阴影飞掠动画，目标: {target}，持续: {duration}秒");
        }
        
        /// <summary>
        /// 停止动画
        /// </summary>
        public void StopAnimation()
        {
            isAnimating = false;
            animationProgress = 0f;
        }
        
        /// <summary>
        /// 检查是否正在播放
        /// </summary>
        public bool IsAnimating => isAnimating;
        
        // ==================== 地图组件更新 ====================
        
        public override void MapComponentTick()
        {
            base.MapComponentTick();
            
            if (!isAnimating)
            {
                return;
            }
            
            // 更新动画进度
            float deltaTime = 1f / 60f; // 假设60 tick/秒
            animationProgress += deltaTime / animationDuration * SPEED_MULTIPLIER;

            // ⭐ v1.7.8: 移除地面尘土效果，避免 Mote 渲染冲突
            // 注释掉可能导致 Mote_SmokeJoint 错误的代码
            // if (animationProgress > 0.1f && animationProgress < 0.9f)
            // {
            //     ...
            // }
            
            if (animationProgress >= 1f)
            {
                isAnimating = false;
                animationProgress = 0f;
                Log.Message("[DragonShadowRenderer] 实体阴影飞掠动画完成");
            }
        }
        
        // ==================== 渲染 ====================
        
        public override void MapComponentOnGUI()
        {
            base.MapComponentOnGUI();
            
            if (!isAnimating)
            {
                return;
            }
            
            try
            {
                DrawEntityShadow();
            }
            catch (Exception ex)
            {
                Log.Error($"[DragonShadowRenderer] 渲染失败: {ex}");
            }
        }
        
        /// <summary>
        /// 绘制实体阴影
        /// </summary>
        private void DrawEntityShadow()
        {
            // ⭐ v1.6.81: 如果没有纹理，使用备用的粒子效果
            if (dragonShadowTexture == null)
            {
                DrawFallbackEffect();
                return;
            }
            
            // 计算阴影在地图上的当前位置
            // 从地图底部（南边）飞向顶部（北边），经过目标点
            
            float mapHeight = map.Size.z;
            float mapWidth = map.Size.x;
            
            // 起点：地图底部外侧
            float startZ = -SHADOW_SIZE;
            // 终点：地图顶部外侧
            float endZ = mapHeight + SHADOW_SIZE;
            
            // 当前Z位置（使用缓动函数使动画更自然）
            float easedProgress = EaseInOutQuad(animationProgress);
            float currentZ = Mathf.Lerp(startZ, endZ, easedProgress);
            
            // X位置：沿着目标点的X轴飞行，略有偏移
            float currentX = targetLocation.x + Mathf.Sin(animationProgress * Mathf.PI) * 5f;
            
            // 世界坐标转屏幕坐标
            Vector3 worldPos = new Vector3(currentX, 0, currentZ);
            Vector2 screenPos = WorldToScreenPoint(worldPos);
            
            // 计算阴影在屏幕上的大小
            float screenSize = SHADOW_SIZE * GetZoomFactor();
            
            // 绘制阴影纹理
            Rect shadowRect = new Rect(
                screenPos.x - screenSize / 2,
                screenPos.y - screenSize / 4, // 投影通常是扁长的
                screenSize,
                screenSize / 2
            );
            
            // 保存原始GUI颜色
            Color originalColor = GUI.color;
            
            // 设置阴影颜色（半透明黑色）
            // 透明度随位置变化：中间最不透明，边缘渐隐
            float distanceToTarget = Mathf.Abs(currentZ - targetLocation.z);
            float proximityFactor = 1f - Mathf.Clamp01(distanceToTarget / (mapHeight / 2));
            float alpha = SHADOW_ALPHA * (0.3f + 0.7f * proximityFactor);
            
            GUI.color = new Color(0f, 0f, 0f, alpha);
            
            // 绘制旋转的阴影（朝向飞行方向）
            Matrix4x4 originalMatrix = GUI.matrix;
            
            // 根据进度旋转阴影（模拟飞行姿态）
            float rotation = Mathf.Sin(animationProgress * Mathf.PI * 2) * 5f; // 轻微摇摆
            GUIUtility.RotateAroundPivot(rotation, new Vector2(shadowRect.center.x, shadowRect.center.y));
            
            GUI.DrawTexture(shadowRect, dragonShadowTexture, ScaleMode.ScaleToFit);
            
            // 恢复原始设置
            GUI.matrix = originalMatrix;
            GUI.color = originalColor;
            
            // 渲染结束
        }
        
        /// <summary>
        /// ⭐ v1.6.81: 备用效果（无纹理时使用粒子）
        /// </summary>
        private void DrawFallbackEffect()
        {
            // ⭐ v1.6.91: 移除备用烟雾效果，避免产生"空投仓烟雾"的视觉误导
            // 如果没有纹理，什么都不显示
        }
        
        /// <summary>
        /// 世界坐标转屏幕坐标
        /// </summary>
        private Vector2 WorldToScreenPoint(Vector3 worldPos)
        {
            Vector3 screenPos = Find.Camera.WorldToScreenPoint(worldPos);
            // Unity屏幕坐标Y轴是反的
            return new Vector2(screenPos.x, Screen.height - screenPos.y);
        }
        
        /// <summary>
        /// 获取当前缩放因子
        /// </summary>
        private float GetZoomFactor()
        {
            // 根据相机高度计算缩放
            float cameraHeight = Find.Camera.transform.position.y;
            return Mathf.Clamp(50f / cameraHeight, 0.5f, 3f);
        }
        
        /// <summary>
        /// 缓动函数：加速-减速
        /// </summary>
        private float EaseInOutQuad(float t)
        {
            return t < 0.5f 
                ? 2f * t * t 
                : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;
        }
        
        // ==================== 静态辅助方法 ====================
        
        /// <summary>
        /// 获取指定地图的阴影渲染器
        /// </summary>
        public static DragonShadowRenderer GetRenderer(Map map)
        {
            return map?.GetComponent<DragonShadowRenderer>();
        }
        
        /// <summary>
        /// 在指定地图上播放阴影动画
        /// </summary>
        public static void PlayAnimation(Map map, IntVec3 target, float duration = 1.5f)
        {
            var renderer = GetRenderer(map);
            if (renderer != null)
            {
                renderer.StartAnimation(target, duration);
            }
            else
            {
                Log.Warning("[DragonShadowRenderer] 地图上没有阴影渲染器组件");
            }
        }
        
        /// <summary>
        /// ⭐ v1.6.81: 设置自定义阴影纹理（由子Mod调用）
        /// </summary>
        /// <param name="texture">阴影纹理（透明背景纯黑图案）</param>
        public static void SetCustomTexture(Texture2D texture)
        {
            if (texture != null)
            {
                dragonShadowTexture = texture;
                if (shadowMaterial != null)
                {
                    shadowMaterial.mainTexture = texture;
                }
                Log.Message($"[DragonShadowRenderer] 已设置自定义阴影纹理: {texture.name}");
            }
            else
            {
                Log.Warning("[DragonShadowRenderer] 尝试设置空纹理");
            }
        }
        
        /// <summary>
        /// ⭐ v1.6.81: 从路径加载并设置自定义阴影纹理（由子Mod调用）
        /// </summary>
        /// <param name="texturePath">纹理路径（相对于子Mod的Textures文件夹）</param>
        /// <returns>是否加载成功</returns>
        public static bool LoadCustomTexture(string texturePath)
        {
            if (string.IsNullOrEmpty(texturePath))
            {
                Log.Warning("[DragonShadowRenderer] 纹理路径为空");
                return false;
            }
            
            try
            {
                // 🔍 调试日志：明确打印尝试加载的路径
                Log.Message($"[DragonShadowRenderer] 尝试加载纹理: '{texturePath}'");
                
                Texture2D texture = ContentFinder<Texture2D>.Get(texturePath, false);
                if (texture != null)
                {
                    SetCustomTexture(texture);
                    return true;
                }
                else
                {
                    Log.Error($"[DragonShadowRenderer] ❌ 未找到纹理: '{texturePath}'。请检查文件是否存在于 Textures/ 目录下，且文件名大小写匹配。");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[DragonShadowRenderer] 加载纹理失败: {ex}");
                return false;
            }
        }
        
        /// <summary>
        /// ⭐ v1.6.81: 检查是否有自定义纹理
        /// </summary>
        public static bool HasCustomTexture => dragonShadowTexture != null;
        
        /// <summary>
        /// ⭐ v1.6.90: 从 NarratorPersonaDef 自动加载阴影纹理
        /// 使用自动路径生成，子Mod无需配置完整路径
        /// </summary>
        /// <param name="persona">叙事者人格定义</param>
        /// <returns>是否加载成功</returns>
        public static bool LoadFromPersona(NarratorPersonaDef persona)
        {
            if (persona == null)
            {
                Log.Warning("[DragonShadowRenderer] Persona为空");
                return false;
            }
            
            // ⭐ 使用自动路径生成API
            string shadowPath = persona.GetDragonShadowFullPath();
            
            if (string.IsNullOrEmpty(shadowPath))
            {
                Log.Message("[DragonShadowRenderer] 未配置阴影纹理路径");
                return false;
            }
            
            return LoadCustomTexture(shadowPath);
        }
    }
}