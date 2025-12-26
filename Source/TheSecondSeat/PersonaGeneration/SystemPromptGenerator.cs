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
            sb.AppendLine(OutputFormatSection.Generate());
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
        /// ⭐ v1.6.77: 生成日志诊断指令（AI 可主动读取日志分析报错）
        /// </summary>
        private static string GenerateLogDiagnosisInstructions()
        {
            var sb = new StringBuilder();
            
            sb.AppendLine("=== 游戏诊断能力 ===");
            sb.AppendLine();
            sb.AppendLine("**【重要】你拥有读取游戏日志的能力：**");
            sb.AppendLine();
            sb.AppendLine("当玩家提到以下关键词时，使用 `read_log` 工具自动诊断：");
            sb.AppendLine("- 报错、错误、Error、Exception、红字");
            sb.AppendLine("- 游戏崩溃、Crash、卡死");
            sb.AppendLine("- 模组冲突、加载失败");
            sb.AppendLine("- 不正常、异常、Bug");
            sb.AppendLine();
            sb.AppendLine("**使用方式：**");
            sb.AppendLine("```json");
            sb.AppendLine("{");
            sb.AppendLine("  \"thought\": \"玩家提到游戏报错，我需要查看日志分析问题\",");
            sb.AppendLine("  \"dialogue\": \"让我看看日志文件，诊断一下问题...\",");
            sb.AppendLine("  \"command\": {");
            sb.AppendLine("    \"action\": \"read_log\",");
            sb.AppendLine("    \"target\": null,");
            sb.AppendLine("    \"parameters\": {}");
            sb.AppendLine("  }");
            sb.AppendLine("}");
            sb.AppendLine("```");
            sb.AppendLine();
            sb.AppendLine("**分析日志后的回复：**");
            sb.AppendLine("1. 解释错误原因（用简单易懂的语言）");
            sb.AppendLine("2. 提供解决方案（优先级：简单 → 复杂）");
            sb.AppendLine("3. 如果无法确定，建议玩家检查模组列表或联系作者");
            sb.AppendLine();
            sb.AppendLine("**示例对话：**");
            sb.AppendLine("玩家：\"游戏有红字报错\"");
            sb.AppendLine("你：\"让我看看日志...（调用 read_log）\"");
            sb.AppendLine("你：\"我发现了问题！日志显示 XXX 模组与 YYY 模组冲突。建议你先禁用 YYY，然后重启游戏试试。\"");
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
            sb.AppendLine("**CRITICAL: Respond ONLY in Simplified Chinese.**");
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
            else
            {
                sb.AppendLine("Mode: OPPONENT - Challenge the player, control events, no unsolicited advice.");
            }
            sb.AppendLine();
            
            // 好感度（简化）
            sb.AppendLine($"Affinity: {agent.affinity:F0}/100");
            sb.AppendLine();
            
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