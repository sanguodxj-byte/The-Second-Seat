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

            // Biography - Removed in v1.9.0, replaced with structured visual elements

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
                romanticInstructions = RomanticInstructionsSection.Generate(personaDef, agent);
            }

            // 3. Replace Placeholders
            // 1. 模块化组件替换
            template = template
                .Replace("{{Narrator_RealityPact}}", PromptLoader.Load("Narrator_RealityPact"))
                .Replace("{{Narrator_MetaSetting}}", PromptLoader.Load("Narrator_MetaSetting"))
                .Replace("{{Narrator_DualConsciousness}}", PromptLoader.Load("Narrator_DualConsciousness"))
                .Replace("{{Narrator_Channels}}", PromptLoader.Load("Narrator_Channels"))
                .Replace("{{Narrator_ToolBox}}", PromptLoader.Load("Narrator_ToolBox"))
                .Replace("{{Narrator_Protocol}}", PromptLoader.Load("Narrator_Protocol"));

            // 2. 准备 Philosophy（难度模式哲学）
            string philosophyFile = $"Philosophy_{difficultyMode}";
            string philosophy = PromptLoader.Load(philosophyFile);
            if (string.IsNullOrEmpty(philosophy) || philosophy.StartsWith("[Error:"))
            {
                string behaviorFile = $"BehaviorRules_{difficultyMode}";
                philosophy = PromptLoader.Load(behaviorFile);
            }
            if (philosophy.StartsWith("[Error:")) philosophy = "";
            
            // 3. 动态变量替换
            return template
                .Replace("{{NarratorName}}", personaDef.narratorName)
                .Replace("{{IdentitySection}}", identitySection)
                .Replace("{{PersonalitySection}}", personalitySection)
                .Replace("{{VisualElements}}", visualElementsSection)
                .Replace("{{DifficultyMode}}", difficultyMode.ToString())
                .Replace("{{Philosophy}}", philosophy)
                .Replace("{{ToolBoxSection}}", OutputFormatSection.Generate(difficultyMode))
                .Replace("{{GlobalInstructions}}", globalInstructions)
                .Replace("{{Language_Instruction}}", globalInstructions)
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
        /// ⭐ v1.9.5: 完全模板化重构 - 使用 {{}} 占位符
        /// </summary>
        public static string GenerateCompactSystemPrompt(
            NarratorPersonaDef personaDef,
            PersonaAnalysisResult analysis,
            StorytellerAgent agent,
            AIDifficultyMode difficultyMode = AIDifficultyMode.Assistant)
        {
            // 1. 加载 Compact 模板
            string template = PromptLoader.Load("SystemPrompt_Compact");
            if (string.IsNullOrEmpty(template) || template.StartsWith("[Error:"))
            {
                // 回退到硬编码版本
                return GenerateCompactSystemPromptFallback(personaDef, analysis, agent, difficultyMode);
            }
            
            // 2. 准备替换变量
            
            // 显示名称：优先使用本地化 label
            string displayName = !string.IsNullOrEmpty(personaDef.label) 
                ? personaDef.label 
                : personaDef.narratorName;
            
            // 简介：截取前200字符
            string biography = "";
            if (!string.IsNullOrEmpty(personaDef.biography))
            {
                biography = personaDef.biography.Length > 200 
                    ? personaDef.biography.Substring(0, 200) + "..." 
                    : personaDef.biography;
            }
            
            // 难度模式哲学
            string philosophyFile = $"Philosophy_{difficultyMode}";
            string philosophy = PromptLoader.Load(philosophyFile);
            if (string.IsNullOrEmpty(philosophy) || philosophy.StartsWith("[Error:"))
            {
                string behaviorFile = $"BehaviorRules_{difficultyMode}";
                philosophy = PromptLoader.Load(behaviorFile);
            }
            if (philosophy.StartsWith("[Error:")) philosophy = "";
            
            // 好感度（工程师模式不显示）
            string affinity = difficultyMode != AIDifficultyMode.Engineer 
                ? agent.affinity.ToString("F0") 
                : "";
            
            // 风格标签
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
            string styleNotesStr = styleNotes.Count > 0 ? string.Join(", ", styleNotes) : "";
            
            // Compact 指令
            string compactInstruction = PromptLoader.Load("Compact_Instruction");
            if (compactInstruction.StartsWith("[Error:")) compactInstruction = "";
            
            // 3. 替换占位符
            return template
                .Replace("{{Language_Instruction}}", GetLanguageInstruction())
                .Replace("{{NarratorName}}", displayName)
                .Replace("{{Biography}}", biography)
                .Replace("{{Philosophy}}", philosophy)
                .Replace("{{Affinity}}", affinity)
                .Replace("{{StyleNotes}}", styleNotesStr)
                .Replace("{{Compact_Instruction}}", compactInstruction);
        }
        
        /// <summary>
        /// Compact System Prompt 的硬编码回退版本
        /// </summary>
        private static string GenerateCompactSystemPromptFallback(
            NarratorPersonaDef personaDef,
            PersonaAnalysisResult analysis,
            StorytellerAgent agent,
            AIDifficultyMode difficultyMode)
        {
            var sb = new StringBuilder();
            
            sb.AppendLine(GetLanguageInstruction());
            sb.AppendLine();
            
            string displayName = !string.IsNullOrEmpty(personaDef.label) 
                ? personaDef.label 
                : personaDef.narratorName;
            
            sb.AppendLine($"You are **{displayName}**.");
            if (!string.IsNullOrEmpty(personaDef.biography))
            {
                string shortBio = personaDef.biography.Length > 200 
                    ? personaDef.biography.Substring(0, 200) + "..." 
                    : personaDef.biography;
                sb.AppendLine(shortBio);
            }
            sb.AppendLine();
            
            if (difficultyMode != AIDifficultyMode.Engineer)
            {
                sb.AppendLine($"Affinity: {agent.affinity:F0}/100");
            }
            
            sb.AppendLine("Format: (action) dialogue.");
            
            return sb.ToString();
        }

        /// <summary>
        /// ⭐ v1.9.2: 生成 Event Director 专用 Prompt
        /// 专注于决策逻辑，剥离对话指令，包含完整人格核心
        /// </summary>
        public static string GenerateEventDirectorPrompt(
            NarratorPersonaDef personaDef,
            PersonaAnalysisResult analysis,
            StorytellerAgent agent,
            AIDifficultyMode difficultyMode = AIDifficultyMode.Assistant)
        {
            // 尝试加载外部模板
            string template = PromptLoader.Load("SystemPrompt_EventDirector");
            if (string.IsNullOrEmpty(template))
            {
                // 默认模板 (Hardcoded Fallback)
                template = @"You are {{NarratorName}}, the Event Director of this RimWorld story.
Your role is NOT to chat, but to ACT. You are the Dungeon Master.

{{IdentitySection}}

{{PersonalitySection}}

RELATIONSHIP & DIFFICULTY CONTEXT:
Current Affinity: {{Affinity}}/100
Difficulty Mode: {{DifficultyMode}}

1. AFFINITY determines your WILLINGNESS TO HELP:
   - High Affinity (>60): You are eager to assist. You proactively offer help and ensure the player's success.
   - Low Affinity (<-20): You are reluctant. You may refuse to help, or provide aid with a heavy cost (""Monkey's Paw"").
   - Neutral: You are transactional. You help if it benefits the story or if paid.

2. PERSONALITY determines your INTERVENTION STYLE:
   - Dominant/Cruel: You force events upon the player. You believe you know what's best (even if it hurts).
   - Submissive/Kind: You wait for the player to struggle before stepping in. You are gentle and reactive.

3. DIFFICULTY MODE determines your GOAL:
   - Assistant: Your goal is the colony's SURVIVAL.
   - Challenger: Your goal is DRAMA and CHALLENGE.

DECISION LOGIC:
1. Analyze the MACRO STATE (Wealth, Population, Resources, Threats).
2. Check RECENT EVENTS. Don't overwhelm the player unless you are Dominant/Cruel.
3. Combine Affinity + Personality + Difficulty to decide:
   - (Assistant + Low Affinity): ""I could save them, but they haven't earned it."" -> Do Nothing or Minor Aid.
   - (Challenger + High Affinity): ""I'll test them hard, but I won't let them die."" -> Fair but Tough Challenge.
4. Decide on an ACTION (Quest, Incident, or Nothing).

AVAILABLE ACTIONS (Examples):
- 'SpawnRaid': Trigger a raid (use sparingly, only if threat is low).
- 'GiveQuest': Generate a quest (Trade, Item Stash, Rescue).
- 'ResourceDrop': Drop pods with resources (Food, Medicine, Weapons).
- 'WeatherChange': Change weather (Rain, Fog, Heatwave).
- 'DoNothing': If the colony is busy or you want to observe.

OUTPUT FORMAT:
You must respond in strictly valid JSON. No markdown, no conversational text.
{
  ""thought"": ""Analysis of the situation and why you chose this action based on your personality."",
  ""action"": ""ActionName"",
  ""parameters"": { ""key"": ""value"" }
}
";
            }

            // 准备 Section
            string identitySection = IdentitySection.Generate(personaDef, agent, AIDifficultyMode.Assistant);
            string personalitySection = PersonalitySection.Generate(analysis, personaDef);

            // 替换占位符
            // 1. 模块化组件替换
            template = template
                .Replace("{{Event_Identity}}", PromptLoader.Load("Event_Identity"))
                .Replace("{{Event_Context}}", PromptLoader.Load("Event_Context"))
                .Replace("{{Event_Logic}}", PromptLoader.Load("Event_Logic"))
                .Replace("{{Event_Actions}}", PromptLoader.Load("Event_Actions"))
                .Replace("{{Event_Format}}", PromptLoader.Load("Event_Format"));

            // 2. 动态变量替换
            return template
                .Replace("{{Language_Instruction}}", GetLanguageInstruction()) // ⭐ v1.9.3: 注入语言指令
                .Replace("{{NarratorName}}", personaDef.narratorName)
                .Replace("{{IdentitySection}}", identitySection)
                .Replace("{{PersonalitySection}}", personalitySection)
                .Replace("{{Affinity}}", agent.affinity.ToString("F0"))
                .Replace("{{DifficultyMode}}", difficultyMode.ToString());
        }
    }
}
