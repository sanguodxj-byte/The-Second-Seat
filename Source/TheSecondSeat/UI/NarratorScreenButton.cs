using UnityEngine;
using Verse;
using RimWorld;
using Verse.Sound;
using TheSecondSeat.Core;
using TheSecondSeat.Settings;
using TheSecondSeat.PersonaGeneration;
using TheSecondSeat.Narrator;

namespace TheSecondSeat.UI
{
    /// <summary>
    /// 在主游戏界面显示 AI 叙事者按钮（屏幕 UI）
    /// ✅ 新增功能：
    /// - 动态显示当前人格头像
    /// - 支持表情系统（根据好感度/事件自动更新）
    /// - 右键快速对话输入框
    /// - 支持拖动改变位置
    /// - ✅ 多段式悬停触摸互动（悬停1秒激活，移动鼠标触发表情）
    /// </summary>
    [StaticConstructorOnStartup]
    public class NarratorScreenButton : Window
    {
        private static NarratorWindow? narratorWindow;
        private static Texture2D? iconReady;
        private static Texture2D? iconProcessing;
        private static Texture2D? iconError;
        private static Texture2D? iconDisabled;
        
        // ✅ 按钮大小调整：64x64 → 128x128
        private const float ButtonSize = 128f;
        private const float MarginFromEdge = 10f;
        private const float IndicatorSize = 16f;
        private const float IndicatorOffset = 6f;
        
        // 当前状态
        private NarratorButtonState currentState = NarratorButtonState.Ready;
        
        // ✅ 动态头像相关
        private Texture2D? currentPortrait = null;
        private NarratorPersonaDef? currentPersona = null;
        private ExpressionType lastExpression = ExpressionType.Neutral;
        private int portraitUpdateTick = 0;
        private const int PORTRAIT_UPDATE_INTERVAL = 30;
        
        // 拖动相关
        private bool isDragging = false;
        private Vector2 dragOffset = Vector2.zero;
        private static Vector2 savedPosition = Vector2.zero;
        private static bool hasLoadedPosition = false;

        // ✅ 多段式悬停触摸互动相关
        private float hoverStartTime = 0f;
        private bool isHovering = false;
        private bool isTouchModeActive = false;
        private const float HOVER_ACTIVATION_TIME = 1.0f; // 悬停1秒激活触摸模式
        
        private Vector2 lastMousePosition = Vector2.zero;
        private float lastTouchTime = 0f;
        private int touchCount = 0;
        private const float TOUCH_COOLDOWN = 0.3f; // 触摸冷却时间
        
        private ExpressionType[] touchExpressions = new[] 
        {
            ExpressionType.Happy,
            ExpressionType.Surprised,
            ExpressionType.Smug
        };

        public NarratorScreenButton()
        {
            this.doCloseX = false;
            this.doCloseButton = false;
            this.closeOnClickedOutside = false;
            this.closeOnCancel = false;
            this.preventCameraMotion = false;
            this.draggable = false;
            this.resizeable = false;
            this.focusWhenOpened = false;
            this.drawShadow = false;
            this.absorbInputAroundWindow = false;
            this.layer = WindowLayer.SubSuper;
            this.doWindowBackground = false;
            this.preventDrawTutor = true;
            
            LoadIcons();
            LoadSavedPosition();
        }

        private static void LoadIcons()
        {
            iconReady = TryLoadTexture("UI/NarratorButton_Ready", "UI/NarratorButton", "UI/Commands/Attack");
            iconProcessing = TryLoadTexture("UI/NarratorButton_Processing", "UI/NarratorButton_Ready", "UI/Commands/Attack");
            iconError = TryLoadTexture("UI/NarratorButton_Error", "UI/NarratorButton_Ready", "UI/Commands/Attack");
            iconDisabled = TryLoadTexture("UI/NarratorButton_Disabled", "UI/NarratorButton_Ready", "UI/Commands/Attack");
        }
        
        private static Texture2D? TryLoadTexture(params string[] paths)
        {
            foreach (var path in paths)
            {
                var texture = ContentFinder<Texture2D>.Get(path, false);
                if (texture != null)
                {
                    return texture;
                }
            }
            return TexButton.Info;
        }

        public override Vector2 InitialSize => new Vector2(ButtonSize, ButtonSize);

        protected override void SetInitialSizeAndPosition()
        {
            if (hasLoadedPosition && savedPosition != Vector2.zero)
            {
                this.windowRect = new Rect(savedPosition.x, savedPosition.y, ButtonSize, ButtonSize);
            }
            else
            {
                // ✅ 修改：默认位置改为左上角（原来是右上角）
                float x = MarginFromEdge;
                float y = MarginFromEdge;
                this.windowRect = new Rect(x, y, ButtonSize, ButtonSize);
                savedPosition = new Vector2(x, y);
            }
        }

        public override void PreOpen()
        {
            base.PreOpen();
            this.doWindowBackground = false;
            this.drawShadow = false;
        }

        public override void DoWindowContents(Rect inRect)
        {
            HandleDragging(inRect);
            HandleHoverAndTouch(inRect); // ✅ 新增：处理悬停和触摸
            
            NarratorButtonAnimator.UpdateAnimation();
            UpdateButtonState();
            
            // ✅ 更新动态头像
            UpdatePortrait();
            
            // ✅ 优先显示人格头像，否则显示状态图标
            Texture2D currentIcon = currentPortrait ?? GetCurrentIcon();
            
            if (currentIcon != null)
            {
                GUI.DrawTexture(inRect, currentIcon, ScaleMode.ScaleToFit);
            }
            
            // 绘制指示灯
            Rect indicatorRect = new Rect(
                inRect.xMax - IndicatorSize - IndicatorOffset,
                inRect.y + IndicatorOffset,
                IndicatorSize,
                IndicatorSize
            );
            NarratorButtonAnimator.DrawIndicatorLight(indicatorRect, currentState);
            
            // ✅ 悬停效果（增强版：显示触摸模式提示）
            if (Mouse.IsOver(inRect) && !isDragging)
            {
                GUI.color = new Color(1f, 1f, 1f, 0.3f);
                Widgets.DrawBox(inRect, 2);
                GUI.color = Color.white;
                
                string tooltip = GetStateTooltip();
                
                // ✅ 根据触摸模式状态显示不同提示
                if (isTouchModeActive)
                {
                    tooltip += "\n\n✨ 触摸模式激活！移动鼠标进行互动";
                }
                else
                {
                    tooltip += "\n\nShift+左键拖动 | 左键打开窗口 | 右键快速对话";
                    tooltip += "\n💡 悬停1秒激活触摸模式";
                }
                
                TooltipHandler.TipRegion(inRect, tooltip);
            }
            
            // 拖动状态提示
            if (isDragging)
            {
                GUI.color = new Color(0.2f, 0.8f, 1f, 0.6f);
                Widgets.DrawBox(inRect, 3);
                GUI.color = Color.white;
            }
            
            // ✅ 绘制悬停进度条
            if (isHovering && !isTouchModeActive && !isDragging)
            {
                float progress = (Time.realtimeSinceStartup - hoverStartTime) / HOVER_ACTIVATION_TIME;
                DrawHoverProgress(inRect, progress);
            }
            
            // ✅ 触摸模式指示器
            if (isTouchModeActive)
            {
                DrawTouchModeIndicator(inRect);
            }
        }

        /// <summary>
        /// ✅ 处理悬停和触摸互动
        /// </summary>
        private void HandleHoverAndTouch(Rect inRect)
        {
            if (currentPersona == null) return;
            
            bool mouseOver = Mouse.IsOver(inRect);
            
            // ✅ 悬停检测（不在拖动状态下）
            if (mouseOver && !isDragging)
            {
                if (!isHovering)
                {
                    // 开始悬停
                    isHovering = true;
                    hoverStartTime = Time.realtimeSinceStartup;
                }
                else
                {
                    // 检查是否达到激活时间
                    float hoverDuration = Time.realtimeSinceStartup - hoverStartTime;
                    
                    if (!isTouchModeActive && hoverDuration >= HOVER_ACTIVATION_TIME)
                    {
                        // 激活触摸模式
                        ActivateTouchMode();
                    }
                }
                
                // ✅ 触摸模式下的鼠标移动检测
                if (isTouchModeActive)
                {
                    Vector2 currentMousePos = Event.current.mousePosition;
                    
                    // 检测鼠标移动
                    if (Vector2.Distance(currentMousePos, lastMousePosition) > 5f) // 移动超过5像素
                    {
                        float currentTime = Time.realtimeSinceStartup;
                        
                        if (currentTime - lastTouchTime > TOUCH_COOLDOWN)
                        {
                            OnTouchMove(currentMousePos);
                            lastTouchTime = currentTime;
                        }
                    }
                    
                    lastMousePosition = currentMousePos;
                }
            }
            else
            {
                // 鼠标离开头像
                if (isHovering || isTouchModeActive)
                {
                    DeactivateTouchMode();
                }
                
                isHovering = false;
            }
        }

        /// <summary>
        /// ✅ 激活触摸模式
        /// </summary>
        private void ActivateTouchMode()
        {
            if (currentPersona == null) return;
            
            isTouchModeActive = true;
            touchCount = 0;
            lastMousePosition = Event.current.mousePosition;
            
            // ✅ 触发"疑惑"表情
            TriggerExpression(ExpressionType.Confused, duration: 2f);
            
            // ✅ 播放激活音效
            SoundDefOf.Quest_Accepted.PlayOneShotOnCamera(null);
            
            // ✅ 显示浮动提示
            ShowFloatingText("(・ω・)?", new Color(0.8f, 0.9f, 1f));
        }

        /// <summary>
        /// ✅ 取消触摸模式
        /// </summary>
        private void DeactivateTouchMode()
        {
            if (!isTouchModeActive) return;
            
            isTouchModeActive = false;
            touchCount = 0;
            
            // ✅ 恢复默认表情（使用好感度决定）
            RestoreDefaultExpression();
        }

        /// <summary>
        /// ✅ 触摸移动事件（鼠标在头像上移动）
        /// </summary>
        private void OnTouchMove(Vector2 mousePos)
        {
            if (currentPersona == null) return;
            
            touchCount++;
            
            // ✅ 计算移动速度（用于判断是否快速移动）
            float moveSpeed = Vector2.Distance(mousePos, lastMousePosition) / Time.deltaTime;
            
            if (moveSpeed > 500f) // 快速移动
            {
                // 害羞表情
                TriggerExpression(ExpressionType.Shy, duration: 1.5f);
                ShowFloatingText("(/ω＼)", new Color(1f, 0.6f, 0.6f));
                SoundDefOf.Click.PlayOneShotOnCamera(null);
            }
            else if (touchCount % 3 == 0) // 每3次移动触发一次
            {
                // 随机开心表情
                var expression = touchExpressions[Random.Range(0, touchExpressions.Length)];
                TriggerExpression(expression, duration: 2f);
                
                string[] emojis = { "(´▽｀)", "(๑˃ᴗ˂)✧", "(≧▽≦)", "ヾ(◍°∇°◍)ﾉ" };
                ShowFloatingText(emojis[Random.Range(0, emojis.Length)], new Color(1f, 0.8f, 0.9f));
                SoundDefOf.Click.PlayOneShotOnCamera(null);
            }
            
            // ✅ 连续触摸奖励
            if (touchCount >= 10)
            {
                OnTouchCombo();
                touchCount = 0;
            }
        }

        /// <summary>
        /// ✅ 连续触摸奖励（10次移动后触发）
        /// </summary>
        private void OnTouchCombo()
        {
            if (currentPersona == null) return;
            
            // ✅ 触发特殊表情
            bool isHappy = Random.value > 0.3f;
            TriggerExpression(isHappy ? ExpressionType.Happy : ExpressionType.Smug, duration: 3f);
            
            ShowFloatingText(isHappy ? "(*^▽^*)" : "(￣︶￣)↗", new Color(1f, 0.7f, 0.3f));
            SoundDefOf.Quest_Concluded.PlayOneShotOnCamera(null);
            
            // ✅ 增加好感度
            ModifyAffinity(3f, "触摸互动");
            
            // ✅ 显示好感度提示
            Messages.Message($"好感度 +3（触摸互动）", MessageTypeDefOf.PositiveEvent);
        }

        /// <summary>
        /// ✅ 触发表情变化
        /// </summary>
        private void TriggerExpression(ExpressionType expression, float duration = 2f)
        {
            if (currentPersona == null) return;
            
            ExpressionSystem.SetExpression(currentPersona.defName, expression, (int)(duration * 60), "触摸互动");
            
            // ✅ 清除头像缓存，强制刷新
            if (lastExpression != expression)
            {
                AvatarLoader.ClearAvatarCache(currentPersona.defName, lastExpression);
                PortraitLoader.ClearPortraitCache(currentPersona.defName, lastExpression);
            }
        }

        /// <summary>
        /// ✅ 恢复默认表情（根据好感度）
        /// </summary>
        private void RestoreDefaultExpression()
        {
            if (currentPersona == null) return;
            
            var agent = Current.Game?.GetComponent<Storyteller.StorytellerAgent>();
            if (agent != null)
            {
                float affinity = agent.GetAffinity();
                
                ExpressionType defaultExpression = affinity switch
                {
                    >= 80 => ExpressionType.Happy,
                    >= 60 => ExpressionType.Neutral,
                    >= 40 => ExpressionType.Neutral,
                    >= 20 => ExpressionType.Sad,
                    _ => ExpressionType.Angry
                };
                
                TriggerExpression(defaultExpression, duration: 3f);
            }
            else
            {
                TriggerExpression(ExpressionType.Neutral, duration: 3f);
            }
        }

        /// <summary>
        /// ✅ 显示浮动文字（表情符号）
        /// </summary>
        private void ShowFloatingText(string text, Color color)
        {
            try
            {
                var pos = new Vector3(windowRect.center.x, windowRect.y - 30f, 0f);
                MoteMaker.ThrowText(pos.ToIntVec3().ToVector3Shifted(), Find.CurrentMap, text, color, 2f);
            }
            catch
            {
                // 静默忽略（可能在没有地图时调用）
            }
        }

        /// <summary>
        /// ✅ 绘制悬停进度条
        /// </summary>
        private void DrawHoverProgress(Rect inRect, float progress)
        {
            progress = Mathf.Clamp01(progress);
            
            // 进度条背景（头像底部）
            var progressBarRect = new Rect(inRect.x, inRect.yMax + 2f, inRect.width, 6f);
            Widgets.DrawBoxSolid(progressBarRect, new Color(0.2f, 0.2f, 0.2f, 0.6f));
            
            // 进度条填充
            var fillRect = new Rect(progressBarRect.x, progressBarRect.y, progressBarRect.width * progress, progressBarRect.height);
            Color fillColor = Color.Lerp(new Color(0.3f, 0.8f, 1f), new Color(1f, 0.8f, 0.3f), progress);
            Widgets.DrawBoxSolid(fillRect, fillColor);
        }

        /// <summary>
        /// ✅ 绘制触摸模式指示器
        /// </summary>
        private void DrawTouchModeIndicator(Rect inRect)
        {
            // ✅ 绘制闪烁边框
            float alpha = 0.5f + 0.5f * Mathf.Sin(Time.realtimeSinceStartup * 3f);
            GUI.color = new Color(1f, 0.8f, 0.3f, alpha);
            Widgets.DrawBox(inRect, 3);
            GUI.color = Color.white;
            
            // ✅ 触摸计数显示（可选）
            if (touchCount > 0)
            {
                var countRect = new Rect(inRect.xMax - 30f, inRect.yMax - 30f, 25f, 20f);
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = new Color(1f, 1f, 1f, 0.8f);
                Widgets.Label(countRect, $"×{touchCount}");
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
            }
        }

        /// <summary>
        /// ✅ 修改好感度
        /// </summary>
        private void ModifyAffinity(float delta, string reason)
        {
            var agent = Current.Game?.GetComponent<Storyteller.StorytellerAgent>();
            if (agent != null)
            {
                agent.ModifyAffinity(delta, reason);
            }
        }

        /// <summary>
        /// ✅ 更新动态头像（支持表情系统）
        /// </summary>
        private void UpdatePortrait()
        {
            if (Find.TickManager.TicksGame - portraitUpdateTick < PORTRAIT_UPDATE_INTERVAL)
            {
                return;
            }
            
            portraitUpdateTick = Find.TickManager.TicksGame;
            
            try
            {
                var manager = Current.Game?.GetComponent<NarratorManager>();
                if (manager == null)
                {
                    currentPortrait = null;
                    return;
                }
                
                var persona = manager.GetCurrentPersona();
                if (persona == null)
                {
                    currentPortrait = null;
                    return;
                }
                
                var expressionState = ExpressionSystem.GetExpressionState(persona.defName);
                ExpressionType currentExpression = expressionState.CurrentExpression;
                
                if (persona != currentPersona || currentExpression != lastExpression)
                {
                    if (currentPersona != null && lastExpression != currentExpression)
                    {
                        AvatarLoader.ClearAvatarCache(currentPersona.defName, lastExpression);
                        PortraitLoader.ClearPortraitCache(currentPersona.defName, lastExpression);
                    }
                    
                    currentPersona = persona;
                    lastExpression = currentExpression;
                    currentPortrait = AvatarLoader.LoadAvatar(persona, currentExpression);
                }
            }
            catch (System.Exception ex)
            {
                Log.Warning($"[NarratorScreenButton] 更新头像失败: {ex.Message}");
                currentPortrait = null;
            }
        }

        private void HandleDragging(Rect inRect)
        {
            Event currentEvent = Event.current;
            
            if (currentEvent.type == EventType.MouseDown && currentEvent.button == 0 && Mouse.IsOver(inRect))
            {
                // Shift + 左键 = 拖动
                if (currentEvent.shift)
                {
                    isDragging = true;
                    dragOffset = Event.current.mousePosition;
                    DeactivateTouchMode(); // 拖动时取消触摸模式
                    currentEvent.Use();
                }
                // 普通左键 = 打开窗口（只在非触摸模式下）
                else if (!isDragging && !isTouchModeActive)
                {
                    ToggleNarratorWindow();
                    currentEvent.Use();
                }
            }
            // ✅ 右键 = 快速对话
            else if (currentEvent.type == EventType.MouseDown && currentEvent.button == 1 && Mouse.IsOver(inRect))
            {
                OpenQuickDialogue();
                DeactivateTouchMode(); // 右键时取消触摸模式
                currentEvent.Use();
            }
            else if (currentEvent.type == EventType.MouseUp && isDragging)
            {
                isDragging = false;
                SavePosition();
                currentEvent.Use();
            }
            else if (currentEvent.type == EventType.MouseDrag && isDragging)
            {
                Vector2 mousePos = GUIUtility.GUIToScreenPoint(Event.current.mousePosition);
                Vector2 newPos = mousePos - dragOffset;
                
                newPos.x = Mathf.Clamp(newPos.x, 0, Verse.UI.screenWidth - ButtonSize);
                newPos.y = Mathf.Clamp(newPos.y, 0, Verse.UI.screenHeight - ButtonSize);
                
                windowRect.x = newPos.x;
                windowRect.y = newPos.y;
                
                currentEvent.Use();
            }
        }

        private void OpenQuickDialogue()
        {
            if (currentState == NarratorButtonState.Disabled) return;
            
            var quickWindow = new QuickDialogueWindow();
            Find.WindowStack.Add(quickWindow);
            SoundStarter.PlayOneShotOnCamera(SoundDefOf.Tick_High, null);
        }

        private void SavePosition()
        {
            savedPosition = new Vector2(windowRect.x, windowRect.y);
            
            PlayerPrefs.SetFloat("TheSecondSeat_ButtonX", windowRect.x);
            PlayerPrefs.SetFloat("TheSecondSeat_ButtonY", windowRect.y);
            PlayerPrefs.Save();
            
            Log.Message($"[The Second Seat] Button position saved: ({windowRect.x:F0}, {windowRect.y:F0})");
        }

        private void LoadSavedPosition()
        {
            if (hasLoadedPosition) return;
            
            if (PlayerPrefs.HasKey("TheSecondSeat_ButtonX") && PlayerPrefs.HasKey("TheSecondSeat_ButtonY"))
            {
                float x = PlayerPrefs.GetFloat("TheSecondSeat_ButtonX");
                float y = PlayerPrefs.GetFloat("TheSecondSeat_ButtonY");
                
                if (x > 0 && y > 0)
                {
                    savedPosition = new Vector2(x, y);
                    hasLoadedPosition = true;
                    Log.Message($"[The Second Seat] Button position loaded: ({savedPosition.x:F0}, {savedPosition.y:F0})");
                }
            }
        }

        private void UpdateButtonState()
        {
            var controller = Current.Game?.GetComponent<NarratorController>();
            
            if (controller != null && !string.IsNullOrEmpty(controller.LastError))
            {
                currentState = NarratorButtonState.Error;
                return;
            }
            
            if (controller?.IsProcessing ?? false)
            {
                currentState = NarratorButtonState.Processing;
                return;
            }
            
            currentState = NarratorButtonState.Ready;
        }

        private Texture2D GetCurrentIcon()
        {
            return currentState switch
            {
                NarratorButtonState.Ready => iconReady ?? TexButton.Info,
                NarratorButtonState.Processing => iconProcessing ?? iconReady ?? TexButton.Info,
                NarratorButtonState.Error => iconError ?? iconReady ?? TexButton.Info,
                NarratorButtonState.Disabled => iconDisabled ?? iconReady ?? TexButton.Info,
                _ => iconReady ?? TexButton.Info
            };
        }

        private string GetStateTooltip()
        {
            string baseTooltip = currentState switch
            {
                NarratorButtonState.Ready => "TSS_ButtonState_Ready".Translate(),
                NarratorButtonState.Processing => "TSS_ButtonState_Processing".Translate(),
                NarratorButtonState.Error => "TSS_ButtonState_Error".Translate(),
                NarratorButtonState.Disabled => "TSS_ButtonState_Disabled".Translate(),
                _ => "TSS_NarratorButton_Tooltip".Translate()
            };
            
            if (currentPersona != null)
            {
                baseTooltip = $"{currentPersona.narratorName} ({lastExpression})\n{baseTooltip}";
            }
            
            return baseTooltip;
        }

        private void ToggleNarratorWindow()
        {
            if (currentState == NarratorButtonState.Disabled) return;
            
            if (narratorWindow == null || !Find.WindowStack.IsOpen(narratorWindow))
            {
                narratorWindow = new NarratorWindow();
                Find.WindowStack.Add(narratorWindow);
                SoundStarter.PlayOneShotOnCamera(SoundDefOf.Click, null);
            }
            else
            {
                Find.WindowStack.TryRemove(narratorWindow);
                narratorWindow = null;
                SoundStarter.PlayOneShotOnCamera(SoundDefOf.Click, null);
            }
        }

        public override void WindowUpdate()
        {
            base.WindowUpdate();
            
            if (Current.ProgramState != ProgramState.Playing)
            {
                this.Close();
            }
        }
    }
}
