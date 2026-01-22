using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;
using RimWorld;

namespace TheSecondSeat.Monitoring
{
    /// <summary>
    /// 事件聚合器 - 低 Token 消耗的监控系统
    /// 
    /// 设计原则：
    /// 1. 本地聚合：在本地收集和分类事件，减少 LLM 调用频率
    /// 2. 规则引擎：使用规则预处理，只在关键事件时触发 LLM
    /// 3. 摘要生成：将多个事件压缩为简短摘要
    /// 4. 优先级队列：按重要性排序，优先处理高优先级事件
    /// </summary>
    public class EventAggregator : GameComponent
    {
        // 事件缓冲区
        private List<GameEvent> eventBuffer = new List<GameEvent>();
        private const int MAX_BUFFER_SIZE = 100;
        
        // 聚合周期（游戏 Tick）
        private const int AGGREGATION_INTERVAL = 2500; // 约 1 游戏小时
        private int lastAggregationTick = 0;
        
        // 事件统计
        private Dictionary<EventCategory, int> eventCounts = new Dictionary<EventCategory, int>();
        private Dictionary<string, float> pawnMoodTrends = new Dictionary<string, float>();
        
        // 触发阈值
        private const int CRITICAL_EVENT_THRESHOLD = 1;  // 关键事件立即触发
        private const int NORMAL_EVENT_THRESHOLD = 5;    // 普通事件累积触发
        private const float MOOD_CHANGE_THRESHOLD = 0.15f; // 心情变化阈值
        
        // 回调
        public static Action<string, EventPriority> OnEventSummaryReady;
        
        public EventAggregator(Game game) { }
        
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref eventBuffer, "eventBuffer", LookMode.Deep);
            Scribe_Values.Look(ref lastAggregationTick, "lastAggregationTick");
        }
        
        public override void GameComponentTick()
        {
            int currentTick = Find.TickManager.TicksGame;
            
            // 定期聚合
            if (currentTick - lastAggregationTick >= AGGREGATION_INTERVAL)
            {
                ProcessAggregation();
                lastAggregationTick = currentTick;
            }
        }
        
        /// <summary>
        /// 记录游戏事件
        /// </summary>
        public void RecordEvent(GameEvent evt)
        {
            if (evt == null) return;
            
            // 添加到缓冲区
            eventBuffer.Add(evt);
            
            // 更新统计
            if (!eventCounts.ContainsKey(evt.Category))
                eventCounts[evt.Category] = 0;
            eventCounts[evt.Category]++;
            
            // 检查是否需要立即触发
            if (evt.Priority == EventPriority.Critical)
            {
                TriggerImmediateNotification(evt);
            }
            
            // 缓冲区溢出保护
            if (eventBuffer.Count > MAX_BUFFER_SIZE)
            {
                // 移除最旧的低优先级事件
                var toRemove = eventBuffer
                    .Where(e => e.Priority == EventPriority.Low)
                    .OrderBy(e => e.Timestamp)
                    .Take(20)
                    .ToList();
                foreach (var e in toRemove)
                    eventBuffer.Remove(e);
            }
        }
        
        /// <summary>
        /// 快捷方法：记录简单事件
        /// </summary>
        public void RecordSimpleEvent(string description, EventCategory category, EventPriority priority = EventPriority.Normal)
        {
            RecordEvent(new GameEvent
            {
                Description = description,
                Category = category,
                Priority = priority,
                Timestamp = Find.TickManager.TicksGame
            });
        }
        
        /// <summary>
        /// 处理聚合
        /// </summary>
        private void ProcessAggregation()
        {
            if (eventBuffer.Count == 0) return;
            
            // 按类别分组
            var groupedEvents = eventBuffer.GroupBy(e => e.Category).ToList();
            
            // 生成摘要
            var summaryBuilder = new StringBuilder();
            var highestPriority = EventPriority.Low;
            
            foreach (var group in groupedEvents)
            {
                var categoryEvents = group.ToList();
                if (categoryEvents.Count == 0) continue;
                
                // 更新最高优先级
                var maxPriority = categoryEvents.Max(e => e.Priority);
                if (maxPriority > highestPriority)
                    highestPriority = maxPriority;
                
                // 生成类别摘要
                string categorySummary = GenerateCategorySummary(group.Key, categoryEvents);
                if (!string.IsNullOrEmpty(categorySummary))
                {
                    summaryBuilder.AppendLine(categorySummary);
                }
            }
            
            // 添加心情趋势
            string moodSummary = GenerateMoodTrendSummary();
            if (!string.IsNullOrEmpty(moodSummary))
            {
                summaryBuilder.AppendLine(moodSummary);
            }
            
            // 触发回调
            string finalSummary = summaryBuilder.ToString().Trim();
            if (!string.IsNullOrEmpty(finalSummary))
            {
                OnEventSummaryReady?.Invoke(finalSummary, highestPriority);
            }
            
            // 清理缓冲区（保留高优先级事件）
            eventBuffer.RemoveAll(e => e.Priority < EventPriority.High);
            eventCounts.Clear();
        }
        
        /// <summary>
        /// 生成类别摘要（压缩多个事件为简短描述）
        /// </summary>
        private string GenerateCategorySummary(EventCategory category, List<GameEvent> events)
        {
            if (events.Count == 0) return null;
            
            switch (category)
            {
                case EventCategory.Combat:
                    return GenerateCombatSummary(events);
                case EventCategory.Social:
                    return GenerateSocialSummary(events);
                case EventCategory.Health:
                    return GenerateHealthSummary(events);
                case EventCategory.Resource:
                    return GenerateResourceSummary(events);
                case EventCategory.Construction:
                    return GenerateConstructionSummary(events);
                case EventCategory.Mood:
                    return GenerateMoodSummary(events);
                default:
                    return GenerateGenericSummary(category, events);
            }
        }
        
        private string GenerateCombatSummary(List<GameEvent> events)
        {
            int count = events.Count;
            var criticalEvents = events.Where(e => e.Priority >= EventPriority.High).ToList();
            
            if (criticalEvents.Any())
            {
                return $"[战斗] {criticalEvents.First().Description}";
            }
            
            return count > 1 ? $"[战斗] 发生了 {count} 次战斗相关事件" : null;
        }
        
        private string GenerateSocialSummary(List<GameEvent> events)
        {
            int count = events.Count;
            if (count < 3) return null; // 社交事件需要累积
            
            var positiveCount = events.Count(e => e.Tags?.Contains("positive") == true);
            var negativeCount = events.Count(e => e.Tags?.Contains("negative") == true);
            
            if (negativeCount > positiveCount * 2)
            {
                return $"[社交] 殖民地社交氛围紧张，发生了 {negativeCount} 次负面互动";
            }
            else if (positiveCount > negativeCount * 2)
            {
                return $"[社交] 殖民地社交氛围良好，发生了 {positiveCount} 次正面互动";
            }
            
            return null;
        }
        
        private string GenerateHealthSummary(List<GameEvent> events)
        {
            var injuries = events.Where(e => e.Tags?.Contains("injury") == true).ToList();
            var diseases = events.Where(e => e.Tags?.Contains("disease") == true).ToList();
            var deaths = events.Where(e => e.Tags?.Contains("death") == true).ToList();
            
            var parts = new List<string>();
            
            if (deaths.Any())
            {
                parts.Add($"{deaths.Count} 人死亡");
            }
            if (injuries.Count >= 3)
            {
                parts.Add($"{injuries.Count} 人受伤");
            }
            if (diseases.Any())
            {
                parts.Add($"{diseases.Count} 人患病");
            }
            
            return parts.Any() ? $"[健康] {string.Join("，", parts)}" : null;
        }
        
        private string GenerateResourceSummary(List<GameEvent> events)
        {
            var shortages = events.Where(e => e.Tags?.Contains("shortage") == true).ToList();
            var gains = events.Where(e => e.Tags?.Contains("gain") == true).ToList();
            
            if (shortages.Any())
            {
                return $"[资源] 资源短缺警告：{shortages.First().Description}";
            }
            
            return null;
        }
        
        private string GenerateConstructionSummary(List<GameEvent> events)
        {
            int completed = events.Count(e => e.Tags?.Contains("completed") == true);
            int failed = events.Count(e => e.Tags?.Contains("failed") == true);
            
            if (failed > 0)
            {
                return $"[建筑] {failed} 个建筑项目失败";
            }
            if (completed >= 3)
            {
                return $"[建筑] 完成了 {completed} 个建筑项目";
            }
            
            return null;
        }
        
        private string GenerateMoodSummary(List<GameEvent> events)
        {
            var breakdowns = events.Where(e => e.Tags?.Contains("breakdown") == true).ToList();
            var inspirations = events.Where(e => e.Tags?.Contains("inspiration") == true).ToList();
            
            if (breakdowns.Any())
            {
                return $"[心情] {breakdowns.Count} 人精神崩溃";
            }
            if (inspirations.Any())
            {
                return $"[心情] {inspirations.Count} 人获得灵感";
            }
            
            return null;
        }
        
        private string GenerateGenericSummary(EventCategory category, List<GameEvent> events)
        {
            if (events.Count < 3) return null;
            return $"[{category}] 发生了 {events.Count} 个相关事件";
        }
        
        /// <summary>
        /// 生成心情趋势摘要
        /// </summary>
        private string GenerateMoodTrendSummary()
        {
            if (Find.Maps == null || Find.Maps.Count == 0) return null;
            
            var currentMoods = new Dictionary<string, float>();
            
            foreach (var map in Find.Maps)
            {
                if (map.mapPawns == null) continue;
                foreach (var pawn in map.mapPawns.FreeColonists)
                {
                    if (pawn?.needs?.mood == null) continue;
                    string key = pawn.ThingID;
                    float currentMood = pawn.needs.mood.CurLevel;
                    currentMoods[key] = currentMood;
                    
                    // 检查趋势
                    if (pawnMoodTrends.TryGetValue(key, out float previousMood))
                    {
                        float change = currentMood - previousMood;
                        if (Math.Abs(change) > MOOD_CHANGE_THRESHOLD)
                        {
                            string pawnName = pawn.Name?.ToStringShort ?? pawn.LabelShort;
                            if (change < -MOOD_CHANGE_THRESHOLD)
                            {
                                RecordSimpleEvent($"{pawnName} 心情大幅下降", EventCategory.Mood, EventPriority.Normal);
                            }
                        }
                    }
                }
            }
            
            // 更新趋势
            pawnMoodTrends = currentMoods;
            
            // 计算整体心情
            if (currentMoods.Count > 0)
            {
                float avgMood = currentMoods.Values.Average();
                int lowMoodCount = currentMoods.Values.Count(m => m < 0.3f);
                
                if (lowMoodCount >= 3)
                {
                    return $"[心情趋势] {lowMoodCount} 名殖民者心情低落，需要关注";
                }
            }
            
            return null;
        }
        
        /// <summary>
        /// 立即触发通知（用于关键事件）
        /// </summary>
        private void TriggerImmediateNotification(GameEvent evt)
        {
            OnEventSummaryReady?.Invoke($"[紧急] {evt.Description}", EventPriority.Critical);
        }
        
        /// <summary>
        /// 获取当前事件统计
        /// </summary>
        public Dictionary<EventCategory, int> GetEventStats()
        {
            return new Dictionary<EventCategory, int>(eventCounts);
        }
        
        /// <summary>
        /// 获取缓冲区大小
        /// </summary>
        public int GetBufferSize() => eventBuffer.Count;
    }
    
    /// <summary>
    /// 游戏事件
    /// </summary>
    public class GameEvent : IExposable
    {
        public string Description;
        public EventCategory Category;
        public EventPriority Priority;
        public int Timestamp;
        public List<string> Tags;
        public string PawnId;
        
        public void ExposeData()
        {
            Scribe_Values.Look(ref Description, "description");
            Scribe_Values.Look(ref Category, "category");
            Scribe_Values.Look(ref Priority, "priority");
            Scribe_Values.Look(ref Timestamp, "timestamp");
            Scribe_Collections.Look(ref Tags, "tags", LookMode.Value);
            Scribe_Values.Look(ref PawnId, "pawnId");
        }
    }
    
    /// <summary>
    /// 事件类别
    /// </summary>
    public enum EventCategory
    {
        Combat,
        Social,
        Health,
        Resource,
        Construction,
        Mood,
        Work,
        Trade,
        Weather,
        Other
    }
    
    /// <summary>
    /// 事件优先级
    /// </summary>
    public enum EventPriority
    {
        Low = 0,
        Normal = 1,
        High = 2,
        Critical = 3
    }
}
