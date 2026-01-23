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

namespace TheSecondSeat.Autonomous
{
    /// <summary>
    /// 叙事者自主行为系统 - AI主动观察并提出建议
    /// </summary>
    public class AutonomousBehaviorSystem : GameComponent
    {
        private int ticksSinceLastCheck = 0;
        private const int CheckInterval = 18000; // 5分钟检查一次
        private List<AutonomousSuggestion> pendingSuggestions = new List<AutonomousSuggestion>();

        public AutonomousBehaviorSystem(Game game) : base()
        {
        }

        public override void GameComponentTick()
        {
            base.GameComponentTick();

            ticksSinceLastCheck++;

            if (ticksSinceLastCheck >= CheckInterval)
            {
                ticksSinceLastCheck = 0;
                CheckForProactiveSuggestions();
            }
        }

        /// <summary>
        /// 主动检查游戏状态并生成建议
        /// </summary>
        private void CheckForProactiveSuggestions()
        {
            var map = Find.CurrentMap;
            if (map == null) return;

            var agent = Current.Game?.GetComponent<Narrator.NarratorManager>()?.GetStorytellerAgent();
            if (agent == null) return;

            // 只有在高好感度时才主动提出建议
            if (agent.affinity < 30f) return;

            var snapshot = GameStateSnapshotUtility.CaptureSnapshotSafe();
            var suggestions = GenerateSuggestions(snapshot, agent);

            foreach (var suggestion in suggestions)
            {
                if (ShouldExecuteAutomatically(suggestion, agent))
                {
                    // 自动执行（仅在极高好感度）
                    ExecuteSuggestion(suggestion);
                }
                else
                {
                    // 提示玩家
                    NotifyPlayer(suggestion);
                }
            }
        }

        /// <summary>
        /// 生成主动建议
        /// </summary>
        private List<AutonomousSuggestion> GenerateSuggestions(GameStateSnapshot snapshot, StorytellerAgent agent)
        {
            var suggestions = new List<AutonomousSuggestion>();

            // 1. 检查成熟作物
            if (HasMatureCrops())
            {
                suggestions.Add(new AutonomousSuggestion
                {
                    action = "BatchHarvest",
                    reason = "检测到成熟的作物等待收获",
                    priority = SuggestionPriority.Medium,
                    requiresApproval = agent.affinity < 60f
                });
            }

            // 2. 检查受损建筑
            if (HasDamagedStructures())
            {
                suggestions.Add(new AutonomousSuggestion
                {
                    action = "PriorityRepair",
                    reason = "发现多个受损建筑需要修复",
                    priority = SuggestionPriority.Medium,
                    requiresApproval = agent.affinity < 70f
                });
            }

            // 3. 检查威胁
            if (snapshot.threats.raidActive && snapshot.colonists.Any(c => c.health < 50))
            {
                suggestions.Add(new AutonomousSuggestion
                {
                    action = "EmergencyRetreat",
                    reason = "袭击正在进行，部分殖民者受伤严重",
                    priority = SuggestionPriority.High,
                    requiresApproval = agent.affinity < 80f
                });
            }

            // 4. 检查资源短缺
            if (snapshot.resources.food < 50)
            {
                suggestions.Add(new AutonomousSuggestion
                {
                    action = "Suggestion_Food",
                    reason = "食物储备不足，建议优先种植或交易",
                    priority = SuggestionPriority.High,
                    requiresApproval = true, // 这是建议，不是命令
                    isAdviceOnly = true
                });
            }

            return suggestions;
        }

        /// <summary>
        /// 判断是否应该自动执行
        /// </summary>
        private bool ShouldExecuteAutomatically(AutonomousSuggestion suggestion, StorytellerAgent agent)
        {
            // 只有极高好感度且不需要批准的建议才自动执行
            if (suggestion.requiresApproval) return false;
            if (agent.affinity < 85f) return false;
            
            // 人格特质影响
            if (agent.primaryTrait == PersonalityTrait.Protective && suggestion.priority == SuggestionPriority.High)
            {
                return true;
            }

            if (agent.primaryTrait == PersonalityTrait.Manipulative)
            {
                // 操控型人格更倾向于自主执行
                return agent.affinity >= 75f;
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

            // 添加到待处理列表
            pendingSuggestions.Add(suggestion);
            suggestion.timestamp = Find.TickManager.TicksGame;
        }

        /// <summary>
        /// 检查是否有成熟作物
        /// </summary>
        private bool HasMatureCrops()
        {
            var map = Find.CurrentMap;
            if (map == null) return false;

            var maturePlants = map.listerThings.ThingsInGroup(ThingRequestGroup.HarvestablePlant)
                .OfType<Plant>()
                .Where(p => p.HarvestableNow && p.Spawned);

            return maturePlants.Count() >= 10; // 至少10株成熟作物
        }

        /// <summary>
        /// 检查是否有受损建筑
        /// </summary>
        private bool HasDamagedStructures()
        {
            var map = Find.CurrentMap;
            if (map == null) return false;

            var damaged = map.listerThings.AllThings
                .Where(t => t.def.useHitPoints && 
                           t.HitPoints < t.MaxHitPoints * 0.7f && 
                           t.def.building != null);

            return damaged.Count() >= 3; // 至少3个受损建筑
        }

        /// <summary>
        /// 处理玩家对建议的回复
        /// </summary>
        public void ProcessPlayerResponse(string response)
        {
            response = response.ToLower().Trim();

            if (response.Contains("同意") || response.Contains("执行") || response.Contains("好") || 
                response.Contains("agree") || response.Contains("yes") || response.Contains("ok"))
            {
                // 执行最近的建议
                if (pendingSuggestions.Count > 0)
                {
                    var latest = pendingSuggestions.Last();
                    ExecuteSuggestion(latest);
                    pendingSuggestions.Remove(latest);
                }
            }
            else if (response.Contains("拒绝") || response.Contains("不要") || response.Contains("no"))
            {
                if (pendingSuggestions.Count > 0)
                {
                    pendingSuggestions.RemoveAt(pendingSuggestions.Count - 1);
                    
                    // 降低好感度
                    var narrator = Current.Game?.GetComponent<Narrator.NarratorManager>();
                    narrator?.ModifyFavorability(-0.5f, "玩家拒绝了建议");
                }
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref ticksSinceLastCheck, "ticksSinceLastCheck", 0);
            Scribe_Collections.Look(ref pendingSuggestions, "pendingSuggestions", LookMode.Deep);
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
