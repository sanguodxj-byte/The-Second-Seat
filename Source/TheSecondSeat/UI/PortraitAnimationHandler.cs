using UnityEngine;
using Verse;
using System;
using TheSecondSeat.Utils;

namespace TheSecondSeat.UI
{
    /// <summary>
    /// 管理全身立绘的姿态动画。
    /// 从 FullBodyPortraitPanel 中分离出来的独立系统。
    /// </summary>
    public class PortraitAnimationHandler
    {
        // 动画状态
        public string OverridePosture { get; private set; }
        public string ActiveEffect { get; private set; }
        public bool IsPlayingAnimation { get; private set; }

        private Action onAnimationComplete;
        private float animationTimer;
        private float animationDuration;
        
        private readonly FullBodyPortraitPanel panel;

        public PortraitAnimationHandler(FullBodyPortraitPanel panel)
        {
            this.panel = panel;
        }

        /// <summary>
        /// ⭐ 触发姿态动画
        /// </summary>
        public bool TriggerAnimation(string postureName, string effectName, float duration, Action callback = null)
        {
            // 🛡️ 检查姿态资源是否存在
            if (!string.IsNullOrEmpty(postureName))
            {
                string personaName = panel.GetPersonaResourceName();
                if (string.IsNullOrEmpty(personaName)) return false;

                var postureTexture = TSS_AssetLoader.LoadDescentPosture(personaName, postureName, null);
                if (postureTexture == null)
                {
                    if (Prefs.DevMode)
                    {
                        Log.Warning($"[PortraitAnimationHandler] 姿态资源不存在，跳过动画: {postureName}");
                    }
                    callback?.Invoke();
                    return false;
                }
            }

            // 初始化动画状态
            OverridePosture = postureName;
            ActiveEffect = effectName;
            animationDuration = duration;
            animationTimer = 0f;
            onAnimationComplete = callback;
            IsPlayingAnimation = true;

            return true;
        }

        /// <summary>
        /// ⭐ 停止当前动画并恢复默认状态
        /// </summary>
        public void StopAnimation()
        {
            if (!IsPlayingAnimation) return;

            try
            {
                onAnimationComplete?.Invoke();
            }
            catch (Exception ex)
            {
                Log.Error($"[PortraitAnimationHandler] 动画回调异常: {ex}");
            }

            // 清除动画状态
            OverridePosture = null;
            ActiveEffect = null;
            animationTimer = 0f;
            animationDuration = 0f;
            onAnimationComplete = null;
            IsPlayingAnimation = false;
        }

        /// <summary>
        /// ⭐ 每帧更新动画状态
        /// </summary>
        public void Update()
        {
            if (!IsPlayingAnimation) return;

            animationTimer += Time.deltaTime;

            if (animationTimer >= animationDuration)
            {
                StopAnimation();
            }
        }
        
        /// <summary>
        /// ⭐ 计算动画 Alpha 值（淡入/保持/淡出）
        /// </summary>
        public float CalculateAnimationAlpha()
        {
            if (!IsPlayingAnimation || animationDuration <= 0f)
            {
                return 1.0f;
            }

            float progress = animationTimer / animationDuration;

            if (progress < 0.1f)
                return Mathf.Lerp(0f, 1f, progress / 0.1f);
            else if (progress < 0.9f)
                return 1.0f;
            else
                return Mathf.Lerp(1f, 0f, (progress - 0.9f) / 0.1f);
        }
    }
}