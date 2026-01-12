using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using TheSecondSeat.Commands;
using TheSecondSeat.LLM;
using TheSecondSeat.Narrator;
using TheSecondSeat.Monitoring;
using TheSecondSeat.NaturalLanguage;
using TheSecondSeat.Execution;
using TheSecondSeat.Integration;
using TheSecondSeat.WebSearch;
using TheSecondSeat.PersonaGeneration;
using TheSecondSeat.Utils;
using TheSecondSeat.RimAgent.Tools;
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
        
        // ⭐ v1.6.82: 主线程纹理预加载标记
        private bool hasPreloadedAssets = false;

        // ? 自动错误检测
        private int ticksSinceLastErrorCheck = 0;
        private const int ErrorCheckInterval = 300; // 5秒
        private string lastHandledError = "";

        public string LastDialogue => lastDialogue;
        public bool IsProcessing => isProcessing;
        public string LastError => lastError;
        
        /// <summary>
        /// 获取当前叙事者人格的 defName（静态属性，供外部访问）
        /// </summary>
        public static string? CurrentPersonaDefName
        {
            get
            {
                try
                {
                    var controller = Current.Game?.GetComponent<NarratorController>();
                    if (controller?.narratorManager != null)
                    {
                        var persona = controller.narratorManager.GetCurrentPersona();
                        return persona?.defName;
                    }
                }
                catch { }
                return null;
            }
        }

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
            
            // ⭐ v1.6.82: 主线程纹理预加载（只执行一次，在问候之前）
            if (!hasPreloadedAssets && narratorManager != null)
            {
                hasPreloadedAssets = true;
                PreloadAssetsOnMainThread();
            }

            // ? v1.6.82: 处理调度的表情切换
            ProcessScheduledExpressions();
            
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

            // ? 自动错误检测与修复循环
            // 每 5 秒检查一次是否有新的红字错误
            ticksSinceLastErrorCheck++;
            if (ticksSinceLastErrorCheck >= ErrorCheckInterval)
            {
                ticksSinceLastErrorCheck = 0;
                CheckForRuntimeErrors();
            }
        }

        /// <summary>
        /// ? 检查运行时错误并触发 AI 自动修复
        /// </summary>
        private void CheckForRuntimeErrors()
        {
            // 如果 AI 正在处理中，跳过本次检查，避免打断
            if (isProcessing) return;

            // 检查 LogAnalysisTool 是否捕获到新错误
            string currentError = LogAnalysisTool.LastErrorMessage;

            // 如果有错误，且该错误未被处理过（或者是新的错误内容）
            if (!string.IsNullOrEmpty(currentError) && currentError != lastHandledError)
            {
                lastHandledError = currentError;
                
                // ? 只有在开发者模式或特定设置下才启用自动修复建议
                // 这里我们假设如果安装了这个 Mod，用户就期望有这个功能
                // 但为了不打扰正常游戏，我们只针对看起来像 XML 配置错误的报错进行积极干预
                // 或者我们可以总是提示，让 AI 决定是否值得打扰玩家

                Log.Message($"[NarratorController] 自动检测到新错误，正在唤醒 AI 工程师: {currentError}");

                // 构建系统警报消息
                // 引导 AI 使用 analyze_last_error 工具
                string alertMessage = $"[SYSTEM ALERT] A runtime error has been detected: \"{currentError}\". " +
                                      "Please use the 'analyze_last_error' tool to investigate the cause. " +
                                      "If it looks like a configuration typo (e.g. in XML), try to fix it using 'patch_file'. " +
                                      "If you cannot fix it, briefly explain the issue to the player.";

                // 触发 AI 更新，传入警报消息
                // 这将启动 ReAct 循环
                TriggerNarratorUpdate(alertMessage);
            }
        }
        
        /// <summary>
        /// ⭐ v1.6.82: 在主线程预加载所有纹理资源
        /// 避免首次显示时的卡顿
        /// </summary>
        private void PreloadAssetsOnMainThread()
        {
            try
            {
                // 初始化主线程 ID
                TSS_AssetLoader.InitializeMainThread();
                
                // 获取所有已加载的叙事者人格
                var allPersonas = DefDatabase<NarratorPersonaDef>.AllDefsListForReading;
                int preloadedCount = 0;
                
                foreach (var persona in allPersonas)
                {
                    if (persona == null) continue;
                    
                    // 预加载立绘
                    if (!string.IsNullOrEmpty(persona.portraitPath))
                    {
                        TSS_AssetLoader.LoadTexture(persona.portraitPath);
                    }
                    
                    // 预加载分层立绘配置
                    if (persona.useLayeredPortrait)
                    {
                        var config = persona.GetLayeredConfig();
                        if (config != null)
                        {
                            // 预加载所有表情的 base_body
                            LayeredPortraitCompositor.PreloadAllExpressions(config);
                        }
                    }
                    
                    // 预加载降临姿态
                    if (persona.hasDescentMode)
                    {
                        string personaName = persona.narratorName?.Split(' ')[0] ?? persona.defName;
                        
                        if (persona.descentPostures != null)
                        {
                            if (!string.IsNullOrEmpty(persona.descentPostures.standing))
                            {
                                TSS_AssetLoader.LoadDescentPosture(personaName, persona.descentPostures.standing);
                            }
                            if (!string.IsNullOrEmpty(persona.descentPostures.floating))
                            {
                                TSS_AssetLoader.LoadDescentPosture(personaName, persona.descentPostures.floating);
                            }
                            if (!string.IsNullOrEmpty(persona.descentPostures.combat))
                            {
                                TSS_AssetLoader.LoadDescentPosture(personaName, persona.descentPostures.combat);
                            }
                        }
                    }
                    
                    preloadedCount++;
                }
                
                if (Prefs.DevMode)
                {
                    Log.Message($"[NarratorController] ⭐ 主线程预加载完成: {preloadedCount} 个叙事者人格");
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[NarratorController] 预加载资源失败: {ex.Message}");
            }
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
                    PersonaGeneration.ExpressionSystem.SetThinkingExpression(persona.defName);
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[NarratorController] 设置思考表情失败: {ex.Message}");
            }

            // 2. 然后在后台线程处理（不访问游戏数据）
            Task.Run(async () => await ProcessNarratorUpdateAsync(userMessage, gameStateJson, selectionContext, isGreeting));
        }

        private async Task ProcessNarratorUpdateAsync(string userMessage, string gameStateJson, string selectionContext, bool isGreeting = false)
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
                    // 包含时间、搜索结果、选中物体上下文
                    enhancedUserMessage = timeContext + searchContext + selectionContext + userMessage;
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
        
        // ? 默认 System Prompt
        private string GetDefaultSystemPrompt()
        {
            return "你是一个 RimWorld 的 AI 叙事者。你的任务是观察游戏状态，与玩家互动，并根据需要执行操作。";
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
                    ApplyExpressionFromResponse(response, narratorDefName, displayText);
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
                
                // ? 准备流式消息（但先不显示，等待 TTS 或超时）
                UI.DialogueOverlayPanel.SetStreamingMessage($"【{narratorName}】{displayText}");
                
                Log.Message($"[NarratorController] AI says: {displayText}");

                // ? 自动播放 TTS（根据设置）
                // 如果 TTS 禁用，将立即触发流式显示
                // ? 传递 emoticonId 以便在显示消息时使用
                AutoPlayTTS(displayText, emoticonId);

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
        /// ⭐ 自动播放 TTS（叙事者发言时）
        /// ⭐ v1.6.90: 修复 TTS 与对话同步 - 对话框等待 TTS 加载完成后同时开始
        /// - 启用 TTS 时：由 TTSAudioPlayer 在音频开始播放时触发 StartStreaming(clip.length)
        /// - 禁用 TTS 时：使用估算时长立即开始流式显示
        /// </summary>
        private void AutoPlayTTS(string text, string emoticonId = "")
        {
            try
            {
                // 清除动作标记（括号内的内容）- 提前计算以便估算时长
                string cleanText = System.Text.RegularExpressions.Regex.Replace(text, @"\([^)]*\)", "").Trim();
                
                // ⭐ 估算文本阅读时长（中文约每秒5个字符，最少2秒）
                float estimatedDuration = Math.Max(2f, cleanText.Length / 5f);
                
                // 检查是否启用 TTS
                var modSettings = LoadedModManager.GetMod<Settings.TheSecondSeatMod>()?.GetSettings<Settings.TheSecondSeatSettings>();
                
                if (modSettings == null || !modSettings.enableTTS || !modSettings.autoPlayTTS)
                {
                    // ⭐ TTS 未启用，使用估算时长开始流式显示
                    Log.Message($"[NarratorController] TTS disabled. Streaming with estimated duration: {estimatedDuration:F2}s");
                    UI.NarratorWindow.AddAIMessage(text, emoticonId); // 立即显示
                    UI.DialogueOverlayPanel.StartStreaming(estimatedDuration);
                    return;
                }
                
                if (string.IsNullOrEmpty(cleanText))
                {
                    // 没有语音内容，使用最短时长显示
                    UI.NarratorWindow.AddAIMessage(text, emoticonId); // 立即显示
                    UI.DialogueOverlayPanel.StartStreaming(1f);
                    return;
                }
                
                // 获取当前叙事者 defName
                string personaDefName = "Cassandra_Classic";
                if (narratorManager != null)
                {
                    var persona = narratorManager.GetCurrentPersona();
                    if (persona != null)
                    {
                        personaDefName = persona.defName;
                    }
                }
                
                // 显示加载提示
                Messages.Message("正在生成语音...", MessageTypeDefOf.SilentInput);
                
                // ⭐ 在后台线程生成 TTS 音频
                // 注意：此时不调用 StartStreaming！等待 TTSAudioPlayer 在音频播放时触发
                Task.Run(async () =>
                {
                    try
                    {
                        string? audioPath = await TTS.TTSService.Instance.SpeakAsync(cleanText, personaDefName);
                        
                        if (!string.IsNullOrEmpty(audioPath))
                        {
                            Log.Message($"[NarratorController] TTS 音频生成完成: {audioPath} (Persona: {personaDefName})");
                            
                            // ⭐ 在主线程播放音频
                            // TTSAudioPlayer.PlayAndDelete 内部会在音频开始播放时调用 StartStreaming(clip.length)
                            Verse.LongEventHandler.ExecuteWhenFinished(() =>
                            {
                                try
                                {
                                    // ⭐ 播放音频 - TTSAudioPlayer 会自动触发 StartStreaming(clip.length)
                                    UI.NarratorWindow.AddAIMessage(text, emoticonId); // 播放时显示
                                    TTS.TTSAudioPlayer.Instance.PlayAndDelete(audioPath, personaDefName);
                                }
                                catch (Exception playEx)
                                {
                                    Log.Error($"[NarratorController] TTS playback failed: {playEx.Message}");
                                    // ⭐ 出错时使用估算时长回退
                                    float fallbackDuration = Math.Max(2f, cleanText.Length / 5f);
                                    Log.Warning($"[NarratorController] TTS playback failed. Falling back to estimated duration: {fallbackDuration:F2}s");
                                    UI.NarratorWindow.AddAIMessage(text, emoticonId); // 回退时显示
                                    UI.DialogueOverlayPanel.StartStreaming(fallbackDuration);
                                }
                            });
                        }
                        else
                        {
                            // ⭐ TTS 生成失败，使用估算时长回退
                            Verse.LongEventHandler.ExecuteWhenFinished(() =>
                            {
                                Log.Warning("[NarratorController] TTS audio generation returned null. Falling back to estimated duration.");
                                float fallbackDuration = Math.Max(2f, cleanText.Length / 5f);
                                UI.NarratorWindow.AddAIMessage(text, emoticonId); // 回退时显示
                                UI.DialogueOverlayPanel.StartStreaming(fallbackDuration);
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"[NarratorController] TTS processing task failed: {ex.Message}");
                        
                        // ⭐ TTS 异常，使用估算时长回退
                        Verse.LongEventHandler.ExecuteWhenFinished(() =>
                        {
                            float fallbackDuration = Math.Max(2f, cleanText.Length / 5f);
                            Log.Warning($"[NarratorController] TTS task failed. Falling back to estimated duration: {fallbackDuration:F2}s");
                            UI.NarratorWindow.AddAIMessage(text, emoticonId); // 回退时显示
                            UI.DialogueOverlayPanel.StartStreaming(fallbackDuration);
                        });
                    }
                });
            }
            catch (Exception ex)
            {
                Log.Warning($"[NarratorController] AutoPlayTTS 异常: {ex.Message}");
                // ⭐ 最外层异常使用最小时长回退
                UI.NarratorWindow.AddAIMessage(text, emoticonId); // 异常时显示
                UI.DialogueOverlayPanel.StartStreaming(2f);
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

        /// <summary>
        /// ? v1.6.82: 应用表情 - 支持单表情和多表情序列
        /// </summary>
        private void ApplyExpressionFromResponse(LLMResponse response, string narratorDefName, string dialogueText)
        {
            // 优先级: emotionSequence > emotions > expression > 自动推断
            
            // 1. 检查详细情绪序列 (emotionSequence)
            if (response.emotionSequence != null && response.emotionSequence.Count > 0)
            {
                ApplyEmotionSequence(response.emotionSequence, narratorDefName);
                return;
            }
            
            // 2. 检查紧凑情绪序列 (emotions: "happy|sad|angry")
            if (!string.IsNullOrEmpty(response.emotions))
            {
                ApplyCompactEmotionSequence(response.emotions, narratorDefName, dialogueText);
                return;
            }
            
            // 3. 单表情模式 (expression)
            if (!string.IsNullOrEmpty(response.expression))
            {
                // ? 修复：如果 expression 包含管道符，视为紧凑序列处理
                if (response.expression.Contains("|"))
                {
                    Log.Warning($"[NarratorController] 检测到 expression 字段包含多个表情 ({response.expression})，自动转为序列处理");
                    ApplyCompactEmotionSequence(response.expression, narratorDefName, dialogueText);
                    return;
                }

                // 使用 EmotionParser 解析
                var (expressionType, intensity) = PersonaGeneration.EmotionParser.Parse(response.expression);

                if (expressionType != PersonaGeneration.ExpressionType.Neutral || intensity > 0)
                {
                    PersonaGeneration.ExpressionSystem.SetExpression(
                        narratorDefName,
                        expressionType,
                        180,  // 3 秒
                        "对话触发",
                        intensity // 传递解析出的强度
                    );
                    Log.Message($"[NarratorController] AI 表情切换: {expressionType} (强度: {intensity})");
                }
                else
                {
                    Log.Warning($"[NarratorController] 解析表情失败或为中性: {response.expression}");
                    // 回退到自动推断
                    PersonaGeneration.ExpressionSystem.UpdateExpressionByDialogueTone(narratorDefName, dialogueText);
                }
                return;
            }
            
            // 4. 没有提供表情，自动推断
            PersonaGeneration.ExpressionSystem.UpdateExpressionByDialogueTone(narratorDefName, dialogueText);
        }
        
        /// <summary>
        /// ? v1.6.82: 应用详细情绪序列
        /// </summary>
        private void ApplyEmotionSequence(List<EmotionSegment> segments, string narratorDefName)
        {
            Log.Message($"[NarratorController] 应用情绪序列: {segments.Count} 个片段");
            
            // 计算总时长
            float totalDuration = 0f;
            foreach (var segment in segments)
            {
                // 如果没有指定时长，按文本长度估算 (中文约 3字/秒)
                float segmentDuration = segment.estimatedDuration > 0
                    ? segment.estimatedDuration
                    : segment.text.Length / 3f;
                totalDuration += segmentDuration;
            }
            
            // 创建延迟表情切换任务
            float accumulatedDelay = 0f;
            for (int i = 0; i < segments.Count; i++)
            {
                var segment = segments[i];
                float delay = accumulatedDelay;
                
                // 解析情绪标签
                var (expressionType, intensity) = PersonaGeneration.EmotionParser.Parse(segment.emotion);
                
                if (expressionType != PersonaGeneration.ExpressionType.Neutral || intensity > 0)
                {
                    // 使用延迟任务设置表情
                    int delayTicks = (int)(delay * 60); // 秒转换为 ticks
                    float segmentDuration = segment.estimatedDuration > 0
                        ? segment.estimatedDuration
                        : segment.text.Length / 3f;
                    int durationTicks = (int)(segmentDuration * 60);
                    
                    // 记录延迟执行
                    ScheduleExpressionChange(narratorDefName, expressionType, delayTicks, durationTicks, intensity);
                    
                    accumulatedDelay += segmentDuration;
                }
            }
        }
        
        /// <summary>
        /// ? v1.6.82: 应用紧凑情绪序列 (emotions: "happy|sad|angry")
        /// </summary>
        private void ApplyCompactEmotionSequence(string emotionsStr, string narratorDefName, string dialogueText)
        {
            string[] emotions = emotionsStr.Split('|');
            Log.Message($"[NarratorController] 应用紧凑情绪序列: {emotionsStr} ({emotions.Length} 个)");
            
            // 按句子分割对话（按标点）
            string[] sentences = System.Text.RegularExpressions.Regex.Split(dialogueText, @"(?<=[。！？\.!\?])");
            sentences = System.Array.FindAll(sentences, s => !string.IsNullOrWhiteSpace(s));
            
            // 计算每个情绪的持续时间
            float totalChars = dialogueText.Length;
            float charsPerSecond = 3f; // 中文约 3字/秒
            float totalDuration = totalChars / charsPerSecond;
            float durationPerEmotion = totalDuration / emotions.Length;
            
            // 设置第一个表情（立即）
            if (emotions.Length > 0)
            {
                var (firstExpressionType, firstIntensity) = PersonaGeneration.EmotionParser.Parse(emotions[0].Trim());
                
                if (firstExpressionType != PersonaGeneration.ExpressionType.Neutral || firstIntensity > 0)
                {
                    int firstDuration = (int)(durationPerEmotion * 60);
                    PersonaGeneration.ExpressionSystem.SetExpression(
                        narratorDefName,
                        firstExpressionType,
                        firstDuration,
                        "情绪序列-1",
                        firstIntensity
                    );
                    Log.Message($"[NarratorController] 情绪 1/{emotions.Length}: {firstExpressionType} (强度: {firstIntensity}) (立即)");
                }
            }
            
            // 调度后续表情切换
            for (int i = 1; i < emotions.Length; i++)
            {
                var (expressionType, intensity) = PersonaGeneration.EmotionParser.Parse(emotions[i].Trim());
                
                if (expressionType != PersonaGeneration.ExpressionType.Neutral || intensity > 0)
                {
                    // 延迟时间 = 前面所有表情的持续时间总和
                    int delayTicks = (int)(i * durationPerEmotion * 60);
                    int durationTicks = (int)(durationPerEmotion * 60);
                    
                    ScheduleExpressionChange(narratorDefName, expressionType, delayTicks, durationTicks, intensity);
                    Log.Message($"[NarratorController] 情绪 {i+1}/{emotions.Length}: {expressionType} (强度: {intensity}) (延迟 {delayTicks} ticks)");
                }
            }
        }
        
        // ? 表情调度系统
        
        private class ScheduledExpression
        {
            public string narratorDefName;
            public PersonaGeneration.ExpressionType expression;
            public int triggerTick; // 触发的游戏 tick
            public int durationTicks;
            public int intensity; // 新增
        }
        
        private List<ScheduledExpression> scheduledExpressions = new List<ScheduledExpression>();
        
        /// <summary>
        /// ? 调度一个延迟的表情切换
        /// </summary>
        private void ScheduleExpressionChange(string narratorDefName, PersonaGeneration.ExpressionType expression, int delayTicks, int durationTicks, int intensity = 0)
        {
            int currentTick = GenTicks.TicksGame;
            scheduledExpressions.Add(new ScheduledExpression
            {
                narratorDefName = narratorDefName,
                expression = expression,
                triggerTick = currentTick + delayTicks,
                durationTicks = durationTicks,
                intensity = intensity
            });
        }
        
        /// <summary>
        /// ? 在 GameComponentTick 中调用，处理到期的表情切换
        /// </summary>
        private void ProcessScheduledExpressions()
        {
            if (scheduledExpressions.Count == 0) return;
            
            int currentTick = GenTicks.TicksGame;
            
            // 使用倒序循环避免额外的 List 分配
            for (int i = scheduledExpressions.Count - 1; i >= 0; i--)
            {
                var item = scheduledExpressions[i];
                if (item.triggerTick <= currentTick)
                {
                    // 应用表情
                    PersonaGeneration.ExpressionSystem.SetExpression(
                        item.narratorDefName,
                        item.expression,
                        item.durationTicks,
                        "定时情绪序列",
                        item.intensity // 传递 intensity
                    );
                    
                    // 从列表中移除
                    scheduledExpressions.RemoveAt(i);
                }
            }
        }
    }
}
