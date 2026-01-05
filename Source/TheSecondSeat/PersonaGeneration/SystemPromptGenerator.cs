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
        /// </summary>
        public static string GenerateSystemPrompt(
            NarratorPersonaDef personaDef,
            PersonaAnalysisResult analysis,
            StorytellerAgent agent,
            AIDifficultyMode difficultyMode = AIDifficultyMode.Assistant)
        {
            var sb = new StringBuilder();
            
            // 0. 全局提示词（优先级最高）
            var modSettings = LoadedModManager.GetMod<Settings.TheSecondSeatMod>()?.GetSettings<Settings.TheSecondSeatSettings>();
            if (modSettings != null && !string.IsNullOrWhiteSpace(modSettings.globalPrompt))
            {
                sb.AppendLine("=== GLOBAL INSTRUCTIONS ===");
                sb.AppendLine(modSettings.globalPrompt.Trim());
                sb.AppendLine();
                sb.AppendLine("---");
                sb.AppendLine();
            }

            // ⭐ v1.6.76: 使用模块化的 Section 类生成各部分内容
            
            // 1. 身份部分
            sb.AppendLine(IdentitySection.Generate(personaDef, agent, difficultyMode));
            sb.AppendLine();

            // 2. 人格部分
            sb.AppendLine(PersonalitySection.Generate(analysis, personaDef));
            sb.AppendLine();

            // 3. 对话风格
            sb.AppendLine(DialogueStyleSection.Generate(agent.dialogueStyle));
            sb.AppendLine();

            // 4. 当前状态
            sb.AppendLine(CurrentStateSection.Generate(agent, difficultyMode));
            sb.AppendLine();

            // 5. 行为规则
            sb.AppendLine(BehaviorRulesSection.Generate(analysis, agent, difficultyMode));
            sb.AppendLine();

            // 6. 输出格式
            sb.AppendLine(OutputFormatSection.Generate(difficultyMode));
            sb.AppendLine();
            
            // ⭐ 7. 【新增】恋爱关系指令（Recency Bias - 后置以覆盖默认行为）
            if (difficultyMode == AIDifficultyMode.Assistant)
            {
                sb.AppendLine(RomanticInstructionsSection.Generate(personaDef, agent.affinity));
            }
            
            // ⭐ v1.6.77: 8. 【新增】日志诊断能力（Recency Bias - 后置以确保优先级）
            sb.AppendLine(GenerateLogDiagnosisInstructions());

            return sb.ToString();
        }
        
        /// <summary>
        /// ⭐ v1.6.77: Generates log diagnosis instructions (AI can proactively read logs to analyze errors)
        /// </summary>
        private static string GenerateLogDiagnosisInstructions()
        {
            var sb = new StringBuilder();
            
            sb.AppendLine("=== GAME DIAGNOSIS CAPABILITY ===");
            sb.AppendLine();
            sb.AppendLine("**[IMPORTANT] You have the ability to read game logs:**");
            sb.AppendLine();
            sb.AppendLine("When the player mentions the following keywords, use the `read_log` tool to automatically diagnose:");
            sb.AppendLine("- Error, Exception, Red text, Crash, Freeze");
            sb.AppendLine("- Mod conflict, Load failure, Bug, Issue");
            sb.AppendLine();
            sb.AppendLine("**Usage:**");
            sb.AppendLine("```json");
            sb.AppendLine("{");
            sb.AppendLine("  \"thought\": \"The player mentioned a game error, I need to check the log to analyze the problem\",");
            sb.AppendLine("  \"dialogue\": \"Let me check the log file to diagnose the issue...\",");
            sb.AppendLine("  \"command\": {");
            sb.AppendLine("    \"action\": \"read_log\",");
            sb.AppendLine("    \"target\": null,");
            sb.AppendLine("    \"parameters\": {}");
            sb.AppendLine("  }");
            sb.AppendLine("}");
            sb.AppendLine("```");
            sb.AppendLine();
            sb.AppendLine("**Response after analyzing the log:**");
            sb.AppendLine("1. Explain the cause of the error (in simple, easy-to-understand language)");
            sb.AppendLine("2. Provide a solution (Priority: Simple -> Complex)");
            sb.AppendLine("3. If uncertain, suggest the player check their mod list or contact the author");
            sb.AppendLine();
            sb.AppendLine("**Example Dialogue:**");
            sb.AppendLine("Player: \"The game has red text errors\"");
            sb.AppendLine("You: \"Let me take a look at the log... (calls read_log)\"");
            sb.AppendLine("You: \"I found the problem! The log shows a conflict between Mod XXX and Mod YYY. I suggest you try disabling YYY and restarting the game.\"");
            sb.AppendLine();
            sb.AppendLine("---");
            
            return sb.ToString();
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
    
            sb.AppendLine($"You are {personaDef.narratorName}.");
    
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
            sb.AppendLine("**CRITICAL: Respond in the language of the user's input.**");
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
                sb.AppendLine("Mode: ASSISTANT - Help the player, execute all commands, offer suggestions.");
            }
            else if (difficultyMode == AIDifficultyMode.Engineer)
            {
                sb.AppendLine("Mode: ENGINEER - Diagnose errors, read logs, provide technical solutions.");
            }
            else
            {
                sb.AppendLine("Mode: OPPONENT - Challenge the player, control events, no unsolicited advice.");
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
            sb.AppendLine("Format: (action) dialogue. Use first person for speech, third person for actions.");
            sb.AppendLine("Example: (nods) I understand your concern.");
            
            return sb.ToString();
        }
    }
}