using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;
using TheSecondSeat.NaturalLanguage;
using TheSecondSeat.Commands.Implementations;

namespace TheSecondSeat.Execution
{
    /// <summary>
    /// 游戏动作执行器 - 命令路由器
    /// ? v1.6.40: 完全迁移到路由器架构，移除所有直接实现
    /// </summary>
    public static class GameActionExecutor
    {
        /// <summary>
        /// 执行解析后的命令
        /// ? 线程安全：必须在主线程执行
        /// </summary>
        public static ExecutionResult Execute(ParsedCommand command)
        {
            if (command == null)
            {
                return ExecutionResult.Failed("命令为空");
            }

            Log.Message($"[GameActionExecutor] 执行命令: {command.action} (Target={command.parameters.target}, Scope={command.parameters.scope})");

            // ? 检查是否在主线程
            if (!UnityEngine.Application.isPlaying)
            {
                return ExecutionResult.Failed("游戏未运行");
            }

            try
            {
                // ? 转换参数为 Dictionary<string, object>
                Dictionary<string, object> paramsDict = ConvertParams(command.parameters);

                // ? 根据 action 字符串实例化对应的命令类并执行
                bool success = command.action switch
                {
                    // === 批量操作命令 ===
                    "BatchHarvest" => new BatchHarvestCommand().Execute(command.parameters.target, paramsDict),
                    "BatchEquip" => new BatchEquipCommand().Execute(command.parameters.target, paramsDict),
                    "BatchCapture" => new BatchCaptureCommand().Execute(command.parameters.target, paramsDict),
                    "BatchMine" => new BatchMineCommand().Execute(command.parameters.target, paramsDict),
                    "BatchLogging" => new BatchLoggingCommand().Execute(command.parameters.target, paramsDict),
                    "PriorityRepair" => new PriorityRepairCommand().Execute(command.parameters.target, paramsDict),
                    "EmergencyRetreat" => new EmergencyRetreatCommand().Execute(command.parameters.target, paramsDict),
                    "DesignatePlantCut" => new DesignatePlantCutCommand().Execute(command.parameters.target, paramsDict),
                    
                    // === 对弈者模式事件命令 ===
                    "TriggerEvent" => new TriggerEventCommand().Execute(command.parameters.target, paramsDict),
                    "ScheduleEvent" => new ScheduleEventCommand().Execute(command.parameters.target, paramsDict),
                    
                    // === ? v1.6.40: 殖民者操作命令（已迁移） ===
                    "DraftPawn" => new DraftPawnCommand().Execute(command.parameters.target, paramsDict),
                    "MovePawn" => new MovePawnCommand().Execute(command.parameters.target, paramsDict),
                    "HealPawn" => new HealPawnCommand().Execute(command.parameters.target, paramsDict),
                    "SetWorkPriority" => new SetWorkPriorityCommand().Execute(command.parameters.target, paramsDict),
                    "EquipWeapon" => new EquipWeaponCommand().Execute(command.parameters.target, paramsDict),
                    
                    // === ? v1.6.40: 资源管理命令（已迁移） ===
                    "ForbidItems" => new ForbidItemsCommand().Execute(command.parameters.target, paramsDict),
                    "AllowItems" => new AllowItemsCommand().Execute(command.parameters.target, paramsDict),
                    
                    // === ? v1.6.40: 政策修改命令（已迁移） ===
                    "ChangePolicy" => new ChangePolicyCommand_New().Execute(command.parameters.target, paramsDict),
                    
                    // === 暂不支持的命令 ===
                    "DesignateConstruction" => throw new NotImplementedException("建造命令需要更多的建筑数据，暂不支持"),
                    "AssignWork" => throw new NotImplementedException("工作分配需要更多的工作类型，暂不支持"),
                    
                    _ => throw new NotImplementedException($"未知命令: {command.action}")
                };

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
