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

            // 统一设置颜色
            GUI.color = new Color(1f, 1f, 1f, alpha);

            // 绘制
            bool isGhostMode = !panel.AnimationHandler.IsPlayingAnimation && mouseOver && !shiftHeld;
            DrawLayeredPortrait(animatedRect, panel.CurrentPersona, isGhostMode);
            
            if (panel.AnimationHandler.IsPlayingAnimation)
            {
                DrawEffectLayer(animatedRect);
            }

            // 恢复颜色
            GUI.color = Color.white;
        }
        
        private void DrawLayeredPortrait(Rect rect, NarratorPersonaDef persona, bool isGhostMode)
        {
            string personaName = panel.GetPersonaResourceName();
            
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
                    GUI.DrawTexture(drawRect, postureTexture);
                }
                else if (cachedBodyBase != null)
                {
                    Widgets.DrawTextureFitted(rect, cachedBodyBase, 1.0f);
                }
                else
                {
                    DrawMinimalPlaceholder(rect, persona);
                }
                return; // 降临姿态不支持分层
            }
            else
            {
                if (cachedBodyBase == null)
                {
                    DrawMinimalPlaceholder(rect, persona);
                    return;
                }
                Widgets.DrawTextureFitted(rect, cachedBodyBase, 1.0f);
            }
            
            if (isGhostMode) return;

            // Layer 2: 嘴巴层
            string mouthLayerName = MouthAnimationSystem.GetMouthLayerName(persona.defName);
            if (!string.IsNullOrEmpty(mouthLayerName))
            {
                var mouthTexture = PortraitLoader.GetLayerTexture(persona, mouthLayerName, suppressWarning: true)
                    ?? PortraitLoader.GetLayerTexture(persona, "Closed_mouth", suppressWarning: true)
                    ?? PortraitLoader.GetLayerTexture(persona, "Neutral_mouth", suppressWarning: true);
                
                if (mouthTexture != null)
                {
                    Widgets.DrawTextureFitted(rect, mouthTexture, 1.0f);
                }
            }

            // Layer 3: 眼睛层
            string eyeLayerName = BlinkAnimationSystem.GetEyeLayerName(persona.defName);
            if (!string.IsNullOrEmpty(eyeLayerName))
            {
                var eyeTexture = PortraitLoader.GetLayerTexture(persona, eyeLayerName);
                if (eyeTexture != null)
                {
                    Widgets.DrawTextureFitted(rect, eyeTexture, 1.0f);
                }
            }
            
            // Layer 4: 特效层 (腮红等)
            var expressionState = ExpressionSystem.GetExpressionState(persona.defName);
            if (expressionState.CurrentExpression == ExpressionType.Shy || expressionState.CurrentExpression == ExpressionType.Angry)
            {
                string flushLayerName = expressionState.CurrentExpression == ExpressionType.Shy ? "flush_shy" : "flush_angry";
                var flushTexture = PortraitLoader.GetLayerTexture(persona, flushLayerName);
                if (flushTexture != null)
                {
                    Widgets.DrawTextureFitted(rect, flushTexture, 1.0f);
                }
            }
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