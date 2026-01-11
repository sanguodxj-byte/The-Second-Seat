using UnityEngine;
using Verse;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using TheSecondSeat.Settings;

namespace TheSecondSeat.UI
{
    public class DialogueOverlayPanel : Window
    {
        private static List<string> messages = new List<string>();
        private static Vector2 scrollPosition = Vector2.zero;
        private const int MaxMessages = 50;

        private string currentFullMessage = "";
        private string currentDisplayedMessage = "";
        private bool isStreaming = false;
        private float charTimer = 0f;
        private float charsPerSecond = 20f;
        private bool isActionPart = false;
        private int currentCharIndex = 0;
        private static bool positionLoaded = false;
        
        // ? v1.6.96: 自动关闭计时器
        private float autoCloseTimer = 0f;
        private const float AutoCloseDelay = 5f;
        
        private static readonly Color BackgroundColor = new Color(0f, 0f, 0f, 0.8f); // ? v1.6.91: 80% 透明度
        private static readonly Color TextColor = new Color(0.9f, 0.9f, 0.9f, 1f);

        public override Vector2 InitialSize => new Vector2(600f, 200f);

        public DialogueOverlayPanel()
        {
            if (!positionLoaded)
            {
                if (TheSecondSeatMod.Settings.dialogueRect.width > 0 && TheSecondSeatMod.Settings.dialogueRect.height > 0)
                {
                    this.windowRect = TheSecondSeatMod.Settings.dialogueRect;
                }
                positionLoaded = true;
            }
            
            this.doCloseX = true;
            this.doCloseButton = false;
            this.draggable = true;
            this.resizeable = true;
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
            CompleteStreaming();
            messages.Add(text);
            if (messages.Count > MaxMessages)
            {
                messages.RemoveAt(0);
            }
            ScrollToBottom();
            autoCloseTimer = 0f; // 重置自动关闭计时器
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
                // 去除动作标签和空格，以获得更准确的语速计算
                string cleanMessage = System.Text.RegularExpressions.Regex.Replace(currentFullMessage, @"\([^)]*\)", "").Trim();
                charsPerSecond = cleanMessage.Length / audioDuration;
                // 设置一个最小速度，防止因音频过长导致文字太慢
                charsPerSecond = Mathf.Max(charsPerSecond, 3f);
            }
            else
            {
                // ? v1.6.91: 如果时长无效，则立即完成，而不是用默认速度
                Log.Warning($"[DialogueOverlayPanel] Invalid audio duration ({audioDuration:F2}s). Completing streaming immediately.");
                CompleteStreaming();
            }
        }


        public override void PreClose()
        {
            base.PreClose();
            TheSecondSeatMod.Settings.dialogueRect = this.windowRect;
            TheSecondSeatMod.Settings.Write();
        }

        public override void DoWindowContents(Rect inRect)
        {
            if (messages.Count == 0 && !isStreaming)
            {
                this.Close();
                return;
            }
            
            UpdateStreaming();
            
            // ? v1.6.96: 自动关闭逻辑
            if (!isStreaming)
            {
                autoCloseTimer += Time.deltaTime;
                if (autoCloseTimer >= AutoCloseDelay)
                {
                    this.Close();
                    return;
                }
            }

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
            }
        }
    }
}
