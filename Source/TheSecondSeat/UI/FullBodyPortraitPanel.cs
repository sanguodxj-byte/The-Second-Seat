using UnityEngine;
using Verse;
using RimWorld;
using TheSecondSeat.PersonaGeneration;
using TheSecondSeat.Narrator;
using TheSecondSeat.Settings;

namespace TheSecondSeat.UI
{
    /// <summary>
    /// ? v1.6.24: 全身立绘面板（画面左侧）
    /// ? v1.6.25: 新增触摸互动系统
    /// 功能：
    /// - 显示 1024x1574 全身立绘（使用分层系统）
    /// - 动态表情切换
    /// - 呼吸动画
    /// - 眨眼和张嘴动画
    /// - ? 触摸互动（悬停1秒激活，移动鼠标触发表情）
    /// </summary>
    [StaticConstructorOnStartup]
    public class FullBodyPortraitPanel : Window
    {
        private const float PORTRAIT_WIDTH = 1024f;
        private const float PORTRAIT_HEIGHT = 1574f;
        private const float SCALE_FACTOR = 0.35f;  // 缩放到35%（约358x551）
        
        private float displayWidth;
        private float displayHeight;
        
        private Texture2D? currentPortrait = null;
        private NarratorPersonaDef? currentPersona = null;
        private ExpressionType lastExpression = ExpressionType.Neutral;
        private int portraitUpdateTick = 0;
        private const int PORTRAIT_UPDATE_INTERVAL = 30;
        
        // ? v1.6.25: 触摸互动系统相关
        private float hoverStartTime = 0f;
        private bool isHovering = false;
        private bool isTouchModeActive = false;
        private const float HOVER_ACTIVATION_TIME = 1.0f;
        
        private Vector2 lastMousePosition = Vector2.zero;
        private float lastTouchTime = 0f;
        private int touchCount = 0;
        private const float TOUCH_COOLDOWN = 0.3f;
        
        private ExpressionType[] touchExpressions = new[] 
        {
            ExpressionType.Happy,
            ExpressionType.Surprised,
            ExpressionType.Smug,
            ExpressionType.Shy
        };

        // ? 边框闪烁动画相关
        private float borderFlashStartTime = 0f;
        private int borderFlashCount = 0;
        private const float SLOW_FLASH_DURATION = 1.0f;
        private const float FAST_FLASH_DURATION = 0.15f;
        private const float FAST_FLASH_INTERVAL = 0.05f;
        
        public FullBodyPortraitPanel()
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
            
            // 计算缩放后的尺寸
            displayWidth = PORTRAIT_WIDTH * SCALE_FACTOR;
            displayHeight = PORTRAIT_HEIGHT * SCALE_FACTOR;
        }

        public override Vector2 InitialSize => new Vector2(displayWidth, displayHeight);

        protected override void SetInitialSizeAndPosition()
        {
            // 固定在屏幕左侧，垂直居中
            float x = 10f;
            float y = (Verse.UI.screenHeight - displayHeight) / 2f;
            
            this.windowRect = new Rect(x, y, displayWidth, displayHeight);
        }

        public override void PreOpen()
        {
            base.PreOpen();
            this.doWindowBackground = false;
            this.drawShadow = false;
        }

        public override void DoWindowContents(Rect inRect)
        {
            // ? v1.6.25: 处理触摸互动
            HandleHoverAndTouch(inRect);
            
            // 更新立绘
            UpdatePortrait();
            
            if (currentPortrait != null && currentPersona != null)
            {
                // ? 应用呼吸动画偏移
                float breathingOffset = ExpressionSystem.GetBreathingOffset(currentPersona.defName);
                Rect drawRect = new Rect(inRect.x, inRect.y + breathingOffset, inRect.width, inRect.height);
                
                // 绘制立绘（保持纵横比，填充满）
                GUI.DrawTexture(drawRect, currentPortrait, ScaleMode.ScaleToFit);
            }
            else
            {
                // 占位符：绘制半透明背景
                Widgets.DrawBoxSolid(inRect, new Color(0.1f, 0.1f, 0.1f, 0.3f));
                
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = new Color(0.5f, 0.5f, 0.5f);
                Widgets.Label(inRect, "立绘加载中...");
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
            }
            
            // ? 悬停提示
            if (Mouse.IsOver(inRect) && currentPersona != null)
            {
                string tooltip = $"{currentPersona.narratorName}\n表情: {lastExpression}";
                
                // ? 触摸模式提示
                if (isTouchModeActive)
                {
                    tooltip += "\n\n? 触摸模式激活！移动鼠标与她互动";
                }
                else if (isHovering)
                {
                    float progress = (Time.realtimeSinceStartup - hoverStartTime) / HOVER_ACTIVATION_TIME;
                    tooltip += $"\n\n?? 悬停进度: {(progress * 100):F0}%";
                }
                else
                {
                    tooltip += "\n\n?? 悬停1秒激活触摸模式";
                }
                
                TooltipHandler.TipRegion(inRect, tooltip);
            }
            
            // ? 绘制悬停进度条
            if (isHovering && !isTouchModeActive)
            {
                float progress = (Time.realtimeSinceStartup - hoverStartTime) / HOVER_ACTIVATION_TIME;
                DrawHoverProgress(inRect, progress);
            }
            
            // ? 触摸模式指示器
            if (isTouchModeActive)
            {
                DrawTouchModeIndicator(inRect);
            }
            
            // ? 绘制边框闪烁效果
            DrawBorderFlash(inRect);
        }

        /// <summary>
        /// ? v1.6.25: 处理悬停和触摸互动
        /// </summary>
        private void HandleHoverAndTouch(Rect inRect)
        {
            if (currentPersona == null) return;
            
            bool mouseOver = Mouse.IsOver(inRect);
            
            if (mouseOver)
            {
                if (!isHovering)
                {
                    isHovering = true;
                    hoverStartTime = Time.realtimeSinceStartup;
                }
                else
                {
                    float hoverDuration = Time.realtimeSinceStartup - hoverStartTime;
                    
                    if (!isTouchModeActive && hoverDuration >= HOVER_ACTIVATION_TIME)
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
                if (isHovering || isTouchModeActive)
                {
                    DeactivateTouchMode();
                }
                
                isHovering = false;
            }
        }

        /// <summary>
        /// ? 激活触摸模式
        /// </summary>
        private void ActivateTouchMode()
        {
            if (currentPersona == null) return;
            
            isTouchModeActive = true;
            touchCount = 0;
            lastMousePosition = Event.current.mousePosition;
            
            TriggerExpression(ExpressionType.Confused, duration: 2f);
            StartBorderFlash(1);
            ShowFloatingText("(?ω?)?", new Color(0.8f, 0.9f, 1f));
            
            if (Prefs.DevMode)
            {
                Log.Message("[FullBodyPortraitPanel] 触摸模式激活");
            }
        }

        /// <summary>
        /// ? 取消触摸模式
        /// </summary>
        private void DeactivateTouchMode()
        {
            if (!isTouchModeActive) return;
            
            isTouchModeActive = false;
            touchCount = 0;
            
            RestoreDefaultExpression();
            
            if (Prefs.DevMode)
            {
                Log.Message("[FullBodyPortraitPanel] 触摸模式取消");
            }
        }

        /// <summary>
        /// ? 触摸移动事件
        /// </summary>
        private void OnTouchMove(Vector2 mousePos)
        {
            if (currentPersona == null) return;
            
            touchCount++;
            
            float moveSpeed = Vector2.Distance(mousePos, lastMousePosition) / Time.deltaTime;
            
            if (moveSpeed > 500f)
            {
                TriggerExpression(ExpressionType.Shy, duration: 1.5f);
                ShowFloatingText("(/ω＼)", new Color(1f, 0.6f, 0.6f));
            }
            else if (touchCount % 3 == 0)
            {
                var expression = touchExpressions[Random.Range(0, touchExpressions.Length)];
                TriggerExpression(expression, duration: 2f);
                
                string[] emojis = { "(?▽｀)", "(????)?", "(≧▽≦)", "ヾ(?°?°?)?", "(????)" };
                ShowFloatingText(emojis[Random.Range(0, emojis.Length)], new Color(1f, 0.8f, 0.9f));
            }
            
            if (touchCount >= 10)
            {
                OnTouchCombo();
                touchCount = 0;
            }
        }

        /// <summary>
        /// ? 连续触摸奖励
        /// </summary>
        private void OnTouchCombo()
        {
            if (currentPersona == null) return;
            
            bool isHappy = Random.value > 0.3f;
            TriggerExpression(isHappy ? ExpressionType.Happy : ExpressionType.Smug, duration: 3f);
            
            ShowFloatingText(isHappy ? "(*^▽^*)" : "(￣︶￣)↗", new Color(1f, 0.7f, 0.3f));
            StartBorderFlash(3);
            
            ModifyAffinity(5f, "全身立绘触摸互动");
            Messages.Message($"好感度 +5（全身立绘互动）", MessageTypeDefOf.PositiveEvent);
        }

        /// <summary>
        /// ? 触发表情变化
        /// </summary>
        private void TriggerExpression(ExpressionType expression, float duration = 2f)
        {
            if (currentPersona == null) return;
            
            ExpressionSystem.SetExpression(currentPersona.defName, expression, (int)(duration * 60), "立绘触摸互动");
            
            if (lastExpression != expression)
            {
                LayeredPortraitCompositor.ClearCache(currentPersona.defName, lastExpression);
            }
        }

        /// <summary>
        /// ? 恢复默认表情
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
        /// ? 显示浮动文字
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
                // 静默忽略
            }
        }

        /// <summary>
        /// ? 绘制悬停进度条
        /// </summary>
        private void DrawHoverProgress(Rect inRect, float progress)
        {
            progress = Mathf.Clamp01(progress);
            
            var progressBarRect = new Rect(inRect.x, inRect.yMax + 2f, inRect.width, 8f);
            Widgets.DrawBoxSolid(progressBarRect, new Color(0.2f, 0.2f, 0.2f, 0.6f));
            
            var fillRect = new Rect(progressBarRect.x, progressBarRect.y, progressBarRect.width * progress, progressBarRect.height);
            Color fillColor = Color.Lerp(new Color(0.3f, 0.8f, 1f), new Color(1f, 0.8f, 0.3f), progress);
            Widgets.DrawBoxSolid(fillRect, fillColor);
        }

        /// <summary>
        /// ? 绘制触摸模式指示器
        /// </summary>
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

        /// <summary>
        /// ? 启动边框闪烁动画
        /// </summary>
        private void StartBorderFlash(int count)
        {
            borderFlashStartTime = Time.realtimeSinceStartup;
            borderFlashCount = count;
        }

        /// <summary>
        /// ? 绘制边框闪烁效果
        /// </summary>
        private void DrawBorderFlash(Rect inRect)
        {
            if (borderFlashCount <= 0) return;
            
            float elapsed = Time.realtimeSinceStartup - borderFlashStartTime;
            float alpha = 0f;
            
            if (borderFlashCount == 1)
            {
                if (elapsed < SLOW_FLASH_DURATION)
                {
                    float progress = elapsed / SLOW_FLASH_DURATION;
                    alpha = Mathf.Sin(progress * Mathf.PI);
                }
                else
                {
                    borderFlashCount = 0;
                }
            }
            else if (borderFlashCount == 3)
            {
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

        /// <summary>
        /// ? 修改好感度
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
        /// ? 更新立绘（强制使用分层系统）
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
                        LayeredPortraitCompositor.ClearCache(currentPersona.defName, lastExpression);
                    }
                    
                    currentPersona = persona;
                    lastExpression = currentExpression;
                    
                    currentPortrait = LoadLayeredPortrait(persona, currentExpression);
                }
            }
            catch (System.Exception ex)
            {
                Log.Warning($"[FullBodyPortraitPanel] 更新立绘失败: {ex.Message}");
                currentPortrait = null;
            }
        }

        /// <summary>
        /// ? 加载分层立绘（强制使用分层系统）
        /// </summary>
        private Texture2D LoadLayeredPortrait(NarratorPersonaDef persona, ExpressionType expression)
        {
            try
            {
                if (!persona.useLayeredPortrait)
                {
                    persona.useLayeredPortrait = true;
                    
                    if (Prefs.DevMode)
                    {
                        Log.Message($"[FullBodyPortraitPanel] 强制启用分层立绘系统: {persona.defName}");
                    }
                }
                
                var config = persona.GetLayeredConfig();
                if (config == null)
                {
                    Log.Warning($"[FullBodyPortraitPanel] Layered config is null for {persona.defName}");
                    return GeneratePlaceholder(persona.primaryColor);
                }
                
                Texture2D composite = LayeredPortraitCompositor.CompositeLayers(
                    config, 
                    expression, 
                    "default"
                );
                
                if (composite == null)
                {
                    Log.Error($"[FullBodyPortraitPanel] Layered composite failed for {persona.defName}");
                    return GeneratePlaceholder(persona.primaryColor);
                }
                
                if (Prefs.DevMode)
                {
                    Log.Message($"[FullBodyPortraitPanel] ? Layered portrait loaded: {persona.defName} ({expression})");
                }
                
                return composite;
            }
            catch (System.Exception ex)
            {
                Log.Error($"[FullBodyPortraitPanel] Layered portrait loading failed: {ex}");
                return GeneratePlaceholder(persona.primaryColor);
            }
        }

        /// <summary>
        /// ? 生成占位符纹理
        /// </summary>
        private Texture2D GeneratePlaceholder(Color color)
        {
            int width = (int)PORTRAIT_WIDTH;
            int height = (int)PORTRAIT_HEIGHT;
            
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            
            Color darkColor = color * 0.3f;
            Color lightColor = color * 0.8f;
            
            for (int y = 0; y < height; y++)
            {
                float t = y / (float)height;
                Color gradientColor = Color.Lerp(darkColor, lightColor, t);
                
                for (int x = 0; x < width; x++)
                {
                    texture.SetPixel(x, y, gradientColor);
                }
            }
            
            texture.Apply();
            return texture;
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
