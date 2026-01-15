using System;
using TheSecondSeat.Monitoring;
using TheSecondSeat.RimAgent.Tools;
using Verse;

namespace TheSecondSeat.Core.Components
{
    /// <summary>
    /// Monitors runtime errors and triggers AI intervention if needed
    /// </summary>
    public class NarratorRuntimeMonitor
    {
        private int ticksSinceLastErrorCheck = 0;
        private const int ErrorCheckInterval = 300; // 5秒
        private string lastHandledError = "";
        
        // Callback to trigger AI update
        private readonly Action<string> triggerUpdateCallback;

        public NarratorRuntimeMonitor(Action<string> updateCallback)
        {
            triggerUpdateCallback = updateCallback;
        }

        public void Tick(bool isProcessing)
        {
            // ? 自动错误检测与修复循环
            // 每 5 秒检查一次是否有新的红字错误
            ticksSinceLastErrorCheck++;
            if (ticksSinceLastErrorCheck >= ErrorCheckInterval)
            {
                ticksSinceLastErrorCheck = 0;
                CheckForRuntimeErrors(isProcessing);
            }
        }

        /// <summary>
        /// ? 检查运行时错误并触发 AI 自动修复
        /// </summary>
        private void CheckForRuntimeErrors(bool isProcessing)
        {
            // 如果 AI 正在处理中，跳过本次检查，避免打断
            if (isProcessing) return;

            // 检查 LogAnalysisTool 是否捕获到新错误
            string currentError = LogAnalysisTool.LastErrorMessage;

            // 如果有错误，且该错误未被处理过（或者是新的错误内容）
            if (!string.IsNullOrEmpty(currentError) && currentError != lastHandledError)
            {
                lastHandledError = currentError;
                
                // ? 只有在开发者模式或特定设置下才启用自动修复建议
                // 这里我们假设如果安装了这个 Mod，用户就期望有这个功能
                // 但为了不打扰正常游戏，我们只针对看起来像 XML 配置错误的报错进行积极干预
                // 或者我们可以总是提示，让 AI 决定是否值得打扰玩家

                Log.Message($"[NarratorController] 自动检测到新错误，正在唤醒 AI 工程师: {currentError}");

                // 构建系统警报消息
                // 引导 AI 使用 analyze_last_error 工具
                string alertMessage = $"[SYSTEM ALERT] A runtime error has been detected: \"{currentError}\". " +
                                      "Please use the 'analyze_last_error' tool to investigate the cause. " +
                                      "If it looks like a configuration typo (e.g. in XML), try to fix it using 'patch_file'. " +
                                      "If you cannot fix it, briefly explain the issue to the player.";

                // 触发 AI 更新，传入警报消息
                // 这将启动 ReAct 循环
                triggerUpdateCallback?.Invoke(alertMessage);
            }
        }
    }
}
