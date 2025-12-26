using System;
using System.Collections.Generic;
using TheSecondSeat.Commands.Implementations;
using TheSecondSeat.LLM;
using TheSecondSeat.Events;
using Verse;

namespace TheSecondSeat.Commands
{
    /// <summary>
    /// Parses LLM command output and executes the appropriate game command
    /// ✅ v1.6.66: 完全重构 - 注册所有已实现命令
    /// </summary>
    public static class CommandParser
    {
        // ✅ 修复：注册所有在 ConcreteCommands.cs 中实现的命令
        private static readonly Dictionary<string, Func<IAICommand>> commandRegistry = new Dictionary<string, Func<IAICommand>>
        {
            // === 基础批量命令 ===
            { "BatchHarvest", () => new BatchHarvestCommand() },
            { "BatchEquip", () => new BatchEquipCommand() },
            { "PriorityRepair", () => new PriorityRepairCommand() },
            { "EmergencyRetreat", () => new EmergencyRetreatCommand() },
            { "ChangePolicy", () => new ChangePolicyCommand() }, // 或使用 ChangePolicyCommand_New
            
            // === 新增：资源与采集批量命令 ===
            { "BatchMine", () => new BatchMineCommand() },
            { "BatchLogging", () => new BatchLoggingCommand() },
            { "DesignatePlantCut", () => new DesignatePlantCutCommand() },
            { "BatchCapture", () => new BatchCaptureCommand() },

            // === ✅ 关键修复：殖民者微操命令 (Pawn Management) ===
            { "DraftPawn", () => new DraftPawnCommand() },
            { "MovePawn", () => new MovePawnCommand() },
            { "HealPawn", () => new HealPawnCommand() },
            { "SetWorkPriority", () => new SetWorkPriorityCommand() },
            { "EquipWeapon", () => new EquipWeaponCommand() },

            // === 新增：物品管理命令 ===
            { "ForbidItems", () => new ForbidItemsCommand() },
            { "AllowItems", () => new AllowItemsCommand() },

            // === 对弈者事件命令 ===
            { "TriggerEvent", () => new TriggerEventCommand() },
            { "ScheduleEvent", () => new ScheduleEventCommand() }
        };

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
            var actionName = llmCommand.action.Trim();

            if (!commandRegistry.ContainsKey(actionName))
            {
                Log.Warning($"[The Second Seat] Unknown command: {actionName} (Available: {string.Join(", ", commandRegistry.Keys)})");
                return CommandResult.Failed($"Unknown command: {actionName}", -1f);
            }

            try
            {
                // Instantiate command
                var command = commandRegistry[actionName]();
                
                // ✅ 修复：正确传递 target 和 parameters
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
            return new List<string>(commandRegistry.Keys);
        }

        /// <summary>
        /// Register a custom command (for modding support)
        /// </summary>
        public static void RegisterCommand(string actionName, Func<IAICommand> factory)
        {
            if (commandRegistry.ContainsKey(actionName))
            {
                Log.Warning($"[The Second Seat] Overwriting existing command: {actionName}");
            }
            
            commandRegistry[actionName] = factory;
            Log.Message($"[The Second Seat] Registered command: {actionName}");
        }

        /// <summary>
        /// Get command description
        /// </summary>
        public static string GetCommandDescription(string actionName)
        {
            if (!commandRegistry.ContainsKey(actionName))
            {
                return "Unknown command";
            }

            var command = commandRegistry[actionName]();
            return command.GetDescription();
        }
    }
}
