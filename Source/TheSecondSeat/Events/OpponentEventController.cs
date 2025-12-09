using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using TheSecondSeat.Storyteller;
using TheSecondSeat.Narrator;
using TheSecondSeat.PersonaGeneration;
using TheSecondSeat.Settings;

namespace TheSecondSeat.Events
{
    /// <summary>
    /// 奕者模式事件控制器
    /// ? 核心设计：好感度影响AI的"游戏风格"，而非简单的难度调整
    /// ? 高好感度：更多戏剧性/有趣的事件，AI愿意"配合演出"
    /// ? 低好感度：更多针对性的挑战，AI"认真对弈"
    /// </summary>
    public class OpponentEventController : GameComponent
    {
        // 单例访问
        private static OpponentEventController? instance;
        public static OpponentEventController? Instance => instance;
        
        // 事件生成间隔（游戏ticks）
        private int ticksSinceLastEvent = 0;
        private int baseEventInterval = 60000; // 约1游戏天
        
        // 事件队列（AI可以预安排事件）
        private List<ScheduledEvent> scheduledEvents = new List<ScheduledEvent>();
        
        // 事件历史（用于避免重复）
        private List<string> recentEvents = new List<string>();
        private const int MaxRecentEvents = 10;
        
        // ? 殖民地实力评估（用于动态难度）
        private float lastColonyWealth = 0f;
        private int lastColonistCount = 0;
        private float lastMilitaryStrength = 0f;
        
        // ? 事件统计
        private int positiveEventsTriggered = 0;
        private int negativeEventsTriggered = 0;
        
        // 是否接管叙事者
        private bool isActive = false;
        
        public bool IsActive => isActive;

        public OpponentEventController(Game game) : base()
        {
            instance = this;
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            instance = this;
            CheckAndActivate();
        }

        /// <summary>
        /// 检查是否应该激活奕者模式
        /// </summary>
        private void CheckAndActivate()
        {
            var mod = LoadedModManager.GetMod<TheSecondSeatMod>();
            var modSettings = mod?.GetSettings<TheSecondSeatSettings>();
            
            if (modSettings?.difficultyMode == AIDifficultyMode.Opponent)
            {
                Activate();
            }
            else
            {
                Deactivate();
            }
        }

        /// <summary>
        /// 激活奕者模式
        /// </summary>
        public void Activate()
        {
            isActive = true;
            Log.Message("[OpponentEventController] ? 奕者模式已激活 - AI将控制事件发生");
        }

        /// <summary>
        /// 停用奕者模式
        /// </summary>
        public void Deactivate()
        {
            isActive = false;
            scheduledEvents.Clear();
            Log.Message("[OpponentEventController] 奕者模式已停用 - 恢复原版叙事者");
        }

        public override void GameComponentTick()
        {
            base.GameComponentTick();

            // 检查模式变化
            if (Find.TickManager.TicksGame % 2500 == 0) // 每分钟检查一次
            {
                CheckAndActivate();
                UpdateColonyAssessment();
            }

            if (!isActive) return;

            ticksSinceLastEvent++;

            // 处理预定事件
            ProcessScheduledEvents();

            // 检查是否应该生成事件
            if (ticksSinceLastEvent >= GetDynamicEventInterval())
            {
                ticksSinceLastEvent = 0;
                ConsiderGeneratingEvent();
            }
        }

        /// <summary>
        /// ? 更新殖民地实力评估
        /// </summary>
        private void UpdateColonyAssessment()
        {
            var map = Find.CurrentMap;
            if (map == null) return;

            lastColonyWealth = map.wealthWatcher.WealthTotal;
            lastColonistCount = map.mapPawns.FreeColonistsSpawnedCount;
            
            // ? 评估军事实力
            lastMilitaryStrength = CalculateMilitaryStrength(map);
        }

        /// <summary>
        /// ? 计算殖民地军事实力
        /// </summary>
        private float CalculateMilitaryStrength(Map map)
        {
            float strength = 0f;
            
            foreach (var colonist in map.mapPawns.FreeColonistsSpawned)
            {
                // 基础战斗力
                strength += 100f;
                
                // 武器加成
                if (colonist.equipment?.Primary != null)
                {
                    var weapon = colonist.equipment.Primary;
                    strength += weapon.GetStatValue(StatDefOf.RangedWeapon_DamageMultiplier, true) * 50f;
                    strength += weapon.GetStatValue(StatDefOf.MeleeWeapon_AverageDPS, true) * 30f;
                }
                
                // 技能加成
                var shootingSkill = colonist.skills?.GetSkill(SkillDefOf.Shooting)?.Level ?? 0;
                var meleeSkill = colonist.skills?.GetSkill(SkillDefOf.Melee)?.Level ?? 0;
                strength += (shootingSkill + meleeSkill) * 10f;
                
                // 健康惩罚
                if (colonist.health.summaryHealth.SummaryHealthPercent < 1f)
                {
                    strength *= colonist.health.summaryHealth.SummaryHealthPercent;
                }
            }
            
            // 防御建筑加成
            var turrets = map.listerBuildings.allBuildingsColonist
                .Where(b => b.def.building?.IsTurret == true)
                .Count();
            strength += turrets * 200f;
            
            return strength;
        }

        /// <summary>
        /// ? 事件间隔基于殖民地发展阶段和好感度
        /// </summary>
        private int GetDynamicEventInterval()
        {
            var agent = GetStorytellerAgent();
            float baseFactor = 1f;
            
            // 殖民地越强大，事件越频繁
            if (lastColonyWealth > 500000f)
                baseFactor *= 0.7f;
            else if (lastColonyWealth > 200000f)
                baseFactor *= 0.85f;
            else if (lastColonyWealth < 50000f)
                baseFactor *= 1.3f;

            // ? 好感度影响事件频率
            if (agent != null)
            {
                if (agent.affinity < -50f)
                    baseFactor *= 0.8f;  // 低好感度：事件更频繁
                else if (agent.affinity > 60f)
                    baseFactor *= 1.1f;  // 高好感度：事件稍少
            }

            return (int)(baseEventInterval * baseFactor);
        }

        /// <summary>
        /// 处理预定事件
        /// </summary>
        private void ProcessScheduledEvents()
        {
            if (scheduledEvents.Count == 0) return;

            int currentTick = Find.TickManager.TicksGame;
            var eventsToTrigger = scheduledEvents.Where(e => e.scheduledTick <= currentTick).ToList();

            foreach (var evt in eventsToTrigger)
            {
                TriggerScheduledEvent(evt);
                scheduledEvents.Remove(evt);
            }
        }

        /// <summary>
        /// 触发预定事件
        /// </summary>
        private void TriggerScheduledEvent(ScheduledEvent evt)
        {
            var map = Find.CurrentMap;
            if (map == null) return;

            var agent = GetStorytellerAgent();
            if (agent == null) return;

            var eventGenerator = AffinityDrivenEvents.Instance;
            
            // ? 简化：直接使用TriggerEvent方法
            eventGenerator.TriggerEvent(map, evt.eventDefName, 0.5f);
            
            Log.Message($"[OpponentEventController] 触发预定事件: {evt.eventDefName}");
            RecordRecentEvent(evt.eventDefName);
            
            if (!string.IsNullOrEmpty(evt.aiComment))
            {
                Messages.Message($"叙事者：{evt.aiComment}", MessageTypeDefOf.NeutralEvent);
            }
        }

        /// <summary>
        /// ? 考虑生成事件（主逻辑）
        /// </summary>
        private void ConsiderGeneratingEvent()
        {
            var map = Find.CurrentMap;
            if (map == null) return;

            var agent = GetStorytellerAgent();
            if (agent == null) return;

            // ? 根据好感度选择事件策略
            EventStrategy strategy = DetermineEventStrategy(agent.affinity);
            
            var eventGenerator = AffinityDrivenEvents.Instance;
            
            // ? 简化：根据策略直接触发正面/负面事件
            bool triggerPositive = strategy == EventStrategy.Dramatic && Rand.Chance(0.6f) ||
                                  strategy == EventStrategy.Balanced && Rand.Chance(0.5f) ||
                                  strategy == EventStrategy.Competitive && Rand.Chance(0.3f);
            
            if (triggerPositive)
            {
                eventGenerator.TriggerPositiveEvent(map);
                positiveEventsTriggered++;
                string aiComment = GenerateStrategyComment(null, strategy, agent);
                Messages.Message($"叙事者：{aiComment}", MessageTypeDefOf.PositiveEvent);
            }
            else
            {
                float severity = strategy switch
                {
                    EventStrategy.Ruthless => 0.8f,
                    EventStrategy.Tactical => 0.6f,
                    EventStrategy.Competitive => 0.5f,
                    _ => 0.4f
                };
                
                eventGenerator.TriggerNegativeEvent(map, severity);
                negativeEventsTriggered++;
                string aiComment = GenerateStrategyComment(null, strategy, agent);
                Messages.Message($"叙事者：{aiComment}", MessageTypeDefOf.NegativeEvent);
            }
            
            Log.Message($"[OpponentEventController] 生成事件 ({strategy})");
        }

        /// <summary>
        /// ? 获取事件强度倍率（基于好感度）
        /// </summary>
        private float GetIntensityMultiplier(float affinity, EventCategory category)
        {
            // 高好感度：负面事件更弱，正面事件更强
            if (affinity >= 60f)
            {
                return category == EventCategory.Negative ? 0.7f : 1.3f;
            }
            // 中高好感度：轻微调整
            else if (affinity >= 30f)
            {
                return category == EventCategory.Negative ? 0.85f : 1.15f;
            }
            // 中性
            else if (affinity >= -30f)
            {
                return 1.0f;
            }
            // 低好感度：负面事件更强
            else if (affinity >= -70f)
            {
                return category == EventCategory.Negative ? 1.2f : 0.9f;
            }
            // 极低好感度：负面事件显著增强
            else
            {
                return category == EventCategory.Negative ? 1.4f : 0.8f;
            }
        }

        /// <summary>
        /// ? 带强度调整的事件触发
        /// </summary>
        private bool TriggerEventWithIntensity(StorytellerEventDef eventDef, Map map, StorytellerAgent agent, float intensityMultiplier, out string comment)
        {
            comment = "";

            if (eventDef.incidentDef == null)
                return false;

            IncidentParms parms = StorytellerUtility.DefaultParmsNow(eventDef.incidentDef.category, map);
            
            // ? 应用强度倍率
            parms.points *= intensityMultiplier;
            
            // ? 确保不会太弱或太强
            parms.points = Math.Max(parms.points, 100f);  // 最低100点
            parms.points = Math.Min(parms.points, parms.points * 2f);  // 最高原始值的2倍

            bool success = eventDef.incidentDef.Worker.TryExecute(parms);

            if (success)
            {
                comment = $"事件强度: {intensityMultiplier:P0}";
                
                // 记录到记忆系统
                Integration.MemoryContextBuilder.RecordEvent(
                    $"奕者事件: {eventDef.incidentDef.label}",
                    Integration.MemoryImportance.High
                );
                
                agent.eventsTriggered++;
            }

            return success;
        }

        /// <summary>
        /// ? 更新事件统计
        /// </summary>
        private void UpdateEventStats(StorytellerEventDef eventDef)
        {
            if (eventDef.category == EventCategory.Positive)
                positiveEventsTriggered++;
            else if (eventDef.category == EventCategory.Negative)
                negativeEventsTriggered++;
        }

        /// <summary>
        /// ? 确定事件策略（核心设计）
        /// </summary>
        private EventStrategy DetermineEventStrategy(float affinity)
        {
            if (affinity >= 60f)
                return EventStrategy.Dramatic;    // 戏剧性：有趣的组合事件
            else if (affinity >= 30f)
                return EventStrategy.Balanced;    // 平衡：挑战与机遇并存
            else if (affinity >= -30f)
                return EventStrategy.Competitive; // 竞争：认真的对手
            else if (affinity >= -70f)
                return EventStrategy.Tactical;    // 战术：针对性挑战
            else
                return EventStrategy.Ruthless;    // 无情：全力以赴
        }

        /// <summary>
        /// ? 根据策略筛选事件
        /// </summary>
        private List<StorytellerEventDef> FilterEventsByStrategy(
            List<StorytellerEventDef> allEvents, 
            EventStrategy strategy,
            StorytellerAgent agent,
            Map map)
        {
            var filtered = new List<StorytellerEventDef>();

            foreach (var evt in allEvents)
            {
                if (evt.incidentDef == null) continue;
                if (!evt.incidentDef.TargetAllowed(map)) continue;
                
                bool include = false;
                
                switch (strategy)
                {
                    case EventStrategy.Dramatic:
                        // 戏剧性：各种类型都要
                        include = true;
                        break;
                        
                    case EventStrategy.Balanced:
                        // 平衡：正负面各半
                        include = true;
                        break;
                        
                    case EventStrategy.Competitive:
                        // 竞争：稍偏负面
                        include = evt.category != EventCategory.Positive || Rand.Chance(0.4f);
                        break;
                        
                    case EventStrategy.Tactical:
                        // 战术：针对弱点
                        include = IsEventTargetingWeakness(evt, map);
                        break;
                        
                    case EventStrategy.Ruthless:
                        // 无情：主要负面
                        include = evt.category == EventCategory.Negative || Rand.Chance(0.2f);
                        break;
                }
                
                if (include)
                    filtered.Add(evt);
            }

            return filtered.Count > 0 ? filtered : allEvents;
        }

        /// <summary>
        /// ? 检查事件是否针对殖民地弱点
        /// </summary>
        private bool IsEventTargetingWeakness(StorytellerEventDef evt, Map map)
        {
            int colonistCount = map.mapPawns.FreeColonistsSpawnedCount;
            float food = map.resourceCounter.GetCount(ThingDefOf.MealSimple) + 
                         map.resourceCounter.GetCount(ThingDefOf.MealFine);
            int medicine = map.resourceCounter.GetCount(ThingDefOf.MedicineHerbal) +
                          map.resourceCounter.GetCount(ThingDefOf.MedicineIndustrial);

            // 食物短缺
            if (food < colonistCount * 10)
            {
                if (evt.defName == "ToxicFallout" || evt.defName == "Disease")
                    return true;
            }

            // 医药短缺
            if (medicine < colonistCount * 2)
            {
                if (evt.defName == "Disease")
                    return true;
            }

            // 人少
            if (colonistCount <= 5)
            {
                if (evt.defName == "Raid" || evt.defName == "MechanoidRaid")
                    return true;
            }
            
            // 军事弱
            if (lastMilitaryStrength < colonistCount * 200)
            {
                if (evt.defName == "Raid")
                    return true;
            }

            return Rand.Chance(0.5f);
        }

        /// <summary>
        /// ? 根据策略选择事件
        /// </summary>
        private StorytellerEventDef? SelectEventWithStrategy(
            List<StorytellerEventDef> events, 
            EventStrategy strategy,
            StorytellerAgent agent)
        {
            if (events.Count == 0) return null;

            var weightedEvents = events.Select(e => new
            {
                Event = e,
                Weight = CalculateStrategyWeight(e, strategy, agent)
            }).ToList();

            float totalWeight = weightedEvents.Sum(w => w.Weight);
            float roll = Rand.Range(0f, totalWeight);
            float cumulative = 0f;

            foreach (var item in weightedEvents)
            {
                cumulative += item.Weight;
                if (roll <= cumulative)
                    return item.Event;
            }

            return weightedEvents.Last().Event;
        }

        /// <summary>
        /// ? 计算策略权重
        /// </summary>
        private float CalculateStrategyWeight(StorytellerEventDef evt, EventStrategy strategy, StorytellerAgent agent)
        {
            float weight = evt.baseWeight;

            switch (strategy)
            {
                case EventStrategy.Dramatic:
                    if (evt.baseWeight < 1f) weight *= 2f;  // 稀有事件权重增加
                    break;
                    
                case EventStrategy.Competitive:
                    if (evt.category == EventCategory.Negative) weight *= 1.3f;
                    break;
                    
                case EventStrategy.Tactical:
                    // 已在筛选阶段处理
                    break;
                    
                case EventStrategy.Ruthless:
                    if (evt.category == EventCategory.Negative) weight *= 1.5f;
                    break;
            }

            // 混沌人格：随机性
            if (agent.primaryTrait == PersonalityTrait.Chaotic)
                weight *= Rand.Range(0.5f, 2f);

            return Math.Max(0.1f, weight);
        }

        /// <summary>
        /// ? 生成策略评论
        /// </summary>
        private string GenerateStrategyComment(StorytellerEventDef? evt, EventStrategy strategy, StorytellerAgent agent)
        {
            bool isPositive = evt?.category == EventCategory.Positive;
            bool isNegative = evt?.category == EventCategory.Negative;

            switch (strategy)
            {
                case EventStrategy.Dramatic:
                    return isPositive 
                        ? "看来你需要转机...给你点有趣的" 
                        : "每个伟大的故事都需要挑战...展现你的韧性吧！";
                        
                case EventStrategy.Balanced:
                    return isPositive 
                        ? "作为公平的对手，我给你一些帮助。" 
                        : "挑战是游戏的一部分，准备好了吗？";
                        
                case EventStrategy.Competitive:
                    return isPositive 
                        ? "偶尔的喘息...不要习惯。" 
                        : "考验你的时候到了。";
                        
                case EventStrategy.Tactical:
                    return isNegative 
                        ? "我注意到了你的弱点..." 
                        : "暂时放过你，但下次可不一定。";
                        
                case EventStrategy.Ruthless:
                    return isNegative 
                        ? "不要指望我会仁慈。" 
                        : "...这只是暴风雨前的平静。";
            }

            return "看你如何应对了。";
        }

        /// <summary>
        /// 记录最近事件
        /// </summary>
        private void RecordRecentEvent(string eventDefName)
        {
            recentEvents.Add(eventDefName);
            if (recentEvents.Count > MaxRecentEvents)
                recentEvents.RemoveAt(0);
        }

        /// <summary>
        /// AI 主动触发事件（通过对话）
        /// </summary>
        public bool TriggerEventByAI(string eventType, string aiComment = "")
        {
            if (!isActive) return false;

            var map = Find.CurrentMap;
            if (map == null) return false;

            var agent = GetStorytellerAgent();
            if (agent == null) return false;

            var eventGenerator = AffinityDrivenEvents.Instance;
            
            // ? 简化：映射事件类型到正面/负面
            bool isPositive = eventType.ToLower() switch
            {
                "trader" or "商队" or "贸易" => true,
                "wanderer" or "流浪者" or "加入者" => true,
                "resource" or "资源" or "空投" => true,
                _ => false
            };
            
            bool success;
            if (isPositive)
            {
                eventGenerator.TriggerPositiveEvent(map);
                positiveEventsTriggered++;
                success = true;
            }
            else
            {
                eventGenerator.TriggerNegativeEvent(map, 0.5f);
                negativeEventsTriggered++;
                success = true;
            }

            if (success)
            {
                RecordRecentEvent(eventType);
                
                string finalComment = !string.IsNullOrEmpty(aiComment) ? aiComment : "看你如何应对。";
                Messages.Message($"叙事者：{finalComment}", MessageTypeDefOf.NeutralEvent);
                
                Log.Message($"[OpponentEventController] AI触发事件: {eventType}");
            }

            return success;
        }

        /// <summary>
        /// ? 匹配事件类型
        /// </summary>
        private StorytellerEventDef? MatchEventByType(List<StorytellerEventDef> allEvents, string eventType)
        {
            return eventType.ToLower() switch
            {
                "raid" or "袭击" => allEvents.FirstOrDefault(e => e.defName == "Raid"),
                "trader" or "商队" or "贸易" => allEvents.FirstOrDefault(e => e.defName == "TraderCaravanArrival"),
                "wanderer" or "流浪者" or "加入者" => allEvents.FirstOrDefault(e => e.defName == "WandererJoin"),
                "disease" or "疾病" or "瘟疫" => allEvents.FirstOrDefault(e => e.defName == "Disease"),
                "resource" or "资源" or "空投" => allEvents.FirstOrDefault(e => e.defName == "ResourcePodCrash"),
                "eclipse" or "日蚀" => allEvents.FirstOrDefault(e => e.defName == "Eclipse"),
                "toxic" or "毒尘" or "有毒沉降" => allEvents.FirstOrDefault(e => e.defName == "ToxicFallout"),
                _ => allEvents.FirstOrDefault(e => e.defName.Equals(eventType, StringComparison.OrdinalIgnoreCase))
            };
        }

        /// <summary>
        /// AI 安排未来事件
        /// </summary>
        public void ScheduleEvent(string eventType, int delayTicks, string aiComment = "")
        {
            if (!isActive) return;

            scheduledEvents.Add(new ScheduledEvent
            {
                eventDefName = eventType,
                scheduledTick = Find.TickManager.TicksGame + delayTicks,
                aiComment = aiComment
            });
            
            Log.Message($"[OpponentEventController] AI安排事件: {eventType}, {delayTicks / 2500}分钟后触发");
        }

        /// <summary>
        /// 获取可用事件列表
        /// </summary>
        public List<string> GetAvailableEventTypes()
        {
            return new List<string>
            {
                "raid (袭击)",
                "trader (商队)",
                "wanderer (流浪者)",
                "disease (疾病)",
                "resource (资源空投)",
                "eclipse (日蚀)",
                "toxic (毒尘)"
            };
        }

        /// <summary>
        /// 获取当前状态信息
        /// </summary>
        public string GetStatusInfo()
        {
            var agent = GetStorytellerAgent();
            
            string status = $"奕者模式: {(isActive ? "激活" : "停用")}\n";
            status += $"待处理事件: {scheduledEvents.Count}\n";
            status += $"距下次事件: {(GetDynamicEventInterval() - ticksSinceLastEvent) / 2500}分钟\n";
            status += $"正面事件: {positiveEventsTriggered} | 负面事件: {negativeEventsTriggered}\n";
            
            if (agent != null)
            {
                var strategy = DetermineEventStrategy(agent.affinity);
                status += $"当前好感度: {agent.affinity:F0}\n";
                status += $"当前策略: {GetStrategyName(strategy)}\n";
                status += $"事件强度倍率: {GetIntensityMultiplier(agent.affinity, EventCategory.Negative):P0}\n";
            }
            
            status += $"殖民地财富: {lastColonyWealth:N0}\n";
            status += $"军事实力: {lastMilitaryStrength:N0}\n";

            return status;
        }

        private string GetStrategyName(EventStrategy strategy)
        {
            return strategy switch
            {
                EventStrategy.Dramatic => "戏剧性（制造精彩故事）",
                EventStrategy.Balanced => "平衡（公平对弈）",
                EventStrategy.Competitive => "竞争（认真对手）",
                EventStrategy.Tactical => "战术（针对弱点）",
                EventStrategy.Ruthless => "无情（全力以赴）",
                _ => "未知"
            };
        }

        private StorytellerAgent? GetStorytellerAgent()
        {
            return Current.Game?.GetComponent<NarratorManager>()?.GetStorytellerAgent();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref ticksSinceLastEvent, "ticksSinceLastEvent", 0);
            Scribe_Values.Look(ref isActive, "isActive", false);
            Scribe_Values.Look(ref lastColonyWealth, "lastColonyWealth", 0f);
            Scribe_Values.Look(ref lastColonistCount, "lastColonistCount", 0);
            Scribe_Values.Look(ref lastMilitaryStrength, "lastMilitaryStrength", 0f);
            Scribe_Values.Look(ref positiveEventsTriggered, "positiveEventsTriggered", 0);
            Scribe_Values.Look(ref negativeEventsTriggered, "negativeEventsTriggered", 0);
            Scribe_Collections.Look(ref scheduledEvents, "scheduledEvents", LookMode.Deep);
            Scribe_Collections.Look(ref recentEvents, "recentEvents", LookMode.Value);
            
            if (scheduledEvents == null) scheduledEvents = new List<ScheduledEvent>();
            if (recentEvents == null) recentEvents = new List<string>();
        }
    }

    /// <summary>
    /// 事件策略枚举
    /// </summary>
    public enum EventStrategy
    {
        Dramatic,    // 戏剧性：制造精彩故事
        Balanced,    // 平衡：挑战与机遇并存
        Competitive, // 竞争：认真的对手
        Tactical,    // 战术：针对殖民地弱点
        Ruthless     // 无情：全力以赴
    }

    /// <summary>
    /// 预定事件数据
    /// </summary>
    public class ScheduledEvent : IExposable
    {
        public string eventDefName = "";
        public int scheduledTick = 0;
        public string aiComment = "";

        public void ExposeData()
        {
            Scribe_Values.Look(ref eventDefName, "eventDefName", "");
            Scribe_Values.Look(ref scheduledTick, "scheduledTick", 0);
            Scribe_Values.Look(ref aiComment, "aiComment", "");
        }
    }
}
