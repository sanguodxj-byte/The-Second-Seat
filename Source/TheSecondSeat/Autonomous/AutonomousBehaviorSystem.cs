using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using RimWorld;
using TheSecondSeat.Storyteller;
using TheSecondSeat.Monitoring;
using TheSecondSeat.NaturalLanguage;
using TheSecondSeat.Execution;
using TheSecondSeat.Integration;
using TheSecondSeat.Defs;
using TheSecondSeat.Patches;

namespace TheSecondSeat.Autonomous
{
    /// <summary>
    /// ⭐ v2.9.0: 叙事者自主行为系统 - 事件驱动 + 数据驱动重构
    /// 
    /// 重构内容：
    /// 1. 使用 NarratorBehaviorDef 替代所有硬编码数值
    /// 2. 使用 BuildingDamageEventPatches 事件驱动替代全图扫描
    /// 3. 优化性能，消除 O(n) 全图遍历
    /// </summary>
    public class AutonomousBehaviorSystem : GameComponent
    {
        private int ticksSinceLastCheck = 0;
        private List<AutonomousSuggestion> pendingSuggestions = new List<AutonomousSuggestion>();
        
        // ⭐ v2.9.0: 缓存 Def 引用
        private NarratorBehaviorDef behaviorDef;
        
        /// <summary>
        /// 获取行为配置（延迟加载）
        /// </summary>
        private NarratorBehaviorDef BehaviorDef
        {
            get
            {
                if (behaviorDef == null)
                {
                    behaviorDef = NarratorBehaviorDef.Default;
                }
                return behaviorDef;
            }
        }

        public AutonomousBehaviorSystem(Game game) : base()
        {
        }

        public override void GameComponentTick()
        {
            base.GameComponentTick();

            ticksSinceLastCheck++;

            // ⭐ v2.9.0: 使用 Def 配置的检查间隔
            if (ticksSinceLastCheck >= BehaviorDef.checkIntervalTicks)
            {
                ticksSinceLastCheck = 0;
                CheckForProactiveSuggestions();
            }
        }

        /// <summary>
        /// 主动检查游戏状态并生成建议
        /// ⭐ v2.9.0: 使用事件驱动数据，不再全图扫描
        /// </summary>
        private void CheckForProactiveSuggestions()
        {
            var map = Find.CurrentMap;
            if (map == null) return;

            var agent = Current.Game?.GetComponent<Narrator.NarratorManager>()?.GetStorytellerAgent();
            if (agent == null) return;

            // ⭐ v2.9.0: 使用 Def 配置的好感度阈值
            if (agent.affinity < BehaviorDef.minAffinityForSuggestions) return;

            var snapshot = GameStateSnapshotUtility.CaptureSnapshotSafe();
            var suggestions = GenerateSuggestions(snapshot, agent, map);

            foreach (var suggestion in suggestions)
            {
                if (ShouldExecuteAutomatically(suggestion, agent))
                {
                    ExecuteSuggestion(suggestion);
                }
                else
                {
                    NotifyPlayer(suggestion);
                }
            }
        }

        /// <summary>
        /// 生成主动建议
        /// ⭐ v2.9.0: 使用事件驱动的缓存数据
        /// </summary>
        private List<AutonomousSuggestion> GenerateSuggestions(GameStateSnapshot snapshot, StorytellerAgent agent, Map map)
        {
            var suggestions = new List<AutonomousSuggestion>();
            var def = BehaviorDef;

            // 1. 检查成熟作物（这个仍然使用游戏内置的高效索引）
            if (HasMatureCrops(map, def.minMatureCropsForSuggestion))
            {
                suggestions.Add(new AutonomousSuggestion
                {
                    action = "BatchHarvest",
                    reason = "检测到成熟的作物等待收获",
                    priority = SuggestionPriority.Medium,
                    requiresApproval = agent.affinity < def.affinityForHarvestApproval
                });
            }

            // 2. ⭐ v2.9.0: 使用事件驱动的受损建筑缓存（O(1) 复杂度）
            if (BuildingDamageEventPatches.HasDamagedBuildings(map, def.minDamagedBuildingsForSuggestion))
            {
                suggestions.Add(new AutonomousSuggestion
                {
                    action = "PriorityRepair",
                    reason = $"发现 {BuildingDamageEventPatches.GetDamagedBuildingCount(map)} 个受损建筑需要修复",
                    priority = SuggestionPriority.Medium,
                    requiresApproval = agent.affinity < def.affinityForRepairApproval
                });
            }

            // 3. 检查威胁（使用快照数据）
            if (snapshot.threats.raidActive && snapshot.colonists.Any(c => c.health < def.lowHealthThreshold))
            {
                suggestions.Add(new AutonomousSuggestion
                {
                    action = "EmergencyRetreat",
                    reason = "袭击正在进行，部分殖民者受伤严重",
                    priority = SuggestionPriority.High,
                    requiresApproval = agent.affinity < def.affinityForEmergencyApproval
                });
            }

            // 4. 检查资源短缺（使用快照数据）
            if (snapshot.resources.food < def.lowFoodThreshold)
            {
                suggestions.Add(new AutonomousSuggestion
                {
                    action = "Suggestion_Food",
                    reason = "食物储备不足，建议优先种植或交易",
                    priority = SuggestionPriority.High,
                    requiresApproval = true,
                    isAdviceOnly = true
                });
            }

            return suggestions;
        }

        /// <summary>
        /// 判断是否应该自动执行
        /// ⭐ v2.9.0: 使用 Def 配置的阈值
        /// </summary>
        private bool ShouldExecuteAutomatically(AutonomousSuggestion suggestion, StorytellerAgent agent)
        {
            var def = BehaviorDef;
            
            if (suggestion.requiresApproval) return false;
            if (agent.affinity < def.minAffinityForAutoAction) return false;
            
            // 人格特质影响
            if (agent.primaryTrait == PersonalityTrait.Protective && suggestion.priority == SuggestionPriority.High)
            {
                return true;
            }

            if (agent.primaryTrait == PersonalityTrait.Manipulative)
            {
                // ⭐ v2.9.0: 使用 Def 配置的阈值
                return agent.affinity >= def.manipulativeAutoActionThreshold;
            }

            return false;
        }

        /// <summary>
        /// 执行建议
        /// </summary>
        private void ExecuteSuggestion(AutonomousSuggestion suggestion)
        {
            if (suggestion.isAdviceOnly) return;

            var parsed = new ParsedCommand
            {
                action = suggestion.action,
                parameters = new AdvancedCommandParams
                {
                    scope = "Map"
                },
                confidence = 1f,
                originalQuery = suggestion.reason
            };

            var result = GameActionExecutor.Execute(parsed);

            if (result.success)
            {
                Messages.Message(
                    $"[自主行动] 叙事者执行了：{suggestion.action} - {suggestion.reason}",
                    MessageTypeDefOf.PositiveEvent
                );

                MemoryContextBuilder.RecordEvent(
                    $"自主执行：{suggestion.action} - {result.message}",
                    MemoryImportance.High
                );
            }
        }

        /// <summary>
        /// 通知玩家建议
        /// </summary>
        private void NotifyPlayer(AutonomousSuggestion suggestion)
        {
            string message = suggestion.isAdviceOnly
                ? $"叙事者建议：{suggestion.reason}"
                : $"叙事者建议执行：{suggestion.action} - {suggestion.reason}\n（在对话窗口回复'同意'或'执行'来确认）";

            Messages.Message(message, MessageTypeDefOf.NeutralEvent);

            pendingSuggestions.Add(suggestion);
            suggestion.timestamp = Find.TickManager.TicksGame;
        }

        /// <summary>
        /// 检查是否有成熟作物
        /// 注意：ThingsInGroup 使用游戏内置的优化索引，性能可接受
        /// </summary>
        private bool HasMatureCrops(Map map, int minCount)
        {
            if (map == null) return false;

            // 使用 RimWorld 内置的分组索引（已优化）
            var maturePlants = map.listerThings.ThingsInGroup(ThingRequestGroup.HarvestablePlant);
            
            int count = 0;
            foreach (var thing in maturePlants)
            {
                if (thing is Plant plant && plant.HarvestableNow && plant.Spawned)
                {
                    count++;
                    if (count >= minCount) return true;
                }
            }
            
            return false;
        }

        /// <summary>
        /// 处理玩家对建议的回复
        /// ⭐ v3.0.0: 支持结构化意图解析 (JSON Intent)
        /// </summary>
        public void ProcessPlayerResponse(string response)
        {
            // 1. 尝试解析为结构化响应
            var parsed = LLM.LLMResponseParser.Parse(response);
            if (parsed != null && !string.IsNullOrEmpty(parsed.intent))
            {
                HandleStructuredIntent(parsed.intent);
                return;
            }

            // 2. 回退到传统的字符串匹配 (Legacy)
            HandleLegacyStringResponse(response);
        }

        /// <summary>
        /// 处理结构化意图
        /// </summary>
        private void HandleStructuredIntent(string intent)
        {
            intent = intent.ToUpperInvariant();
            if (intent == "APPROVE" || intent == "AGREE" || intent == "CONFIRM")
            {
                ResolvePendingSuggestion(true);
            }
            else if (intent == "REJECT" || intent == "DENY" || intent == "CANCEL")
            {
                ResolvePendingSuggestion(false);
            }
        }

        /// <summary>
        /// 解决当前挂起的建议
        /// </summary>
        public void ResolvePendingSuggestion(bool accepted)
        {
            if (pendingSuggestions.Count == 0) return;

            var latest = pendingSuggestions.Last();
            
            if (accepted)
            {
                ExecuteSuggestion(latest);
                pendingSuggestions.Remove(latest);
            }
            else
            {
                pendingSuggestions.Remove(latest); // Remove first to avoid re-triggering logic if needed
                
                // ⭐ v2.9.0: 使用 Def 配置的惩罚值
                var narrator = Current.Game?.GetComponent<Narrator.NarratorManager>();
                narrator?.ModifyFavorability(BehaviorDef.rejectSuggestionPenalty, "玩家拒绝了建议");
            }
        }

        /// <summary>
        /// 传统的字符串匹配处理 (Legacy)
        /// </summary>
        private void HandleLegacyStringResponse(string response)
        {
            response = response.ToLower().Trim();

            if (response.Contains("同意") || response.Contains("执行") || response.Contains("好") ||
                response.Contains("agree") || response.Contains("yes") || response.Contains("ok"))
            {
                ResolvePendingSuggestion(true);
            }
            else if (response.Contains("拒绝") || response.Contains("不要") || response.Contains("no"))
            {
                ResolvePendingSuggestion(false);
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref ticksSinceLastCheck, "ticksSinceLastCheck", 0);
            Scribe_Collections.Look(ref pendingSuggestions, "pendingSuggestions", LookMode.Deep);
            
            // 重新加载时清除缓存
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                behaviorDef = null;
            }
        }
    }

    /// <summary>
    /// 自主建议
    /// </summary>
    public class AutonomousSuggestion : IExposable
    {
        public string action = "";
        public string reason = "";
        public SuggestionPriority priority = SuggestionPriority.Low;
        public bool requiresApproval = true;
        public bool isAdviceOnly = false;
        public int timestamp = 0;

        public void ExposeData()
        {
            Scribe_Values.Look(ref action, "action", "");
            Scribe_Values.Look(ref reason, "reason", "");
            Scribe_Values.Look(ref priority, "priority", SuggestionPriority.Low);
            Scribe_Values.Look(ref requiresApproval, "requiresApproval", true);
            Scribe_Values.Look(ref isAdviceOnly, "isAdviceOnly", false);
            Scribe_Values.Look(ref timestamp, "timestamp", 0);
        }
    }

    public enum SuggestionPriority
    {
        Low,
        Medium,
        High,
        Critical
    }
}
