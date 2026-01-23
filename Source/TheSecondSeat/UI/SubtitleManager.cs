using UnityEngine;
using Verse;

namespace TheSecondSeat.UI
{
    /// <summary>
    /// 管理字幕的显示和打字机效果
    /// </summary>
    public class SubtitleManager
    {
        private static SubtitleManager _instance;
        public static SubtitleManager Instance => _instance ??= new SubtitleManager();

        private string fullText = "";
        private string currentText = "";
        private float charTimer = 0f;
        private float displayTimer = 0f;
        private float charsPerSecond = 30f;
        private bool isTyping = false;
        private bool isVisible = false;

        public string CurrentText => currentText;
        public bool IsVisible => isVisible;

        /// <summary>
        /// 显示字幕
        /// </summary>
        /// <param name="text">文本内容</param>
        /// <param name="duration">预计持续时间（用于调整打字速度，-1为自动）</param>
        public void ShowSubtitle(string text, float duration = -1f)
        {
            if (string.IsNullOrEmpty(text)) return;

            fullText = text;
            currentText = "";
            charTimer = 0f;
            isTyping = true;
            isVisible = true;

            if (duration > 0)
            {
                // 根据持续时间调整打字速度
                // 假设打字占用的时间是音频时长的 80% 到 100%
                float typingDuration = duration * 0.9f;
                charsPerSecond = text.Length / typingDuration;
                
                // 限制速度范围，避免过快或过慢
                charsPerSecond = Mathf.Clamp(charsPerSecond, 5f, 60f);
                
                // 显示停留时间 = 音频时长 + 缓冲
                displayTimer = duration + 1.0f;
            }
            else
            {
                charsPerSecond = 30f; // 默认速度
                float estimatedTypingTime = text.Length / charsPerSecond;
                displayTimer = estimatedTypingTime + 3.0f;
            }
        }

        /// <summary>
        /// 每帧更新（需在 OnGUI 或 Update 中调用）
        /// </summary>
        public void Update(float deltaTime)
        {
            if (!isVisible) return;

            // 打字机效果
            if (isTyping)
            {
                charTimer += deltaTime;
                if (charTimer >= 1f / charsPerSecond)
                {
                    charTimer = 0f;
                    if (currentText.Length < fullText.Length)
                    {
                        currentText += fullText[currentText.Length];
                    }
                    else
                    {
                        isTyping = false;
                    }
                }
            }
            else
            {
                // 停留倒计时
                displayTimer -= deltaTime;
                if (displayTimer <= 0f)
                {
                    isVisible = false;
                }
            }
        }

        /// <summary>
        /// 强制隐藏
        /// </summary>
        public void Hide()
        {
            isVisible = false;
            currentText = "";
        }
    }
}