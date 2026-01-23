using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using RimWorld;
using HarmonyLib;

namespace TheSecondSeat.Monitoring
{
    /// <summary>
    /// ⭐ v1.9.0: 高性能游戏状态观察器
    /// 
    /// 核心优化策略：
    /// 1. 分频更新：高频数据(心情/健康)每60tick，低频数据(仓库/财富)每2500tick
    /// 2. 脏标记模式：只在状态变化时才更新缓存
    /// 3. 事件驱动：监听游戏事件，避免无谓的全量扫描
    /// 4. 增量更新：只返回变化的数据，减少Token消耗
    /// </summary>
    public class GameStateObserver : GameComponent
    {
        // ========== 更新频率 ==========
        private const int HIGH_FREQ_INTERVAL = 60;        // 1秒 - 心情/健康/战斗
        private const int LOW_FREQ_INTERVAL = 2500;       // ~42秒 - 仓库/财富
        private const int VERY_LOW_FREQ_INTERVAL = 15000; // ~4分钟 - 全量扫描
        
        // ========== Tick 计数器 ==========
        private int highFreqTicks = 0;
        private int lowFreqTicks = 0;
        private int veryLowFreqTicks = 0;
        
        // ========== 脏标记 ==========
        private bool isHighFreqDirty = true;
        private bool isLowFreqDirty = true;
        private bool forceFullScan = false;
        
        // ========== 缓存数据 ==========
        private HighFreqState cachedHighFreqState = new HighFreqState();
        private LowFreqState cachedLowFreqState = new LowFreqState();
        
        // ========== 变化检测 ==========
        private float lastAverageMood = 0f;
        private int lastColonistCount = 0;
        private bool lastInCombat = false;
        private float lastColonyWealth = 0f;
        private int lastFoodCount = 0;
        
        // ========== 事件队列 ==========
        private Queue<GameEvent> pendingEvents = new Queue<GameEvent>();
        private const int MAX_EVENT_QUEUE = 20;
        
        public GameStateObserver(Game game) : base() { }
        
        public override void GameComponentTick()
        {
            base.GameComponentTick();
            
            // 高频更新
            highFreqTicks++;
            if (highFreqTicks >= HIGH_FREQ_INTERVAL)
            {
                highFreqTicks = 0;
                UpdateHighFreqState();
            }
            
            // 低频更新
            lowFreqTicks++;
            if (lowFreqTicks >= LOW_FREQ_INTERVAL)
            {
                lowFreqTicks = 0;
                UpdateLowFreqState();
            }
            
            // 极低频更新（全量扫描）
            veryLowFreqTicks++;
            if (veryLowFreqTicks >= VERY_LOW_FREQ_INTERVAL || forceFullScan)
            {
                veryLowFreqTicks = 0;
                forceFullScan = false;
                PerformFullScan();
            }
        }
        
        /// <summary>
        /// ⭐ 高频状态更新（每秒）
        /// 只更新关键数据：心情、健康、战斗状态
        /// </summary>
        private void UpdateHighFreqState()
        {
            Map map = Find.CurrentMap;
            if (map == null) return;
            
            var colonists = map.mapPawns.FreeColonistsSpawned;
            if (colonists == null || !colonists.Any()) return;
            
            // 计算平均心情（O(n)，但n通常很小）
            float totalMood = 0f;
            int healthyCounts = 0;
            int downedCount = 0;
            bool inCombat = false;
            
            foreach (var pawn in colonists)
            {
                if (pawn.Dead) continue;
                
                // 心情
                if (pawn.needs?.mood != null)
                {
                    totalMood += pawn.needs.mood.CurLevelPercentage;
                }
                
                // 健康
                if (pawn.Downed)
                    downedCount++;
                else if (pawn.health?.summaryHealth?.SummaryHealthPercent > 0.5f)
                    healthyCounts++;
                    
                // 战斗状态
                if (pawn.InMentalState || pawn.CurJobDef == JobDefOf.AttackMelee || 
                    pawn.CurJobDef == JobDefOf.AttackStatic)
                {
                    inCombat = true;
                }
            }
            
            int count = colonists.Count();
            float avgMood = count > 0 ? totalMood / count : 0f;
            
            // 检测变化
            bool moodChanged = Math.Abs(avgMood - lastAverageMood) > 0.05f;
            bool colonistCountChanged = count != lastColonistCount;
            bool combatChanged = inCombat != lastInCombat;
            
            if (moodChanged || colonistCountChanged || combatChanged)
            {
                isHighFreqDirty = true;
                
                // 更新缓存
                cachedHighFreqState.AverageMood = avgMood;
                cachedHighFreqState.ColonistCount = count;
                cachedHighFreqState.HealthyCount = healthyCounts;
                cachedHighFreqState.DownedCount = downedCount;
                cachedHighFreqState.InCombat = inCombat;
                cachedHighFreqState.LastUpdate = Find.TickManager.TicksGame;
                
                // 更新检测值
                lastAverageMood = avgMood;
                lastColonistCount = count;
                lastInCombat = inCombat;
                
                // 生成变化事件
                if (combatChanged && inCombat)
                {
                    EnqueueEvent(GameEventType.CombatStarted, "进入战斗状态");
                }
                else if (combatChanged && !inCombat)
                {
                    EnqueueEvent(GameEventType.CombatEnded, "脱离战斗状态");
                }
                
                if (colonistCountChanged)
                {
                    if (count > lastColonistCount)
                        EnqueueEvent(GameEventType.ColonistJoined, $"殖民者人数变为{count}");
                    else
                        EnqueueEvent(GameEventType.ColonistLost, $"殖民者人数变为{count}");
                }
            }
        }
        
        /// <summary>
        /// ⭐ 低频状态更新（约每分钟）
        /// 更新资源数据：仓库库存、财富值
        /// </summary>
        private void UpdateLowFreqState()
        {
            Map map = Find.CurrentMap;
            if (map == null) return;
            
            // 财富计算（相对昂贵，但低频调用）
            float wealth = map.wealthWatcher.WealthTotal;
            
            // 食物统计（只统计营养来源，不遍历所有物品）
            int foodCount = 0;
            var foods = map.listerThings.ThingsInGroup(ThingRequestGroup.FoodSourceNotPlantOrTree);
            if (foods != null)
            {
                foodCount = foods.Sum(t => t.stackCount);
            }
            
            // 检测变化
            bool wealthChanged = Math.Abs(wealth - lastColonyWealth) > lastColonyWealth * 0.1f; // 10%变化
            bool foodChanged = Math.Abs(foodCount - lastFoodCount) > lastFoodCount * 0.2f; // 20%变化
            
            if (wealthChanged || foodChanged)
            {
                isLowFreqDirty = true;
                
                cachedLowFreqState.ColonyWealth = wealth;
                cachedLowFreqState.FoodCount = foodCount;
                cachedLowFreqState.LastUpdate = Find.TickManager.TicksGame;
                
                lastColonyWealth = wealth;
                lastFoodCount = foodCount;
            }
        }
        
        /// <summary>
        /// ⭐ 全量扫描（极低频，或事件触发）
        /// 用于需要完整数据的场景
        /// </summary>
        private void PerformFullScan()
        {
            Map map = Find.CurrentMap;
            if (map == null) return;
            
            // 更新所有数据
            UpdateHighFreqState();
            UpdateLowFreqState();
            
            // 标记脏
            isHighFreqDirty = true;
            isLowFreqDirty = true;
            
            if (Prefs.DevMode)
            {
                Log.Message("[GameStateObserver] Full scan completed");
            }
        }
        
        /// <summary>
        /// ⭐ 事件触发：强制全量扫描
        /// </summary>
        public void TriggerFullScan()
        {
            forceFullScan = true;
        }
        
        /// <summary>
        /// ⭐ 添加事件到队列
        /// </summary>
        private void EnqueueEvent(GameEventType type, string description)
        {
            if (pendingEvents.Count >= MAX_EVENT_QUEUE)
            {
                pendingEvents.Dequeue(); // 移除最旧的事件
            }
            
            pendingEvents.Enqueue(new GameEvent
            {
                Type = type,
                Description = description,
                GameTick = Find.TickManager.TicksGame
            });
        }
        
        /// <summary>
        /// ⭐ 获取待处理事件（消费后清空）
        /// </summary>
        public List<GameEvent> ConsumeEvents()
        {
            var events = pendingEvents.ToList();
            pendingEvents.Clear();
            return events;
        }
        
        /// <summary>
        /// ⭐ 获取高频状态（只读）
        /// </summary>
        public HighFreqState GetHighFreqState()
        {
            isHighFreqDirty = false;
            return cachedHighFreqState;
        }
        
        /// <summary>
        /// ⭐ 获取低频状态（只读）
        /// </summary>
        public LowFreqState GetLowFreqState()
        {
            isLowFreqDirty = false;
            return cachedLowFreqState;
        }
        
        /// <summary>
        /// ⭐ 检查是否有新数据
        /// </summary>
        public bool HasNewData => isHighFreqDirty || isLowFreqDirty;
        
        /// <summary>
        /// ⭐ 获取简化状态JSON（用于AI，减少Token）
        /// </summary>
        public string GetCompactStateJson()
        {
            return $"{{\"colonists\":{cachedHighFreqState.ColonistCount},\"mood\":{cachedHighFreqState.AverageMood:F2},\"combat\":{(cachedHighFreqState.InCombat ? "true" : "false")},\"food\":{cachedLowFreqState.FoodCount}}}";
        }
        
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref lastAverageMood, "lastAverageMood", 0f);
            Scribe_Values.Look(ref lastColonistCount, "lastColonistCount", 0);
            Scribe_Values.Look(ref lastInCombat, "lastInCombat", false);
            Scribe_Values.Look(ref lastColonyWealth, "lastColonyWealth", 0f);
            Scribe_Values.Look(ref lastFoodCount, "lastFoodCount", 0);
        }
    }
    
    /// <summary>
    /// 高频状态数据结构
    /// </summary>
    public class HighFreqState
    {
        public float AverageMood;
        public int ColonistCount;
        public int HealthyCount;
        public int DownedCount;
        public bool InCombat;
        public int LastUpdate;
    }
    
    /// <summary>
    /// 低频状态数据结构
    /// </summary>
    public class LowFreqState
    {
        public float ColonyWealth;
        public int FoodCount;
        public int LastUpdate;
    }
    
    /// <summary>
    /// 游戏事件类型
    /// </summary>
    public enum GameEventType
    {
        CombatStarted,
        CombatEnded,
        ColonistJoined,
        ColonistLost,
        RaidStarted,
        LetterReceived,
        ResourceLow,
        MoodCrisis
    }
    
    /// <summary>
    /// 游戏事件
    /// </summary>
    public struct GameEvent
    {
        public GameEventType Type;
        public string Description;
        public int GameTick;
    }
    
    /// <summary>
    /// ⭐ 事件监听补丁：监听游戏内事件
    /// RimWorld 1.5: ReceiveLetter(Letter let, string debugInfo, int delayTicks, bool playSound)
    /// </summary>
    [HarmonyPatch(typeof(LetterStack), "ReceiveLetter", typeof(Letter), typeof(string), typeof(int), typeof(bool))]
    public static class LetterStack_ReceiveLetter_Patch
    {
        public static void Postfix(Letter let)
        {
            try
            {
                var observer = Current.Game?.GetComponent<GameStateObserver>();
                if (observer != null)
                {
                    // 触发全量扫描
                    observer.TriggerFullScan();
                }
            }
            catch { }
        }
    }
}