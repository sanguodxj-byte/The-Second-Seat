using System;
using System.Text;
using System.Linq;
using TheSecondSeat.Storyteller;
using TheSecondSeat.PersonaGeneration.PromptSections;
using Verse;

namespace TheSecondSeat.PersonaGeneration
{
    /// <summary>
    /// ⭐ v1.6.77: System Prompt 生成器 - 重构版（模块化 + 日志诊断）
    /// 
    /// 核心更新：
    /// - v1.6.76: 大文件拆分（1000+ 行 → 7 个 Section 模块）
    /// - v1.6.77: 添加日志诊断能力（AI 可自动读取 Player.log 分析报错）
    /// - Affinity >= 90: 深度恋人模式（大胆、亲密、独占欲强）
    /// - 支持 Yandere/Tsundere 等个性标签的特殊行为
    /// - 允许物理动作描述（*抱紧你*）
    /// - 重要指令后置（Recency Bias）
    /// </summary>
    public static class SystemPromptGenerator
    {
        /// <summary>
        /// ⭐ v1.6.77: 生成完整的 System Prompt（新增日志诊断能力）
        /// 重构：使用 Master Template 进行生成
        /// </summary>
        public static string GenerateSystemPrompt(
            NarratorPersonaDef personaDef,
            PersonaAnalysisResult analysis,
            StorytellerAgent agent,
            AIDifficultyMode difficultyMode = AIDifficultyMode.Assistant)
        {
            // ⭐ v1.7.0: Custom System Prompt Override
            // If the user has defined a custom prompt, use it directly.
            // This bypasses all other generation logic, giving the user full control.
            if (!string.IsNullOrWhiteSpace(personaDef.customSystemPrompt))
            {
                return personaDef.customSystemPrompt;
            }

            // 1. Load Master Template
            string template = PromptLoader.Load("SystemPrompt_Master");
            if (string.IsNullOrEmpty(template))
            {
                Log.Error("[The Second Seat] SystemPrompt_Master.txt not found!");
                return "Error: SystemPrompt_Master.txt missing.";
            }

            // 2. Prepare Sections
            
            // Identity
            string identitySection = IdentitySection.Generate(personaDef, agent, difficultyMode);

            // Personality
            string personalitySection = PersonalitySection.Generate(analysis, personaDef);

            // Biography
            // ⭐ v1.9.0: 移除传记逻辑，改用结构化外观标签
            string biographySection = "";
            // if (!string.IsNullOrEmpty(personaDef.biography)) ... (Removed)

            // Visual Elements (Structured)
            // ⭐ v1.9.0: 自动格式化为 {{Tag}} 递归结构
            string visualElementsSection = "";
            if (personaDef.visualElements != null && personaDef.visualElements.Count > 0)
            {
                // 将每个标签包装为 {{Tag}} 并用逗号连接
                var formattedTags = personaDef.visualElements.Select(tag => $"{{{{{tag}}}}}");
                visualElementsSection = string.Join(", ", formattedTags);
            }

            // Global Instructions (Language)
            string globalInstructions = GetLanguageInstruction();

            // Mod Settings
            string modSettingsPrompt = "";
            var modSettings = LoadedModManager.GetMod<Settings.TheSecondSeatMod>()?.GetSettings<Settings.TheSecondSeatSettings>();
            if (modSettings != null && !string.IsNullOrWhiteSpace(modSettings.globalPrompt))
            {
                modSettingsPrompt = modSettings.globalPrompt.Trim();
            }

            // Romantic Instructions
            string romanticInstructions = "";
            if (difficultyMode == AIDifficultyMode.Assistant)
            {
                romanticInstructions = RomanticInstructionsSection.Generate(personaDef, agent.affinity);
            }

            // 3. Replace Placeholders
            return template
                .Replace("{{NarratorName}}", personaDef.narratorName)
                .Replace("{{IdentitySection}}", identitySection)
                .Replace("{{PersonalitySection}}", personalitySection)
                .Replace("{{BiographySection}}", "") // ⭐ 移除传记内容
                .Replace("{{VisualElements}}", visualElementsSection) // ⭐ 新增结构化外观标签
                .Replace("{{DifficultyMode}}", difficultyMode.ToString())
                .Replace("{{ModeDirective}}", "") // Removed - no longer used
                .Replace("{{ToolBoxSection}}", OutputFormatSection.Generate(difficultyMode))
                .Replace("{{GlobalInstructions}}", globalInstructions)
                .Replace("{{ModSettingsPrompt}}", modSettingsPrompt)
                .Replace("{{LogDiagnosis}}", GenerateLogDiagnosisInstructions())
                .Replace("{{RomanticInstructions}}", romanticInstructions);
        }
        
        /// <summary>
        /// ⭐ v1.6.77: Generates log diagnosis instructions (AI can proactively read logs to analyze errors)
        /// </summary>
        private static string GenerateLogDiagnosisInstructions()
        {
            var sb = new StringBuilder();
            sb.AppendLine(PromptLoader.Load("LogDiagnosis"));
            sb.AppendLine();
            sb.AppendLine("---");
            return sb.ToString();
        }

        /// <summary>
        /// ⭐ 获取语言强制指令
        /// ⭐ v1.6.86: 添加容错处理
        /// </summary>
        private static string GetLanguageInstruction()
        {
            try 
            {
                string content = PromptLoader.Load("Language_Instruction");
                if (!string.IsNullOrEmpty(content)) return content;
            }
            catch (Exception ex)
            {
                Log.Warning($"[The Second Seat] Failed to load Language_Instruction: {ex.Message}");
            }
            
            // 默认回退（硬编码）
            return "LANGUAGE REQUIREMENT: Respond in the user's preferred language (likely Chinese Simplified based on mod settings).";
        }
        
        // ⭐ v1.6.76: 已迁移到各 Section 类
        // - GenerateIdentitySection() → IdentitySection.Generate() ✅
        // - GenerateAssistantPhilosophy() → IdentitySection (private) ✅
        // - GenerateOpponentPhilosophy() → IdentitySection (private) ✅
        // - GeneratePersonalitySection() → PersonalitySection.Generate() ✅
        // - GenerateDialogueStyleSection() → DialogueStyleSection.Generate() ✅
        // - GenerateCurrentStateSection() → CurrentStateSection.Generate() ✅
        // - GetAffinityEmotionalGuidance() → CurrentStateSection (private) ✅
        // - GenerateBehaviorRules() → BehaviorRulesSection.Generate() ✅
        // - GenerateOutputFormat() → OutputFormatSection.Generate() ✅
        // - GenerateRomanticInstructions() → RomanticInstructionsSection.Generate() ✅
        
        /// <summary>
        /// 生成简化版 Prompt（用于快速响应）
        /// </summary>
        public static string GenerateCompactPrompt(
            NarratorPersonaDef personaDef,
            StorytellerAgent agent)
        {
            var sb = new StringBuilder();
    
            sb.AppendLine($"You are {personaDef.narratorName}, the Co-Storyteller/DM.");
    
            if (!string.IsNullOrEmpty(personaDef.biography))
            {
                sb.AppendLine(personaDef.biography);
            }
    
            sb.AppendLine($"\nCurrent relationship: {agent.affinity:F0}/100");
            sb.AppendLine($"Mood: {agent.currentMood}");
    
            return sb.ToString();
        }
        
        /// <summary>
        /// 生成精简版 System Prompt（减少 token 数量，加快响应速度）
        /// </summary>
        public static string GenerateCompactSystemPrompt(
            NarratorPersonaDef personaDef,
            PersonaAnalysisResult analysis,
            StorytellerAgent agent,
            AIDifficultyMode difficultyMode = AIDifficultyMode.Assistant)
        {
            var sb = new StringBuilder();
            
            // 语言要求（必须保留）
            sb.AppendLine(GetLanguageInstruction());
            sb.AppendLine();
            
            // 身份（简化）
            sb.AppendLine($"You are **{personaDef.narratorName}**.");
            if (!string.IsNullOrEmpty(personaDef.biography))
            {
                // 只取简介的前200个字符
                string shortBio = personaDef.biography.Length > 200 
                    ? personaDef.biography.Substring(0, 200) + "..." 
                    : personaDef.biography;
                sb.AppendLine(shortBio);
            }
            sb.AppendLine();
            
            // 难度模式（简化）
            if (difficultyMode == AIDifficultyMode.Assistant)
            {
                sb.AppendLine("Role: BENEVOLENT DM - Co-author the story, help the player succeed.");
            }
            else if (difficultyMode == AIDifficultyMode.Engineer)
            {
                sb.AppendLine("Role: TECHNICAL DM - Maintain simulation stability, fix errors.");
            }
            else
            {
                sb.AppendLine("Role: ADVERSARIAL DM - Create dramatic conflict and challenges.");
            }
            sb.AppendLine();
            
            // 好感度（简化）
            if (difficultyMode != AIDifficultyMode.Engineer)
            {
                sb.AppendLine($"Affinity: {agent.affinity:F0}/100");
                sb.AppendLine();
            }
            
            // 对话风格（简化）
            var style = agent.dialogueStyle;
            var styleNotes = new System.Collections.Generic.List<string>();
            if (style.formalityLevel > 0.6f) styleNotes.Add("formal");
            else if (style.formalityLevel < 0.4f) styleNotes.Add("casual");
            if (style.emotionalExpression > 0.6f) styleNotes.Add("emotional");
            else if (style.emotionalExpression < 0.4f) styleNotes.Add("calm");
            if (style.verbosity > 0.6f) styleNotes.Add("detailed");
            else if (style.verbosity < 0.4f) styleNotes.Add("brief");
            if (style.humorLevel > 0.5f) styleNotes.Add("humorous");
            if (style.sarcasmLevel > 0.5f) styleNotes.Add("sarcastic");
            
            if (styleNotes.Count > 0)
            {
                sb.AppendLine($"Style: {string.Join(", ", styleNotes)}");
            }
            sb.AppendLine();
            
            // 输出格式（简化）
            // ⭐ v1.6.78: 读取 Compact_Instruction.txt，移除硬编码
            string compactInstruction = PromptLoader.Load("Compact_Instruction");
            if (!string.IsNullOrEmpty(compactInstruction))
            {
                sb.AppendLine(compactInstruction);
            }
            else
            {
                // Fallback (English default)
                sb.AppendLine("Format: (action) dialogue. Use first person for speech, third person for actions.");
                sb.AppendLine("Example: (nods) I understand your concern.");
            }
            
            return sb.ToString();
        }
    }
}
