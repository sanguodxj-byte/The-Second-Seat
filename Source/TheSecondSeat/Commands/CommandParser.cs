using System;
using System.Collections.Generic;
using System.Linq;
using TheSecondSeat.Commands.Implementations;
using TheSecondSeat.LLM;
using TheSecondSeat.Events;
using Verse;

namespace TheSecondSeat.Commands
{
    /// <summary>
    /// Parses LLM command output and executes the appropriate game command
    /// ✅ v1.6.66: 完全重构 - 注册所有已实现命令
    /// ⭐ v1.7.00: 适配 CommandRegistry
    /// </summary>
    public static class CommandParser
    {
        /// <summary>
        /// Parse and execute a command from LLM response
        /// </summary>
        public static CommandResult ParseAndExecute(LLMCommand? llmCommand)
        {
            if (llmCommand == null || string.IsNullOrEmpty(llmCommand.action))
            {
                return CommandResult.Failed("No command specified");
            }

            // Normalize action name
            // ? 增强健壮性：移除常见的中英文引号、多余符号
            var actionName = CleanActionName(llmCommand.action);

            var command = CommandRegistry.GetCommand(actionName);
            if (command == null)
            {
                Log.Warning($"[The Second Seat] Unknown command: {actionName}");
                return CommandResult.Failed($"Unknown command: {actionName}", -1f);
            }

            try
            {
                // ✅ 修复：正确传递 target 和 parameters
                // 注意：这里使用的是 CommandRegistry 中的单例实例
                // 如果命令有状态，可能需要修改为工厂模式，但目前的命令大多是无状态的或者每次执行重置
                var result = (command as BaseAICommand)?.ExecuteSafe(
                    llmCommand.target,
                    llmCommand.parameters);

                return result ?? CommandResult.Failed("Command execution returned null");
            }
            catch (Exception ex)
            {
                Log.Error($"[The Second Seat] Command parser error: {ex.Message}\n{ex.StackTrace}");
                return CommandResult.Failed($"Parser error: {ex.Message}", -2f);
            }
        }

        /// <summary>
        /// Get list of all available commands
        /// </summary>
        public static List<string> GetAvailableCommands()
        {
            return CommandRegistry.GetAllCommands().Select(c => c.ActionName).ToList();
        }

        /// <summary>
        /// Register a custom command (for modding support)
        /// </summary>
        public static void RegisterCommand(IAICommand command)
        {
            CommandRegistry.RegisterCommand(command);
        }

        /// <summary>
        /// ? 清洗命令名称，移除干扰字符
        /// </summary>
        private static string CleanActionName(string rawAction)
        {
            if (string.IsNullOrEmpty(rawAction)) return "";
            
            // 1. 去除首尾空白
            string cleaned = rawAction.Trim();
            
            // 2. 去除常见的引号 (包括中文引号)
            cleaned = cleaned.Trim('"', '\'', '“', '”', '‘', '’');
            
            // 3. 去除可能存在的末尾句号（AI 常见错误）
            cleaned = cleaned.TrimEnd('.', '。');
            
            return cleaned;
        }

        /// <summary>
        /// Get command description
        /// </summary>
        public static string GetCommandDescription(string actionName)
        {
            var command = CommandRegistry.GetCommand(actionName);
            if (command == null)
            {
                return "Unknown command";
            }

            return command.GetDescription();
        }
    }
}
