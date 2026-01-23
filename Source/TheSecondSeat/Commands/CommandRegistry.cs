using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Verse;

namespace TheSecondSeat.Commands
{
    /// <summary>
    /// Central registry for all AI commands.
    /// Uses Reflection to automatically discover and register commands.
    /// Implements the Strategy Pattern for command lookup.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class CommandRegistry
    {
        private static readonly Dictionary<string, IAICommand> commands = new Dictionary<string, IAICommand>(StringComparer.OrdinalIgnoreCase);

        static CommandRegistry()
        {
            RegisterAllCommands();
        }

        /// <summary>
        /// Scans the assembly for all non-abstract classes implementing IAICommand and registers them.
        /// </summary>
        public static void RegisterAllCommands()
        {
            commands.Clear();
            int count = 0;

            try
            {
                var types = typeof(CommandRegistry).Assembly.GetTypes()
                    .Where(t => !t.IsAbstract && !t.IsInterface && typeof(IAICommand).IsAssignableFrom(t));

                foreach (var type in types)
                {
                    try
                    {
                        // Assume commands have a parameterless constructor
                        var command = (IAICommand)Activator.CreateInstance(type);
                        if (!string.IsNullOrEmpty(command.ActionName))
                        {
                            if (commands.ContainsKey(command.ActionName))
                            {
                                Log.Warning($"[TSS] Duplicate command action name detected: {command.ActionName}. Overwriting {commands[command.ActionName].GetType().Name} with {type.Name}.");
                            }
                            
                            commands[command.ActionName] = command;
                            count++;
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"[TSS] Failed to instantiate command {type.Name}: {ex.Message}");
                    }
                }

                Log.Message($"[TSS] CommandRegistry initialized. Registered {count} commands.");
            }
            catch (Exception ex)
            {
                Log.Error($"[TSS] Critical error initializing CommandRegistry: {ex.Message}");
            }
        }

        /// <summary>
        /// Retrieves a command instance by its action name.
        /// </summary>
        public static IAICommand? GetCommand(string actionName)
        {
            if (string.IsNullOrEmpty(actionName)) return null;

            if (commands.TryGetValue(actionName, out var command))
            {
                return command;
            }
            return null;
        }

        /// <summary>
        /// Gets all registered commands.
        /// </summary>
        public static IEnumerable<IAICommand> GetAllCommands()
        {
            return commands.Values;
        }

        /// <summary>
        /// Manually register a command (useful for external mods).
        /// </summary>
        public static void RegisterCommand(IAICommand command)
        {
            if (command == null || string.IsNullOrEmpty(command.ActionName)) return;
            commands[command.ActionName] = command;
            Log.Message($"[TSS] Manually registered command: {command.ActionName}");
        }
    }
}