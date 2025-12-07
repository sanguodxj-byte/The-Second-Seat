using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using RimWorld;
using TheSecondSeat.Storyteller;
using TheSecondSeat.PersonaGeneration;
using UnityEngine;

namespace TheSecondSeat.Narrator
{
    /// <summary>
    /// Manages the AI narrator's favorability and state
    /// ? 好感度范围：-1000 (憎恨) 到 +1000 (爱慕/魂之友/主)
    /// ? 起始位置：0 (冷淡)
    /// </summary>
    public class NarratorManager : GameComponent
    {
        // ? 好感度系统变量
        private float favorability = 0f; // -1000 (仇恨) 到 +1000 (挚爱/灵魂绑定/契)
        private List<FavorabilityEvent> recentEvents = new List<FavorabilityEvent>();
        private const int MaxRecentEvents = 20;

        // Storyteller agent
        private StorytellerAgent? storytellerAgent;

        // 当前人格相关
        private NarratorPersonaDef? currentPersonaDef;
        private PersonaAnalysisResult? currentAnalysis;
        
        // ? 新增：用于存档保存的人格 defName
        private string savedPersonaDefName = "Cassandra_Classic";
        
        // ? 新增：自动问候标记
        private bool hasGreetedThisSession = false;
        private int ticksSinceLoad = 0;
        private const int GreetingDelayTicks = 60; // 1秒后问候（60 ticks）

        public float Favorability => favorability;
        public List<FavorabilityEvent> RecentEvents => recentEvents;

        public NarratorManager(Game game) : base()
        {
            // ? 初始好感度为 0（冷淡）
            favorability = 0f;
            InitializeDefaultPersona();
        }

        /// <summary>
        /// 初始化默认人格（Cassandra Classic）
        /// </summary>
        private void InitializeDefaultPersona()
        {
            var cassandraDef = DefDatabase<NarratorPersonaDef>.GetNamedSilentFail("Cassandra_Classic");
            if (cassandraDef != null)
            {
                LoadPersona(cassandraDef);
            }
            else
            {
                storytellerAgent = new StorytellerAgent();
                Log.Warning("[NarratorManager] Cassandra_Classic 人格定义未找到，使用默认配置");
            }
        }

        /// <summary>
        /// 加载指定人格
        /// </summary>
        public void LoadPersona(NarratorPersonaDef personaDef)
        {
            currentPersonaDef = personaDef;
            currentAnalysis = PersonaAnalyzer.AnalyzePersonaDef(personaDef);
            
            // ? 保存 defName 用于存档
            savedPersonaDefName = personaDef.defName;
            
            if (storytellerAgent == null)
            {
                storytellerAgent = new StorytellerAgent();
            }

            storytellerAgent.name = personaDef.narratorName;
            storytellerAgent.primaryTrait = currentAnalysis.SuggestedPersonality ?? PersonalityTrait.Strategic;
            
            // 复制对话风格从人格分析结果
            if (currentAnalysis.DialogueStyle != null)
            {
                storytellerAgent.dialogueStyle = currentAnalysis.DialogueStyle;
            }
            
            // ? 设置初始好感度（-500 ~ 500）
            if (personaDef.baseAffinityBias != 0f)
            {
                favorability = personaDef.baseAffinityBias * 500f;
                
                // ? 转换到 StorytellerAgent 的 -100~100 范围
                float normalizedAffinity = favorability / 10f;
                storytellerAgent.affinity = normalizedAffinity;
            }
            
            // ? **关键修复**：立即根据好感度调整对话风格
            storytellerAgent.AdjustDialogueStyleByAffinity();

            Log.Message($"[NarratorManager] 已加载人格: {personaDef.narratorName} ({storytellerAgent.primaryTrait}), 好感度: {favorability:F0}, 对话风格已调整");
        }

        /// <summary>
        /// 获取所有可用的人格定义
        /// </summary>
        public static List<NarratorPersonaDef> GetAllPersonas()
        {
            return DefDatabase<NarratorPersonaDef>.AllDefsListForReading;
        }

        /// <summary>
        /// 切换人格
        /// </summary>
        public void SwitchPersona(string defName)
        {
            var personaDef = DefDatabase<NarratorPersonaDef>.GetNamedSilentFail(defName);
            if (personaDef != null)
            {
                LoadPersona(personaDef);
                Messages.Message($"叙事者已切换为：{personaDef.narratorName}", MessageTypeDefOf.PositiveEvent);
            }
            else
            {
                Log.Error($"[NarratorManager] 未找到人格定义: {defName}");
            }
        }

        /// <summary>
        /// ? 修改好感度（-1000 到 +1000）
        /// </summary>
        public void ModifyFavorability(float amount, string reason)
        {
            float oldValue = favorability;
            favorability = Mathf.Clamp(favorability + amount, -1000f, 1000f);

            recentEvents.Add(new FavorabilityEvent
            {
                timestamp = Find.TickManager.TicksGame,
                change = amount,
                reason = reason,
                newValue = favorability
            });

            if (recentEvents.Count > MaxRecentEvents)
            {
                recentEvents.RemoveAt(0);
            }

            // ? 更新 StorytellerAgent（转换到 -100~100 范围）
            if (storytellerAgent != null)
            {
                float normalizedAmount = amount / 10f; // -1000~1000 → -100~100
                storytellerAgent.ModifyAffinity(normalizedAmount, reason);
            }

            Log.Message($"[NarratorManager] Favorability: {oldValue:F0} -> {favorability:F0} ({reason})");
        }

        public string GetRecentEventsSummary()
        {
            if (recentEvents.Count == 0)
                return "No recent events.";

            int skip = Math.Max(0, recentEvents.Count - 5);
            return string.Join("\n", recentEvents.Skip(skip).Select(e =>
                $"[{e.GetTimeAgo()}] {e.change:+0;-0} - {e.reason}"
            ));
        }

        /// <summary>
        /// ? 重新定义好感度等级（移除"中立"）
        /// </summary>
        public AffinityTier CurrentTier
        {
            get
            {
                if (favorability >= 850f) return AffinityTier.SoulBound;      // 魂之友/主 (850~1000)
                if (favorability >= 600f) return AffinityTier.Adoration;      // 爱慕 (600~849)
                if (favorability >= 300f) return AffinityTier.Devoted;        // 倾心 (300~599)
                if (favorability >= 100f) return AffinityTier.Warm;           // 温暖 (100~299)
                if (favorability >= -100f) return AffinityTier.Indifferent;   // 冷淡 (-100~99)
                if (favorability >= -400f) return AffinityTier.Cold;          // 疏远 (-400~-101)
                if (favorability >= -700f) return AffinityTier.Hostile;       // 敌意 (-700~-401)
                return AffinityTier.Hatred;                                   // 憎恨 (-1000~-701)
            }
        }

        /// <summary>
        /// 获取动态 System Prompt（基于当前人格）
        /// ? 支持精简模式以加快响应速度
        /// </summary>
        public string GetDynamicSystemPrompt()
        {
            if (currentPersonaDef == null || currentAnalysis == null || storytellerAgent == null)
            {
                return GetFallbackSystemPrompt();
            }

            // ? 获取设置
            var modSettings = LoadedModManager.GetMod<Settings.TheSecondSeatMod>()?
                .GetSettings<Settings.TheSecondSeatSettings>();
            var difficultyMode = modSettings?.difficultyMode ?? PersonaGeneration.AIDifficultyMode.Assistant;
            bool useCompact = modSettings?.useCompactPrompt ?? true;  // 默认使用精简版

            // ? 根据设置选择精简版或完整版
            if (useCompact)
            {
                return SystemPromptGenerator.GenerateCompactSystemPrompt(
                    currentPersonaDef,
                    currentAnalysis,
                    storytellerAgent,
                    difficultyMode
                );
            }
            else
            {
                return SystemPromptGenerator.GenerateSystemPrompt(
                    currentPersonaDef,
                    currentAnalysis,
                    storytellerAgent,
                    difficultyMode
                );
            }
        }

        /// <summary>
        /// 获取紧凑版 System Prompt（节省 Token）
        /// </summary>
        public string GetCompactSystemPrompt()
        {
            if (currentPersonaDef == null || storytellerAgent == null)
            {
                return GetFallbackSystemPrompt();
            }

            return SystemPromptGenerator.GenerateCompactPrompt(
                currentPersonaDef,
                storytellerAgent
            );
        }

        private string GetFallbackSystemPrompt()
        {
            return @"You are a transcendent consciousness observing this rimworld colony.
You manifest as Cassandra, the strategic storyteller.
Respond in JSON: {""dialogue"": ""..."", ""command"": {...}}";
        }

        public StorytellerAgent GetStorytellerAgent()
        {
            if (storytellerAgent == null)
            {
                storytellerAgent = new StorytellerAgent();
            }
            return storytellerAgent;
        }

        public NarratorPersonaDef? GetCurrentPersona()
        {
            return currentPersonaDef;
        }

        public PersonaAnalysisResult? GetCurrentAnalysis()
        {
            return currentAnalysis;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref favorability, "favorability", 0f);
            
            if (Scribe.mode == LoadSaveMode.LoadingVars && recentEvents == null)
            {
                recentEvents = new List<FavorabilityEvent>();
            }
            
            Scribe_Collections.Look(ref recentEvents, "recentEvents", LookMode.Deep);
            
            if (recentEvents == null)
            {
                recentEvents = new List<FavorabilityEvent>();
            }
            
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                if (storytellerAgent == null)
                {
                    storytellerAgent = new StorytellerAgent();
                }
                
                // ? 保存前确保 defName 是最新的
                if (currentPersonaDef != null)
                {
                    savedPersonaDefName = currentPersonaDef.defName;
                }
            }
            
            Scribe_Deep.Look(ref storytellerAgent, "storytellerAgent");
            
            if (Scribe.mode == LoadSaveMode.LoadingVars || Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (storytellerAgent == null)
                {
                    Log.Warning("[NarratorManager] storytellerAgent 为 null，创建默认实例");
                    storytellerAgent = new StorytellerAgent();
                }
            }
            
            // ? 使用成员变量保存人格 defName
            Scribe_Values.Look(ref savedPersonaDefName, "currentPersonaDefName", "Cassandra_Classic");
            
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                // 加载时，根据保存的 defName 恢复人格
                Log.Message($"[NarratorManager] 正在加载人格: {savedPersonaDefName}");
                
                if (!string.IsNullOrEmpty(savedPersonaDefName))
                {
                    var personaDef = DefDatabase<NarratorPersonaDef>.GetNamedSilentFail(savedPersonaDefName);
                    if (personaDef != null)
                    {
                        // ? 直接加载，不使用 LongEventHandler（可能导致时机问题）
                        LoadPersona(personaDef);
                        Log.Message($"[NarratorManager] ? 成功恢复人格: {personaDef.narratorName}");
                    }
                    else
                    {
                        Log.Warning($"[NarratorManager] ? 未找到人格 {savedPersonaDefName}，使用默认人格");
                        InitializeDefaultPersona();
                    }
                }
                else
                {
                    Log.Warning("[NarratorManager] savedPersonaDefName 为空，使用默认人格");
                    InitializeDefaultPersona();
                }
                
                // ? **关键修复**：加载存档后，根据好感度调整对话风格
                if (storytellerAgent != null)
                {
                    storytellerAgent.AdjustDialogueStyleByAffinity();
                }
                
                // ? 重置自动问候标记，允许自动问候
                hasGreetedThisSession = false;
                ticksSinceLoad = 0;
            }
        }
        
        /// <summary>
        /// ? 新增：GameComponent Tick - 用于自动问候和表情系统更新
        /// </summary>
        public override void GameComponentTick()
        {
            base.GameComponentTick();
            
            // ? 新增：更新当前人格的表情过渡
            if (currentPersonaDef != null)
            {
                ExpressionSystem.UpdateTransition(currentPersonaDef.defName);
            }
            
            // 检查是否需要自动问候
            if (!hasGreetedThisSession && Current.ProgramState == ProgramState.Playing)
            {
                ticksSinceLoad++;
                
                if (ticksSinceLoad >= GreetingDelayTicks)
                {
                    hasGreetedThisSession = true;
                    TriggerAutoGreeting();
                }
            }
        }
        
        /// <summary>
        /// ? 触发自动问候（带日志错误检查）
        /// </summary>
        private void TriggerAutoGreeting()
        {
            try
            {
                var controller = Current.Game?.GetComponent<Core.NarratorController>();
                if (controller != null && !controller.IsProcessing)
                {
                    // 构建问候消息（带时间上下文）
                    DateTime now = DateTime.Now;
                    
                    // ? 新增：检查日志错误
                    var logErrors = CheckRecentLogErrors();
                    
                    string greetingContext = BuildGreetingContext(now, logErrors);
                    
                    Log.Message($"[NarratorManager] 触发自动问候（时间：{now:HH:mm}，错误数：{logErrors.Count}）");
                    
                    // 触发 AI 问候
                    controller.TriggerNarratorUpdate(greetingContext);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[NarratorManager] 自动问候失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// ? 新增：检查最近的日志错误
        /// </summary>
        private List<LogError> CheckRecentLogErrors()
        {
            var errors = new List<LogError>();
            
            try
            {
                // 使用反射获取 RimWorld 日志消息队列
                var logType = typeof(Log);
                var messageQueueField = logType.GetField("messageQueue", 
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
                
                if (messageQueueField == null)
                {
                    Log.Warning("[NarratorManager] 无法访问日志队列");
                    return errors;
                }
                
                var messageQueue = messageQueueField.GetValue(null);
                if (messageQueue == null)
                    return errors;
                
                // 尝试获取消息列表
                var messagesProperty = messageQueue.GetType().GetProperty("Messages");
                if (messagesProperty == null)
                {
                    // 尝试其他方式：直接转为 IEnumerable
                    var enumerableQueue = messageQueue as System.Collections.IEnumerable;
                    if (enumerableQueue == null)
                        return errors;
                    
                    var logMessages = new List<object>();
                    foreach (var item in enumerableQueue)
                    {
                        logMessages.Add(item);
                    }
                    
                    ProcessLogMessages(logMessages, errors);
                }
                else
                {
                    var messages = messagesProperty.GetValue(messageQueue) as System.Collections.IList;
                    if (messages != null)
                    {
                        var logMessages = new List<object>();
                        foreach (var item in messages)
                        {
                            logMessages.Add(item);
                        }
                        ProcessLogMessages(logMessages, errors);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[NarratorManager] 检查日志错误时出错: {ex.Message}");
            }
            
            return errors;
        }
        
        /// <summary>
        /// ? 处理日志消息列表
        /// </summary>
        private void ProcessLogMessages(List<object> logMessages, List<LogError> errors)
        {
            if (logMessages.Count == 0)
                return;
            
            // 只检查最近的 50 条日志
            int startIndex = Math.Max(0, logMessages.Count - 50);
            
            for (int i = startIndex; i < logMessages.Count; i++)
            {
                var msg = logMessages[i];
                if (msg == null) continue;
                
                // 使用反射获取消息类型和内容
                var msgType = msg.GetType();
                var typeProperty = msgType.GetProperty("type") ?? msgType.GetField("type")?.GetValue(msg) as System.Reflection.PropertyInfo;
                var textProperty = msgType.GetProperty("text") ?? msgType.GetField("text")?.GetValue(msg) as System.Reflection.PropertyInfo;
                
                // 尝试直接访问字段
                var typeField = msgType.GetField("type");
                var textField = msgType.GetField("text");
                var stackTraceField = msgType.GetField("stackTrace");
                
                object? typeValue = null;
                string? textValue = null;
                string? stackTraceValue = null;
                
                if (typeField != null)
                    typeValue = typeField.GetValue(msg);
                else if (typeProperty != null)
                    typeValue = ((System.Reflection.PropertyInfo)typeProperty).GetValue(msg);
                
                if (textField != null)
                    textValue = textField.GetValue(msg) as string;
                else if (textProperty != null)
                    textValue = ((System.Reflection.PropertyInfo)textProperty).GetValue(msg) as string;
                
                if (stackTraceField != null)
                    stackTraceValue = stackTraceField.GetValue(msg) as string;
                
                // 检查是否为错误类型
                bool isError = false;
                if (typeValue != null)
                {
                    string typeStr = typeValue.ToString() ?? "";
                    isError = typeStr.Contains("Error") || typeStr == "2"; // LogMessageType.Error 的值
                }
                
                if (isError && !string.IsNullOrEmpty(textValue))
                {
                    // 排除一些常见的无害错误
                    if (ShouldIgnoreError(textValue))
                        continue;
                    
                    errors.Add(new LogError
                    {
                        Message = TruncateMessage(textValue, 200),
                        StackTrace = TruncateMessage(stackTraceValue ?? "", 300),
                        IsException = textValue.Contains("Exception") || textValue.Contains("NullReference")
                    });
                    
                    // 最多收集 5 个错误
                    if (errors.Count >= 5)
                        break;
                }
            }
        }
        
        /// <summary>
        /// ? 判断是否应该忽略某些常见无害错误
        /// </summary>
        private bool ShouldIgnoreError(string errorText)
        {
            // 忽略的错误模式
            string[] ignorePatterns = new[]
            {
                "MissingMethodException: Default constructor",  // 常见的Mod兼容问题
                "Shader",  // 着色器警告
                "音频",
                "Audio",
                "Font",  // 字体问题
                "Could not load UnityEngine.Texture2D",  // 纹理加载问题（通常无害）
            };
            
            foreach (var pattern in ignorePatterns)
            {
                if (errorText.Contains(pattern))
                    return true;
            }
            
            return false;
        }
        
        /// <summary>
        /// ? 截断消息长度
        /// </summary>
        private string TruncateMessage(string message, int maxLength)
        {
            if (string.IsNullOrEmpty(message))
                return "";
            
            if (message.Length <= maxLength)
                return message;
            
            return message.Substring(0, maxLength) + "...";
        }
        
        /// <summary>
        /// ? 构建问候上下文（带错误检查）
        /// </summary>
        private string BuildGreetingContext(DateTime now, List<LogError> logErrors)
        {
            string timePeriod = GetTimePeriod(now.Hour);
            string dayOfWeek = now.ToString("dddd", System.Globalization.CultureInfo.GetCultureInfo("zh-CN"));
            bool isWeekend = now.DayOfWeek == DayOfWeek.Saturday || now.DayOfWeek == DayOfWeek.Sunday;
            
            // 构建自然语言的问候请求
            var context = "[玩家刚刚加载了存档，请主动向玩家打招呼]\n";
            context += $"当前真实时间: {now:yyyy年MM月dd日 HH:mm} {dayOfWeek}";
            if (isWeekend) context += "（周末）";
            context += $"\n时段: {timePeriod}\n";
            
            // ? 新增：如果有严重错误，在问候中提及
            if (logErrors.Count > 0)
            {
                context += "\n?? **检测到游戏日志中存在严重错误！**\n";
                context += $"发现 {logErrors.Count} 个错误/异常：\n";
                
                for (int i = 0; i < logErrors.Count; i++)
                {
                    var error = logErrors[i];
                    context += $"\n【错误 {i + 1}】\n";
                    context += $"消息: {error.Message}\n";
                    if (!string.IsNullOrEmpty(error.StackTrace))
                    {
                        context += $"堆栈: {error.StackTrace}\n";
                    }
                }
                
                context += "\n**请在问候玩家后，询问玩家是否需要帮助处理这些错误。**\n";
                context += "如果玩家同意，你可以：\n";
                context += "1. 根据错误信息分析可能的原因\n";
                context += "2. 使用联网搜索功能查找解决方案\n";
                context += "3. 提供具体的修复建议\n";
            }
            else
            {
                context += "请根据当前时间和好感度，用合适的语气向玩家问候。可以提及时间、关心玩家状态、或者简单打个招呼。";
            }
            
            return context;
        }
        
        /// <summary>
        /// ? 获取时间段描述
        /// </summary>
        private string GetTimePeriod(int hour)
        {
            if (hour >= 0 && hour < 6) return "深夜";
            else if (hour >= 6 && hour < 9) return "清晨";
            else if (hour >= 9 && hour < 12) return "上午";
            else if (hour >= 12 && hour < 14) return "中午";
            else if (hour >= 14 && hour < 18) return "下午";
            else if (hour >= 18 && hour < 20) return "傍晚";
            else if (hour >= 20 && hour < 22) return "晚上";
            else return "深夜";
        }
    }

    public class FavorabilityEvent : IExposable
    {
        public int timestamp;
        public float change;
        public string reason = "";
        public float newValue;

        public string GetTimeAgo()
        {
            int ticksAgo = Find.TickManager.TicksGame - timestamp;
            int minutesAgo = ticksAgo / 3600;
            
            if (minutesAgo < 1) return "Just now";
            if (minutesAgo < 60) return $"{minutesAgo}m ago";
            return $"{minutesAgo / 60}h ago";
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref timestamp, "timestamp", 0);
            Scribe_Values.Look(ref change, "change", 0f);
            Scribe_Values.Look(ref reason, "reason", "");
            Scribe_Values.Look(ref newValue, "newValue", 0f);
        }
    }

    /// <summary>
    /// ? 重新定义好感度等级（移除"中立"，覆盖 -1000 到 +1000）
    /// </summary>
    public enum AffinityTier
    {
        Hatred,        // 憎恨 (-1000 ~ -701)
        Hostile,       // 敌意 (-700 ~ -401)
        Cold,          // 疏远 (-400 ~ -101)
        Indifferent,   // 冷淡 (-100 ~ 99)  ← 起始点 0
        Warm,          // 温暖 (100 ~ 299)
        Devoted,       // 倾心 (300 ~ 599)
        Adoration,     // 爱慕 (600 ~ 849)
        SoulBound      // 魂之友/主 (850 ~ 1000)
    }
    
    /// <summary>
    /// ? 新增：日志错误信息
    /// </summary>
    public class LogError
    {
        public string Message { get; set; } = "";
        public string StackTrace { get; set; } = "";
        public bool IsException { get; set; } = false;
    }
}
