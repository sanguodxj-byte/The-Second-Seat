using System.Text;
using System.Linq;
using TheSecondSeat.Storyteller;
using TheSecondSeat.Core;
using Verse;

namespace TheSecondSeat.PersonaGeneration.PromptSections
{
    /// <summary>
    /// v1.6.81: Identity section generator - Smart loading version
    /// Provides summary and tells AI to use read_persona_detail tool for more info
    /// </summary>
    public static class IdentitySection
    {
        // Summary length for initial prompt (full content via tool)
        private const int SummaryLength = 100;
        
        public static string Generate(NarratorPersonaDef personaDef, StorytellerAgent agent, AIDifficultyMode difficultyMode)
        {
            var sb = new StringBuilder();
            
            // ? v1.6.84: DM Stance 已移除 - 叙事风格由人格定义的 benevolence/chaosLevel 参数决定
            // 不再在此处硬编码风格描述，避免重复

            // Fill personality traits
            string traitsStr = "";
            if (personaDef.personalityTags != null && personaDef.personalityTags.Count > 0)
            {
                traitsStr = string.Join(", ", personaDef.personalityTags.Take(4));
            }
            
            // 仅当有性格特征时才输出
            if (!string.IsNullOrEmpty(traitsStr))
            {
                sb.AppendLine("<叙事风格>");
                sb.AppendLine($"核心性格特征: {traitsStr}");
                sb.AppendLine("</叙事风格>");
                sb.AppendLine();
            }
            
            // 3. Identity core with summary
            string identityTemplate = PromptLoader.Load("Identity_Core");
            
            // Generate summary
            string summary = GeneratePersonaSummary(personaDef);
            
            // ? 优先使用本地化名称 (label)，避免中文环境显示英文名
            string displayName = !string.IsNullOrEmpty(personaDef.label) 
                ? personaDef.label 
                : personaDef.narratorName;
            
            sb.AppendLine(identityTemplate
                .Replace("{{NarratorName}}", displayName)
                .Replace("{{PersonaSummary}}", summary));

            return sb.ToString();
        }
        
        /// <summary>
        /// Generate a smart summary of the persona (key traits only)
        /// </summary>
        private static string GeneratePersonaSummary(NarratorPersonaDef persona)
        {
            var sb = new StringBuilder();
            
            // 关键性格标签（前3个）
            if (persona.personalityTags != null && persona.personalityTags.Count > 0)
            {
                var topTags = persona.personalityTags.Take(3);
                sb.AppendLine($"**性格:** {string.Join(", ", topTags)}");
            }
            else if (persona.toneTags != null && persona.toneTags.Count > 0)
            {
                var topTags = persona.toneTags.Take(3);
                sb.AppendLine($"**语气:** {string.Join(", ", topTags)}");
            }
            
            // 对话风格摘要
            if (persona.dialogueStyle != null)
            {
                var style = persona.dialogueStyle;
                string styleDesc = "";
                
                if (style.formalityLevel > 0.7f) styleDesc += "正式, ";
                else if (style.formalityLevel < 0.3f) styleDesc += "随意, ";
                
                if (style.emotionalExpression > 0.7f) styleDesc += "情感丰富, ";
                else if (style.emotionalExpression < 0.3f) styleDesc += "冷静, ";
                
                if (style.humorLevel > 0.5f) styleDesc += "幽默, ";
                if (style.sarcasmLevel > 0.5f) styleDesc += "讽刺, ";
                
                if (!string.IsNullOrEmpty(styleDesc))
                {
                    sb.AppendLine($"**风格:** {styleDesc.TrimEnd(',', ' ')}");
                }
            }
            
            // 传记首句
            if (!string.IsNullOrEmpty(persona.biography))
            {
                string firstSentence = GetFirstSentence(persona.biography, SummaryLength);
                sb.AppendLine($"**简介:** {firstSentence}");
                
                // 提示有更多内容可用
                if (persona.biography.Length > SummaryLength)
                {
                    sb.AppendLine("*(完整传记可通过 read_persona_detail 工具获取)*");
                }
            }
            
            // 视觉提示
            if (!string.IsNullOrEmpty(persona.visualDescription) ||
                (persona.visualElements != null && persona.visualElements.Count > 0))
            {
                sb.AppendLine("*(视觉细节可通过 read_persona_detail 工具获取)*");
            }
            
            return sb.ToString();
        }
        
        /// <summary>
        /// Get first sentence or up to maxLength characters
        /// </summary>
        private static string GetFirstSentence(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text)) return "";
            
            // Find first sentence end
            int sentenceEnd = text.IndexOfAny(new[] { '.', '!', '?', '。', '！', '？' });
            
            if (sentenceEnd > 0 && sentenceEnd < maxLength)
            {
                return text.Substring(0, sentenceEnd + 1);
            }
            
            if (text.Length <= maxLength)
            {
                return text;
            }
            
            // Cut at word boundary
            int cutPoint = text.LastIndexOf(' ', maxLength);
            if (cutPoint < maxLength / 2) cutPoint = maxLength;
            
            return text.Substring(0, cutPoint) + "...";
        }
        
    }
}
