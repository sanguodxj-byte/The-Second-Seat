using UnityEngine;
using Verse;
using RimWorld;
using TheSecondSeat.Core;
using TheSecondSeat.Settings; // 添加引用

namespace TheSecondSeat.UI
{
    /// <summary>
    /// 快速对话窗口
    /// ? 右键 AI 按钮打开，快速发送消息
    /// ? ESC 关闭
    /// ? 极简设计：输入框、发送按钮、快速回复按钮
    /// </summary>
    public class QuickDialogueWindow : Window
    {
        private string userInput = "";
        private const float WindowWidth = 400f;
        private const float InputHeight = 24f;  // ? 减小单行高度
        private const float QuickButtonHeight = 28f;  // 快速按钮高度
        private const float Padding = 8f;  // ? 减小内边距
        private const float WindowHeight = 120f;  // ? 固定总高度，确保所有元素可见
        private const float SendButtonWidth = 60f;
        
        // ? 用于跟踪是否需要发送
        private bool pendingSend = false;
        private string pendingMessage = "";
        
        public override Vector2 InitialSize => new Vector2(WindowWidth, WindowHeight);

        public QuickDialogueWindow()
        {
            this.doCloseButton = false;
            this.doCloseX = false;  // ? 移除关闭按钮
            this.closeOnClickedOutside = true;
            this.closeOnCancel = true;  // ESC 关闭
            this.absorbInputAroundWindow = true;
            this.forcePause = false;  // 不暂停游戏
            this.draggable = true;
            this.resizeable = false;
            this.preventCameraMotion = false;
        }

        protected override void SetInitialSizeAndPosition()
        {
            Vector2 savedPos = TheSecondSeatMod.Settings.QuickDialoguePos;
            
            // 如果有保存的位置（x >= 0），则使用它
            if (savedPos.x >= 0 && savedPos.y >= 0)
            {
                // 确保位置在屏幕范围内
                float x = Mathf.Clamp(savedPos.x, 0f, Verse.UI.screenWidth - WindowWidth);
                float y = Mathf.Clamp(savedPos.y, 0f, Verse.UI.screenHeight - WindowHeight);
                this.windowRect = new Rect(x, y, WindowWidth, WindowHeight);
            }
            else
            {
                // 否则居中显示
                float x = (Verse.UI.screenWidth - WindowWidth) / 2f;
                float y = (Verse.UI.screenHeight - WindowHeight) / 2f;
                this.windowRect = new Rect(x, y, WindowWidth, WindowHeight);
            }
        }

        public override void PreClose()
        {
            base.PreClose();
            // 保存位置
            TheSecondSeatMod.Settings.QuickDialoguePos = this.windowRect.position;
            TheSecondSeatMod.Settings.Write();
        }

        public override void PreOpen()
        {
            base.PreOpen();
            userInput = "";
            pendingSend = false;
            pendingMessage = "";
        }

        public override void DoWindowContents(Rect inRect)
        {
            float curY = Padding;
            
            // 输入框区域（单行输入）
            float inputWidth = inRect.width - SendButtonWidth - Padding * 2 - 5f;
            float inputAreaHeight = InputHeight + 4f;  // 单行高度 + 一点边距
            
            // 输入框（使用 TextField 单行输入，Enter 键不会换行）
            var inputRect = new Rect(Padding, curY, inputWidth, inputAreaHeight);

            // 处理键盘事件：Enter发送
            // 🔧 修复: 移到 SetNextControlName 之前
            // 同时增加 Input.GetKeyDown 检查作为备用 (根据用户反馈)
            bool isEnterPressed = (Event.current.type == EventType.KeyDown && (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter));

            if (GUI.GetNameOfFocusedControl() == "QuickDialogueInput" && isEnterPressed)
            {
                if (!string.IsNullOrWhiteSpace(userInput))
                {
                    Event.current.Use(); // 消耗事件，防止换行
                    pendingSend = true;
                    pendingMessage = userInput;
                }
            }

            // 使用 TextField 代替 TextArea（单行输入，Enter 不会换行）
            GUI.SetNextControlName("QuickDialogueInput"); // 🔧 确保紧贴控件调用
            string text = Widgets.TextField(inputRect, userInput);
            if (text != userInput)
            {
                userInput = text;
            }
            
            // 自动聚焦输入框 (仅在刚打开且没有焦点时聚焦)
            if (GUI.GetNameOfFocusedControl() != "QuickDialogueInput" && string.IsNullOrEmpty(GUI.GetNameOfFocusedControl()))
            {
                GUI.FocusControl("QuickDialogueInput");
            }
            
            // 发送按钮（垂直居中于输入框）
            var sendButtonRect = new Rect(inputRect.xMax + 5f, curY + (inputAreaHeight - InputHeight) / 2f, SendButtonWidth, InputHeight);
            if (Widgets.ButtonText(sendButtonRect, "TSS_QuickDialogue_Send".Translate()))
            {
                if (!string.IsNullOrWhiteSpace(userInput))
                {
                    pendingSend = true;
                    pendingMessage = userInput;
                }
            }
            
            curY += inputAreaHeight + Padding;
            
            // ? 快速回复按钮区域
            float quickButtonWidth = (inRect.width - Padding * 3) / 2f;
            
            // 同意按钮
            var agreeRect = new Rect(Padding, curY, quickButtonWidth, QuickButtonHeight);
            GUI.color = new Color(0.3f, 0.8f, 0.3f);  // 绿色
            if (Widgets.ButtonText(agreeRect, "TSS_QuickDialogue_Agree".Translate()))
            {
                pendingSend = true;
                pendingMessage = "TSS_QuickDialogue_AgreeMsg".Translate();
            }
            GUI.color = Color.white;
            
            // 拒绝按钮
            var rejectRect = new Rect(agreeRect.xMax + Padding, curY, quickButtonWidth, QuickButtonHeight);
            GUI.color = new Color(0.8f, 0.3f, 0.3f);  // 红色
            if (Widgets.ButtonText(rejectRect, "TSS_QuickDialogue_Reject".Translate()))
            {
                pendingSend = true;
                pendingMessage = "TSS_QuickDialogue_RejectMsg".Translate();
            }
            GUI.color = Color.white;
            
            // ? 在渲染完成后处理发送（避免在渲染中修改状态）
            if (pendingSend)
            {
                pendingSend = false;
                SendMessage(pendingMessage);
            }
        }

        private void SendMessage(string message)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(message))
                {
                    return;
                }
                
                var controller = Current.Game?.GetComponent<NarratorController>();
                if (controller == null)
                {
                    Messages.Message("叙事者控制器未找到", MessageTypeDefOf.RejectInput);
                    this.Close();
                    return;
                }

                // 添加到聊天历史
                NarratorWindow.AddAIMessage($"[你]: {message}", "");
                
                // 触发 AI 响应
                controller.TriggerNarratorUpdate(message);
                
                // 关闭窗口
                this.Close();
            }
            catch (System.Exception ex)
            {
                Log.Error($"[QuickDialogueWindow] 发送消息失败: {ex}");
                Messages.Message("发送失败：" + ex.Message, MessageTypeDefOf.RejectInput);
                this.Close();
            }
        }
    }
}
