using UnityEngine;
using Verse;
using RimWorld;
using TheSecondSeat.Settings;
using TheSecondSeat.Core;
using TheSecondSeat.PersonaGeneration;

namespace TheSecondSeat.UI
{
    /// <summary>
    /// 处理全身立绘的所有用户交互。
    /// 从 FullBodyPortraitPanel 中分离出来的独立系统。
    /// </summary>
    public class PortraitInteractionHandler
    {
        private readonly FullBodyPortraitPanel panel;
        
        // 拖动和缩放
        private bool isDragging = false;
        private bool isResizing = false;
        private Vector2 dragStartPos;
        private Rect dragStartRect;
        private const float ResizeHandleSize = 20f;
        
        // 触摸互动
        private float hoverStartTime = 0f;
        private bool isHovering = false;
        private bool isTouchModeActive = false;
        private Vector2 lastMousePosition = Vector2.zero;
        private float lastTouchTime = 0f;
        private int touchCount = 0;
        
        // 区域交互
        private float headRubProgress = 0f;
        private float lastHeadPatTime = 0f;
        
        // 边框闪烁
        private float borderFlashStartTime = 0f;
        private int borderFlashCount = 0;
        
        private ExpressionType[] touchExpressions = new[] 
        {
            ExpressionType.Happy, ExpressionType.Surprised, ExpressionType.Smug, ExpressionType.Shy
        };

        public PortraitInteractionHandler(FullBodyPortraitPanel panel)
        {
            this.panel = panel;
        }

        /// <summary>
        /// 主更新循环，处理输入和交互状态。
        /// </summary>
        public void HandleInteractions()
        {
            HandlePanelInteraction();
            
            bool mouseOver = panel.DrawRect.Contains(Event.current.mousePosition);
            bool shiftHeld = Event.current.shift;

            if (panel.AnimationHandler.IsPlayingAnimation || !shiftHeld || !mouseOver)
            {
                ResetInteractionState();
                return;
            }

            // 处理区域交互
            bool interactionHandled = HandleZoneInteraction();
            
            // 处理触摸互动（如果区域交互未处理）
            if (!interactionHandled)
            {
                HandleHoverAndTouch();
            }
            
            // 绘制交互提示
            DrawInteractionUI();
            
            // 拦截输入
            if (Event.current.type == EventType.MouseDown)
            {
                Event.current.Use();
            }
        }

        private void ResetInteractionState()
        {
            if (isTouchModeActive || isHovering)
            {
                DeactivateTouchMode();
            }
            isHovering = false;

            if (headRubProgress > 0f)
            {
                headRubProgress -= TSSFrameworkConfig.Interaction.HeadRubDecayRate * Time.deltaTime;
                if (headRubProgress < 0f) headRubProgress = 0f;
            }
        }
        
        private void HandlePanelInteraction()
        {
            Vector2 mousePos = Event.current.mousePosition;
            Rect resizeHandleRect = new Rect(panel.DrawRect.xMax - ResizeHandleSize, panel.DrawRect.yMax - ResizeHandleSize, ResizeHandleSize, ResizeHandleSize);
            bool mouseOverResize = resizeHandleRect.Contains(mousePos);

            if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
            {
                if (mouseOverResize)
                {
                    isResizing = true;
                    isDragging = false;
                    dragStartPos = mousePos;
                    dragStartRect = panel.DrawRect;
                    Event.current.Use();
                }
                else if (panel.DrawRect.Contains(mousePos))
                {
                    isDragging = true;
                    isResizing = false;
                    dragStartPos = mousePos;
                    dragStartRect = panel.DrawRect;
                    Event.current.Use();
                }
            }
            else if (Event.current.type == EventType.MouseDrag)
            {
                if (isResizing)
                {
                    Vector2 delta = mousePos - dragStartPos;
                    float newWidth = Mathf.Max(100f, dragStartRect.width + delta.x);
                    float newHeight = Mathf.Max(100f, dragStartRect.height + delta.y);
                    panel.UpdateDrawRect(panel.DrawRect.x, panel.DrawRect.y, newWidth, newHeight);
                    Event.current.Use();
                }
                else if (isDragging)
                {
                    Vector2 delta = mousePos - dragStartPos;
                    float newX = dragStartRect.x + delta.x;
                    float newY = dragStartRect.y + delta.y;
                    panel.UpdateDrawRect(newX, newY, panel.DrawRect.width, panel.DrawRect.height);
                    Event.current.Use();
                }
            }
            else if (Event.current.type == EventType.MouseUp)
            {
                isDragging = false;
                isResizing = false;
            }
        }

        private bool HandleZoneInteraction()
        {
            Vector2 mousePos = Event.current.mousePosition;
            var zone = GetInteractionZone(mousePos);
            
            if (zone == InteractionPhrases.InteractionZone.Head)
            {
                bool isMouseDragging = (Event.current.type == EventType.MouseDrag) || (Event.current.type == EventType.MouseMove && Event.current.button == 0);
                
                if (isMouseDragging)
                {
                    float moveDistance = Vector2.Distance(mousePos, lastMousePosition);
                    headRubProgress += moveDistance * 0.5f;
                    
                    UpdateExpressionByHeadRub(headRubProgress);
                    
                    if (headRubProgress >= TSSFrameworkConfig.Interaction.HeadRubThreshold)
                    {
                        float currentTime = Time.realtimeSinceStartup;
                        if (currentTime - lastHeadPatTime >= TSSFrameworkConfig.Interaction.HeadPatCooldown)
                        {
                            DoHeadPatInteraction();
                            headRubProgress = 0f;
                            lastHeadPatTime = currentTime;
                            Event.current.Use();
                            return true;
                        }
                    }
                    lastMousePosition = mousePos;
                }
                else if (Event.current.type == EventType.MouseDown)
                {
                    lastMousePosition = mousePos;
                }
            }
            else
            {
                if (headRubProgress > 0f)
                {
                    headRubProgress -= TSSFrameworkConfig.Interaction.HeadRubDecayRate * Time.deltaTime;
                    if (headRubProgress < 0f) headRubProgress = 0f;
                }
            }
            
            if (zone == InteractionPhrases.InteractionZone.Body)
            {
                if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
                {
                    DoPokeInteraction();
                    Event.current.Use();
                    return true;
                }
            }
            
            return false;
        }

        private void HandleHoverAndTouch()
        {
            if (!isHovering)
            {
                isHovering = true;
                hoverStartTime = Time.realtimeSinceStartup;
            }
            else
            {
                float hoverDuration = Time.realtimeSinceStartup - hoverStartTime;
                if (!isTouchModeActive && hoverDuration >= TSSFrameworkConfig.Interaction.HoverActivationTime)
                {
                    ActivateTouchMode();
                }
            }
            
            if (isTouchModeActive)
            {
                Vector2 currentMousePos = Event.current.mousePosition;
                if (Vector2.Distance(currentMousePos, lastMousePosition) > 5f)
                {
                    float currentTime = Time.realtimeSinceStartup;
                    if (currentTime - lastTouchTime > TSSFrameworkConfig.Interaction.TouchCooldown)
                    {
                        OnTouchMove(currentMousePos);
                        lastTouchTime = currentTime;
                    }
                }
                lastMousePosition = currentMousePos;
            }
        }
        
        public void StartBorderFlash(int count)
        {
            borderFlashStartTime = Time.realtimeSinceStartup;
            borderFlashCount = count;
        }

        #region Interaction Effects

        private void DoHeadPatInteraction()
        {
            float affinity = panel.StorytellerAgent?.GetAffinity() ?? 0f;
            float bonus = TSSFrameworkConfig.Interaction.HeadPatAffinityBonus;
            
            ExpressionType exprType = SelectExpressionByAffinity(affinity, ExpressionType.Shy, ExpressionType.Happy);
            int intensity = (affinity >= TSSFrameworkConfig.Interaction.HighAffinityThreshold) ? 2 : 0;
            panel.TriggerExpression(exprType, duration: 3f, intensity: intensity);
            
            string phrase = PhraseManager.Instance.TriggerHeadPat(panel.GetPersonaResourceName());
            if (string.IsNullOrEmpty(phrase))
            {
                phrase = InteractionPhrases.GetHeadPatPhrase(affinity);
            }
            
            panel.AddFloatingText(phrase, GetTextColorByAffinity(affinity));
            panel.ShowInteractionText(phrase);
            
            if (affinity >= TSSFrameworkConfig.Interaction.HighAffinityThreshold)
            {
                StartBorderFlash(1);
                panel.ModifyAffinity(bonus, "头部摸摸互动");
                Messages.Message($"好感度 +{bonus}（头部摸摸）", MessageTypeDefOf.PositiveEvent);
            }
            else if (affinity < TSSFrameworkConfig.Interaction.LowAffinityThreshold)
            {
                panel.ModifyAffinity(-1f, "不受欢迎的触碰");
            }
        }
        
        private void UpdateExpressionByHeadRub(float progress)
        {
            if (panel.CurrentPersona == null) return;
            
            int targetIntensity = 1 + Mathf.FloorToInt(progress / 200f);
            targetIntensity = Mathf.Clamp(targetIntensity, 1, 3);
            
            var state = ExpressionSystem.GetExpressionState(panel.GetPersonaResourceName());
            if (state.Intensity != targetIntensity)
            {
                ExpressionSystem.SetExpression(panel.GetPersonaResourceName(), ExpressionType.Happy, ExpressionTrigger.Manual, 30, targetIntensity);
            }
        }

        private void DoPokeInteraction()
        {
            float affinity = panel.StorytellerAgent?.GetAffinity() ?? 0f;
            float bonus = TSSFrameworkConfig.Interaction.PokeAffinityBonus;
            
            panel.TriggerExpression(SelectExpressionByAffinity(affinity, ExpressionType.Surprised, ExpressionType.Happy), duration: 2f);
            
            string phrase = PhraseManager.Instance.TriggerBodyPoke(panel.GetPersonaResourceName());
            if (string.IsNullOrEmpty(phrase))
            {
                phrase = InteractionPhrases.GetPokePhrase(affinity);
            }
            
            panel.AddFloatingText(phrase, GetTextColorByAffinity(affinity));
            panel.ShowInteractionText(phrase);
            
            if (affinity >= TSSFrameworkConfig.Interaction.HighAffinityThreshold)
                panel.ModifyAffinity(bonus, "身体戳戳互动");
            else if (affinity < TSSFrameworkConfig.Interaction.LowAffinityThreshold)
                panel.ModifyAffinity(-0.5f, "烦人的触碰");
        }
        
        private void ActivateTouchMode()
        {
            isTouchModeActive = true;
            touchCount = 0;
            lastMousePosition = Event.current.mousePosition;
            
            panel.TriggerExpression(ExpressionType.Confused, duration: 2f);
            StartBorderFlash(1);
            panel.AddFloatingText("(?ω?)?", new Color(0.8f, 0.9f, 1f));
        }

        private void DeactivateTouchMode()
        {
            if (!isTouchModeActive) return;
            
            isTouchModeActive = false;
            touchCount = 0;
            panel.RestoreDefaultExpression();
        }

        private void OnTouchMove(Vector2 mousePos)
        {
            touchCount++;
            float moveSpeed = Vector2.Distance(mousePos, lastMousePosition) / Time.deltaTime;
            
            if (moveSpeed > 500f)
            {
                panel.TriggerExpression(ExpressionType.Shy, duration: 1.5f);
                panel.AddFloatingText("(/ω＼)", new Color(1f, 0.6f, 0.6f));
            }
            else if (touchCount % 3 == 0)
            {
                var expression = touchExpressions[UnityEngine.Random.Range(0, touchExpressions.Length)];
                panel.TriggerExpression(expression, duration: 2f);
                
                string[] emojis = { "(?▽｀)", "(๑˃ᴗ˂)✧", "(≧▽≦)", "ヾ(◍°∇°◍)ﾉ", "(๑˃̵ᴗ˂̵)" };
                panel.AddFloatingText(emojis[UnityEngine.Random.Range(0, emojis.Length)], new Color(1f, 0.8f, 0.9f));
            }
            
            if (touchCount >= 10)
            {
                OnTouchCombo();
                touchCount = 0;
            }
        }
        
        private void OnTouchCombo()
        {
            bool isHappy = UnityEngine.Random.value > 0.3f;
            panel.TriggerExpression(isHappy ? ExpressionType.Happy : ExpressionType.Smug, duration: 3f);
            
            panel.AddFloatingText(isHappy ? "(*^▽^*)" : "(￣︶￣)↗", new Color(1f, 0.7f, 0.3f));
            StartBorderFlash(3);
            
            float bonus = TSSFrameworkConfig.Interaction.TouchComboAffinityBonus;
            panel.ModifyAffinity(bonus, "全身立绘触摸互动");
            Messages.Message($"好感度 +{bonus}（全身立绘互动）", MessageTypeDefOf.PositiveEvent);
        }

        #endregion

        #region UI Drawing

        private void DrawInteractionUI()
        {
            Rect inRect = panel.DrawRect;
            if (isHovering && !isTouchModeActive)
            {
                float progress = (Time.realtimeSinceStartup - hoverStartTime) / TSSFrameworkConfig.Interaction.HoverActivationTime;
                DrawHoverProgress(inRect, progress);
            }
            if (headRubProgress > 0f)
            {
                DrawHeadRubProgress(inRect, headRubProgress / TSSFrameworkConfig.Interaction.HeadRubThreshold);
            }
            if (isTouchModeActive)
            {
                DrawTouchModeIndicator(inRect);
            }
            DrawBorderFlash(inRect);
        }

        private void DrawProgressBar(Rect barRect, float progress, Color startColor, Color endColor)
        {
            progress = Mathf.Clamp01(progress);
            Widgets.DrawBoxSolid(barRect, new Color(0.2f, 0.2f, 0.2f, 0.6f));
            Widgets.DrawBoxSolid(new Rect(barRect.x, barRect.y, barRect.width * progress, barRect.height), Color.Lerp(startColor, endColor, progress));
        }

        private void DrawHoverProgress(Rect inRect, float progress) => 
            DrawProgressBar(new Rect(inRect.x, inRect.yMax + 2f, inRect.width, 8f), progress, new Color(0.3f, 0.8f, 1f), new Color(1f, 0.8f, 0.3f));

        private void DrawHeadRubProgress(Rect inRect, float progress)
        {
            var barRect = new Rect(inRect.x, inRect.y - 12f, inRect.width, 8f);
            DrawProgressBar(barRect, progress, new Color(1f, 0.6f, 0.6f), new Color(1f, 0.3f, 0.3f));
            
            if (progress > 0.5f)
            {
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = new Color(1f, 1f, 1f, 0.8f);
                Widgets.Label(barRect, "继续摸摸...");
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
            }
        }

        private void DrawTouchModeIndicator(Rect inRect)
        {
            float alpha = 0.5f + 0.5f * Mathf.Sin(Time.realtimeSinceStartup * 3f);
            GUI.color = new Color(1f, 0.8f, 0.3f, alpha);
            Widgets.DrawBox(inRect, 3);
            GUI.color = Color.white;
            
            if (touchCount > 0)
            {
                var countRect = new Rect(inRect.xMax - 40f, inRect.yMax - 40f, 35f, 25f);
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = new Color(1f, 1f, 1f, 0.9f);
                Widgets.Label(countRect, $"×{touchCount}");
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
            }
        }

        private void DrawBorderFlash(Rect inRect)
        {
            if (borderFlashCount <= 0) return;
            
            float elapsed = Time.realtimeSinceStartup - borderFlashStartTime;
            float alpha = 0f;
            
            if (borderFlashCount == 1) // Slow flash
            {
                if (elapsed < 1.0f) alpha = Mathf.Sin((elapsed / 1.0f) * Mathf.PI);
                else borderFlashCount = 0;
            }
            else if (borderFlashCount > 1) // Fast flashes
            {
                float cycleDuration = 0.15f + 0.05f;
                int currentCycle = Mathf.FloorToInt(elapsed / cycleDuration);
                if (currentCycle < borderFlashCount)
                {
                    float cycleProgress = (elapsed % cycleDuration) / 0.15f;
                    if (cycleProgress < 1.0f) alpha = Mathf.Sin(cycleProgress * Mathf.PI);
                }
                else
                {
                    borderFlashCount = 0;
                }
            }
            
            if (alpha > 0f)
            {
                GUI.color = new Color(1f, 1f, 1f, alpha * 0.5f);
                Widgets.DrawBox(inRect, 3);
                GUI.color = Color.white;
            }
        }
        
        #endregion

        #region Helpers
        
        private InteractionPhrases.InteractionZone GetInteractionZone(Vector2 mousePos)
        {
            Rect rect = panel.DrawRect;
            if (!rect.Contains(mousePos)) return InteractionPhrases.InteractionZone.None;
            
            float relativeY = (mousePos.y - rect.y) / rect.height;
            if (relativeY < 0.25f) return InteractionPhrases.InteractionZone.Head;
            
            // A simplified check for body hit. A more precise check would involve texture alpha.
            float relativeX = (mousePos.x - rect.x) / rect.width;
            if (relativeX > 0.1f && relativeX < 0.9f && relativeY > 0.1f && relativeY < 0.9f)
            {
                 return InteractionPhrases.InteractionZone.Body;
            }
            
            return InteractionPhrases.InteractionZone.None;
        }
        
        private ExpressionType SelectExpressionByAffinity(float affinity, ExpressionType highPositive, ExpressionType lowPositive)
        {
            if (affinity >= TSSFrameworkConfig.Interaction.HighAffinityThreshold)
                return UnityEngine.Random.value > 0.5f ? highPositive : ExpressionType.Happy;
            if (affinity >= TSSFrameworkConfig.Interaction.LowAffinityThreshold)
                return UnityEngine.Random.value > 0.5f ? ExpressionType.Confused : ExpressionType.Neutral;
            return ExpressionType.Angry;
        }
        
        private Color GetTextColorByAffinity(float affinity)
        {
            return affinity >= TSSFrameworkConfig.Interaction.HighAffinityThreshold ? TSSFrameworkConfig.Colors.HighAffinityTextColor
                 : affinity >= TSSFrameworkConfig.Interaction.LowAffinityThreshold ? TSSFrameworkConfig.Colors.NeutralAffinityTextColor
                 : TSSFrameworkConfig.Colors.LowAffinityTextColor;
        }
        
        #endregion
    }
}