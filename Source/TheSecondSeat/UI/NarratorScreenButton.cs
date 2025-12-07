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
        private const float IndicatorSize = 16f;  // 8f → 16f（按比例放大）
        private const float IndicatorOffset = 6f; // 3f → 6f（按比例放大）
        
        // 当前状态
        private NarratorButtonState currentState = NarratorButtonState.Ready;
        
        // ✅ 动态头像相关
        private Texture2D? currentPortrait = null;
        private NarratorPersonaDef? currentPersona = null;
        private ExpressionType lastExpression = ExpressionType.Neutral;
        private int portraitUpdateTick = 0;
        private const int PORTRAIT_UPDATE_INTERVAL = 60; // 每秒更新一次
        
        // 拖动相关
        private bool isDragging = false;
        private Vector2 dragOffset = Vector2.zero;
        private static Vector2 savedPosition = Vector2.zero;
        private static bool hasLoadedPosition = false;

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
                float x = MarginFromEdge;  // 左上角
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
            
            // 悬停效果
            if (Mouse.IsOver(inRect) && !isDragging)
            {
                GUI.color = new Color(1f, 1f, 1f, 0.3f);
                Widgets.DrawBox(inRect, 2);
                GUI.color = Color.white;
                
                string tooltip = GetStateTooltip() + "\n\nShift+左键拖动 | 左键打开窗口 | 右键快速对话";
                TooltipHandler.TipRegion(inRect, tooltip);
            }
            
            // 拖动状态提示
            if (isDragging)
            {
                GUI.color = new Color(0.2f, 0.8f, 1f, 0.6f);
                Widgets.DrawBox(inRect, 3);
                GUI.color = Color.white;
            }
        }

        /// <summary>
        /// ✅ 更新动态头像（支持表情系统）
        /// </summary>
        private void UpdatePortrait()
        {
            // ✅ 修复：降低更新间隔到 30 ticks（0.5秒），提高响应速度
            if (Find.TickManager.TicksGame - portraitUpdateTick < 30)
            {
                return;
            }
            
            portraitUpdateTick = Find.TickManager.TicksGame;
            
            try
            {
                // 获取当前人格
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
                
                // ✅ 获取当前表情状态
                var expressionState = ExpressionSystem.GetExpressionState(persona.defName);
                ExpressionType currentExpression = expressionState.CurrentExpression;
                
                // ✅ 修复：强制刷新逻辑 - 如果表情变化，立即清除缓存并重新加载
                if (persona != currentPersona || currentExpression != lastExpression)
                {
                    // ✅ 清除旧的头像缓存（如果表情变化了）
                    if (currentPersona != null && lastExpression != currentExpression)
                    {
                        AvatarLoader.ClearAvatarCache(currentPersona.defName, lastExpression);
                        PortraitLoader.ClearPortraitCache(currentPersona.defName, lastExpression);
                    }
                    
                    currentPersona = persona;
                    lastExpression = currentExpression;
                    
                    // ✅ 使用AvatarLoader加载UI按钮专用头像
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
                    // ✅ 修复：dragOffset 应该是鼠标在窗口内的相对位置，而不是绝对位置差
                    dragOffset = Event.current.mousePosition;
                    currentEvent.Use();
                }
                // 普通左键 = 打开窗口
                else if (!isDragging)
                {
                    ToggleNarratorWindow();
                    currentEvent.Use();
                }
            }
            // ✅ 右键 = 快速对话
            else if (currentEvent.type == EventType.MouseDown && currentEvent.button == 1 && Mouse.IsOver(inRect))
            {
                OpenQuickDialogue();
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
                // ✅ 修复：使用 GUIUtility.GUIToScreenPoint 获取屏幕坐标
                Vector2 mousePos = GUIUtility.GUIToScreenPoint(Event.current.mousePosition);
                
                // 计算新位置（鼠标位置 - 拖动偏移）
                Vector2 newPos = mousePos - dragOffset;
                
                // 限制在屏幕范围内
                newPos.x = Mathf.Clamp(newPos.x, 0, Verse.UI.screenWidth - ButtonSize);
                newPos.y = Mathf.Clamp(newPos.y, 0, Verse.UI.screenHeight - ButtonSize);
                
                windowRect.x = newPos.x;
                windowRect.y = newPos.y;
                
                currentEvent.Use();
            }
        }

        /// <summary>
        /// ✅ 打开快速对话窗口
        /// </summary>
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
            
            // ✅ 添加当前人格和表情信息
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
