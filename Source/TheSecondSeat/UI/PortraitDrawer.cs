using System.Collections.Generic;
using UnityEngine;
using Verse;
using TheSecondSeat.PersonaGeneration;
using TheSecondSeat.Utils;

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

            // 呼吸动画
            float breathingOffset = panel.AnimationHandler.IsPlayingAnimation ? 0f : ExpressionSystem.GetBreathingOffset(panel.GetPersonaResourceName());
            Rect animatedRect = new Rect(panel.DrawRect.x, panel.DrawRect.y + breathingOffset, panel.DrawRect.width, panel.DrawRect.height);

            // 绘制 (使用 GPU 加速渲染)
            DrawLayeredPortraitGPU(animatedRect, panel.CurrentPersona, alpha);
            
            if (panel.AnimationHandler.IsPlayingAnimation)
            {
                DrawEffectLayer(animatedRect);
            }
        }
        
        private void DrawLayeredPortraitGPU(Rect rect, NarratorPersonaDef persona, float alpha)
        {
            string personaName = panel.GetPersonaResourceName();
            List<Texture2D> layers = new List<Texture2D>();
            
            // Layer 1: 身体层
            if (panel.AnimationHandler.IsPlayingAnimation && !string.IsNullOrEmpty(panel.AnimationHandler.OverridePosture))
            {
                Texture2D postureTexture = TSS_AssetLoader.LoadDescentPosture(personaName, panel.AnimationHandler.OverridePosture, null);
                if (postureTexture != null)
                {
                    float aspect = (float)postureTexture.width / postureTexture.height;
                    float targetHeight = rect.height;
                    float targetWidth = targetHeight * aspect;
                    float xOffset = (rect.width - targetWidth) / 2f;
                    Rect drawRect = new Rect(rect.x + xOffset, rect.y, targetWidth, targetHeight);
                    
                    // 单层也走 GPU 渲染以应用统一的 Alpha
                    GPULayeredRenderer.DrawDynamicPortrait(drawRect, new List<Texture2D> { postureTexture }, alpha);
                }
                else if (cachedBodyBase != null)
                {
                    layers.Add(cachedBodyBase);
                }
                else
                {
                    DrawMinimalPlaceholder(rect, persona);
                }
                
                if (layers.Count == 0) return; // 如果是 postureTexture 路径已经处理了，或者没有 bodyBase
            }
            else
            {
                if (cachedBodyBase == null)
                {
                    DrawMinimalPlaceholder(rect, persona);
                    return;
                }
                layers.Add(cachedBodyBase);
            }
            
            // 如果已经处理了特殊姿态（layers为空但已返回），则不再继续
            if (layers.Count == 0) return;

            // Layer 2: 嘴巴层 - TTS口型优先，静默时使用闭嘴
            // 设计原则：表情系统不干涉口型动画
            string mouthLayerName = MouthAnimationSystem.GetMouthLayerName(persona.defName);
            bool isTTSSpeaking = !string.IsNullOrEmpty(mouthLayerName);
            
            // ⭐ v1.13.0: 诊断日志
            if (Prefs.DevMode && Time.frameCount % 120 == 0)
            {
                bool ttsSpeaking = TTS.TTSAudioPlayer.IsSpeaking(persona.defName);
                bool anyoneSpeaking = TTS.TTSAudioPlayer.IsAnyoneSpeaking();
                Log.Message($"[PortraitDrawer] {persona.defName}: mouthLayerName={mouthLayerName ?? "null"}, isTTSSpeaking={isTTSSpeaking}, ttsSpeaking={ttsSpeaking}, anyoneSpeaking={anyoneSpeaking}");
            }
            
            Texture2D mouthTexture = null;
            
            if (isTTSSpeaking)
            {
                // ⭐ v1.11.3: 处理 "USE_BASE" 特殊标记
                // 当 Viseme 为 Small（微张）时，使用 base_body 自带的默认嘴型，不添加额外图层
                if (mouthLayerName != "USE_BASE")
                {
                    // 说话中：完全由 TTS 口型系统控制，表情系统不干涉
                    mouthTexture = PortraitLoader.GetLayerTexture(persona, mouthLayerName, suppressWarning: true);
                    
                    // ⭐ v1.13.0: 诊断纹理加载
                    if (Prefs.DevMode && mouthTexture == null && Time.frameCount % 120 == 0)
                    {
                        Log.Warning($"[PortraitDrawer] Failed to load mouth texture: {mouthLayerName}");
                    }
                }
                // 如果是 "USE_BASE"，mouthTexture 保持为 null，不加载任何嘴巴层
            }
            
            // 回退：仅在非说话状态下使用默认闭嘴
            // ⭐ v1.11.3: 说话状态下如果 mouthLayerName 是 "USE_BASE"，不回退到闭嘴
            if (mouthTexture == null && !(isTTSSpeaking && mouthLayerName == "USE_BASE"))
            {
                mouthTexture = PortraitLoader.GetLayerTexture(persona, "Closed_mouth", suppressWarning: true)
                    ?? PortraitLoader.GetLayerTexture(persona, "Neutral_mouth", suppressWarning: true);
            }
            
            if (mouthTexture != null)
            {
                layers.Add(mouthTexture);
            }

            // Layer 3: 眼睛层
            string eyeLayerName = BlinkAnimationSystem.GetEyeLayerName(persona.defName);
            if (!string.IsNullOrEmpty(eyeLayerName))
            {
                var eyeTexture = PortraitLoader.GetLayerTexture(persona, eyeLayerName);
                if (eyeTexture != null)
                {
                    layers.Add(eyeTexture);
                }
            }
            
            // Layer 4: 特效层 (腮红等) - 支持变体
            var expressionState = ExpressionSystem.GetExpressionState(persona.defName);
            if (expressionState != null && (expressionState.CurrentExpression == ExpressionType.Shy || expressionState.CurrentExpression == ExpressionType.Angry))
            {
                string flushLayerName = GetFlushLayerName(persona.defName, expressionState);
                var flushTexture = PortraitLoader.GetLayerTexture(persona, flushLayerName, suppressWarning: true);
                
                // 如果变体不存在，尝试基础腮红
                if (flushTexture == null)
                {
                    string baseFlush = expressionState.CurrentExpression == ExpressionType.Shy ? "flush_shy" : "flush_angry";
                    flushTexture = PortraitLoader.GetLayerTexture(persona, baseFlush, suppressWarning: true);
                }
                
                if (flushTexture != null)
                {
                    layers.Add(flushTexture);
                }
            }
            
            // 统一 GPU 渲染
            GPULayeredRenderer.DrawDynamicPortrait(rect, layers, alpha);
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
            string message = $"{personaName}\n\n<color=#888888>立绘资源加载中...</color>";
            
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
