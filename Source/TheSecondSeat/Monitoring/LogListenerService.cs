using System;
using System.Collections.Generic;
using UnityEngine;
using TheSecondSeat.Core;
using Verse;
using TheSecondSeat.Settings;

namespace TheSecondSeat.Monitoring
{
    /// <summary>
    /// 监听 Unity 日志事件，用于捕获运行时错误
    /// 替代原有的 Tick 轮询机制，提高性能
    /// </summary>
    public class LogListenerService
    {
        private static LogListenerService instance;
        public static LogListenerService Instance => instance ?? (instance = new LogListenerService());

        private bool isListening = false;
        private Action<string, string> onErrorDetected;

        // 去重和冷却机制
        private Dictionary<string, float> lastErrorTimes = new Dictionary<string, float>();
        private float lastGlobalErrorTime = -999f;
        private const float SAME_ERROR_COOLDOWN = 15f; // 同一个错误15秒内只报一次
        private const float GLOBAL_ERROR_COOLDOWN = 3f; // 任意错误之间至少间隔3秒

        private LogListenerService() { }

        /// <summary>
        /// 初始化监听服务
        /// </summary>
        /// <param name="onErrorCallback">当检测到错误时的回调</param>
        public void Initialize(Action<string, string> onErrorCallback)
        {
            if (isListening) return;

            this.onErrorDetected = onErrorCallback;
            Application.logMessageReceived += HandleLogMessage;
            isListening = true;
            Log.Message("[LogListenerService] Initialized and listening for errors.");
        }

        /// <summary>
        /// 停止监听
        /// </summary>
        public void Shutdown()
        {
            if (!isListening) return;

            Application.logMessageReceived -= HandleLogMessage;
            isListening = false;
            onErrorDetected = null;
        }

        private void HandleLogMessage(string condition, string stackTrace, LogType type)
        {
            // 检查工程师模式是否开启
            if (!TheSecondSeatMod.Settings.engineerMode) return;

            // 只关注错误和异常
            if (type != LogType.Error && type != LogType.Exception) return;

            // 过滤掉非关键错误或已知错误（可根据需要扩展）
            if (ShouldIgnore(condition)) return;

            // 检查冷却时间
            float now = Time.realtimeSinceStartup;

            // 1. 全局冷却：防止短时间内大量不同错误爆发
            if (now - lastGlobalErrorTime < GLOBAL_ERROR_COOLDOWN) return;

            // 2. 特定错误冷却：防止同一个错误刷屏
            if (lastErrorTimes.TryGetValue(condition, out float lastTime))
            {
                if (now - lastTime < SAME_ERROR_COOLDOWN) return;
            }

            // 更新时间戳
            lastGlobalErrorTime = now;
            lastErrorTimes[condition] = now;

            // 简单的字典清理策略：如果太大就清空一次
            if (lastErrorTimes.Count > 100)
            {
                lastErrorTimes.Clear();
                lastErrorTimes[condition] = now;
            }

            // 触发回调
            try 
            {
                onErrorDetected?.Invoke(condition, stackTrace);
            }
            catch (Exception ex)
            {
                // 避免回调中的错误导致循环崩溃
                // 这里不能用 Log.Error，否则可能导致死循环，只能默默吞掉或打印到控制台
                Console.WriteLine($"[LogListenerService] Error in callback: {ex.Message}");
            }
        }

        private bool ShouldIgnore(string condition)
        {
            if (string.IsNullOrEmpty(condition)) return true;

            // 示例：忽略某些特定的无关紧要的 Unity 错误
            // if (condition.Contains("Shader warning")) return true;

            return false;
        }
    }
}
