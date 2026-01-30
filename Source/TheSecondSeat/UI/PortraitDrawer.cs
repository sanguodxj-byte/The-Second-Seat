using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using TheSecondSeat.PersonaGeneration;
using TheSecondSeat.Utils;
using TheSecondSeat.Core;

namespace TheSecondSeat.UI
{
    /// <summary>
    /// 负责全身立绘的绘制逻辑。
    /// 从 FullBodyPortraitPanel 中分离出来的独立系统。
    /// </summary>
    public class PortraitDrawer
    {
        private readonly FullBodyPortraitPanel panel;
        
        // 缓存
        private Texture2D cachedBodyBase = null;
        private string cachedPersonaDefName = null;

        public PortraitDrawer(FullBodyPortraitPanel panel)
        {
            this.panel = panel;
        }

        /// <summary>
        /// 主绘制入口
        /// </summary>
        public void Draw()
        {
            if (panel.CurrentPersona == null) return;
            
            if (!HasAvailablePortrait())
            {
                DrawNoPortraitPlaceholder();
                return;
            }

            UpdateBodyBaseIfNeeded();
            
            string personaResName = panel.GetPersonaResourceName();

            bool mouseOver = panel.DrawRect.Contains(Event.current.mousePosition);
            bool shiftHeld = Event.current.shift;
            
            // 计算 Alpha
            float alpha = 1.0f;
            if (panel.AnimationHandler.IsPlayingAnimation)
            {
                alpha = panel.AnimationHandler.CalculateAnimationAlpha();
            }
            else if (mouseOver && !shiftHeld)
            {
                alpha = 0.2f; // 幽灵模式
            }

            // ⭐ v1.13.5: 呼吸动画 + 身体微动
            float breathingOffset = 0f;
            float swayOffset = 0f;
            
            if (!panel.AnimationHandler.IsPlayingAnimation)
            {
                // 获取基础呼吸/摇摆 (用于整体位置)
                breathingOffset = ExpressionSystem.GetBreathingOffset(personaResName) * 0.5f;
                swayOffset = ExpressionSystem.GetSwayOffset(personaResName) * 0.5f;
            }

            Rect animatedRect = new Rect(panel.DrawRect.x + swayOffset, panel.DrawRect.y + breathingOffset, panel.DrawRect.width, panel.DrawRect.height);
            
            // 绘制 (使用 GPU 加速渲染 + 多层视差)
            // 如果处于幽灵模式（alpha < 1），使用整体透明度合成，避免部件高亮
            if (alpha < 0.99f)
            {
                DrawLayeredPortraitGPU_Transparent(animatedRect, panel.CurrentPersona, alpha);
            }
            else
            {
                DrawLayeredPortraitGPU(animatedRect, panel.CurrentPersona, 1.0f);
            }
            
            if (panel.AnimationHandler.IsPlayingAnimation)
            {
                DrawEffectLayer(animatedRect);
            }

            // ⭐ v1.9.6: 绘制生物节律指示器
            DrawBioRhythmIndicator(animatedRect);
        }

        /// <summary>
        /// ⭐ v1.9.6: 绘制生物节律心情指示器
        /// </summary>
        private void DrawBioRhythmIndicator(Rect portraitRect)
        {
            var bioSystem = Current.Game?.GetComponent<NarratorBioRhythm>();
            if (bioSystem == null) return;

            var settings = LoadedModManager.GetMod<Settings.TheSecondSeatMod>()?.GetSettings<Settings.TheSecondSeatSettings>();
            if (settings != null && !settings.enableBioRhythm) return;

            // 直接获取公开属性
            float mood = bioSystem.CurrentMood;

            // 颜色映射
            Color moodColor;
            if (mood > 80f) { moodColor = new Color(1f, 0.8f, 0.2f); } // 金色
            else if (mood > 60f) { moodColor = new Color(0.4f, 0.8f, 0.4f); } // 绿色
            else if (mood > 40f) { moodColor = new Color(0.4f, 0.6f, 0.8f); } // 蓝色
            else if (mood > 20f) { moodColor = new Color(0.7f, 0.7f, 0.7f); } // 灰色
            else { moodColor = new Color(0.8f, 0.3f, 0.3f); } // 红色

            // 绘制指示器 (右上角小圆点)
            float indicatorSize = 12f;
            // 注意：portraitRect 可能会有偏移（呼吸动画），我们希望指示器相对稳定，或者跟随。
            // 这里跟随 portraitRect，所以会一起呼吸移动。
            Rect indicatorRect = new Rect(portraitRect.xMax - indicatorSize - 8f, portraitRect.y + 8f, indicatorSize, indicatorSize);
            
            // 外圈光晕
            GUI.color = new Color(moodColor.r, moodColor.g, moodColor.b, 0.3f);
            Widgets.DrawBoxSolid(indicatorRect.ExpandedBy(2f), GUI.color);
            
            // 核心
            GUI.color = moodColor;
            Widgets.DrawBoxSolid(indicatorRect, moodColor);
            GUI.color = Color.white;

            // Tooltip
            if (Mouse.IsOver(indicatorRect))
            {
                TooltipHandler.TipRegion(indicatorRect, bioSystem.GetCurrentBioContext());
            }
        }
        
        private void DrawLayeredPortraitGPU(Rect rect, NarratorPersonaDef persona, float alpha)
        {
            var state = ExpressionSystem.GetExpressionState(persona.defName);
            var renderTree = RenderTreeDefManager.GetRenderTree(persona.defName);
            var animConfig = renderTree?.animation ?? new AnimationConfig();
            
            // 保存原始 GUI color
            Color originalColor = GUI.color;

            // ⭐ v1.13.3: 透明度混合优化 (Cross-fade)
            // 如果处于表情过渡期间，同时绘制旧表情和新表情
            if (state != null && state.TransitionProgress < 1f)
            {
                // 1. 绘制旧表情 (不透明底图)
                GUI.color = new Color(originalColor.r, originalColor.g, originalColor.b, originalColor.a * alpha);
                var oldLayers = GetLayerInfosForExpression(persona, state.PreviousExpression, 0, animConfig);
                if (oldLayers != null && oldLayers.Count > 0)
                {
                    GPULayeredRenderer.DrawLayersWithOffsetsOnGUI(rect, oldLayers);
                }
                
                // 2. 绘制新表情 (淡入覆盖)
                float t = state.TransitionProgress;
                float smoothT = t * t * (3f - 2f * t); // EaseInOut
                GUI.color = new Color(originalColor.r, originalColor.g, originalColor.b, originalColor.a * alpha * smoothT);
                
                int variant = state.Intensity > 0 ? state.Intensity : state.CurrentVariant;
                var newLayers = GetLayerInfosForExpression(persona, state.CurrentExpression, variant, animConfig);
                if (newLayers != null && newLayers.Count > 0)
                {
                    GPULayeredRenderer.DrawLayersWithOffsetsOnGUI(rect, newLayers);
                }
            }
            else
            {
                // 无过渡，直接绘制当前表情
                GUI.color = new Color(originalColor.r, originalColor.g, originalColor.b, originalColor.a * alpha);
                
                int variant = (state != null && state.Intensity > 0) ? state.Intensity : (state?.CurrentVariant ?? 0);
                var layers = GetLayerInfosForExpression(persona, state?.CurrentExpression ?? ExpressionType.Neutral, variant, animConfig);
                
                if (layers != null && layers.Count > 0)
                {
                    GPULayeredRenderer.DrawLayersWithOffsetsOnGUI(rect, layers);
                }
            }
            
            // 恢复 GUI color
            GUI.color = originalColor;
        }

        /// <summary>
        /// ⭐ v3.1.1: 修复幽灵模式下的透明度混合异常
        /// 使用 GPU 整体合成后再绘制，避免部件高亮
        /// </summary>
        private void DrawLayeredPortraitGPU_Transparent(Rect rect, NarratorPersonaDef persona, float alpha)
        {
            var state = ExpressionSystem.GetExpressionState(persona.defName);
            var renderTree = RenderTreeDefManager.GetRenderTree(persona.defName);
            var animConfig = renderTree?.animation ?? new AnimationConfig();
            
            int variant = (state != null && state.Intensity > 0) ? state.Intensity : (state?.CurrentVariant ?? 0);
            var layers = GetLayerInfosForExpression(persona, state?.CurrentExpression ?? ExpressionType.Neutral, variant, animConfig);
            
            if (layers != null && layers.Count > 0)
            {
                // 使用新的整体合成绘制方法
                GPULayeredRenderer.DrawDynamicPortraitWithOffsets(rect, layers, alpha);
            }
        }
        
        /// <summary>
        /// ⭐ v1.14.5: 获取带偏移和缩放信息的图层列表
        /// 支持视差、呼吸缩放、平滑眨眼
        /// </summary>
        private List<LayerDrawInfo> GetLayerInfosForExpression(NarratorPersonaDef persona, ExpressionType expression, int variant, AnimationConfig anim)
        {
            string personaName = panel.GetPersonaResourceName();
            List<LayerDrawInfo> layers = new List<LayerDrawInfo>();
            
            // 基础缩放和偏移计算
            float breathScale = ExpressionSystem.GetBreathingScale(personaName, anim.breathScaleIntensity, 0f);
            
            // ⭐ 修复：强制五官与身体呼吸同步，避免因底图包含头部而导致的错位
            // float headBreathScale = ExpressionSystem.GetBreathingScale(personaName, anim.breathScaleIntensity, anim.headBreathLag);
            float headBreathScale = breathScale; 
            
            // Layer 1: 身体层
            Texture2D bodyTex = null;
            if (panel.AnimationHandler.IsPlayingAnimation && !string.IsNullOrEmpty(panel.AnimationHandler.OverridePosture))
            {
                bodyTex = TSS_AssetLoader.LoadDescentPosture(personaName, panel.AnimationHandler.OverridePosture, null);
            }
            
            if (bodyTex == null)
            {
                if (cachedBodyBase == null) UpdateBodyBaseIfNeeded();
                bodyTex = cachedBodyBase;
            }
            
            if (bodyTex != null)
            {
                // 缩放中心在底部：OffsetY 需要调整
                float scaleY = breathScale;
                float offsetY = (1f - scaleY); // 底部对齐
                float offsetX = (1f - breathScale) * 0.5f; // 水平居中
                
                layers.Add(new LayerDrawInfo(bodyTex, offsetX, offsetY, breathScale, scaleY));
            }
            
            // Layer 2: 嘴巴层
            string mouthLayerName = MouthAnimationSystem.GetMouthLayerName(persona.defName);
            bool isTTSSpeaking = !string.IsNullOrEmpty(mouthLayerName);
            Texture2D mouthTex = null;
            
            if (isTTSSpeaking && mouthLayerName != "USE_BASE")
            {
                mouthTex = PortraitLoader.GetLayerTexture(persona, mouthLayerName, suppressWarning: true);
            }
            
            if (mouthTex == null && !(isTTSSpeaking && mouthLayerName == "USE_BASE"))
            {
                string staticMouth = MouthAnimationSystem.GetStaticMouthLayerName(persona.defName, expression, variant);
                mouthTex = PortraitLoader.GetLayerTexture(persona, staticMouth, suppressWarning: true);
                
                // Fallbacks...
                if (mouthTex == null && staticMouth.Any(char.IsDigit))
                {
                    string baseMouth = System.Text.RegularExpressions.Regex.Replace(staticMouth, @"\d+", "");
                    if (baseMouth != staticMouth) mouthTex = PortraitLoader.GetLayerTexture(persona, baseMouth, suppressWarning: true);
                }
                if (mouthTex == null)
                {
                    mouthTex = PortraitLoader.GetLayerTexture(persona, "Closed_mouth", suppressWarning: true)
                        ?? PortraitLoader.GetLayerTexture(persona, "Neutral_mouth", suppressWarning: true);
                }
            }
            
            if (mouthTex != null)
            {
                // 嘴巴层使用 headBreathScale
                float scaleY = headBreathScale;
                float offsetY = (1f - scaleY); // 底部对齐
                float offsetX = (1f - headBreathScale) * 0.5f;
                
                layers.Add(new LayerDrawInfo(mouthTex, offsetX, offsetY, headBreathScale, scaleY));
            }

            // Layer 3: 眼睛层 (支持平滑眨眼)
            Texture2D eyeTex = null;
            
            // 获取眨眼进度
            // ⭐ 修复：对于包含完整面部的大图层，移除眨眼缩放逻辑，仅依赖纹理替换
            // 否则整个头部会被压扁，导致眼睛错位
            bool useClosedTexture = false;
            
            float blinkProgress = BlinkAnimationSystem.GetBlinkProgress(persona.defName);
            if (blinkProgress > 0.01f && blinkProgress < 0.99f)
            {
                // 处于眨眼过程中：如果闭眼程度超过一半，显示闭眼图
                // 如果没有中间态图片，只能这样硬切
                if (blinkProgress > 0.3f && blinkProgress < 0.7f)
                {
                    useClosedTexture = true;
                }
            }
            else if (BlinkAnimationSystem.GetEyeLayerName(persona.defName) == "closed_eyes")
            {
                useClosedTexture = true;
            }
            
            if (useClosedTexture)
            {
                eyeTex = PortraitLoader.GetLayerTexture(persona, "closed_eyes", suppressWarning: true);
            }
            else
            {
                // 获取睁眼图 (表情相关)
                string exprEyeName = BlinkAnimationSystem.GetEyesLayerNameForExpression(persona.defName, expression, variant);
                if (!string.IsNullOrEmpty(exprEyeName))
                {
                    eyeTex = PortraitLoader.GetLayerTexture(persona, exprEyeName, suppressWarning: true);
                    if (eyeTex == null && exprEyeName.Any(char.IsDigit))
                    {
                        string baseEye = System.Text.RegularExpressions.Regex.Replace(exprEyeName, @"\d+", "");
                        eyeTex = PortraitLoader.GetLayerTexture(persona, baseEye, suppressWarning: true);
                    }
                }
                
                if (eyeTex == null)
                {
                    eyeTex = PortraitLoader.GetLayerTexture(persona, "base_eyes", suppressWarning: true)
                          ?? PortraitLoader.GetLayerTexture(persona, "neutral_eyes", suppressWarning: true);
                }
            }
            
            if (eyeTex != null)
            {
                // 眼睛层使用 headBreathScale
                float totalScaleY = headBreathScale; // 不再乘 blinkScaleY
                
                // 统一底部对齐
                float offsetY = (1f - totalScaleY); 
                float offsetX = (1f - headBreathScale) * 0.5f;
                
                layers.Add(new LayerDrawInfo(eyeTex, offsetX, offsetY, headBreathScale, totalScaleY));
            }
            
            // Layer 4: 特效层 (腮红等)
            if (expression == ExpressionType.Shy || expression == ExpressionType.Angry)
            {
                string baseName = expression == ExpressionType.Shy ? "flush_shy" : "flush_angry";
                string exprName = expression.ToString().ToLower();
                string flushLayerName = variant > 0 ? $"flush_{exprName}{variant}" : baseName;
                
                var flushTexture = PortraitLoader.GetLayerTexture(persona, flushLayerName, suppressWarning: true);
                if (flushTexture == null && variant > 0) flushTexture = PortraitLoader.GetLayerTexture(persona, baseName, suppressWarning: true);
                
                if (flushTexture != null)
                {
                    // 特效层通常在脸上，跟随 head
                    float scaleY = headBreathScale;
                    float offsetY = (1f - scaleY); // 底部对齐
                    float offsetX = (1f - headBreathScale) * 0.5f;
                    
                    layers.Add(new LayerDrawInfo(flushTexture, offsetX, offsetY, headBreathScale, scaleY));
                }
            }
            
            return layers;
        }
        
        private void DrawEffectLayer(Rect rect)
        {
            string effectName = panel.AnimationHandler.ActiveEffect;
            if (string.IsNullOrEmpty(effectName) || panel.CurrentPersona == null) return;
            
            string personaName = panel.GetPersonaResourceName();
            Texture2D effectTexture = TSS_AssetLoader.LoadDescentEffect(personaName, effectName, null);
            if (effectTexture == null) return;

            float effectAlpha = 0.5f + 0.5f * Mathf.Sin(Time.time * 3f);
            
            Color originalColor = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, effectAlpha * GUI.color.a);
            Widgets.DrawTextureFitted(rect, effectTexture, 1.0f);
            GUI.color = originalColor;
        }

        private void UpdateBodyBaseIfNeeded()
        {
            if (panel.CurrentPersona == null)
            {
                cachedBodyBase = null;
                cachedPersonaDefName = null;
                return;
            }
            
            if (cachedPersonaDefName == panel.CurrentPersona.defName && cachedBodyBase != null)
            {
                return;
            }
            
            cachedBodyBase = PortraitLoader.GetLayerTexture(panel.CurrentPersona, "base_body")
                ?? PortraitLoader.GetLayerTexture(panel.CurrentPersona, "body")
                ?? PortraitLoader.GetLayerTexture(panel.CurrentPersona, "base");
            
            cachedPersonaDefName = panel.CurrentPersona.defName;
        }

        private bool HasAvailablePortrait()
        {
            if (panel.CurrentPersona == null) return false;
            
            if (!string.IsNullOrEmpty(panel.CurrentPersona.portraitPath))
            {
                if (TSS_AssetLoader.TextureExists(panel.CurrentPersona.portraitPath))
                {
                    return true;
                }
            }

            return TSS_AssetLoader.HasPortrait(panel.GetPersonaResourceName());
        }
        
        private void DrawNoPortraitPlaceholder()
        {
            Widgets.DrawBoxSolid(panel.DrawRect, new Color(0.15f, 0.15f, 0.2f, 0.6f));
            Widgets.DrawBox(panel.DrawRect, 2);
            
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleCenter;
            
            string personaName = panel.CurrentPersona?.narratorName ?? "Unknown";
            string message = $"{personaName}\n\n<color=#888888>{"TSS_Portrait_Loading".Translate()}</color>";
            
            Widgets.Label(panel.DrawRect.ContractedBy(20f), message);
            
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;
        }

        /// <summary>
        /// 获取腮红图层名称，支持变体
        /// </summary>
        private string GetFlushLayerName(string defName, ExpressionState state)
        {
            if (state == null) return "flush_shy";
            
            string exprName = state.CurrentExpression.ToString().ToLower();
            string baseName = state.CurrentExpression == ExpressionType.Shy ? "flush_shy" : "flush_angry";
            
            // 获取变体编号
            int variant = state.Intensity > 0 ? state.Intensity : state.CurrentVariant;
            
            if (variant <= 0)
            {
                return baseName;
            }
            
            // 返回变体名：flush_shy1, flush_shy2, flush_angry1, flush_angry2 等
            return $"flush_{exprName}{variant}";
        }
        
        private void DrawMinimalPlaceholder(Rect rect, NarratorPersonaDef persona)
        {
            Color bgColor = persona.primaryColor;
            bgColor.a = 0.3f;
            Widgets.DrawBoxSolid(rect, bgColor);
            
            GUI.color = new Color(persona.primaryColor.r, persona.primaryColor.g, persona.primaryColor.b, 0.8f);
            Widgets.DrawBox(rect, 1);
            GUI.color = Color.white;
            
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = new Color(1f, 1f, 1f, 0.7f);
            
            Rect labelRect = new Rect(rect.x, rect.center.y - 15f, rect.width, 30f);
            Widgets.Label(labelRect, persona.narratorName);
            
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;
        }
    }
}
