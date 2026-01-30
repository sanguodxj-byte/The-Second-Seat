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
using Newtonsoft.Json;

namespace TheSecondSeat.Narrator
{
    /// <summary>
    /// ⭐ v1.9.7: 叙事者模式枚举
    /// </summary>
    public enum NarratorMode
    {
        Assistant,   // 协助者 - 帮助玩家
        Opponent,    // 对弈者 - 制造挑战
        Engineer     // 工程师 - 技术专注
    }

    /// <summary>
    /// Manages the AI narrator's favorability and state
    /// ⭐ v1.6.65: 集成 RimAgent 系统
    /// </summary>
    public partial class NarratorManager : GameComponent
    {
        // ⭐ v1.6.65: RimAgent 实例 (Narrator - 负责对话)
        private RimAgent.RimAgent narratorAgent;

        // ⭐ v1.9.2: Event Director Agent (DM - 负责行动)
        private RimAgent.RimAgent eventAgent;
        private float lastEventCheckRealTime = 0f;
        private float nextEventCheckInterval = 300f; // 动态间隔
        
        // ⭐ v1.9.7: 叙事者模式
        private NarratorMode currentNarratorMode = NarratorMode.Assistant;
        
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
        
        /// <summary>
        /// ⭐ v1.9.7: 静态实例访问器（便于 UI 访问）
        /// </summary>
        public static NarratorManager Instance => Current.Game?.GetComponent<NarratorManager>();
        
        /// <summary>
        /// ⭐ v1.9.7: 当前人格（属性别名）
        /// </summary>
        public NarratorPersonaDef CurrentPersona => currentPersonaDef;
        
        /// <summary>
        /// ⭐ v1.9.7: 叙事者代理（属性别名）
        /// </summary>
        public StorytellerAgent StorytellerAgent => storytellerAgent;
        
        /// <summary>
        /// ⭐ v1.9.7: 当前叙事者模式
        /// </summary>
        public NarratorMode CurrentNarratorMode => currentNarratorMode;
        
        /// <summary>
        /// ⭐ v1.9.7: 设置叙事者模式
        /// </summary>
        public void SetNarratorMode(NarratorMode mode)
        {
            if (currentNarratorMode != mode)
            {
                currentNarratorMode = mode;
                Log.Message($"[NarratorManager] Mode switched to: {mode}");
                
                // 重新初始化 Agent 以应用新模式
                InitializeRimAgent();
            }
        }

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
        /// ⭐ v1.9.2: 同时初始化 EventDirector
        /// </summary>
        private void InitializeRimAgent()
        {
            try
            {
                var provider = LLMProviderFactory.GetProvider("auto");
                
                // 1. 初始化 Narrator Agent (对话)
                narratorAgent = new RimAgent.RimAgent(
                    "main-narrator",
                    GetDynamicSystemPrompt(),
                    provider
                );

                // 2. 初始化 Event Director Agent (行动)
                // 注意：这里需要先确保 storytellerAgent 已初始化
                if (storytellerAgent == null) storytellerAgent = new StorytellerAgent();
                
                // 获取当前难度模式
                var modSettings = LoadedModManager.GetMod<Settings.TheSecondSeatMod>()?
                    .GetSettings<Settings.TheSecondSeatSettings>();
                var difficultyMode = modSettings?.difficultyMode ?? AIDifficultyMode.Assistant;

                // ⭐ 修复：安全获取 PersonaDef，防止 Cassandra_Classic 缺失导致崩溃
                var targetPersona = currentPersonaDef
                    ?? DefDatabase<NarratorPersonaDef>.GetNamedSilentFail("Cassandra_Classic")
                    ?? DefDatabase<NarratorPersonaDef>.AllDefsListForReading.FirstOrDefault();

                if (targetPersona == null)
                {
                    // 如果没有任何 PersonaDef，创建一个临时的
                    targetPersona = new NarratorPersonaDef
                    {
                        defName = "Fallback_Narrator",
                        narratorName = "Narrator",
                        label = "Narrator"
                    };
                }

                string eventPrompt = SystemPromptGenerator.GenerateEventDirectorPrompt(
                    targetPersona,
                    currentAnalysis ?? new PersonaAnalysisResult(),
                    storytellerAgent,
                    difficultyMode
                );

                eventAgent = new RimAgent.RimAgent(
                    "event-director",
                    eventPrompt,
                    provider
                );
                
                // ✅ 修复：创建工具实例并注册到全局工具库
                var searchTool = new RimAgent.Tools.SearchTool();
                var analyzeTool = new RimAgent.Tools.AnalyzeTool();
                var commandTool = new RimAgent.Tools.CommandTool();
                var personaDetailTool = new RimAgent.Tools.PersonaDetailTool();
                var promptModifierTool = new RimAgent.Tools.PromptModifierTool();
                var logReaderTool = new RimAgent.Tools.LogReaderTool();
                var logAnalysisTool = new RimAgent.Tools.LogAnalysisTool();
                var filePatcherTool = new RimAgent.Tools.FilePatcherTool();
                var rollDiceTool = new RimAgent.Tools.RollDiceTool();
                
                // 注册到全局库 (如果已存在会自动跳过或覆盖)
                RimAgent.RimAgentTools.RegisterTool(searchTool.Name, searchTool);
                RimAgent.RimAgentTools.RegisterTool(analyzeTool.Name, analyzeTool);
                RimAgent.RimAgentTools.RegisterTool(commandTool.Name, commandTool);
                RimAgent.RimAgentTools.RegisterTool(personaDetailTool.Name, personaDetailTool);
                RimAgent.RimAgentTools.RegisterTool(promptModifierTool.Name, promptModifierTool);
                RimAgent.RimAgentTools.RegisterTool(logReaderTool.Name, logReaderTool);
                RimAgent.RimAgentTools.RegisterTool(logAnalysisTool.Name, logAnalysisTool);
                RimAgent.RimAgentTools.RegisterTool(filePatcherTool.Name, filePatcherTool);
                RimAgent.RimAgentTools.RegisterTool(rollDiceTool.Name, rollDiceTool);
                
                // 注册工具到 Narrator Agent
                narratorAgent.RegisterTool(searchTool.Name);
                narratorAgent.RegisterTool(analyzeTool.Name);
                narratorAgent.RegisterTool(commandTool.Name);
                narratorAgent.RegisterTool(personaDetailTool.Name);
                narratorAgent.RegisterTool(promptModifierTool.Name);
                narratorAgent.RegisterTool(logReaderTool.Name);
                narratorAgent.RegisterTool(logAnalysisTool.Name);
                narratorAgent.RegisterTool(filePatcherTool.Name);
                narratorAgent.RegisterTool(rollDiceTool.Name);

                // 注册工具到 Event Director Agent
                // EventDirector 主要需要 CommandTool 来执行 Incident/Quest
                eventAgent.RegisterTool(commandTool.Name);
                
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
                
                // 使用 ConcurrentRequestManager 管理请求
                var response = await ConcurrentRequestManager.Instance.EnqueueAsync(
                    async () => await narratorAgent.ExecuteAsync(
                        userInput,
                        temperature: 0.7f,
                        maxTokens: 5000
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
            
            // ⭐ v1.9.3: 初始化或重置性格标签
            // 如果是新游戏/切换人格 (resetAffinity=true) 或者旧存档迁移 (activePersonalityTags=null)
            if (resetAffinity || storytellerAgent.activePersonalityTags == null)
            {
                storytellerAgent.activePersonalityTags = new List<string>();
                if (personaDef.personalityTags != null)
                {
                    storytellerAgent.activePersonalityTags.AddRange(personaDef.personalityTags);
                }
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
            // 改用 "dialogueOverlayRect_v3" 并分别保存坐标，彻底解决格式问题
            if (Scribe.mode == LoadSaveMode.Saving && dialogueOverlayRect.HasValue)
            {
                float x = dialogueOverlayRect.Value.x;
                float y = dialogueOverlayRect.Value.y;
                float w = dialogueOverlayRect.Value.width;
                float h = dialogueOverlayRect.Value.height;
                
                Scribe_Values.Look(ref x, "dialogueOverlayRect_x");
                Scribe_Values.Look(ref y, "dialogueOverlayRect_y");
                Scribe_Values.Look(ref w, "dialogueOverlayRect_w");
                Scribe_Values.Look(ref h, "dialogueOverlayRect_h");
            }
            else if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                // 尝试加载新格式 (v3)
                float x = 0f, y = 0f, w = 0f, h = 0f;
                Scribe_Values.Look(ref x, "dialogueOverlayRect_x", -9999f);
                
                if (x != -9999f)
                {
                    Scribe_Values.Look(ref y, "dialogueOverlayRect_y");
                    Scribe_Values.Look(ref w, "dialogueOverlayRect_w");
                    Scribe_Values.Look(ref h, "dialogueOverlayRect_h");
                    dialogueOverlayRect = new Rect(x, y, w, h);
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

                // ? 随时间自然增长好感度 (每2小时 +0.05)
                // 每天增加 0.6，非常缓慢
                if (Find.TickManager.TicksGame % 5000 == 0)
                {
                    if (favorability < 100f)
                    {
                        // 静默增加，不记录到事件日志
                        favorability = Mathf.Min(favorability + 0.05f, 100f);
                        
                        // 同步到 StorytellerAgent
                        if (storytellerAgent != null)
                        {
                            storytellerAgent.affinity = favorability;
                        }
                    }
                }

                // ⭐ v1.9.2: Event Director 现实时间心跳检查
                // 仅在地图加载且非暂停状态下检查
                if (Time.realtimeSinceStartup - lastEventCheckRealTime > nextEventCheckInterval)
                {
                    lastEventCheckRealTime = Time.realtimeSinceStartup;
                    
                    // 设置下一次检查的随机波动 (4~8分钟)
                    // 这样玩家无法预测 AI 何时会介入
                    nextEventCheckInterval = UnityEngine.Random.Range(240f, 480f);
                    
                    // 异步触发，不阻塞主线程
                    _ = ProcessEventTickAsync();
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

        /// <summary>
        /// ⭐ v1.9.2: Event Director 核心循环
        /// 获取宏观状态 -> 决策 -> 执行 -> 清空历史
        /// </summary>
        private async Task ProcessEventTickAsync()
        {
            try
            {
                if (eventAgent == null) return;

                // ⭐ 关键修复：每次执行前动态更新 System Prompt
                // 确保 AI 知道最新的好感度、人格状态和难度设置
                var modSettings = LoadedModManager.GetMod<Settings.TheSecondSeatMod>()?
                    .GetSettings<Settings.TheSecondSeatSettings>();
                var difficultyMode = modSettings?.difficultyMode ?? AIDifficultyMode.Assistant;

                eventAgent.SystemPrompt = SystemPromptGenerator.GenerateEventDirectorPrompt(
                    currentPersonaDef ?? DefDatabase<NarratorPersonaDef>.GetNamed("Cassandra_Classic"),
                    currentAnalysis ?? new PersonaAnalysisResult(),
                    storytellerAgent,
                    difficultyMode
                );

                // 1. 获取宏观状态 (Macro State)
                string macroState = GetMacroGameState();
                
                // 2. 构建请求
                string request = $"Current Time: {GenDate.DateFullStringAt(Find.TickManager.TicksAbs, Find.WorldGrid.LongLatOf(Find.CurrentMap.Tile))}\n" +
                                 $"Macro State: {macroState}\n" +
                                 $"Task: Analyze the situation and decide on an action. Respond in JSON.";

                // 3. 执行 Agent (无状态模式)
                // 我们不使用 ConcurrentRequestManager，因为这是后台任务，不应阻塞玩家对话
                // 但为了线程安全，我们依然需要小心
                // ⭐ v1.9.6: 增加 maxTokens 到 5000，避免 JSON 响应被截断
                var response = await eventAgent.ExecuteAsync(request, temperature: 0.7f, maxTokens: 5000);

                // ⭐ v1.9.6: 添加失败日志，帮助诊断空回复问题
                if (!response.Success)
                {
                    Log.Warning($"[EventDirector] Agent execution failed: {response.Error ?? "Unknown error (LLM returned empty response)"}");
                    // 记录最后的请求内容以便调试
                    if (eventAgent.LastPrompt != null)
                    {
                        Log.Message($"[EventDirector] Last prompt length: {eventAgent.LastPrompt.Length} chars");
                    }
                    return;
                }

                // 成功响应 - 解析并执行 Action
                {
                    // 4. 解析并执行 Action
                    Log.Message($"[EventDirector] Decision: {response.Content}");
                    
                    try 
                    {
                        // 尝试提取 JSON (处理可能存在的 markdown 代码块)
                        string jsonContent = response.Content;
                        if (jsonContent.Contains("```json"))
                        {
                            int start = jsonContent.IndexOf("```json") + 7;
                            int end = jsonContent.IndexOf("```", start);
                            if (end > start) jsonContent = jsonContent.Substring(start, end - start).Trim();
                        }
                        else if (jsonContent.Contains("```"))
                        {
                            int start = jsonContent.IndexOf("```") + 3;
                            int end = jsonContent.IndexOf("```", start);
                            if (end > start) jsonContent = jsonContent.Substring(start, end - start).Trim();
                        }

                        var decision = JsonConvert.DeserializeObject<EventDecision>(jsonContent);
                        if (decision != null && !string.IsNullOrEmpty(decision.action) && decision.action != "DoNothing")
                        {
                            var toolParams = MapActionToToolParams(decision.action, decision.parameters);
                            if (toolParams != null)
                            {
                                string actionName = toolParams.ContainsKey("action") ? toolParams["action"].ToString() : "Unknown";
                                Log.Message($"[EventDirector] Executing tool action: {actionName}");
                                
                                // 使用 CommandTool 执行
                                var commandTool = new RimAgent.Tools.CommandTool();
                                await commandTool.ExecuteAsync(toolParams);
                            }
                        }
                    }
                    catch (Exception parseEx)
                    {
                        Log.Warning($"[EventDirector] Failed to parse decision: {parseEx.Message}");
                    }
                }

                // 5. 关键：清空历史，保持无状态
                eventAgent.ClearHistory();
            }
            catch (Exception ex)
            {
                Log.Warning($"[EventDirector] Tick failed: {ex.Message}");
            }
        }

        /// <summary>
        /// ⭐ v1.9.2: 获取宏观游戏状态 (Token 优化版)
        /// </summary>
        private string GetMacroGameState()
        {
            try
            {
                Map map = Find.CurrentMap;
                if (map == null) return "No active map.";

                var sb = new System.Text.StringBuilder();
                sb.Append("{");

                // 1. Wealth
                sb.Append($"\"Wealth\":{map.wealthWatcher.WealthTotal:F0},");

                // 2. Population
                int colonists = map.mapPawns.FreeColonistsCount;
                int downed = map.mapPawns.FreeColonists.Count(p => p.Downed);
                int prisoners = map.mapPawns.PrisonersOfColonyCount;
                int animals = map.mapPawns.SpawnedColonyAnimals.Count;
                sb.Append($"\"Population\":{{\"Colonists\":{colonists},\"Downed\":{downed},\"Prisoners\":{prisoners},\"Animals\":{animals}}},");

                // 3. Resources (Aggregated)
                float food = 0;
                int medicine = 0;
                int wood = 0;
                int steel = 0;
                int components = 0;
                int weapons = 0;

                // 使用 ResourceCounter 高效统计
                // 注意：ResourceCounter 可能不包含所有东西，需要结合 ThingCategory
                // 这里为了性能，我们只统计仓库里的
                foreach (var def in map.resourceCounter.AllCountedAmounts.Keys)
                {
                    int count = map.resourceCounter.GetCount(def);
                    if (count <= 0) continue;

                    if (def.IsNutritionGivingIngestible) food += def.GetStatValueAbstract(StatDefOf.Nutrition) * count;
                    if (def.IsMedicine) medicine += count;
                    if (def == ThingDefOf.WoodLog) wood += count;
                    if (def == ThingDefOf.Steel) steel += count;
                    if (def == ThingDefOf.ComponentIndustrial || def == ThingDefOf.ComponentSpacer) components += count;
                    if (def.IsWeapon) weapons += count;
                }
                sb.Append($"\"Resources\":{{\"FoodNutrition\":{food:F0},\"Medicine\":{medicine},\"Wood\":{wood},\"Steel\":{steel},\"Components\":{components},\"Weapons\":{weapons}}},");

                // 4. Power (Grid)
                // 简单遍历所有 PowerNet
                float powerGain = 0;
                float powerStored = 0;
                if (map.powerNetManager != null)
                {
                    foreach (var net in map.powerNetManager.AllNetsListForReading)
                    {
                        powerGain += net.CurrentEnergyGainRate() * 60000f; // W -> W/Day approx? No, just W
                        powerStored += net.CurrentStoredEnergy();
                    }
                }
                sb.Append($"\"Power\":{{\"GridGain\":{powerGain:F0},\"Stored\":{powerStored:F0}}},");

                // 5. Tech Level
                sb.Append($"\"TechLevel\":\"{Faction.OfPlayer.def.techLevel}\",");

                // 6. Threats
                float threatPoints = StorytellerUtility.DefaultThreatPointsNow(map);
                sb.Append($"\"ThreatPoints\":{threatPoints:F0}");

                sb.Append("}");
                return sb.ToString();
            }
            catch (Exception ex)
            {
                return $"{{\"Error\":\"{ex.Message}\"}}";
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

    /// <summary>
    /// JSON 决策结构
    /// </summary>
    public class EventDecision
    {
        public string thought { get; set; }
        public string action { get; set; }
        public Dictionary<string, string> parameters { get; set; }
    }

    // 扩展 NarratorManager 以包含辅助方法
    public partial class NarratorManager
    {
        /// <summary>
        /// 将 AI Action 映射为 CommandTool 参数字典
        /// </summary>
        private Dictionary<string, object> MapActionToToolParams(string action, Dictionary<string, string> parameters)
        {
            var toolParams = new Dictionary<string, object>();
            var innerParams = new Dictionary<string, object>();

            // 将所有 AI 参数复制到 innerParams
            if (parameters != null)
            {
                foreach (var kvp in parameters)
                {
                    innerParams[kvp.Key] = kvp.Value;
                }
            }

            switch (action)
            {
                case "SpawnRaid":
                    toolParams["action"] = "TriggerEvent";
                    toolParams["target"] = "RaidEnemy"; // 默认袭击
                    // 如果 AI 指定了 specific raid type (e.g. MechCluster), 可以在这里覆盖 target
                    break;
                
                case "GiveQuest":
                    toolParams["action"] = "TriggerEvent";
                    toolParams["target"] = "Quest_TradeRequest"; // 默认贸易任务
                    break;
                
                case "ResourceDrop":
                    toolParams["action"] = "TriggerEvent";
                    toolParams["target"] = "ResourcePodCrash";
                    break;
                
                case "WeatherChange":
                    // 目前没有 WeatherCommand，暂时记录日志或忽略
                    // 或者如果以后实现了 SetWeather 命令：
                    // toolParams["action"] = "SetWeather";
                    // toolParams["target"] = parameters.ContainsKey("type") ? parameters["type"] : "Rain";
                    Log.Warning("[EventDirector] WeatherChange not yet supported.");
                    return null;
                
                default:
                    Log.Warning($"[EventDirector] Unknown action: {action}");
                    return null;
            }

            toolParams["params"] = innerParams;
            return toolParams;
        }
    }
}
