using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Verse;
using TheSecondSeat.Commands;
using TheSecondSeat.LLM;

namespace TheSecondSeat.RimAgent.Tools
{
    /// <summary>
    /// 命令工具 - 执行游戏命令
    /// ⭐ v1.6.84: 修复线程安全和参数传递问题
    /// </summary>
    public class CommandTool : ITool
    {
        public string Name => "command";
        public string Description => "执行游戏命令（如批量收获、批量装备等）";
        
        // 用于主线程同步的结果容器
        private volatile ToolResult? _pendingResult;
        private volatile bool _executionComplete;
        
        public async Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
        {
            Log.Message($"[CommandTool] ExecuteAsync called with parameters: {string.Join(", ", parameters.Keys)}");
            
            try
            {
                if (!parameters.TryGetValue("action", out var actionObj))
                {
                    return new ToolResult { Success = false, Error = "Missing parameter: action" };
                }
                
                string action = actionObj?.ToString() ?? "";
                
                // ⭐ v1.6.84: 修复参数传递 - 收集所有非 action 参数
                var commandParams = new Dictionary<string, object>();
                foreach (var kvp in parameters)
                {
                    if (kvp.Key != "action")
                    {
                        commandParams[kvp.Key] = kvp.Value;
                    }
                }
                
                // 如果有嵌套的 params 对象，也合并进来
                if (parameters.TryGetValue("params", out var paramsObj) && paramsObj is Dictionary<string, object> nestedParams)
                {
                    foreach (var kvp in nestedParams)
                    {
                        commandParams[kvp.Key] = kvp.Value;
                    }
                }
                
                // 获取 target
                string? target = null;
                if (parameters.TryGetValue("target", out var targetObj))
                {
                    target = targetObj?.ToString();
                }
                
                // 创建 LLMCommand 对象
                var llmCommand = new LLMCommand
                {
                    action = action,
                    target = target,
                    parameters = commandParams.Count > 0 ? commandParams : null
                };
                
                Log.Message($"[CommandTool] Prepared command: action={action}, target={target}, params={commandParams.Count}");
                
                // ⭐ v1.6.84: 确保在主线程执行
                _executionComplete = false;
                _pendingResult = null;
                
                // 使用 LongEventHandler 确保主线程执行
                Verse.LongEventHandler.ExecuteWhenFinished(() =>
                {
                    try
                    {
                        var result = CommandParser.ParseAndExecute(llmCommand);
                        
                        if (result.Success)
                        {
                            _pendingResult = new ToolResult { Success = true, Data = result.Message };
                        }
                        else
                        {
                            _pendingResult = new ToolResult { Success = false, Error = result.Message };
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"[CommandTool] Main thread execution error: {ex.Message}");
                        _pendingResult = new ToolResult { Success = false, Error = $"执行异常: {ex.Message}" };
                    }
                    finally
                    {
                        _executionComplete = true;
                    }
                });
                
                // 等待主线程执行完成（最多等待 5 秒）
                int waitCount = 0;
                const int maxWaitMs = 5000;
                const int checkIntervalMs = 50;
                
                while (!_executionComplete && waitCount < maxWaitMs)
                {
                    await Task.Delay(checkIntervalMs);
                    waitCount += checkIntervalMs;
                }
                
                if (!_executionComplete)
                {
                    Log.Warning("[CommandTool] Command execution timed out");
                    return new ToolResult { Success = false, Error = "命令执行超时" };
                }
                
                return _pendingResult ?? new ToolResult { Success = false, Error = "未知错误" };
            }
            catch (Exception ex)
            {
                Log.Error($"[CommandTool] Error: {ex.Message}\n{ex.StackTrace}");
                return new ToolResult { Success = false, Error = ex.Message };
            }
        }
    }
}