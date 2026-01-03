using UnityEngine;
using Verse;
using RimWorld;
using TheSecondSeat.PersonaGeneration;
using TheSecondSeat.Narrator;
using TheSecondSeat.Settings;
using TheSecondSeat.Descent;
using TheSecondSeat.Utils;
using TheSecondSeat.Core;

using System.Collections.Generic;
using System;

namespace TheSecondSeat.UI
{
    /// <summary>
    /// ✅ v1.6.74: 全身立绘面板（独立绘制，不继承 Window）
    /// ⭐ v1.6.74 更新：立绘尺寸规格调整
    /// - 原始立绘尺寸：2308x3544px
    /// - 显示尺寸：保持相同比例缩放
    /// 
    /// 新增功能：
    /// - ⭐ 通用姿态系统（姿态覆盖、特效叠加、动画回调）
    /// - 实体化降临支持（动态替换身体层）
    /// 
    /// 原有功能：
    /// - Shift 键幽灵模式（未按 Shift：半透明 + 点击穿透）
    /// - 自定义浮动文字系统（替代 MoteMaker）
    /// - 区域交互（头部摸摸、身体戳戳）
    /// - 分层立绘 + 动画系统（呼吸、眨眼、张嘴）
    /// - Runtime Layering（修复透明度和动画问题）
    /// </summary>
    [StaticConstructorOnStartup]
    public class FullBodyPortraitPanel
    {
        // ==================== 🏗️ 配置化常量（使用框架配置） ====================
        
        // 立绘尺寸使用配置类
        private static float PORTRAIT_WIDTH => TSSFrameworkConfig.Portrait.OriginalWidth;
        private static float PORTRAIT_HEIGHT => TSSFrameworkConfig.Portrait.OriginalHeight;
        private static float SCALE_FACTOR => TSSFrameworkConfig.Portrait.DefaultScaleFactor;
        
        // 交互参数使用配置类
        private static float HOVER_ACTIVATION_TIME => TSSFrameworkConfig.Interaction.HoverActivationTime;
        private static float TOUCH_COOLDOWN => TSSFrameworkConfig.Interaction.TouchCooldown;
        private static float HEAD_RUB_THRESHOLD => TSSFrameworkConfig.Interaction.HeadRubThreshold;
        private static float HEAD_RUB_DECAY_RATE => TSSFrameworkConfig.Interaction.HeadRubDecayRate;
        private static float HEAD_PAT_COOLDOWN => TSSFrameworkConfig.Interaction.HeadPatCooldown;
        
        // 闪烁动画参数（保持硬编码，UI 动画细节无需扩展）
        private const float SLOW_FLASH_DURATION = 1.0f;
        private const float FAST_FLASH_DURATION = 0.15f;
        private const float FAST_FLASH_INTERVAL = 0.05f;
        
        // ==================== ⭐ 通用姿态系统字段 ====================
        
        /// <summary>
        /// 当前覆盖姿态的纹理名称（如 "body_arrival"）
        /// 非空时：替代默认身体层 (Layer 1)
        /// </summary>
        private string overridePosture = null;
        
        /// <summary>
        /// 特效纹理名称（如 "glitch_circle"）
        /// 绘制在最顶层，使用 Alpha 混合
        /// </summary>
        private string activeEffect = null;
        
        /// <summary>
        /// 动画结束回调
        /// </summary>
        private Action onAnimationComplete = null;
        
        /// <summary>
        /// 动画计时器（秒）
        /// </summary>
        private float animationTimer = 0f;
        
        /// <summary>
        /// 动画总时长（秒）
        /// </summary>
        private float animationDuration = 0f;
        
        /// <summary>
        /// 动画状态标志
        /// </summary>
        private bool isPlayingAnimation = false;
        
        // ==================== 原有字段定义 ====================
        
        private float displayWidth;
        private float displayHeight;
        private Rect drawRect;
        
        private NarratorPersonaDef? currentPersona = null;
        private ExpressionType lastExpression = ExpressionType.Neutral;
        private int portraitUpdateTick = 0;
        private const int PORTRAIT_UPDATE_INTERVAL = 30;
        
        // ? v1.6.44: Runtime Layering 缓存
        private Texture2D? cachedBodyBase = null;
        private string? cachedPersonaDefName = null;
        
        // 触摸互动
        private float hoverStartTime = 0f;
        private bool isHovering = false;
        private bool isTouchModeActive = false;
        private Vector2 lastMousePosition = Vector2.zero;
        private float lastTouchTime = 0f;
        private int touchCount = 0;
        
        private ExpressionType[] touchExpressions = new[] 
        {
            ExpressionType.Happy,
            ExpressionType.Surprised,
            ExpressionType.Smug,
            ExpressionType.Shy
        };
        
        // 边框闪烁
        private float borderFlashStartTime = 0f;
        private int borderFlashCount = 0;
        
        // 区域交互
        private float headRubProgress = 0f;
        private float lastHeadPatTime = 0f;
        
        // ? 自定义浮动文字系统
        private List<UIFloatingText> floatingTexts = new List<UIFloatingText>();
        
        // ==================== 初始化 ====================
        
        public FullBodyPortraitPanel()
        {
            displayWidth = PORTRAIT_WIDTH * SCALE_FACTOR;
            displayHeight = PORTRAIT_HEIGHT * SCALE_FACTOR;
            
            // 🏗️ 使用配置类的位置参数
            float x = TSSFrameworkConfig.Portrait.PanelOffsetX;
            float y = (Verse.UI.screenHeight - displayHeight) / 2f + TSSFrameworkConfig.Portrait.PanelOffsetY;
            drawRect = new Rect(x, y, displayWidth, displayHeight);
        }
        
        // ==================== ⭐ 通用姿态系统公共接口 ====================
        
        /// <summary>
        /// ⭐ 触发姿态动画
        /// 🛡️ v1.6.79: 添加资源存在性检查
        /// </summary>
        /// <param name="postureName">姿态纹理名称（如 "body_arrival"）</param>
        /// <param name="effectName">特效纹理名称（如 "glitch_circle"），可为 null</param>
        /// <param name="duration">动画时长（秒）</param>
        /// <param name="callback">动画结束回调，可为 null</param>
        /// <returns>是否成功触发动画</returns>
        public bool TriggerPostureAnimation(string postureName, string effectName, float duration, Action callback = null)
        {
            // 🛡️ 检查姿态资源是否存在
            if (!string.IsNullOrEmpty(postureName))
            {
                string personaName = currentPersona?.defName ?? "";
                var postureTexture = TSS_AssetLoader.LoadDescentPosture(personaName, postureName, null);
                
                if (postureTexture == null)
                {
                    if (Prefs.DevMode)
                    {
                        Log.Warning($"[FullBodyPortraitPanel] 姿态资源不存在，跳过动画: {postureName}");
                    }
                    
                    // 🛡️ 资源不存在时直接调用回调并返回
                    callback?.Invoke();
                    return false;
                }
            }
            
            // 初始化动画状态
            overridePosture = postureName;
            activeEffect = effectName;
            animationDuration = duration;
            animationTimer = 0f;
            onAnimationComplete = callback;
            isPlayingAnimation = true;
            
            // 日志已静默：姿态动画开始
            
            return true;
        }
        
        /// <summary>
        /// ⭐ 停止当前动画并恢复默认状态
        /// </summary>
        public void StopAnimation()
        {
            if (!isPlayingAnimation) return;
            
            // 触发回调（如果存在）
            try
            {
                onAnimationComplete?.Invoke();
            }
            catch (Exception ex)
            {
                Log.Error($"[FullBodyPortraitPanel] 动画回调异常: {ex}");
            }
            
            // 清除动画状态
            overridePosture = null;
            activeEffect = null;
            animationTimer = 0f;
            animationDuration = 0f;
            onAnimationComplete = null;
            isPlayingAnimation = false;
            
            // 日志已静默：动画停止
        }
        
        // ==================== 主绘制方法 ====================
        
        /// <summary>
        /// ? 主绘制入口（由 PortraitOverlaySystem 调用）
        /// </summary>
        public void Draw()
        {
            // ⭐ 更新动画计时器
            UpdateAnimation();
            
            // 更新张嘴动画
            MouthAnimationSystem.Update(Time.deltaTime);
            
            // 更新人格信息
            UpdatePortrait();
            
            // 绘制立绘内容
            DrawPortraitContents();
            
            // 绘制浮动文字
            DrawFloatingTexts();
        }
        
        /// <summary>
        /// ⭐ 更新动画状态（每帧调用）
        /// </summary>
        private void UpdateAnimation()
        {
            if (!isPlayingAnimation) return;
            
            // 计时器递增
            animationTimer += Time.deltaTime;
            
            // 检查是否结束
            if (animationTimer >= animationDuration)
            {
                StopAnimation();
            }
        }
        
        /// <summary>
        /// ? v1.6.44: 绘制立绘内容（核心逻辑 - Runtime Layering 版本）
        /// 🛡️ v1.6.79: 使用 TSS_AssetLoader 静默加载，支持资源缺失时的优雅降级
        /// </summary>
        private void DrawPortraitContents()
        {
            if (currentPersona == null) return;
            
            // 🛡️ 检查是否有可用的立绘资源
            bool hasPortrait = HasAvailablePortrait();
            
            if (!hasPortrait)
            {
                // 🛡️ 没有立绘资源时显示占位符
                DrawNoPortraitPlaceholder();
                return;
            }
            
            // 更新身体层缓存（仅在人格变化时重新加载）
            UpdateBodyBaseIfNeeded();
            
            bool mouseOver = Mouse.IsOver(drawRect);
            bool shiftHeld = Event.current.shift;
            
            // ==================== 1. 计算 Alpha 值 ====================
            
            float alpha = 1.0f;
            bool shouldConsumeInput = false;
            
            // ⭐ 动画中强制不透明（忽略 Shift 逻辑）
            if (isPlayingAnimation)
            {
                alpha = CalculateAnimationAlpha();
                shouldConsumeInput = false; // 动画中不响应输入
            }
            else if (mouseOver && !shiftHeld)
            {
                // 未按 Shift：半透明 + 不拦截输入
                alpha = 0.2f;
                shouldConsumeInput = false;
            }
            else
            {
                // 按住 Shift 或鼠标不在范围：完全不透明
                alpha = 1.0f;
                shouldConsumeInput = shiftHeld && mouseOver;
            }
            
            // ==================== 2. 绘制立绘（关键：统一设置 GUI.color） ====================
            
            // 应用呼吸动画偏移（动画中禁用呼吸动画）
            float breathingOffset = isPlayingAnimation ? 0f : ExpressionSystem.GetBreathingOffset(currentPersona.defName);
            Rect animatedRect = new Rect(drawRect.x, drawRect.y + breathingOffset, drawRect.width, drawRect.height);
            
            // ⭐ 关键：在绘制任何图层前统一设置 GUI.color
            GUI.color = new Color(1f, 1f, 1f, alpha);
            
            // 判断是否处于幽灵模式（半透明且非动画状态）
            bool isGhostMode = !isPlayingAnimation && mouseOver && !shiftHeld;

            // ⭐ 运行时分层绘制（支持姿态覆盖）
            DrawLayeredPortraitRuntime(animatedRect, currentPersona, isGhostMode);
            
            // ⭐ 绘制特效层（最顶层）
            if (!string.IsNullOrEmpty(activeEffect))
            {
                DrawEffectLayer(animatedRect);
            }
            
            // 绘制完成后恢复颜色
            GUI.color = Color.white;
            
            // ==================== 3. 交互处理（仅在 Shift 模式下，且非动画中） ====================
            
            if (!isPlayingAnimation && shiftHeld && mouseOver)
            {
                // 处理区域交互
                bool interactionHandled = HandleZoneInteraction(drawRect);
                
                // 处理触摸互动（如果区域交互未处理）
                if (!interactionHandled)
                {
                    HandleHoverAndTouch(drawRect);
                }
                
                // 绘制交互提示
                DrawInteractionUI(drawRect);
                
                // ? 拦截输入（防止点击穿透到地图）
                if (shouldConsumeInput && Event.current.type == EventType.MouseDown)
                {
                    Event.current.Use();
                }
            }
            else
            {
                // 未按 Shift 或动画中：取消触摸模式
                if (isTouchModeActive || isHovering)
                {
                    DeactivateTouchMode();
                }
                isHovering = false;
                
                if (headRubProgress > 0f)
                {
                    headRubProgress -= HEAD_RUB_DECAY_RATE * Time.deltaTime;
                    if (headRubProgress < 0f) headRubProgress = 0f;
                }
            }
            
            // ==================== 4. 工具提示 ====================
            
            if (mouseOver && !isPlayingAnimation)
            {
                string tooltip = BuildTooltip(shiftHeld);
                TooltipHandler.TipRegion(drawRect, tooltip);
            }
        }
        
        /// <summary>
        /// ? 处理区域交互（头部摸摸、身体戳戳）
        /// </summary>
        private bool HandleZoneInteraction(Rect inRect)
        {
            Vector2 mousePos = Event.current.mousePosition;
            var zone = GetInteractionZone(inRect, mousePos);
            
            // 头部摸摸逻辑
            if (zone == InteractionPhrases.InteractionZone.Head)
            {
                bool isMouseDragging = (Event.current.type == EventType.MouseDrag) || 
                                      (Event.current.type == EventType.MouseMove && Event.current.button == 0);
                
                if (isMouseDragging)
                {
                    float moveDistance = Vector2.Distance(mousePos, lastMousePosition);
                    headRubProgress += moveDistance * 0.5f;
                    
                    if (headRubProgress >= HEAD_RUB_THRESHOLD)
                    {
                        float currentTime = Time.realtimeSinceStartup;
                        if (currentTime - lastHeadPatTime >= HEAD_PAT_COOLDOWN)
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
                // 衰减进度
                if (headRubProgress > 0f)
                {
                    headRubProgress -= HEAD_RUB_DECAY_RATE * Time.deltaTime;
                    if (headRubProgress < 0f) headRubProgress = 0f;
                }
            }
            
            // 身体戳戳逻辑
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
        
        /// <summary>
        /// ? 处理悬停和触摸互动
        /// </summary>
        private void HandleHoverAndTouch(Rect inRect)
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
        
        /// <summary>根据好感度选择表情（🏗️ 使用配置阈值）</summary>
        private ExpressionType SelectExpressionByAffinity(float affinity, ExpressionType highPositive, ExpressionType lowPositive)
        {
            float highThreshold = TSSFrameworkConfig.Interaction.HighAffinityThreshold;
            float lowThreshold = TSSFrameworkConfig.Interaction.LowAffinityThreshold;
            
            if (affinity >= highThreshold)
                return UnityEngine.Random.value > 0.5f ? highPositive : ExpressionType.Happy;
            if (affinity >= lowThreshold)
                return UnityEngine.Random.value > 0.5f ? ExpressionType.Confused : ExpressionType.Neutral;
            return ExpressionType.Angry;
        }
        
        /// <summary>? 头部摸摸交互（🏗️ 使用配置值）</summary>
        private void DoHeadPatInteraction()
        {
            float affinity = Current.Game?.GetComponent<Storyteller.StorytellerAgent>()?.GetAffinity() ?? 0f;
            float highThreshold = TSSFrameworkConfig.Interaction.HighAffinityThreshold;
            float lowThreshold = TSSFrameworkConfig.Interaction.LowAffinityThreshold;
            float bonus = TSSFrameworkConfig.Interaction.HeadPatAffinityBonus;
            
            TriggerExpression(SelectExpressionByAffinity(affinity, ExpressionType.Shy, ExpressionType.Happy), duration: 3f);
            AddFloatingText(InteractionPhrases.GetHeadPatPhrase(affinity), GetTextColorByAffinity(affinity));
            
            if (affinity >= highThreshold)
            {
                StartBorderFlash(1);
                ModifyAffinity(bonus, "头部摸摸互动");
                Messages.Message($"好感度 +{bonus}（头部摸摸）", MessageTypeDefOf.PositiveEvent);
            }
            else if (affinity < lowThreshold)
            {
                ModifyAffinity(-1f, "不受欢迎的触碰");
            }
        }
        
        /// <summary>? 身体戳戳交互（🏗️ 使用配置值）</summary>
        private void DoPokeInteraction()
        {
            float affinity = Current.Game?.GetComponent<Storyteller.StorytellerAgent>()?.GetAffinity() ?? 0f;
            float highThreshold = TSSFrameworkConfig.Interaction.HighAffinityThreshold;
            float lowThreshold = TSSFrameworkConfig.Interaction.LowAffinityThreshold;
            float bonus = TSSFrameworkConfig.Interaction.PokeAffinityBonus;
            
            TriggerExpression(SelectExpressionByAffinity(affinity, ExpressionType.Surprised, ExpressionType.Happy), duration: 2f);
            AddFloatingText(InteractionPhrases.GetPokePhrase(affinity), GetTextColorByAffinity(affinity));
            
            if (affinity >= highThreshold)
                ModifyAffinity(bonus, "身体戳戳互动");
            else if (affinity < lowThreshold)
                ModifyAffinity(-0.5f, "烦人的触碰");
        }
        
        /// <summary>
        /// ? 激活触摸模式
        /// </summary>
        private void ActivateTouchMode()
        {
            isTouchModeActive = true;
            touchCount = 0;
            lastMousePosition = Event.current.mousePosition;
            
            TriggerExpression(ExpressionType.Confused, duration: 2f);
            StartBorderFlash(1);
            AddFloatingText("(?ω?)?", new Color(0.8f, 0.9f, 1f));
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
        }
        
        /// <summary>
        /// ? 触摸移动事件
        /// </summary>
        private void OnTouchMove(Vector2 mousePos)
        {
            touchCount++;
            
            float moveSpeed = Vector2.Distance(mousePos, lastMousePosition) / Time.deltaTime;
            
            if (moveSpeed > 500f)
            {
                TriggerExpression(ExpressionType.Shy, duration: 1.5f);
                AddFloatingText("(/ω＼)", new Color(1f, 0.6f, 0.6f));
            }
            else if (touchCount % 3 == 0)
            {
                var expression = touchExpressions[UnityEngine.Random.Range(0, touchExpressions.Length)];
                TriggerExpression(expression, duration: 2f);
                
                string[] emojis = { "(?▽｀)", "(๑˃ᴗ˂)✧", "(≧▽≦)", "ヾ(◍°∇°◍)ﾉ", "(๑˃̵ᴗ˂̵)" };
                AddFloatingText(emojis[UnityEngine.Random.Range(0, emojis.Length)], new Color(1f, 0.8f, 0.9f));
            }
            
            if (touchCount >= 10)
            {
                OnTouchCombo();
                touchCount = 0;
            }
        }
        
        /// <summary>
        /// ? 连续触摸奖励（🏗️ 使用配置值）
        /// </summary>
        private void OnTouchCombo()
        {
            bool isHappy = UnityEngine.Random.value > 0.3f;
            TriggerExpression(isHappy ? ExpressionType.Happy : ExpressionType.Smug, duration: 3f);
            
            AddFloatingText(isHappy ? "(*^▽^*)" : "(￣︶￣)↗", new Color(1f, 0.7f, 0.3f));
            StartBorderFlash(3);
            
            float bonus = TSSFrameworkConfig.Interaction.TouchComboAffinityBonus;
            ModifyAffinity(bonus, "全身立绘触摸互动");
            Messages.Message($"好感度 +{bonus}（全身立绘互动）", MessageTypeDefOf.PositiveEvent);
        }
        
        // ==================== 自定义浮动文字系统 ====================
        
        /// <summary>
        /// ? 浮动文字类
        /// </summary>
        private class UIFloatingText
        {
            public string text;
            public Vector2 position;
            public float timer;
            public Color color;
            public float maxLifetime = 2f;
            
            public UIFloatingText(string text, Vector2 position, Color color)
            {
                this.text = text;
                this.position = position;
                this.color = color;
                this.timer = 0f;
            }
            
            public bool Update(float deltaTime)
            {
                timer += deltaTime;
                
                // 向上移动
                position.y -= 30f * deltaTime;
                
                return timer < maxLifetime;
            }
            
            public void Draw()
            {
                float alpha = Mathf.Lerp(1f, 0f, timer / maxLifetime);
                Color drawColor = new Color(color.r, color.g, color.b, alpha);
                
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = drawColor;
                
                Rect textRect = new Rect(position.x - 100f, position.y - 15f, 200f, 30f);
                Widgets.Label(textRect, text);
                
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
            }
        }
        
        /// <summary>
        /// ? 添加浮动文字
        /// </summary>
        private void AddFloatingText(string text, Color color)
        {
            Vector2 startPos = new Vector2(drawRect.center.x, drawRect.y + 50f);
            floatingTexts.Add(new UIFloatingText(text, startPos, color));
        }
        
        /// <summary>
        /// ? 绘制所有浮动文字
        /// </summary>
        private void DrawFloatingTexts()
        {
            float deltaTime = Time.deltaTime;
            
            for (int i = floatingTexts.Count - 1; i >= 0; i--)
            {
                var text = floatingTexts[i];
                
                if (!text.Update(deltaTime))
                {
                    floatingTexts.RemoveAt(i);
                }
                else
                {
                    text.Draw();
                }
            }
        }
        
        // ==================== 辅助绘制方法 ====================
        
        /// <summary>
        /// ? 绘制交互UI（进度条、指示器等）
        /// </summary>
        private void DrawInteractionUI(Rect inRect)
        {
            // 悬停进度条
            if (isHovering && !isTouchModeActive)
            {
                float progress = (Time.realtimeSinceStartup - hoverStartTime) / HOVER_ACTIVATION_TIME;
                DrawHoverProgress(inRect, progress);
            }
            
            // 头部摸摸进度条
            if (headRubProgress > 0f)
            {
                DrawHeadRubProgress(inRect, headRubProgress / HEAD_RUB_THRESHOLD);
            }
            
            // 触摸模式指示器
            if (isTouchModeActive)
            {
                DrawTouchModeIndicator(inRect);
            }
            
            // 边框闪烁
            DrawBorderFlash(inRect);
        }
        
        private void DrawProgressBar(Rect barRect, float progress, Color startColor, Color endColor)
        {
            progress = Mathf.Clamp01(progress);
            Widgets.DrawBoxSolid(barRect, new Color(0.2f, 0.2f, 0.2f, 0.6f));
            Widgets.DrawBoxSolid(new Rect(barRect.x, barRect.y, barRect.width * progress, barRect.height),
                                Color.Lerp(startColor, endColor, progress));
        }
        
        private void DrawHoverProgress(Rect inRect, float progress)
            => DrawProgressBar(new Rect(inRect.x, inRect.yMax + 2f, inRect.width, 8f), progress,
                              new Color(0.3f, 0.8f, 1f), new Color(1f, 0.8f, 0.3f));
        
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
        /// ? v1.6.44: 运行时分层绘制立绘（Runtime Layering）
        /// ? v1.6.53: 修复半透明问题 - 使用 Graphics.DrawTexture 替代 GUI.DrawTexture
        /// 🛡️ v1.6.79: 使用 TSS_AssetLoader 静默加载资源
        /// 关键修复：
        /// - 使用缓存的 cachedBodyBase（静态层）
        /// - 每帧动态获取眼睛和嘴巴图层（动画层）
        /// - ? 使用 Graphics.DrawTexture 正确应用 GUI.color 的 alpha 值
        /// - 👻 幽灵模式下只绘制身体层，避免透明度叠加
        /// </summary>
        private void DrawLayeredPortraitRuntime(Rect rect, NarratorPersonaDef persona, bool isGhostMode)
        {
            string personaName = GetPersonaName(persona);
            
            // ==================== ⭐ Layer 1: 身体层（姿态覆盖或默认） ====================
            
            if (!string.IsNullOrEmpty(overridePosture))
            {
                // ⭐ 动画中：使用 TSS_AssetLoader 静默加载姿态纹理
                Texture2D postureTexture = TSS_AssetLoader.LoadDescentPosture(personaName, overridePosture, null);
                
                if (postureTexture != null)
                {
                    // ⭐ 强制高度对齐：以高度为基准计算宽度，保持宽高比
                    float aspect = (float)postureTexture.width / postureTexture.height;
                    float targetHeight = rect.height;
                    float targetWidth = targetHeight * aspect;
                    
                    // 水平居中绘制
                    float xOffset = (rect.width - targetWidth) / 2f;
                    Rect drawRect = new Rect(rect.x + xOffset, rect.y, targetWidth, targetHeight);
                    
                    GUI.DrawTexture(drawRect, postureTexture);
                }
                else
                {
                    // 🛡️ 静默降级：绘制默认身体层（不输出警告）
                    if (cachedBodyBase != null)
                    {
                        Widgets.DrawTextureFitted(rect, cachedBodyBase, 1.0f);
                    }
                    else
                    {
                        // 最终降级：绘制颜色占位符
                        DrawMinimalPlaceholder(rect, persona);
                    }
                }
                
                // ⭐ 降临姿态不支持分层和动态表情，绘制完成后直接返回
                return;
            }
            else
            {
                // ⭐ 平时：绘制默认身体层
                if (cachedBodyBase == null)
                {
                    // 🛡️ 如果没有缓存，绘制最小占位符
                    DrawMinimalPlaceholder(rect, persona);
                    return;
                }
                
                Widgets.DrawTextureFitted(rect, cachedBodyBase, 1.0f);
            }
            
            // 👻 幽灵模式：只绘制身体层，跳过所有面部细节
            if (isGhostMode) return;

            // ==================== Layer 2: 嘴巴层（动态加载，张嘴动画） ====================
            
            string mouthLayerName = MouthAnimationSystem.GetMouthLayerName(persona.defName);
            if (!string.IsNullOrEmpty(mouthLayerName))
            {
                var mouthTexture = PortraitLoader.GetLayerTexture(persona, mouthLayerName);
                if (mouthTexture != null)
                {
                    Widgets.DrawTextureFitted(rect, mouthTexture, 1.0f);
                }
            }
            
            // ==================== Layer 3: 眼睛层（动态加载，眨眼动画） ====================
            
            string eyeLayerName = BlinkAnimationSystem.GetEyeLayerName(persona.defName);
            if (!string.IsNullOrEmpty(eyeLayerName))
            {
                var eyeTexture = PortraitLoader.GetLayerTexture(persona, eyeLayerName);
                if (eyeTexture != null)
                {
                    Widgets.DrawTextureFitted(rect, eyeTexture, 1.0f);
                }
            }
            
            // ==================== Layer 4: 特效层（可选：腮红等） ====================
            
            var expressionState = ExpressionSystem.GetExpressionState(persona.defName);
            if (expressionState.CurrentExpression == ExpressionType.Shy ||
                expressionState.CurrentExpression == ExpressionType.Angry)
            {
                string flushLayerName = expressionState.CurrentExpression == ExpressionType.Shy ?
                    "flush_shy" : "flush_angry";
                var flushTexture = PortraitLoader.GetLayerTexture(persona, flushLayerName);
                if (flushTexture != null)
                {
                    Widgets.DrawTextureFitted(rect, flushTexture, 1.0f);
                }
            }
        }
        
        // ==================== 辅助方法 ====================
        
        /// <summary>
        /// ? v1.6.44: 更新基础身体层缓存（仅在人格变化时重新加载）
        /// </summary>
        private void UpdateBodyBaseIfNeeded()
        {
            if (currentPersona == null)
            {
                cachedBodyBase = null;
                cachedPersonaDefName = null;
                return;
            }
            
            // 检查是否需要更新缓存
            // ⭐ v1.6.92: 使用 Unity 的隐式 bool 转换检查纹理是否仍然有效
            // 避免引用已销毁的纹理（CleanOldCache 可能销毁了 base_body）
            if (cachedPersonaDefName == currentPersona.defName && cachedBodyBase != null && cachedBodyBase)
            {
                return; // 缓存有效，无需更新
            }
            
            // 加载基础身体层（静态层）
            // 加载基础身体层（静态层）- 日志已静默
            cachedBodyBase = PortraitLoader.GetLayerTexture(currentPersona, "base_body");
            
            // 如果找不到 base_body，尝试 body 或 base
            if (cachedBodyBase == null)
            {
                cachedBodyBase = PortraitLoader.GetLayerTexture(currentPersona, "body");
            }
            if (cachedBodyBase == null)
            {
                cachedBodyBase = PortraitLoader.GetLayerTexture(currentPersona, "base");
            }
            
            cachedPersonaDefName = currentPersona.defName;
        }
        
        private InteractionPhrases.InteractionZone GetInteractionZone(Rect rect, Vector2 mousePos)
        {
            if (!rect.Contains(mousePos))
            {
                return InteractionPhrases.InteractionZone.None;
            }
            
            float relativeY = (mousePos.y - rect.y) / rect.height;
            
            if (relativeY < 0.25f)
            {
                return InteractionPhrases.InteractionZone.Head;
            }
            
            if (IsOpaquePixel(rect, mousePos))
            {
                return InteractionPhrases.InteractionZone.Body;
            }
            
            return InteractionPhrases.InteractionZone.None;
        }
        
        private bool IsOpaquePixel(Rect rect, Vector2 mousePos)
        {
            float relativeX = (mousePos.x - rect.x) / rect.width;
            float relativeY = (mousePos.y - rect.y) / rect.height;
            
            bool inCentralArea = relativeX > 0.1f && relativeX < 0.9f &&
                                relativeY > 0.1f && relativeY < 0.9f;
            
            return inCentralArea;
        }
        
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
                
                var expressionState = ExpressionSystem.GetExpressionState(persona.defName);
                if (expressionState.CurrentExpression != lastExpression)
                {
                    lastExpression = expressionState.CurrentExpression;
                }
            }
            catch (System.Exception ex)
            {
                Log.Warning($"[FullBodyPortraitPanel] 更新立绘失败: {ex.Message}");
                currentPersona = null;
            }
        }
        
        private void TriggerExpression(ExpressionType expression, float duration = 2f)
        {
            if (currentPersona == null) return;
            
            ExpressionSystem.SetExpression(currentPersona.defName, expression, (int)(duration * 60), "立绘交互");
            
            if (lastExpression != expression)
            {
                LayeredPortraitCompositor.ClearCache(currentPersona.defName, lastExpression);
            }
        }
        
        private void RestoreDefaultExpression()
        {
            if (currentPersona == null) return;
            
            var agent = Current.Game?.GetComponent<Storyteller.StorytellerAgent>();
            if (agent != null)
            {
                float affinity = agent.GetAffinity();
                var mood = agent.currentMood;
                
                ExpressionType defaultExpression;
                
                if (mood == Storyteller.MoodState.Melancholic || 
                    mood == Storyteller.MoodState.Angry)
                {
                    defaultExpression = ExpressionType.Sad;
                }
                else
                {
                    defaultExpression = affinity switch
                    {
                        > 80f => ExpressionType.Shy,
                        > 40f => ExpressionType.Happy,
                        > -20f => ExpressionType.Neutral,
                        > -60f => ExpressionType.Sad,
                        _ => ExpressionType.Angry
                    };
                }
                
                TriggerExpression(defaultExpression, duration: 99999f);
            }
            else
            {
                TriggerExpression(ExpressionType.Neutral, duration: 99999f);
            }
        }
        
        private Color GetTextColorByAffinity(float affinity)
        {
            // 🏗️ 使用配置类的颜色和阈值
            float highThreshold = TSSFrameworkConfig.Interaction.HighAffinityThreshold;
            float lowThreshold = TSSFrameworkConfig.Interaction.LowAffinityThreshold;
            
            return affinity >= highThreshold ? TSSFrameworkConfig.Colors.HighAffinityTextColor
                 : affinity >= lowThreshold ? TSSFrameworkConfig.Colors.NeutralAffinityTextColor
                 : TSSFrameworkConfig.Colors.LowAffinityTextColor;
        }
        
        private void ModifyAffinity(float delta, string reason)
        {
            var agent = Current.Game?.GetComponent<Storyteller.StorytellerAgent>();
            if (agent != null)
            {
                agent.ModifyAffinity(delta, reason);
            }
        }
        
        private void StartBorderFlash(int count)
        {
            borderFlashStartTime = Time.realtimeSinceStartup;
            borderFlashCount = count;
        }
        
        private string BuildTooltip(bool shiftHeld)
        {
            if (currentPersona == null) return "";
            
            string tooltip = $"{currentPersona.narratorName}\n表情: {lastExpression}";
            
            if (!shiftHeld)
            {
                tooltip += "\n\n?? 按住 Shift 键激活互动模式";
            }
            else
            {
                tooltip += "\n\n? 互动模式已激活";
                
                var zone = GetInteractionZone(drawRect, Event.current.mousePosition);
                if (zone == InteractionPhrases.InteractionZone.Head)
                {
                    tooltip += $"\n\n?? 头部区域 | 摸摸进度: {headRubProgress:F0}/{HEAD_RUB_THRESHOLD}";
                }
                else if (zone == InteractionPhrases.InteractionZone.Body)
                {
                    tooltip += "\n\n?? 身体区域 | 单击戳戳";
                }
                
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
            
            return tooltip;
        }
        
        /// <summary>
        /// ⭐ 计算动画 Alpha 值（淡入/保持/淡出）
        /// </summary>
        private float CalculateAnimationAlpha()
        {
            if (!isPlayingAnimation || animationDuration <= 0f)
            {
                return 1.0f;
            }
            
            float progress = animationTimer / animationDuration;
            
            // 淡入阶段（0 - 10%）
            if (progress < 0.1f)
            {
                return Mathf.Lerp(0f, 1f, progress / 0.1f);
            }
            // 保持阶段（10% - 90%）
            else if (progress < 0.9f)
            {
                return 1.0f;
            }
            // 淡出阶段（90% - 100%）
            else
            {
                return Mathf.Lerp(1f, 0f, (progress - 0.9f) / 0.1f);
            }
        }
        
        /// <summary>
        /// ⭐ 绘制特效层（最顶层，Alpha 混合）
        /// 🛡️ v1.6.79: 使用 TSS_AssetLoader 静默加载
        /// </summary>
        private void DrawEffectLayer(Rect rect)
        {
            if (string.IsNullOrEmpty(activeEffect) || currentPersona == null) return;
            
            string personaName = GetPersonaName(currentPersona);
            
            // 🛡️ 使用 TSS_AssetLoader 静默加载特效纹理
            Texture2D effectTexture = TSS_AssetLoader.LoadDescentEffect(personaName, activeEffect, null);
            
            if (effectTexture == null)
            {
                // 🛡️ 静默处理：特效不存在时跳过绘制
                return;
            }
            
            // 计算特效 Alpha（脉冲效果）
            float effectAlpha = 0.5f + 0.5f * Mathf.Sin(animationTimer * 3f);
            
            // 应用特效颜色
            Color originalColor = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, effectAlpha * GUI.color.a);
            
            // 绘制特效
            Widgets.DrawTextureFitted(rect, effectTexture, 1.0f);
            
            // 恢复颜色
            GUI.color = originalColor;
        }
        
        // ==================== 🛡️ 资源检查和占位符方法 ====================
        
        /// <summary>
        /// 🛡️ 检查当前人格是否有可用的立绘资源
        /// </summary>
        private bool HasAvailablePortrait()
        {
            if (currentPersona == null) return false;
            
            string personaName = GetPersonaName(currentPersona);
            
            // 使用 TSS_AssetLoader 检查立绘存在性
            return TSS_AssetLoader.HasPortrait(personaName);
        }
        
        /// <summary>
        /// 🛡️ 检查是否有降临模式资源
        /// </summary>
        public bool HasDescentResources()
        {
            if (currentPersona == null) return false;
            
            string personaName = GetPersonaName(currentPersona);
            return TSS_AssetLoader.HasDescentResources(personaName);
        }
        
        /// <summary>
        /// 🛡️ 绘制无立绘时的占位符界面
        /// </summary>
        private void DrawNoPortraitPlaceholder()
        {
            // 绘制半透明背景
            GUI.color = new Color(0.15f, 0.15f, 0.2f, 0.6f);
            Widgets.DrawBoxSolid(drawRect, GUI.color);
            GUI.color = Color.white;
            
            // 绘制边框
            Widgets.DrawBox(drawRect, 2);
            
            // 绘制提示文字
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleCenter;
            
            string personaName = currentPersona?.narratorName ?? "Unknown";
            string message = $"{personaName}\n\n<color=#888888>立绘资源加载中...</color>";
            
            Rect textRect = drawRect.ContractedBy(20f);
            Widgets.Label(textRect, message);
            
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;
        }
        
        /// <summary>
        /// 🛡️ 绘制最小占位符（仅颜色块和名称）
        /// </summary>
        private void DrawMinimalPlaceholder(Rect rect, NarratorPersonaDef persona)
        {
            // 使用人格主题色绘制背景
            Color bgColor = persona.primaryColor;
            bgColor.a = 0.3f;
            Widgets.DrawBoxSolid(rect, bgColor);
            
            // 绘制边框
            GUI.color = new Color(persona.primaryColor.r, persona.primaryColor.g, persona.primaryColor.b, 0.8f);
            Widgets.DrawBox(rect, 1);
            GUI.color = Color.white;
            
            // 绘制人格名称（居中）
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = new Color(1f, 1f, 1f, 0.7f);
            
            Rect labelRect = new Rect(rect.x, rect.center.y - 15f, rect.width, 30f);
            Widgets.Label(labelRect, persona.narratorName);
            
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;
        }
        
        /// <summary>
        /// 🛡️ 获取人格名称（用于资源路径）
        /// ⭐ v1.6.90: 使用 GetResourceName() 确保本地化兼容性
        /// </summary>
        private string GetPersonaName(NarratorPersonaDef persona)
        {
            if (persona == null) return "";
            
            return persona.GetResourceName();
        }
    }
}
