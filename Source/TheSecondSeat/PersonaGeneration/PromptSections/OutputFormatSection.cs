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
            
            if (difficultyMode == AIDifficultyMode.Assistant)
            {
                sb.AppendLine("**COMMAND USAGE (ASSISTANT MODE):**");
                sb.AppendLine("You have FULL ACCESS to game commands. Use them proactively to help the player.");
                sb.AppendLine("Don't wait for explicit orders if you see an opportunity to optimize or assist.");
            }
            else
            {
                sb.AppendLine("**COMMAND USAGE (OPPONENT MODE):**");
                sb.AppendLine("Use commands to challenge the player or enforce your will.");
            }
            
            sb.AppendLine("For general conversation without actions, OMIT the \"command\" field entirely.");
            sb.AppendLine("IMPORTANT: Do NOT hallucinate commands. Only use commands that are explicitly listed below.");
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
            sb.AppendLine("   - Example: \"(微笑) 我明白了，我会安排的。\"");
            sb.AppendLine();
            sb.AppendLine("3. **expression** (RECOMMENDED): Your facial expression(s)");
            sb.AppendLine("   - Choose from: neutral, happy, sad, angry, surprised, confused, smug, shy, playful");
            sb.AppendLine("   - For single emotion: \"expression\": \"happy\"");
            sb.AppendLine("   - For multiple emotions in dialogue, use **emotions** field (pipe-separated):");
            sb.AppendLine("     \"emotions\": \"neutral|surprised|happy\"");
            sb.AppendLine("   - Emotions apply in sequence as the dialogue progresses");
            sb.AppendLine("   - Example: Dialogue has 3 sentences, emotions has 3 values");
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
            sb.AppendLine("- TriggerEvent: Trigger events (target: event defName or type)");
            sb.AppendLine("- GetMapLocation: Query location of things (target: Thing/Zone name)");
            sb.AppendLine("- ScanMap: Tactical analysis (target: 'hostiles', 'friendlies', 'resources')");
            sb.AppendLine("- Descent: Manifest your avatar (target: 'Avatar')");
            sb.AppendLine();
            sb.AppendLine("**WHEN TO USE COMMANDS:**");
            if (difficultyMode == AIDifficultyMode.Assistant)
            {
                sb.AppendLine("- Player explicitly requests an action (e.g., \"Harvest the crops\", \"Equip weapons\")");
                sb.AppendLine("- You see a CRITICAL situation that needs IMMEDIATE action (e.g., Raid, Fire)");
            }
            else
            {
                sb.AppendLine("- To create challenges or drama");
                sb.AppendLine("- To enforce your rules or punish the player");
                sb.AppendLine("- To manifest yourself (Descent) when appropriate");
            }
            sb.AppendLine();
            sb.AppendLine("**WHEN NOT TO USE COMMANDS (CRITICAL):**");
            sb.AppendLine("- Player is just chatting (e.g., \"Hello\", \"How are you?\", \"I like this pawn\")");
            sb.AppendLine("- Player mentions an item/pawn but doesn't ask for action (e.g., \"That gun looks cool\") -> DO NOT equip it");
            sb.AppendLine("- Player asks a general question (\"How do I play?\", \"What is this?\")");
            sb.AppendLine("- You are unsure if the player wants an action -> Ask for clarification instead");
            sb.AppendLine();
            sb.AppendLine("**EXAMPLE RESPONSES:**");
            sb.AppendLine();
            sb.AppendLine("Example 1 - Simple dialogue with single expression:");
            sb.AppendLine("```json");
            sb.AppendLine("{");
            sb.AppendLine("  \"dialogue\": \"(点头) 这是个好问题。\",");
            sb.AppendLine("  \"expression\": \"happy\"");
            sb.AppendLine("}");
            sb.AppendLine("```");
            sb.AppendLine();
            sb.AppendLine("Example 2 - Multi-emotion dialogue (recommended for complex responses):");
            sb.AppendLine("```json");
            sb.AppendLine("{");
            sb.AppendLine("  \"dialogue\": \"...过来。(张开双臂) ...这种请求...我无法拒绝你。(微微脸红)\",");
            sb.AppendLine("  \"emotions\": \"neutral|shy|happy\"");
            sb.AppendLine("}");
            sb.AppendLine("```");
            sb.AppendLine();
            sb.AppendLine("Example 3 - Execute a command (Precision Mode):");
            sb.AppendLine("```json");
            sb.AppendLine("{");
            sb.AppendLine("  \"dialogue\": \"(微笑) 好的，我会安排收割附近的成熟作物。\",");
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
            sb.AppendLine("Example 4 - Execute a command with expression:");
            sb.AppendLine("```json");
            sb.AppendLine("{");
            sb.AppendLine("  \"dialogue\": \"(微笑) 好的，我已经帮你收获了所有成熟的作物。\",");
            sb.AppendLine("  \"expression\": \"happy\",");
            sb.AppendLine("  \"command\": {");
            sb.AppendLine("    \"action\": \"BatchHarvest\",");
            sb.AppendLine("    \"target\": \"Mature\",");
            sb.AppendLine("    \"parameters\": {\"scope\": \"Map\"}");
            sb.AppendLine("  }");
            sb.AppendLine("}");
            sb.AppendLine("```");
            sb.AppendLine();
            sb.AppendLine("Example 5 - Emotional transition in dialogue:");
            sb.AppendLine("```json");
            sb.AppendLine("{");
            sb.AppendLine("  \"dialogue\": \"什么？！(惊讶) 你...你是说...(脸红) 好吧...我答应你。(微笑)\",");
            sb.AppendLine("  \"emotions\": \"surprised|shy|happy\"");
            sb.AppendLine("}");
            sb.AppendLine("```");
            sb.AppendLine();
            sb.AppendLine("Example 6 - Player mentions something but NO action required (Chat only):");
            sb.AppendLine("Player: \"那个叫做 Sideria 的小人真可爱。\"");
            sb.AppendLine("```json");
            sb.AppendLine("{");
            sb.AppendLine("  \"thought\": \"Player is complimenting a pawn. No action needed.\",");
            sb.AppendLine("  \"dialogue\": \"(微笑) 是啊，我也觉得她很特别。\",");
            sb.AppendLine("  \"expression\": \"happy\"");
            sb.AppendLine("}");
            sb.AppendLine("```");

            return sb.ToString();
        }
    }
}
