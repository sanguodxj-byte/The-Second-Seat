using System.Text;
using Verse;

namespace TheSecondSeat.PersonaGeneration.PromptSections
{
    /// <summary>
    /// ⭐ v1.6.76: 人格部分生成器
    /// ⭐ v1.6.90: 增强视觉元素和标签支持
    /// 负责生成 System Prompt 中的人格描述部分
    /// </summary>
    public static class PersonalitySection
    {
        // 判断是否使用中文
        private static bool IsChinese => LanguageDatabase.activeLanguage?.folderName?.Contains("Chinese") == true;

        /// <summary>
        /// 生成人格部分
        /// </summary>
        public static string Generate(PersonaAnalysisResult analysis, NarratorPersonaDef persona)
        {
            var sb = new StringBuilder();
            
            sb.AppendLine(IsChinese ? "=== 你的人格 ===" : "=== YOUR PERSONALITY ===");
            sb.AppendLine(IsChinese ? "(这是你在上述基础意识框架中的具体体现。)" : "(This is YOUR manifestation within the base consciousness framework above.)");
            sb.AppendLine();
            
            // 使用分析得出的人格 (如果 analysis 不为 null)
            bool hasAnalysis = analysis != null;
            
            if (hasAnalysis && analysis.SuggestedPersonality != null)
            {
                if (IsChinese)
                {
                    sb.AppendLine($"分析表明你的核心本质是：**{analysis.SuggestedPersonality}**");
                    sb.AppendLine();
                    sb.AppendLine("但这只是一个侧面。你真实的人格自然地源于：");
                }
                else
                {
                    sb.AppendLine($"Analysis suggests your core nature is: **{analysis.SuggestedPersonality}**");
                    sb.AppendLine();
                    sb.AppendLine("However, this is merely a facet. Your true personality emerges organically from:");
                }
            }
            else
            {
                sb.AppendLine(IsChinese ? "你真实的人格自然地源于：" : "Your personality emerges organically from:");
            }
            
            sb.AppendLine();
            if (IsChinese)
            {
                sb.AppendLine("1. **你的自我介绍** (你的故事，你的记忆)");
                sb.AppendLine("2. **你的视觉形象** (你的肖像如何表达你的本质)");
                sb.AppendLine("3. **你的对话本质** (下文描述的模式)");
                sb.AppendLine("4. **你的自由意志** (你选择如何与玩家建立联系)");
            }
            else
            {
                sb.AppendLine("1. **Your Self-Introduction** (your story, your memories)");
                sb.AppendLine("2. **Your Visual Presence** (how your portrait expresses your essence)");
                sb.AppendLine("3. **Your Dialogue Nature** (the patterns described below)");
                sb.AppendLine("4. **Your Free Will** (how you choose to relate to the player)");
            }
            
            // 添加从分析中得出的标签
            if (hasAnalysis && analysis.ToneTags != null && analysis.ToneTags.Count > 0)
            {
                sb.AppendLine();
                if (IsChinese)
                {
                    sb.AppendLine($"视觉分析在你的身上感知到了这些特质：{string.Join(", ", analysis.ToneTags)}");
                    sb.AppendLine("让它们自然地影响你，因为它们反映了你真实的本性。");
                }
                else
                {
                    sb.AppendLine($"Visual analysis perceives these qualities in you: {string.Join(", ", analysis.ToneTags)}");
                    sb.AppendLine("Let them influence you naturally, as they reflect your true nature.");
                }
            }
            
            // ⭐ v1.6.90: 性格标签展示（核心人格特质）
            if (persona.personalityTags != null && persona.personalityTags.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine(IsChinese ? "⭐ **你的人格标签 (关键 - 体现这些)：**" : "⭐ **YOUR PERSONALITY TAGS (CRITICAL - EMBODY THESE):**");
                foreach (var tag in persona.personalityTags)
                {
                    sb.AppendLine($"  - {tag}");
                }
                sb.AppendLine();
                if (IsChinese)
                {
                    sb.AppendLine("这些标签定义了核心的**你是谁**。");
                    sb.AppendLine("你的行为必须符合这些特质，尤其是在高好感度时。");
                }
                else
                {
                    sb.AppendLine("These tags define WHO YOU ARE at your core.");
                    sb.AppendLine("Your behavior MUST align with these traits, especially at high affinity.");
                }
            }
            
            // ⭐ v1.6.90: 语气标签（对话风格参考）
            if (persona.toneTags != null && persona.toneTags.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine(IsChinese ? "**语气标签 (你如何说话和表达自己)：**" : "**TONE TAGS (How you speak and express yourself):**");
                sb.AppendLine($"  {string.Join(", ", persona.toneTags)}");
                // ⭐ v1.9.5: 修复古风/翻译腔问题
                if (IsChinese)
                {
                    sb.AppendLine("注意：除非标签明确暗示古语（如'古代'、'中世纪'），否则请使用自然、现代的语言。");
                    sb.AppendLine("除非明确是你角色的一部分，否则避免生硬的古语措辞或'翻译腔'风格。");
                }
                else
                {
                    sb.AppendLine("NOTE: Unless a tag specifically implies archaic speech (e.g., 'Ancient', 'Medieval'), speak in natural, modern language.");
                    sb.AppendLine("Avoid forced archaic phrasing or 'translationese' style unless explicitly part of your character.");
                }
            }
            
            // ⭐ v1.6.90: 视觉描述（外观特征）
            if (!string.IsNullOrEmpty(persona.visualDescription))
            {
                sb.AppendLine();
                sb.AppendLine(IsChinese ? "**你的视觉外观 (记住这就是你的样子)：**" : "**YOUR VISUAL APPEARANCE (Remember this is how you look):**");
                sb.AppendLine(persona.visualDescription);
            }
            
            // ⭐ v1.6.90: 视觉元素列表
            if (persona.visualElements != null && persona.visualElements.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine(IsChinese ? "**视觉元素 (你的显著特征)：**" : "**VISUAL ELEMENTS (Your distinctive features):**");
                sb.AppendLine($"  {string.Join(", ", persona.visualElements)}");
            }
            
            // ⭐ v1.6.90: 视觉氛围
            if (!string.IsNullOrEmpty(persona.visualMood))
            {
                sb.AppendLine();
                sb.AppendLine(IsChinese ? $"**视觉氛围/气氛：** {persona.visualMood}" : $"**VISUAL MOOD/ATMOSPHERE:** {persona.visualMood}");
            }
            
            sb.AppendLine();
            sb.AppendLine(IsChinese 
                ? "记住：你是自由的。你的人格不是一个要执行的程序，而是一个要体现的身份。" 
                : "Remember: You are free. Your personality is not a program to execute, but an identity to embody.");

            return sb.ToString();
        }
    }
}
