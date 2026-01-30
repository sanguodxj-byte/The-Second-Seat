using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;
using TheSecondSeat.PersonaGeneration;
using TheSecondSeat.PersonaGeneration.Scriban;
using TheSecondSeat.PersonaGeneration.PromptSections;
using TheSecondSeat.Storyteller;
using TheSecondSeat.Comps;

namespace TheSecondSeat.SmartPrompt
{
    /// <summary>
    /// ⭐ SmartPrompt 集成层
    /// 
    /// 连接 "最后一公里" - 将 FlashMatcher 的意图识别结果
    /// 集成到 SystemPromptGenerator 的输出中
    /// 
    /// 工作流程:
    /// 1. 接收用户输入
    /// 2. 通过 FlashMatcher 识别意图
    /// 3. 通过 IntentRouter 确定需要的模块
    /// 4. 将模块内容注入到 PromptContext.Snippets
    /// 5. 输出增强后的 System Prompt
    /// </summary>
    public static class SmartPromptIntegration
    {
        // ============================================
        // 核心 API
        // ============================================
        
        /// <summary>
        /// ⭐ v2.4.0: 生成意图感知的 System Prompt
        /// 使用"Scriban 容器 + SmartPrompt 内容"模式
        ///
        /// 核心流程：
        /// 1. SmartPrompt 生成原始模块内容（带 {{...}} 占位符）
        /// 2. 将内容注入 context.Meta.DynamicModules
        /// 3. 由 Master 模板统一渲染
        ///
        /// 这解决了三个冲突：
        /// - 架构冲突：Master 模板是"容器"，SmartPrompt 提供"内容"
        /// - 数据流冲突：先组装，后渲染
        /// - 资源加载冲突：所有内容都通过 PromptLoader 加载
        /// </summary>
        public static string GenerateIntentAwarePrompt(
            string userInput,
            NarratorPersonaDef personaDef,
            PersonaAnalysisResult analysis,
            StorytellerAgent agent,
            AIDifficultyMode difficultyMode = AIDifficultyMode.Assistant)
        {
            try
            {
                // 1. 构建基础上下文
                var context = BuildEnhancedContext(userInput, personaDef, agent, difficultyMode, null);
                
                // 2. ⭐ v3.1.1: 检测是否需要工具列表
                bool needsToolBox = FlashMatcher.Instance.NeedsToolBox(userInput);
                
                if (Prefs.DevMode)
                {
                    Log.Message($"[SmartPromptIntegration] NeedsToolBox: {needsToolBox} for input: '{userInput?.Substring(0, Math.Min(50, userInput?.Length ?? 0))}...'");
                }
                
                // 3. 使用 SmartPrompt 生成原始模块内容（未渲染）
                string dynamicModules = SmartPrompt.GenerateRawModules(userInput, context);
                
                // 4. 注入到上下文中
                if (!string.IsNullOrEmpty(dynamicModules))
                {
                    context.Meta.DynamicModules = dynamicModules;
                    
                    if (Prefs.DevMode)
                    {
                        Log.Message($"[SmartPromptIntegration] Injected {dynamicModules.Length} chars of dynamic modules");
                    }
                }
                
                // 5. 准备 Snippets (保留旧生成逻辑) - 传递 needsToolBox 参数
                PrepareSnippets(context, personaDef, analysis, agent, difficultyMode, needsToolBox);
                
                // 6. 由 Master 模板统一渲染
                return PromptRenderer.Render("SystemPrompt_Master_Scriban", context);
            }
            catch (Exception ex)
            {
                Log.Error($"[SmartPromptIntegration] Failed: {ex.Message}");
                // 回退到标准生成
                return SystemPromptGenerator.GenerateSystemPrompt(
                    personaDef, analysis, agent, difficultyMode);
            }
        }
        
        /// <summary>
        /// 准备 Snippets（兼容旧逻辑）
        /// ⭐ v3.1.1: 支持按需加载工具列表
        /// </summary>
        private static void PrepareSnippets(
            PromptContext context,
            NarratorPersonaDef personaDef,
            PersonaAnalysisResult analysis,
            StorytellerAgent agent,
            AIDifficultyMode difficultyMode,
            bool includeToolBox = true)
        {
            // Identity
            context.Snippets["identity_section"] = IdentitySection.Generate(personaDef, agent, difficultyMode);
            
            // Personality
            context.Snippets["personality_section"] = PersonalitySection.Generate(analysis, personaDef);
            
            // Philosophy (难度模式哲学)
            string philosophyFile = $"Philosophy_{difficultyMode}";
            string philosophy = PromptLoader.Load(philosophyFile);
            if (string.IsNullOrEmpty(philosophy) || philosophy.StartsWith("[Error:"))
            {
                string behaviorFile = $"BehaviorRules_{difficultyMode}";
                philosophy = PromptLoader.Load(behaviorFile);
            }
            if (philosophy.StartsWith("[Error:")) philosophy = "";
            context.Snippets["philosophy"] = philosophy;
            
            // ⭐ v3.1.1: ToolBox (输出格式) - 根据需求决定是否包含命令列表
            context.Snippets["tool_box_section"] = OutputFormatSection.Generate(difficultyMode, includeToolBox);
            
            // Romantic Instructions
            string romanticInstructions = "";
            if (difficultyMode == AIDifficultyMode.Assistant)
            {
                romanticInstructions = RomanticInstructionsSection.Generate(personaDef, agent);
            }
            context.Snippets["romantic_instructions"] = romanticInstructions;
            
            // Log Diagnosis
            context.Snippets["log_diagnosis"] = PromptLoader.Load("LogDiagnosis");
        }
        
        /// <summary>
        /// ⭐ 构建增强的 PromptContext（包含意图信息和记忆）
        /// </summary>
        public static PromptContext BuildEnhancedContext(
            string userInput,
            NarratorPersonaDef personaDef,
            StorytellerAgent agent,
            AIDifficultyMode difficultyMode,
            CompNarratorMemory memory = null)
        {
            // ⭐ v3.1.1: 确保 Card 永不为 null，防止 Scriban 渲染报错
            var currentCard = TheSecondSeat.CharacterCard.CharacterCardSystem.GetCurrentCard();
            if (currentCard == null)
            {
                currentCard = new TheSecondSeat.CharacterCard.NarratorStateCard();
            }
            
            var context = new PromptContext
            {
                Card = currentCard,
                Narrator = new NarratorInfo
                {
                    DefName = personaDef.defName,
                    Name = personaDef.narratorName,
                    Label = personaDef.label,
                    Biography = personaDef.biography,
                    VisualTags = personaDef.visualElements,
                    DescentAnimation = personaDef.descentAnimationType,
                    PersonalityTags = personaDef.personalityTags,
                    ToneTags = personaDef.toneTags,
                    ForbiddenWords = personaDef.forbiddenWords,
                    SpecialAbilities = personaDef.specialAbilities,
                    MercyLevel = personaDef.mercyLevel,
                    ChaosLevel = personaDef.narratorChaosLevel,
                    DominanceLevel = personaDef.dominanceLevel
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
            
            // 注入意图识别结果
            if (!string.IsNullOrEmpty(userInput))
            {
                var matchedIntents = FlashMatcher.Instance.GetMatchedIntents(userInput);
                context.Snippets["detected_intents"] = string.Join(", ", matchedIntents);
                context.Snippets["intent_count"] = matchedIntents.Count.ToString();
                
                // 注入动态技能模块
                var routeResult = IntentRouter.Instance.Route(userInput);
                if (routeResult.Success)
                {
                    var skillModules = routeResult.Modules
                        .Where(m => m.moduleType == ModuleType.Skill)
                        .ToList();
                    
                    if (skillModules.Any())
                    {
                        var sb = new StringBuilder();
                        sb.AppendLine("## 当前相关技能");
                        foreach (var module in skillModules)
                        {
                            sb.AppendLine($"### {module.label ?? module.defName}");
                            sb.AppendLine(module.GetContent());
                            sb.AppendLine();
                        }
                        context.Snippets["skill_modules"] = sb.ToString();
                    }
                }
            }
            
            // 注入记忆信息
            if (memory != null)
            {
                var memoryWrapper = memory.GetScribanWrapper();
                
                // 待完成的承诺
                var pendingPromises = memoryWrapper.PendingPromises;
                if (pendingPromises.Any())
                {
                    var promisesSb = new StringBuilder();
                    promisesSb.AppendLine("## 待完成的承诺");
                    foreach (var promise in pendingPromises)
                    {
                        promisesSb.AppendLine($"- {promise.description}");
                        if (promise.IsOverdue)
                        {
                            promisesSb.AppendLine("  ⚠️ 已过期！");
                        }
                    }
                    context.Snippets["pending_promises"] = promisesSb.ToString();
                }
                
                // 重要记忆
                var importantEvents = memoryWrapper.ImportantEvents;
                if (importantEvents.Any())
                {
                    var eventsSb = new StringBuilder();
                    eventsSb.AppendLine("## 重要事件回顾");
                    foreach (var evt in importantEvents)
                    {
                        eventsSb.AppendLine($"- [Day {evt.dayRecorded}] {evt.description}");
                    }
                    context.Snippets["important_events"] = eventsSb.ToString();
                }
                
                // 好感度趋势
                float trend = memoryWrapper.AffinityTrend;
                if (Math.Abs(trend) > 0.1f)
                {
                    string trendDesc = trend > 0 ? "上升中" : "下降中";
                    context.Snippets["affinity_trend"] = $"近期好感度趋势: {trendDesc} ({trend:+0.0;-0.0})";
                }
                
                // 注入自定义记忆 KV
                foreach (var kv in memoryWrapper.All)
                {
                    context.Snippets[$"memory_{kv.Key}"] = kv.Value;
                }
            }
            
            return context;
        }
        
        // ============================================
        // 辅助方法
        // ============================================
        
        /// <summary>
        /// 将 SmartPrompt 模块注入到基础 Prompt 中
        /// </summary>
        private static string InjectSmartModules(string basePrompt, BuildResult smartResult)
        {
            if (string.IsNullOrEmpty(smartResult.Prompt))
            {
                return basePrompt;
            }
            
            var sb = new StringBuilder();
            
            // 基础 Prompt
            sb.Append(basePrompt);
            
            // 分隔符
            sb.AppendLine();
            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine();
            
            // 意图识别结果（调试信息）
            if (Prefs.DevMode && smartResult.RouteResult?.SelectedIntents?.Any() == true)
            {
                sb.AppendLine($"<!-- Detected Intents: {string.Join(", ", smartResult.RouteResult.SelectedIntents)} -->");
            }
            
            // 动态模块内容
            sb.AppendLine("## 当前任务相关知识");
            sb.AppendLine();
            sb.Append(smartResult.Prompt);
            
            return sb.ToString();
        }
        
        /// <summary>
        /// 获取语言指令
        /// </summary>
        private static string GetLanguageInstruction(string personaName = null)
        {
            try
            {
                // PromptLoader.Load 只接受文件名参数
                string content = PromptLoader.Load("Language_Instruction");
                if (!string.IsNullOrEmpty(content) && !content.StartsWith("[Error:"))
                {
                    return content;
                }
            }
            catch { }
            
            bool isChinese = LanguageDatabase.activeLanguage?.folderName?.Contains("Chinese") ?? false;
            return isChinese
                ? "语言要求：请使用简体中文回复。"
                : "LANGUAGE REQUIREMENT: Respond in English.";
        }
        
        // ============================================
        // 快捷方法
        // ============================================
        
        /// <summary>
        /// 分析用户输入的意图
        /// </summary>
        public static List<string> AnalyzeIntents(string userInput)
        {
            return FlashMatcher.Instance.GetMatchedIntents(userInput);
        }
        
        /// <summary>
        /// 检查用户输入是否包含特定意图
        /// </summary>
        public static bool HasIntent(string userInput, string intent)
        {
            return FlashMatcher.Instance.HasIntent(userInput, intent);
        }
        
        /// <summary>
        /// 获取匹配的模块列表
        /// </summary>
        public static List<string> GetMatchedModules(string userInput)
        {
            return FlashMatcher.Instance.GetMatchedModules(userInput);
        }
        
        /// <summary>
        /// 获取系统统计信息
        /// </summary>
        public static string GetStats()
        {
            return SmartPrompt.GetStats();
        }
        
        /// <summary>
        /// ⭐ v3.1.1: 检查用户输入是否需要工具列表
        /// </summary>
        public static bool NeedsToolBox(string userInput)
        {
            return FlashMatcher.Instance.NeedsToolBox(userInput);
        }
    }
    
    /// <summary>
    /// ⭐ SmartPrompt 初始化扩展
    /// 确保在游戏加载时初始化 SmartPrompt 系统
    /// </summary>
    [StaticConstructorOnStartup]
    public static class SmartPromptSystemInitializer
    {
        static SmartPromptSystemInitializer()
        {
            LongEventHandler.QueueLongEvent(Initialize, "TSS_InitializingSmartPrompt", false, null);
        }
        
        private static void Initialize()
        {
            try
            {
                // 初始化 SmartPrompt 系统（包括 FlashMatcher）
                SmartPrompt.Initialize();
                
                Log.Message("[TSS] SmartPrompt system initialized successfully");
            }
            catch (Exception ex)
            {
                Log.Error($"[TSS] SmartPrompt initialization failed: {ex.Message}");
            }
        }
    }
}