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
            
            Log.Message($"[NarratorAgent] 初始化完��: {agentId} (MaxIterations=3, Tools={coreAgent.GetDebugInfo()})");
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

                // 2. 构建 System Prompt（使用 SmartPrompt）
                var systemPrompt = BuildSystemPrompt(userMessage, isGreeting, shadowPawn);

                // 3. 确保 Agent 存在
                if (coreAgent == null)
                    Initialize();
                coreAgent.SystemPrompt = systemPrompt;

                // 4. 联网搜索（如果需要）
                string searchContext = await PerformWebSearchIfNeeded(userMessage);

                // 5. 构建用户消息
                string enhancedUserMessage = BuildUserMessage(userMessage, selectionContext, searchContext, isGreeting);

                // 6. 执行 ReAct 循环
                var agentResponse = await coreAgent.ExecuteAsync(enhancedUserMessage, temperature: 0.7f, maxTokens: 800);

                if (!agentResponse.Success || string.IsNullOrEmpty(agentResponse.Content))
                {
                    lastError = $"LLM API 调用失败: {agentResponse.Error ?? "无响应"}";
                    Log.Error($"[NarratorAgent] {lastError}");
                    LongEventHandler.ExecuteWhenFinished(() => Messages.Message("TSS_APICallFailed".Translate(), MessageTypeDefOf.NegativeEvent));
                    return;
                }

                // 7. 解析响应
                LLMResponse response = llmProvider.LastFullResponse ?? ParseAgentResponse(agentResponse.Content);

                // 8. 在主线程处理
                LongEventHandler.ExecuteWhenFinished(() => ProcessResponse(response, userMessage));
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
        
        private string BuildSystemPrompt(string userMessage, bool isGreeting, Pawn shadowPawn)
        {
            var basePrompt = narratorManager?.GetDynamicSystemPrompt() ?? GetDefaultSystemPrompt();
            
            // SmartPrompt 动态加载
            string smartPromptSection = "";
            if (!isGreeting && !string.IsNullOrEmpty(userMessage))
            {
                try
                {
                    var smartResult = SmartPromptBuilder.Instance.Build(userMessage);
                    if (!string.IsNullOrEmpty(smartResult.Prompt))
                    {
                        smartPromptSection = $"\n\n[相关知识模块 ({smartResult.ModuleCount} 个)]\n{smartResult.Prompt}";
                        Log.Message($"[NarratorAgent] SmartPrompt 激活: {smartResult.ModuleCount} 个模块");
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning($"[NarratorAgent] SmartPrompt 加载失败: {ex.Message}");
                }
            }
            
            var systemPrompt = basePrompt + smartPromptSection;
            
            // 注入记忆
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
        
        private LLMResponse ParseAgentResponse(string content)
        {
            var parsed = LLMResponseParser.Parse(content);
            if (parsed != null) return parsed;
            
            return new LLMResponse
            {
                dialogue = content,
                expression = "neutral",
                affinityDelta = 0f
            };
        }
        
        private string GetDefaultSystemPrompt()
        {
            return "你是一个 RimWorld 的 AI 叙事者。你的任务是观察游戏状态，与玩家互动，并根据需要执行操作。";
        }

        private void ProcessResponse(LLMResponse response, string userMessage)
        {
            try
            {
                NarratorIdleSystem.RecordActivity("AI响应");
                
                var interactionMonitor = Current.Game?.GetComponent<PlayerInteractionMonitor>();
                interactionMonitor?.RecordConversation(!string.IsNullOrEmpty(userMessage));

                string narratorDefName = "Cassandra_Classic";
                string narratorName = "卡桑德拉";
                if (narratorManager != null)
                {
                    var persona = narratorManager.GetCurrentPersona();
                    if (persona != null)
                    {
                        narratorDefName = persona.defName;
                        narratorName = !string.IsNullOrEmpty(persona.label) ? persona.label : persona.narratorName;
                    }
                }

                // 记录对话
                if (!string.IsNullOrEmpty(userMessage))
                {
                    MemoryContextBuilder.RecordConversation("Player", userMessage, false);
                    RimTalkMemoryIntegration.RecordConversation(narratorDefName, narratorName, "Player", userMessage, importance: 0.8f, tags: new List<string> { "玩家对话" });
                }

                string displayText = response.dialogue;
                if (string.IsNullOrEmpty(displayText))
                    displayText = "[AI 正在思考...]";
                
                lastDialogue = displayText;
                
                // 表情
                try { expressionController.ApplyExpressionFromResponse(response, narratorDefName, displayText); }
                catch (Exception ex) { Log.Warning($"[NarratorAgent] 更新表情失败: {ex.Message}"); }
                
                // 记录 AI 回复
                MemoryContextBuilder.RecordConversation("Cassandra", displayText, false);
                RimTalkMemoryIntegration.RecordConversation(narratorDefName, narratorName, "Narrator", displayText, importance: 0.7f, tags: new List<string> { "AI回复" });
                
                // 好感度
                if (response.affinityDelta != 0f && narratorManager != null)
                {
                    float clampedDelta = UnityEngine.Mathf.Clamp(response.affinityDelta, -10f, 10f);
                    narratorManager.ModifyFavorability(clampedDelta, "对话互动");
                }

                // 角色卡更新
                if (response.updateCard != null)
                {
                    var bio = Current.Game?.GetComponent<NarratorBioRhythm>();
                    if (bio != null && !string.IsNullOrEmpty(response.updateCard.energy))
                    {
                        float newEnergy = response.updateCard.energy.ToLower() switch
                        {
                            "energetic" => 90f,
                            "active" => 70f,
                            "normal" => 50f,
                            "tired" => 30f,
                            "exhausted" => 10f,
                            _ => -1f
                        };
                        if (newEnergy >= 0) bio.SetEnergy(newEnergy);
                    }
                }
                
                Log.Message($"[NarratorAgent] 输出: {displayText}");

                // TTS
                string emoticonId = response.emoticon ?? "";
                ttsHandler.AutoPlayTTS(displayText, emoticonId);

                // 命令
                if (response.command != null)
                    ExecuteCommand(response.command);
            }
            catch (Exception ex)
            {
                Log.Error($"[NarratorAgent] 处理响应失败: {ex.Message}\n{ex.StackTrace}");
            }
        }
        
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
        
        public void Dispose()
        {
            coreAgent?.Dispose();
            coreAgent = null;
        }
    }
}
