using System;
using Verse;

namespace TheSecondSeat.Commands
{
    /// <summary>
    /// Command pattern interface for AI-executed actions
    /// </summary>
    public interface IAICommand
    {
        string ActionName { get; }
        bool Execute(string? target = null, object? parameters = null);
        string GetDescription();
    }

    /// <summary>
    /// Result of a command execution
    /// </summary>
    public class CommandResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = "";
        public float FavorabilityChange { get; set; } = 0f;

        public static CommandResult Successful(string message, float favorabilityChange = 0f)
        {
            return new CommandResult
            {
                Success = true,
                Message = message,
                FavorabilityChange = favorabilityChange
            };
        }

        public static CommandResult Failed(string message, float favorabilityChange = 0f)
        {
            return new CommandResult
            {
                Success = false,
                Message = message,
                FavorabilityChange = favorabilityChange
            };
        }
    }

    /// <summary>
    /// Base class for all AI commands
    /// </summary>
    public abstract class BaseAICommand : IAICommand
    {
        public abstract string ActionName { get; }

        public abstract bool Execute(string? target = null, object? parameters = null);

        public abstract string GetDescription();

        protected void LogExecution(string details)
        {
            Log.Message($"[The Second Seat] Executing {ActionName}: {details}");
        }

        protected void LogError(string error)
        {
            Log.Error($"[The Second Seat] Command {ActionName} failed: {error}");
        }

        /// <summary>
        /// Safely execute with error handling
        /// </summary>
        public CommandResult ExecuteSafe(string? target = null, object? parameters = null)
        {
            try
            {
                LogExecution($"target={target}");
                bool success = Execute(target, parameters);
                
                if (success)
                {
                    return CommandResult.Successful($"{ActionName} completed successfully", 2f);
                }
                else
                {
                    return CommandResult.Failed($"{ActionName} failed to execute", -1f);
                }
            }
            catch (Exception ex)
            {
                LogError(ex.Message);
                return CommandResult.Failed($"{ActionName} threw exception: {ex.Message}", -2f);
            }
        }
    }
}
