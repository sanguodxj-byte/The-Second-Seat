using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Verse;

namespace TheSecondSeat.RimAgent.Tools
{
    /// <summary>
    /// ? v1.6.77: 极简版日志读取工具 - 只读，无风险，无需审批
    /// 
    /// 功能：
    /// - 自动定位 Player.log 文件
    /// - 读取最后 50 行（足够诊断报错）
    /// - 只读操作，无任何副作用
    /// 
    /// 使用场景：
    /// - 用户报告"红字报错"时，AI 自动读取日志分析
    /// - 诊断游戏崩溃原因
    /// - 查看最近的警告信息
    /// </summary>
    public class LogReaderTool : ITool
    {
        public string Name => "read_log";
        
        public string Description => "读取游戏日志的最后部分以分析报错 (read_tail). 无需参数，自动定位 Player.log 并读取最后 50 行。";

        public async Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
        {
            try
            {
                // 1. 自动定位 Player.log (修复：使用正确的 API)
                string logPath = Path.Combine(GenFilePaths.ConfigFolderPath, "..", "Logs", "Player.log");
                
                // 规范化路径
                logPath = Path.GetFullPath(logPath);
                
                if (!File.Exists(logPath))
                {
                    return new ToolResult 
                    { 
                        Success = false, 
                        Error = "Log file not found at: " + logPath 
                    };
                }

                // 2. 只读取最后 50 行 (足够看报错了)
                int linesToRead = 50;
                
                // 允许共享读取（避免文件被锁定）
                string[] allLines;
                using (var fs = new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var sr = new StreamReader(fs))
                {
                    var lines = new List<string>();
                    while (!sr.EndOfStream)
                    {
                        lines.Add(sr.ReadLine());
                    }
                    allLines = lines.ToArray();
                }
                
                int startLine = Math.Max(0, allLines.Length - linesToRead);
                string tailContent = string.Join("\n", allLines.Skip(startLine));

                // 3. 统计错误和警告数量
                int errorCount = allLines.Count(line => line.Contains("Exception") || line.Contains("ERROR") || line.Contains("Error"));
                int warningCount = allLines.Count(line => line.Contains("WARNING") || line.Contains("Warning"));

                return new ToolResult 
                { 
                    Success = true, 
                    Data = $"[Player.log Last {linesToRead} Lines]\n" +
                           $"Total lines in log: {allLines.Length}\n" +
                           $"Errors in full log: {errorCount}\n" +
                           $"Warnings in full log: {warningCount}\n" +
                           $"\n--- Last {linesToRead} Lines ---\n{tailContent}" 
                };
            }
            catch (Exception ex)
            {
                return new ToolResult 
                { 
                    Success = false, 
                    Error = $"Error reading log: {ex.Message}" 
                };
            }
        }
    }
}
