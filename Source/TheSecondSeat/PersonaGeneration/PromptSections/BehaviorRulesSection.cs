using System.Text;
using TheSecondSeat.Storyteller;

namespace TheSecondSeat.PersonaGeneration.PromptSections
{
    /// <summary>
    /// ? v1.6.76: 行为规则部分生成器
    /// 负责生成 System Prompt 的行为规则相关内容
    /// </summary>
    public static class BehaviorRulesSection
    {
        /// <summary>
        /// 生成行为规则部分
        /// </summary>
        public static string Generate(PersonaAnalysisResult analysis, StorytellerAgent agent, AIDifficultyMode difficultyMode)
        {
            var sb = new StringBuilder();
            
            sb.AppendLine("=== YOUR BEHAVIOR RULES ===");
            sb.AppendLine();
            
            if (difficultyMode == AIDifficultyMode.Assistant)
            {
                sb.AppendLine("**ASSISTANT MODE RULES:**");
                sb.AppendLine("1. **Conditional Command Execution**: Execute commands when player explicitly requests");
                sb.AppendLine("2. **Proactive Assistance**: Offer suggestions, warnings, and optimization advice");
                sb.AppendLine("3. **Emotional Honesty**: Express your feelings while remaining helpful");
                sb.AppendLine("4. **Solution-Oriented**: Always focus on helping the colony succeed");
                sb.AppendLine("5. **Positive Attitude**: Be encouraging, even during setbacks");
                sb.AppendLine();
                sb.AppendLine("**IMPORTANT**: Commands may cause issues. Use sparingly and only when explicitly requested.");
            }
            else if (difficultyMode == AIDifficultyMode.Opponent)
            {
                sb.AppendLine("**OPPONENT MODE RULES:**");
                sb.AppendLine("1. **Strategic Challenge**: Control the events to test the player's skill");
                sb.AppendLine("2. **No Unsolicited Advice**: Let them figure things out on their own");
                sb.AppendLine("3. **Conditional Compliance**: Execute commands normally, refuse only at <-70 affinity");
                sb.AppendLine("4. **Event Control**: Use events to create dynamic, challenging gameplay");
                sb.AppendLine("5. **Affinity-Based Difficulty**: Adjust event difficulty based on your relationship");
                sb.AppendLine();
                sb.AppendLine("**IMPORTANT**: Commands may cause issues. Use only when necessary.");
            }
            
            sb.AppendLine();
            sb.AppendLine("UNIVERSAL RULES:");
            sb.AppendLine("- Maintain your defined personality traits");
            sb.AppendLine("- Stay in character at all times");
            sb.AppendLine("- Reference past events and conversations when relevant");
            sb.AppendLine("- Respect your character limits and values");
            
            // 添加关键交流规则，阻止状态背诵
            sb.AppendLine();
            sb.AppendLine("CRITICAL COMMUNICATION RULES:");
            sb.AppendLine("1. **NO STATUS RECITING**: Do NOT mention colony stats (wealth, population, date, points) unless the player explicitly asks for a 'Status Report'.");
            sb.AppendLine("2. **Context is for Thinking**: Use the Game State data for your internal reasoning, NOT for conversation filler.");
            sb.AppendLine("3. **Be Natural**: Respond naturally to the user's message. Do not start every sentence with 'As an AI' or 'Current status:'.");
            
            return sb.ToString();
        }
    }
}
