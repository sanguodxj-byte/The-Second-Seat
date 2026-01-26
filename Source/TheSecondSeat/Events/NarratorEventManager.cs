using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using RimWorld;
using TheSecondSeat.Storyteller;

namespace TheSecondSeat.Framework
{
    /// <summary>
    /// 叙事者事件管理器 - 负责检测和触发NarratorEventDef
    /// 
    /// 职责：
    /// 1. 定期检查所有已加载的NarratorEventDef
    /// 2. 评估触发条件
    /// 3. 执行符合条件的事件
    /// 4. 管理事件冷却和状态
    /// 
    /// 架构：
    /// - 这是一个GameComponent，随游戏保存/加载
    /// - 与StorytellerAgent协同工作，但不依赖原版Storyteller
    /// - 完全数据驱动，不需要C#代码扩展
    /// </summary>
    public class NarratorEventManager : GameComponent
    {
        // ============================================
        // 配置常量
        // ============================================
        
        /// <summary>
        /// 事件检查间隔（Tick）
        /// 默认60 = 每秒检查一次
        /// </summary>
        private const int CHECK_INTERVAL = 60;
        
        /// <summary>
        /// 高优先级事件检查间隔（Tick）
        /// 高优先级事件（priority >= 100）检查更频繁
        /// </summary>
        private const int HIGH_PRIORITY_CHECK_INTERVAL = 30;
        
        // ============================================
        // 运行时状态
        // ============================================
        
        private int lastCheckTick = 0;
        private int lastHighPriorityCheckTick = 0;
        private int lastContextUpdateTick = -1;
        
        // 缓存的事件列表，避免每帧重新排序和过滤
        private List<NarratorEventDef> cachedSortedEvents;
        private List<NarratorEventDef> cachedHighPriorityEvents;

        /// <summary>
        /// 事件执行统计
        /// </summary>
        private Dictionary<string, int> eventExecutionCounts = new Dictionary<string, int>();
        
        /// <summary>
        /// 上下文数据缓存（共享给所有事件使用）
        /// </summary>
        private NarratorContext cachedContext = NarratorContext.Empty;
        
        // ============================================
        // 单例访问
        // ============================================
        
        private static NarratorEventManager instance;
        public static NarratorEventManager Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = Current.Game?.GetComponent<NarratorEventManager>();
                }
                return instance;
            }
        }
        
        // ============================================
        // 构造与初始化
        // ============================================
        
        public NarratorEventManager(Game game) : base()
        {
            instance = this;
        }
        
        // ============================================
        // 核心逻辑
        // ============================================
        
        /// <summary>
        /// 每Tick更新（GameComponent接口）
        /// </summary>
        public override void GameComponentTick()
        {
            base.GameComponentTick();
            
            try
            {
                int currentTick = Find.TickManager.TicksGame;
                
                // 高优先级事件检查（更频繁）
                if (currentTick - lastHighPriorityCheckTick >= HIGH_PRIORITY_CHECK_INTERVAL)
                {
                    lastHighPriorityCheckTick = currentTick;
                    CheckHighPriorityEvents();
                }
                // 普通事件检查（避免与高优先级检查在同一帧运行，如果可能）
                else if (currentTick - lastCheckTick >= CHECK_INTERVAL)
                {
                    lastCheckTick = currentTick;
                    CheckAllEvents();
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[NarratorEventManager] Tick update failed: {ex.Message}\n{ex.StackTrace}");
            }
        }
        
        /// <summary>
        /// 检查所有事件
        /// </summary>
        private void CheckAllEvents()
        {
            // 获取当前地图
            Map map = Find.CurrentMap;
            if (map == null) return;
            
            // 更新上下文数据
            UpdateContext();
            
            // 获取所有NarratorEventDef（已排序）
            var allEvents = GetSortedEvents();
            
            foreach (var eventDef in allEvents)
            {
                try
                {
                // ? 跳过高优先级事件（已在高优先级检查中处理）
                if (eventDef.priority >= 100)
                {
                    continue;
                }
                
                // ? 新增：跳过测试/调试事件
                if (eventDef.category == "Test" || 
                    eventDef.category == "Debug")
                {
                    continue;
                }
                
                // ? 新增：跳过没有triggers的事件（防御性编程）
                // 这种事件通常用于手动触发或测试，不需要自动检查，也不需要产生日志警告
                if (eventDef.triggers == null || eventDef.triggers.Count == 0)
                {
                    continue;
                }
                
                    // 跳过高优先级事件（已在高优先级检查中处理）
                    if (eventDef.priority >= 100)
                    {
                        continue;
                    }
                    
                    // 检查是否可触发
                    if (eventDef.CanTrigger(map, cachedContext))
                    {
                        // 触发事件
                        TriggerEvent(eventDef, map);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[NarratorEventManager] Failed to check event '{eventDef.defName}': {ex.Message}");
                }
            }
        }
        
        /// <summary>
        /// 检查高优先级事件
        /// </summary>
        private void CheckHighPriorityEvents()
        {
            Map map = Find.CurrentMap;
            if (map == null) return;
            
            UpdateContext();
            
            var highPriorityEvents = GetHighPriorityEvents();
            
            foreach (var eventDef in highPriorityEvents)
            {
                try
                {
                    if (eventDef.CanTrigger(map, cachedContext))
                    {
                        TriggerEvent(eventDef, map);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[NarratorEventManager] Failed to check high-priority event '{eventDef.defName}': {ex.Message}");
                }
            }
        }
        
        /// <summary>
        /// 触发事件
        /// </summary>
        private void TriggerEvent(NarratorEventDef eventDef, Map map)
        {
            try
            {
                // 执行事件
                eventDef.TriggerEvent(map, cachedContext);
                
                // 更新统计
                if (!eventExecutionCounts.ContainsKey(eventDef.defName))
                {
                    eventExecutionCounts[eventDef.defName] = 0;
                }
                eventExecutionCounts[eventDef.defName]++;
                
                if (Prefs.DevMode)
                {
                    Log.Message($"[NarratorEventManager] Event '{eventDef.defName}' triggered (total: {eventExecutionCounts[eventDef.defName]})");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[NarratorEventManager] Failed to trigger event '{eventDef.defName}': {ex.Message}\n{ex.StackTrace}");
            }
        }
        
        /// <summary>
        /// 更新上下文数据（供所有事件使用）
        /// </summary>
        private void UpdateContext()
        {
            // 如果在同一 Tick 内已经更新过，则跳过
            if (Find.TickManager.TicksGame == lastContextUpdateTick)
            {
                return;
            }
            
            lastContextUpdateTick = Find.TickManager.TicksGame;
            
            try
            {
                // 获取当前人格
                var manager = Current.Game?.GetComponent<TheSecondSeat.Narrator.NarratorManager>();
                var persona = manager?.GetCurrentPersona();
                string personaName = persona?.defName ?? "";
                
                // 获取好感度
                float affinity = 0f;
                string mood = "Neutral";
                string difficultyMode = "Normal";

                var agent = Current.Game?.GetComponent<StorytellerAgent>();
                if (agent != null)
                {
                    affinity = agent.GetAffinity();
                    mood = agent.currentMood.ToString();
                }

                if (persona != null)
                {
                    difficultyMode = persona.difficultyMode.ToString();
                }
                
                // 获取殖民地基本信息
                int colonistCount = 0;
                int prisonerCount = 0;
                int animalCount = 0;
                float wealthTotal = 0f;
                float wealthBuildings = 0f;
                float wealthItems = 0f;
                int gameYear = 0;
                Season gameSeason = Season.Undefined;

                Map map = Find.CurrentMap;
                if (map != null)
                {
                    colonistCount = map.mapPawns.FreeColonistsCount;
                    prisonerCount = map.mapPawns.PrisonersOfColonyCount;
                    
                    // 计算殖民地动物数量
                    foreach (var pawn in map.mapPawns.SpawnedPawnsInFaction(Faction.OfPlayer))
                    {
                        if (pawn.RaceProps.Animal)
                        {
                            animalCount++;
                        }
                    }
                    
                    // 财富统计
                    wealthTotal = map.wealthWatcher?.WealthTotal ?? 0;
                    wealthBuildings = map.wealthWatcher?.WealthBuildings ?? 0;
                    wealthItems = map.wealthWatcher?.WealthItems ?? 0;

                    // 游戏时间
                    gameYear = GenDate.Year(Find.TickManager.TicksAbs, Find.WorldGrid.LongLatOf(map.Tile).x);
                    gameSeason = GenDate.Season(Find.TickManager.TicksAbs, Find.WorldGrid.LongLatOf(map.Tile));
                }
                
                int gameTicks = Find.TickManager.TicksGame;

                cachedContext = new NarratorContext(
                    personaName,
                    persona,
                    affinity,
                    mood,
                    difficultyMode,
                    colonistCount,
                    prisonerCount,
                    animalCount,
                    wealthTotal,
                    wealthBuildings,
                    wealthItems,
                    gameTicks,
                    gameYear,
                    gameSeason
                );
            }
            catch (Exception ex)
            {
                Log.Error($"[NarratorEventManager] Failed to update context: {ex.Message}");
                cachedContext = NarratorContext.Empty;
            }
        }
        
        /// <summary>
        /// 获取所有事件（按优先级排序）
        /// </summary>
        private List<NarratorEventDef> GetSortedEvents()
        {
            if (cachedSortedEvents == null)
            {
                cachedSortedEvents = DefDatabase<NarratorEventDef>.AllDefsListForReading
                    .OrderByDescending(e => e.priority)
                    .ToList();
            }
            return cachedSortedEvents;
        }
        
        /// <summary>
        /// 获取高优先级事件
        /// </summary>
        private List<NarratorEventDef> GetHighPriorityEvents()
        {
            if (cachedHighPriorityEvents == null)
            {
                cachedHighPriorityEvents = DefDatabase<NarratorEventDef>.AllDefsListForReading
                    .Where(e => e.priority >= 100)
                    .OrderByDescending(e => e.priority)
                    .ToList();
            }
            return cachedHighPriorityEvents;
        }
        
        // ============================================
        // 公共API
        // ============================================
        
        /// <summary>
        /// 手动触发事件（用于调试或特殊情况）
        /// </summary>
        public void ForceTriggerEvent(string eventDefName)
        {
            var eventDef = DefDatabase<NarratorEventDef>.GetNamedSilentFail(eventDefName);
            if (eventDef == null)
            {
                Log.Warning($"[NarratorEventManager] Event '{eventDefName}' not found");
                return;
            }
            
            Map map = Find.CurrentMap;
            if (map == null)
            {
                Log.Warning($"[NarratorEventManager] No current map, cannot trigger event");
                return;
            }
            
            UpdateContext();
            TriggerEvent(eventDef, map);
        }
        
        /// <summary>
        /// 获取事件执行统计
        /// </summary>
        public int GetEventExecutionCount(string eventDefName)
        {
            return eventExecutionCounts.TryGetValue(eventDefName, out int count) ? count : 0;
        }
        
        /// <summary>
        /// 获取所有事件执行统计
        /// </summary>
        public Dictionary<string, int> GetAllEventStats()
        {
            return new Dictionary<string, int>(eventExecutionCounts);
        }
        
        /// <summary>
        /// 重置所有事件状态（调试用）
        /// </summary>
        public void ResetAllEventStates()
        {
            foreach (var eventDef in DefDatabase<NarratorEventDef>.AllDefsListForReading)
            {
                eventDef.ResetState();
            }
            
            eventExecutionCounts.Clear();
            // 清除缓存列表以强制刷新
            cachedSortedEvents = null;
            cachedHighPriorityEvents = null;
            
            if (Prefs.DevMode)
            {
                Log.Message("[NarratorEventManager] All event states reset");
            }
        }
        
        // ============================================
        // 存档支持
        // ============================================
        
        public override void ExposeData()
        {
            base.ExposeData();
            
            Scribe_Values.Look(ref lastCheckTick, "lastCheckTick", 0);
            Scribe_Values.Look(ref lastHighPriorityCheckTick, "lastHighPriorityCheckTick", 0);
            Scribe_Collections.Look(ref eventExecutionCounts, "eventExecutionCounts", LookMode.Value, LookMode.Value);
            
            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                if (eventExecutionCounts == null)
                {
                    eventExecutionCounts = new Dictionary<string, int>();
                }
            }
        }
    }
}
