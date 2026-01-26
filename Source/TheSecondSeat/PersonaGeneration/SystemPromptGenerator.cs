using System;
using System.Text;
using System.Linq;
using TheSecondSeat.Storyteller;
using TheSecondSeat.PersonaGeneration.PromptSections;
using TheSecondSeat.PersonaGeneration.Scriban; // ⭐ v2.0.0
using TheSecondSeat.Descent; // ⭐ v2.1.0: 降临系统
using TheSecondSeat.Core; // ⭐ v2.1.0: NarratorBioRhythm
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
            if (!string.IsNullOrWhiteSpace(personaDef.customSystemPrompt))
            {
                return personaDef.customSystemPrompt;
            }

            // ⭐ v2.0.0: Scriban 重构
            // 构建上下文
            var context = new PromptContext
            {
                // ⭐ 注入角色卡数据 (v2.2.0+)
                Card = TheSecondSeat.CharacterCard.CharacterCardSystem.GetCurrentCard(),

                Narrator = new NarratorInfo
                {
                    DefName = personaDef.defName, // ⭐ v3.0: 传递 Persona DefName
                    Name = personaDef.narratorName,
                    Label = personaDef.label,
                    Biography = personaDef.biography,
                    VisualTags = personaDef.visualElements,
                    DescentAnimation = personaDef.descentAnimationType
                },
                Agent = new AgentInfo
                {
                    Affinity = agent.affinity,
                    Mood = agent.currentMood.ToString(),
                    DialogueStyle = new DialogueStyleInfo
                    {
                        Formality = agent.dialogueStyle.formalityLevel,
                        Emotional = agent.dialogueStyle.emotionalExpression,
                        Verbosity = agent.dialogueStyle.verbosity,
                        Humor = agent.dialogueStyle.humorLevel,
                        Sarcasm = agent.dialogueStyle.sarcasmLevel,
                        UseEmoticons = agent.dialogueStyle.useEmoticons,
                        UseEllipsis = agent.dialogueStyle.useEllipsis,
                        UseExclamation = agent.dialogueStyle.useExclamation
                    }
                },
                Meta = new MetaInfo
                {
                    DifficultyMode = difficultyMode.ToString(),
                    LanguageInstruction = GetLanguageInstruction(personaDef.defName)
                }
            };

            // 获取 Mod 设置
            var modSettings = LoadedModManager.GetMod<Settings.TheSecondSeatMod>()?.GetSettings<Settings.TheSecondSeatSettings>();
            if (modSettings != null && !string.IsNullOrWhiteSpace(modSettings.globalPrompt))
            {
                context.Meta.ModSettingsPrompt = modSettings.globalPrompt.Trim();
            }

            // 准备 Snippets (保留旧生成逻辑)
            
            // Identity
            context.Snippets["identity_section"] = IdentitySection.Generate(personaDef, agent, difficultyMode);

            // Personality
            context.Snippets["personality_section"] = PersonalitySection.Generate(analysis, personaDef);

            // Philosophy (难度模式哲学)
            string philosophyFile = $"Philosophy_{difficultyMode}";
            string philosophy = PromptLoader.Load(philosophyFile, personaDef.defName); // ⭐ v3.0: 支持 Persona 专属
            if (string.IsNullOrEmpty(philosophy) || philosophy.StartsWith("[Error:"))
            {
                string behaviorFile = $"BehaviorRules_{difficultyMode}";
                philosophy = PromptLoader.Load(behaviorFile, personaDef.defName); // ⭐ v3.0: 支持 Persona 专属
            }
            if (philosophy.StartsWith("[Error:")) philosophy = "";
            context.Snippets["philosophy"] = philosophy;

            // ToolBox (输出格式)
            context.Snippets["tool_box_section"] = OutputFormatSection.Generate(difficultyMode);

            // Romantic Instructions
            string romanticInstructions = "";
            if (difficultyMode == AIDifficultyMode.Assistant)
            {
                romanticInstructions = RomanticInstructionsSection.Generate(personaDef, agent);
            }
            context.Snippets["romantic_instructions"] = romanticInstructions;

            // Log Diagnosis
            context.Snippets["log_diagnosis"] = GenerateLogDiagnosisInstructions(personaDef.defName);

            // ⭐ v2.2.0: 降临状态和生物节律已集成到 CharacterCard (context.Card) 中
            // 不再需要手动构建 DescentInfo 或 BioContext

            // 渲染
            // 优先尝试加载 _Scriban 版本，如果不存在则回退到旧版逻辑 (暂不实现自动回退，因为我们提供了文件)
            return PromptRenderer.Render("SystemPrompt_Master_Scriban", context);
        }
        
        /// <summary>
        /// ⭐ v1.6.77: Generates log diagnosis instructions (AI can proactively read logs to analyze errors)
        /// </summary>
        private static string GenerateLogDiagnosisInstructions(string personaName = null)
        {
            var sb = new StringBuilder();
            sb.AppendLine(PromptLoader.Load("LogDiagnosis", personaName));
            sb.AppendLine();
            sb.AppendLine("---");
            return sb.ToString();
        }

        /// <summary>
        /// ⭐ 获取语言强制指令
        /// ⭐ v1.6.86: 添加容错处理
        /// </summary>
        private static string GetLanguageInstruction(string personaName = null)
        {
            try
            {
                string content = PromptLoader.Load("Language_Instruction", personaName);
                if (!string.IsNullOrEmpty(content)) return content;
            }
            catch (Exception ex)
            {
                Log.Warning($"[The Second Seat] Failed to load Language_Instruction: {ex.Message}");
            }
            
            // 默认回退（硬编码）
            bool isChinese = LanguageDatabase.activeLanguage?.folderName?.Contains("Chinese") ?? false;
            return isChinese
                ? "语言要求：请使用简体中文回复。"
                : "LANGUAGE REQUIREMENT: Respond in English.";
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
            string template = PromptLoader.Load("SystemPrompt_Compact", personaDef.defName);
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
            string philosophy = PromptLoader.Load(philosophyFile, personaDef.defName);
            if (string.IsNullOrEmpty(philosophy) || philosophy.StartsWith("[Error:"))
            {
                string behaviorFile = $"BehaviorRules_{difficultyMode}";
                philosophy = PromptLoader.Load(behaviorFile, personaDef.defName);
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
            string compactInstruction = PromptLoader.Load("Compact_Instruction", personaDef.defName);
            if (compactInstruction.StartsWith("[Error:")) compactInstruction = "";
            
            // 3. 替换占位符
            return template
                .Replace("{{Language_Instruction}}", GetLanguageInstruction(personaDef.defName))
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
            bool isChinese = LanguageDatabase.activeLanguage?.folderName?.Contains("Chinese") ?? false;
            
            sb.AppendLine(GetLanguageInstruction(personaDef.defName));
            sb.AppendLine();
            
            string displayName = !string.IsNullOrEmpty(personaDef.label)
                ? personaDef.label
                : personaDef.narratorName;
            
            sb.AppendLine(isChinese ? $"你是 **{displayName}**。" : $"You are **{displayName}**.");
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
                sb.AppendLine(isChinese
                    ? $"好感度: {agent.affinity:F0}/100"
                    : $"Affinity: {agent.affinity:F0}/100");
            }
            
            sb.AppendLine(isChinese ? "格式: (动作) 对话。" : "Format: (action) dialogue.");
            
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
            // ⭐ 获取角色卡 (已在 NarratorUpdateService 主线程中更新)
            var card = TheSecondSeat.CharacterCard.CharacterCardSystem.GetCurrentCard();

            // ⭐ v2.0.0: Scriban 重构
            // 构建上下文
            var context = new PromptContext
            {
                // ⭐ 注入新的卡片数据
                Card = card,

                Narrator = new NarratorInfo
                {
                    DefName = personaDef.defName, // ⭐ v3.0: 传递 Persona DefName
                    Name = personaDef.narratorName,
                    Label = personaDef.label,
                    Biography = personaDef.biography,
                    VisualTags = personaDef.visualElements,
                    DescentAnimation = personaDef.descentAnimationType
                },
                Agent = new AgentInfo
                {
                    Affinity = agent.affinity,
                    Mood = agent.currentMood.ToString(),
                    DialogueStyle = new DialogueStyleInfo
                    {
                        Formality = agent.dialogueStyle.formalityLevel,
                        Emotional = agent.dialogueStyle.emotionalExpression,
                        Verbosity = agent.dialogueStyle.verbosity,
                        Humor = agent.dialogueStyle.humorLevel,
                        Sarcasm = agent.dialogueStyle.sarcasmLevel,
                        UseEmoticons = agent.dialogueStyle.useEmoticons,
                        UseEllipsis = agent.dialogueStyle.useEllipsis,
                        UseExclamation = agent.dialogueStyle.useExclamation
                    }
                },
                Meta = new MetaInfo
                {
                    DifficultyMode = difficultyMode.ToString(),
                    LanguageInstruction = GetLanguageInstruction(personaDef.defName)
                }
            };

            // 准备 Section (兼容性)
            string identitySection = IdentitySection.Generate(personaDef, agent, AIDifficultyMode.Assistant);
            string personalitySection = PersonalitySection.Generate(analysis, personaDef);
            
            context.Snippets["identity_section"] = identitySection;
            context.Snippets["personality_section"] = personalitySection;

            // 渲染
            // 优先尝试加载 _Scriban 版本，如果不存在则回退到旧版逻辑 (暂不实现自动回退，因为我们提供了文件)
            return PromptRenderer.Render("SystemPrompt_EventDirector_Scriban", context);
        }
        
    }
}
