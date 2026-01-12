using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace TheSecondSeat.RimAgent.Tools
{
    /// <summary>
    /// 日志分析工具 (听诊器)
    /// 用于捕获最近的红字错误，供AI分析原因
    /// ? v1.6.97: 增加错误去重和历史记录功能，防止刷屏导致丢失关键信息
    /// </summary>
    public class LogAnalysisTool : ITool
    {
        public string Name => "analyze_last_error";
        public string Description => "Analyzes recent game errors. Captures unique error messages and stack traces from the console to help diagnose bugs. Returns a summary of recent unique errors.";

        private static bool _initialized = false;
        
        // 使用字典存储唯一的错误信息，防止刷屏
        private static readonly Dictionary<string, ErrorData> _uniqueErrors = new Dictionary<string, ErrorData>();
        // 记录错误发生的顺序（Key）
        private static readonly List<string> _recentErrorKeys = new List<string>();
        // 最大存储的唯一错误数量
        private const int MaxUniqueErrors = 10;

        private class ErrorData
        {
            public string Message;
            public string StackTrace;
            public int Count;
            public DateTime LastTime;
        }

        // 在 Mod 启动时注册监听 (需要在 StaticConstructorOnStartup 或 Mod.ctor 中调用)
        /// <summary>
        /// 获取最近一次报错的信息（供 NarratorController 轮询检测）
        /// </summary>
        public static string LastErrorMessage
        {
            get
            {
                lock (_uniqueErrors)
                {
                    if (_recentErrorKeys.Count == 0) return "";
                    
                    // 获取最近的一个错误 Key
                    string lastKey = _recentErrorKeys[_recentErrorKeys.Count - 1];
                    
                    if (_uniqueErrors.TryGetValue(lastKey, out var errorData))
                    {
                        return errorData.Message;
                    }
                    return "";
                }
            }
        }
        public static void Init()
        {
            if (_initialized) return;
            
            Application.logMessageReceived += HandleLog;
            _initialized = true;
        }

        /// <summary>
        /// ✅ v1.7.0: 注销日志监听，防止内存泄漏
        /// </summary>
        public static void Cleanup()
        {
            if (!_initialized) return;

            Application.logMessageReceived -= HandleLog;
            _initialized = false;
        }

        private static void HandleLog(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception)
            {
                // 使用错误信息作为 Key 进行去重
                string key = condition;
                
                lock (_uniqueErrors)
                {
                    if (_uniqueErrors.ContainsKey(key))
                    {
                        // 已存在的错误：增加计数，更新时间
                        _uniqueErrors[key].Count++;
                        _uniqueErrors[key].LastTime = DateTime.Now;
                        
                        // 移动到最近列表的末尾
                        _recentErrorKeys.Remove(key);
                        _recentErrorKeys.Add(key);
                    }
                    else
                    {
                        // 新错误：如果满了，移除最旧的
                        if (_uniqueErrors.Count >= MaxUniqueErrors)
                        {
                            string oldestKey = _recentErrorKeys[0];
                            _recentErrorKeys.RemoveAt(0);
                            _uniqueErrors.Remove(oldestKey);
                        }

                        _uniqueErrors[key] = new ErrorData
                        {
                            Message = condition,
                            StackTrace = stackTrace,
                            Count = 1,
                            LastTime = DateTime.Now
                        };
                        _recentErrorKeys.Add(key);
                    }
                }
            }
        }

        public async Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
        {
            await Task.CompletedTask;

            lock (_uniqueErrors)
            {
                if (_uniqueErrors.Count == 0)
                {
                    return new ToolResult
                    {
                        Success = true,
                        Data = "No errors detected recently."
                    };
                }

                var sb = new StringBuilder();
                sb.AppendLine($"[DIAGNOSTIC REPORT - {_uniqueErrors.Count} Unique Errors Captured]");
                
                // 倒序遍历，显示最近的错误
                for (int i = _recentErrorKeys.Count - 1; i >= 0; i--)
                {
                    string key = _recentErrorKeys[i];
                    if (!_uniqueErrors.TryGetValue(key, out var error)) continue;
                    
                    sb.AppendLine($"\n--- Error #{_recentErrorKeys.Count - i} (Count: {error.Count}) ---");
                    sb.AppendLine($"Time: {error.LastTime:HH:mm:ss}");
                    sb.AppendLine($"Message: {error.Message}");
                    
                    // 最近的一条错误显示完整堆栈（限制长度），其他的显示简略堆栈
                    if (i == _recentErrorKeys.Count - 1)
                    {
                         string fullTrace = error.StackTrace.Length > 2000
                            ? error.StackTrace.Substring(0, 2000) + "\n...[Truncated]"
                            : error.StackTrace;
                         sb.AppendLine($"Stack Trace:\n{fullTrace}");
                    }
                    else
                    {
                        string traceSnippet = error.StackTrace.Length > 200
                            ? error.StackTrace.Substring(0, 200).Replace("\n", " ") + "..."
                            : error.StackTrace.Replace("\n", " ");
                        sb.AppendLine($"Stack Trace (Brief): {traceSnippet}");
                    }
                }

                // 智能提示
                if (sb.ToString().Contains(".xml") || sb.ToString().Contains(".json"))
                {
                    sb.AppendLine("\n[ANALYSIS HINT]");
                    sb.AppendLine("POTENTIAL CULPRIT FILE DETECTED IN LOG. Please check the file path mentioned in the error message.");
                }

                return new ToolResult
                {
                    Success = true,
                    Data = sb.ToString()
                };
            }
        }
        
        /// <summary>
        /// 清空错误日志缓存
        /// </summary>
        public static void Clear()
        {
            lock (_uniqueErrors)
            {
                _uniqueErrors.Clear();
                _recentErrorKeys.Clear();
            }
        }
    }
}
