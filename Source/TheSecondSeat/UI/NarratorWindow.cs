using RimWorld;
using UnityEngine;
using Verse;
using TheSecondSeat.Core;
using TheSecondSeat.Narrator;
using TheSecondSeat.PersonaGeneration; // ? 添加命名空间
using System.Collections.Generic;
using System.Linq; // ? 引入 Linq 命名空间

namespace TheSecondSeat.UI
{
    /// <summary>
    /// 现代化聊天界面 UI - 深色主题，无 emoji
    /// ? 不暂停游戏
    /// ? AI 回复显示在聊天框内
    /// </summary>
    public class NarratorWindow : Window
    {
        private string userInput = "";
        
        // ? 用于安全处理消息发送（避免在 Layout/Repaint 期间修改 UI 状态）
        private bool pendingSend = false;
        private string pendingMessage = "";

        private Vector2 chatScrollPosition = Vector2.zero;
        private NarratorController? controller;
        private NarratorManager? manager;
        
        // ? 聊天历史记录（公共静态，供外部访问）
        private static List<ChatMessage> chatHistory = new List<ChatMessage>();
        
        // ? 用于智能滚动的消息计数
        private static int lastMessageCount = 0;
        
        // ? 用于自动滚动到底部的标记
        private bool scrollToBottom = false;
        
        // ? 新生：动态头像相关
        private Texture2D? currentPortrait = null;
        private ExpressionType lastExpression = ExpressionType.Neutral;
        private int portraitUpdateTick = 0;
        private const int PORTRAIT_UPDATE_INTERVAL = 30; // 0.5秒更新一次

        // 深色低亮度配色
        private static readonly Color BackgroundDark = new Color(0.08f, 0.09f, 0.10f, 0.98f);
        private static readonly Color SidebarDark = new Color(0.10f, 0.11f, 0.12f, 1f);
        private static readonly Color MessageBubbleUser = new Color(0.15f, 0.30f, 0.60f, 0.9f);
        private static readonly Color MessageBubbleAI = new Color(0.16f, 0.17f, 0.18f, 0.95f);
        private static readonly Color InputBoxBg = new Color(0.12f, 0.13f, 0.14f, 1f);
        private static readonly Color AccentCyan = new Color(0.15f, 0.60f, 0.70f, 1f);
        private static readonly Color TextLight = new Color(0.85f, 0.85f, 0.85f, 1f);
        private static readonly Color TextDim = new Color(0.55f, 0.55f, 0.55f, 1f);

        public override Vector2 InitialSize => new Vector2(900f, 700f);

        /// <summary>
        /// ? 添加 AI 消息到聊天历史（供 NarratorController 调用）
        /// </summary>
        public static void AddAIMessage(string content, string emoticonId = "")
        {
            var msg = new ChatMessage
            {
                sender = "AI",
                content = content,
                timestamp = System.DateTime.Now
            };
            
            chatHistory.Add(msg);
        }

        /// <summary>
        /// ? 清空聊天历史（供外部调用）
        /// </summary>
        public static void ClearChatHistory()
        {
            chatHistory.Clear();
        }
        
        /// <summary>
        /// ? 设置输入框文本（供外部调用，如指令列表点击）
        /// </summary>
        public static void SetInputText(string text)
        {
            // 查找已打开的 NarratorWindow 实例
            var window = Find.WindowStack?.Windows
                .OfType<NarratorWindow>()
                .FirstOrDefault();
            
            if (window != null)
            {
                window.userInput = text;
                // 日志已静默：输入框设置
            }
        }
        
        /// <summary>
        /// ? 设置输入文本并自动发送
        /// </summary>
        public static void SetInputTextAndSend(string text)
        {
            // 查找已打开的 NarratorWindow 实例
            var window = Find.WindowStack?.Windows
                .OfType<NarratorWindow>()
                .FirstOrDefault();
            
            if (window != null)
            {
                window.userInput = text;
                window.SendMessage(text);
                window.userInput = "";  // 清空输入框
                // 日志已静默：自动发送
            }
        }

        public NarratorWindow()
        {
            doCloseButton = false;
            doCloseX = false;
            closeOnClickedOutside = false;
            closeOnCancel = true;  // ESC 可关闭
            closeOnAccept = false;
            absorbInputAroundWindow = false;
            draggable = true;
            resizeable = false; // ? 用户反馈：固定大小以避免布局问题
            
            // ? 不暂停游戏
            forcePause = false;
            preventCameraMotion = false;
            
            // ? 删除默认欢迎消息 - 聊天记录现在默认为空
            // 不再添加 "你好，我是 AI 叙事者..." 的默认消息
        }

        public override void WindowUpdate()
        {
            base.WindowUpdate();

            // ? 在 Update 中处理消息发送，避免在 OnGUI (Layout/Repaint) 期间修改 UI 状态导致 Layout Mismatch
            if (pendingSend)
            {
                pendingSend = false;
                SendMessage(pendingMessage);
            }

            // ? 在 Update 中更新动画状态，确保帧率独立且一致
            if (manager == null)
            {
                manager = Current.Game?.GetComponent<NarratorManager>();
            }
            
            if (manager != null)
            {
                var persona = manager.GetCurrentPersona();
                if (persona != null)
                {
                    // 更新呼吸动画 (60FPS)
                    ExpressionSystem_WithBreathing.UpdateBreathingTransition(persona.defName, 0.016f);
                }
            }
        }

        public override void DoWindowContents(Rect inRect)
        {
            try
            {
                // 获取组件
                if (controller == null)
                {
                    controller = Current.Game?.GetComponent<NarratorController>();
                }
                if (manager == null)
                {
                    manager = Current.Game?.GetComponent<NarratorManager>();
                }

                // 布局：左侧边栏 + 右侧聊天区域
                float sidebarWidth = 220f;
                Rect sidebarRect = new Rect(inRect.x, inRect.y, sidebarWidth, inRect.height);
                Rect chatAreaRect = new Rect(inRect.x + sidebarWidth + 10f, inRect.y,
                    inRect.width - sidebarWidth - 10f, inRect.height);

                DrawSidebar(sidebarRect);
                DrawChatArea(chatAreaRect);
            }
            catch (System.Exception ex)
            {
                // ? 捕获并记录 GUI 错误，防止整个 UI 崩溃
                // 只在 Repaint 事件中记录，避免刷屏
                if (Event.current.type == EventType.Repaint)
                {
                    Log.Warning($"[NarratorWindow] GUI Error: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 绘制左侧边栏 - 极简版：删除关闭按钮，立绘最大化
        /// </summary>
        private void DrawSidebar(Rect rect)
        {
            Widgets.DrawBoxSolid(rect, SidebarDark);
            
            var listing = new Listing_Standard();
            listing.Begin(rect.ContractedBy(15f));

            // ? 删除顶部标题，直接开始绘制立绘

            // 1. 人格立绘（占据最大空间）
            if (manager != null)
            {
                var persona = manager.GetCurrentPersona();
                if (persona != null)
                {
                    // ? 检查好感度系统是否启用
                    var settings = LoadedModManager.GetMod<Settings.TheSecondSeatMod>()?.GetSettings<Settings.TheSecondSeatSettings>();
                    bool affinityEnabled = settings?.enableAffinitySystem ?? true;
                    
                    // ? 计算立绘区域：如果好感度系统禁用，减少底部预留空间
                    float bottomContentHeight = affinityEnabled ? 215f : 180f; // 禁用时减少 35px（好感度条高度）
                    float remainingSpace = rect.height - 30f - bottomContentHeight;
                    float portraitHeight = Mathf.Max(250f, remainingSpace);
                    
                    DrawPersonaCardCompact(listing, persona, portraitHeight);
                }
            }

            // 2. 将剩余内容推到底部
            var settings2 = LoadedModManager.GetMod<Settings.TheSecondSeatMod>()?.GetSettings<Settings.TheSecondSeatSettings>();
            bool affinityEnabled2 = settings2?.enableAffinitySystem ?? true;
            float bottomContentHeight2 = affinityEnabled2 ? 215f : 180f;
            float spaceToFill = rect.height - listing.CurHeight - bottomContentHeight2 - 30f;
            if (spaceToFill > 0)
            {
                listing.Gap(spaceToFill);
            }

            // 3. 好感度显示（仅当好感度系统启用时显示）
            if (manager != null && affinityEnabled2)
            {
                listing.Gap(5f);
                
                var favorability = manager.Favorability;
                var tier = manager.CurrentTier;
                
                // ? 好感度条（包含等级和数值）
                var barRect = listing.GetRect(25f);
                DrawIntegratedFavorabilityBar(barRect, favorability, tier);
            }

            listing.Gap(12f);

            // 4. 快捷按钮区域（无标题）
            if (DrawModernButton(listing.GetRect(32f), "切换人格", AccentCyan))
            {
                Find.WindowStack.Add(new PersonaSelectionWindow(manager));
            }
            
            listing.Gap(4f);

            if (DrawModernButton(listing.GetRect(32f), "状态汇报", new Color(0.35f, 0.55f, 0.40f)))
            {
                SendMessage("请给我汇报殖民地状态");
            }
            
            listing.Gap(4f);

            if (DrawModernButton(listing.GetRect(32f), "指令列表", new Color(0.40f, 0.50f, 0.60f)))
            {
                Find.WindowStack.Add(new CommandListWindow());
            }
            
            listing.Gap(4f);

            if (DrawModernButton(listing.GetRect(32f), "清空聊天", new Color(0.60f, 0.30f, 0.30f)))
            {
                chatHistory.Clear();
                chatHistory.Add(new ChatMessage
                {
                    sender = "System",
                    content = "聊天记录已清空",
                    timestamp = System.DateTime.Now
                });
            }

            // ? 删除关闭按钮 - 使用ESC键关闭

            listing.End();
        }

        /// <summary>
        /// 绘制紧凑版人格卡片
        /// ? v1.6.34: 使用运行时分层绘制，支持眨眼和张嘴动画
        /// </summary>
        private void DrawPersonaCardCompact(Listing_Standard listing, PersonaGeneration.NarratorPersonaDef persona, float height)
        {
            var cardRect = listing.GetRect(height);
            
            // 去除外框
            Widgets.DrawBoxSolid(cardRect, new Color(0.12f, 0.13f, 0.14f, 0.8f));
            
            var innerRect = cardRect.ContractedBy(6f);
            
            // ? v1.6.34: 立绘区域（占据几乎全部空间，底部留 20px 给名字）
            var portraitRect = new Rect(innerRect.x, innerRect.y, innerRect.width, innerRect.height - 22f);
            
            // ? 应用呼吸动画偏移
            float breathingOffset = ExpressionSystem.GetBreathingOffset(persona.defName);
            
            // ? 动画更新已移至 WindowUpdate
            // ExpressionSystem_WithBreathing.UpdateBreathingTransition(persona.defName, 0.016f);
            
            portraitRect.y += breathingOffset;
            
            // ? v1.6.34: 运行时分层绘制（每帧重新组合）
            DrawLayeredPortraitRuntime(portraitRect, persona);

            // ? 生物节律心情指示器 (右上角)
            DrawBioRhythmIndicator(portraitRect);
            
            // 名字区域（底部，缩小字体）
            var nameRect = new Rect(innerRect.x, innerRect.yMax - 20f, innerRect.width, 18f);
            
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = AccentCyan;
            Widgets.Label(nameRect, persona.narratorName);
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
        }
        
        /// <summary>
        /// ? 绘制生物节律心情指示器
        /// </summary>
        private void DrawBioRhythmIndicator(Rect portraitRect)
        {
            var bioSystem = Current.Game?.GetComponent<NarratorBioRhythm>();
            if (bioSystem == null) return;

            var settings = LoadedModManager.GetMod<Settings.TheSecondSeatMod>()?.GetSettings<Settings.TheSecondSeatSettings>();
            if (settings != null && !settings.enableBioRhythm) return;

            // 直接获取公开属性
            float mood = bioSystem.CurrentMood;

            // 颜色映射
            Color moodColor;
            if (mood > 80f) { moodColor = new Color(1f, 0.8f, 0.2f); } // 金色
            else if (mood > 60f) { moodColor = new Color(0.4f, 0.8f, 0.4f); } // 绿色
            else if (mood > 40f) { moodColor = new Color(0.4f, 0.6f, 0.8f); } // 蓝色
            else if (mood > 20f) { moodColor = new Color(0.7f, 0.7f, 0.7f); } // 灰色
            else { moodColor = new Color(0.8f, 0.3f, 0.3f); } // 红色

            // 绘制指示器 (右上角小圆点)
            float indicatorSize = 12f;
            Rect indicatorRect = new Rect(portraitRect.xMax - indicatorSize - 8f, portraitRect.y + 8f, indicatorSize, indicatorSize);
            
            // 外圈光晕
            GUI.color = new Color(moodColor.r, moodColor.g, moodColor.b, 0.3f);
            Widgets.DrawBoxSolid(indicatorRect.ExpandedBy(2f), GUI.color);
            
            // 核心
            GUI.color = moodColor;
            Widgets.DrawBoxSolid(indicatorRect, moodColor);
            GUI.color = Color.white;

            // Tooltip
            if (Mouse.IsOver(indicatorRect))
            {
                TooltipHandler.TipRegion(indicatorRect, bioSystem.GetCurrentBioContext());
            }
        }

        /// <summary>
        /// ? v1.6.34: 运行时分层绘制立绘（支持眨眼和张嘴动画）
        /// ? v1.6.35: 修复路径错误，确保使用分层立绘系统
        /// </summary>
        private void DrawLayeredPortraitRuntime(Rect rect, PersonaGeneration.NarratorPersonaDef persona)
        {
            // ? v1.6.35: 移除useLayeredPortrait检查，强制使用分层系统
            // 原因：所有立绘文件已部署在 Layered/{PersonaName}/ 文件夹下
            
            // 1. 绘制基础身体层（始终显示）
            var baseBody = PortraitLoader.GetLayerTexture(persona, "base_body");
            if (baseBody == null)
            {
                // 回退到占位符
                if (Prefs.DevMode)
                {
                    Log.Warning($"[NarratorWindow] base_body not found for {persona.defName}");
                }
                Widgets.DrawBoxSolid(rect, persona.primaryColor);
                return;
            }
            GUI.DrawTexture(rect, baseBody, ScaleMode.ScaleToFit);
            
            // 2. ? 获取当前眼睛层（调用眨眼动画系统）
            string eyeLayerName = BlinkAnimationSystem.GetEyeLayerName(persona.defName);
            if (!string.IsNullOrEmpty(eyeLayerName))
            {
                // ? v1.12.3: 抑制警告，避免因缺失特定表情眼睛纹理而刷屏
                // PortraitLoader 内部已有失败计数机制
                var eyeTexture = PortraitLoader.GetLayerTexture(persona, eyeLayerName, suppressWarning: true);
                
                // 如果特定表情眼睛未找到，尝试回退到基础眼睛 (base_eyes 或 neutral_eyes)
                if (eyeTexture == null && eyeLayerName != "base_eyes" && eyeLayerName != "neutral_eyes")
                {
                    eyeTexture = PortraitLoader.GetLayerTexture(persona, "base_eyes", suppressWarning: true) 
                              ?? PortraitLoader.GetLayerTexture(persona, "neutral_eyes", suppressWarning: true);
                }

                if (eyeTexture != null)
                {
                    GUI.DrawTexture(rect, eyeTexture, ScaleMode.ScaleToFit);
                }
            }
            
            // 3. ? 获取当前嘴巴层（调用张嘴动画系统）
            string mouthLayerName = MouthAnimationSystem.GetMouthLayerName(persona.defName);
            if (!string.IsNullOrEmpty(mouthLayerName))
            {
                // ⭐ v1.8.3: 修复口型回退逻辑，优先使用子 Mod 纹理
                // 回退顺序：高保真口型 → Closed_mouth（子 Mod）→ Neutral_mouth → 不绘制
                var mouthTexture = PortraitLoader.GetLayerTexture(persona, mouthLayerName, suppressWarning: true);
                
                if (mouthTexture == null && mouthLayerName != "Closed_mouth")
                {
                    // 第1级回退：Closed_mouth（子 Mod 的闭嘴纹理）
                    mouthTexture = PortraitLoader.GetLayerTexture(persona, "Closed_mouth", suppressWarning: true);
                }
                
                if (mouthTexture == null && mouthLayerName != "Neutral_mouth")
                {
                    // 第2级回退：Neutral_mouth（子 Mod 的微张纹理）
                    mouthTexture = PortraitLoader.GetLayerTexture(persona, "Neutral_mouth", suppressWarning: true);
                }
                
                // 不再回退到 opened_mouth（主 Mod 旧纹理）

                if (mouthTexture != null)
                {
                    GUI.DrawTexture(rect, mouthTexture, ScaleMode.ScaleToFit);
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
        /// 绘制右侧聊天区域
        /// </summary>
        private void DrawChatArea(Rect rect)
        {
            Widgets.DrawBoxSolid(rect, BackgroundDark);
            
            // ? 增加输入区域高度，以适应多行输入
            float inputHeight = 130f; // 稍微增加高度以确保多行可见性
            Rect chatHistoryRect = new Rect(rect.x, rect.y, rect.width, rect.height - inputHeight - 15f);
            Rect inputAreaRect = new Rect(rect.x, rect.yMax - inputHeight, rect.width, inputHeight);

            DrawChatHistory(chatHistoryRect);
            DrawInputArea(inputAreaRect);
        }

        /// <summary>
        /// 绘制聊天历史
        /// ? 修复：滚动条能显示全部内容，新消息时自动滚动
        /// </summary>
        private void DrawChatHistory(Rect rect)
        {
            var innerRect = rect.ContractedBy(15f);
            
            // ? 创建消息副本，避免并发修改
            var messages = chatHistory.ToList();
            
            // 计算内容总高度
            float contentHeight = 20f; // 顶部padding
            foreach (var msg in messages)
            {
                // 注意：这里使用的宽度计算需要与 DrawChatMessage 中的保持一致
                // DrawChatMessage 中 bubbleWidth = rect.width * 0.7f
                // rect.width 在这里对应 innerRect.width - 20f (滚动条)
                float bubbleWidth = (innerRect.width - 20f) * 0.7f;
                
                // ? 修复：计算高度时，给宽度减去一个小的缓冲值 (5f)，防止因浮点数精度或 UI 缩放导致换行计算不一致
                // 如果 CalcHeight 认为宽度够不换行，但实际绘制时换行了，就会导致高度不够。
                // 减小计算宽度会强制 CalcHeight 倾向于算出更高的高度（更多换行），这是安全的。
                float textHeight = CalculateMessageHeight(msg, bubbleWidth - 20f - 5f);
                float totalMsgHeight = textHeight + 20f;
                
                // ? 修复累积误差：每条消息额外增加 10f 的预计算高度
                // 用户反馈随着消息增多截断越严重，说明存在每条消息的固定高度偏差。
                // 这里的 +10f 是为了抵消这种累积误差，宁可底部留白也不要截断。
                contentHeight += totalMsgHeight + 15f + 10f; // 消息高度 + 间距 + 累积误差缓冲
            }
            
            // ? 添加充足的底部padding，确保最后一条消息完全可见
            contentHeight += 200f;
            
            // ? viewRect高度必须大于等于contentHeight，否则滚动条无法到达底部
            var viewRect = new Rect(0, 0, innerRect.width - 20f, Mathf.Max(contentHeight, innerRect.height));
            
            Widgets.BeginScrollView(innerRect, ref chatScrollPosition, viewRect);
            try
            {
                float curY = 20f; // 从顶部padding开始
                foreach (var msg in messages)
                {
                    float msgHeight = DrawChatMessage(new Rect(0, curY, viewRect.width, 9999f), msg);
                    curY += msgHeight + 15f;
                }
            }
            finally
            {
                Widgets.EndScrollView();
            }
            
            // ? 新消息时自动滚动到底部
            if (messages.Count > lastMessageCount)
            {
                lastMessageCount = messages.Count;
                // ? 计算正确的滚动位置（内容高度 - 可见区域高度）
                float maxScroll = Mathf.Max(0, contentHeight - innerRect.height);
                chatScrollPosition.y = maxScroll;
            }
        }

        /// <summary>
        /// 绘制单条聊天消息
        /// ? 支持显示表情包
        /// </summary>
        private float DrawChatMessage(Rect rect, ChatMessage message)
        {
            bool isUser = message.sender == "User";
            bool isSystem = message.sender == "System";
            
            float bubbleWidth = rect.width * 0.7f;
            
            float textHeight = CalculateMessageHeight(message, bubbleWidth - 20f);
            float bubbleHeight = textHeight + 20f;
            
            Rect bubbleRect;
            Color bubbleColor;
            
            if (isSystem)
            {
                // 系统消息居中
                bubbleRect = new Rect((rect.width - bubbleWidth * 0.7f) / 2f, rect.y, 
                    bubbleWidth * 0.7f, bubbleHeight);
                bubbleColor = new Color(0.22f, 0.22f, 0.22f, 0.7f);
            }
            else if (isUser)
            {
                // 用户消息靠右
                bubbleRect = new Rect(rect.width - bubbleWidth - 10f, rect.y, bubbleWidth, bubbleHeight);
                bubbleColor = MessageBubbleUser;
            }
            else
            {
                // AI 消息靠左
                bubbleRect = new Rect(10f, rect.y, bubbleWidth, bubbleHeight);
                bubbleColor = MessageBubbleAI;
            }
            
            // 绘制气泡背景
            Widgets.DrawBoxSolid(bubbleRect, bubbleColor);
            
            // 绘制文字内容
            var textRect = bubbleRect.ContractedBy(10f);
            Text.Font = GameFont.Small;
            GUI.color = TextLight;
            Widgets.Label(textRect, message.content);
            GUI.color = Color.white;
            
            return bubbleHeight;
        }

        /// <summary>
        /// 计算消息高度
        /// </summary>
        private float CalculateMessageHeight(ChatMessage message, float width)
        {
            Text.Font = GameFont.Small;
            return Text.CalcHeight(message.content, width);
        }

        /// <summary>
        /// 绘制输入区域
        /// </summary>
        private void DrawInputArea(Rect rect)
        {
            var innerRect = rect.ContractedBy(15f, 10f);
            
            // 输入框背景
            Widgets.DrawBoxSolid(innerRect, InputBoxBg);
            Widgets.DrawBox(innerRect, 1);
            
            // 输入框 + 发送按钮布局
            float buttonWidth = 80f;
            // ? 增加输入框高度 (35f -> 80f) 以支持多行显示
            // 使用 TextArea 后，高度需要足够大以显示多行
            Rect textFieldRect = new Rect(innerRect.x + 10f, innerRect.y + 10f,
                innerRect.width - buttonWidth - 25f, innerRect.height - 20f);
            // ? 调整发送按钮位置到右下角
            Rect sendButtonRect = new Rect(innerRect.xMax - buttonWidth - 10f, innerRect.yMax - 45f,
                buttonWidth, 35f);

            // ? 输入框（设置控件名称）
            GUI.SetNextControlName("UserInputField");
            
            // ? 监听回车键发送 (Shift+Enter 换行)
            // 必须在 TextArea 绘制之前拦截，否则 TextArea 会先处理并插入换行符
            if (Event.current.type == EventType.KeyDown && 
                (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter) &&
                GUI.GetNameOfFocusedControl() == "UserInputField")
            {
                // 如果没有按下 Shift，则作为发送指令处理
                if (!Event.current.shift)
                {
                    if (!string.IsNullOrWhiteSpace(userInput))
                    {
                        pendingSend = true;
                        pendingMessage = userInput;
                        userInput = "";
                    }
                    // 无论是否发送，都吞噬掉这个 Enter 事件，防止在 TextArea 中换行
                    Event.current.Use();
                }
                // 如果按下了 Shift，不处理事件，让 TextArea 处理（插入换行）
            }

            Text.Font = GameFont.Small;
            
            // ? 绘制输入框 (改为 TextArea 以支持多行)
            // 注意：Widgets.TextArea 可能会在某些情况下吃掉 Enter 键，所以上面的拦截至关重要
            userInput = Widgets.TextArea(textFieldRect, userInput);

            // 发送按钮
            if (DrawModernButton(sendButtonRect, "发送", AccentCyan))
            {
                if (!string.IsNullOrWhiteSpace(userInput))
                {
                    pendingSend = true;
                    pendingMessage = userInput;
                    userInput = "";
                    GUI.FocusControl("UserInputField");
                }
            }
            
            // ? AI思考中的提示（如果正在处理）
            if (controller?.IsProcessing ?? false)
            {
                // ? 调整提示位置到输入框上方，避免被按钮遮挡
                var statusRect = new Rect(innerRect.x + 10f, innerRect.y - 18f, 
                    innerRect.width - 20f, 15f);
                Text.Font = GameFont.Tiny;
                GUI.color = new Color(0.90f, 0.75f, 0.30f);
                Widgets.Label(statusRect, "● AI 正在思考...");
                GUI.color = Color.white;
            }
            // ? 空输入框时的占位符提示（简洁版）
            else if (string.IsNullOrWhiteSpace(userInput))
            {
                var hintRect = new Rect(textFieldRect.x + 5f, textFieldRect.y,
                    textFieldRect.width - 10f, textFieldRect.height);
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleLeft;
                GUI.color = new Color(TextDim.r, TextDim.g, TextDim.b, 0.5f);
                Widgets.Label(hintRect, "输入消息...");
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
            }
        }

        /// <summary>
        /// 发送消息
        /// ? 添加：分析对话内容并更新表情
        /// </summary>
        private void SendMessage(string message)
        {
            // 添加用户消息到历史
            chatHistory.Add(new ChatMessage
            {
                sender = "User",
                content = message,
                timestamp = System.DateTime.Now
            });
            
            // ? 新增：分析用户消息的情感并更新表情
            if (manager != null)
            {
                var persona = manager.GetCurrentPersona();
                if (persona != null)
                {
                    ExpressionSystem.UpdateExpressionByDialogueTone(persona.defName, message);
                }
            }
            
            // 触发 AI 响应
            controller?.TriggerNarratorUpdate(message);
        }

        /// <summary>
        /// 绘制现代化按钮
        /// </summary>
        private bool DrawModernButton(Rect rect, string label, Color color)
        {
            bool mouseOver = Mouse.IsOver(rect);
            
            Color bgColor = mouseOver 
                ? new Color(color.r * 1.2f, color.g * 1.2f, color.b * 1.2f, color.a) 
                : color;
            
            Widgets.DrawBoxSolid(rect, bgColor);
            
            if (mouseOver)
            {
                Widgets.DrawBox(rect, 1);
            }
            
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = TextLight;
            Widgets.Label(rect, label);
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            
            return Widgets.ButtonInvisible(rect);
        }

        /// <summary>
        /// 绘制集成好感度条（包含等级名称和数值）
        /// ? 将等级名称和数值集成到条内部
        /// </summary>
        private void DrawIntegratedFavorabilityBar(Rect rect, float value, Narrator.AffinityTier tier)
        {
            // 归一化：-100~100 → 0~1
            var normalized = (value + 100f) / 200f;
            
            // 背景
            Widgets.DrawBoxSolid(rect, new Color(0.08f, 0.08f, 0.08f, 0.9f));
            
            // 填充条
            Color fillColor = GetFavorabilityColor(value);
            var fillRect = new Rect(rect.x, rect.y, rect.width * normalized, rect.height);
            Widgets.DrawBoxSolid(fillRect, fillColor);
            
            // 边框
            Widgets.DrawBox(rect, 1);
            
            // 中心标记线（0 的位置）
            float centerX = rect.x + rect.width * 0.5f;
            Widgets.DrawLine(
                new Vector2(centerX, rect.y),
                new Vector2(centerX, rect.yMax),
                new Color(1f, 1f, 1f, 0.3f),
                1f
            );
            
            // ? 集成文本：左侧显示等级，右侧显示数值
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleLeft;
            
            // 等级名称（左侧，带颜色）
            var tierRect = new Rect(rect.x + 5f, rect.y, rect.width * 0.6f, rect.height);
            GUI.color = GetTierColor(tier);
            Widgets.Label(tierRect, GetTierName(tier));
            
            // 数值（右侧，灰色）
            Text.Anchor = TextAnchor.MiddleRight;
            var valueRect = new Rect(rect.x, rect.y, rect.width - 5f, rect.height);
            GUI.color = new Color(0.8f, 0.8f, 0.8f, 0.9f);
            Widgets.Label(valueRect, $"{value:F0}");
            
            // 恢复
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
        }

        /// <summary>
        /// 获取关系等级颜色
        /// ? 更新为新的 8 级等级系统
        /// </summary>
        private Color GetTierColor(Narrator.AffinityTier tier)
        {
            return tier switch
            {
                Narrator.AffinityTier.Hatred => new Color(0.60f, 0.10f, 0.10f),      // 深红
                Narrator.AffinityTier.Hostile => new Color(0.80f, 0.25f, 0.25f),     // 红色
                Narrator.AffinityTier.Cold => new Color(0.55f, 0.55f, 0.75f),        // 冷蓝
                Narrator.AffinityTier.Indifferent => new Color(0.65f, 0.65f, 0.65f), // 灰色
                Narrator.AffinityTier.Warm => new Color(0.80f, 0.75f, 0.40f),        // 暖黄
                Narrator.AffinityTier.Devoted => new Color(0.80f, 0.50f, 0.75f),     // 粉紫
                Narrator.AffinityTier.Adoration => new Color(0.90f, 0.40f, 0.70f),   // 亮粉
                Narrator.AffinityTier.SoulBound => new Color(1.00f, 0.80f, 0.20f),   // 金色
                _ => Color.white
            };
        }

        /// <summary>
        /// 获取关系等级名称
        /// </summary>
        private string GetTierName(Narrator.AffinityTier tier)
        {
            return $"TSS_Tier_{tier}".Translate();
        }

        /// <summary>
        /// 获取好感度颜色（用于进度条）
        /// ? 更新为新的范围 -100~100
        /// </summary>
        private Color GetFavorabilityColor(float value)
        {
            if (value < -70f) return new Color(0.60f, 0.10f, 0.10f);  // 憎恨：深红
            if (value < -40f) return new Color(0.80f, 0.25f, 0.25f);  // 敌意：红色
            if (value < -10f) return new Color(0.70f, 0.50f, 0.70f);  // 疏远：冷蓝紫
            if (value < 10f) return new Color(0.65f, 0.65f, 0.65f);   // 冷淡：灰色
            if (value < 30f) return new Color(0.80f, 0.75f, 0.40f);   // 温暖：暖黄
            if (value < 60f) return new Color(0.80f, 0.50f, 0.75f);   // 倾心：粉紫
            if (value < 85f) return new Color(0.90f, 0.40f, 0.70f);   // 爱慕：亮粉
            return new Color(1.00f, 0.80f, 0.20f);                     // 魂之友：金色
        }

        /// <summary>
        /// 聊天消息数据结构
        /// </summary>
        private class ChatMessage
        {
            public string sender = "";
            public string content = "";
            public System.DateTime timestamp;
        }
    }

    /// <summary>
    /// Gizmo to open the narrator window
    /// </summary>
    public class Command_OpenNarrator : Command
    {
        public Command_OpenNarrator()
        {
            defaultLabel = "TSS_OpenNarratorLabel".Translate();
            defaultDesc = "TSS_OpenNarratorDesc".Translate();
            icon = ContentFinder<Texture2D>.Get("UI/Commands/Attack", false);
        }

        public override void ProcessInput(Event ev)
        {
            base.ProcessInput(ev);
            Find.WindowStack.Add(new NarratorWindow());
        }
    }
}
