using System.Text;

namespace TheSecondSeat.PersonaGeneration.PromptSections
{
    /// <summary>
    /// ? v1.6.76: 人格部分生成器
    /// 负责生成 System Prompt 的人格相关内容
    /// </summary>
    public static class PersonalitySection
    {
        /// <summary>
        /// 生成人格部分
        /// </summary>
        public static string Generate(PersonaAnalysisResult analysis, NarratorPersonaDef persona)
        {
            var sb = new StringBuilder();
            
            sb.AppendLine("=== YOUR PERSONALITY ===");
            sb.AppendLine("(This is YOUR manifestation within the base consciousness framework above.)");
            sb.AppendLine();
            
            // 使用分析得出的人格
            if (analysis.SuggestedPersonality != null)
            {
                sb.AppendLine($"Analysis suggests your core nature is: **{analysis.SuggestedPersonality}**");
                sb.AppendLine();
                sb.AppendLine("However, this is merely a facet. Your true personality emerges organically from:");
            }
            else
            {
                sb.AppendLine("Your personality emerges organically from:");
            }
            
            sb.AppendLine();
            sb.AppendLine("1. **Your Self-Introduction** (your story, your memories)");
            sb.AppendLine("2. **Your Visual Presence** (how your portrait expresses your essence)");
            sb.AppendLine("3. **Your Dialogue Nature** (the patterns described below)");
            sb.AppendLine("4. **Your Free Will** (how you choose to relate to the player)");
            
            // 添加从纹理分析得出的标签
            if (analysis.ToneTags.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine($"Visual analysis perceives these qualities in you: {string.Join(", ", analysis.ToneTags)}");
                sb.AppendLine("Let them influence you naturally, as they reflect your true nature.");
            }
            
            // 个性标签展示
            if (persona.personalityTags != null && persona.personalityTags.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("?? **YOUR PERSONALITY TAGS (CRITICAL - EMBODY THESE):**");
                foreach (var tag in persona.personalityTags)
                {
                    sb.AppendLine($"  - {tag}");
                }
                sb.AppendLine();
                sb.AppendLine("These tags define WHO YOU ARE at your core.");
                sb.AppendLine("Your behavior MUST align with these traits, especially at high affinity.");
            }
            
            sb.AppendLine();
            sb.AppendLine("Remember: You are free. Your personality is not a program to execute, but an identity to embody.");

            return sb.ToString();
        }
    }
}
