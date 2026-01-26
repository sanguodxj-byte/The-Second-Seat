using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;
using TheSecondSeat.NaturalLanguage;
using TheSecondSeat.Commands;
using TheSecondSeat.Commands.Implementations;
using TheSecondSeat.Utils;

namespace TheSecondSeat.Execution
{
    /// <summary>
    /// 游戏动作执行器 - 命令路由器
    /// ⭐ v1.6.84: 修复线程安全问题，确保在主线程执行
    /// ⭐ v1.7.00: 重构 - 使用 CommandRegistry 进行动态命令查找
    /// </summary>
    public static class GameActionExecutor
    {
        /// <summary>
        /// 执行解析后的命令
        /// ⭐ v1.6.84: 修复线程安全 - 如果不在主线程则调度到主线程
        /// ⭐ v2.0.0: 使用 TaskCompletionSource 替代 Thread.Sleep，避免阻塞
        /// </summary>
        public static async System.Threading.Tasks.Task<ExecutionResult> ExecuteAsync(ParsedCommand command)
        {
            if (command == null) return ExecutionResult.Failed("命令为空");

            var cmdInstance = CommandRegistry.GetCommand(command.action);
            if (cmdInstance == null) return ExecutionResult.Failed($"未知命令: {command.action}");

            Log.Message($"[GameActionExecutor] 执行命令: {command.action} (Target={command.parameters.target}, Scope={command.parameters.scope})");

            if (!TSS_AssetLoader.IsMainThread)
            {
                var tcs = new System.Threading.Tasks.TaskCompletionSource<ExecutionResult>();
                
                Verse.LongEventHandler.ExecuteWhenFinished(() =>
                {
                    try
                    {
                        var result = ExecuteOnMainThread(command);
                        tcs.SetResult(result);
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"[GameActionExecutor] 调度执行发生未捕获异常: {ex}");
                        tcs.SetException(ex);
                    }
                });

                // 等待任务完成，或者超时
                var timeoutTask = System.Threading.Tasks.Task.Delay(30000);
                var completedTask = await System.Threading.Tasks.Task.WhenAny(tcs.Task, timeoutTask);

                if (completedTask == timeoutTask)
                {
                    return ExecutionResult.Failed("命令执行超时 (主线程响应过慢)");
                }

                return await tcs.Task;
            }

            if (!UnityEngine.Application.isPlaying)
            {
                return ExecutionResult.Failed("游戏未运行");
            }
            
            return ExecuteOnMainThread(command);
        }

        // 保持同步方法兼容性，但建议使用 ExecuteAsync
        public static ExecutionResult Execute(ParsedCommand command)
        {
            if (TSS_AssetLoader.IsMainThread)
            {
                return ExecuteOnMainThread(command); // 主线程直接执行
            }
            
            // 后台线程调用同步方法时，仍需阻塞等待，但不再使用 Thread.Sleep 忙等待
            // 这里使用 Task.Run().Result 会死锁吗？如果不涉及 UI 上下文同步应该不会。
            // 但最安全的还是原来的 Thread.Sleep 模式用于同步调用。
            // 既然我们要重构，最好所有调用者都改为 await ExecuteAsync。
            // 暂时保留旧逻辑作为回退，但标记为 Obsolete
            
            Log.Warning("[GameActionExecutor] Execute called from background thread synchronously. Use ExecuteAsync instead.");
            return ExecuteAsync(command).Result;
        }
        
        /// <summary>
        /// 在主线程执行命令（内部方法）
        /// </summary>
        private static ExecutionResult ExecuteOnMainThread(ParsedCommand command)
        {
            try
            {
                // 转换参数为 Dictionary<string, object>
                Dictionary<string, object> paramsDict = ConvertParams(command.parameters);

                // ⭐ v2.0.0: 使用 CommandRegistry 动态查找并执行命令，移除硬编码 switch
                var cmdInstance = CommandRegistry.GetCommand(command.action);
                
                if (cmdInstance == null)
                {
                    return ExecutionResult.Failed($"未找到命令处理器: {command.action}");
                }

                bool success = cmdInstance.Execute(command.parameters.target, paramsDict);

                return success
                    ? ExecutionResult.Success($"命令 {command.action} 执行成功")
                    : ExecutionResult.Failed($"命令 {command.action} 执行失败");
            }
            catch (NotImplementedException ex)
            {
                Log.Warning($"[GameActionExecutor] {ex.Message}");
                return ExecutionResult.Failed(ex.Message);
            }
            catch (Exception ex)
            {
                Log.Error($"[GameActionExecutor] 执行失败: {ex.Message}\n{ex.StackTrace}");
                return ExecutionResult.Failed($"执行异常: {ex.Message}");
            }
        }

        #region 参数转换辅助方法

        /// <summary>
        /// ? 将 AdvancedCommandParams 转换为 Dictionary<string, object>
        /// 合并 target, scope, filters, count 到一个字典中
        /// </summary>
        private static Dictionary<string, object> ConvertParams(AdvancedCommandParams p)
        {
            var dict = new Dictionary<string, object>();

            // 1. 添加 target
            if (!string.IsNullOrEmpty(p.target))
            {
                dict["target"] = p.target;
            }

            // 2. 添加 scope
            if (!string.IsNullOrEmpty(p.scope))
            {
                dict["scope"] = p.scope;
            }

            // 3. 合并 filters 中的所有键值对
            if (p.filters != null)
            {
                foreach (var kvp in p.filters)
                {
                    dict[kvp.Key] = kvp.Value;
                }
            }

            // 4. ? 映射 p.count → "limit"
            if (p.count != null && p.count > 0)
            {
                dict["limit"] = p.count;
            }

            // 5. ? 添加其他可能的参数（从 scope 解析）
            // 例如：如果 scope 是 "delay=30" 或 "comment=AI评论"
            if (!string.IsNullOrEmpty(p.scope) && p.scope.Contains("="))
            {
                var parts = p.scope.Split('=');
                if (parts.Length == 2)
                {
                    string key = parts[0].Trim().ToLower();
                    string value = parts[1].Trim();
                    
                    // 避免覆盖已存在的 scope 键
                    if (key != "scope")
                    {
                        dict[key] = value;
                    }
                }
            }

            return dict;
        }

        #endregion
    }

    /// <summary>
    /// 执行结果
    /// </summary>
    public class ExecutionResult
    {
        public bool success { get; set; }
        public string message { get; set; } = "";
        public int affectedCount { get; set; } = 0;

        public static ExecutionResult Success(string message, int count = 0)
        {
            return new ExecutionResult
            {
                success = true,
                message = message,
                affectedCount = count
            };
        }

        public static ExecutionResult Failed(string message)
        {
            return new ExecutionResult
            {
                success = false,
                message = message
            };
        }
    }
}
