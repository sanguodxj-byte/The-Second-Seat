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
    /// </summary>
    public static class CommandParser
    {
        private static readonly Dictionary<string, Func<IAICommand>> commandRegistry = new Dictionary<string, Func<IAICommand>>
        {
            { "BatchHarvest", () => new BatchHarvestCommand() },
            { "BatchEquip", () => new BatchEquipCommand() },
            { "PriorityRepair", () => new PriorityRepairCommand() },
            { "EmergencyRetreat", () => new EmergencyRetreatCommand() },
            { "ChangePolicy", () => new ChangePolicyCommand() },
            // ? 事件触发命令（对弈者模式）
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
                Log.Warning($"[The Second Seat] Unknown command: {actionName}");
                return CommandResult.Failed($"Unknown command: {actionName}", -1f);
            }

            try
            {
                // Instantiate command
                var command = commandRegistry[actionName]();
                
                // Execute with safe wrapper
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
