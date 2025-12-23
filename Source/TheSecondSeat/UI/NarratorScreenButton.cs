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
    /// - ✅ v1.6.24: 立绘模式下管理全身立绘面板
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
        private const float ButtonSize = 128f;  // ✅ 已经是 128，无需修改
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
        
        // ✅ v1.6.21: 上一次设置缓存（用于检测模式切换）
        private bool lastUsePortraitMode = false;
        
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

        // ✅ 边框闪烁动画相关
        private float borderFlashStartTime = 0f;
        private int borderFlashCount = 0;
        private const float SLOW_FLASH_DURATION = 1.0f;  // 慢速闪烁持续时间
        private const float FAST_FLASH_DURATION = 0.15f;  // 快速闪烁单次持续时间
        private const float FAST_FLASH_INTERVAL = 0.05f;  // 快速闪烁间隔

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
            
            // ✅ 触发"疑惑"表情（使用Confused类型）
            TriggerExpression(ExpressionType.Confused, duration: 2f);
            
            // ✅ 触发单次缓慢白色边框闪烁（不播放音效）
            StartBorderFlash(1);  // 闪烁1次
            
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
                // ✅ 移除音效
            }
            else if (touchCount % 3 == 0) // 每3次移动触发一次
            {
                // 随机开心表情
                var expression = touchExpressions[Random.Range(0, touchExpressions.Length)];
                TriggerExpression(expression, duration: 2f);
                
                string[] emojis = { "(´▽｀)", "(๑˃ᴗ˂)✧", "(≧▽≦)", "ヾ(◍°∇°◍)ﾉ" };
                ShowFloatingText(emojis[Random.Range(0, emojis.Length)], new Color(1f, 0.8f, 0.9f));
                // ✅ 移除音效
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
            
            // ✅ 触发快速闪烁3次（不播放音效）
            StartBorderFlash(3);
            
            // ✅ 增加好感度
            ModifyAffinity(3f, "触摸互动");
            
            // ✅ 显示好感度提示
            Messages.Message($"好感度 +3（触摸互动）", MessageTypeDefOf.PositiveEvent);
        }

        /// <summary>
        /// ✅ 启动边框闪烁动画
        /// </summary>
        /// <param name="count">闪烁次数（1=慢速单次，3=快速三次）</param>
        private void StartBorderFlash(int count)
        {
            borderFlashStartTime = Time.realtimeSinceStartup;
            borderFlashCount = count;
        }

        /// <summary>
        /// ✅ 绘制边框闪烁效果
        /// </summary>
        private void DrawBorderFlash(Rect inRect)
        {
            if (borderFlashCount <= 0) return;
            
            float elapsed = Time.realtimeSinceStartup - borderFlashStartTime;
            float alpha = 0f;
            
            if (borderFlashCount == 1)
            {
                // 慢速单次闪烁：1秒内从0→1→0
                if (elapsed < SLOW_FLASH_DURATION)
                {
                    float progress = elapsed / SLOW_FLASH_DURATION;
                    alpha = Mathf.Sin(progress * Mathf.PI);  // 平滑的sin曲线
                }
                else
                {
                    borderFlashCount = 0;  // 闪烁结束
                }
            }
            else if (borderFlashCount == 3)
            {
                // 快速三次闪烁：每次0.15秒，间隔0.05秒
                float cycleDuration = FAST_FLASH_DURATION + FAST_FLASH_INTERVAL;
                int currentCycle = Mathf.FloorToInt(elapsed / cycleDuration);
                
                if (currentCycle < 3)
                {
                    float cycleProgress = (elapsed % cycleDuration) / FAST_FLASH_DURATION;
                    
                    if (cycleProgress < 1.0f)
                    {
                        alpha = Mathf.Sin(cycleProgress * Mathf.PI);
                    }
                }
                else
                {
                    borderFlashCount = 0;  // 闪烁结束
                }
            }
            
            // 绘制白色边框（✅ 透明度从0.8降低到0.4，更低调）
            if (alpha > 0f)
            {
                GUI.color = new Color(1f, 1f, 1f, alpha * 0.4f);  // ✅ 从0.8f改为0.4f
                Widgets.DrawBox(inRect, 3);
                GUI.color = Color.white;
            }
        }

        public override void DoWindowContents(Rect inRect)
        {
            HandleDragging(inRect);
            HandleHoverAndTouch(inRect);
            
            NarratorButtonAnimator.UpdateAnimation();
            UpdateButtonState();
            
            // ✅ 更新动态头像
            UpdatePortrait();
            
            // ✅ 优先显示人格头像，否则显示状态图标
            Texture2D currentIcon = currentPortrait ?? GetCurrentIcon();
            
            if (currentIcon != null)
            {
                // ✅ 新增：如果是立绘/头像模式，应用呼吸偏移
                Rect drawRect = inRect;
                if (currentPortrait != null && currentPersona != null)
                {
                    float breathingOffset = ExpressionSystem.GetBreathingOffset(currentPersona.defName);
                    drawRect = new Rect(inRect.x, inRect.y + breathingOffset, inRect.width, inRect.height);
                }
                
                GUI.DrawTexture(drawRect, currentIcon, ScaleMode.ScaleToFit);
            }
            
            // 绘制指示灯
            Rect indicatorRect = new Rect(
                inRect.xMax - IndicatorSize - IndicatorOffset,
                inRect.y + IndicatorOffset,
                IndicatorSize,
                IndicatorSize
            );
            NarratorButtonAnimator.DrawIndicatorLight(indicatorRect, currentState);
            
            // ✅ 悬停效果（增强版：触摸模式下不显示提示框）
            // ✅ v1.6.52: 修复 - 触摸模式激活后不显示悬停提示
            // ✅ v1.6.65: 隐藏触摸模式下的鼠标悬浮提示
            if (Mouse.IsOver(inRect) && !isDragging && !isTouchModeActive)
            {
                GUI.color = new Color(1f, 1f, 1f, 0.3f);
                Widgets.DrawBox(inRect, 2);
                GUI.color = Color.white;
                
                // ✅ 完全隐藏触摸模式提示
                string tooltip = GetStateTooltip();
                tooltip += "\n\nShift+左键拖动 | 左键打开窗口 | 右键快速对话";
                // 移除触摸模式提示
                
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
            
            // ✅ 绘制边框闪烁效果（最后绘制，覆盖在最上层）
            DrawBorderFlash(inRect);
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

        /// <summary>
        /// ✅ 更新动态头像（支持表情系统和立绘模式）
        /// ✅ v1.6.21: 检测设置变化，自动切换头像/立绘模式
        /// ✅ v1.6.24: AI按钮始终显示头像（512x512），立绘由独立面板显示
        /// </summary>
        private void UpdatePortrait()
        {
            if (Find.TickManager.TicksGame - portraitUpdateTick < PORTRAIT_UPDATE_INTERVAL)
            {
                return;
            }
            
            portraitUpdateTick = Find.TickManager.TicksGame;
            
            // ✅ v1.6.21: 获取设置并检测变化
            var modSettings = LoadedModManager.GetMod<TheSecondSeatMod>()?.GetSettings<TheSecondSeatSettings>();
            bool currentPortraitMode = modSettings?.usePortraitMode ?? false;
            
            // ✅ v1.6.21: 检测模式切换
            if (currentPortraitMode != lastUsePortraitMode)
            {
                AvatarLoader.ClearAllCache();
                PortraitLoader.ClearAllCache();
                try { LayeredPortraitCompositor.ClearAllCache(); } catch { }
                
                lastUsePortraitMode = currentPortraitMode;
                currentPortrait = null;
                currentPersona = null;
                
                // ✅ 移除模式切换日志
                // if (Prefs.DevMode)
                // {
                //     Log.Message($"[NarratorScreenButton] Portrait mode changed to: {(currentPortraitMode ? "立绘模式" : "头像模式")}");
                // }
            }
            
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
                    
                    // ✅ v1.6.24: AI按钮始终使用头像模式（512x512）
                    // 立绘由 FullBodyPortraitPanel 独立显示
                    currentPortrait = AvatarLoader.LoadAvatar(persona, currentExpression);
                }
            }
            catch (System.Exception ex)
            {
                Log.Warning($"[NarratorScreenButton] 更新头像失败: {ex.Message}");
                currentPortrait = null;
            }
        }

        public override void WindowUpdate()
        {
            base.WindowUpdate();
            
            if (Current.ProgramState != ProgramState.Playing)
            {
                this.Close();
            }
            
            // ✅ v1.6.36: 每帧更新张嘴动画系统（TTS口型同步）
            float deltaTime = Time.deltaTime;
            MouthAnimationSystem.Update(deltaTime);
            
            // ✅ v1.6.42: 管理全身立绘面板（通过 PortraitOverlaySystem）
            ManageFullBodyPortraitPanel();
        }

        /// <summary>
        /// ✅ v1.6.42: 管理全身立绘面板（通过 PortraitOverlaySystem）
        /// </summary>
        private void ManageFullBodyPortraitPanel()
        {
            var modSettings = LoadedModManager.GetMod<Settings.TheSecondSeatMod>()?.GetSettings<Settings.TheSecondSeatSettings>();
            bool shouldShowFullBodyPortrait = modSettings?.usePortraitMode ?? false;
            
            // ✅ 使用 PortraitOverlaySystem 控制立绘显示
            PortraitOverlaySystem.Toggle(shouldShowFullBodyPortrait);
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
        /// ✅ v1.6.40: 恢复默认表情（基于好感度的待机表情系统）
        /// 根据 agent.affinity 决定待机表情：
        /// - 高好感 (> 80): Shy（暗恋/脸红）
        /// - 良好 (> 40): Happy（微笑）
        /// - 中立 (-20 to 40): Neutral（平静）
        /// - 不佳 (-60 to -20): Sad（失望/冷淡）
        /// - 敌对 (< -60): Angry（憎恨）
        /// 
        /// ✅ 心情覆盖：如果 currentMood 是负面状态，强制使用 Sad 表情
        /// ✅ 无限持续：duration = 99999f，表情保持到下次互动事件
        /// </summary>
        private void RestoreDefaultExpression()
        {
            if (currentPersona == null) return;
            
            var agent = Current.Game?.GetComponent<Storyteller.StorytellerAgent>();
            if (agent != null)
            {
                float affinity = agent.GetAffinity();
                var mood = agent.currentMood;
                
                ExpressionType defaultExpression;
                
                // ✅ 心情覆盖：心情极差时强制显示悲伤表情
                if (mood == Storyteller.MoodState.Melancholic || 
                    mood == Storyteller.MoodState.Angry)
                {
                    defaultExpression = ExpressionType.Sad;
                }
                else
                {
                    // ✅ 基于好感度的待机表情
                    defaultExpression = affinity switch
                    {
                        > 80f => ExpressionType.Shy,      // 高好感：害羞/暗恋
                        > 40f => ExpressionType.Happy,    // 良好：开心/微笑
                        > -20f => ExpressionType.Neutral, // 中立：平静
                        > -60f => ExpressionType.Sad,     // 不佳：失望/冷淡
                        _ => ExpressionType.Angry         // 敌对：憎恨
                    };
                }
                
                // ✅ 关键：无限持续时间 (99999f)，直到下次互动事件
                TriggerExpression(defaultExpression, duration: 99999f);
                
                if (Prefs.DevMode)
                {
                    Log.Message($"[NarratorScreenButton] 恢复待机表情: {defaultExpression} (Affinity={affinity:F1}, Mood={mood})");
                }
            }
            else
            {
                // ✅ 无 agent 时，使用中立表情（无限持续）
                TriggerExpression(ExpressionType.Neutral, duration: 99999f);
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
    }
}
