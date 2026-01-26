using System;
using UnityEngine;
using Verse;
using Verse.Sound;
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
        private Vector2 lastMousePosition = Vector2.zero;
        private float currentRubDuration = 0f; // 当前头部悬浮时长
        private float bodyHoverTime = 0f;      // 当前身体悬浮时长
        private float lastHeadPatTime = 0f;
        private float lastPokeTime = 0f;
        private float lastTouchMoveTime = 0f;
        private int touchCount = 0;
        
        // ⭐ 摸头表情状态追踪（避免每帧重复触发）
        private int lastHeadPatPhaseIndex = -1;      // 上一次摸头阶段索引
        private ExpressionType lastHeadRubExpression = ExpressionType.Neutral;  // 上一次摸头表情
        private int lastHeadRubIntensity = 0;        // 上一次摸头表情强度
        
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
            
            // 如果正在拖动或调整大小，跳过交互处理
            if (isDragging || isResizing)
            {
                return;
            }
            
            bool mouseOver = panel.DrawRect.Contains(Event.current.mousePosition);

            // 如果鼠标移出面板，或者未按住 Shift，重置交互状态
            if (panel.AnimationHandler.IsPlayingAnimation || !mouseOver || !Event.current.shift)
            {
                ResetInteractionState();
                return;
            }
            
            // ⭐ 所有交互改为悬浮触发 (Shift + Hover)
            HandleHoverInteraction();
            
            // 绘制交互提示
            DrawInteractionUI();
        }

        private void ResetInteractionState()
        {
            if (currentRubDuration > 0f || bodyHoverTime > 0f)
            {
                // 如果是从摸头状态退出，让表情维持 30秒 后再自然过期
                // 而不是立即强制恢复默认
                var state = ExpressionSystem.GetExpressionState(panel.CurrentPersona?.defName);
                if (state != null)
                {
                    state.DurationTicks = 1800; // 30秒
                    state.ExpressionStartTick = Find.TickManager.TicksGame; // 重置计时器
                }
                // panel.RestoreDefaultExpression(); // 不再立即调用
            }
            
            currentRubDuration = 0f;
            bodyHoverTime = 0f;
            touchCount = 0;
            
            lastHeadPatPhaseIndex = -1;
            lastHeadRubExpression = ExpressionType.Neutral;
            lastHeadRubIntensity = 0;
        }
        
        private void HandlePanelInteraction()
        {
            Vector2 mousePos = Event.current.mousePosition;
            Rect resizeHandleRect = new Rect(panel.DrawRect.xMax - ResizeHandleSize, panel.DrawRect.yMax - ResizeHandleSize, ResizeHandleSize, ResizeHandleSize);
            bool mouseOverResize = resizeHandleRect.Contains(mousePos);
            bool mouseOverPanel = panel.DrawRect.Contains(mousePos);
            
            // ⭐ 移动面板需要：Alt + 左键，或者鼠标中键
            // 调整大小需要：拖动右下角手柄（任意按键方式）
            bool isAltLeftClick = Event.current.button == 0 && Event.current.alt;
            bool isMiddleClick = Event.current.button == 2;
            bool isDragKey = isAltLeftClick || isMiddleClick;
            
            // ⭐ v2.6.5: 关键修复 - 当 Alt 键按下且鼠标在面板区域内时，
            // 需要在所有鼠标事件类型上消耗事件，防止原版框选功能获取事件
            if (Event.current.alt && mouseOverPanel)
            {
                // 对于所有鼠标相关事件，如果在面板区域内且按住 Alt，都需要处理
                if (Event.current.type == EventType.MouseDown ||
                    Event.current.type == EventType.MouseDrag ||
                    Event.current.type == EventType.MouseUp)
                {
                    // 即使不是左键，也要阻止事件传播到原版系统
                    // 但只有左键才真正执行拖动逻辑
                }
            }

            if (Event.current.type == EventType.MouseDown)
            {
                // 调整大小：右下角手柄 + 左键
                if (mouseOverResize && Event.current.button == 0)
                {
                    isResizing = true;
                    isDragging = false;
                    dragStartPos = mousePos;
                    dragStartRect = panel.DrawRect;
                    Event.current.Use();
                    return; // 立即返回，确保事件被完全消耗
                }
                // 拖动移动：Alt+左键 或 中键
                else if (isDragKey && mouseOverPanel)
                {
                    isDragging = true;
                    isResizing = false;
                    dragStartPos = mousePos;
                    dragStartRect = panel.DrawRect;
                    Event.current.Use();
                    return; // 立即返回，确保事件被完全消耗
                }
                // ⭐ v2.6.5: 即使不开始拖动，Alt+鼠标在面板上也要消耗事件
                else if (Event.current.alt && mouseOverPanel && Event.current.button == 0)
                {
                    Event.current.Use();
                    return;
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
                    return;
                }
                else if (isDragging)
                {
                    Vector2 delta = mousePos - dragStartPos;
                    float newX = dragStartRect.x + delta.x;
                    float newY = dragStartRect.y + delta.y;
                    panel.UpdateDrawRect(newX, newY, panel.DrawRect.width, panel.DrawRect.height);
                    Event.current.Use();
                    return;
                }
                // ⭐ v2.6.5: Alt+拖动在面板上也要消耗事件（即使没有开始拖动状态）
                else if (Event.current.alt && mouseOverPanel)
                {
                    Event.current.Use();
                    return;
                }
            }
            else if (Event.current.type == EventType.MouseUp)
            {
                bool wasDragging = isDragging;
                bool wasResizing = isResizing;
                isDragging = false;
                isResizing = false;
                
                // ⭐ v2.6.5: 如果之前在拖动/调整大小，或者 Alt+鼠标在面板上，消耗事件
                if (wasDragging || wasResizing || (Event.current.alt && mouseOverPanel))
                {
                    Event.current.Use();
                    return;
                }
            }
            
            // ⭐ 绘制移动提示（当按住 Alt 且鼠标悬停时）
            if (Event.current.alt && mouseOverPanel && !isResizing)
            {
                // 绘制移动模式指示边框
                GUI.color = new Color(0.3f, 0.7f, 1f, 0.5f);
                Widgets.DrawBox(panel.DrawRect, 2);
                GUI.color = Color.white;
            }
        }

        private void HandleHoverInteraction()
        {
            Vector2 mousePos = Event.current.mousePosition;
            var zone = GetInteractionZone(mousePos);
            float moveDist = Vector2.Distance(mousePos, lastMousePosition);
            lastMousePosition = mousePos;
            
            // ⭐ v2.3.0: 记录玩家交互活动，唤醒打瞌睡的叙事者
            if (zone != InteractionPhrases.InteractionZone.None)
            {
                NarratorIdleSystem.RecordActivity("玩家交互");
            }
            
            // 头部交互：悬浮持续触发（摸头）
            if (zone == InteractionPhrases.InteractionZone.Head)
            {
                currentRubDuration += Time.deltaTime;
                bodyHoverTime = 0f;
                
                // 持续更新摸头视觉效果（表情和手部）
                // 实际的好感度/对话触发有冷却时间
                DoHeadPatInteraction();
            }
            // 身体交互：悬浮停顿触发（戳/注视）
            else if (zone == InteractionPhrases.InteractionZone.Body)
            {
                currentRubDuration = 0f;
                bodyHoverTime += Time.deltaTime;
                
                // 悬浮超过 0.5秒 触发身体互动
                if (bodyHoverTime > 0.5f)
                {
                    float currentTime = Time.realtimeSinceStartup;
                    if (currentTime - lastPokeTime >= 5f) // 5秒冷却
                    {
                        DoPokeInteraction();
                        lastPokeTime = currentTime;
                        // 触发后不重置 bodyHoverTime，防止连续触发，依靠冷却控制
                    }
                }
            }
            // 通用区域：移动触发抚摸
            else
            {
                currentRubDuration = 0f;
                bodyHoverTime = 0f;
                
                if (moveDist > 2f) // 有明显的移动
                {
                    float currentTime = Time.realtimeSinceStartup;
                    if (currentTime - lastTouchMoveTime > 0.2f) // 限制频率
                    {
                        OnTouchMove(mousePos);
                        lastTouchMoveTime = currentTime;
                    }
                }
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
            // 视觉效果由 DrawHeadPatOverlay 处理（基于 currentRubDuration）
            
            // 逻辑触发（对话、好感度）受冷却限制
            float currentTime = Time.realtimeSinceStartup;
            if (currentTime - lastHeadPatTime < TSSFrameworkConfig.Interaction.HeadPatCooldown)
            {
                return;
            }
            lastHeadPatTime = currentTime;
            
            // ⭐ v2.3.0: 记录玩家交互活动，唤醒打瞌睡的叙事者
            NarratorIdleSystem.RecordActivity("摸头互动");

            float affinity = panel.StorytellerAgent?.GetAffinity() ?? 0f;
            float bonus = TSSFrameworkConfig.Interaction.HeadPatAffinityBonus;
            
            // 检查是否启用了 RenderTree 的 HeadPat 系统
            var def = RenderTreeDefManager.GetRenderTree(panel.CurrentPersona?.defName);
            bool headPatEnabled = def != null && def.headPat != null && def.headPat.enabled;

            // 只有在未启用 HeadPat 系统时，才在这里手动触发表情
            // 否则交由 DrawHeadPatOverlay 控制持续表情（防止 duration 冲突导致表情闪烁或提前结束）
            if (!headPatEnabled)
            {
                ExpressionType exprType = SelectExpressionByAffinity(affinity, ExpressionType.Shy, ExpressionType.Happy);
                int intensity = (affinity >= TSSFrameworkConfig.Interaction.HighAffinityThreshold) ? 2 : 0;
                panel.TriggerExpression(exprType, duration: 2f, intensity: intensity);
            }
            
            string phrase = PhraseManager.Instance.TriggerHeadPat(panel.GetPersonaResourceName());
            if (string.IsNullOrEmpty(phrase))
            {
                phrase = InteractionPhrases.GetHeadPatPhrase(affinity);
            }
            
            panel.ShowInteractionText(phrase);
            NarratorWindow.AddAIMessage(phrase);
            
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

        private void DoPokeInteraction()
        {
            // ⭐ v2.3.0: 记录玩家交互活动，唤醒打瞌睡的叙事者
            NarratorIdleSystem.RecordActivity("戳戳互动");
            
            float affinity = panel.StorytellerAgent?.GetAffinity() ?? 0f;
            float bonus = TSSFrameworkConfig.Interaction.PokeAffinityBonus;
            
            panel.TriggerExpression(SelectExpressionByAffinity(affinity, ExpressionType.Surprised, ExpressionType.Happy), duration: 2f);
            
            string phrase = PhraseManager.Instance.TriggerBodyPoke(panel.GetPersonaResourceName());
            if (string.IsNullOrEmpty(phrase))
            {
                phrase = InteractionPhrases.GetPokePhrase(affinity);
            }
            
            // ⭐ 使用短语库：显示在对话框和聊天记录中
            panel.ShowInteractionText(phrase);
            NarratorWindow.AddAIMessage(phrase);
            
            if (affinity >= TSSFrameworkConfig.Interaction.HighAffinityThreshold)
                panel.ModifyAffinity(bonus, "身体戳戳互动");
            else if (affinity < TSSFrameworkConfig.Interaction.LowAffinityThreshold)
                panel.ModifyAffinity(-0.5f, "烦人的触碰");
        }
        
        private void OnTouchMove(Vector2 mousePos)
        {
            touchCount++;
            
            if (touchCount % 10 == 0) // 每移动一定距离触发一次反馈
            {
                var expression = touchExpressions[UnityEngine.Random.Range(0, touchExpressions.Length)];
                panel.TriggerExpression(expression, duration: 1f);
            }
            
            if (touchCount >= 50) // 连续抚摸触发连击
            {
                OnTouchCombo();
                touchCount = 0;
            }
        }

        private void OnTouchCombo()
        {
            bool isHappy = UnityEngine.Random.value > 0.3f;
            panel.TriggerExpression(isHappy ? ExpressionType.Happy : ExpressionType.Smug, duration: 2f);
            StartBorderFlash(2);
            
            float bonus = TSSFrameworkConfig.Interaction.TouchComboAffinityBonus;
            panel.ModifyAffinity(bonus, "全身立绘触摸互动");
        }

        #endregion

        #region UI Drawing

        private void DrawInteractionUI()
        {
            Rect inRect = panel.DrawRect;
            if (currentRubDuration > 0f)
            {
                DrawHeadPatOverlay(inRect);
            }
            DrawBorderFlash(inRect);
        }

        /// <summary>
        /// 绘制摸头手部覆盖层
        /// ⭐ v1.14.1: 表情完全由 XML 配置的 HeadPatPhase.expression 控制
        /// 只在阶段变化时更新表情，避免每帧重复触发
        /// </summary>
        private void DrawHeadPatOverlay(Rect inRect)
        {
            // 通过 PersonaDefName 获取 RenderTreeDef
            var def = RenderTreeDefManager.GetRenderTree(panel.CurrentPersona?.defName);
            if (def == null || def.headPat == null || !def.headPat.enabled) return;

            var phase = def.GetHeadPatPhase(currentRubDuration);
            if (phase == null)
            {
                // 阶段为空时重置追踪状态
                lastHeadPatPhaseIndex = -1;
                return;
            }
            
            // 获取当前阶段索引
            int currentPhaseIndex = def.GetHeadPatPhaseIndex(currentRubDuration);
            
            // 绘制手部纹理（这部分每帧都需要执行）
            if (!string.IsNullOrEmpty(phase.textureName))
            {
                Texture2D handTex = ContentFinder<Texture2D>.Get(phase.textureName, false);
                if (handTex != null)
                {
                    Vector2 mousePos = Event.current.mousePosition;
                    float handSize = inRect.width * 0.4f;
                    Rect handRect = new Rect(mousePos.x - handSize / 2f, mousePos.y - handSize / 2f, handSize, handSize);
                    GUI.DrawTexture(handRect, handTex);
                }
            }
            
            // ⭐ v1.14.2: 只有当阶段变化时才更新表情
            // 表情类型和变体编号完全由 XML 配置的 HeadPatPhase 决定
            // 用户可在渲染树编辑器中自行配置 expression 和 variant
            if (currentPhaseIndex != lastHeadPatPhaseIndex)
            {
                lastHeadPatPhaseIndex = currentPhaseIndex;
                
                if (!string.IsNullOrEmpty(phase.expression))
                {
                    if (Enum.TryParse(phase.expression, true, out ExpressionType expr))
                    {
                        // ⭐ 根据配置的 variant 决定是否使用变体版本
                        // 使用超长持续时间 (99999)，直到 ResetInteractionState 强制恢复默认
                        if (phase.variant > 0)
                        {
                            // 使用用户配置的固定变体编号
                            ExpressionSystem.SetExpressionWithFixedVariant(
                                panel.GetPersonaResourceName(),
                                expr,
                                ExpressionTrigger.Manual,
                                durationTicks: 99999,
                                fixedVariant: phase.variant
                            );
                        }
                        else
                        {
                            // variant <= 0 时使用基础表情（无变体）
                            ExpressionSystem.SetExpression(
                                panel.GetPersonaResourceName(),
                                expr,
                                ExpressionTrigger.Manual,
                                durationTicks: 99999
                            );
                        }
                    }
                }

                // 播放音效 (带异常防护)
                if (!string.IsNullOrEmpty(phase.sound))
                {
                    try
                    {
                        SoundDef sound = DefDatabase<SoundDef>.GetNamed(phase.sound, false);
                        sound?.PlayOneShotOnCamera();
                    }
                    catch (Exception ex)
                    {
                        // 仅在开发模式下记录警告，避免干扰玩家
                        if (Prefs.DevMode)
                        {
                            Log.Warning($"[PortraitInteractionHandler] Failed to play sound '{phase.sound}': {ex.Message}");
                        }
                    }
                }
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
        
        /// <summary>
        /// 检测鼠标位置对应的交互区域
        /// 优先使用 RenderTreeDef 中配置的区域坐标（由多模态分析引擎提供）
        /// 如果没有配置则使用默认的硬编码逻辑
        /// </summary>
        private InteractionPhrases.InteractionZone GetInteractionZone(Vector2 mousePos)
        {
            Rect rect = panel.DrawRect;
            if (!rect.Contains(mousePos)) return InteractionPhrases.InteractionZone.None;
            
            // 计算归一化坐标 (左上角为原点, 0.0-1.0)
            float normalizedX = (mousePos.x - rect.x) / rect.width;
            float normalizedY = (mousePos.y - rect.y) / rect.height;
            
            // 尝试获取配置的交互区域
            var def = RenderTreeDefManager.GetRenderTree(panel.CurrentPersona?.defName);
            if (def?.interactionZones != null && def.interactionZones.HasCustomConfig)
            {
                // 优先检查头部区域（头部优先级高于身体）
                if (def.interactionZones.head != null && 
                    def.interactionZones.head.Contains(normalizedX, normalizedY))
                {
                    return InteractionPhrases.InteractionZone.Head;
                }
                
                // 检查身体区域
                if (def.interactionZones.body != null && 
                    def.interactionZones.body.Contains(normalizedX, normalizedY))
                {
                    return InteractionPhrases.InteractionZone.Body;
                }
                
                // 已配置但不在任何区域内
                return InteractionPhrases.InteractionZone.None;
            }
            
            // ⭐ 回退到默认硬编码逻辑（无配置时使用）
            // 头部：上部 25%
            if (normalizedY < 0.25f) 
            {
                return InteractionPhrases.InteractionZone.Head;
            }
            
            // 身体：中心区域 (X: 10%-90%, Y: 10%-90%)
            if (normalizedX > 0.1f && normalizedX < 0.9f && normalizedY > 0.1f && normalizedY < 0.9f)
            {
                return InteractionPhrases.InteractionZone.Body;
            }
            
            return InteractionPhrases.InteractionZone.None;
        }
        
        /// <summary>
        /// 根据好感度选择表情
        /// ⭐ 修复：移除随机性，使用确定性的表情选择
        /// </summary>
        private ExpressionType SelectExpressionByAffinity(float affinity, ExpressionType highPositive, ExpressionType lowPositive)
        {
            // ⭐ 使用确定性选择：好感度高时返回 highPositive，中等返回 lowPositive
            if (affinity >= TSSFrameworkConfig.Interaction.HighAffinityThreshold)
                return highPositive;  // 高好感度：使用主要正面表情
            if (affinity >= TSSFrameworkConfig.Interaction.LowAffinityThreshold)
                return lowPositive;   // 中等好感度：使用次要表情
            return ExpressionType.Angry;  // 低好感度：生气
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
