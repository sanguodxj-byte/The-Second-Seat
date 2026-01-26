using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Verse;
using TheSecondSeat.Narrator;

namespace TheSecondSeat.RimAgent.Tools
{
    /// <summary>
    /// A tool that allows the narrator to roll a D20 with an affinity modifier.
    /// Used for determining the outcome of high-stakes actions.
    /// </summary>
    public class RollDiceTool : ITool
    {
        public string Name => "RollDice";

        public string Description => "Rolls a 20-sided die (D20) with an affinity modifier based on your relationship with the player. " +
                                     "Use this when the outcome of an action is uncertain or high-stakes. " +
                                     "Parameters: 'difficulty' (optional integer, default 10). " +
                                     "Returns the roll result, modifier, and whether it succeeded.";

        public Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
        {
            try
            {
                // 1. Parse Difficulty
                int difficulty = 10;
                if (parameters != null && parameters.ContainsKey("difficulty"))
                {
                    if (int.TryParse(parameters["difficulty"].ToString(), out int parsedDiff))
                    {
                        difficulty = parsedDiff;
                    }
                }

                // 2. Get Affinity Modifier
                float affinity = 0f;
                if (NarratorManager.Instance != null)
                {
                    affinity = NarratorManager.Instance.Favorability;
                }
                
                // Modifier = Affinity / 10 (rounded)
                // e.g., 100 -> +10, 50 -> +5, -20 -> -2
                int modifier = (int)Math.Round(affinity / 10f);

                // 3. Roll D20
                int d20 = Rand.Range(1, 21); // 1 to 20 inclusive

                // 4. Calculate Total
                int total = d20 + modifier;
                bool success = total >= difficulty;

                // 5. Construct Result
                string resultMessage = $"[Fate Dice]\n" +
                                       $"Difficulty: {difficulty}\n" +
                                       $"Roll: D20({d20}) + Affinity({modifier}) = {total}\n" +
                                       $"Result: {(success ? "SUCCESS" : "FAILURE")}\n" +
                                       $"Affinity Impact: Your current affinity ({affinity:F0}) provided a {modifier:+0;-0} modifier.";

                return Task.FromResult(ToolResult.Successful(resultMessage));
            }
            catch (Exception ex)
            {
                return Task.FromResult(ToolResult.Failure($"Failed to roll dice: {ex.Message}"));
            }
        }
    }
}