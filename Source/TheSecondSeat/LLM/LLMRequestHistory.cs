using System;
using System.Collections.Generic;
using Verse;

namespace TheSecondSeat.LLM
{
    /// <summary>
    /// 记录 LLM 请求历史，用于调试和 Token 消耗监控
    /// </summary>
    public static class LLMRequestHistory
    {
        private const int MaxHistoryCount = 20;
        private static List<RequestLog> _logs = new List<RequestLog>();
        private static readonly object _lock = new object();

        public static List<RequestLog> Logs
        {
            get
            {
                lock (_lock)
                {
                    return new List<RequestLog>(_logs);
                }
            }
        }

        public static void Add(RequestLog log)
        {
            lock (_lock)
            {
                if (_logs.Count >= MaxHistoryCount)
                {
                    _logs.RemoveAt(0);
                }
                _logs.Add(log);
            }
        }

        public static void Clear()
        {
            lock (_lock)
            {
                _logs.Clear();
            }
        }
    }

    public class RequestLog
    {
        public DateTime Timestamp { get; set; }
        public string Endpoint { get; set; }
        public string Model { get; set; } // 可以从 Request Body 中解析，或者调用时传入
        public string RequestType { get; set; } // ⭐ v2.7.0: 请求类型（Chat, Vision, Agent, Test 等）
        public string RequestJson { get; set; }
        public string ResponseJson { get; set; }
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        
        public int PromptTokens { get; set; }
        public int CompletionTokens { get; set; }
        public int TotalTokens { get; set; }

        public float DurationSeconds { get; set; }

        /// <summary>
        /// ⭐ v2.7.0: 获取显示标签（用于 UI 列表）
        /// </summary>
        public string DisplayLabel
        {
            get
            {
                string type = !string.IsNullOrEmpty(RequestType) ? RequestType : "Chat";
                string model = !string.IsNullOrEmpty(Model) ? GetShortModelName(Model) : "?";
                return $"{type} ({model})";
            }
        }

        private string GetShortModelName(string fullModel)
        {
            if (string.IsNullOrEmpty(fullModel)) return "?";
            // 截取模型名称的关键部分
            if (fullModel.Contains("/"))
            {
                fullModel = fullModel.Substring(fullModel.LastIndexOf('/') + 1);
            }
            if (fullModel.Length > 12)
            {
                return fullModel.Substring(0, 10) + "..";
            }
            return fullModel;
        }

        public string Summary => $"[{Timestamp:HH:mm:ss}] {TotalTokens} tokens (In:{PromptTokens}/Out:{CompletionTokens}) - {(Success ? "OK" : "Fail")}";
    }
}
