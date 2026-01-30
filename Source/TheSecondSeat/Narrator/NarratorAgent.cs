using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TheSecondSeat.Commands;
using TheSecondSeat.Core;
using TheSecondSeat.Core.Components;
using TheSecondSeat.Execution;
using TheSecondSeat.Integration;
using TheSecondSeat.LLM;
using TheSecondSeat.Monitoring;
using TheSecondSeat.NaturalLanguage;
using TheSecondSeat.PersonaGeneration;
using TheSecondSeat.RimAgent;
using TheSecondSeat.RimAgent.Tools;
using TheSecondSeat.SmartPrompt;
using TheSecondSeat.UI;
using TheSecondSeat.WebSearch;
using UnityEngine;
using Verse;
using RimWorld;

namespace TheSecondSeat.Narrator
{
    /// <summary>
    /// ⭐ v3.0.0: 叙事者 Agent - 核心 AI 对话处理器
    /// 
    /// 职责：
    /// - 捕获游戏状态和上下文
    /// - 构建和管理 System Prompt
    /// - 与 LLM API 通信（通过 RimAgent 核心）
    /// - 处理响应并执行命令
    /// 
    /// 架构：
    /// NarratorController (GameComponent) 
    ///   └── NarratorAgent (本类, 核心服务)
    ///         └── RimAgent.RimAgent (底层 ReAct 循环)
    ///               └── LLMServiceProvider → LLM API
    /// </summary>
    public class NarratorAgent
    {
        private readonly NarratorManager? narratorManager;
        private readonly NarratorExpressionController expressionController;
        private readonly NarratorTTSHandler ttsHandler;
        
        private bool isProcessing = false;
        private string lastDialogue = "";
        private string lastError = "";
        
        // ⭐ v3.0.0: 底层 RimAgent 实例（ReAct 循环核心）
        private RimAgent.RimAgent coreAgent;
        private LLMServiceProvider llmProvider;

        public bool IsProcessing => isProcessing;
        public string LastDialogue => lastDialogue;
        public string LastError => lastError;
        
        /// <summary>
        /// 获取底层 RimAgent 核心（用于调试窗口）
        /// </summary>
        public RimAgent.RimAgent CoreAgent => coreAgent;

        public NarratorAgent(NarratorManager? manager, NarratorExpressionController exprCtrl, NarratorTTSHandler tts)
        {
            narratorManager = manager;
            expressionController = exprCtrl;
            ttsHandler = tts;
            
            llmProvider = new LLMServiceProvider
            {
                RequestType = "Narrator"
            };
        }

        /// <summary>
        /// ⭐ v3.0.0: 初始化底层 Agent（需要在人格加载后调用）
        /// 配置：
        /// - MaxIterations = 3（限制 ReAct 循环次数）
        /// - MaxHistorySize = 5（限制对话历史，减少 Token）
        /// - 注册完整工具集（Pull 模式按需获取状态）
        /// - 集成 SmartPrompt 系统（动态加载相关模块）
        /// </summary>
        public void Initialize()
        {
            if (coreAgent != null)
            {
                coreAgent.Dispose();
            }
            
            string systemPrompt = narratorManager?.GetDynamicSystemPrompt() ?? GetDefaultSystemPrompt();
            string agentId = $"Narrator_{narratorManager?.GetCurrentPersona()?.defName ?? "Default"}";
            
            coreAgent = new RimAgent.RimAgent(agentId, systemPrompt, llmProvider);
            coreAgent.MaxIterations = 3;
            coreAgent.MaxHistorySize = 5;
            
            RegisterTools();
            
            Log.Message($"[NarratorAgent] 初始化完成: {agentId} (MaxIterations=3, Tools={coreAgent.GetDebugInfo()})");
        }
        
        /// <summary>
        /// ⭐ v3.0.0: 注册叙事者所需的完整工具集
        /// </summary>
        private void RegisterTools()
        {
            // 核心工具：游戏状态查询（Pull 模式）
            coreAgent.RegisterTool(new GameStateTool());
            
            // 空间查询工具
            TryRegisterTool(new SpatialQueryTool(), "SpatialQueryTool");
            
            // 任务发布工具
            TryRegisterTool(new QuestIssueTool(), "QuestIssueTool");
            
            // 文件修补工具
            TryRegisterTool(new FilePatcherTool(), "FilePatcherTool");
            
            // 全局工具库
            var globalTools = new[] { "analyze_last_error", "search_items", "execute_command" };
            foreach (var toolName in globalTools)
            {
                try { coreAgent.RegisterTool(toolName); }
                catch (Exception ex) { Log.Warning($"[NarratorAgent] 注册全局工具 {toolName} 失败: {ex.Message}"); }
            }
        }
        
        private void TryRegisterTool(ITool tool, string name)
        {
            try
            {
                coreAgent.RegisterTool(tool);
                Log.Message($"[NarratorAgent] 已注册 {name}");
            }
            catch (Exception ex)
            {
                Log.Warning($"[NarratorAgent] 注册 {name} 失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 触发叙事者更新
        /// </summary>
        public void TriggerUpdate(string userMessage = "", bool hasGreetedOnLoad = true)
        {
            if (isProcessing)
            {
                Messages.Message("TSS_AlreadyProcessing".Translate(), MessageTypeDefOf.RejectInput);
                return;
            }

            // 在主线程捕获游戏状态
            GameStateSnapshot snapshot;
            string selectionContext = "";
            
            try
            {
                snapshot = GameStateSnapshotUtility.CaptureSnapshotSafe();
                selectionContext = CaptureSelectionContext();
            }
            catch (Exception ex)
            {
                Log.Error($"[NarratorAgent] 捕获游戏状态失败: {ex.Message}");
                Messages.Message("Failed to capture game state", MessageTypeDefOf.RejectInput);
                return;
            }

            bool isGreeting = string.IsNullOrEmpty(userMessage) && !hasGreetedOnLoad;
            
            // 设置思考表情
            SetThinkingExpression();
            
            // 更新角色卡
            UpdateCharacterCard();

            // 获取影子实体
            Pawn shadowPawn = GetShadowPawn();

            // 异步处理
            StartUpdateAsync(userMessage, snapshot, selectionContext, isGreeting, shadowPawn);
        }
        
        private string CaptureSelectionContext()
        {
            if (Find.Selector == null || Find.Selector.SelectedObjects.Count == 0)
                return "";
                
            var selectedInfo = new List<string>();
            foreach (var obj in Find.Selector.SelectedObjects)
            {
                if (obj is Thing t)
                    selectedInfo.Add($"{t.Label} (def: {t.def.defName}) @ {t.Position}");
                else if (obj is Zone z)
                    selectedInfo.Add($"区域: {z.label} (Cells: {z.Cells.Count})");
            }
            
            if (selectedInfo.Count > 0)
            {
                Log.Message($"[NarratorAgent] 捕获选中物体: {selectedInfo.Count} 个");
                return $"[玩家当前选中:\n{string.Join("\n", selectedInfo)}\n]\n";
            }
            return "";
        }
        
        private void SetThinkingExpression()
        {
            try
            {
                var persona = narratorManager?.GetCurrentPersona();
                if (persona != null)
                    ExpressionSystem.SetThinkingExpression(persona.defName);
            }
            catch (Exception ex)
            {
                Log.Warning($"[NarratorAgent] 设置思考表情失败: {ex.Message}");
            }
        }
        
        private void UpdateCharacterCard()
        {
            try
            {
                TheSecondSeat.CharacterCard.CharacterCardSystem.UpdateCard();
            }
            catch (Exception ex)
            {
                Log.Warning($"[NarratorAgent] 更新角色卡失败: {ex.Message}");
            }
        }
        
        private Pawn GetShadowPawn()
        {
            try
            {
                var persona = narratorManager?.GetCurrentPersona();
                if (persona != null && NarratorShadowManager.Instance != null)
                    return NarratorShadowManager.Instance.GetOrCreateShadowPawn(persona);
            }
            catch (Exception ex)
            {
                Log.Warning($"[NarratorAgent] 获取影子实体失败: {ex.Message}");
            }
            return null;
        }

        private async void StartUpdateAsync(string userMessage, GameStateSnapshot snapshot, string selectionContext, bool isGreeting, Pawn shadowPawn)
        {
            await ProcessUpdateAsync(userMessage, snapshot, selectionContext, isGreeting, shadowPawn);
        }

        private async Task ProcessUpdateAsync(string userMessage, GameStateSnapshot snapshot, string selectionContext, bool isGreeting = false, Pawn shadowPawn = null)
        {
            isProcessing = true;
            lastError = "";

            try
            {
                // 1. 缓存游戏状态（Pull 模式）
                GameStateTool.CachedSnapshot = snapshot;

                // ⭐ v3.0: 获取基础输入文本（用于 SmartPrompt 意图匹配）
                // 即使是系统触发的事件，也需要一个描述性的文本来匹配关键词
                string baseInput = GetBaseInput(userMessage, isGreeting);

                // 2. 构建 System Prompt（使用 SmartPrompt，传入 baseInput）
                // 此时 SmartPrompt 会分析 baseInput 中的关键词（如 "加载", "袭击"）来加载对应模块
                var systemPrompt = BuildSystemPrompt(baseInput, isGreeting, shadowPawn);

                // 3. 确保 Agent 存在
                if (coreAgent == null)
                    Initialize();
                coreAgent.SystemPrompt = systemPrompt;

                // 4. 联网搜索（如果需要，仅针对玩家显式输入）
                string searchContext = "";
                if (!string.IsNullOrEmpty(userMessage))
                {
                    searchContext = await PerformWebSearchIfNeeded(userMessage);
                }

                // 5. 构建最终发给 LLM 的用户消息
                string enhancedUserMessage = BuildUserMessage(userMessage, selectionContext, searchContext, isGreeting);

                // 6. 使用 RimAgent 核心执行 (自动处理 ReAct 循环和 JSON 解析)
                var agentResponse = await coreAgent.ExecuteAsync(enhancedUserMessage);

                if (!agentResponse.Success)
                {
                    Log.Error($"[NarratorAgent] Agent execution failed: {agentResponse.Error}");
                    LongEventHandler.ExecuteWhenFinished(() => Messages.Message($"AI 错误: {agentResponse.Error}", MessageTypeDefOf.NegativeEvent));
                    return;
                }

                string contentToShow = agentResponse.Content;
                if (string.IsNullOrEmpty(contentToShow))
                {
                    contentToShow = "[无回应]";
                }

                // 7. 处理好感度 (从 Metadata 中提取)
                if (agentResponse.Metadata != null && agentResponse.Metadata.TryGetValue("affinity_impact", out var impactObj))
                {
                    try
                    {
                        var impactJson = Newtonsoft.Json.JsonConvert.SerializeObject(impactObj);
                        var impact = Newtonsoft.Json.JsonConvert.DeserializeObject<AffinityImpact>(impactJson);
                        
                        if (impact != null && !string.IsNullOrEmpty(impact.change))
                        {
                            if (float.TryParse(impact.change.Replace("+", ""), out float delta))
                            {
                                if (narratorManager != null)
                                {
                                    narratorManager.ModifyFavorability(delta, impact.reason ?? "Interaction");
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Warning($"[NarratorAgent] Failed to parse affinity impact: {ex.Message}");
                    }
                }

                // 8. 处理表情 (从 Metadata 中提取并构建 LLMResponse 代理)
                var fakeLLMResponse = new LLMResponse();
                if (agentResponse.Metadata != null && agentResponse.Metadata.TryGetValue("emotion", out var emotionObj))
                {
                    // 支持 "Happy" 或 "Happy|Sad" (多段情绪)
                    fakeLLMResponse.emotion = emotionObj?.ToString();
                }

                // 9. 显示结果 & 应用表情 & TTS
                string capturedEmoticon = fakeLLMResponse.emoticon ?? fakeLLMResponse.emotion ?? "";
                LongEventHandler.ExecuteWhenFinished(() => {
                    Messages.Message("AI Responded", MessageTypeDefOf.TaskCompletion);
                    
                    // 应用表情 (通过 Controller 复用高级逻辑：多段情绪、字数估算等)
                    string defName = narratorManager?.GetCurrentPersona()?.defName ?? "Cassandra_Classic";
                    try
                    {
                        expressionController.ApplyExpressionFromResponse(fakeLLMResponse, defName, contentToShow);
                    }
                    catch (Exception ex) { Log.Warning($"[NarratorAgent] 更新表情失败: {ex.Message}"); }
                    
                    // ⭐ v3.1.0: 调用 TTS 播放（修复：之前漏掉了这一步）
                    ttsHandler.AutoPlayTTS(contentToShow, capturedEmoticon);
                });
            }
            catch (Exception ex)
            {
                lastError = $"错误: {ex.Message}";
                Log.Error($"[NarratorAgent] 处理失败: {ex.Message}\n{ex.StackTrace}");
                LongEventHandler.ExecuteWhenFinished(() => Messages.Message($"AI 处理失败: {ex.Message}", MessageTypeDefOf.NegativeEvent));
            }
            finally
            {
                isProcessing = false;
            }
        }
        
        private string BuildSystemPrompt(string baseInput, bool isGreeting, Pawn shadowPawn)
        {
            // ⭐ v3.0: 传递 baseInput 给 Generator，由 Preset 中的 {{ load_smart_modules }} 处理
            // baseInput 包含了玩家输入或系统事件描述
            
            string systemPrompt = "";
            if (narratorManager != null)
            {
                var persona = narratorManager.GetCurrentPersona();
                var agent = narratorManager.StorytellerAgent;
                
                if (persona != null && agent != null)
                {
                    AIDifficultyMode difficultyMode = AIDifficultyMode.Assistant;
                    try
                    {
                        difficultyMode = (AIDifficultyMode)Enum.Parse(typeof(AIDifficultyMode), narratorManager.CurrentNarratorMode.ToString());
                    }
                    catch { }

                    // ⭐ 关键修改：直接传入 baseInput，不再判断 isGreeting
                    // 这样即使是 Greeting 或系统事件，只要 baseInput 中有关键词，也能触发 SmartPrompt
                    systemPrompt = SystemPromptGenerator.GenerateSystemPrompt(
                        persona, 
                        null, 
                        agent, 
                        difficultyMode,
                        baseInput 
                    );
                }
                else
                {
                    systemPrompt = GetDefaultSystemPrompt();
                }
            }
            else
            {
                systemPrompt = GetDefaultSystemPrompt();
            }
            
            // 注入记忆 (保持兼容性)
            systemPrompt = SimpleRimTalkIntegration.GetMemoryPrompt(
                basePrompt: systemPrompt,
                pawn: shadowPawn,
                maxPersonalMemories: 5,
                maxKnowledgeEntries: 3
            );
            
            Log.Message("[NarratorAgent] 已构建 System Prompt（含 SmartPrompt 和记忆）");
            return systemPrompt;
        }
        
        private async Task<string> PerformWebSearchIfNeeded(string userMessage)
        {
            var modSettings = LoadedModManager.GetMod<Settings.TheSecondSeatMod>()?.GetSettings<Settings.TheSecondSeatSettings>();
            
            if (modSettings?.enableWebSearch == true && 
                !string.IsNullOrEmpty(userMessage) && 
                WebSearchService.ShouldSearch(userMessage))
            {
                Log.Message($"[NarratorAgent] 执行联网搜索: {userMessage}");
                var searchResult = await WebSearchService.Instance.SearchAsync(userMessage, maxResults: 3);
                
                if (searchResult != null)
                {
                    MemoryContextBuilder.RecordEvent($"网络搜索: {userMessage} - 找到 {searchResult.Results.Count} 个结果", MemoryImportance.Medium);
                    return WebSearchService.FormatResultsForContext(searchResult);
                }
            }
            return "";
        }
        
        /// <summary>
        /// ⭐ v3.0: 获取基础输入文本（用于 SmartPrompt 匹配）
        /// </summary>
        private string GetBaseInput(string userMessage, bool isGreeting)
        {
            if (!string.IsNullOrEmpty(userMessage)) return userMessage;

            if (isGreeting)
            {
                return "[系统事件: 游戏加载] 玩家进入游戏，需要问候。";
            }

            // TODO: 处理其他系统事件（如 Raid）
            // 如果 userMessage 为空且不是 Greeting，可能是其他自动触发的事件
            // 目前 TriggerUpdate 主要用于 Greeting 和 玩家对话
            // 未来扩展时需在此处添加更多逻辑
            return "[系统事件: 自动触发]";
        }

        private string BuildUserMessage(string userMessage, string selectionContext, string searchContext, bool isGreeting)
        {
            if (isGreeting || string.IsNullOrEmpty(userMessage))
            {
                var bioRhythm = Current.Game?.GetComponent<NarratorBioRhythm>();
                string bioContext = bioRhythm?.GetCurrentBioContext() ?? "";
                string realTime = DateTime.Now.ToString("HH:mm");
                
                if (!string.IsNullOrEmpty(bioContext))
                {
                    return $"[系统事件: 游戏加载]\n现实时间: {realTime}\n{bioContext}\n" +
                           $"指令: 玩家刚刚加载了游戏存档。请综合分析你当前的【生活节律】数据和【现实时间】，以最自然的方式向玩家打招呼。\n" +
                           $"请保持简短沉浸，不需要汇报殖民地状态。";
                }
                return $"[系统事件: 游戏加载]\n现实时间: {realTime}\n指令: 请根据当前时间向玩家打招呼。";
            }
            
            return searchContext + selectionContext + userMessage;
        }
        
        private string GetDefaultSystemPrompt()
        {
            return "你是一个 RimWorld 的 AI 叙事者。你的任务是观察游戏状态，与玩家互动，并根据需要执行操作。";
        }

        // ⭐ v3.1.0: 已删除未使用的 ProcessResponse 方法（旧架构遗留代码）
        // 新流程: ProcessUpdateAsync -> RimAgent.ExecuteAsync -> ttsHandler.AutoPlayTTS
        
        private void ExecuteCommand(LLMCommand llmCommand)
        {
            try
            {
                var parsedCommand = new ParsedCommand
                {
                    action = llmCommand.action,
                    originalQuery = "",
                    confidence = 1f,
                    parameters = new AdvancedCommandParams
                    {
                        target = llmCommand.target,
                        scope = "Map"
                    }
                };

                if (llmCommand.parameters != null)
                {
                    parsedCommand.parameters.filters = llmCommand.parameters;
                    
                    if (llmCommand.parameters.TryGetValue("limit", out var limitObj))
                        if (int.TryParse(limitObj?.ToString(), out int limit))
                            parsedCommand.parameters.count = limit;
                    
                    if (llmCommand.parameters.TryGetValue("scope", out var scopeObj))
                        parsedCommand.parameters.scope = scopeObj?.ToString() ?? "Map";
                }

                var result = GameActionExecutor.Execute(parsedCommand);
                MemoryContextBuilder.RecordEvent($"执行命令 {llmCommand.action}: {result.message}", result.success ? MemoryImportance.High : MemoryImportance.Medium);

                if (!result.success)
                    Messages.Message($"命令失败: {result.message}", MessageTypeDefOf.RejectInput);
                else
                    Log.Message($"[NarratorAgent] 命令成功: {result.message}");
            }
            catch (Exception ex)
            {
                Log.Error($"[NarratorAgent] 执行命令失败: {ex.Message}");
                Messages.Message($"命令执行异常: {ex.Message}", MessageTypeDefOf.RejectInput);
            }
        }
        
        // Helper class for affinity parsing
        private class AffinityImpact
        {
            public string change { get; set; }
            public string reason { get; set; }
        }

        public void Dispose()
        {
            coreAgent?.Dispose();
            coreAgent = null;
        }
    }
}
