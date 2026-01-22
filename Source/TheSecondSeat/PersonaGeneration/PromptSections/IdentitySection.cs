using System.Text;
using System.Linq;
using System.Collections.Generic;
using TheSecondSeat.Storyteller;
using TheSecondSeat.Core;
using Verse;

namespace TheSecondSeat.PersonaGeneration.PromptSections
{
    /// <summary>
    /// v1.9.9: Identity section generator - 无限标签版本
    /// 输出格式：性格特质标签(带数值) + 外观标签 + 对话风格参数
    /// </summary>
    public static class IdentitySection
    {
        public static string Generate(NarratorPersonaDef personaDef, StorytellerAgent agent, AIDifficultyMode difficultyMode)
        {
            var sb = new StringBuilder();
            
            // 1. 性格特质标签（带数值）- 无数量限制
            string traitsBlock = GenerateTraitsBlock(personaDef);
            if (!string.IsNullOrEmpty(traitsBlock))
            {
                sb.AppendLine("<性格特质>");
                sb.AppendLine(traitsBlock);
                sb.AppendLine("</性格特质>");
                sb.AppendLine();
            }
            
            // 2. 外观标签 - 无数量限制
            string visualBlock = GenerateVisualBlock(personaDef);
            if (!string.IsNullOrEmpty(visualBlock))
            {
                sb.AppendLine("<外观特征>");
                sb.AppendLine(visualBlock);
                sb.AppendLine("</外观特征>");
                sb.AppendLine();
            }
            
            // 3. 对话风格参数（数值化）
            string styleBlock = GenerateDialogueStyleBlock(personaDef);
            if (!string.IsNullOrEmpty(styleBlock))
            {
                sb.AppendLine("<对话风格>");
                sb.AppendLine(styleBlock);
                sb.AppendLine("</对话风格>");
                sb.AppendLine();
            }
            
            // 4. 叙事模式参数
            string narratorBlock = GenerateNarratorModeBlock(personaDef);
            if (!string.IsNullOrEmpty(narratorBlock))
            {
                sb.AppendLine("<叙事模式>");
                sb.AppendLine(narratorBlock);
                sb.AppendLine("</叙事模式>");
                sb.AppendLine();
            }
            
            // 5. 身份核心（名称+简介）
            string identityTemplate = PromptLoader.Load("Identity_Core");
            string displayName = !string.IsNullOrEmpty(personaDef.label) 
                ? personaDef.label 
                : personaDef.narratorName;
            
            string briefBio = GetBriefBio(personaDef);
            
            sb.AppendLine(identityTemplate
                .Replace("{{NarratorName}}", displayName)
                .Replace("{{PersonaSummary}}", briefBio));

            return sb.ToString();
        }
        
        /// <summary>
        /// 生成性格特质块 - 格式: "特质名 数值/1.0"
        /// 无数量限制，读取所有配置的标签
        /// </summary>
        private static string GenerateTraitsBlock(NarratorPersonaDef persona)
        {
            var sb = new StringBuilder();
            
            if (persona.personalityTags != null && persona.personalityTags.Count > 0)
            {
                // 遍历所有标签，无数量限制
                int count = persona.personalityTags.Count;
                for (int i = 0; i < count; i++)
                {
                    string tag = persona.personalityTags[i];
                    
                    // 检查标签是否已包含数值（格式: "温柔:0.8" 或 "温柔 0.8"）
                    if (tag.Contains(":") || (tag.Contains(" ") && char.IsDigit(tag[tag.LastIndexOf(' ') + 1])))
                    {
                        // 已有数值，标准化格式
                        sb.AppendLine(tag.Replace(":", " ") + "/1.0");
                    }
                    else
                    {
                        // 无数值，基于位置自动分配权重（第一个最高）
                        float weight = 1.0f - (i * 0.1f);
                        weight = System.Math.Max(weight, 0.3f);
                        sb.AppendLine($"{tag} {weight:F1}/1.0");
                    }
                }
            }
            else if (persona.toneTags != null && persona.toneTags.Count > 0)
            {
                // 回退到语气标签，无数量限制
                int count = persona.toneTags.Count;
                for (int i = 0; i < count; i++)
                {
                    string tag = persona.toneTags[i];
                    float weight = 1.0f - (i * 0.15f);
                    weight = System.Math.Max(weight, 0.3f);
                    sb.AppendLine($"{tag} {weight:F1}/1.0");
                }
            }
            
            return sb.ToString().TrimEnd();
        }
        
        /// <summary>
        /// 生成外观特征块 - 无数量限制
        /// </summary>
        private static string GenerateVisualBlock(NarratorPersonaDef persona)
        {
            var sb = new StringBuilder();
            
            // 遍历所有视觉元素，无数量限制
            if (persona.visualElements != null && persona.visualElements.Count > 0)
            {
                sb.AppendLine(string.Join(", ", persona.visualElements));
            }
            
            if (!string.IsNullOrEmpty(persona.visualMood))
            {
                sb.AppendLine($"氛围: {persona.visualMood}");
            }
            
            return sb.ToString().TrimEnd();
        }
        
        /// <summary>
        /// 生成对话风格块 - 格式: "参数名: 数值/1.0"
        /// </summary>
        private static string GenerateDialogueStyleBlock(NarratorPersonaDef persona)
        {
            if (persona.dialogueStyle == null) return "";
            
            var style = persona.dialogueStyle;
            var sb = new StringBuilder();
            
            // 核心风格参数（全部输出，让AI理解完整人格）
            sb.AppendLine($"正式度: {style.formalityLevel:F1}/1.0");
            sb.AppendLine($"情感度: {style.emotionalExpression:F1}/1.0");
            sb.AppendLine($"详细度: {style.verbosity:F1}/1.0");
            sb.AppendLine($"幽默度: {style.humorLevel:F1}/1.0");
            sb.AppendLine($"讽刺度: {style.sarcasmLevel:F1}/1.0");
            
            // 标点风格（布尔转为描述）
            var punctStyles = new List<string>();
            if (style.useEmoticons) punctStyles.Add("表情符号");
            if (style.useEllipsis) punctStyles.Add("省略号");
            if (style.useExclamation) punctStyles.Add("感叹号");
            
            if (punctStyles.Count > 0)
            {
                sb.AppendLine($"标点偏好: {string.Join(", ", punctStyles)}");
            }
            
            // 礼貌度（从语气标签推断）
            float politeness = 0.5f;
            if (persona.toneTags != null)
            {
                if (persona.toneTags.Any(t => t.Contains("礼貌") || t.Contains("恭敬")))
                    politeness = 0.8f;
                else if (persona.toneTags.Any(t => t.Contains("傲慢") || t.Contains("高冷")))
                    politeness = 0.3f;
            }
            sb.AppendLine($"礼貌度: {politeness:F1}/1.0");
            
            return sb.ToString().TrimEnd();
        }
        
        /// <summary>
        /// 生成叙事模式块 - 基于 mercyLevel/chaosLevel/dominanceLevel
        /// </summary>
        private static string GenerateNarratorModeBlock(NarratorPersonaDef persona)
        {
            var sb = new StringBuilder();
            
            sb.AppendLine($"仁慈度: {persona.mercyLevel:F1}/1.0");
            sb.AppendLine($"混乱度: {persona.narratorChaosLevel:F1}/1.0");
            sb.AppendLine($"强势度: {persona.dominanceLevel:F1}/1.0");
            
            return sb.ToString().TrimEnd();
        }
        
        /// <summary>
        /// 获取简短传记（首句或前80字符）
        /// </summary>
        private static string GetBriefBio(NarratorPersonaDef persona)
        {
            if (string.IsNullOrEmpty(persona.biography)) 
                return "(无传记)";
            
            string bio = persona.biography;
            
            // 查找首句
            int sentenceEnd = bio.IndexOfAny(new[] { '.', '!', '?', '。', '！', '？' });
            if (sentenceEnd > 0 && sentenceEnd < 80)
            {
                return bio.Substring(0, sentenceEnd + 1);
            }
            
            // 截断到80字符
            if (bio.Length <= 80) return bio;
            
            return bio.Substring(0, 80) + "...";
        }
    }
}
