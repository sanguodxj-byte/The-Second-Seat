using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TheSecondSeat.Commands;
using TheSecondSeat.LLM;
using TheSecondSeat.Narrator;
using TheSecondSeat.Observer;
using TheSecondSeat.NaturalLanguage;
using TheSecondSeat.Execution;
using TheSecondSeat.Integration;
using TheSecondSeat.RimAgent;
using TheSecondSeat.WebSearch;
using Verse;
using RimWorld;

namespace TheSecondSeat.Core
{
    /// <summary>
    /// Main controller that orchestrates the AI narrator loop
    /// ? 修复：移除自动定时更新，只在玩家主动触发或首次加载时发送消息
    /// </summary>
    public class NarratorController : GameComponent
    {
        private NarratorManager? narratorManager;
        private TheSecondSeat.RimAgent.RimAgent? agent;
        private bool isProcessing = false;
        private string lastDialogue = "";
        private string lastError = "";
        
        // ? 首次加载标记（只在游戏加载时触发一次问候）
        private bool hasGreetedOnLoad = false;
        private int ticksSinceLoad = 0;
        private const int GreetingDelayTicks = 300; // 加载后5秒再发送问候

        public string LastDialogue => lastDialogue;
        public bool IsProcessing => isProcessing;
        public string LastError => lastError;

        public NarratorController(Game game) : base()
        {
        }

        public override void GameComponentTick()
        {
            base.GameComponentTick();

            // Get narrator manager from game
            if (narratorManager == null)
            {
                narratorManager = Current.Game.GetComponent<NarratorManager>();
            }
            
            // 初始化 RimAgent（如果未初始化）
            if (agent == null && narratorManager != null)
            {
                try
                {
                    var modSettings = LoadedModManager.GetMod<Settings.TheSecondSeatMod>()?.GetSettings<Settings.TheSecondSeatSettings>();
                    if (modSettings != null)
                    {
                        var provider = TheSecondSeat.RimAgent.LLMProviderFactory.GetProvider(modSettings.llmProvider);
                        
                        agent = new TheSecondSeat.RimAgent.RimAgent(
                            modSettings.agentName,
                            narratorManager.GetDynamicSystemPrompt(),
                            provider
                        );
                        
                        // 注册工具
                        agent.RegisterTool("search");
                        agent.RegisterTool("command");
                        agent.RegisterTool("analyze");
                        
                        Log.Message("[NarratorController] RimAgent 初始化完成，已注册 3 个工具");
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[NarratorController] RimAgent 初始化失败: {ex.Message}");
                }
            }

            // ? 只在首次加载后发送一次问候（延迟5秒）
            if (!hasGreetedOnLoad)
            {
                ticksSinceLoad++;
                if (ticksSinceLoad >= GreetingDelayTicks && !isProcessing)
                {
                    hasGreetedOnLoad = true;
                    // ? 发送加载问候（只触发一次）
                    TriggerLoadGreeting();
                }
            }
            
            // ? 移除自动定时更新 - 不再每60秒自动发送消息
            // 现在AI只会在以下情况发言：
            // 1. 游戏首次加载时（一次）
            // 2. 玩家主动发送消息时
            // 3. 玩家点击聊天窗口发送按钮时
        }

        /// <summary>
        /// ? 首次加载问候（只触发一次）
        /// </summary>
        private void TriggerLoadGreeting()
        {
            Log.Message("[NarratorController] 发送加载问候...");
            TriggerNarratorUpdate(""); // 空消息，让AI自由发挥
        }

        /// <summary>
        /// Manually trigger a narrator update
        /// </summary>
        public void TriggerNarratorUpdate(string userMessage = "")
        {
            if (isProcessing)
            {
                Messages.Message("TSS_AlreadyProcessing".Translate(), MessageTypeDefOf.RejectInput);
                return;
            }

            // 修复：在主线程捕获游戏状态
            GameStateSnapshot snapshot;
            string gameStateJson;
            
            try
            {
                // 1. 在主线程捕获游戏状态（必须！）
                snapshot = GameStateObserver.CaptureSnapshot();
                gameStateJson = GameStateObserver.SnapshotToJson(snapshot);
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
                    PersonaGeneration.ExpressionSystem.SetThinkingExpression(persona.defName);
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[NarratorController] 设置思考表情失败: {ex.Message}");
            }

            // 2. 然后在后台线程处理（不访问游戏数据）
            Task.Run(async () => await ProcessNarratorUpdateAsync(userMessage, gameStateJson, isGreeting));
        }

        private async Task ProcessNarratorUpdateAsync(string userMessage, string gameStateJson, bool isGreeting = false)
        {
            isProcessing = true;
            lastError = ""; // 清除之前的错误

            try
            {
                // ? 获取本地时间信息
                DateTime now = DateTime.Now;
                string timeContext = BuildTimeContext(now);
                
                // 游戏状态已经在主线程捕获，直接使用 JSON 字符串

                // 2. Get dynamic system prompt based on favorability
                var systemPrompt = narratorManager?.GetDynamicSystemPrompt() ?? GetDefaultSystemPrompt();
                
                // ⭐ v1.6.75: 修复 - 更新 Agent 的 System Prompt（确保使用最新约束）
                if (agent != null)
                {
                    agent.SystemPrompt = systemPrompt;
                    Log.Message($"[NarratorController] 已更新 Agent System Prompt，长度: {systemPrompt.Length}");
                }
                
                // ? 在系统提示词中添加时间信息
                systemPrompt = InjectTimeIntoSystemPrompt(systemPrompt, now);

                // ? 使用新的 SimpleRimTalkIntegration.GetMemoryPrompt（叙事者模式 pawn = null）
                systemPrompt = SimpleRimTalkIntegration.GetMemoryPrompt(
                    basePrompt: systemPrompt,
                    pawn: null,  // ? 叙事者 AI 模式
                    maxPersonalMemories: 5,  // 无效（pawn == null）
                    maxKnowledgeEntries: 3   // 自动 +5 = 8 条共通知识 + 全局状态
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
                    enhancedUserMessage = timeContext + 
                        "玩家刚刚加载了游戏存档。请简短地打个招呼，不需要汇报殖民地状态。";
                }
                else
                {
                    // ? 玩家主动发送消息（不包含 memoryContext）
                    enhancedUserMessage = timeContext + searchContext + userMessage;
                }

                // 5. 使用 RimAgent（支持完整游戏状态传递）
                TheSecondSeat.LLM.LLMResponse response;
                
                if (agent != null)
                {
                    // ✅ 使用 RimAgent（传递完整 gameStateJson）
                    Log.Message("[NarratorController] 使用 RimAgent 执行请求（传递完整游戏状态）");
                    
                    var agentResponse = await agent.ExecuteAsync(
                        enhancedUserMessage,
                        gameStateJson,
                        0.5f,  // ⭐ v1.6.75: 降低 temperature 以强制遵守约束
                        modSettings?.maxTokens ?? 500
                    );
                    
                    if (!agentResponse.Success)
                    {
                        lastError = $"Agent 调用失败: {agentResponse.Error}";
                        Log.Error($"[NarratorController] Agent execution failed: {agentResponse.Error}");
                        
                        Verse.LongEventHandler.ExecuteWhenFinished(() => 
                        {
                            Messages.Message($"AI 处理失败: {agentResponse.Error}", MessageTypeDefOf.NegativeEvent);
                        });
                        return;
                    }
                    
                    // 解析 Agent 响应为 LLMResponse
                    response = ParseAgentResponse(agentResponse.Content);
                }
                else
                {
                    // 降级：使用原始 LLMService
                    Log.Warning("[NarratorController] Agent 未初始化，降级使用 LLMService");
                    
                    response = await LLMService.Instance.SendStateAndGetActionAsync(
                        systemPrompt, 
                        gameStateJson, 
                        enhancedUserMessage);
                }

                if (response == null)
                {
                    lastError = "LLM API 调用失败 - 请检查配置";
                    Log.Error("[The Second Seat] No response from LLM - API call failed");
                    
                    // 在主线程显示错误消息
                    Verse.LongEventHandler.ExecuteWhenFinished(() => 
                    {
                        Messages.Message("TSS_APICallFailed".Translate(), MessageTypeDefOf.NegativeEvent);
                    });
                    return;
                }

                // 6. Process response on main thread - 修复：使用 LongEventHandler
                Verse.LongEventHandler.ExecuteWhenFinished(() => ProcessResponse(response, userMessage));
            }
            catch (Exception ex)
            {
                lastError = $"错误: {ex.Message}";
                Log.Error($"[The Second Seat] Error in narrator update: {ex.Message}\n{ex.StackTrace}");
                
                // 在主线程显示错误消息
                Verse.LongEventHandler.ExecuteWhenFinished(() => 
                {
                    Messages.Message($"AI 处理失败: {ex.Message}", MessageTypeDefOf.NegativeEvent);
                });
            }
            finally
            {
                isProcessing = false;
            }
        }
        
        /// <summary>
        /// ? 构建时间上下文信息（供 AI 参考）
        /// ? 简化：移除过多的提示，避免AI过度关注时间
        /// </summary>
        private string BuildTimeContext(DateTime now)
        {
            string timePeriod = GetTimePeriod(now.Hour);
            
            // ? 简化时间上下文，只提供基本信息
            return $"[当前时间: {timePeriod}]\n";
        }
        
        /// <summary>
        /// ? 将时间信息注入系统提示词
        /// ? 简化：减少对时间的强调
        /// </summary>
        private string InjectTimeIntoSystemPrompt(string originalPrompt, DateTime now)
        {
            // ? 简化：不再添加过多的时间相关指示
            // 时间信息已经在 BuildTimeContext 中提供
            return originalPrompt;
        }
        
        /// <summary>
        /// ? 获取时间段描述
        /// </summary>
        private string GetTimePeriod(int hour)
        {
            if (hour >= 0 && hour < 6)
                return "深夜";
            else if (hour >= 6 && hour < 9)
                return "清晨";
            else if (hour >= 9 && hour < 12)
                return "上午";
            else if (hour >= 12 && hour < 14)
                return "中午";
            else if (hour >= 14 && hour < 18)
                return "下午";
            else if (hour >= 18 && hour < 20)
                return "傍晚";
            else if (hour >= 20 && hour < 22)
                return "晚上";
            else
                return "深夜";
        }

        private void ProcessResponse(TheSecondSeat.LLM.LLMResponse response, string userMessage)
        {
            try
            {
                Log.Message("[NarratorController] ===== ProcessResponse 开始 =====");
                Log.Message($"[NarratorController] dialogue: {response.dialogue?.Substring(0, Math.Min(50, response.dialogue?.Length ?? 0))}...");
                Log.Message($"[NarratorController] command: {(response.command != null ? response.command.action : "null")}");
                Log.Message($"[NarratorController] emotion: {response.emotion}"); // ✅ v1.6.66: 新增
                Log.Message($"[NarratorController] viseme: {response.viseme}");   // ✅ v1.6.66: 新增
                
                // 记录对话（作为一次交互）
                var interactionMonitor = Current.Game?.GetComponent<Monitoring.PlayerInteractionMonitor>();
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
                        narratorName = persona.narratorName;
                    }
                }

                // ✅ v1.6.66: 记录情绪标签到情绪追踪系统
                try
                {
                    var emotionTracker = Current.Game?.GetComponent<EmotionTracker>();
                    if (emotionTracker != null && !string.IsNullOrEmpty(response.emotion))
                    {
                        emotionTracker.RecordEmotion(response.emotion, userMessage);
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning($"[NarratorController] 记录情绪失败: {ex.Message}");
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
                
                // ? 新增：提取并应用 expression 字段
                try
                {
                    if (!string.IsNullOrEmpty(response.expression))
                    {
                        // 尝试解析表情类型
                        if (System.Enum.TryParse<PersonaGeneration.ExpressionType>(response.expression, true, out var expressionType))
                        {
                            // 设置表情，持续 3 秒
                            PersonaGeneration.ExpressionSystem.SetExpression(
                                narratorDefName, 
                                expressionType, 
                                180,  // 3 秒 = 180 ticks
                                "对话触发"
                            );
                            
                            Log.Message($"[NarratorController] AI 表情切换: {response.expression}");
                        }
                        else
                        {
                            Log.Warning($"[NarratorController] 无效的表情类型: {response.expression}");
                        }
                    }
                    else if (!string.IsNullOrEmpty(response.emotion))
                    {
                        // ✅ v1.6.66: 如果没有 expression，使用 emotion 映射表情
                        var emotionExpression = MapEmotionToExpression(response.emotion);
                        PersonaGeneration.ExpressionSystem.SetExpression(
                            narratorDefName, 
                            emotionExpression, 
                            180,
                            "情绪触发"
                        );
                        
                        Log.Message($"[NarratorController] 根据情绪设置表情: {response.emotion} → {emotionExpression}");
                    }
                    else
                    {
                        // 如果 AI 没有提供 expression，根据对话内容自动推断
                        PersonaGeneration.ExpressionSystem.UpdateExpressionByDialogueTone(narratorDefName, displayText);
                    }
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
                    tags: new List<string> { "AI回复", "叙述者互动", narratorName, response.emotion ?? "neutral" } // ✅ v1.6.66: 添加情绪标签
                );
                
                // 获取表情ID（如果有）
                string emoticonId = "";
                if (!string.IsNullOrEmpty(response.emoticon))
                {
                    emoticonId = response.emoticon;
                    Log.Message($"[NarratorController] AI 使用表情符: {emoticonId}");
                }
                
                // 添加到聊天窗口（带表情符）
                UI.NarratorWindow.AddAIMessage(displayText, emoticonId);

                // ? 同时发送到系统消息
                SendAsSystemMessage(displayText);

                Log.Message($"[NarratorController] AI says: {displayText}");

                // ⭐ v1.6.75: 自动播放 TTS（支持多种情绪格式）
                if (!string.IsNullOrEmpty(response.emotions))
                {
                    // 紧凑格式：emotions = "happy|worried|angry"
                    AutoPlayTTSWithCompactEmotions(displayText, response.emotions, narratorDefName);
                }
                else if (response.emotionSequence != null && response.emotionSequence.Count > 0)
                {
                    // 详细格式：emotionSequence = [{text, emotion, duration}, ...]
                    AutoPlayTTSWithEmotionSequence(displayText, response.emotionSequence, narratorDefName);
                }
                else
                {
                    // 单情绪模式（向后兼容）
                    AutoPlayTTS(displayText, response.emotion ?? "neutral");
                }
                
                // ✅ 执行命令（确保在主线程）
                if (response.command != null)
                {
                    Log.Message($"[NarratorController] ===== 检测到命令: {response.command.action} =====");
                    Log.Message($"[NarratorController] command.target: {response.command.target}");
                    Log.Message($"[NarratorController] command.parameters: {response.command.parameters}");
                    
                    // ✅ 确保在主线程执行
                    ExecuteAdvancedCommand(response.command);
                }
                else
                {
                    Log.Message("[NarratorController] 无命令需要执行");
                }
                
                Log.Message("[NarratorController] ===== ProcessResponse 完成 =====");
            }
            catch (Exception ex)
            {
                Log.Error($"[NarratorController] Error processing response: {ex.Message}\n{ex.StackTrace}");
            }
        }
        
        /// <summary>
        /// ✅ v1.6.66: 情绪映射到表情
        /// ⭐ v1.6.75: 扩展支持更多情绪标签（包括缩写）
        /// </summary>
        private PersonaGeneration.ExpressionType MapEmotionToExpression(string emotion)
        {
            if (string.IsNullOrEmpty(emotion))
                return PersonaGeneration.ExpressionType.Neutral;
            
            return emotion.ToLower() switch
            {
                // ⭐ 完整名称
                "happy" => PersonaGeneration.ExpressionType.Happy,
                "sad" => PersonaGeneration.ExpressionType.Sad,
                "angry" => PersonaGeneration.ExpressionType.Angry,
                "surprised" => PersonaGeneration.ExpressionType.Surprised,
                "confused" => PersonaGeneration.ExpressionType.Confused,
                "worried" => PersonaGeneration.ExpressionType.Worried,
                "shy" => PersonaGeneration.ExpressionType.Shy,
                "smug" => PersonaGeneration.ExpressionType.Smug,
                "disappointed" => PersonaGeneration.ExpressionType.Disappointed,
                "thoughtful" => PersonaGeneration.ExpressionType.Thoughtful,
                "annoyed" => PersonaGeneration.ExpressionType.Annoyed,
                "playful" => PersonaGeneration.ExpressionType.Playful,
                "neutral" => PersonaGeneration.ExpressionType.Neutral,
                
                // ⭐ v1.6.75: 缩写支持
                "h" => PersonaGeneration.ExpressionType.Happy,
                "s" => PersonaGeneration.ExpressionType.Sad,
                "a" => PersonaGeneration.ExpressionType.Angry,
                "su" => PersonaGeneration.ExpressionType.Surprised,
                "c" => PersonaGeneration.ExpressionType.Confused,
                "w" => PersonaGeneration.ExpressionType.Worried,
                "sh" => PersonaGeneration.ExpressionType.Shy,
                "sm" => PersonaGeneration.ExpressionType.Smug,
                "n" => PersonaGeneration.ExpressionType.Neutral,
                
                // ⭐ 其他常见同义词
                "joy" => PersonaGeneration.ExpressionType.Happy,
                "excited" => PersonaGeneration.ExpressionType.Surprised,
                "thinking" => PersonaGeneration.ExpressionType.Thoughtful,
                "frustrated" => PersonaGeneration.ExpressionType.Annoyed,
                "proud" => PersonaGeneration.ExpressionType.Smug,
                "embarrassed" => PersonaGeneration.ExpressionType.Shy,
                
                _ => PersonaGeneration.ExpressionType.Neutral
            };
        }
        
        /// <summary>
        /// ? 自动播放 TTS（叙事者发言时）
        /// ? 优化：添加加载状态提示
        /// ? v1.6.51: 修复嘴部动画 - 传递 personaDefName 参数
        /// ⭐ v1.6.75: 新增 - 支持情绪标签传递到 TTS
        /// ⭐ v1.6.75: 修复 - 情绪持续时间基于 TTS 音频时长
        /// </summary>
        private void AutoPlayTTS(string text, string emotion = "neutral")
        {
            try
            {
                // 检查是否启用 TTS
                var modSettings = LoadedModManager.GetMod<Settings.TheSecondSeatMod>()?.GetSettings<Settings.TheSecondSeatSettings>();
                
                if (modSettings == null || !modSettings.enableTTS || !modSettings.autoPlayTTS)
                {
                    return; // TTS 未启用，跳过
                }
                
                // 清除动作标记（括号内的内容）
                string cleanText = System.Text.RegularExpressions.Regex.Replace(text, @"\([^)]*\)", "").Trim();
                
                if (string.IsNullOrEmpty(cleanText))
                {
                    return; // 没有实际文本，跳过
                }
                
                // 获取人格 DefName
                string personaDefName = "Cassandra_Classic";
                if (narratorManager != null)
                {
                    var persona = narratorManager.GetCurrentPersona();
                    if (persona != null)
                    {
                        personaDefName = persona.defName;
                    }
                }
                
                // ⭐ v1.6.75: 估算 TTS 音频时长（基于文本长度）
                int estimatedDurationTicks = EstimateTTSDuration(cleanText);
                
                // ⭐ v1.6.75: 设置当前情绪到表情系统（供 TTS 获取）
                if (!string.IsNullOrEmpty(emotion) && emotion != "neutral")
                {
                    var emotionExpression = MapEmotionToExpression(emotion);
                    PersonaGeneration.ExpressionSystem.SetExpression(
                        personaDefName, 
                        emotionExpression, 
                        estimatedDurationTicks,  // ⭐ 使用估算的音频时长
                        "TTS 情绪"
                    );
                    
                    if (Prefs.DevMode)
                    {
                        Log.Message($"[NarratorController] 设置情绪表情: {emotion} → {emotionExpression}，持续 {estimatedDurationTicks} ticks ({estimatedDurationTicks / 60f:F1}秒)");
                    }
                }
                
                // 显示 TTS 加载提示
                Messages.Message("正在生成语音...", MessageTypeDefOf.SilentInput);
                
                // 在后台线程生成 TTS
                Task.Run(async () => 
                {
                    try
                    {
                        string? audioPath = await TTS.TTSService.Instance.SpeakAsync(cleanText, personaDefName);
                        
                        if (!string.IsNullOrEmpty(audioPath))
                        {
                            Log.Message($"[NarratorController] TTS 生成成功: {audioPath} (Persona: {personaDefName}, Emotion: {emotion})");
                            
                            // 在主线程播放 TTS
                            Verse.LongEventHandler.ExecuteWhenFinished(() => 
                            {
                                try
                                {
                                    TTS.TTSAudioPlayer.Instance.PlayAndDelete(audioPath, personaDefName);
                                    Messages.Message($"正在播放语音: {System.IO.Path.GetFileName(audioPath)}", MessageTypeDefOf.TaskCompletion);
                                }
                                catch (Exception playEx)
                                {
                                    Log.Error($"[NarratorController] 播放音频失败: {playEx.Message}");
                                }
                            });
                        }
                        else
                        {
                            // TTS 生成失败
                            Verse.LongEventHandler.ExecuteWhenFinished(() => 
                            {
                                Messages.Message("语音生成失败，请检查 TTS 配置", MessageTypeDefOf.RejectInput);
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Warning($"[NarratorController] TTS 生成失败: {ex.Message}");
                        
                        // 显示错误提示
                        Verse.LongEventHandler.ExecuteWhenFinished(() => 
                        {
                            Messages.Message($"语音生成失败: {ex.Message}", MessageTypeDefOf.RejectInput);
                        });
                    }
                });
            }
            catch (Exception ex)
            {
                Log.Warning($"[NarratorController] AutoPlayTTS 失败: {ex.Message}");
            }
        }

        /// <summary>
        /// ⭐ v1.6.75: 估算 TTS 音频时长（基于文本长度）
        /// </summary>
        /// <param name="text">要转换的文本</param>
        /// <returns>估算的播放时长（ticks）</returns>
        private int EstimateTTSDuration(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return 180; // 默认 3 秒
            }
            
            // 统计字符数
            int chineseChars = 0;
            int englishWords = 0;
            
            foreach (char c in text)
            {
                // 中文字符（CJK Unified Ideographs）
                if (c >= 0x4E00 && c <= 0x9FFF)
                {
                    chineseChars++;
                }
                // 英文和空格
                else if (char.IsLetter(c) || char.IsWhiteSpace(c))
                {
                    // 粗略估算英文单词数
                }
            }
            
            // 估算英文单词数（平均 5 个字符 + 空格 = 1 个单词）
            englishWords = (text.Length - chineseChars) / 6;
            
            // 估算播放时长
            // - 中文：每个字约 0.4 秒（正常语速）
            // - 英文：每个单词约 0.5 秒
            float estimatedSeconds = (chineseChars * 0.4f) + (englishWords * 0.5f);
            
            // 添加缓冲时间（+1秒，确保表情不会提前消失）
            estimatedSeconds += 1.0f;
            
            // 限制范围：最少 3 秒，最多 30 秒
            estimatedSeconds = UnityEngine.Mathf.Clamp(estimatedSeconds, 3f, 30f);
            
            // 转换为 ticks（60 ticks = 1 秒）
            int ticks = (int)(estimatedSeconds * 60f);
            
            if (Prefs.DevMode)
            {
                Log.Message($"[NarratorController] TTS 时长估算: {text.Length} 字符 → {estimatedSeconds:F1} 秒 ({ticks} ticks)");
            }
            
            return ticks;
        }
        
        /// <summary>
        /// ⭐ v1.6.75: 紧凑情绪格式播放（最省 token）
        /// </summary>
        /// <param name="fullText">完整对话文本</param>
        /// <param name="emotionsCompact">情绪序列（紧凑格式，如 "happy|worried|angry"）</param>
        /// <param name="personaDefName">人格 DefName</param>
        private void AutoPlayTTSWithCompactEmotions(string fullText, string emotionsCompact, string personaDefName)
        {
            try
            {
                // 检查是否启用 TTS
                var modSettings = LoadedModManager.GetMod<Settings.TheSecondSeatMod>()?.GetSettings<Settings.TheSecondSeatSettings>();
                
                if (modSettings == null || !modSettings.enableTTS || !modSettings.autoPlayTTS)
                {
                    return;
                }
                
                // 解析紧凑情绪格式
                var emotionTags = emotionsCompact.Split('|');
                
                if (emotionTags.Length == 0)
                {
                    // 降级到单情绪模式
                    AutoPlayTTS(fullText, "neutral");
                    return;
                }
                
                // 清理文本
                string cleanText = System.Text.RegularExpressions.Regex.Replace(fullText, @"\([^)]*\)", "").Trim();
                
                if (string.IsNullOrEmpty(cleanText))
                {
                    return;
                }
                
                // 按 | 分割文本（如果文本中有 | 分隔符）
                var textSegments = fullText.Split('|');
                
                // 如果文本分段数量与情绪标签数量匹配，使用精确对应
                List<TheSecondSeat.LLM.EmotionSegment> segments;
                
                if (textSegments.Length == emotionTags.Length)
                {
                    // 精确对应模式
                    segments = new List<TheSecondSeat.LLM.EmotionSegment>();
                    for (int i = 0; i < textSegments.Length; i++)
                    {
                        segments.Add(new TheSecondSeat.LLM.EmotionSegment
                        {
                            text = textSegments[i].Trim(),
                            emotion = ExpandEmotionTag(emotionTags[i].Trim()),
                            estimatedDuration = 0f  // 自动估算
                        });
                    }
                }
                else
                {
                    // 均分模式：文本均分给每个情绪
                    segments = new List<TheSecondSeat.LLM.EmotionSegment>();
                    int charsPerSegment = cleanText.Length / emotionTags.Length;
                    int startIndex = 0;
                    
                    for (int i = 0; i < emotionTags.Length; i++)
                    {
                        int segmentLength = (i == emotionTags.Length - 1)
                            ? cleanText.Length - startIndex  // 最后一段取剩余
                            : charsPerSegment;
                        
                        string segmentText = cleanText.Substring(startIndex, segmentLength);
                        
                        segments.Add(new TheSecondSeat.LLM.EmotionSegment
                        {
                            text = segmentText,
                            emotion = ExpandEmotionTag(emotionTags[i].Trim()),
                            estimatedDuration = 0f
                        });
                        
                        startIndex += segmentLength;
                    }
                }
                
                if (Prefs.DevMode)
                {
                    Log.Message($"[NarratorController] 紧凑情绪格式: {emotionsCompact} → {segments.Count} 个片段");
                }
                
                // 使用标准情绪序列播放
                AutoPlayTTSWithEmotionSequence(fullText, segments, personaDefName);
            }
            catch (Exception ex)
            {
                Log.Warning($"[NarratorController] 紧凑情绪格式解析失败: {ex.Message}，降级到单情绪模式");
                AutoPlayTTS(fullText, "neutral");
            }
        }
        
        /// <summary>
        /// ⭐ v1.6.75: 扩展情绪标签缩写
        /// </summary>
        /// <param name="tag">情绪标签（可以是缩写或完整名称）</param>
        /// <returns>完整的情绪标签名称</returns>
        private string ExpandEmotionTag(string tag)
        {
            if (string.IsNullOrEmpty(tag))
            {
                return "neutral";
            }
            
            // 支持缩写映射
            return tag.ToLower() switch
            {
                "h" => "happy",
                "s" => "sad",
                "a" => "angry",
                "su" => "surprised",
                "w" => "worried",
                "c" => "confused",
                "n" => "neutral",
                "sm" => "smug",
                "sh" => "shy",
                _ => tag  // 已经是完整名称
            };
        }
        
        /// <summary>
        /// ⭐ v1.6.75: 多情绪序列播放（支持情绪分段切换）
        /// </summary>
        /// <param name="fullText">完整对话文本</param>
        /// <param name="emotionSequence">情绪序列</param>
        /// <param name="personaDefName">人格 DefName</param>
        private void AutoPlayTTSWithEmotionSequence(string fullText, List<TheSecondSeat.LLM.EmotionSegment> emotionSequence, string personaDefName)
        {
            try
            {
                // 检查是否启用 TTS
                var modSettings = LoadedModManager.GetMod<Settings.TheSecondSeatMod>()?.GetSettings<Settings.TheSecondSeatSettings>();
                
                if (modSettings == null || !modSettings.enableTTS || !modSettings.autoPlayTTS)
                {
                    return;
                }
                
                if (Prefs.DevMode)
                {
                    Log.Message($"[NarratorController] 多情绪序列播放: {emotionSequence.Count} 个片段");
                }
                
                // 清理完整文本
                string cleanText = System.Text.RegularExpressions.Regex.Replace(fullText, @"\([^)]*\)", "").Trim();
                
                if (string.IsNullOrEmpty(cleanText))
                {
                    return;
                }
                
                // 生成 TTS
                Messages.Message("正在生成语音...", MessageTypeDefOf.SilentInput);
                
                Task.Run(async () => 
                {
                    try
                    {
                        // 生成完整音频
                        string? audioPath = await TTS.TTSService.Instance.SpeakAsync(cleanText, personaDefName);
                        
                        if (!string.IsNullOrEmpty(audioPath))
                        {
                            Log.Message($"[NarratorController] TTS 生成成功: {audioPath}");
                            
                            // 在主线程安排情绪切换序列
                            Verse.LongEventHandler.ExecuteWhenFinished(() => 
                            {
                                try
                                {
                                    // 开始播放音频
                                    TTS.TTSAudioPlayer.Instance.PlayAndDelete(audioPath, personaDefName);
                                    
                                    // 安排情绪切换任务
                                    ScheduleEmotionSequence(personaDefName, emotionSequence);
                                    
                                    Messages.Message($"正在播放语音: {System.IO.Path.GetFileName(audioPath)}", MessageTypeDefOf.TaskCompletion);
                                }
                                catch (Exception playEx)
                                {
                                    Log.Error($"[NarratorController] 播放音频失败: {playEx.Message}");
                                }
                            });
                        }
                        else
                        {
                            Verse.LongEventHandler.ExecuteWhenFinished(() => 
                            {
                                Messages.Message("语音生成失败，请检查 TTS 配置", MessageTypeDefOf.RejectInput);
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Warning($"[NarratorController] TTS 生成失败: {ex.Message}");
                        
                        Verse.LongEventHandler.ExecuteWhenFinished(() => 
                        {
                            Messages.Message($"语音生成失败: {ex.Message}", MessageTypeDefOf.RejectInput);
                        });
                    }
                });
            }
            catch (Exception ex)
            {
                Log.Warning($"[NarratorController] AutoPlayTTSWithEmotionSequence 失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// ⭐ v1.6.75: 安排情绪切换序列（按时间延迟切换表情）
        /// </summary>
        /// <param name="personaDefName">人格 DefName</param>
        /// <param name="emotionSequence">情绪序列</param>
        private void ScheduleEmotionSequence(string personaDefName, List<TheSecondSeat.LLM.EmotionSegment> emotionSequence)
        {
            int accumulatedTicks = 0;
            
            foreach (var segment in emotionSequence)
            {
                // 估算片段时长
                int segmentDuration = segment.estimatedDuration > 0
                    ? (int)(segment.estimatedDuration * 60f)  // 使用 LLM 提供的时长
                    : EstimateTTSDuration(segment.text);      // 自动估算
                
                // 捕获变量用于闭包
                int delay = accumulatedTicks;
                string emotion = segment.emotion;
                int duration = segmentDuration;
                
                // 安排延迟任务
                Task.Run(async () =>
                {
                    // 等待指定时长
                    await Task.Delay(delay * 1000 / 60); // ticks 转毫秒
                    
                    // 在主线程切换表情
                    Verse.LongEventHandler.ExecuteWhenFinished(() =>
                    {
                        var emotionExpression = MapEmotionToExpression(emotion);
                        PersonaGeneration.ExpressionSystem.SetExpression(
                            personaDefName, 
                            emotionExpression, 
                            duration,
                            $"情绪序列: {emotion}"
                        );
                        
                        if (Prefs.DevMode)
                        {
                            Log.Message($"[NarratorController] 切换情绪: {emotion} → {emotionExpression}，持续 {duration / 60f:F1} 秒");
                        }
                    });
                });
                
                // 累积延迟
                accumulatedTicks += segmentDuration;
            }
        }
        /// <summary>
        /// 将叙事者消息伪装成系统消息发送
        /// </summary>
        private void SendAsSystemMessage(string message)
        {
            try
            {
                // 获取当前人格名称
                string narratorName = "叙事者";
                if (narratorManager != null)
                {
                    var persona = narratorManager.GetCurrentPersona();
                    if (persona != null)
                    {
                        narratorName = persona.narratorName;
                    }
                }
                
                // 格式化为系统消息风格
                string systemMessage = $"【{narratorName}】{message}";
                
                // 发送到游戏系统消息
                Messages.Message(systemMessage, MessageTypeDefOf.NeutralEvent);
                
                Log.Message($"[NarratorController] 系统消息已发送: {systemMessage}");
            }
            catch (Exception ex)
            {
                Log.Warning($"[NarratorController] 发送系统消息失败: {ex.Message}");
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

                // ? 8. 更新好感度
                if (narratorManager != null)
                {
                    float affinityChange = result.success ? 2f : -1f;
                    narratorManager.ModifyFavorability(
                        affinityChange,
                        $"命令 {llmCommand.action}: {result.message}"
                    );
                }

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

        private string GetDefaultSystemPrompt()
        {
            return @"你是Cassandra，AI叙事者。用JSON格式回复，包含'thought'、'dialogue'和可选的'command'字段。";
        }

        /// <summary>
        /// 解析 Agent 响应为 LLMResponse
        /// ✅ v1.6.66: 增强 JSON 清理，支持 API 响应格式（choices[].message.content）
        /// </summary>
        private TheSecondSeat.LLM.LLMResponse ParseAgentResponse(string response)
        {
            if (string.IsNullOrWhiteSpace(response))
            {
                Log.Warning("[NarratorController] 响应为空");
                return new TheSecondSeat.LLM.LLMResponse { dialogue = "[AI 没有响应]" };
            }

            try
            {
                // 清理响应（移除 Markdown 代码块）
                string cleanedResponse = response.Trim();
                if (cleanedResponse.StartsWith("```json"))
                {
                    cleanedResponse = cleanedResponse.Substring(7);
                }
                if (cleanedResponse.StartsWith("```"))
                {
                    cleanedResponse = cleanedResponse.Substring(3);
                }
                if (cleanedResponse.EndsWith("```"))
                {
                    cleanedResponse = cleanedResponse.Substring(0, cleanedResponse.Length - 3);
                }
                cleanedResponse = cleanedResponse.Trim();

                // ✅ 修复 1: 先尝试解析为 API 响应格式
                Newtonsoft.Json.Linq.JObject apiResponse = null;
                string content = null;

                try
                {
                    apiResponse = Newtonsoft.Json.Linq.JObject.Parse(cleanedResponse);
                    
                    // 提取 choices[0].message.content
                    content = apiResponse["choices"]?[0]?["message"]?["content"]?.ToString();
                    
                    if (string.IsNullOrWhiteSpace(content))
                    {
                        Log.Warning("[NarratorController] API 响应中没有 content 字段，尝试直接解析");
                        content = cleanedResponse; // 降级：直接使用原始响应
                    }
                    else
                    {
                        Log.Message($"[NarratorController] 成功提取 content 字段，长度: {content.Length}");
                    }
                }
                catch (Newtonsoft.Json.JsonException)
                {
                    // 不是 API 格式，直接使用原始响应
                    content = cleanedResponse;
                    Log.Message("[NarratorController] 不是 API 响应格式，直接使用原始内容");
                }

                // ✅ 修复 2: 尝试解析 content 为 LLMResponse
                try
                {
                    var llmResponse = Newtonsoft.Json.JsonConvert.DeserializeObject<TheSecondSeat.LLM.LLMResponse>(content);
                    if (llmResponse != null && !string.IsNullOrWhiteSpace(llmResponse.dialogue))
                    {
                        Log.Message($"[NarratorController] 成功解析 JSON: dialogue 长度 {llmResponse.dialogue.Length}");
                        return llmResponse;
                    }
                }
                catch (Newtonsoft.Json.JsonException jsonEx)
                {
                    Log.Warning($"[NarratorController] content 不是 JSON: {jsonEx.Message}，尝试直接使用");
                }

                // ✅ 修复 3: 降级处理 - 直接使用 content 作为 dialogue
                Log.Message("[NarratorController] 使用 content 作为纯文本 dialogue");
                return new TheSecondSeat.LLM.LLMResponse
                {
                    dialogue = content,
                    expression = null,
                    emotion = "neutral",
                    viseme = "Closed",
                    command = null
                };
            }
            catch (Exception ex)
            {
                Log.Error($"[NarratorController] 解析响应失败: {ex.Message}\n{ex.StackTrace}");
                return new TheSecondSeat.LLM.LLMResponse 
                { 
                    dialogue = "[AI 响应格式错误，请查看日志]" 
                };
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref lastDialogue, "lastDialogue", "");
            Scribe_Values.Look(ref hasGreetedOnLoad, "hasGreetedOnLoad", false);
            // ? 注意：不保存 ticksSinceLoad，每次加载都重新计时
        }
    }
}

namespace UnityEngine
{
    /// <summary>
    /// Helper to execute callbacks on Unity's main thread
    /// </summary>
    public static class ApplicationExtensions
    {
        public static void CallOnMainThread(Action action)
        {
            // RimWorld/Unity specific: use LongEventHandler for main thread execution
            Verse.LongEventHandler.ExecuteWhenFinished(action);
        }
    }
}


