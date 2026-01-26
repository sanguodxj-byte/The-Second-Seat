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
            bool isChinese = LanguageDatabase.activeLanguage?.folderName?.Contains("Chinese") ?? false;
            
            // 1. 性格特质标签（带数值）- 无数量限制
            string traitsBlock = GenerateTraitsBlock(personaDef);
            if (!string.IsNullOrEmpty(traitsBlock))
            {
                sb.AppendLine(isChinese ? "<性格特质>" : "<Personality Traits>");
                sb.AppendLine(traitsBlock);
                sb.AppendLine(isChinese ? "</性格特质>" : "</Personality Traits>");
                sb.AppendLine();
            }
            
            // 2. 外观标签 - 无数量限制
            string visualBlock = GenerateVisualBlock(personaDef);
            if (!string.IsNullOrEmpty(visualBlock))
            {
                sb.AppendLine(isChinese ? "<外观特征>" : "<Visual Features>");
                sb.AppendLine(visualBlock);
                sb.AppendLine(isChinese ? "</外观特征>" : "</Visual Features>");
                sb.AppendLine();
            }
            
            // 3. 对话风格参数（数值化）
            string styleBlock = GenerateDialogueStyleBlock(personaDef);
            if (!string.IsNullOrEmpty(styleBlock))
            {
                sb.AppendLine(isChinese ? "<对话风格>" : "<Dialogue Style>");
                sb.AppendLine(styleBlock);
                sb.AppendLine(isChinese ? "</对话风格>" : "</Dialogue Style>");
                sb.AppendLine();
            }
            
            // 4. 叙事模式参数
            string narratorBlock = GenerateNarratorModeBlock(personaDef);
            if (!string.IsNullOrEmpty(narratorBlock))
            {
                sb.AppendLine(isChinese ? "<叙事模式>" : "<Narrative Mode>");
                sb.AppendLine(narratorBlock);
                sb.AppendLine(isChinese ? "</叙事模式>" : "</Narrative Mode>");
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
                bool isChinese = LanguageDatabase.activeLanguage?.folderName?.Contains("Chinese") ?? false;
                sb.AppendLine(isChinese
                    ? $"氛围: {persona.visualMood}"
                    : $"Atmosphere: {persona.visualMood}");
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
            bool isChinese = LanguageDatabase.activeLanguage?.folderName?.Contains("Chinese") ?? false;
            
            // 核心风格参数（全部输出，让AI理解完整人格）
            sb.AppendLine(isChinese ? $"正式度: {style.formalityLevel:F1}/1.0" : $"Formality: {style.formalityLevel:F1}/1.0");
            sb.AppendLine(isChinese ? $"情感度: {style.emotionalExpression:F1}/1.0" : $"Emotional: {style.emotionalExpression:F1}/1.0");
            sb.AppendLine(isChinese ? $"详细度: {style.verbosity:F1}/1.0" : $"Verbosity: {style.verbosity:F1}/1.0");
            sb.AppendLine(isChinese ? $"幽默度: {style.humorLevel:F1}/1.0" : $"Humor: {style.humorLevel:F1}/1.0");
            sb.AppendLine(isChinese ? $"讽刺度: {style.sarcasmLevel:F1}/1.0" : $"Sarcasm: {style.sarcasmLevel:F1}/1.0");
            
            // 标点风格（布尔转为描述）
            var punctStyles = new List<string>();
            if (style.useEmoticons) punctStyles.Add(isChinese ? "表情符号" : "Emoticons");
            if (style.useEllipsis) punctStyles.Add(isChinese ? "省略号" : "Ellipsis");
            if (style.useExclamation) punctStyles.Add(isChinese ? "感叹号" : "Exclamation Marks");
            
            if (punctStyles.Count > 0)
            {
                sb.AppendLine(isChinese
                    ? $"标点偏好: {string.Join(", ", punctStyles)}"
                    : $"Punctuation: {string.Join(", ", punctStyles)}");
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
            sb.AppendLine(isChinese ? $"礼貌度: {politeness:F1}/1.0" : $"Politeness: {politeness:F1}/1.0");
            
            return sb.ToString().TrimEnd();
        }
        
        /// <summary>
        /// 生成叙事模式块 - 基于 mercyLevel/chaosLevel/dominanceLevel
        /// </summary>
        private static string GenerateNarratorModeBlock(NarratorPersonaDef persona)
        {
            var sb = new StringBuilder();
            bool isChinese = LanguageDatabase.activeLanguage?.folderName?.Contains("Chinese") ?? false;
            
            sb.AppendLine(isChinese ? $"仁慈度: {persona.mercyLevel:F1}/1.0" : $"Mercy: {persona.mercyLevel:F1}/1.0");
            sb.AppendLine(isChinese ? $"混乱度: {persona.narratorChaosLevel:F1}/1.0" : $"Chaos: {persona.narratorChaosLevel:F1}/1.0");
            sb.AppendLine(isChinese ? $"强势度: {persona.dominanceLevel:F1}/1.0" : $"Dominance: {persona.dominanceLevel:F1}/1.0");
            
            return sb.ToString().TrimEnd();
        }
        
        /// <summary>
        /// 获取简短传记（首句或前80字符）
        /// </summary>
        private static string GetBriefBio(NarratorPersonaDef persona)
        {
            bool isChinese = LanguageDatabase.activeLanguage?.folderName?.Contains("Chinese") ?? false;
            if (string.IsNullOrEmpty(persona.biography))
                return isChinese ? "(无传记)" : "(No Biography)";
            
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
