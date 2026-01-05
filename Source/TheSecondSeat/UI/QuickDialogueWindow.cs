using UnityEngine;
using Verse;
using RimWorld;
using TheSecondSeat.Core;

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
            // 居中显示
            float x = (Verse.UI.screenWidth - WindowWidth) / 2f;
            float y = (Verse.UI.screenHeight - WindowHeight) / 2f;
            this.windowRect = new Rect(x, y, WindowWidth, WindowHeight);
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
            
            // ? 输入框区域（单倍高度，紧凑显示）
            float inputWidth = inRect.width - SendButtonWidth - Padding * 2 - 5f;
            float inputAreaHeight = InputHeight * 1.5f;  // ? 1.5倍高度，足够显示1-2行
            
            // 输入框（使用 TextArea 支持多行）
            GUI.SetNextControlName("QuickDialogueInput");
            var inputRect = new Rect(Padding, curY, inputWidth, inputAreaHeight);
            userInput = Widgets.TextArea(inputRect, userInput);
            
            // ? 自动聚焦输入框
            GUI.FocusControl("QuickDialogueInput");
            
            // 发送按钮（垂直居中于输入框）
            var sendButtonRect = new Rect(inputRect.xMax + 5f, curY + (inputAreaHeight - InputHeight) / 2f, SendButtonWidth, InputHeight);
            if (Widgets.ButtonText(sendButtonRect, "发送"))
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
            if (Widgets.ButtonText(agreeRect, "? 同意 / 好的"))
            {
                pendingSend = true;
                pendingMessage = "好的，我同意。";
            }
            GUI.color = Color.white;
            
            // 拒绝按钮
            var rejectRect = new Rect(agreeRect.xMax + Padding, curY, quickButtonWidth, QuickButtonHeight);
            GUI.color = new Color(0.8f, 0.3f, 0.3f);  // 红色
            if (Widgets.ButtonText(rejectRect, "? 拒绝 / 不要"))
            {
                pendingSend = true;
                pendingMessage = "不，我拒绝。";
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
