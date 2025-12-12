using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace TheSecondSeat.PersonaGeneration
{
    /// <summary>
    /// 呼吸模式枚举
    /// 定义不同情境下的呼吸样式
    /// </summary>
    public enum BreathingMode
    {
        Calm,        // 平静 - 慢速、小振幅
        Normal,      // 正常 - 中速、中振幅
        Excited,     // 兴奋 - 快速、大振幅
        Nervous,     // 紧张 - 非常快速、中等振幅
        Relaxed,     // 放松 - 很慢、很小振幅
        Intense      // 强烈 - 非常快速、非常大振幅
    }

    /// <summary>
    /// 高级呼吸动画系统
    /// 提供更丰富的呼吸动画控制，包括多种预设模式、平滑过渡和情绪同步
    /// </summary>
    public static class ExpressionSystem_WithBreathing
    {
        // 呼吸模式的参数配置
        private static readonly Dictionary<BreathingMode, BreathingParameters> breathingPresets = new Dictionary<BreathingMode, BreathingParameters>
        {
            {
                BreathingMode.Calm,
                new BreathingParameters
                {
                    speed = 0.35f,
                    amplitude = 1.2f,
                    smoothness = 0.9f,
                    description = "平静呼吸"
                }
            },
            {
                BreathingMode.Normal,
                new BreathingParameters
                {
                    speed = 0.5f,
                    amplitude = 2.0f,
                    smoothness = 0.8f,
                    description = "正常呼吸"
                }
            },
            {
                BreathingMode.Excited,
                new BreathingParameters
                {
                    speed = 0.9f,
                    amplitude = 3.2f,
                    smoothness = 0.6f,
                    description = "兴奋呼吸"
                }
            },
            {
                BreathingMode.Nervous,
                new BreathingParameters
                {
                    speed = 1.2f,
                    amplitude = 2.8f,
                    smoothness = 0.5f,
                    description = "紧张呼吸"
                }
            },
            {
                BreathingMode.Relaxed,
                new BreathingParameters
                {
                    speed = 0.25f,
                    amplitude = 0.8f,
                    smoothness = 0.95f,
                    description = "放松呼吸"
                }
            },
            {
                BreathingMode.Intense,
                new BreathingParameters
                {
                    speed = 1.5f,
                    amplitude = 4.0f,
                    smoothness = 0.4f,
                    description = "强烈呼吸"
                }
            }
        };

        /// <summary>
        /// 呼吸参数结构
        /// </summary>
        private class BreathingParameters
        {
            public float speed;        // 呼吸速度（秒/周期）
            public float amplitude;    // 呼吸振幅（像素）
            public float smoothness;   // 平滑度（0-1，越高越平滑）
            public string description; // 描述
        }

        /// <summary>
        /// 高级呼吸状态
        /// </summary>
        private class AdvancedBreathingState
        {
            public BreathingMode currentMode = BreathingMode.Normal;
            public BreathingMode targetMode = BreathingMode.Normal;
            public float modeTransitionProgress = 1f;  // 模式过渡进度（0-1）
            public float currentSpeed;
            public float currentAmplitude;
            public float targetSpeed;
            public float targetAmplitude;
        }

        private static Dictionary<string, AdvancedBreathingState> advancedStates = new Dictionary<string, AdvancedBreathingState>();

        /// <summary>
        /// ? 设置呼吸模式（带平滑过渡）
        /// </summary>
        public static void SetBreathingMode(string personaDefName, BreathingMode mode)
        {
            if (!advancedStates.ContainsKey(personaDefName))
            {
                // 初始化高级状态
                var preset = breathingPresets[mode];
                advancedStates[personaDefName] = new AdvancedBreathingState
                {
                    currentMode = mode,
                    targetMode = mode,
                    modeTransitionProgress = 1f,
                    currentSpeed = preset.speed,
                    currentAmplitude = preset.amplitude,
                    targetSpeed = preset.speed,
                    targetAmplitude = preset.amplitude
                };

                if (Prefs.DevMode)
                {
                    Log.Message($"[ExpressionSystem_WithBreathing] {personaDefName} 初始化呼吸模式: {preset.description}");
                }
                return;
            }

            var state = advancedStates[personaDefName];

            // 如果模式相同，跳过
            if (state.targetMode == mode && state.modeTransitionProgress >= 1f)
            {
                return;
            }

            // 开始过渡到新模式
            state.currentMode = state.targetMode;
            state.targetMode = mode;
            state.modeTransitionProgress = 0f;

            var targetPreset = breathingPresets[mode];
            state.targetSpeed = targetPreset.speed;
            state.targetAmplitude = targetPreset.amplitude;

            if (Prefs.DevMode)
            {
                Log.Message($"[ExpressionSystem_WithBreathing] {personaDefName} 切换呼吸模式: {breathingPresets[state.currentMode].description} → {targetPreset.description}");
            }
        }

        /// <summary>
        /// ? 根据表情自动选择呼吸模式
        /// </summary>
        public static void SyncBreathingWithExpression(string personaDefName, ExpressionType expression)
        {
            BreathingMode mode = expression switch
            {
                ExpressionType.Happy => BreathingMode.Excited,
                ExpressionType.Sad => BreathingMode.Calm,
                ExpressionType.Angry => BreathingMode.Intense,
                ExpressionType.Surprised => BreathingMode.Excited,
                ExpressionType.Worried => BreathingMode.Nervous,
                ExpressionType.Smug => BreathingMode.Relaxed,
                ExpressionType.Disappointed => BreathingMode.Calm,
                ExpressionType.Thoughtful => BreathingMode.Calm,
                ExpressionType.Annoyed => BreathingMode.Nervous,
                ExpressionType.Playful => BreathingMode.Excited,
                ExpressionType.Shy => BreathingMode.Nervous,
                ExpressionType.Confused => BreathingMode.Normal,
                _ => BreathingMode.Normal
            };

            SetBreathingMode(personaDefName, mode);
        }

        /// <summary>
        /// ? 更新呼吸模式过渡
        /// 应该在每帧调用，以实现平滑的模式过渡
        /// </summary>
        public static void UpdateBreathingTransition(string personaDefName, float deltaTime)
        {
            if (!advancedStates.ContainsKey(personaDefName))
            {
                return;
            }

            var state = advancedStates[personaDefName];

            // 如果正在过渡
            if (state.modeTransitionProgress < 1f)
            {
                // 根据平滑度计算过渡速度
                var currentPreset = breathingPresets[state.currentMode];
                var targetPreset = breathingPresets[state.targetMode];
                float transitionSpeed = Mathf.Lerp(currentPreset.smoothness, targetPreset.smoothness, state.modeTransitionProgress);

                // 更新过渡进度
                state.modeTransitionProgress += deltaTime * transitionSpeed;
                state.modeTransitionProgress = Mathf.Clamp01(state.modeTransitionProgress);

                // 平滑插值参数
                state.currentSpeed = Mathf.Lerp(currentPreset.speed, state.targetSpeed, state.modeTransitionProgress);
                state.currentAmplitude = Mathf.Lerp(currentPreset.amplitude, state.targetAmplitude, state.modeTransitionProgress);
            }
        }

        /// <summary>
        /// ? 获取当前呼吸模式的描述
        /// </summary>
        public static string GetCurrentBreathingDescription(string personaDefName)
        {
            if (!advancedStates.ContainsKey(personaDefName))
            {
                return "未初始化";
            }

            var state = advancedStates[personaDefName];
            var currentPreset = breathingPresets[state.currentMode];

            if (state.modeTransitionProgress < 1f)
            {
                var targetPreset = breathingPresets[state.targetMode];
                return $"{currentPreset.description} → {targetPreset.description} ({state.modeTransitionProgress:P0})";
            }

            return currentPreset.description;
        }

        /// <summary>
        /// ? 获取所有可用的呼吸模式
        /// </summary>
        public static List<BreathingMode> GetAllBreathingModes()
        {
            return new List<BreathingMode>(breathingPresets.Keys);
        }

        /// <summary>
        /// ? 清除高级呼吸状态
        /// </summary>
        public static void ClearAdvancedBreathingState(string personaDefName)
        {
            if (advancedStates.ContainsKey(personaDefName))
            {
                advancedStates.Remove(personaDefName);
            }
        }

        /// <summary>
        /// ? 清除所有高级呼吸状态
        /// </summary>
        public static void ClearAllAdvancedBreathingStates()
        {
            advancedStates.Clear();
        }

        /// <summary>
        /// ? 获取调试信息
        /// </summary>
        public static string GetDebugInfo()
        {
            var info = $"[ExpressionSystem_WithBreathing] 高级呼吸状态数量: {advancedStates.Count}\n";

            foreach (var kvp in advancedStates)
            {
                var state = kvp.Value;
                info += $"  {kvp.Key}:\n";
                info += $"    当前模式: {GetCurrentBreathingDescription(kvp.Key)}\n";
                info += $"    速度: {state.currentSpeed:F2}\n";
                info += $"    振幅: {state.currentAmplitude:F2}\n";
            }

            return info;
        }
    }
}
