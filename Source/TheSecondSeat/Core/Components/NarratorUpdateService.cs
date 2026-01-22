using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TheSecondSeat.Commands;
using TheSecondSeat.Core;
using TheSecondSeat.Execution;
using TheSecondSeat.Integration;
using TheSecondSeat.LLM;
using TheSecondSeat.Monitoring;
using TheSecondSeat.Narrator;
using TheSecondSeat.NaturalLanguage;
using TheSecondSeat.PersonaGeneration;
using TheSecondSeat.RimAgent.Tools;
using TheSecondSeat.UI;
using TheSecondSeat.WebSearch;
using UnityEngine;
using Verse;
using RimWorld;

namespace TheSecondSeat.Core.Components
{
    /// <summary>
    /// Handles the core AI update logic: capturing state, querying LLM, and processing response
    /// </summary>
    public class NarratorUpdateService
    {
        private readonly NarratorManager? narratorManager;
        private readonly NarratorExpressionController expressionController;
        private readonly NarratorTTSHandler ttsHandler;
        
        private bool isProcessing = false;
        private string lastDialogue = "";
        private string lastError = "";

        public bool IsProcessing => isProcessing;
        public string LastDialogue => lastDialogue;
        public string LastError => lastError;

        public NarratorUpdateService(NarratorManager? manager, NarratorExpressionController exprCtrl, NarratorTTSHandler tts)
        {
            narratorManager = manager;
            expressionController = exprCtrl;
            ttsHandler = tts;
        }

        /// <summary>
        /// Manually trigger a narrator update
        /// </summary>
        public void TriggerNarratorUpdate(string userMessage = "", bool hasGreetedOnLoad = true)
        {
            if (isProcessing)
            {
                Messages.Message("TSS_AlreadyProcessing".Translate(), MessageTypeDefOf.RejectInput);
                return;
            }

            // 修复：在主线程捕获游戏状态
            GameStateSnapshot snapshot;
            string gameStateJson;
            string selectionContext = "";
            
            try
            {
                // 1. 在主线程捕获游戏状态（必须！）
                snapshot = GameStateSnapshotUtility.CaptureSnapshotSafe();
                gameStateJson = GameStateSnapshotUtility.SnapshotToJson(snapshot);

                // ? 捕获玩家当前选中的物体
                if (Find.Selector != null && Find.Selector.SelectedObjects.Count > 0)
                {
                    var selectedInfo = new List<string>();
                    foreach (var obj in Find.Selector.SelectedObjects)
                    {
                        if (obj is Thing t)
                        {
                            selectedInfo.Add($"{t.Label} (def: {t.def.defName}) @ {t.Position}");
                        }
                        else if (obj is Zone z)
                        {
                            selectedInfo.Add($"区域: {z.label} (Cells: {z.Cells.Count})");
                        }
                    }
                    
                    if (selectedInfo.Count > 0)
                    {
                        selectionContext = $"[玩家当前选中:\n{string.Join("\n", selectedInfo)}\n]\n";
                        Log.Message($"[NarratorController] 捕获选中物体: {selectedInfo.Count} 个");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[The Second Seat] Failed to capture game state: {ex.Message}");
                Messages.Message("Failed to capture game state", MessageTypeDefOf.RejectInput);
                return;
            }

            // ? 判断是否是首次问候
            bool isGreeting = string.IsNullOrEmpty(userMessage) && !hasGreetedOnLoad;
            
            // ? 设置思考表情（数据传输中）
            try
            {
                var persona = narratorManager?.GetCurrentPersona();
                if (persona != null)
                {
                    ExpressionSystem.SetThinkingExpression(persona.defName);
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[NarratorController] 设置思考表情失败: {ex.Message}");
            }

            // ? 在主线程预先更新角色卡 (CharacterCard)
            // 确保在后台线程生成 Prompt 时可以直接读取缓存，避免访问 Unity API
            try
            {
                TheSecondSeat.CharacterCard.CharacterCardSystem.UpdateCard();
            }
            catch (Exception ex)
            {
                Log.Warning($"[NarratorController] 更新角色卡失败: {ex.Message}");
            }

            // ⭐ v2.3.0: 获取影子实体 (Shadow Pawn)
            Pawn shadowPawn = null;
            try
            {
                var persona = narratorManager?.GetCurrentPersona();
                if (persona != null && NarratorShadowManager.Instance != null)
                {
                    shadowPawn = NarratorShadowManager.Instance.GetOrCreateShadowPawn(persona);
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[NarratorController] 获取影子实体失败: {ex.Message}");
            }

            // 2. 在主线程异步处理 (UnityWebRequest 必须在主线程)
            StartUpdateAsync();
        }

        private async void StartUpdateAsync(string userMessage, string gameStateJson, string selectionContext, bool isGreeting, Pawn shadowPawn)
        {
            await ProcessNarratorUpdateAsync(userMessage, gameStateJson, selectionContext, isGreeting, shadowPawn);
        }

        private async Task ProcessNarratorUpdateAsync(string userMessage, string gameStateJson, string selectionContext, bool isGreeting = false, Pawn shadowPawn = null)
        {
            isProcessing = true;
            lastError = ""; // 清除之前的错误

            try
            {
                // 游戏状态已经在主线程捕获，直接使用 JSON 字符串

                // 2. Get dynamic system prompt based on favorability
                // CharacterCard 已经在主线程更新，GetDynamicSystemPrompt -> SystemPromptGenerator 会自动读取它
                var systemPrompt = narratorManager?.GetDynamicSystemPrompt() ?? GetDefaultSystemPrompt();
                
                // ? 使用新的 SimpleRimTalkIntegration.GetMemoryPrompt
                // ⭐ v2.3.0: 传入 ShadowPawn 以启用长期记忆
                systemPrompt = SimpleRimTalkIntegration.GetMemoryPrompt(
                    basePrompt: systemPrompt,
                    pawn: shadowPawn,
                    maxPersonalMemories: 5,
                    maxKnowledgeEntries: 3
                );
                
                Log.Message("[NarratorController] 已注入记忆上下文和全局游戏状态到 System Prompt");

                // 4. 检查是否需要联网搜索
                string searchContext = "";
                var modSettings = LoadedModManager.GetMod<Settings.TheSecondSeatMod>()?.GetSettings<Settings.TheSecondSeatSettings>();
                
                if (modSettings?.enableWebSearch == true && 
                    !string.IsNullOrEmpty(userMessage) && 
                    WebSearchService.ShouldSearch(userMessage))
                {
                    Log.Message($"[NarratorController] 检测到需要联网搜索: {userMessage}");
                    var searchResult = await WebSearchService.Instance.SearchAsync(userMessage, maxResults: 3);
                    
                    if (searchResult != null)
                    {
                        searchContext = WebSearchService.FormatResultsForContext(searchResult);
                        Log.Message($"[NarratorController] 搜索完成，添加 {searchResult.Results.Count} 个结果到上下文");
                        
                        // 记录搜索到记忆
                        MemoryContextBuilder.RecordEvent(
                            $"网络搜索: {userMessage} - 找到 {searchResult.Results.Count} 个结果",
                            MemoryImportance.Medium
                        );
                    }
                }

                // ? 根据情况构建用户消息（不再包含 memoryContext）
                string enhancedUserMessage;
                if (isGreeting || string.IsNullOrEmpty(userMessage))
                {
                    // ? 首次加载问候 - 简单提示，不要强调"观察状态"
                    enhancedUserMessage = "玩家刚刚加载了游戏存档。请简短地打个招呼，不需要汇报殖民地状态。";
                }
                else
                {
                    // ? 玩家主动发送消息（不包含 memoryContext）
                    // 包含搜索结果、选中物体上下文
                    // 时间和生物节律已移至 System Prompt (CharacterCard)
                    enhancedUserMessage = searchContext + selectionContext + userMessage;
                }

                // 5. Send to LLM
                var response = await LLMService.Instance.SendStateAndGetActionAsync(
                    systemPrompt, 
                    gameStateJson, 
                    enhancedUserMessage);

                if (response == null)
                {
                    lastError = "LLM API 调用失败 - 请检查配置";
                    Log.Error("[The Second Seat] No response from LLM - API call failed");
                    
                    // 在主线程显示错误消息
                    LongEventHandler.ExecuteWhenFinished(() => 
                    {
                        Messages.Message("TSS_APICallFailed".Translate(), MessageTypeDefOf.NegativeEvent);
                    });
                    return;
                }

                // 6. Process response on main thread - 修复：使用 LongEventHandler
                LongEventHandler.ExecuteWhenFinished(() => ProcessResponse(response, userMessage));
            }
            catch (Exception ex)
            {
                lastError = $"错误: {ex.Message}";
                Log.Error($"[The Second Seat] Error in narrator update: {ex.Message}\n{ex.StackTrace}");
                
                // 在主线程显示错误消息
                LongEventHandler.ExecuteWhenFinished(() => 
                {
                    Messages.Message($"AI 处理失败: {ex.Message}", MessageTypeDefOf.NegativeEvent);
                });
            }
            finally
            {
                isProcessing = false;
            }
        }
        
        
        // ? 默认 System Prompt
        private string GetDefaultSystemPrompt()
        {
            return "你是一个 RimWorld 的 AI 叙事者。你的任务是观察游戏状态，与玩家互动，并根据需要执行操作。";
        }

        private void ProcessResponse(LLMResponse response, string userMessage)
        {
            try
            {
                // ⭐ v2.3.0: 记录活动，唤醒打瞌睡的叙事者
                NarratorIdleSystem.RecordActivity("AI响应");
                
                // 记录对话（作为一次交互）
                var interactionMonitor = Current.Game?.GetComponent<PlayerInteractionMonitor>();
                interactionMonitor?.RecordConversation(!string.IsNullOrEmpty(userMessage));

                // 获取当前人格信息
                string narratorDefName = "Cassandra_Classic";
                string narratorName = "卡桑德拉";
                if (narratorManager != null)
                {
                    var persona = narratorManager.GetCurrentPersona();
                    if (persona != null)
                    {
                        narratorDefName = persona.defName;
                        // ? 优先使用本地化名字 (label)，避免中文环境显示英文名
                        narratorName = !string.IsNullOrEmpty(persona.label) ? persona.label : persona.narratorName;
                    }
                }

                // ? 记录玩家消息到 RimTalk 记忆扩展
                if (!string.IsNullOrEmpty(userMessage))
                {
                    // 内部记忆系统
                    MemoryContextBuilder.RecordConversation("Player", userMessage, false);
                    
                    // ? RimTalk 记忆扩展
                    RimTalkMemoryIntegration.RecordConversation(
                        narratorDefName,
                        narratorName,
                        "Player",
                        userMessage,
                        importance: 0.8f,
                        tags: new List<string> { "玩家对话", "叙述者互动" }
                    );
                }

                // 获取干净的对话内容
                string displayText = response.dialogue;
                
                if (string.IsNullOrEmpty(displayText))
                {
                    displayText = "[AI 正在思考...]";
                }
                
                // 保存最新的对话（带动作）
                lastDialogue = displayText;
                
                // ? v1.6.82: 提取并应用表情 - 支持单表情和多表情序列
                try
                {
                    expressionController.ApplyExpressionFromResponse(response, narratorDefName, displayText);
                }
                catch (Exception ex)
                {
                    Log.Warning($"[NarratorController] 更新表情失败: {ex.Message}");
                }
                
                // ? 记录 AI 回复到 RimTalk 记忆扩展
                MemoryContextBuilder.RecordConversation("Cassandra", displayText, false);
                RimTalkMemoryIntegration.RecordConversation(
                    narratorDefName,
                    narratorName,
                    "Narrator",
                    displayText,
                    importance: 0.7f,
                    tags: new List<string> { "AI回复", "叙述者互动", narratorName }
                );
                
                // ⭐ v1.6.95: 处理对话好感度变化
                if (response.affinityDelta != 0f && narratorManager != null)
                {
                    // 限制范围 -10 ~ +10，避免异常值
                    float clampedDelta = UnityEngine.Mathf.Clamp(response.affinityDelta, -10f, 10f);
                    narratorManager.ModifyFavorability(clampedDelta, "对话互动");
                    
                    // 在 Dev 模式下显示好感度变化
                    if (Prefs.DevMode && UnityEngine.Mathf.Abs(clampedDelta) > 0.1f)
                    {
                        string deltaText = clampedDelta > 0 ? $"+{clampedDelta:F1}" : $"{clampedDelta:F1}";
                        Log.Message($"[NarratorController] 对话好感度变化: {deltaText}");
                    }
                }

                // ⭐ v2.2.0: 处理角色卡更新
                if (response.updateCard != null)
                {
                    var bio = Current.Game?.GetComponent<NarratorBioRhythm>();
                    if (bio != null && !string.IsNullOrEmpty(response.updateCard.energy))
                    {
                        float newEnergy = -1f;
                        switch (response.updateCard.energy.ToLower())
                        {
                            case "energetic": newEnergy = 90f; break;
                            case "active": newEnergy = 70f; break;
                            case "normal": newEnergy = 50f; break;
                            case "tired": newEnergy = 30f; break;
                            case "exhausted": newEnergy = 10f; break;
                        }

                        if (newEnergy >= 0)
                        {
                            bio.SetEnergy(newEnergy);
                            Log.Message($"[NarratorController] AI 更新精力状态: {response.updateCard.energy} ({newEnergy})");
                        }
                    }
                }
                
                // 获取表情ID（如果有）
                string emoticonId = "";
                if (!string.IsNullOrEmpty(response.emoticon))
                {
                    emoticonId = response.emoticon;
                    Log.Message($"[NarratorController] AI 使用表情符: {emoticonId}");
                }
                
                // ? 准备流式消息（但先不显示，等待 TTS 或超时）
                // DialogueOverlayPanel.SetStreamingMessage(displayText); // ? 移除此行，由 AutoPlayTTS 内部调用
                
                Log.Message($"[NarratorController] AI says: {displayText}");

                // ? 自动播放 TTS（根据设置）
                // 如果 TTS 禁用，将立即触发流式显示
                // ? 传递 emoticonId 以便在显示消息时使用
                ttsHandler.AutoPlayTTS(displayText, emoticonId);

                // 执行命令（默认执行）
                if (response.command != null)
                {
                    ExecuteAdvancedCommand(response.command);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[NarratorController] Error processing response: {ex.Message}\n{ex.StackTrace}");
            }
        }
        
        /// <summary>
        /// 执行高级命令（直接构造 ParsedCommand，不再重复解析）
        /// ? v1.6.40: 优化命令执行流程，避免数据丢失
        /// </summary>
        private void ExecuteAdvancedCommand(LLMCommand llmCommand)
        {
            try
            {
                // ? 1. 直接构造 ParsedCommand
                var parsedCommand = new ParsedCommand
                {
                    action = llmCommand.action,
                    originalQuery = "",
                    confidence = 1f, // LLM 输出的命令置信度默认为 1
                    parameters = new AdvancedCommandParams
                    {
                        target = llmCommand.target,
                        scope = "Map" // 默认作用域
                    }
                };

                // ? 2. 处理 parameters 字段
                if (llmCommand.parameters != null)
                {
                    // 尝试转换为 Dictionary<string, object>
                    var paramsDict = new Dictionary<string, object>();
                    
                    // 处理 JObject 或 dynamic 类型
                    if (llmCommand.parameters is Newtonsoft.Json.Linq.JObject jObj)
                    {
                        // 反序列化为 Dictionary
                        paramsDict = jObj.ToObject<Dictionary<string, object>>() ?? new Dictionary<string, object>();
                    }
                    else if (llmCommand.parameters is Dictionary<string, object> dict)
                    {
                        paramsDict = dict;
                    }
                    else
                    {
                        // 尝试序列化再反序列化
                        try
                        {
                            string json = Newtonsoft.Json.JsonConvert.SerializeObject(llmCommand.parameters);
                            paramsDict = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, object>>(json) 
                                ?? new Dictionary<string, object>();
                        }
                        catch
                        {
                            Log.Warning($"[NarratorController] 无法转换 parameters: {llmCommand.parameters}");
                        }
                    }
                    
                    // ? 3. 将 Dictionary 赋值给 filters
                    parsedCommand.parameters.filters = paramsDict;
                    
                    // ? 4. 关键！检查 "limit" 并映射到 count
                    if (paramsDict.TryGetValue("limit", out var limitObj))
                    {
                        if (limitObj is int limitInt)
                        {
                            parsedCommand.parameters.count = limitInt;
                        }
                        else if (int.TryParse(limitObj?.ToString(), out int parsedLimit))
                        {
                            parsedCommand.parameters.count = parsedLimit;
                        }
                    }
                    
                    // ? 5. 如果 parameters 包含 scope，覆盖默认值
                    if (paramsDict.TryGetValue("scope", out var scopeObj))
                    {
                        parsedCommand.parameters.scope = scopeObj?.ToString() ?? "Map";
                    }
                    
                    // ? 6. 处理 priority 标志
                    if (paramsDict.TryGetValue("priority", out var priorityObj))
                    {
                        if (priorityObj is bool priorityBool)
                        {
                            parsedCommand.parameters.priority = priorityBool;
                        }
                        else if (bool.TryParse(priorityObj?.ToString(), out bool parsedPriority))
                        {
                            parsedCommand.parameters.priority = parsedPriority;
                        }
                    }
                }

                Log.Message($"[NarratorController] 构造 ParsedCommand: Action={parsedCommand.action}, Target={parsedCommand.parameters.target}, Count={parsedCommand.parameters.count}");

                // ? 7. 执行命令
                var result = GameActionExecutor.Execute(parsedCommand);

                // ? 9. 记录到记忆
                MemoryContextBuilder.RecordEvent(
                    $"执行命令 {llmCommand.action}: {result.message}",
                    result.success ? MemoryImportance.High : MemoryImportance.Medium
                );

                // ? 10. 显示结果
                if (!result.success)
                {
                    Messages.Message($"命令失败: {result.message}", MessageTypeDefOf.RejectInput);
                }
                else
                {
                    Log.Message($"[NarratorController] 命令成功: {result.message}");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[NarratorController] 执行命令失败: {ex.Message}\n{ex.StackTrace}");
                Messages.Message($"命令执行异常: {ex.Message}", MessageTypeDefOf.RejectInput);
            }
        }
    }
}
