using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using RimWorld;

namespace TheSecondSeat.Comps
{
    /// <summary>
    /// ⭐ 挂载在 Shadow Pawn 上的记忆组件
    /// 利用 RimWorld 原生存档系统保存叙事者的私有数据
    ///
    /// 核心功能:
    /// 1. NarrativeKVStore - 键值对存储（支持 Scriban 模板访问）
    /// 2. 事件记忆 - 记录重要的叙事事件
    /// 3. 承诺追踪 - 追踪 AI 对玩家的承诺
    /// 4. 关系历史 - 好感度变化记录
    /// </summary>
    public class CompNarratorMemory : ThingComp
    {
        // ============================================
        // 核心数据存储
        // ============================================
        
        /// <summary>
        /// ⭐ 叙事键值存储
        /// 可通过 Scriban 模板访问: {{ memory.promised_drop_pod }}
        /// </summary>
        private Dictionary<string, string> narrativeKVStore = new Dictionary<string, string>();
        
        /// <summary>
        /// 事件记忆列表（最多保留100条）
        /// </summary>
        private List<NarrativeMemoryEntry> eventMemories = new List<NarrativeMemoryEntry>();
        
        /// <summary>
        /// AI 对玩家的承诺
        /// </summary>
        private List<NarratorPromise> promises = new List<NarratorPromise>();
        
        /// <summary>
        /// 好感度变化历史（最多保留50条）
        /// </summary>
        private List<AffinityChangeRecord> affinityHistory = new List<AffinityChangeRecord>();
        
        /// <summary>
        /// 重要标记（如：首次见面、首次战斗等）
        /// </summary>
        private HashSet<string> milestones = new HashSet<string>();
        
        // ============================================
        // 配置常量
        // ============================================
        
        private const int MaxEventMemories = 100;
        private const int MaxAffinityHistory = 50;
        private const int MaxPromises = 20;
        
        // ============================================
        // 公共 API - 键值存储
        // ============================================
        
        /// <summary>
        /// 设置记忆值
        /// </summary>
        public void Set(string key, string value)
        {
            if (string.IsNullOrEmpty(key)) return;
            narrativeKVStore[key] = value;
        }
        
        /// <summary>
        /// 获取记忆值
        /// </summary>
        public string Get(string key, string defaultValue = "")
        {
            if (string.IsNullOrEmpty(key)) return defaultValue;
            return narrativeKVStore.TryGetValue(key, out string value) ? value : defaultValue;
        }
        
        /// <summary>
        /// 检查是否存在某个键
        /// </summary>
        public bool Has(string key)
        {
            return !string.IsNullOrEmpty(key) && narrativeKVStore.ContainsKey(key);
        }
        
        /// <summary>
        /// 删除记忆
        /// </summary>
        public bool Remove(string key)
        {
            return narrativeKVStore.Remove(key);
        }
        
        /// <summary>
        /// 获取所有键值对（用于 Scriban 模板）
        /// </summary>
        public Dictionary<string, string> GetAllKV()
        {
            return new Dictionary<string, string>(narrativeKVStore);
        }
        
        /// <summary>
        /// 设置数值类型的记忆
        /// </summary>
        public void SetInt(string key, int value) => Set(key, value.ToString());
        public void SetFloat(string key, float value) => Set(key, value.ToString("F2"));
        public void SetBool(string key, bool value) => Set(key, value.ToString().ToLower());
        
        /// <summary>
        /// 获取数值类型的记忆
        /// </summary>
        public int GetInt(string key, int defaultValue = 0)
        {
            string val = Get(key);
            return int.TryParse(val, out int result) ? result : defaultValue;
        }
        
        public float GetFloat(string key, float defaultValue = 0f)
        {
            string val = Get(key);
            return float.TryParse(val, out float result) ? result : defaultValue;
        }
        
        public bool GetBool(string key, bool defaultValue = false)
        {
            string val = Get(key);
            return bool.TryParse(val, out bool result) ? result : defaultValue;
        }
        
        // ============================================
        // 公共 API - 事件记忆
        // ============================================
        
        /// <summary>
        /// 记录一个叙事事件
        /// </summary>
        public void RecordEvent(string eventType, string description, int importance = 1)
        {
            var entry = new NarrativeMemoryEntry
            {
                eventType = eventType,
                description = description,
                importance = importance,
                tickRecorded = Find.TickManager?.TicksGame ?? 0,
                dayRecorded = GenDate.DaysPassed
            };
            
            eventMemories.Add(entry);
            
            // 限制数量，移除最旧的低重要性记忆
            while (eventMemories.Count > MaxEventMemories)
            {
                var toRemove = eventMemories
                    .OrderBy(e => e.importance)
                    .ThenBy(e => e.tickRecorded)
                    .First();
                eventMemories.Remove(toRemove);
            }
        }
        
        /// <summary>
        /// 获取最近的事件记忆
        /// </summary>
        public List<NarrativeMemoryEntry> GetRecentEvents(int count = 10, string eventType = null)
        {
            var query = eventMemories.AsEnumerable();
            
            if (!string.IsNullOrEmpty(eventType))
            {
                query = query.Where(e => e.eventType == eventType);
            }
            
            return query
                .OrderByDescending(e => e.tickRecorded)
                .Take(count)
                .ToList();
        }
        
        /// <summary>
        /// 获取重要事件（importance >= 3）
        /// </summary>
        public List<NarrativeMemoryEntry> GetImportantEvents(int count = 5)
        {
            return eventMemories
                .Where(e => e.importance >= 3)
                .OrderByDescending(e => e.tickRecorded)
                .Take(count)
                .ToList();
        }
        
        // ============================================
        // 公共 API - 承诺系统
        // ============================================
        
        /// <summary>
        /// 记录一个承诺
        /// </summary>
        public void MakePromise(string promiseType, string description, int dueDay = -1)
        {
            var promise = new NarratorPromise
            {
                promiseType = promiseType,
                description = description,
                dayMade = GenDate.DaysPassed,
                dueDay = dueDay,
                fulfilled = false
            };
            
            promises.Add(promise);
            
            // 限制数量
            while (promises.Count > MaxPromises)
            {
                promises.RemoveAt(0);
            }
            
            // 同时记录为事件
            RecordEvent("Promise", $"承诺: {description}", 2);
        }
        
        /// <summary>
        /// 标记承诺已完成
        /// </summary>
        public bool FulfillPromise(string promiseType)
        {
            var promise = promises.FirstOrDefault(p => p.promiseType == promiseType && !p.fulfilled);
            if (promise != null)
            {
                promise.fulfilled = true;
                promise.fulfilledDay = GenDate.DaysPassed;
                RecordEvent("PromiseFulfilled", $"兑现: {promise.description}", 2);
                return true;
            }
            return false;
        }
        
        /// <summary>
        /// 获取未完成的承诺
        /// </summary>
        public List<NarratorPromise> GetPendingPromises()
        {
            return promises.Where(p => !p.fulfilled).ToList();
        }
        
        /// <summary>
        /// 获取过期的承诺（已超过 dueDay）
        /// </summary>
        public List<NarratorPromise> GetOverduePromises()
        {
            int today = GenDate.DaysPassed;
            return promises
                .Where(p => !p.fulfilled && p.dueDay > 0 && p.dueDay < today)
                .ToList();
        }
        
        // ============================================
        // 公共 API - 好感度历史
        // ============================================
        
        /// <summary>
        /// 记录好感度变化
        /// </summary>
        public void RecordAffinityChange(float oldValue, float newValue, string reason)
        {
            var record = new AffinityChangeRecord
            {
                oldValue = oldValue,
                newValue = newValue,
                change = newValue - oldValue,
                reason = reason,
                tickRecorded = Find.TickManager?.TicksGame ?? 0,
                dayRecorded = GenDate.DaysPassed
            };
            
            affinityHistory.Add(record);
            
            while (affinityHistory.Count > MaxAffinityHistory)
            {
                affinityHistory.RemoveAt(0);
            }
        }
        
        /// <summary>
        /// 获取好感度变化趋势（最近N天的平均变化）
        /// </summary>
        public float GetAffinityTrend(int days = 7)
        {
            int cutoffDay = GenDate.DaysPassed - days;
            var recentChanges = affinityHistory.Where(r => r.dayRecorded >= cutoffDay).ToList();
            
            if (recentChanges.Count == 0) return 0;
            return recentChanges.Sum(r => r.change) / recentChanges.Count;
        }
        
        // ============================================
        // 公共 API - 里程碑
        // ============================================
        
        /// <summary>
        /// 标记一个里程碑
        /// </summary>
        public void MarkMilestone(string milestone)
        {
            if (milestones.Add(milestone))
            {
                RecordEvent("Milestone", milestone, 3);
            }
        }
        
        /// <summary>
        /// 检查里程碑是否已达成
        /// </summary>
        public bool HasMilestone(string milestone)
        {
            return milestones.Contains(milestone);
        }
        
        /// <summary>
        /// 获取所有里程碑
        /// </summary>
        public List<string> GetAllMilestones()
        {
            return milestones.ToList();
        }
        
        // ============================================
        // Scriban 集成
        // ============================================
        
        /// <summary>
        /// 获取用于 Scriban 模板的记忆对象
        /// </summary>
        public MemoryScribanWrapper GetScribanWrapper()
        {
            return new MemoryScribanWrapper(this);
        }
        
        // ============================================
        // 存档系统
        // ============================================
        
        public override void PostExposeData()
        {
            base.PostExposeData();
            
            // 保存键值存储
            Scribe_Collections.Look(ref narrativeKVStore, "narrativeKVStore", LookMode.Value, LookMode.Value);
            
            // 保存事件记忆
            Scribe_Collections.Look(ref eventMemories, "eventMemories", LookMode.Deep);
            
            // 保存承诺
            Scribe_Collections.Look(ref promises, "promises", LookMode.Deep);
            
            // 保存好感度历史
            Scribe_Collections.Look(ref affinityHistory, "affinityHistory", LookMode.Deep);
            
            // 保存里程碑
            Scribe_Collections.Look(ref milestones, "milestones", LookMode.Value);
            
            // 初始化为空集合（加载失败时的兜底）
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                narrativeKVStore ??= new Dictionary<string, string>();
                eventMemories ??= new List<NarrativeMemoryEntry>();
                promises ??= new List<NarratorPromise>();
                affinityHistory ??= new List<AffinityChangeRecord>();
                milestones ??= new HashSet<string>();
            }
        }
        
        /// <summary>
        /// 获取调试信息
        /// </summary>
        public string GetDebugInfo()
        {
            return $"[CompNarratorMemory] KV: {narrativeKVStore.Count}, Events: {eventMemories.Count}, " +
                   $"Promises: {promises.Count} ({GetPendingPromises().Count} pending), " +
                   $"Milestones: {milestones.Count}";
        }
    }
    
    // ============================================
    // 数据结构
    // ============================================
    
    /// <summary>
    /// 叙事事件记忆条目
    /// </summary>
    public class NarrativeMemoryEntry : IExposable
    {
        public string eventType;
        public string description;
        public int importance;
        public int tickRecorded;
        public int dayRecorded;
        
        public void ExposeData()
        {
            Scribe_Values.Look(ref eventType, "eventType", "");
            Scribe_Values.Look(ref description, "description", "");
            Scribe_Values.Look(ref importance, "importance", 1);
            Scribe_Values.Look(ref tickRecorded, "tickRecorded", 0);
            Scribe_Values.Look(ref dayRecorded, "dayRecorded", 0);
        }
        
        public override string ToString()
        {
            return $"[Day {dayRecorded}] {eventType}: {description}";
        }
    }
    
    /// <summary>
    /// AI 承诺记录
    /// </summary>
    public class NarratorPromise : IExposable
    {
        public string promiseType;
        public string description;
        public int dayMade;
        public int dueDay;
        public bool fulfilled;
        public int fulfilledDay;
        
        public void ExposeData()
        {
            Scribe_Values.Look(ref promiseType, "promiseType", "");
            Scribe_Values.Look(ref description, "description", "");
            Scribe_Values.Look(ref dayMade, "dayMade", 0);
            Scribe_Values.Look(ref dueDay, "dueDay", -1);
            Scribe_Values.Look(ref fulfilled, "fulfilled", false);
            Scribe_Values.Look(ref fulfilledDay, "fulfilledDay", 0);
        }
        
        public bool IsOverdue => !fulfilled && dueDay > 0 && dueDay < GenDate.DaysPassed;
        
        public override string ToString()
        {
            string status = fulfilled ? "✓" : (IsOverdue ? "⚠ OVERDUE" : "...");
            return $"[{status}] {promiseType}: {description}";
        }
    }
    
    /// <summary>
    /// 好感度变化记录
    /// </summary>
    public class AffinityChangeRecord : IExposable
    {
        public float oldValue;
        public float newValue;
        public float change;
        public string reason;
        public int tickRecorded;
        public int dayRecorded;
        
        public void ExposeData()
        {
            Scribe_Values.Look(ref oldValue, "oldValue", 0f);
            Scribe_Values.Look(ref newValue, "newValue", 0f);
            Scribe_Values.Look(ref change, "change", 0f);
            Scribe_Values.Look(ref reason, "reason", "");
            Scribe_Values.Look(ref tickRecorded, "tickRecorded", 0);
            Scribe_Values.Look(ref dayRecorded, "dayRecorded", 0);
        }
    }
    
    /// <summary>
    /// Scriban 模板包装器
    /// 允许在模板中使用 {{ memory.key }} 语法访问记忆
    /// </summary>
    public class MemoryScribanWrapper
    {
        private readonly CompNarratorMemory _memory;
        
        public MemoryScribanWrapper(CompNarratorMemory memory)
        {
            _memory = memory;
        }
        
        /// <summary>
        /// 索引器，支持 {{ memory["key"] }} 或 {{ memory.key }} 语法
        /// </summary>
        public string this[string key] => _memory.Get(key);
        
        /// <summary>
        /// 获取所有键值对
        /// </summary>
        public Dictionary<string, string> All => _memory.GetAllKV();
        
        /// <summary>
        /// 获取待完成的承诺
        /// </summary>
        public List<NarratorPromise> PendingPromises => _memory.GetPendingPromises();
        
        /// <summary>
        /// 获取过期的承诺
        /// </summary>
        public List<NarratorPromise> OverduePromises => _memory.GetOverduePromises();
        
        /// <summary>
        /// 获取最近的事件
        /// </summary>
        public List<NarrativeMemoryEntry> RecentEvents => _memory.GetRecentEvents(5);
        
        /// <summary>
        /// 获取重要事件
        /// </summary>
        public List<NarrativeMemoryEntry> ImportantEvents => _memory.GetImportantEvents(3);
        
        /// <summary>
        /// 获取好感度趋势
        /// </summary>
        public float AffinityTrend => _memory.GetAffinityTrend();
        
        /// <summary>
        /// 检查里程碑
        /// </summary>
        public bool HasMilestone(string milestone) => _memory.HasMilestone(milestone);
    }
    
    /// <summary>
    /// CompProperties 定义
    /// </summary>
    public class CompProperties_NarratorMemory : CompProperties
    {
        public CompProperties_NarratorMemory()
        {
            this.compClass = typeof(CompNarratorMemory);
        }
    }
}