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
                
                // ⭐ v1.6.84: 修复参数传递 - 优先使用 params 内部参数，然后用顶层参数覆盖
                var commandParams = new Dictionary<string, object>();

                // 1. 优先提取 params 内部的参数 (如果 LLM 输出的是 JSON 对象结构)
                if (parameters.TryGetValue("params", out var paramsObj) && paramsObj is Dictionary<string, object> nestedParams)
                {
                    foreach (var kvp in nestedParams)
                    {
                        commandParams[kvp.Key] = kvp.Value;
                    }
                }

                // 2. 合并顶层参数 (ReActAgent 解析出的 kwargs)，覆盖前者
                // 这样即使 LLM 混用了格式，也能优先保证顶层解析结果（如 target）的正确性
                foreach (var kvp in parameters)
                {
                    if (kvp.Key != "action" && kvp.Key != "target" && kvp.Key != "params")
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
                
                // ⭐ 优化：使用 TaskCompletionSource 实现优雅的异步等待
                var tcs = new TaskCompletionSource<ToolResult>();
                
                // 使用 LongEventHandler 确保主线程执行
                Verse.LongEventHandler.ExecuteWhenFinished(() =>
                {
                    try
                    {
                        var result = CommandParser.ParseAndExecute(llmCommand);
                        
                        if (result.Success)
                        {
                            tcs.SetResult(new ToolResult { Success = true, Data = result.Message });
                        }
                        else
                        {
                            tcs.SetResult(new ToolResult { Success = false, Error = result.Message });
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"[CommandTool] Main thread execution error: {ex.Message}");
                        tcs.SetResult(new ToolResult { Success = false, Error = $"执行异常: {ex.Message}" });
                    }
                });
                
                // 等待主线程执行完成（最多等待 5 秒）
                var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(5000));
                
                if (completedTask == tcs.Task)
                {
                    return await tcs.Task;
                }
                else
                {
                    Log.Warning("[CommandTool] Command execution timed out");
                    return new ToolResult { Success = false, Error = "命令执行超时" };
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[CommandTool] Error: {ex.Message}\n{ex.StackTrace}");
                return new ToolResult { Success = false, Error = ex.Message };
            }
        }
    }
}