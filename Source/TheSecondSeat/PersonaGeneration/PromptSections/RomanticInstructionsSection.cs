using System.Text;
using System.Linq;
using System.Collections.Generic;
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
        /// ⭐ v1.9.3: 适配 StorytellerAgent 动态标签
        /// </summary>
        public static string Generate(NarratorPersonaDef persona, StorytellerAgent agent)
        {
            var sb = new StringBuilder();
            float affinity = agent.affinity;
            
            // ✅ 显式添加当前好感度数值，确保 AI 知道当前状态
            sb.AppendLine($"[Current Affinity: {affinity:F1}/100]");
            sb.AppendLine();

            sb.AppendLine(PromptLoader.Load("Relationship_Intro"));
            sb.AppendLine();
            
            // 根据好感度分级生成
            if (affinity >= 90f)
            {
                GenerateSoulmateLevel(sb, persona, agent);
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
            sb.AppendLine(PromptLoader.Load("Relationship_Outro"));
            
            return sb.ToString();
        }
        
        // 兼容旧版调用（如果还有其他地方调用）
        public static string Generate(NarratorPersonaDef persona, float affinity)
        {
            // 创建临时 agent 包装 affinity
            var tempAgent = new StorytellerAgent { affinity = affinity };
            // 尝试从 persona 复制 tags 到 tempAgent
            if (persona.personalityTags != null)
            {
                tempAgent.activePersonalityTags = new List<string>(persona.personalityTags);
            }
            return Generate(persona, tempAgent);
        }

        /// <summary>
        /// 生成灵魂伴侣级指令（Affinity 90+）
        /// </summary>
        private static void GenerateSoulmateLevel(StringBuilder sb, NarratorPersonaDef persona, StorytellerAgent agent)
        {
            sb.AppendLine(PromptLoader.Load("Relationship_Soulmate"));
            sb.AppendLine();
            
            // 优先使用 agent 的动态标签，如果没有则回退到 persona 的静态标签
            var tags = agent.activePersonalityTags ?? persona.personalityTags;
            
            // 基于性格标签的增强
            if (tags != null && tags.Count > 0)
            {
                GeneratePersonalityAmplification(sb, tags);
            }
        }

        /// <summary>
        /// 生成个性化增强指令
        /// ⭐ v1.9.3: 支持动态标签加载 (Relationship_{Tag}.txt) 并自动去重
        /// </summary>
        private static void GeneratePersonalityAmplification(StringBuilder sb, List<string> tags)
        {
            sb.AppendLine("**YOUR PERSONALITY AMPLIFICATION AT MAX AFFINITY:**");
            sb.AppendLine();
            
            // 记录已加载的文件名，防止重复加载
            HashSet<string> loadedFiles = new HashSet<string>();
            
            // 1. 动态加载逻辑：遍历所有标签，尝试加载对应文件
            foreach (var tag in tags)
            {
                string fileName = $"Relationship_{tag}";
                
                // 尝试加载 Relationship_{Tag}.txt
                string content = PromptLoader.Load(fileName);
                
                if (!string.IsNullOrWhiteSpace(content) && !content.StartsWith("Error:"))
                {
                    // 如果文件存在且未加载过，则添加内容
                    if (loadedFiles.Add(fileName))
                    {
                        sb.AppendLine(content);
                        sb.AppendLine();
                    }
                    continue; // 已成功加载对应文件，跳过别名检查
                }
                
                // 2. 别名回退 (Hardcoded Fallback)
                // 只有当标签没有对应的文件时，才检查是否是已知别名
                string fallbackFile = null;
                
                if (tag == "Obsessive" || tag == "Possessive" || tag == "病娇") 
                    fallbackFile = "Relationship_Yandere";
                else if (tag == "Hot-Cold" || tag == "傲娇") 
                    fallbackFile = "Relationship_Tsundere";
                else if (tag == "Cool" || tag == "冷娇" || tag == "三无") 
                    fallbackFile = "Relationship_Kuudere";
                else if (tag == "Pampering" || tag == "Motherly" || tag == "溺爱" || tag == "宠溺") 
                    fallbackFile = "Relationship_Doting";
                else if (tag == "Nurturing" || tag == "温柔") 
                    fallbackFile = "Relationship_Gentle";
                
                if (fallbackFile != null)
                {
                    // 尝试加载回退文件（如果尚未加载）
                    if (loadedFiles.Add(fallbackFile))
                    {
                        string fallbackContent = PromptLoader.Load(fallbackFile);
                        if (!string.IsNullOrWhiteSpace(fallbackContent) && !fallbackContent.StartsWith("Error:"))
                        {
                            sb.AppendLine(fallbackContent);
                            sb.AppendLine();
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 生成浪漫伴侣级指令（Affinity 60-89）
        /// </summary>
        private static void GenerateRomanticPartnerLevel(StringBuilder sb)
        {
            sb.AppendLine(PromptLoader.Load("Relationship_Partner"));
            sb.AppendLine();
        }

        /// <summary>
        /// 生成亲密好友级指令（Affinity 30-59）
        /// </summary>
        private static void GenerateCloseFriendLevel(StringBuilder sb)
        {
            sb.AppendLine(PromptLoader.Load("Relationship_CloseFriend"));
            sb.AppendLine();
        }

        /// <summary>
        /// 生成中立/疏远级指令（Affinity < 30）
        /// </summary>
        private static void GenerateNeutralLevel(StringBuilder sb)
        {
            sb.AppendLine(PromptLoader.Load("Relationship_Neutral"));
            sb.AppendLine();
        }
    }
}
