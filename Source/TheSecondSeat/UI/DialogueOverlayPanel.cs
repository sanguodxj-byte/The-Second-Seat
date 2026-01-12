using UnityEngine;
using Verse;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using TheSecondSeat.Settings;
using TheSecondSeat.Narrator;

namespace TheSecondSeat.UI
{
    public class DialogueOverlayPanel : Window
    {
        private static List<string> messages = new List<string>();
        private static Vector2 scrollPosition = Vector2.zero;
        private const int MaxMessages = 50;
        
        // 自定义缩放状态
        private bool isResizing = false;

        private string currentFullMessage = "";
        private string currentDisplayedMessage = "";
        private bool isStreaming = false;
        private float charTimer = 0f;
        private float charsPerSecond = 20f;
        private bool isActionPart = false;
        private int currentCharIndex = 0;
        
        // ? v1.6.96: 自动关闭计时器
        private float autoCloseTimer = 0f;
        // private const float AutoCloseDelay = 6f; // 改为6秒
        private float audioEndTime = -1f; // 音频结束时间
        private float streamingEndTime = -1f; // 🔧 v1.6.98: 流式传输完成时间
        // private const float MinDisplayTimeAfterStreaming = 3f; // 🔧 v1.6.98: 流式传输完成后最少显示时间
        
        private static readonly Color BackgroundColor = new Color(0f, 0f, 0f, 0.3f); // ? v1.6.91: 70% 透明度 (0.3 alpha)
        private static readonly Color TextColor = new Color(0.9f, 0.9f, 0.9f, 1f);

        // 扁平化滚动条样式
        private static GUIStyle flatScrollbarStyle;
        private static GUIStyle flatScrollbarThumbStyle;
        private static Texture2D scrollbarThumbTex;

        public override Vector2 InitialSize => new Vector2(300f, 100f);

        public DialogueOverlayPanel()
        {
            // 优先从 NarratorManager (存档) 获取位置
            var narratorManager = Current.Game?.GetComponent<NarratorManager>();
            if (narratorManager != null && narratorManager.DialogueOverlayRect.HasValue)
            {
                this.windowRect = narratorManager.DialogueOverlayRect.Value;
            }
            // 否则，从全局设置加载
            else if (TheSecondSeatMod.Settings.dialogueRect.width > 0 && TheSecondSeatMod.Settings.dialogueRect.height > 0)
            {
                this.windowRect = TheSecondSeatMod.Settings.dialogueRect;
            }
            
            this.doCloseX = false; // 隐藏右上角X
            this.doCloseButton = false;
            this.draggable = true;
            this.resizeable = false; // 隐藏右下角标识
            this.absorbInputAroundWindow = false;
            this.closeOnClickedOutside = false;
            this.closeOnCancel = false;
            this.focusWhenOpened = false;
            this.preventCameraMotion = false;
            this.soundAppear = null;
            this.soundClose = null;
            this.forcePause = false;
            this.drawShadow = false; // ? v1.6.91: 移除阴影
            this.optionalTitle = null; // ? v1.6.91: 移除标题
            this.doWindowBackground = false; // ? v1.6.91: 禁用默认背景
        }
        
        public static void AddMessage(string text)
        {
            // Find existing window or create a new one
            var existingWindow = Find.WindowStack.Windows.OfType<DialogueOverlayPanel>().FirstOrDefault();
            if (existingWindow != null)
            {
                existingWindow.InternalAddMessage(text);
                if (!Find.WindowStack.IsOpen<DialogueOverlayPanel>())
                {
                     Find.WindowStack.Add(existingWindow);
                }
            }
            else
            {
                var newWindow = new DialogueOverlayPanel();
                newWindow.InternalAddMessage(text);
                Find.WindowStack.Add(newWindow);
            }
        }
        
        public static void SetStreamingMessage(string text)
        {
             var existingWindow = Find.WindowStack.Windows.OfType<DialogueOverlayPanel>().FirstOrDefault();
            if (existingWindow == null)
            {
                existingWindow = new DialogueOverlayPanel();
                Find.WindowStack.Add(existingWindow);
            }
            existingWindow.InternalSetStreamingMessage(text);
        }

        public static void StartStreaming(float audioDuration)
        {
            var existingWindow = Find.WindowStack.Windows.OfType<DialogueOverlayPanel>().FirstOrDefault();
            existingWindow?.InternalStartStreaming(audioDuration);
        }


        private void InternalAddMessage(string text)
        {
            // 改为使用流式传输逻辑，确保有足够的显示时间
            InternalSetStreamingMessage(text);
        }

        private void InternalSetStreamingMessage(string text)
        {
            CompleteStreaming();
            messages.Clear(); // Clear history for the new conversation
            currentFullMessage = text;
            currentDisplayedMessage = "";
            currentCharIndex = 0;
            isStreaming = true;
            charsPerSecond = 20f;
            isActionPart = false;
            audioEndTime = -1f; // 重置音频结束时间
            messages.Add("");
            if (messages.Count > MaxMessages)
            {
                messages.RemoveAt(0);
            }
            ScrollToBottom();
            autoCloseTimer = 0f; // 重置自动关闭计时器
        }

        private void InternalStartStreaming(float audioDuration)
        {
            // ? v1.6.91: 添加详细日志用于调试同步问题
            Log.Message($"[DialogueOverlayPanel] InternalStartStreaming called. Duration: {audioDuration:F2}s. Message: '{currentFullMessage}'");

            if (!isStreaming || string.IsNullOrEmpty(currentFullMessage)) return;

            if (audioDuration > 0.1f) // ? v1.6.91: 只有在有效时长时才计算速度
            {
                audioEndTime = Time.realtimeSinceStartup + audioDuration;
                
                // 去除动作标签和空格，以获得更准确的语速计算
                // 支持英文括号、中文括号和星号
                string cleanMessage = System.Text.RegularExpressions.Regex.Replace(currentFullMessage, @"(\(.*?\)|（.*?）|\*.*?\*)", "").Trim();
                
                // 如果清理后为空（全是动作），则给一个默认长度避免除零或过快
                float effectiveLength = Mathf.Max(cleanMessage.Length, 1f);
                
                charsPerSecond = effectiveLength / audioDuration;
                // 设置一个最小速度，防止因音频过长导致文字太慢
                charsPerSecond = Mathf.Max(charsPerSecond, 3f);
            }
            else
            {
                // 如果时长无效，保持默认速度，不要立即完成，以确保文本有足够时间展示
                Log.Warning($"[DialogueOverlayPanel] Invalid audio duration ({audioDuration:F2}s). Using default speed.");
                // CompleteStreaming(); // 移除此行，避免过早结束流式传输
            }
        }


        public override void PreClose()
        {
            base.PreClose();
            SaveWindowPosition();
        }
        
        private void SaveWindowPosition()
        {
            // 保存到全局设置
            TheSecondSeatMod.Settings.dialogueRect = this.windowRect;
            TheSecondSeatMod.Settings.Write();
            
            // 保存到存档 (NarratorManager)
            var narratorManager = Current.Game?.GetComponent<NarratorManager>();
            if (narratorManager != null)
            {
                narratorManager.DialogueOverlayRect = this.windowRect;
            }
        }

        public override void DoWindowContents(Rect inRect)
        {
            UpdateStreaming();
            
            // ? v1.6.99: 移除自动清除逻辑 - 对话框将一直显示直到下一条消息到来
            // 用户反馈：更改悬浮对话框显示逻辑，在下次对话开始输出后才清除旧对话

            // ? v1.6.91: 手动绘制背景
            Widgets.DrawBoxSolid(inRect, BackgroundColor);

            // ? v1.6.91: 修改边距为 5px
            Rect innerRect = inRect.ContractedBy(5f);
            float contentHeight = 0f;
            float width = innerRect.width - 16f;

            for (int i = 0; i < messages.Count; i++)
            {
                contentHeight += Text.CalcHeight(messages[i], width) + 5f;
            }

            Rect viewRect = new Rect(0, 0, width, contentHeight);
            
            // 初始化扁平化样式
            EnsureStyles();
            
            // 临时替换样式
            var oldScrollbar = GUI.skin.verticalScrollbar;
            var oldThumb = GUI.skin.verticalScrollbarThumb;
            
            try
            {
                GUI.skin.verticalScrollbar = flatScrollbarStyle;
                GUI.skin.verticalScrollbarThumb = flatScrollbarThumbStyle;

                Widgets.BeginScrollView(innerRect, ref scrollPosition, viewRect);

                float curY = 0f;
                Text.Font = GameFont.Small;
                GUI.color = TextColor;

                for (int i = 0; i < messages.Count; i++)
                {
                    string msg = messages[i];
                    float h = Text.CalcHeight(msg, width);
                    Rect msgRect = new Rect(0, curY, width, h);
                    Widgets.Label(msgRect, msg);
                    curY += h + 5f;
                }
                GUI.color = Color.white;

                Widgets.EndScrollView();
            }
            finally
            {
                // 恢复样式
                GUI.skin.verticalScrollbar = oldScrollbar;
                GUI.skin.verticalScrollbarThumb = oldThumb;
            }
            
            // 自定义缩放逻辑 (隐藏右下角标识但保留功能)
            Rect resizeRect = new Rect(inRect.width - 15f, inRect.height - 15f, 15f, 15f);
            
            if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && resizeRect.Contains(Event.current.mousePosition))
            {
                isResizing = true;
                Event.current.Use();
            }
            
            if (isResizing)
            {
                if (Event.current.type == EventType.MouseDrag)
                {
                    this.windowRect.width = Mathf.Max(this.InitialSize.x, this.windowRect.width + Event.current.delta.x);
                    this.windowRect.height = Mathf.Max(this.InitialSize.y, this.windowRect.height + Event.current.delta.y);
                    Event.current.Use();
                }
                else if (Event.current.type == EventType.MouseUp)
                {
                    isResizing = false;
                    SaveWindowPosition();
                    Event.current.Use();
                }
            }
        }

        private void ScrollToBottom()
        {
            scrollPosition.y = 99999f;
        }

        private void CompleteStreaming()
        {
            if (isStreaming && messages.Count > 0)
            {
                messages[messages.Count - 1] = currentFullMessage;
            }
            isStreaming = false;
            currentFullMessage = "";
            currentDisplayedMessage = "";
            currentCharIndex = 0;
            autoCloseTimer = 0f; // 流式传输完成时重置计时器，开始倒计时
        }

        private void UpdateStreaming()
        {
            if (!isStreaming) return;
            float speed = isActionPart ? charsPerSecond * 5f : charsPerSecond;
            charTimer += Time.deltaTime * speed;

            while (charTimer >= 1f && currentCharIndex < currentFullMessage.Length)
            {
                charTimer -= 1f;
                char c = currentFullMessage[currentCharIndex];
                if (c == '*' || c == '(' || c == '（') isActionPart = true;
                if (c == '*' || c == ')' || c == '）') isActionPart = false;

                currentDisplayedMessage += c;
                messages[messages.Count - 1] = currentDisplayedMessage;
                currentCharIndex++;
                ScrollToBottom();
            }

            if (currentCharIndex >= currentFullMessage.Length)
            {
                isStreaming = false;
                streamingEndTime = Time.realtimeSinceStartup; // 🔧 v1.6.98: 记录流式传输完成时间
                autoCloseTimer = 0f; // 流式传输完成后重置计时器
            }
        }

        private void EnsureStyles()
        {
            if (flatScrollbarStyle == null)
            {
                flatScrollbarStyle = new GUIStyle(GUI.skin.verticalScrollbar);
                flatScrollbarStyle.normal.background = null; // 无背景轨道
                flatScrollbarStyle.fixedWidth = 6f;
            }

            if (scrollbarThumbTex == null)
            {
                // 创建半透明灰色纹理 (50% 不透明度)
                scrollbarThumbTex = SolidColorMaterials.NewSolidColorTexture(new Color(0.5f, 0.5f, 0.5f, 0.5f));
            }

            if (flatScrollbarThumbStyle == null)
            {
                flatScrollbarThumbStyle = new GUIStyle(GUI.skin.verticalScrollbarThumb);
                flatScrollbarThumbStyle.normal.background = scrollbarThumbTex;
                flatScrollbarThumbStyle.hover.background = scrollbarThumbTex;
                flatScrollbarThumbStyle.active.background = scrollbarThumbTex;
                flatScrollbarThumbStyle.fixedWidth = 6f;
            }
        }
    }
}
