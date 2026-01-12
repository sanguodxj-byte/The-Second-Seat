using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Verse;
using RimWorld;
using TheSecondSeat.Storyteller;
using TheSecondSeat.PersonaGeneration;
using TheSecondSeat.RimAgent;
using TheSecondSeat.Monitoring;
using UnityEngine;

namespace TheSecondSeat.Narrator
{
    /// <summary>
    /// Manages the AI narrator's favorability and state
    /// ⭐ v1.6.65: 集成 RimAgent 系统
    /// </summary>
    public class NarratorManager : GameComponent
    {
        // ⭐ v1.6.65: RimAgent 实例
        private RimAgent.RimAgent narratorAgent;
        
        // ? 好感度系统变量
        private float favorability = 0f; // -100 (仇恨) 到 +100 (挚爱/灵魂绑定/契)
        private List<FavorabilityEvent> recentEvents = new List<FavorabilityEvent>();
        private const int MaxRecentEvents = 20;
        
        // Storyteller agent
        private StorytellerAgent? storytellerAgent;

        // 当前人格相关
        private NarratorPersonaDef? currentPersonaDef;
        private PersonaAnalysisResult? currentAnalysis;
        
        // ? 新增：用于存档保存的人格 defName
        private string savedPersonaDefName = "Cassandra_Classic";
        
        // ? 新增：用于存档保存的对话框位置
        private Rect? dialogueOverlayRect = null;

        public Rect? DialogueOverlayRect
        {
            get => dialogueOverlayRect;
            set => dialogueOverlayRect = value;
        }
        
        // ? v1.6.84: 移除自动问候标记 - 统一由 NarratorController 处理
        // private bool hasGreetedThisSession = false;
        // private int ticksSinceLoad = 0;
        // private const int GreetingDelayTicks = 60;

        // ? 日志收集
        private static List<LogError> recentErrors = new List<LogError>();
        private static readonly object logLock = new object();

        public float Favorability => favorability;
        public List<FavorabilityEvent> RecentEvents => recentEvents;

        public NarratorManager(Game game) : base()
        {
            // ? 初始好感度为 0（冷淡）
            favorability = 0f;
            InitializeDefaultPersona();
            
            // ⭐ v1.6.65: 初始化 RimAgent
            InitializeRimAgent();

            // 注册日志监听（防止重复注册）
            Application.logMessageReceived -= HandleLogMessage;
            Application.logMessageReceived += HandleLogMessage;
        }

        private void HandleLogMessage(string condition, string stackTrace, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception) return;
            if (ShouldIgnoreError(condition)) return;

            lock (logLock)
            {
                recentErrors.Add(new LogError
                {
                    Message = TruncateMessage(condition, 200),
                    StackTrace = TruncateMessage(stackTrace, 300),
                    IsException = type == LogType.Exception || condition.Contains("Exception")
                });

                // 保持最近 50 条
                if (recentErrors.Count > 50)
                {
                    recentErrors.RemoveAt(0);
                }
            }
        }
        
        /// <summary>
        /// ⭐ v1.6.65: 初始化 RimAgent
        /// ✅ v1.6.76: 修复工具注册 - 同时注册到RimAgentTools和Agent
        /// </summary>
        private void InitializeRimAgent()
        {
            try
            {
                var provider = LLMProviderFactory.GetProvider("auto");
                narratorAgent = new RimAgent.RimAgent(
                    "main-narrator",
                    GetDynamicSystemPrompt(),
                    provider
                );
                
                // ✅ 修复：创建工具实例并注册到全局工具库
                var searchTool = new RimAgent.Tools.SearchTool();
                var analyzeTool = new RimAgent.Tools.AnalyzeTool();
                var commandTool = new RimAgent.Tools.CommandTool();
                var personaDetailTool = new RimAgent.Tools.PersonaDetailTool();
                
                RimAgent.RimAgentTools.RegisterTool(searchTool.Name, searchTool);
                RimAgent.RimAgentTools.RegisterTool(analyzeTool.Name, analyzeTool);
                RimAgent.RimAgentTools.RegisterTool(commandTool.Name, commandTool);
                RimAgent.RimAgentTools.RegisterTool(personaDetailTool.Name, personaDetailTool);
                
                // 注册工具到Agent（用于列表显示）
                narratorAgent.RegisterTool(searchTool.Name);
                narratorAgent.RegisterTool(analyzeTool.Name);
                narratorAgent.RegisterTool(commandTool.Name);
                narratorAgent.RegisterTool(personaDetailTool.Name);
                
                // ⭐ 修复：注册调试日志工具
                narratorAgent.RegisterTool("read_log");
                narratorAgent.RegisterTool("analyze_last_error");
                
            }
            catch (Exception ex)
            {
                Log.Error($"[NarratorManager] Failed to initialize RimAgent: {ex.Message}");
            }
        }
        
        /// <summary>
        /// ⭐ v1.6.65: 使用 RimAgent 处理用户输入
        /// </summary>
        public async Task<string> ProcessUserInputAsync(string userInput)
        {
            try
            {
                if (narratorAgent == null)
                {
                    InitializeRimAgent();
                }
                
                // ⭐ v1.6.85: 动态获取游戏状态
                Func<string> gameStateProvider = () => {
                    try {
                        // 注意：如果在后台线程调用，需要确保 CaptureSnapshotSafe 是线程安全的
                        var snapshot = GameStateSnapshotUtility.CaptureSnapshotSafe();
                        return GameStateSnapshotUtility.SnapshotToJson(snapshot);
                    } catch (Exception ex) {
                        Log.Warning($"[NarratorManager] Failed to capture game state: {ex.Message}");
                        return "";
                    }
                };

                // 使用 ConcurrentRequestManager 管理请求
                var response = await ConcurrentRequestManager.Instance.EnqueueAsync(
                    async () => await narratorAgent.ExecuteAsync(
                        userInput,
                        gameStateProvider, // 使用动态 provider
                        temperature: 0.7f,
                        maxTokens: 500
                    ),
                    maxRetries: 3
                );
                
                if (response.Success)
                {
                    return response.Content;
                }
                else
                {
                    Log.Error($"[NarratorManager] Agent error: {response.Error}");
                    return "抱歉，我现在无法回应。请稍后再试。";
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[NarratorManager] Error in ProcessUserInputAsync: {ex.Message}");
                return "发生了错误，请检查日志。";
            }
        }
        
        /// <summary>
        /// ⭐ v1.6.65: 获取 Agent 统计信息
        /// </summary>
        public string GetAgentStats()
        {
            if (narratorAgent == null)
                return "Agent not initialized";
            
            return narratorAgent.GetDebugInfo();
        }
        
        /// <summary>
        /// ⭐ v1.6.65: 重置 Agent
        /// </summary>
        public void ResetAgent()
        {
            narratorAgent?.Reset();
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
            }
        }

        /// <summary>
        /// 加载指定人格（用于新游戏或切换人格）
        /// </summary>
        public void LoadPersona(NarratorPersonaDef personaDef)
        {
            LoadPersonaInternal(personaDef, resetAffinity: true);
        }
        
        /// <summary>
        /// ⭐ v1.6.82: 从存档恢复人格（不重置好感度）
        /// </summary>
        private void RestorePersonaFromSave(NarratorPersonaDef personaDef)
        {
            LoadPersonaInternal(personaDef, resetAffinity: false);
        }
        
        /// <summary>
        /// ⭐ v1.6.82: 内部人格加载方法
        /// </summary>
        /// <param name="personaDef">人格定义</param>
        /// <param name="resetAffinity">是否重置好感度（存档恢复时为false）</param>
        private void LoadPersonaInternal(NarratorPersonaDef personaDef, bool resetAffinity)
        {
            currentPersonaDef = personaDef;
            currentAnalysis = PersonaAnalyzer.AnalyzePersonaDef(personaDef);
            
            // 保存 defName 用于存档
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
            
            // ⭐ v1.6.82: 只在非存档恢复时设置初始好感度
            if (resetAffinity)
            {
                // initialAffinity: -100 ~ 100 (直接映射到 StorytellerAgent)
                if (personaDef.initialAffinity != 0f)
                {
                    // 使用 initialAffinity 直接设置 StorytellerAgent 的 affinity
                    storytellerAgent.affinity = Mathf.Clamp(personaDef.initialAffinity, -100f, 100f);
                    
                    // 同步到 NarratorManager 的 favorability（现在也是 -100~100）
                    favorability = storytellerAgent.affinity;
                }
                else if (personaDef.baseAffinityBias != 0f)
                {
                    // 兼容旧版：使用 baseAffinityBias (-1.0 ~ 1.0) -> -100 ~ 100
                    favorability = personaDef.baseAffinityBias * 100f;
                    storytellerAgent.affinity = favorability;
                }
            }
            // 存档恢复时，好感度已经从存档加载，无需重置
            
            // 根据当前好感度调整对话风格
            storytellerAgent.AdjustDialogueStyleByAffinity();
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
        /// ? 修改好感度（-100 到 +100）
        /// </summary>
        public void ModifyFavorability(float amount, string reason)
        {
            float oldValue = favorability;
            favorability = Mathf.Clamp(favorability + amount, -100f, 100f);

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

            // ? 更新 StorytellerAgent（同步更新）
            if (storytellerAgent != null)
            {
                storytellerAgent.ModifyAffinity(amount, reason);
            }

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
        /// ? 重新定义好感度等级（与 StorytellerAgent 保持一致）
        /// </summary>
        public AffinityTier CurrentTier
        {
            get
            {
                if (favorability >= 90f) return AffinityTier.SoulBound;      // 灵魂伴侣 (90+)
                if (favorability >= 60f) return AffinityTier.Adoration;      // 浪漫伴侣 (60-89)
                if (favorability >= 30f) return AffinityTier.Devoted;        // 亲密好友 (30-59)
                if (favorability >= -10f) return AffinityTier.Indifferent;   // 中性 (-10-29)
                if (favorability >= -50f) return AffinityTier.Cold;          // 疏远 (-50~-11)
                return AffinityTier.Hostile;                                 // 敌对 (<-50)
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
            // ⭐ 修复：默认使用完整版 Prompt，确保人格和恋爱关系指令生效
            bool useCompact = modSettings?.useCompactPrompt ?? false;

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
            
            // ? 保存对话框位置 (修复：使用新的键名以避免旧存档格式错误导致的解析异常)
            // 旧的 "dialogueOverlayRect" 可能包含错误的格式 (x:..., y:...)，导致加载崩溃
            // 改用 "dialogueOverlayRect_v2" 并手动处理
            if (Scribe.mode == LoadSaveMode.Saving && dialogueOverlayRect.HasValue)
            {
                Rect r = dialogueOverlayRect.Value;
                Scribe_Values.Look(ref r, "dialogueOverlayRect_v2");
            }
            else if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                Rect r = default;
                Scribe_Values.Look(ref r, "dialogueOverlayRect_v2");
                if (r != default)
                {
                    dialogueOverlayRect = r;
                }
            }
            
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                // ? 迁移旧存档数据：如果好感度超出 -100~100 范围，则除以 10
                if (Math.Abs(favorability) > 100f)
                {
                    favorability /= 10f;
                    Log.Message($"[NarratorManager] Migrated favorability from old scale to new scale: {favorability}");
                }

                // ⭐ v1.6.82: 从存档恢复人格（不重置好感度）
                if (!string.IsNullOrEmpty(savedPersonaDefName))
                {
                    var personaDef = DefDatabase<NarratorPersonaDef>.GetNamedSilentFail(savedPersonaDefName);
                    if (personaDef != null)
                    {
                        // 使用恢复方法，不覆盖存档中的好感度值
                        RestorePersonaFromSave(personaDef);
                    }
                    else
                    {
                        InitializeDefaultPersona();
                    }
                }
                else
                {
                    InitializeDefaultPersona();
                }
                
                // ? **关键修复**：加载存档后，根据好感度调整对话风格
                if (storytellerAgent != null)
                {
                    // 确保 StorytellerAgent 的好感度也同步
                    storytellerAgent.affinity = favorability;
                    storytellerAgent.AdjustDialogueStyleByAffinity();
                }
                
                // ? v1.6.84: 移除自动问候逻辑 - 统一由 NarratorController 处理
                // hasGreetedThisSession = false;
                // ticksSinceLoad = 0;
            }
        }
        
        /// <summary>
        /// ? 新增：GameComponent Tick - 用于自动问候和表情系统更新
        /// ✅ v1.6.76: 添加定期缓存清理（防止内存泄漏）
        /// ✅ v1.6.82: 添加全面的空引用保护
        /// </summary>
        public override void GameComponentTick()
        {
            base.GameComponentTick();
            
            // ✅ v1.6.82: 空引用保护 - 确保游戏已完全加载
            if (Find.TickManager == null || Current.Game == null)
                return;
            
            try
            {
                // ? 新增：更新当前人格的表情过渡
                if (currentPersonaDef != null && !string.IsNullOrEmpty(currentPersonaDef.defName))
                {
                    ExpressionSystem.UpdateTransition(currentPersonaDef.defName);
                }
                
                // ✅ 新增：每10分钟清理一次旧缓存（防止内存泄漏）
                if (Find.TickManager.TicksGame % 36000 == 0) // 36000 ticks = 10分钟
                {
                    PortraitLoader.CleanOldCache();
                    ExpressionSystem.CleanupOldStates();
                }
                
                // ? v1.6.84: 移除自动问候逻辑 - 统一由 NarratorController 处理
                // 避免重复发送问候消息（之前 NarratorManager 和 NarratorController 各自触发一次）
            }
            catch (Exception ex)
            {
                // ✅ v1.6.82: 捕获所有异常，防止游戏崩溃
                Log.Error($"[NarratorManager] GameComponentTick error: {ex.Message}");
            }
        }
        
        // ? v1.6.84: TriggerAutoGreeting 已移除 - 统一由 NarratorController.TriggerLoadGreeting() 处理
        // 这避免了重复发送问候消息的问题
        
        /// <summary>
        /// ? 检查最近的日志错误 (供外部调用)
        /// </summary>
        public List<LogError> GetRecentLogErrors()
        {
            lock (logLock)
            {
                // 返回最近5个错误的副本
                int count = recentErrors.Count;
                return recentErrors.Skip(Math.Max(0, count - 5)).ToList();
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
        
        // ? v1.6.84: BuildGreetingContext 已移除 - 问候逻辑统一由 NarratorController 处理
        
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
    /// ? 重新定义好感度等级（覆盖 -100 到 +100）
    /// </summary>
    public enum AffinityTier
    {
        Hatred,        // 憎恨 (未使用)
        Hostile,       // 敌意 (<-50)
        Cold,          // 疏远 (-50 ~ -11)
        Indifferent,   // 中性 (-10 ~ 29)
        Warm,          // 温暖 (未使用)
        Devoted,       // 亲密好友 (30 ~ 59)
        Adoration,     // 浪漫伴侣 (60 ~ 89)
        SoulBound      // 灵魂伴侣 (90+)
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
