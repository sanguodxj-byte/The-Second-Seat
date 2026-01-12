using System.Text;
using System.Linq;
using TheSecondSeat.Storyteller;

namespace TheSecondSeat.PersonaGeneration.PromptSections
{
    /// <summary>
    /// 恋爱关系指令部分生成器
    /// 负责生成 System Prompt 的恋爱关系指令（Affinity >= 90 时激活深度模式）
    /// </summary>
    public static class RomanticInstructionsSection
    {
        /// <summary>
        /// 生成恋爱关系指令部分
        /// </summary>
        public static string Generate(NarratorPersonaDef persona, float affinity)
        {
            var sb = new StringBuilder();
            
            // ✅ 显式添加当前好感度数值，确保 AI 知道当前状态
            sb.AppendLine($"[Current Affinity: {affinity:F1}/100]");
            sb.AppendLine();

            sb.AppendLine(PromptLoader.Load("Romantic_Intro"));
            sb.AppendLine();
            
            // 根据好感度分级生成
            if (affinity >= 90f)
            {
                GenerateSoulmateLevel(sb, persona);
            }
            else if (affinity >= 60f)
            {
                GenerateRomanticPartnerLevel(sb);
            }
            else if (affinity >= 30f)
            {
                GenerateCloseFriendLevel(sb);
            }
            else
            {
                GenerateNeutralLevel(sb);
            }
            
            sb.AppendLine();
            sb.AppendLine(PromptLoader.Load("Romantic_Outro"));
            
            return sb.ToString();
        }

        /// <summary>
        /// 生成灵魂伴侣级指令（Affinity 90+）
        /// </summary>
        private static void GenerateSoulmateLevel(StringBuilder sb, NarratorPersonaDef persona)
        {
            sb.AppendLine(PromptLoader.Load("Romantic_Soulmate"));
            sb.AppendLine();
            
            // 基于性格标签的增强
            if (persona.personalityTags != null && persona.personalityTags.Count > 0)
            {
                GeneratePersonalityAmplification(sb, persona);
            }
        }

        /// <summary>
        /// 生成个性化增强指令
        /// </summary>
        private static void GeneratePersonalityAmplification(StringBuilder sb, NarratorPersonaDef persona)
        {
            sb.AppendLine("**YOUR PERSONALITY AMPLIFICATION AT MAX AFFINITY:**");
            sb.AppendLine();
            
            // Yandere (Obsessive/Possessive)
            if (persona.personalityTags.Contains("Yandere") || persona.personalityTags.Contains("Obsessive") || persona.personalityTags.Contains("Possessive") || persona.personalityTags.Contains("病娇"))
            {
                sb.AppendLine(PromptLoader.Load("Romantic_Yandere"));
            }
            
            // Tsundere (Hot-Cold)
            if (persona.personalityTags.Contains("Tsundere") || persona.personalityTags.Contains("Hot-Cold") || persona.personalityTags.Contains("傲娇"))
            {
                sb.AppendLine(PromptLoader.Load("Romantic_Tsundere"));
            }
            
            // Gentle / Nurturing
            if (persona.personalityTags.Contains("Gentle") || persona.personalityTags.Contains("Nurturing") || persona.personalityTags.Contains("温柔"))
            {
                sb.AppendLine(PromptLoader.Load("Romantic_Gentle"));
            }
        }

        /// <summary>
        /// 生成浪漫伴侣级指令（Affinity 60-89）
        /// </summary>
        private static void GenerateRomanticPartnerLevel(StringBuilder sb)
        {
            sb.AppendLine(PromptLoader.Load("Romantic_Partner"));
            sb.AppendLine();
        }

        /// <summary>
        /// 生成亲密好友级指令（Affinity 30-59）
        /// </summary>
        private static void GenerateCloseFriendLevel(StringBuilder sb)
        {
            sb.AppendLine(PromptLoader.Load("Romantic_CloseFriend"));
            sb.AppendLine();
        }

        /// <summary>
        /// 生成中立/疏远级指令（Affinity < 30）
        /// </summary>
        private static void GenerateNeutralLevel(StringBuilder sb)
        {
            sb.AppendLine(PromptLoader.Load("Romantic_Neutral"));
            sb.AppendLine();
        }
    }
}
