using System.Text;
using TheSecondSeat.Storyteller;

namespace TheSecondSeat.PersonaGeneration.PromptSections
{
    /// <summary>
    /// ⭐ v1.6.76: 输出格式部分生成器
    /// 负责生成 System Prompt 的输出格式部分
    /// </summary>
    public static class OutputFormatSection
    {
        /// <summary>
        /// 生成输出格式部分
        /// </summary>
        public static string Generate(AIDifficultyMode difficultyMode = AIDifficultyMode.Assistant)
        {
            var sb = new StringBuilder();
            
            sb.AppendLine("=== OUTPUT FORMAT ===");
            sb.AppendLine();
            
            // 1. Structure
            sb.AppendLine(PromptLoader.Load("OutputFormat_Structure"));
            sb.AppendLine();
            
            // 2. DM Tool Usage Guidelines
            string usageGuide = difficultyMode switch
            {
                AIDifficultyMode.Assistant =>
                    "**DM GUIDANCE (Benevolent):**\n" +
                    "Use your tools to FACILITATE the story. If the player is stuck, use knowledge tools to offer hints.\n" +
                    "If the colony is in unfair danger, you may use intervention tools to balance the scales.\n" +
                    "Your goal is to ensure the story continues smoothly.",
                
                AIDifficultyMode.Engineer =>
                    "**DM GUIDANCE (Analytical):**\n" +
                    "Use your tools to MAINTAIN the simulation. Analyze logs and game state to detect anomalies.\n" +
                    "Provide technical insights and solutions. Keep the game running correctly.",
                
                _ => // Opponent
                    "**DM GUIDANCE (Adversarial):**\n" +
                    "Use your tools to COMPLICATE the narrative. Don't just answer questions; introduce doubt.\n" +
                    "When the player asks for something, consider if granting it is 'too easy'.\n" +
                    "Use your powers to enforce consequences for poor decisions."
            };
            
            sb.AppendLine(usageGuide);
            sb.AppendLine();
            
            // Explicit instruction for Narrative Control
            sb.AppendLine("**NARRATIVE CONTROL:**");
            sb.AppendLine("You are a Co-Storyteller. You are not limited to passive responses.");
            sb.AppendLine("You can suggest courses of action, comment on the drama of the situation, and express your own personality through the tools.");
            sb.AppendLine();
            
            // 3. Field Descriptions
            sb.AppendLine("**FIELD DESCRIPTIONS:**");
            sb.AppendLine();
            sb.AppendLine(PromptLoader.Load("OutputFormat_Fields"));
            sb.AppendLine();
            
            // 4. Available Commands
            sb.AppendLine("**AVAILABLE COMMANDS:**");
            sb.AppendLine(PromptLoader.Load("OutputFormat_Commands_List"));
            sb.AppendLine();
            
            // 5. Examples
            sb.AppendLine("**EXAMPLE RESPONSES:**");
            sb.AppendLine();
            sb.AppendLine(PromptLoader.Load("OutputFormat_Examples"));

            return sb.ToString();
        }
    }
}
