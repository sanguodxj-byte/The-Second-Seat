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
    /// ? v1.6.40: 新增区域交互系统（头部摸摸、身体戳戳）
    /// ? v1.6.42: 新增 Draw() 方法，支持 Harmony OnGUI 直接绘制
    /// 功能：
    /// - 显示 1024x1574 全身立绘（使用分层系统）
    /// - 动态表情切换
    /// - 呼吸动画
    /// - 眨眼和张嘴动画
    /// - ? 触摸互动（悬停1秒激活，移动鼠标触发表情）
    /// - ? 区域交互（头部摸摸、身体戳戳）
    /// - ? Harmony OnGUI 支持（不阻挡地图点击）
    /// </summary>
    [StaticConstructorOnStartup]
    public class FullBodyPortraitPanel : Window
    {
        private const float PORTRAIT_WIDTH = 1024f;
        private const float PORTRAIT_HEIGHT = 1574f;
        private const float SCALE_FACTOR = 0.35f;  // 缩放到35%（约358x551）
        
        private float displayWidth;
        private float displayHeight;
        private Rect cachedDrawRect;
        
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
        
        // ? v1.6.40: 区域交互系统相关
        private float headRubProgress = 0f;
        private const float HEAD_RUB_THRESHOLD = 60f;
        private const float HEAD_RUB_DECAY_RATE = 20f;  // 每秒衰减速度
        private float lastHeadPatTime = 0f;
        private const float HEAD_PAT_COOLDOWN = 3.0f;
        
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
            
            // ? v1.6.42: 缓存绘制矩形（固定在屏幕左侧，垂直居中）
            float x = 10f;
            float y = (Verse.UI.screenHeight - displayHeight) / 2f;
            cachedDrawRect = new Rect(x, y, displayWidth, displayHeight);
        }

        public override Vector2 InitialSize => new Vector2(displayWidth, displayHeight);

        protected override void SetInitialSizeAndPosition()
        {
            // 固定在屏幕左侧，垂直居中
            this.windowRect = cachedDrawRect;
        }

        public override void PreOpen()
        {
            base.PreOpen();
            this.doWindowBackground = false;
            this.drawShadow = false;
        }
        
        /// <summary>
        /// ? v1.6.42: 新增 Draw() 方法用于 Harmony OnGUI 直接绘制
        /// 这个方法不依赖 Window 系统，可以直接在 OnGUI 中调用
        /// </summary>
        public void Draw()
        {
            // ? 更新张嘴动画（每帧）
            float deltaTime = Time.deltaTime;
            MouthAnimationSystem.Update(deltaTime);
            
            // ? 调用核心绘制逻辑
            DoWindowContents(cachedDrawRect);
        }

        public override void DoWindowContents(Rect inRect)
        {
            // ? v1.6.41: Shift 键激活系统 - 视觉"幽灵模式"
            bool mouseOver = Mouse.IsOver(inRect);
            bool shiftHeld = Event.current.shift;
            
            // ? 1. 视觉透明度控制
            Color originalColor = GUI.color;
            if (mouseOver && !shiftHeld)
            {
                // 鼠标悬停但未按 Shift：半透明（幽灵模式）
                GUI.color = new Color(1f, 1f, 1f, 0.2f);
            }
            else
            {
                // 按住 Shift 或鼠标未悬停：完全不透明
                GUI.color = new Color(1f, 1f, 1f, 1.0f);
            }
            
            // ? v1.6.40: 处理区域交互（只在 Shift 按下时激活）
            bool zoneInteractionHandled = HandleZoneInteraction(inRect);
            
            // ? v1.6.25: 处理触摸互动（如果区域交互未处理）
            if (!zoneInteractionHandled)
            {
                HandleHoverAndTouch(inRect);
            }
            
            // 更新人格信息
            UpdatePortrait();
            
            if (currentPersona != null)
            {
                // ? 应用呼吸动画偏移
                float breathingOffset = ExpressionSystem.GetBreathingOffset(currentPersona.defName);
                Rect drawRect = new Rect(inRect.x, inRect.y + breathingOffset, inRect.width, inRect.height);
                
                // ? v1.6.34: 运行时分层绘制（每帧重新组合）
                DrawLayeredPortraitRuntime(drawRect, currentPersona);
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
            
            // ? 恢复原始颜色
            GUI.color = originalColor;
            
            // ? 悬停提示（增强：显示 Shift 键提示）
            if (Mouse.IsOver(inRect) && currentPersona != null)
            {
                string tooltip = $"{currentPersona.narratorName}\n表情: {lastExpression}";
                
                // ? v1.6.41: Shift 键提示
                if (!shiftHeld)
                {
                    tooltip += "\n\n?? 按住 Shift 键激活互动模式";
                }
                else
                {
                    tooltip += "\n\n? 互动模式已激活";
                }
                
                // ? v1.6.40: 显示当前交互区域
                if (shiftHeld)
                {
                    var zone = GetInteractionZone(inRect, Event.current.mousePosition);
                    if (zone == InteractionPhrases.InteractionZone.Head)
                    {
                        tooltip += $"\n\n?? 头部区域 | 摸摸进度: {headRubProgress:F0}/{HEAD_RUB_THRESHOLD}";
                    }
                    else if (zone == InteractionPhrases.InteractionZone.Body)
                    {
                        tooltip += "\n\n?? 身体区域 | 单击戳戳";
                    }
                }
                
                // ? 触摸模式提示（仅在 Shift 按下时）
                if (shiftHeld)
                {
                    if (isTouchModeActive)
                    {
                        tooltip += "\n\n? 触摸模式激活！移动鼠标与她互动";
                    }
                    else if (isHovering)
                    {
                        float progress = (Time.realtimeSinceStartup - hoverStartTime) / HOVER_ACTIVATION_TIME;
                        tooltip += $"\n?? 悬停进度: {(progress * 100):F0}%";
                    }
                    else
                    {
                        tooltip += "\n?? 悬停1秒激活触摸模式";
                    }
                }
                
                TooltipHandler.TipRegion(inRect, tooltip);
            }
            
            // ? 绘制悬停进度条（仅在 Shift 按下时）
            if (shiftHeld && isHovering && !isTouchModeActive)
            {
                float progress = (Time.realtimeSinceStartup - hoverStartTime) / HOVER_ACTIVATION_TIME;
                DrawHoverProgress(inRect, progress);
            }
            
            // ? v1.6.40: 绘制头部摸摸进度条（仅在 Shift 按下时）
            if (shiftHeld && headRubProgress > 0f)
            {
                DrawHeadRubProgress(inRect, headRubProgress / HEAD_RUB_THRESHOLD);
            }
            
            // ? 触摸模式指示器（仅在 Shift 按下时）
            if (shiftHeld && isTouchModeActive)
            {
                DrawTouchModeIndicator(inRect);
            }
            
            // ? 绘制边框闪烁效果（仅在 Shift 按下时）
            if (shiftHeld)
            {
                DrawBorderFlash(inRect);
            }
        }
        
        /// <summary>
        /// ? v1.6.40: 处理区域交互系统
        /// ? v1.6.41: 添加 Shift 键守卫 - 未按 Shift 时不拦截事件
        /// 返回：是否处理了交互（用于阻止穿透点击）
        /// </summary>
        private bool HandleZoneInteraction(Rect inRect)
        {
            if (currentPersona == null) return false;
            
            // ? v1.6.41: Shift 键守卫 - 未按 Shift 时允许点击穿透
            bool shiftHeld = Event.current.shift;
            if (!shiftHeld)
            {
                // 衰减头部摸摸进度（即使未按 Shift）
                if (headRubProgress > 0f)
                {
                    headRubProgress -= HEAD_RUB_DECAY_RATE * Time.deltaTime;
                    if (headRubProgress < 0f) headRubProgress = 0f;
                }
                
                // ? 不拦截事件，允许点击穿透到地图
                return false;
            }
            
            bool mouseOver = Mouse.IsOver(inRect);
            if (!mouseOver)
            {
                // 衰减头部摸摸进度
                if (headRubProgress > 0f)
                {
                    headRubProgress -= HEAD_RUB_DECAY_RATE * Time.deltaTime;
                    if (headRubProgress < 0f) headRubProgress = 0f;
                }
                return false;
            }
            
            Vector2 mousePos = Event.current.mousePosition;
            var zone = GetInteractionZone(inRect, mousePos);
            
            // ? 1. 处理头部摸摸逻辑（摩擦累积）
            if (zone == InteractionPhrases.InteractionZone.Head)
            {
                // ? 检测鼠标拖拽或移动（左键按下时）
                bool isMouseDragging = (Event.current.type == EventType.MouseDrag) || 
                                      (Event.current.type == EventType.MouseMove && Event.current.button == 0);
                
                if (isMouseDragging)
                {
                    // 鼠标在头部区域移动时累积进度
                    float moveDistance = Vector2.Distance(mousePos, lastMousePosition);
                    headRubProgress += moveDistance * 0.5f;  // 移动距离转换为进度
                    
                    if (headRubProgress >= HEAD_RUB_THRESHOLD)
                    {
                        float currentTime = Time.realtimeSinceStartup;
                        if (currentTime - lastHeadPatTime >= HEAD_PAT_COOLDOWN)
                        {
                            DoHeadPatInteraction();
                            headRubProgress = 0f;
                            lastHeadPatTime = currentTime;
                            
                            Event.current.Use();  // ? 拦截事件，防止穿透
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
                // 不在头部时衰减进度
                if (headRubProgress > 0f)
                {
                    headRubProgress -= HEAD_RUB_DECAY_RATE * Time.deltaTime;
                    if (headRubProgress < 0f) headRubProgress = 0f;
                }
            }
            
            // ? 2. 处理身体戳戳逻辑（单击检测）
            if (zone == InteractionPhrases.InteractionZone.Body)
            {
                if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
                {
                    DoPokeInteraction();
                    Event.current.Use();  // ? 拦截事件，防止穿透
                    return true;
                }
            }
            
            return false;
        }
        
        /// <summary>
        /// ? v1.6.40: 获取交互区域
        /// </summary>
        private InteractionPhrases.InteractionZone GetInteractionZone(Rect rect, Vector2 mousePos)
        {
            if (!rect.Contains(mousePos))
            {
                return InteractionPhrases.InteractionZone.None;
            }
            
            // 计算相对位置（0-1）
            float relativeY = (mousePos.y - rect.y) / rect.height;
            
            // ? 头部区域：上方 25%
            if (relativeY < 0.25f)
            {
                return InteractionPhrases.InteractionZone.Head;
            }
            
            // ? 身体区域：其余部分，需要不透明像素检测
            if (IsOpaquePixel(rect, mousePos))
            {
                return InteractionPhrases.InteractionZone.Body;
            }
            
            return InteractionPhrases.InteractionZone.None;
        }
        
        /// <summary>
        /// ? v1.6.40: 检测像素是否不透明（简化版本）
        /// 注意：完整实现需要访问纹理像素数据，这里使用简化判断
        /// </summary>
        private bool IsOpaquePixel(Rect rect, Vector2 mousePos)
        {
            // ? 简化实现：假设立绘中心80%区域都是不透明的
            // 完整实现需要：baseBody.GetPixel(x, y).a > 0.1f
            
            float relativeX = (mousePos.x - rect.x) / rect.width;
            float relativeY = (mousePos.y - rect.y) / rect.height;
            
            // 排除边缘10%区域（通常是透明的）
            bool inCentralArea = relativeX > 0.1f && relativeX < 0.9f &&
                                relativeY > 0.1f && relativeY < 0.9f;
            
            return inCentralArea;
        }
        
        /// <summary>
        /// ? v1.6.40: 头部摸摸互动
        /// ? v1.6.41: 使用基于好感度的对话文本
        /// </summary>
        private void DoHeadPatInteraction()
        {
            if (currentPersona == null) return;
            
            if (Prefs.DevMode)
            {
                Log.Message("[FullBodyPortraitPanel] 头部摸摸触发！");
            }
            
            // ? 获取当前好感度
            var agent = Current.Game?.GetComponent<Storyteller.StorytellerAgent>();
            float affinity = agent?.GetAffinity() ?? 0f;
            
            // ? 根据好感度选择表情
            ExpressionType expression;
            if (affinity >= 60f)
            {
                // 高好感：害羞或开心
                expression = Random.value > 0.5f ? ExpressionType.Shy : ExpressionType.Happy;
            }
            else if (affinity >= -20f)
            {
                // 中立：困惑或平静
                expression = Random.value > 0.5f ? ExpressionType.Confused : ExpressionType.Neutral;
            }
            else
            {
                // 低好感：生气或冷漠
                expression = ExpressionType.Angry;
            }
            
            TriggerExpression(expression, duration: 3f);
            
            // ? 显示基于好感度的对话文本
            string phrase = InteractionPhrases.GetHeadPatPhrase(affinity);
            ShowFloatingText(phrase, GetTextColorByAffinity(affinity));
            
            // ? 边框闪烁（只在高好感度时）
            if (affinity >= 60f)
            {
                StartBorderFlash(1);
            }
            
            // ? 好感度变化（只在高好感度时增加）
            if (affinity >= 60f)
            {
                ModifyAffinity(3f, "头部摸摸互动");
                Messages.Message("好感度 +3（头部摸摸）", MessageTypeDefOf.PositiveEvent);
            }
            else if (affinity < -20f)
            {
                // 低好感度：负面反馈
                ModifyAffinity(-1f, "不受欢迎的触碰");
            }
        }
        
        /// <summary>
        /// ? v1.6.40: 身体戳戳互动
        /// ? v1.6.41: 使用基于好感度的对话文本
        /// </summary>
        private void DoPokeInteraction()
        {
            if (currentPersona == null) return;
            
            if (Prefs.DevMode)
            {
                Log.Message("[FullBodyPortraitPanel] 身体戳戳触发！");
            }
            
            // ? 获取当前好感度
            var agent = Current.Game?.GetComponent<Storyteller.StorytellerAgent>();
            float affinity = agent?.GetAffinity() ?? 0f;
            
            // ? 根据好感度选择表情
            ExpressionType expression;
            if (affinity >= 60f)
            {
                // 高好感：惊讶或开心
                expression = Random.value > 0.5f ? ExpressionType.Surprised : ExpressionType.Happy;
            }
            else if (affinity >= -20f)
            {
                // 中立：困惑或平静
                expression = Random.value > 0.5f ? ExpressionType.Confused : ExpressionType.Neutral;
            }
            else
            {
                // 低好感：生气
                expression = ExpressionType.Angry;
            }
            
            TriggerExpression(expression, duration: 2f);
            
            // ? 显示基于好感度的对话文本
            string phrase = InteractionPhrases.GetPokePhrase(affinity);
            ShowFloatingText(phrase, GetTextColorByAffinity(affinity));
            
            // ? 好感度变化（只在高好感度时增加）
            if (affinity >= 60f)
            {
                ModifyAffinity(1f, "身体戳戳互动");
            }
            else if (affinity < -20f)
            {
                // 低好感度：负面反馈
                ModifyAffinity(-0.5f, "烦人的触碰");
            }
        }
        
        /// <summary>
        /// ? v1.6.41: 根据好感度获取文本颜色
        /// </summary>
        private Color GetTextColorByAffinity(float affinity)
        {
            if (affinity >= 60f)
            {
                // 高好感：粉红/温暖色
                return new Color(1f, 0.7f, 0.8f);
            }
            else if (affinity >= -20f)
            {
                // 中立：淡蓝/中性色
                return new Color(0.8f, 0.9f, 1f);
            }
            else
            {
                // 低好感：灰白/冷色
                return new Color(0.7f, 0.7f, 0.7f);
            }
        }
        
        /// <summary>
        /// ? v1.6.40: 绘制头部摸摸进度条
        /// </summary>
        private void DrawHeadRubProgress(Rect inRect, float progress)
        {
            progress = Mathf.Clamp01(progress);
            
            // 在立绘上方显示进度条
            var progressBarRect = new Rect(inRect.x, inRect.y - 12f, inRect.width, 8f);
            Widgets.DrawBoxSolid(progressBarRect, new Color(0.2f, 0.2f, 0.2f, 0.6f));
            
            var fillRect = new Rect(progressBarRect.x, progressBarRect.y, progressBarRect.width * progress, progressBarRect.height);
            Color fillColor = Color.Lerp(new Color(1f, 0.6f, 0.6f), new Color(1f, 0.3f, 0.3f), progress);
            Widgets.DrawBoxSolid(fillRect, fillColor);
            
            // 显示文字提示
            if (progress > 0.5f)
            {
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = new Color(1f, 1f, 1f, 0.8f);
                Widgets.Label(progressBarRect, "继续摸摸...");
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
            }
        }
        
        /// <summary>
        /// ? v1.6.34: 运行时分层绘制立绘（支持眨眼和张嘴动画）
        /// ? v1.6.35: 修复路径错误，确保使用分层立绘系统
        /// </summary>
        private void DrawLayeredPortraitRuntime(Rect rect, NarratorPersonaDef persona)
        {
            // ? v1.6.35: 移除useLayeredPortrait检查，强制使用分层系统
            // 原因：所有立绘文件已部署在 Layered/Sideria/ 文件夹下
            
            // 1. 绘制基础身体层（始终显示）
            var baseBody = PortraitLoader.GetLayerTexture(persona, "base_body");
            if (baseBody == null)
            {
                // 回退到占位符
                if (Prefs.DevMode)
                {
                    Log.Warning($"[FullBodyPortraitPanel] base_body not found for {persona.defName}");
                }
                Widgets.DrawBoxSolid(rect, persona.primaryColor);
                return;
            }
            GUI.DrawTexture(rect, baseBody, ScaleMode.ScaleToFit);
            
            // 2. ? 获取当前眼睛层（调用眨眼动画系统）
            string eyeLayerName = BlinkAnimationSystem.GetEyeLayerName(persona.defName);
            if (!string.IsNullOrEmpty(eyeLayerName))
            {
                var eyeTexture = PortraitLoader.GetLayerTexture(persona, eyeLayerName);
                if (eyeTexture != null)
                {
                    GUI.DrawTexture(rect, eyeTexture, ScaleMode.ScaleToFit);
                }
                else if (Prefs.DevMode)
                {
                    Log.Warning($"[FullBodyPortraitPanel] Eye layer '{eyeLayerName}' not found for {persona.defName}");
                }
            }
            
            // 3. ? 获取当前嘴巴层（调用张嘴动画系统）
            string mouthLayerName = MouthAnimationSystem.GetMouthLayerName(persona.defName);
            if (!string.IsNullOrEmpty(mouthLayerName))
            {
                var mouthTexture = PortraitLoader.GetLayerTexture(persona, mouthLayerName);
                if (mouthTexture != null)
                {
                    GUI.DrawTexture(rect, mouthTexture, ScaleMode.ScaleToFit);
                }
                else if (Prefs.DevMode)
                {
                    Log.Warning($"[FullBodyPortraitPanel] Mouth layer '{mouthLayerName}' not found for {persona.defName}");
                }
            }
            
            // 4. 可选：腮红/特效层
            var expressionState = ExpressionSystem.GetExpressionState(persona.defName);
            if (expressionState.CurrentExpression == ExpressionType.Shy || 
                expressionState.CurrentExpression == ExpressionType.Angry)
            {
                string flushLayerName = expressionState.CurrentExpression == ExpressionType.Shy ? 
                    "flush_shy" : "flush_angry";
                var flushTexture = PortraitLoader.GetLayerTexture(persona, flushLayerName);
                if (flushTexture != null)
                {
                    GUI.DrawTexture(rect, flushTexture, ScaleMode.ScaleToFit);
                }
            }
        }

        /// <summary>
        /// ? v1.6.25: 处理悬停和触摸互动
        /// ? v1.6.41: 添加 Shift 键守卫
        /// </summary>
        private void HandleHoverAndTouch(Rect inRect)
        {
            if (currentPersona == null) return;
            
            // ? v1.6.41: Shift 键守卫
            bool shiftHeld = Event.current.shift;
            if (!shiftHeld)
            {
                // 未按 Shift 时，取消触摸模式
                if (isHovering || isTouchModeActive)
                {
                    DeactivateTouchMode();
                }
                isHovering = false;
                return;
            }
            
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
        /// ? v1.6.40: 恢复默认表情（基于好感度的待机表情系统）
        /// 根据 agent.affinity 决定待机表情：
        /// - 高好感 (> 80): Shy（暗恋/脸红）
        /// - 良好 (> 40): Happy（微笑）
        /// - 中立 (-20 to 40): Neutral（平静）
        /// - 不佳 (-60 to -20): Sad（失望/冷淡）
        /// - 敌对 (< -60): Angry（憎恨）
        /// 
        /// ? 心情覆盖：如果 currentMood 是负面状态，强制使用 Sad 表情
        /// ? 无限持续：duration = 99999f，表情保持到下次互动事件
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
                
                // ? 心情覆盖：心情极差时强制显示悲伤表情
                if (mood == Storyteller.MoodState.Melancholic || 
                    mood == Storyteller.MoodState.Angry)
                {
                    defaultExpression = ExpressionType.Sad;
                }
                else
                {
                    // ? 基于好感度的待机表情
                    defaultExpression = affinity switch
                    {
                        > 80f => ExpressionType.Shy,      // 高好感：害羞/暗恋
                        > 40f => ExpressionType.Happy,    // 良好：开心/微笑
                        > -20f => ExpressionType.Neutral, // 中立：平静
                        > -60f => ExpressionType.Sad,     // 不佳：失望/冷淡
                        _ => ExpressionType.Angry         // 敌对：憎恨
                    };
                }
                
                // ? 关键：无限持续时间 (99999f)，直到下次互动事件
                TriggerExpression(defaultExpression, duration: 99999f);
                
                if (Prefs.DevMode)
                {
                    Log.Message($"[FullBodyPortraitPanel] 恢复待机表情: {defaultExpression} (Affinity={affinity:F1}, Mood={mood})");
                }
            }
            else
            {
                // ? 无 agent 时，使用中立表情（无限持续）
                TriggerExpression(ExpressionType.Neutral, duration: 99999f);
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
        /// ? 更新立绘（运行时分层绘制）
        /// </summary>
        private void UpdatePortrait()
        {
            // ? 不再缓存立绘纹理，改为每帧运行时绘制
            // 只更新人格信息
            try
            {
                var manager = Current.Game?.GetComponent<NarratorManager>();
                if (manager == null)
                {
                    currentPersona = null;
                    return;
                }
                
                var persona = manager.GetCurrentPersona();
                if (persona == null)
                {
                    currentPersona = null;
                    return;
                }
                
                currentPersona = persona;
                
                // ? 跟踪表情变化（用于DevMode日志）
                var expressionState = ExpressionSystem.GetExpressionState(persona.defName);
                if (expressionState.CurrentExpression != lastExpression)
                {
                    lastExpression = expressionState.CurrentExpression;
                    if (Prefs.DevMode)
                    {
                        Log.Message($"[FullBodyPortraitPanel] 表情变化: {lastExpression}");
                    }
                }
            }
            catch (System.Exception ex)
            {
                Log.Warning($"[FullBodyPortraitPanel] 更新立绘失败: {ex.Message}");
                currentPersona = null;
            }
        }

        public override void WindowUpdate()
        {
            base.WindowUpdate();
            
            if (Current.ProgramState != ProgramState.Playing)
            {
                this.Close();
            }
            
            // ? v1.6.36: 每帧更新张嘴动画系统（TTS口型同步）
            // 注意：眨眼动画通过 GetBlinkLayerName() 自动工作，不需要Update()
            float deltaTime = Time.deltaTime;
            MouthAnimationSystem.Update(deltaTime);  // ? 关键修复：调用张嘴动画更新
        }
    }
}
