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
            string personaResName = panel.GetPersonaResourceName();
            float breathingOffset = panel.AnimationHandler.IsPlayingAnimation ? 0f : ExpressionSystem.GetBreathingOffset(personaResName);
            float swayOffset = panel.AnimationHandler.IsPlayingAnimation ? 0f : ExpressionSystem.GetSwayOffset(personaResName);
            Rect animatedRect = new Rect(panel.DrawRect.x + swayOffset, panel.DrawRect.y + breathingOffset, panel.DrawRect.width, panel.DrawRect.height);

            // 绘制 (使用 GPU 加速渲染)
            DrawLayeredPortraitGPU(animatedRect, panel.CurrentPersona, alpha);
            
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
            
            // ⭐ v1.13.3: 透明度混合优化 (Cross-fade)
            // 如果处于表情过渡期间，同时绘制旧表情和新表情
            if (state != null && state.TransitionProgress < 1f)
            {
                // 1. 绘制旧表情 (Alpha = 1.0)
                // 注意：这里使用不透明的旧表情作为底图，然后新表情淡入覆盖
                // 如果使用 Alpha 混合 (1-t) 和 (t)，在透明背景下会导致整体透明度下降
                // 所以我们保持旧表情完全不透明（受全局 alpha 影响），新表情逐渐不透明
                
                // 获取旧表情的图层（需要传入 PreviousExpression）
                // 变体逻辑：对于旧表情，我们可能不知道之前的变体是哪个。
                // 简化处理：假设旧表情使用基础变体(0)或随机变体(0)。
                // 实际上，为了平滑，最好能记录旧表情的变体。但 ExpressionState 没存旧变体。
                // 妥协：旧表情使用变体0（基础）。如果之前是 happy1，现在变成 sad，可能会看到 happy1 -> happy -> sad 的瞬间变化。
                // 但由于是淡出，可能不明显。
                var oldLayers = GetLayersForExpression(persona, state.PreviousExpression, 0);
                if (oldLayers != null && oldLayers.Count > 0)
                {
                    GPULayeredRenderer.DrawDynamicPortrait(rect, oldLayers, alpha);
                }
                
                // 2. 绘制新表情 (Alpha = TransitionProgress)
                // 新表情淡入覆盖在旧表情之上
                var newLayers = GetLayersForExpression(persona, state.CurrentExpression, state.Intensity > 0 ? state.Intensity : state.CurrentVariant);
                if (newLayers != null && newLayers.Count > 0)
                {
                    // 使用平滑插值让过渡更自然 (EaseInOut)
                    float t = state.TransitionProgress;
                    float smoothT = t * t * (3f - 2f * t);
                    
                    GPULayeredRenderer.DrawDynamicPortrait(rect, newLayers, alpha * smoothT);
                }
            }
            else
            {
                // 无过渡，直接绘制当前表情
                int variant = (state != null && state.Intensity > 0) ? state.Intensity : (state?.CurrentVariant ?? 0);
                var layers = GetLayersForExpression(persona, state?.CurrentExpression ?? ExpressionType.Neutral, variant);
                
                if (layers != null && layers.Count > 0)
                {
                    GPULayeredRenderer.DrawDynamicPortrait(rect, layers, alpha);
                }
            }
        }
        
        /// <summary>
        /// ⭐ v1.13.3: 获取特定表情的图层列表
        /// 提取自 DrawLayeredPortraitGPU，用于支持表情混合
        /// </summary>
        private List<Texture2D> GetLayersForExpression(NarratorPersonaDef persona, ExpressionType expression, int variant)
        {
            string personaName = panel.GetPersonaResourceName();
            List<Texture2D> layers = new List<Texture2D>();
            
            // Layer 1: 身体层
            if (panel.AnimationHandler.IsPlayingAnimation && !string.IsNullOrEmpty(panel.AnimationHandler.OverridePosture))
            {
                Texture2D postureTexture = TSS_AssetLoader.LoadDescentPosture(personaName, panel.AnimationHandler.OverridePosture, null);
                if (postureTexture != null)
                {
                    // 特殊姿态直接返回单层
                    return new List<Texture2D> { postureTexture };
                }
                else if (cachedBodyBase != null)
                {
                    layers.Add(cachedBodyBase);
                }
                else
                {
                    // 无法加载任何身体，返回空（外部会处理占位符）
                    if (cachedBodyBase == null) UpdateBodyBaseIfNeeded();
                    if (cachedBodyBase == null) return null;
                }
            }
            else
            {
                if (cachedBodyBase == null) UpdateBodyBaseIfNeeded();
                if (cachedBodyBase == null) return null;
                
                layers.Add(cachedBodyBase);
            }
            
            // Layer 2: 嘴巴层
            // 如果是 TTS 说话状态，忽略表情参数，直接使用当前口型（因为口型是实时的）
            // 如果不是说话状态，使用指定表情的静态嘴型
            string mouthLayerName = MouthAnimationSystem.GetMouthLayerName(persona.defName);
            bool isTTSSpeaking = !string.IsNullOrEmpty(mouthLayerName);
            Texture2D mouthTexture = null;
            
            if (isTTSSpeaking)
            {
                if (mouthLayerName != "USE_BASE")
                {
                    mouthTexture = PortraitLoader.GetLayerTexture(persona, mouthLayerName, suppressWarning: true);
                }
            }
            
            // 回退/静态表情逻辑
            if (mouthTexture == null && !(isTTSSpeaking && mouthLayerName == "USE_BASE"))
            {
                // 使用指定表情和变体获取静态嘴型
                string staticMouth = MouthAnimationSystem.GetStaticMouthLayerName(persona.defName, expression, variant);
                mouthTexture = PortraitLoader.GetLayerTexture(persona, staticMouth, suppressWarning: true);

                // 回退逻辑：如果变体加载失败，尝试基础
                if (mouthTexture == null && staticMouth.Any(char.IsDigit))
                {
                    string baseMouth = System.Text.RegularExpressions.Regex.Replace(staticMouth, @"\d+", "");
                    if (baseMouth != staticMouth)
                    {
                        mouthTexture = PortraitLoader.GetLayerTexture(persona, baseMouth, suppressWarning: true);
                    }
                }

                // 最后回退
                if (mouthTexture == null)
                {
                    mouthTexture = PortraitLoader.GetLayerTexture(persona, "Closed_mouth", suppressWarning: true)
                        ?? PortraitLoader.GetLayerTexture(persona, "Neutral_mouth", suppressWarning: true);
                }
            }
            
            if (mouthTexture != null)
            {
                layers.Add(mouthTexture);
            }

            // Layer 3: 眼睛层
            // 如果当前正在眨眼（闭眼），则忽略表情，显示闭眼
            // 否则显示指定表情的眼睛
            string eyeLayerName = BlinkAnimationSystem.GetEyeLayerName(persona.defName);
            bool isBlinking = eyeLayerName == "closed_eyes";
            
            if (isBlinking)
            {
                var eyeTexture = PortraitLoader.GetLayerTexture(persona, "closed_eyes", suppressWarning: true);
                if (eyeTexture != null) layers.Add(eyeTexture);
            }
            else
            {
                // 获取指定表情的眼睛
                string exprEyeName = BlinkAnimationSystem.GetEyesLayerNameForExpression(persona.defName, expression, variant);
                if (!string.IsNullOrEmpty(exprEyeName))
                {
                    var eyeTexture = PortraitLoader.GetLayerTexture(persona, exprEyeName, suppressWarning: true);
                    
                    // 回退逻辑
                    if (eyeTexture == null && exprEyeName.Any(char.IsDigit))
                    {
                        string baseEye = System.Text.RegularExpressions.Regex.Replace(exprEyeName, @"\d+", "");
                        eyeTexture = PortraitLoader.GetLayerTexture(persona, baseEye, suppressWarning: true);
                    }
                    
                    if (eyeTexture != null) layers.Add(eyeTexture);
                }
                else
                {
                    // Neutral 或 null，尝试 base_eyes
                    var baseEye = PortraitLoader.GetLayerTexture(persona, "base_eyes", suppressWarning: true)
                               ?? PortraitLoader.GetLayerTexture(persona, "neutral_eyes", suppressWarning: true);
                    if (baseEye != null) layers.Add(baseEye);
                }
            }
            
            // Layer 4: 特效层 (腮红等)
            if (expression == ExpressionType.Shy || expression == ExpressionType.Angry)
            {
                string baseName = expression == ExpressionType.Shy ? "flush_shy" : "flush_angry";
                string exprName = expression.ToString().ToLower();
                string flushLayerName = variant > 0 ? $"flush_{exprName}{variant}" : baseName;
                
                var flushTexture = PortraitLoader.GetLayerTexture(persona, flushLayerName, suppressWarning: true);
                
                if (flushTexture == null && variant > 0)
                {
                    flushTexture = PortraitLoader.GetLayerTexture(persona, baseName, suppressWarning: true);
                }
                
                if (flushTexture != null)
                {
                    layers.Add(flushTexture);
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
