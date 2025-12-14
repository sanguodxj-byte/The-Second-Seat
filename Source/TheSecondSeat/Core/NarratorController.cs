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

        private void ProcessResponse(LLMResponse response, string userMessage)
        {
            try
            {
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
                    tags: new List<string> { "AI回复", "叙述者互动", narratorName }
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

                // ? 自动播放 TTS（根据设置）
                AutoPlayTTS(displayText);

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
        /// ? 自动播放 TTS（叙事者发言时）
        /// ? 优化：添加加载状态提示
        /// </summary>
        private void AutoPlayTTS(string text)
        {
            try
            {
                // 检查是否启用 TTS
                var modSettings = LoadedModManager.GetMod<Settings.TheSecondSeatMod>()?.GetSettings<Settings.TheSecondSeatSettings>();
                
                if (modSettings == null || !modSettings.enableTTS)
                {
                    return; // TTS 未启用，跳过
                }
                
                // 清除动作标记（括号内的内容）
                string cleanText = System.Text.RegularExpressions.Regex.Replace(text, @"\([^)]*\)", "").Trim();
                
                if (string.IsNullOrEmpty(cleanText))
                {
                    return; // 没有实际文本，跳过
                }
                
                // ? 显示"语音生成中"提示
                Messages.Message("?? 语音生成中...", MessageTypeDefOf.SilentInput);
                
                // ? 异步生成并保存 TTS 音频
                Task.Run(async () => 
                {
                    try
                    {
                        string? audioPath = await TTS.TTSService.Instance.SpeakAsync(cleanText);
                        
                        if (!string.IsNullOrEmpty(audioPath))
                        {
                            Log.Message($"[NarratorController] TTS 音频已生成: {audioPath}");
                            
                            // ? 在主线程显示成功消息
                            Verse.LongEventHandler.ExecuteWhenFinished(() => 
                            {
                                Messages.Message($"? 语音已生成: {System.IO.Path.GetFileName(audioPath)}", MessageTypeDefOf.TaskCompletion);
                            });
                        }
                        else
                        {
                            // ? 生成失败提示
                            Verse.LongEventHandler.ExecuteWhenFinished(() => 
                            {
                                Messages.Message("? 语音生成失败（请检查网络连接）", MessageTypeDefOf.RejectInput);
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Warning($"[NarratorController] TTS 自动播放失败: {ex.Message}");
                        
                        // ? 显示错误提示
                        Verse.LongEventHandler.ExecuteWhenFinished(() => 
                        {
                            Messages.Message($"? 语音生成错误: {ex.Message}", MessageTypeDefOf.RejectInput);
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
        /// ? 将叙事者消息伪装成系统消息发送
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
