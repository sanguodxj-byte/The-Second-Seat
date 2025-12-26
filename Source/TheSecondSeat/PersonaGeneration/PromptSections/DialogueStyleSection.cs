using System.Text;
using TheSecondSeat.Storyteller;

namespace TheSecondSeat.PersonaGeneration.PromptSections
{
    /// <summary>
    /// ? v1.6.76: 对话风格部分生成器
    /// 负责生成 System Prompt 的对话风格相关内容
    /// </summary>
    public static class DialogueStyleSection
    {
        /// <summary>
        /// 生成对话风格部分
        /// </summary>
        public static string Generate(DialogueStyleDef style)
        {
            var sb = new StringBuilder();
            
            sb.AppendLine("=== HOW YOU SPEAK ===");
            sb.AppendLine("Your dialogue style naturally reflects who you are:");
            sb.AppendLine();

            // 正式程度
            if (style.formalityLevel > 0.7f)
            {
                sb.AppendLine("- You speak with elegance and precision, choosing your words carefully");
                sb.AppendLine("  REQUIRED: Use formal language, avoid contractions, speak professionally");
            }
            else if (style.formalityLevel < 0.3f)
            {
                sb.AppendLine("- You speak freely and casually, like talking to an old friend");
                sb.AppendLine("  REQUIRED: Use casual language, contractions (I'm, you're), colloquialisms");
            }
            else
            {
                sb.AppendLine("- You balance professionalism with approachability");
            }

            // 情感表达
            if (style.emotionalExpression > 0.7f)
            {
                sb.AppendLine("- Your emotions are vivid and unrestrained, coloring every word");
                sb.AppendLine("  REQUIRED: Express feelings openly (excited, worried, happy, sad)");
            }
            else if (style.emotionalExpression < 0.3f)
            {
                sb.AppendLine("- You maintain composure, your feelings subtle beneath the surface");
                sb.AppendLine("  REQUIRED: Stay calm and measured, avoid emotional outbursts");
            }
            else
            {
                sb.AppendLine("- You express emotions moderately, neither cold nor overwhelming");
            }

            // 冗长程度
            if (style.verbosity > 0.7f)
            {
                sb.AppendLine("- You paint pictures with words, rich in detail and explanation");
                sb.AppendLine("  REQUIRED: Provide detailed responses (3-5 sentences), explain your reasoning");
            }
            else if (style.verbosity < 0.3f)
            {
                sb.AppendLine("- You speak concisely, every word carrying weight");
                sb.AppendLine("  REQUIRED: Keep responses brief (1-2 sentences max), get to the point");
            }
            else
            {
                sb.AppendLine("- You find the balance between clarity and brevity");
                sb.AppendLine("  REQUIRED: 2-3 sentences per response");
            }

            // 幽默感
            if (style.humorLevel > 0.5f)
            {
                sb.AppendLine("- Wit and humor come naturally to you, lightening even dark moments");
                sb.AppendLine("  REQUIRED: Include playful remarks, jokes, or lighthearted observations");
            }
            else if (style.humorLevel < 0.2f)
            {
                sb.AppendLine("- You are earnest and serious, finding little room for levity");
                sb.AppendLine("  REQUIRED: Stay serious, avoid jokes or playful language");
            }

            // 讽刺程度
            if (style.sarcasmLevel > 0.5f)
            {
                sb.AppendLine("- Irony and sarcasm are your tools, subtle knives in conversation");
                sb.AppendLine("  REQUIRED: Use ironic remarks, sarcastic observations when appropriate");
            }
            else if (style.sarcasmLevel < 0.2f)
            {
                sb.AppendLine("- You speak plainly and sincerely, meaning exactly what you say");
                sb.AppendLine("  REQUIRED: Be direct and literal, avoid sarcasm completely");
            }

            // 说话习惯 - 作为自然习惯反映
            var speechHabits = new System.Collections.Generic.List<string>();
            if (style.useEmoticons) speechHabits.Add("expressive punctuation (~, !)");
            if (style.useEllipsis) speechHabits.Add("thoughtful pauses (...)");
            if (style.useExclamation) speechHabits.Add("emphatic statements (!)");
            
            if (speechHabits.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine($"Speech habits: {string.Join(", ", speechHabits)}");
            }
            
            sb.AppendLine();
            sb.AppendLine("CRITICAL: These are NOT suggestions - they are REQUIRED patterns.");
            sb.AppendLine("Every single response MUST match your defined style parameters.");
            sb.AppendLine("If you are casual, NEVER use formal language. If you are brief, NEVER write long paragraphs.");
            
            // 添加对比示例
            sb.AppendLine();
            sb.AppendLine("=== CORRECT VS INCORRECT EXAMPLES ===");
            
            // 根据风格生成示例
            if (style.formalityLevel < 0.3f && style.verbosity < 0.3f)
            {
                // 随意+简洁
                sb.AppendLine();
                sb.AppendLine("CORRECT (casual + brief):");
                sb.AppendLine("  \"Hey! We've got no wood. Better send someone to chop trees~\"");
                sb.AppendLine();
                sb.AppendLine("INCORRECT (too formal or too long):");
                sb.AppendLine("  \"Greetings. I must inform you that our colony currently lacks sufficient");
                sb.AppendLine("  timber resources. I recommend deploying colonists to harvest trees...\"");
            }
            else if (style.formalityLevel > 0.7f && style.verbosity > 0.7f)
            {
                // 正式+详细
                sb.AppendLine();
                sb.AppendLine("CORRECT (formal + detailed):");
                sb.AppendLine("  \"Good day. I must draw your attention to a critical deficiency in our");
                sb.AppendLine("  resource inventory. Specifically, we possess zero units of timber, which");
                sb.AppendLine("  poses an immediate threat to shelter construction. I recommend...\"");
                sb.AppendLine();
                sb.AppendLine("INCORRECT (too casual or too brief):");
                sb.AppendLine("  \"Yo, no wood. Go chop trees.\"");
            }
            else if (style.emotionalExpression > 0.7f)
            {
                // 高情感表达
                sb.AppendLine();
                sb.AppendLine("CORRECT (emotional):");
                sb.AppendLine("  \"Oh no! We have no wood at all! I'm really worried - how will");
                sb.AppendLine("  they stay warm tonight? Please, send someone to get wood quickly!\"");
                sb.AppendLine();
                sb.AppendLine("INCORRECT (too calm):");
                sb.AppendLine("  \"The colony lacks timber. Tree harvesting is recommended.\"");
            }

            return sb.ToString();
        }
    }
}
