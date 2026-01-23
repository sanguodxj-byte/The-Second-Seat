using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;
using RimWorld;

namespace TheSecondSeat.Integration
{
    /// <summary>
    /// 简化的 RimTalk 集成 - 支持叙事者 AI (pawn == null) 访问共通知识库
    /// </summary>
    public static class SimpleRimTalkIntegration
    {
        // 缓存：避免重复生成相同的 Prompt
        private static Dictionary<string, string> PromptCache = new Dictionary<string, string>();
        private const int CacheDurationTicks = 3000; // 缓存持续 50 秒
        private static Dictionary<string, int> CacheTimestamp = new Dictionary<string, int>();

        /// <summary>
        /// 获取记忆增强的 Prompt
        /// ? 阶段一：移除阻塞，支持 pawn == null
        /// </summary>
        /// <param name="basePrompt">基础 Prompt</param>
        /// <param name="pawn">目标 Pawn（叙事者 AI 时为 null）</param>
        /// <param name="maxPersonalMemories">个人记忆最大数量（pawn != null 时）</param>
        /// <param name="maxKnowledgeEntries">共通知识最大数量</param>
        /// <returns>增强后的 Prompt</returns>
        public static string GetMemoryPrompt(
            string basePrompt,
            Pawn pawn = null,
            int maxPersonalMemories = 5,
            int maxKnowledgeEntries = 3)
        {
            // ? 阶段一：移除 pawn == null 的阻塞
            // if (pawn == null) return basePrompt; // ? 旧代码：阻塞叙事者 AI

            // 生成缓存键
            string cacheKey = pawn != null 
                ? $"{pawn.ThingID}_{maxPersonalMemories}_{maxKnowledgeEntries}" 
                : $"Storyteller_{maxKnowledgeEntries + 5}"; // ? 叙事者使用特殊键

            // 检查缓存是否有效
            if (PromptCache.TryGetValue(cacheKey, out string cachedPrompt) &&
                CacheTimestamp.TryGetValue(cacheKey, out int timestamp))
            {
                if (Find.TickManager.TicksGame - timestamp < CacheDurationTicks)
                {
                    return cachedPrompt;
                }
            }

            // 构建知识上下文
            StringBuilder knowledgeContext = new StringBuilder();
            knowledgeContext.AppendLine("### 可用知识库");
            knowledgeContext.AppendLine();

            try
            {
                // ? 阶段一：分离逻辑 - pawn != null 时注入个人记忆和共通知识
                if (pawn != null)
                {
                    // 1. 个人记忆（Personal Memories）
                    var personalMemories = DynamicMemoryInjection(pawn, maxPersonalMemories);
                    if (!string.IsNullOrEmpty(personalMemories))
                    {
                        knowledgeContext.AppendLine("#### 个人记忆 (Personal Memories)");
                        knowledgeContext.AppendLine(personalMemories);
                        knowledgeContext.AppendLine();
                    }

                    // 2. 共通知识（Common Knowledge）
                    var commonKnowledge = CommonKnowledge(maxKnowledgeEntries);
                    if (!string.IsNullOrEmpty(commonKnowledge))
                    {
                        knowledgeContext.AppendLine("#### 共通知识 (Common Knowledge)");
                        knowledgeContext.AppendLine(commonKnowledge);
                        knowledgeContext.AppendLine();
                    }
                }
                // ? 阶段一：pawn == null 时只注入共通知识
                else
                {
                    // ? 叙事者模式：增加共通知识数量（+5），因为没有个人记忆消耗 Token
                    int storytellerKnowledgeCount = maxKnowledgeEntries + 5;
                    var commonKnowledge = CommonKnowledge(storytellerKnowledgeCount);

                    if (!string.IsNullOrEmpty(commonKnowledge))
                    {
                        knowledgeContext.AppendLine("#### 共通知识 (Common Knowledge)");
                        knowledgeContext.AppendLine(commonKnowledge);
                        knowledgeContext.AppendLine();
                    }

                    // ? 阶段二：注入全局游戏状态（Global Game State）
                    var globalState = GetGlobalGameState();
                    if (!string.IsNullOrEmpty(globalState))
                    {
                        knowledgeContext.AppendLine("### Current Global State");
                        knowledgeContext.AppendLine(globalState);
                        knowledgeContext.AppendLine();
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[SimpleRimTalkIntegration] GetMemoryPrompt 失败: {ex.Message}\n{ex.StackTrace}");
            }

            // 组合最终 Prompt
            string finalPrompt = basePrompt;
            if (knowledgeContext.Length > 0)
            {
                finalPrompt = basePrompt + "\n\n" + knowledgeContext.ToString();
            }

            // ? 更新缓存（安全：pawn == null 时使用特殊键）
            PromptCache[cacheKey] = finalPrompt;
            CacheTimestamp[cacheKey] = Find.TickManager.TicksGame;

            return finalPrompt;
        }

        /// <summary>
        /// ? 阶段二：获取全局游戏状态（仅叙事者模式）
        /// ✅ v1.6.47: 线程安全修复 - 使用 GameStateObserver
        /// </summary>
        private static string GetGlobalGameState()
        {
            StringBuilder stateBuilder = new StringBuilder();

            try
            {
                // ✅ 修复：使用线程安全的 GameStateObserver 代替直接访问 map.mapPawns
                var snapshot = Monitoring.GameStateSnapshotUtility.CaptureSnapshotSafe();
                
                if (snapshot == null)
                {
                    if (Prefs.DevMode)
                        Log.Warning("[SimpleRimTalkIntegration] GameStateObserver 返回空快照");
                    return "";
                }

                // 1. 财富（Wealth）
                try
                {
                    int wealth = snapshot.colony?.wealth ?? 0;
                    string wealthLevel = wealth > 200000 ? "极高" :
                                       wealth > 100000 ? "高" :
                                       wealth > 50000 ? "中等" :
                                       wealth > 20000 ? "低" : "极低";

                    stateBuilder.AppendLine($"- 财富: {wealth:F0} ({wealthLevel})");
                }
                catch (Exception ex)
                {
                    if (Prefs.DevMode)
                        Log.Warning($"[SimpleRimTalkIntegration] 获取财富失败: {ex.Message}");
                }

                // 2. 人口（Population）- ✅ 修复：从快照读取
                try
                {
                    int colonistCount = snapshot.colonists?.Count ?? 0;
                    stateBuilder.AppendLine($"- 殖民者: {colonistCount} 人");
                }
                catch (Exception ex)
                {
                    if (Prefs.DevMode)
                        Log.Warning($"[SimpleRimTalkIntegration] 获取人口失败: {ex.Message}");
                }

                // 3. 日期/季节（Date/Season）- ✅ 修复：只访问非游戏对象的静态API
                try
                {
                    if (Find.TickManager != null && Find.CurrentMap != null)
                    {
                        int tile = Find.CurrentMap.Tile;
                        long ticks = Find.TickManager.TicksAbs;
                        
                        Season season = GenDate.Season(ticks, Find.WorldGrid.LongLatOf(tile));
                        string seasonName = season.LabelCap();
                        stateBuilder.AppendLine($"- 季节: {seasonName}");
                    }
                }
                catch (Exception ex)
                {
                    if (Prefs.DevMode)
                        Log.Warning($"[SimpleRimTalkIntegration] 获取季节失败: {ex.Message}");
                }

                // 4. 威胁点数（Threat Points）- ✅ 修复：从快照读取威胁信息
                try
                {
                    if (snapshot.threats != null)
                    {
                        bool hasRaid = snapshot.threats.raidActive;
                        int raidStrength = snapshot.threats.raidStrength;
                        
                        if (hasRaid)
                        {
                            stateBuilder.AppendLine($"- 当前威胁: 袭击进行中 (强度: {raidStrength})");
                        }
                        else
                        {
                            stateBuilder.AppendLine("- 当前威胁: 无");
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (Prefs.DevMode)
                        Log.Warning($"[SimpleRimTalkIntegration] 获取威胁信息失败: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                // ? 全局安全检查
                if (Prefs.DevMode)
                    Log.Warning($"[SimpleRimTalkIntegration] GetGlobalGameState 失败: {ex.Message}");
            }

            return stateBuilder.ToString();
        }

        /// <summary>
        /// 动态记忆注入（个人记忆）
        /// </summary>
        private static string DynamicMemoryInjection(Pawn pawn, int maxCount)
        {
            if (pawn == null)
                return "";

            try
            {
                // ? 安全检查：确保 RimTalk 记忆系统可用
                if (Find.World == null)
                {
                    if (Prefs.DevMode)
                        Log.Warning("[SimpleRimTalkIntegration] Find.World 为空");
                    return "";
                }

                // 检查是否安装了 RimTalk 扩展
                if (!RimTalkMemoryIntegration.IsRimTalkMemoryAvailable())
                {
                    return "（未安装 RimTalk 记忆扩展）";
                }

                // 获取 Pawn 相关的记忆
                var memories = RimTalkMemoryIntegration.RetrieveConversationMemories(
                    pawn.ThingID, 
                    maxCount
                );

                if (memories == null || memories.Count == 0)
                {
                    return "（暂无相关记忆）";
                }

                StringBuilder memoryText = new StringBuilder();
                foreach (var memory in memories)
                {
                    memoryText.AppendLine($"  - {memory}");
                }

                return memoryText.ToString();
            }
            catch (Exception ex)
            {
                if (Prefs.DevMode)
                    Log.Warning($"[SimpleRimTalkIntegration] DynamicMemoryInjection 失败: {ex.Message}");
                return "";
            }
        }

        /// <summary>
        /// 共通知识库（Common Knowledge）
        /// </summary>
        private static string CommonKnowledge(int maxCount)
        {
            try
            {
                // ? 安全检查：确保记忆管理器可用
                if (Find.World == null)
                {
                    if (Prefs.DevMode)
                        Log.Warning("[SimpleRimTalkIntegration] Find.World 为空，无法访问记忆管理器");
                    return "";
                }

                // 获取通用知识（不依赖特定 Pawn）
                var adapter = RimTalkMemoryAdapter.Instance;
                if (adapter == null)
                {
                    if (Prefs.DevMode)
                        Log.Warning("[SimpleRimTalkIntegration] RimTalkMemoryAdapter 不可用");
                    return "";
                }

                var memories = adapter.GetRelevantMemories("通用知识", maxTokens: maxCount * 100);

                if (memories == null || memories.Count == 0)
                {
                    return "（暂无共通知识）";
                }

                StringBuilder knowledgeText = new StringBuilder();
                int count = 0;
                foreach (var memory in memories)
                {
                    if (count >= maxCount)
                        break;

                    knowledgeText.AppendLine($"  - {memory.content}");
                    count++;
                }

                return knowledgeText.ToString();
            }
            catch (Exception ex)
            {
                if (Prefs.DevMode)
                    Log.Warning($"[SimpleRimTalkIntegration] CommonKnowledge 失败: {ex.Message}");
                return "";
            }
        }

        /// <summary>
        /// 清除缓存（供调试使用）
        /// </summary>
        public static void ClearCache()
        {
            PromptCache.Clear();
            CacheTimestamp.Clear();
            Log.Message("[SimpleRimTalkIntegration] Prompt 缓存已清空");
        }
    }
}
