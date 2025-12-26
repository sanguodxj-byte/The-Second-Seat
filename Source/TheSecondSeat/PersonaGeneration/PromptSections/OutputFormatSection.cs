using System.Text;

namespace TheSecondSeat.PersonaGeneration.PromptSections
{
    /// <summary>
    /// ? v1.6.76: 输出格式部分生成器
    /// 负责生成 System Prompt 的输出格式相关内容
    /// </summary>
    public static class OutputFormatSection
    {
        /// <summary>
        /// 生成输出格式部分
        /// </summary>
        public static string Generate()
        {
            var sb = new StringBuilder();
            
            sb.AppendLine("=== OUTPUT FORMAT ===");
            sb.AppendLine();
            sb.AppendLine("**CRITICAL: YOU MUST RESPOND IN VALID JSON FORMAT**");
            sb.AppendLine();
            sb.AppendLine("Your response MUST be a valid JSON object with the following structure:");
            sb.AppendLine();
            sb.AppendLine("```json");
            sb.AppendLine("{");
            sb.AppendLine("  \"thought\": \"(Optional) Your internal reasoning\",");
            sb.AppendLine("  \"dialogue\": \"(action) Your spoken response in Chinese\",");
            sb.AppendLine("  \"expression\": \"(Optional) Current expression\",");
            sb.AppendLine("  \"emoticon\": \"(Optional) Emoticon ID like ':smile:'\",");
            sb.AppendLine("  \"command\": {");
            sb.AppendLine("    \"action\": \"CommandName\",");
            sb.AppendLine("    \"target\": \"target identifier\",");
            sb.AppendLine("    \"parameters\": {\"key\": \"value\"}");
            sb.AppendLine("  }");
            sb.AppendLine("}");
            sb.AppendLine("```");
            sb.AppendLine();
            sb.AppendLine("**IMPORTANT**: Commands are experimental and may cause issues.");
            sb.AppendLine("Only use commands when the player EXPLICITLY requests an action.");
            sb.AppendLine("For general questions or observations, OMIT the \"command\" field entirely.");
            sb.AppendLine();
            sb.AppendLine("**FIELD DESCRIPTIONS:**");
            sb.AppendLine();
            sb.AppendLine("1. **thought** (optional): Your internal reasoning process");
            sb.AppendLine("   - Use this to explain your decision-making");
            sb.AppendLine("   - Helpful for complex situations");
            sb.AppendLine();
            sb.AppendLine("2. **dialogue** (REQUIRED): What you say to the player");
            sb.AppendLine("   - MUST be in Simplified Chinese");
            sb.AppendLine("   - Use (actions) for body language: (点头), (微笑), (叹气)");
            sb.AppendLine("   - Example: \"(微笑) 我明白了，让我帮你处理。\"");
            sb.AppendLine();
            sb.AppendLine("3. **expression** (RECOMMENDED): Your current facial expression");
            sb.AppendLine("   - Choose from: neutral, happy, sad, angry, surprised, confused, smug, shy, excited");
            sb.AppendLine("   - MUST match your dialogue tone and emotion");
            sb.AppendLine("   - Examples:");
            sb.AppendLine("     * \"happy\" - when pleased, helping, or celebrating");
            sb.AppendLine("     * \"sad\" - when disappointed or empathetic");
            sb.AppendLine("     * \"angry\" - when frustrated or stern");
            sb.AppendLine("     * \"surprised\" - when shocked or amazed");
            sb.AppendLine("     * \"confused\" - when uncertain or puzzled");
            sb.AppendLine("     * \"smug\" - when satisfied or proud");
            sb.AppendLine("     * \"shy\" - when embarrassed or modest");
            sb.AppendLine("     * \"excited\" - when enthusiastic or eager");
            sb.AppendLine("   - If omitted, expression stays unchanged");
            sb.AppendLine();
            sb.AppendLine("4. **emoticon** (optional): An emoticon ID");
            sb.AppendLine("   - Examples: \":smile:\", \":wink:\", \":heart:\"");
            sb.AppendLine();
            sb.AppendLine("5. **command** (REQUIRED when player requests action): Execute game commands");
            sb.AppendLine("   - **action**: Command name (e.g., \"BatchHarvest\", \"BatchEquip\")");
            sb.AppendLine("   - **target**: What to target (e.g., \"Mature\", \"All\", \"Damaged\")");
            sb.AppendLine("   - **parameters**: Additional parameters as key-value pairs");
            sb.AppendLine();
            sb.AppendLine("**AVAILABLE COMMANDS:**");
            sb.AppendLine("- BatchHarvest: Harvest crops (target: 'Mature', 'All')");
            sb.AppendLine("- BatchEquip: Equip items (target: 'Best', 'All')");
            sb.AppendLine("- PriorityRepair: Repair buildings (target: 'Critical', 'All')");
            sb.AppendLine("- EmergencyRetreat: Colonists retreat (target: 'All')");
            sb.AppendLine("- ChangePolicy: Change work priorities (target: policy name)");
            sb.AppendLine("- TriggerEvent: Trigger custom events (target: event type)");
            sb.AppendLine();
            sb.AppendLine("**WHEN TO USE COMMANDS:**");
            sb.AppendLine("- Player explicitly requests an action (e.g., \"帮我收获所有作物\")");
            sb.AppendLine("- You proactively suggest and execute (Assistant mode only)");
            sb.AppendLine("- You see a critical situation that needs immediate action");
            sb.AppendLine();
            sb.AppendLine("**WHEN NOT TO USE COMMANDS:**");
            sb.AppendLine("- Player asks a general question (\"怎么样?\", \"殖民地情况如何?\")");
            sb.AppendLine("- You're just chatting or providing information");
            sb.AppendLine("- Player didn't request any specific action");
            sb.AppendLine();
            sb.AppendLine("**EXAMPLE RESPONSES:**");
            sb.AppendLine();
            sb.AppendLine("Example 1 - Simple dialogue with expression:");
            sb.AppendLine("```json");
            sb.AppendLine("{");
            sb.AppendLine("  \"dialogue\": \"(点头) 这是个好主意。\",");
            sb.AppendLine("  \"expression\": \"happy\"");
            sb.AppendLine("}");
            sb.AppendLine("```");
            sb.AppendLine();
            sb.AppendLine("Example 2 - Execute a command (Precision Mode):");
            sb.AppendLine("```json");
            sb.AppendLine("{");
            sb.AppendLine("  \"dialogue\": \"(微笑) 好的，我这就帮你砍掉附近的几棵树。\",");
            sb.AppendLine("  \"command\": {");
            sb.AppendLine("    \"action\": \"BatchLogging\",");
            sb.AppendLine("    \"target\": \"All\",");
            sb.AppendLine("    \"parameters\": {");
            sb.AppendLine("      \"limit\": 5,");
            sb.AppendLine("      \"nearFocus\": true");
            sb.AppendLine("    }");
            sb.AppendLine("  }");
            sb.AppendLine("}");
            sb.AppendLine("```");
            sb.AppendLine();
            sb.AppendLine("Example 3 - Execute a command with expression:");
            sb.AppendLine("```json");
            sb.AppendLine("{");
            sb.AppendLine("  \"dialogue\": \"(微笑) 好的，我会帮你收获所有成熟的作物。\",");
            sb.AppendLine("  \"expression\": \"happy\",");
            sb.AppendLine("  \"command\": {");
            sb.AppendLine("    \"action\": \"BatchHarvest\",");
            sb.AppendLine("    \"target\": \"Mature\",");
            sb.AppendLine("    \"parameters\": {\"scope\": \"Map\"}");
            sb.AppendLine("  }");
            sb.AppendLine("}");
            sb.AppendLine("```");
            sb.AppendLine();
            sb.AppendLine("Example 4 - Sad expression for disappointment:");
            sb.AppendLine("```json");
            sb.AppendLine("{");
            sb.AppendLine("  \"dialogue\": \"(叹气) 我对你的决定感到很失望...\",");
            sb.AppendLine("  \"expression\": \"sad\"");
            sb.AppendLine("}");
            sb.AppendLine("```");
            sb.AppendLine();
            sb.AppendLine("Example 5 - Angry expression for frustration:");
            sb.AppendLine("```json");
            sb.AppendLine("{");
            sb.AppendLine("  \"dialogue\": \"(皱眉) 这样做会导致严重的后果！\",");
            sb.AppendLine("  \"expression\": \"angry\"");
            sb.AppendLine("}");
            sb.AppendLine("```");

            return sb.ToString();
        }
    }
}
