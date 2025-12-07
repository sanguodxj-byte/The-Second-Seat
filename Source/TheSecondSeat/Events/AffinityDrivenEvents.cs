using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using TheSecondSeat.Storyteller;
using TheSecondSeat.Integration;

namespace TheSecondSeat.Events
{
    /// <summary>
    /// 事件类型分类
    /// </summary>
    public enum EventCategory
    {
        Positive,        // 正面事件
        Negative,        // 负面事件
        Neutral,         // 中性事件
        Mixed            // 混合事件
    }

    /// <summary>
    /// 叙事者事件定义
    /// </summary>
    public class StorytellerEventDef
    {
        public string defName = "";
        public IncidentDef? incidentDef;
        public EventCategory category = EventCategory.Neutral;
        public float baseWeight = 1f;
        public float minAffinity = -100f;
        public float maxAffinity = 100f;
        public PersonalityTrait? preferredTrait = null;
        public string commentKey = "";  // 翻译键
    }

    /// <summary>
    /// 好感度驱动的事件生成器
    /// </summary>
    public class AffinityDrivenEventGenerator
    {
        private static AffinityDrivenEventGenerator? instance;
        public static AffinityDrivenEventGenerator Instance => instance ??= new AffinityDrivenEventGenerator();

        private List<StorytellerEventDef> eventDefs = new List<StorytellerEventDef>();
        private System.Random random = new System.Random();

        public AffinityDrivenEventGenerator()
        {
            InitializeEventDefs();
        }

        /// <summary>
        /// 初始化事件定义
        /// </summary>
        private void InitializeEventDefs()
        {
            // === 正面事件 ===
            
            // 资源空投 - 使用安全的方式获取
            var resourcePodDef = DefDatabase<IncidentDef>.GetNamedSilentFail("ResourcePodCrash");
            if (resourcePodDef != null)
            {
                AddEventDef(new StorytellerEventDef
                {
                    defName = "ResourcePodCrash",
                    incidentDef = resourcePodDef,
                    category = EventCategory.Positive,
                    baseWeight = 1f,
                    minAffinity = 0f,
                    commentKey = "TSS_Event_ResourceDrop_Positive"
                });
            }

            // 贸易商队
            AddEventDef(new StorytellerEventDef
            {
                defName = "TraderCaravanArrival",
                incidentDef = IncidentDefOf.TraderCaravanArrival,
                category = EventCategory.Positive,
                baseWeight = 1.2f,
                minAffinity = -20f,
                commentKey = "TSS_Event_Trader_Positive"
            });

            // 流浪者加入
            AddEventDef(new StorytellerEventDef
            {
                defName = "WandererJoin",
                incidentDef = IncidentDefOf.WandererJoin,
                category = EventCategory.Positive,
                baseWeight = 1f,
                minAffinity = 30f,
                preferredTrait = PersonalityTrait.Benevolent,
                commentKey = "TSS_Event_WandererJoin_Positive"
            });

            // === 负面事件 ===

            // 袭击
            AddEventDef(new StorytellerEventDef
            {
                defName = "Raid",
                category = EventCategory.Negative,
                baseWeight = 1.0f,
                incidentDef = IncidentDefOf.RaidEnemy,
                commentKey = "TSS_Event_Raid"
            });

            // 有毒尘埃
            AddEventDef(new StorytellerEventDef
            {
                defName = "ToxicFallout",
                incidentDef = IncidentDefOf.ToxicFallout,
                category = EventCategory.Negative,
                baseWeight = 0.8f,
                maxAffinity = 0f,
                preferredTrait = PersonalityTrait.Sadistic,
                commentKey = "TSS_Event_ToxicFallout_Negative"
            });

            // 机械族袭击
            var mechanoidRaid = DefDatabase<IncidentDef>.GetNamedSilentFail("RaidEnemy");
            if (mechanoidRaid != null)
            {
                AddEventDef(new StorytellerEventDef
                {
                    defName = "MechanoidRaid",
                    incidentDef = mechanoidRaid,
                    category = EventCategory.Negative,
                    baseWeight = 1.2f,
                    maxAffinity = -20f,
                    preferredTrait = PersonalityTrait.Sadistic,
                    commentKey = "TSS_Event_MechanoidRaid_Negative"
                });
            }

            // 疾病爆发 - 使用安全方式获取
            var diseaseDef = DefDatabase<IncidentDef>.GetNamedSilentFail("Disease_Flu");
            if (diseaseDef == null)
            {
                // 尝试其他疾病事件
                diseaseDef = DefDatabase<IncidentDef>.GetNamedSilentFail("Disease_Plague");
            }
            if (diseaseDef == null)
            {
                // 最后尝试找任何疾病事件
                diseaseDef = DefDatabase<IncidentDef>.AllDefs.FirstOrDefault(d => d.defName.StartsWith("Disease_"));
            }
            
            if (diseaseDef != null)
            {
                AddEventDef(new StorytellerEventDef
                {
                    defName = "Disease",
                    category = EventCategory.Negative,
                    baseWeight = 0.8f,
                    incidentDef = diseaseDef,
                    commentKey = "TSS_Event_Disease"
                });
            }

            // === 中性事件 ===

            // 日蚀
            AddEventDef(new StorytellerEventDef
            {
                defName = "Eclipse",
                incidentDef = IncidentDefOf.Eclipse,
                category = EventCategory.Neutral,
                baseWeight = 1f,
                commentKey = "TSS_Event_Eclipse_Neutral"
            });

            Log.Message($"[EventGenerator] 已加载 {eventDefs.Count} 个事件定义");
        }

        private void AddEventDef(StorytellerEventDef def)
        {
            if (def.incidentDef != null)
            {
                eventDefs.Add(def);
            }
        }

        /// <summary>
        /// 根据叙事者状态选择事件
        /// </summary>
        public StorytellerEventDef? SelectEvent(StorytellerAgent agent, Map map)
        {
            // 过滤可用事件
            var availableEvents = eventDefs.Where(e => 
                e.incidentDef != null &&
                e.incidentDef.TargetAllowed(map) &&
                agent.affinity >= e.minAffinity &&
                agent.affinity <= e.maxAffinity
            ).ToList();

            if (availableEvents.Count == 0)
            {
                return null;
            }

            // 计算权重
            var weightedEvents = availableEvents.Select(e => new
            {
                EventDef = e,
                Weight = CalculateEventWeight(e, agent)
            }).ToList();

            // 加权随机选择
            float totalWeight = weightedEvents.Sum(w => w.Weight);
            float roll = (float)random.NextDouble() * totalWeight;
            float cumulative = 0f;

            foreach (var item in weightedEvents)
            {
                cumulative += item.Weight;
                if (roll <= cumulative)
                {
                    return item.EventDef;
                }
            }

            return weightedEvents.Last().EventDef;
        }

        /// <summary>
        /// 计算事件权重
        /// </summary>
        private float CalculateEventWeight(StorytellerEventDef eventDef, StorytellerAgent agent)
        {
            float weight = eventDef.baseWeight;

            // 好感度影响
            bool isPositive = eventDef.category == EventCategory.Positive;
            float affinityBias = agent.GetEventBias(isPositive);
            
            if (isPositive)
            {
                weight *= 1f + affinityBias; // 好感度高时增加正面事件权重
            }
            else if (eventDef.category == EventCategory.Negative)
            {
                weight *= 1f - affinityBias; // 好感度高时降低负面事件权重
            }

            // 人格特质加成
            if (eventDef.preferredTrait != null && 
                (agent.primaryTrait == eventDef.preferredTrait || agent.secondaryTrait == eventDef.preferredTrait))
            {
                weight *= 1.5f;
            }

            // 混乱特质增加随机性
            if (agent.primaryTrait == PersonalityTrait.Chaotic)
            {
                weight *= 0.5f + (float)random.NextDouble() * 1.5f; // 0.5x - 2x 随机
            }

            return Math.Max(0.1f, weight);
        }

        /// <summary>
        /// 触发事件并生成评论
        /// </summary>
        public bool TriggerEvent(StorytellerEventDef eventDef, Map map, StorytellerAgent agent, out string comment)
        {
            comment = "";

            if (eventDef.incidentDef == null)
            {
                return false;
            }

            // 生成事件
            IncidentParms parms = StorytellerUtility.DefaultParmsNow(eventDef.incidentDef.category, map);
            
            // 根据好感度调整事件强度
            AdjustIncidentIntensity(parms, agent, eventDef);

            bool success = eventDef.incidentDef.Worker.TryExecute(parms);

            if (success)
            {
                // 生成叙事者评论
                comment = GenerateEventComment(eventDef, agent);

                // 记录到记忆系统
                MemoryContextBuilder.RecordEvent(
                    $"触发事件：{eventDef.incidentDef.label} - {comment}",
                    MemoryImportance.High
                );

                // 更新统计
                agent.eventsTriggered++;

                Log.Message($"[EventGenerator] 触发事件: {eventDef.defName}, 评论: {comment}");
            }

            return success;
        }

        /// <summary>
        /// 根据好感度调整事件强度
        /// </summary>
        private void AdjustIncidentIntensity(IncidentParms parms, StorytellerAgent agent, StorytellerEventDef eventDef)
        {
            if (eventDef.category == EventCategory.Negative)
            {
                // 好感度高时降低负面事件强度
                if (agent.affinity > 50f)
                {
                    parms.points *= 0.7f; // 降低30%强度
                }
                else if (agent.affinity < -50f)
                {
                    parms.points *= 1.3f; // 增加30%强度
                }
            }
            else if (eventDef.category == EventCategory.Positive)
            {
                // 好感度高时增加正面事件收益
                if (agent.affinity > 50f)
                {
                    parms.points *= 1.2f;
                }
            }
        }

        /// <summary>
        /// 生成事件评论
        /// </summary>
        private string GenerateEventComment(StorytellerEventDef eventDef, StorytellerAgent agent)
        {
            // 如果有翻译键，使用翻译
            if (!string.IsNullOrEmpty(eventDef.commentKey))
            {
                var translated = eventDef.commentKey.Translate();
                if (translated != eventDef.commentKey)
                {
                    return translated;
                }
            }

            // 否则根据好感度和情绪生成评论
            return GenerateDynamicComment(eventDef, agent);
        }

        /// <summary>
        /// 动态生成评论
        /// </summary>
        private string GenerateDynamicComment(StorytellerEventDef eventDef, StorytellerAgent agent)
        {
            bool isPositive = eventDef.category == EventCategory.Positive;
            bool isNegative = eventDef.category == EventCategory.Negative;

            // 高好感度
            if (agent.affinity > 50f)
            {
                if (isPositive)
                    return "我为你准备了一份礼物，希望你喜欢~";
                if (isNegative)
                    return "抱歉...这次的挑战可能有点难，但我相信你能应对。";
            }
            // 低好感度
            else if (agent.affinity < -50f)
            {
                if (isPositive)
                    return "别高兴太早，这只是暂时的。";
                if (isNegative)
                    return "哈哈，看你怎么处理这个烂摊子！";
            }
            // 中性
            else
            {
                if (isPositive)
                    return "运气不错。";
                if (isNegative)
                    return "又到了考验你的时候。";
            }

            return "事情发生了。";
        }

        /// <summary>
        /// 获取所有可用事件
        /// </summary>
        public List<StorytellerEventDef> GetAllEvents()
        {
            return new List<StorytellerEventDef>(eventDefs);
        }
    }

    /// <summary>
    /// 自动事件触发器 - 定期根据好感度触发事件
    /// </summary>
    public class AutoEventTrigger : GameComponent
    {
        private int ticksSinceLastCheck = 0;
        private const int CheckInterval = 36000; // 10分钟检查一次
        
        // 上次发送通知的好感度等级
        private int lastAffinityTier = 0;

        public AutoEventTrigger(Game game) : base()
        {
        }

        public override void GameComponentTick()
        {
            base.GameComponentTick();

            ticksSinceLastCheck++;

            // 定期检查好感度变化
            if (ticksSinceLastCheck >= CheckInterval)
            {
                CheckAffinityChanges();
                ticksSinceLastCheck = 0;
            }
        }

        /// <summary>
        /// 检查好感度变化并发送通知
        /// </summary>
        private void CheckAffinityChanges()
        {
            var agent = Current.Game?.GetComponent<Narrator.NarratorManager>()?.GetStorytellerAgent();
            if (agent == null) return;

            // 计算当前好感度等级（简化为 -1, 0, 1）
            int currentTier = GetAffinityTier(agent.affinity);

            // 如果等级发生变化，发送通知
            if (currentTier != lastAffinityTier)
            {
                // 只在达到极端值时通知
                if (currentTier == 1 || currentTier == -1)
                {
                    DifficultyModulator.SendDifficultyAdjustmentNotification(agent);
                }

                lastAffinityTier = currentTier;
            }
        }

        /// <summary>
        /// 获取好感度等级
        /// </summary>
        private int GetAffinityTier(float affinity)
        {
            if (affinity > 60f) return 1;   // 友好
            if (affinity < -30f) return -1; // 敌对
            return 0;                        // 中性
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref ticksSinceLastCheck, "ticksSinceLastCheck", 0);
            Scribe_Values.Look(ref lastAffinityTier, "lastAffinityTier", 0);
        }
    }
}
