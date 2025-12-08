using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace TheSecondSeat.Integration
{
    /// <summary>
    /// RimTalk 集成接口 - 用于与 RimTalk Expand Memory 模组通信
    /// </summary>
    public interface IRimTalkMemoryProvider
    {
        /// <summary>
        /// 添加对话记忆到 RimTalk 系统
        /// </summary>
        void AddConversationMemory(string speaker, string message, MemoryImportance importance);

        /// <summary>
        /// 添加事件记忆
        /// </summary>
        void AddEventMemory(string eventDescription, MemoryImportance importance);

        /// <summary>
        /// 获取相关记忆（用于上下文注入）
        /// </summary>
        List<MemoryEntry> GetRelevantMemories(string query, int maxTokens);

        /// <summary>
        /// 清理旧记忆
        /// </summary>
        void PruneOldMemories(int keepCount);

        /// <summary>
        /// 检查 RimTalk 是否可用
        /// </summary>
        bool IsRimTalkAvailable();
    }

    /// <summary>
    /// 记忆重要性等级
    /// </summary>
    public enum MemoryImportance
    {
        Trivial = 0,      // 琐碎 - 很快被遗忘
        Low = 1,          // 低 - 短期记忆
        Medium = 2,       // 中 - 中期记忆
        High = 3,         // 高 - 长期记忆
        Critical = 4      // 关键 - 永久记忆
    }

    /// <summary>
    /// 记忆条目
    /// </summary>
    public class MemoryEntry : IExposable
    {
        public string id = Guid.NewGuid().ToString();
        public int timestamp;
        public string content = "";
        public MemoryImportance importance = MemoryImportance.Medium;
        public int accessCount = 0;
        public int lastAccessTick = 0;
        public Dictionary<string, float> tags = new Dictionary<string, float>();

        public void ExposeData()
        {
            Scribe_Values.Look(ref id, "id");
            Scribe_Values.Look(ref timestamp, "timestamp");
            Scribe_Values.Look(ref content, "content", "");
            Scribe_Values.Look(ref importance, "importance", MemoryImportance.Medium);
            Scribe_Values.Look(ref accessCount, "accessCount", 0);
            Scribe_Values.Look(ref lastAccessTick, "lastAccessTick", 0);
            
            // 确保 tags 不为 null
            if (Scribe.mode == LoadSaveMode.LoadingVars && tags == null)
            {
                tags = new Dictionary<string, float>();
            }
            
            Scribe_Collections.Look(ref tags, "tags", LookMode.Value, LookMode.Value);
            
            // 加载后再次确保不为 null
            if (tags == null)
            {
                tags = new Dictionary<string, float>();
            }
        }
    }

    /// <summary>
    /// RimTalk 集成适配器 - 默认实现（如果 RimTalk 未安装则使用内置系统）
    /// </summary>
    public class RimTalkMemoryAdapter : IRimTalkMemoryProvider
    {
        private static RimTalkMemoryAdapter? instance;
        public static RimTalkMemoryAdapter Instance => instance ??= new RimTalkMemoryAdapter();

        private List<MemoryEntry> internalMemory = new List<MemoryEntry>();
        private const int MaxInternalMemories = 100;
        private const int MaxTokensPerMemory = 150;

        private bool rimTalkChecked = false;
        private bool rimTalkAvailable = false;

        public bool IsRimTalkAvailable()
        {
            if (!rimTalkChecked)
            {
                CheckRimTalkAvailability();
            }
            return rimTalkAvailable;
        }

        private void CheckRimTalkAvailability()
        {
            rimTalkChecked = true;
            
            // 检查 RimTalk 模组是否加载
            var modContentPack = LoadedModManager.RunningMods.FirstOrDefault(m => 
                m.PackageId.ToLower().Contains("rimtalk"));

            rimTalkAvailable = modContentPack != null;

            if (rimTalkAvailable)
            {
                Log.Message("[The Second Seat] RimTalk 模组已检测到，将使用 RimTalk 记忆系统");
            }
            else
            {
                Log.Message("[The Second Seat] 未检测到 RimTalk，使用内置记忆系统");
            }
        }

        public void AddConversationMemory(string speaker, string message, MemoryImportance importance)
        {
            if (IsRimTalkAvailable())
            {
                // 调用 RimTalk API
                TryAddToRimTalk($"{speaker}: {message}", importance);
            }
            else
            {
                // 使用内置系统
                AddToInternalMemory($"[对话] {speaker}: {message}", importance);
            }
        }

        public void AddEventMemory(string eventDescription, MemoryImportance importance)
        {
            if (IsRimTalkAvailable())
            {
                TryAddToRimTalk($"[事件] {eventDescription}", importance);
            }
            else
            {
                AddToInternalMemory($"[事件] {eventDescription}", importance);
            }
        }

        public List<MemoryEntry> GetRelevantMemories(string query, int maxTokens)
        {
            if (IsRimTalkAvailable())
            {
                return GetFromRimTalk(query, maxTokens);
            }
            else
            {
                return GetFromInternalMemory(query, maxTokens);
            }
        }

        public void PruneOldMemories(int keepCount)
        {
            if (!IsRimTalkAvailable())
            {
                // 内置系统：按重要性和时间排序，保留最重要的
                var sorted = internalMemory
                    .OrderByDescending(m => (int)m.importance * 100 + m.accessCount)
                    .ThenByDescending(m => m.timestamp)
                    .ToList();

                if (sorted.Count > keepCount)
                {
                    internalMemory = sorted.Take(keepCount).ToList();
                }
            }
        }

        // === 内置记忆系统 ===

        private void AddToInternalMemory(string content, MemoryImportance importance)
        {
            var entry = new MemoryEntry
            {
                timestamp = Find.TickManager.TicksGame,
                content = content,
                importance = importance
            };

            // 自动标签提取（简单关键词）
            ExtractTags(entry);

            internalMemory.Add(entry);

            // 超出限制时清理
            if (internalMemory.Count > MaxInternalMemories)
            {
                PruneOldMemories(MaxInternalMemories - 20);
            }

            Log.Message($"[Memory] 添加记忆: {content.Substring(0, Math.Min(50, content.Length))}...");
        }

        private List<MemoryEntry> GetFromInternalMemory(string query, int maxTokens)
        {
            var results = new List<MemoryEntry>();
            int currentTokens = 0;

            // 简单相关性评分
            var scored = internalMemory
                .Select(m => new { Memory = m, Score = CalculateRelevance(m, query) })
                .Where(s => s.Score > 0.1f)
                .OrderByDescending(s => s.Score)
                .ToList();

            foreach (var item in scored)
            {
                int tokens = EstimateTokens(item.Memory.content);
                if (currentTokens + tokens > maxTokens)
                    break;

                results.Add(item.Memory);
                currentTokens += tokens;

                // 更新访问记录
                item.Memory.accessCount++;
                item.Memory.lastAccessTick = Find.TickManager.TicksGame;
            }

            return results;
        }

        private float CalculateRelevance(MemoryEntry memory, string query)
        {
            float score = 0f;

            // 关键词匹配
            var queryWords = query.ToLower().Split(' ');
            var memoryWords = memory.content.ToLower().Split(' ');

            int matches = queryWords.Count(qw => memoryWords.Any(mw => mw.Contains(qw)));
            score += matches * 0.3f;

            // 重要性加成
            score += (int)memory.importance * 0.2f;

            // 访问频率加成
            score += Math.Min(memory.accessCount * 0.05f, 0.5f);

            // 时间衰减
            int age = Find.TickManager.TicksGame - memory.timestamp;
            float ageInDays = age / 60000f;
            score *= Math.Max(0.3f, 1f - (ageInDays * 0.1f));

            return score;
        }

        private void ExtractTags(MemoryEntry entry)
        {
            // 简单标签提取（可扩展为更复杂的NLP）
            var keywords = new[] { "袭击", "贸易", "殖民者", "死亡", "建造", "收获", "火灾", "疾病" };
            
            foreach (var keyword in keywords)
            {
                if (entry.content.Contains(keyword))
                {
                    entry.tags[keyword] = 1f;
                }
            }
        }

        private int EstimateTokens(string text)
        {
            // 简单估算：中文 1 字 ≈ 2 tokens，英文 4 字母 ≈ 1 token
            int chineseChars = text.Count(c => c >= 0x4E00 && c <= 0x9FFF);
            int otherChars = text.Length - chineseChars;
            return chineseChars * 2 + otherChars / 4;
        }

        // === RimTalk 集成（需要反射调用） ===

        private void TryAddToRimTalk(string content, MemoryImportance importance)
        {
            try
            {
                // 使用反射调用 RimTalk API
                // var rimTalkMemory = GenTypes.GetTypeInAnyAssembly("RimTalk.ExpandMemory.MemoryManager");
                // if (rimTalkMemory != null)
                // {
                //     var method = rimTalkMemory.GetMethod("AddMemory");
                //     method?.Invoke(null, new object[] { content, (int)importance });
                // }

                // 临时：使用内置系统
                AddToInternalMemory(content, importance);
            }
            catch (Exception ex)
            {
                Log.Warning($"[The Second Seat] RimTalk 集成失败: {ex.Message}");
                AddToInternalMemory(content, importance);
            }
        }

        private List<MemoryEntry> GetFromRimTalk(string query, int maxTokens)
        {
            try
            {
                // 使用反射调用 RimTalk API
                // var rimTalkMemory = GenTypes.GetTypeInAnyAssembly("RimTalk.ExpandMemory.MemoryManager");
                // if (rimTalkMemory != null)
                // {
                //     var method = rimTalkMemory.GetMethod("GetRelevantMemories");
                //     var result = method?.Invoke(null, new object[] { query, maxTokens });
                //     return ConvertFromRimTalkFormat(result);
                // }

                // 临时：使用内置系统
                return GetFromInternalMemory(query, maxTokens);
            }
            catch (Exception ex)
            {
                Log.Warning($"[The Second Seat] RimTalk 查询失败: {ex.Message}");
                return GetFromInternalMemory(query, maxTokens);
            }
        }
    }

    /// <summary>
    /// 记忆上下文构建器 - 用于 LLM Prompt 注入
    /// </summary>
    public static class MemoryContextBuilder
    {
        /// <summary>
        /// ? 构建记忆上下文（增强版 - 支持叙事者模式和 Pawn 模式）
        /// </summary>
        public static string BuildMemoryContext(string currentQuery, int maxTokens = 1000)
        {
            // ? 使用新的 SimpleRimTalkIntegration（叙事者模式）
            return SimpleRimTalkIntegration.GetMemoryPrompt(
                basePrompt: "",
                pawn: null,  // 叙事者模式：只有共通知识 + 全局状态
                maxKnowledgeEntries: maxTokens / 100  // 估算：每条 ~100 tokens
            );
        }

        /// <summary>
        /// ? 新增：为特定 Pawn 构建记忆上下文
        /// </summary>
        public static string BuildMemoryContextForPawn(Pawn pawn, string currentQuery, int maxTokens = 1000)
        {
            if (pawn == null)
            {
                return BuildMemoryContext(currentQuery, maxTokens);
            }

            return SimpleRimTalkIntegration.GetMemoryPrompt(
                basePrompt: "",
                pawn: pawn,  // Pawn 模式：个人记忆 + 共通知识
                maxPersonalMemories: 5,
                maxKnowledgeEntries: 3
            );
        }

        /// <summary>
        /// 记录对话到记忆系统
        /// </summary>
        public static void RecordConversation(string speaker, string message, bool isImportant = false)
        {
            var importance = isImportant ? MemoryImportance.High : MemoryImportance.Medium;
            RimTalkMemoryAdapter.Instance.AddConversationMemory(speaker, message, importance);
        }

        /// <summary>
        /// 记录游戏事件
        /// </summary>
        public static void RecordEvent(string eventDescription, MemoryImportance importance = MemoryImportance.Medium)
        {
            RimTalkMemoryAdapter.Instance.AddEventMemory(eventDescription, importance);
        }
    }
}
