using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace TheSecondSeat.Storyteller
{
    /// <summary>
    /// 人格特质 - 影响叙事者的行为倾向
    /// </summary>
    public enum PersonalityTrait
    {
        Benevolent,      // 仁慈 - 偏好正面事件
        Sadistic,        // 施虐 - 喜欢折磨玩家
        Chaotic,         // 混乱 - 随机极端
        Strategic,       // 战略 - 平衡难度
        Protective,      // 保护 - 避免致命事件
        Manipulative     // 操控 - 诱导玩家依赖
    }

    /// <summary>
    /// 情绪状态 - 短期情感波动
    /// </summary>
    public enum MoodState
    {
        Joyful,          // 喜悦
        Content,         // 满足
        Neutral,         // 中性
        Irritated,       // 烦躁
        Angry,           // 愤怒
        Melancholic,     // 忧郁
        Excited,         // 兴奋
        Bored            // 无聊
    }

    /// <summary>
    /// 叙事者代理 - 核心人格系统
    /// </summary>
    public class StorytellerAgent : GameComponent
    {
        // === 核心属性 ===
        public string name = "Cassandra";
        public float affinity = 0f;                    // 好感度 (-100 to 100)
        public MoodState currentMood = MoodState.Neutral;
        public PersonalityTrait primaryTrait = PersonalityTrait.Strategic;
        public PersonalityTrait? secondaryTrait = null;
        
        // ⭐ v1.9.3: 运行时性格标签（支持动态修改）
        public List<string> activePersonalityTags = null;

        // Dialogue style used when generating prompts
        public PersonaGeneration.DialogueStyleDef dialogueStyle = new PersonaGeneration.DialogueStyleDef();

        // === 情绪系统 ===
        private float moodValue = 0f;                  // 内部情绪值 (-100 to 100)
        private int ticksSinceLastMoodShift = 0;
        private const int MoodShiftInterval = 30000;   // 每30秒评估一次情绪

        // === 记忆统计 ===
        public int totalConversations = 0;
        public int commandsExecuted = 0;
        public int commandsFailed = 0;
        public int eventsTriggered = 0;

        // === 关系历史 ===
        private List<AffinityEvent> affinityHistory = new List<AffinityEvent>();
        private const int MaxAffinityHistory = 50;

        // ⭐ v3.3.0: 自定义关系轴值 (key -> value)
        private Dictionary<string, float> customRelationships = new Dictionary<string, float>();

        // === 特质影响因子 ===
        private Dictionary<PersonalityTrait, TraitModifiers> traitModifiers = new Dictionary<PersonalityTrait, TraitModifiers>
        {
            { PersonalityTrait.Benevolent, new TraitModifiers { 
                positiveEventBonus = 0.3f, 
                negativeEventPenalty = -0.5f,
                affinityGainMultiplier = 1.2f 
            }},
            { PersonalityTrait.Sadistic, new TraitModifiers { 
                positiveEventBonus = -0.4f, 
                negativeEventPenalty = 0.3f,
                affinityGainMultiplier = 0.8f 
            }},
            { PersonalityTrait.Chaotic, new TraitModifiers { 
                positiveEventBonus = 0f, 
                negativeEventPenalty = 0f,
                affinityGainMultiplier = 1.5f,
                eventRandomness = 0.5f
            }},
            { PersonalityTrait.Strategic, new TraitModifiers { 
                positiveEventBonus = 0.1f, 
                negativeEventPenalty = 0.1f,
                affinityGainMultiplier = 1.0f 
            }},
            { PersonalityTrait.Protective, new TraitModifiers { 
                positiveEventBonus = 0.2f, 
                negativeEventPenalty = -0.6f,
                affinityGainMultiplier = 1.3f 
            }},
            { PersonalityTrait.Manipulative, new TraitModifiers { 
                positiveEventBonus = 0.15f, 
                negativeEventPenalty = 0.15f,
                affinityGainMultiplier = 0.9f,
                dialogueManipulation = 0.3f
            }}
        };

        // ? 添加GameComponent必需的构造函数
        public StorytellerAgent(Game game) : base()
        {
        }

        public StorytellerAgent()
        {
        }

        /// <summary>
        /// 修改好感度并更新对话风格
        /// ? 范围：-100 到 +100（内部使用，由 NarratorManager 从 -1000~1000 映射）
        /// </summary>
        public void ModifyAffinity(float delta, string reason, bool triggerMoodUpdate = true)
        {
            float oldAffinity = affinity;
            
            // 应用性格修正
            float multiplier = GetAffinityMultiplier();
            delta *= multiplier;

            affinity = Math.Max(-100f, Math.Min(100f, affinity + delta));

            // 记录事件
            var evt = new AffinityEvent
            {
                tick = Find.TickManager.TicksGame,
                delta = delta,
                reason = reason,
                newValue = affinity,
                moodAtTime = currentMood
            };

            affinityHistory.Add(evt);
            if (affinityHistory.Count > MaxAffinityHistory)
            {
                affinityHistory.RemoveAt(0);
            }

            Log.Message($"[StorytellerAgent] 好感度变化: {delta:+0.0;-0.0} ({reason}) -> {affinity:F1}");

            // 更新心情
            if (triggerMoodUpdate)
            {
                UpdateMoodBasedOnAffinity(delta);
            }

            // ? **关键修复**：好感度变化时立即更新对话风格
            AdjustDialogueStyleByAffinity();
        }

        /// <summary>
        /// ⭐ v3.3.0: 修改自定义关系轴
        /// </summary>
        public void ModifyRelationship(string axisKey, float delta, string reason)
        {
            if (string.IsNullOrEmpty(axisKey)) return;
            if (axisKey.Equals("Affinity", StringComparison.OrdinalIgnoreCase))
            {
                ModifyAffinity(delta, reason);
                return;
            }

            // 获取或初始化
            if (!customRelationships.ContainsKey(axisKey))
            {
                // 尝试从 PersonaDef 获取初始值
                float initial = 50f;
                var manager = Current.Game?.GetComponent<Narrator.NarratorManager>();
                var persona = manager?.GetCurrentPersona();
                if (persona != null)
                {
                    var axisDef = persona.relationshipAxes.FirstOrDefault(a => a.key == axisKey);
                    if (axisDef != null) initial = axisDef.initial;
                }
                customRelationships[axisKey] = initial;
            }

            // 应用修改
            float current = customRelationships[axisKey];
            float min = 0f;
            float max = 100f;
            
            // 获取范围限制
            var managerRef = Current.Game?.GetComponent<Narrator.NarratorManager>();
            var personaRef = managerRef?.GetCurrentPersona();
            if (personaRef != null)
            {
                var axisDef = personaRef.relationshipAxes.FirstOrDefault(a => a.key == axisKey);
                if (axisDef != null)
                {
                    min = axisDef.min;
                    max = axisDef.max;
                }
            }

            float newValue = Mathf.Clamp(current + delta, min, max);
            customRelationships[axisKey] = newValue;
            
            Log.Message($"[StorytellerAgent] 关系变化 ({axisKey}): {delta:+0.0;-0.0} ({reason}) -> {newValue:F1}");
        }

        /// <summary>
        /// ⭐ v3.3.0: 获取关系值
        /// </summary>
        public float GetRelationship(string axisKey)
        {
            if (string.IsNullOrEmpty(axisKey) || axisKey.Equals("Affinity", StringComparison.OrdinalIgnoreCase))
            {
                return affinity;
            }
            return customRelationships.TryGetValue(axisKey, out float val) ? val : 0f;
        }
        
        /// <summary>
        /// ⭐ v3.3.0: 获取所有自定义关系
        /// </summary>
        public Dictionary<string, float> GetAllCustomRelationships()
        {
            return new Dictionary<string, float>(customRelationships);
        }

        /// <summary>
        /// ⭐ v3.3.0: 获取格式化的关系数据 (用于 Prompt)
        /// </summary>
        public Dictionary<string, string> GetFormattedRelationships(PersonaGeneration.NarratorPersonaDef persona)
        {
            var result = new Dictionary<string, string>();
            
            // 1. 基础好感度
            // 格式: 好感度:当前数值/100(冷淡，魂之绑定)
            // 负值: 好感度:当前数值/-100(冷淡，仇恨)
            
            string affinityMax = affinity >= 0 ? "100" : "-100";
            string currentTier = GetAffinityTierName(affinity);
            string limitTier = affinity >= 0 ? GetAffinityTierName(100f) : GetAffinityTierName(-100f);
            
            // 如果当前就是极限，可能两个词一样，但没关系，保持格式一致
            result["Affinity"] = $"{affinity:F0}/{affinityMax} ({currentTier}, {limitTier})";
            
            // 2. 自定义关系轴
            foreach (var kvp in customRelationships)
            {
                float val = kvp.Value;
                float limit = 100f;
                string label = kvp.Key;
                string currentDesc = "";
                // string limitDesc = "";
                
                // 尝试从 Persona 获取配置
                if (persona != null && persona.relationshipAxes != null)
                {
                    var axisDef = persona.relationshipAxes.FirstOrDefault(a => a.key == kvp.Key);
                    if (axisDef != null)
                    {
                        limit = val >= 0 ? axisDef.max : axisDef.min;
                        if (!string.IsNullOrEmpty(axisDef.label)) label = axisDef.label;
                        
                        // 对于自定义轴，如果没有 Tier 定义，我们暂时只能用 Label
                        currentDesc = label;
                        // limitDesc = "Max";
                    }
                }
                
                // 如果是简单数值，格式化为: 数值/上限
                result[kvp.Key] = $"{val:F0}/{limit}";
            }
            
            return result;
        }

        /// <summary>
        /// 获取好感度修正倍率
        /// </summary>
        private float GetAffinityMultiplier()
        {
            float multiplier = 1f;
            
            if (traitModifiers.TryGetValue(primaryTrait, out var primary))
            {
                multiplier *= primary.affinityGainMultiplier;
            }

            if (secondaryTrait != null && traitModifiers.TryGetValue(secondaryTrait.Value, out var secondary))
            {
                multiplier *= (1f + (secondary.affinityGainMultiplier - 1f) * 0.5f); // 次要特质50%效果
            }

            return multiplier;
        }

        /// <summary>
        /// 根据好感度变化更新情绪
        /// </summary>
        private void UpdateMoodBasedOnAffinity(float affinityDelta)
        {
            moodValue += affinityDelta * 0.5f; // 好感度变化影响情绪值
            moodValue = Math.Max(-100f, Math.Min(100f, moodValue));

            // 更新情绪状态
            currentMood = CalculateMoodState(moodValue, affinity);
        }

        /// <summary>
        /// 计算当前情绪状态
        /// </summary>
        private MoodState CalculateMoodState(float mood, float affinity)
        {
            // 综合考虑情绪值和好感度
            float combinedValue = (mood * 0.7f + affinity * 0.3f);

            if (combinedValue > 60f) return MoodState.Joyful;
            if (combinedValue > 30f) return MoodState.Content;
            if (combinedValue > -10f) return MoodState.Neutral;
            if (combinedValue > -40f) return MoodState.Irritated;
            if (combinedValue > -70f) return MoodState.Angry;
            return MoodState.Melancholic;
        }

        /// <summary>
        /// 定期情绪衰减（回归中性）
        /// </summary>
        public void TickMood()
        {
            ticksSinceLastMoodShift++;

            if (ticksSinceLastMoodShift >= MoodShiftInterval)
            {
                ticksSinceLastMoodShift = 0;

                // 情绪值逐渐向0衰减
                if (Math.Abs(moodValue) > 1f)
                {
                    moodValue *= 0.9f; // 每个周期衰减10%
                    currentMood = CalculateMoodState(moodValue, affinity);
                }
            }
        }

        /// <summary>
        /// 获取事件倾向性修正
        /// </summary>
        public float GetEventBias(bool isPositiveEvent)
        {
            float bias = 0f;

            if (traitModifiers.TryGetValue(primaryTrait, out var primary))
            {
                bias += isPositiveEvent ? primary.positiveEventBonus : primary.negativeEventPenalty;
            }

            if (secondaryTrait != null && traitModifiers.TryGetValue(secondaryTrait.Value, out var secondary))
            {
                bias += (isPositiveEvent ? secondary.positiveEventBonus : secondary.negativeEventPenalty) * 0.5f;
            }

            // 好感度影响
            bias += affinity / 200f; // -0.5 to +0.5

            // 情绪影响
            bias += GetMoodBias() * 0.3f;

            return bias;
        }

        /// <summary>
        /// 获取情绪偏向
        /// </summary>
        private float GetMoodBias()
        {
            return currentMood switch
            {
                MoodState.Joyful => 0.4f,
                MoodState.Content => 0.2f,
                MoodState.Neutral => 0f,
                MoodState.Irritated => -0.2f,
                MoodState.Angry => -0.4f,
                MoodState.Melancholic => -0.3f,
                MoodState.Excited => 0.3f,
                MoodState.Bored => -0.1f,
                _ => 0f
            };
        }

        /// <summary>
        /// 获取性格描述（用于System Prompt）
        /// </summary>
        public string GetPersonalityDescription()
        {
            string primary = GetTraitDescription(primaryTrait);
            string secondary = secondaryTrait != null ? GetTraitDescription(secondaryTrait.Value) : "";

            string description = $"你的核心人格特质是**{GetTraitName(primaryTrait)}**：{primary}";
            
            if (!string.IsNullOrEmpty(secondary))
            {
                description += $"\n你还有次要特质**{GetTraitName(secondaryTrait.Value)}**：{secondary}";
            }

            description += $"\n\n当前情绪：{GetMoodName(currentMood)}";
            description += $"\n好感度：{affinity:F0}/100 ({GetAffinityTierName(affinity)})";
            
            // ? 添加好感度对角色定位的影响
            description += GetAffinityRoleGuidance(affinity);

            return description;
        }
        
        /// <summary>
        /// 获取好感度等级名称
        /// </summary>
        public string GetAffinityTierName(float affinity)
        {
            if (affinity >= 85f) return "TSS_Affinity_SoulBound".Translate();
            if (affinity >= 60f) return "TSS_Affinity_Adoration".Translate();
            if (affinity >= 30f) return "TSS_Affinity_Warm".Translate();
            if (affinity >= -10f) return "TSS_Affinity_Neutral".Translate();
            if (affinity >= -50f) return "TSS_Affinity_Cold".Translate();
            return "TSS_Affinity_Hostile".Translate();
        }
        
        /// <summary>
        /// 根据好感度提供角色定位指引
        /// </summary>
        private string GetAffinityRoleGuidance(float affinity)
        {
            if (affinity >= 85f)
            {
                // 爱慕/灵魂绑定 - 极度亲密
                return "TSS_AffinityRole_SoulBound".Translate();
            }
            else if (affinity >= 60f)
            {
                // 倾慕 - 深度忠诚
                return "TSS_AffinityRole_Adoration".Translate();
            }
            else if (affinity >= 30f)
            {
                // 温暖 - 友好
                return "TSS_AffinityRole_Warm".Translate();
            }
            else if (affinity >= -10f)
            {
                // 中性
                return "TSS_AffinityRole_Neutral".Translate();
            }
            else if (affinity >= -50f)
            {
                // 疏远
                return "TSS_AffinityRole_Aloof".Translate();
            }
            else
            {
                // 敌对/仇恨
                return "TSS_AffinityRole_Hostile".Translate();
            }
        }

        /// <summary>
        /// 获取对话风格（用于生成提示时）
        /// </summary>
        public PersonaGeneration.DialogueStyleDef GetDialogueStyle()
        {
            return dialogueStyle;
        }

        /// <summary>
        /// 设置对话风格（用于测试和调整）
        /// </summary>
        public void SetDialogueStyle(PersonaGeneration.DialogueStyleDef newStyle)
        {
            dialogueStyle = newStyle;
        }

        /// <summary>
        /// 定期（每小时）刷新心情和好感度状态
        /// </summary>
        public void HourlyUpdate()
        {
            // 心情衰减
            TickMood();
        }

        /// <summary>
        /// 每日总结与重置（保留部分记忆）
        /// </summary>
        public void DailyReset()
        {
            // 保存简要的关系历史
            foreach (var evt in affinityHistory)
            {
                // 可选：将重要的事件（如好感度显著变化）记录到日志或统计
            }

            // 重置每日统计
            totalConversations = 0;
            commandsExecuted = 0;
            commandsFailed = 0;
            eventsTriggered = 0;

            // 心情和好感度每日恢复
            ticksSinceLastMoodShift = 0;
        }

        /// <summary>
        /// 根据好感度调整对话风格
        /// </summary>
        public void AdjustDialogueStyleByAffinity()
        {
            // 根据好感度等级调整对话风格参数
            if (affinity >= 85f)
            {
                // 爱慕/灵魂绑定 - 极度亲密
                dialogueStyle.formalityLevel = 0.2f;      // 非常随意
                dialogueStyle.emotionalExpression = 0.9f; // 高情感表达
                dialogueStyle.verbosity = 0.7f;           // 话较多
                dialogueStyle.useEmoticons = true;
            }
            else if (affinity >= 60f)
            {
                // 倾慕 - 温暖友好
                dialogueStyle.formalityLevel = 0.3f;
                dialogueStyle.emotionalExpression = 0.7f;
                dialogueStyle.verbosity = 0.6f;
                dialogueStyle.useEmoticons = true;
            }
            else if (affinity >= 30f)
            {
                // 温暖 - 友好
                dialogueStyle.formalityLevel = 0.4f;
                dialogueStyle.emotionalExpression = 0.5f;
                dialogueStyle.verbosity = 0.5f;
            }
            else if (affinity >= -10f)
            {
                // 中性 - 专业
                dialogueStyle.formalityLevel = 0.6f;
                dialogueStyle.emotionalExpression = 0.3f;
                dialogueStyle.verbosity = 0.4f;
                dialogueStyle.useEmoticons = false;
            }
            else if (affinity >= -50f)
            {
                // 疏远 - 冷淡
                dialogueStyle.formalityLevel = 0.7f;
                dialogueStyle.emotionalExpression = 0.2f;
                dialogueStyle.verbosity = 0.3f;
            }
            else
            {
                // 敌对 - 冷酷
                dialogueStyle.formalityLevel = 0.8f;
                dialogueStyle.emotionalExpression = 0.1f;
                dialogueStyle.verbosity = 0.2f;
            }
        }

        /// <summary>
        /// 获取特质名称
        /// </summary>
        private string GetTraitName(PersonalityTrait trait)
        {
            return $"TSS_Trait_{trait}".Translate();
        }

        /// <summary>
        /// 获取特质描述
        /// </summary>
        private string GetTraitDescription(PersonalityTrait trait)
        {
            return $"TSS_TraitDesc_{trait}".Translate();
        }

        /// <summary>
        /// 获取情绪名称
        /// </summary>
        private string GetMoodName(MoodState mood)
        {
            return $"TSS_Mood_{mood}".Translate();
        }

        /// <summary>
        /// 保存/加载数据
        /// </summary>
        public override void ExposeData()
        {
            base.ExposeData();
            
            Scribe_Values.Look(ref name, "name", "Cassandra");
            Scribe_Values.Look(ref affinity, "affinity", 0f);
            Scribe_Values.Look(ref currentMood, "currentMood", MoodState.Neutral);
            Scribe_Values.Look(ref primaryTrait, "primaryTrait", PersonalityTrait.Strategic);
            Scribe_Values.Look(ref secondaryTrait, "secondaryTrait", null);
            Scribe_Collections.Look(ref activePersonalityTags, "activePersonalityTags", LookMode.Value);
            Scribe_Values.Look(ref moodValue, "moodValue", 0f);
            Scribe_Values.Look(ref ticksSinceLastMoodShift, "ticksSinceLastMoodShift", 0);
            Scribe_Values.Look(ref totalConversations, "totalConversations", 0);
            Scribe_Values.Look(ref commandsExecuted, "commandsExecuted", 0);
            Scribe_Values.Look(ref commandsFailed, "commandsFailed", 0);
            Scribe_Values.Look(ref eventsTriggered, "eventsTriggered", 0);
            
            Scribe_Deep.Look(ref dialogueStyle, "dialogueStyle");
            if (dialogueStyle == null)
            {
                dialogueStyle = new PersonaGeneration.DialogueStyleDef();
            }
            
            Scribe_Collections.Look(ref affinityHistory, "affinityHistory", LookMode.Deep);
            if (affinityHistory == null)
            {
                affinityHistory = new List<AffinityEvent>();
            }

            Scribe_Collections.Look(ref customRelationships, "customRelationships", LookMode.Value, LookMode.Value);
            if (customRelationships == null)
            {
                customRelationships = new Dictionary<string, float>();
            }
        }

        /// <summary>
        /// ? 获取当前好感度（用于其他组件调用）
        /// </summary>
        public float GetAffinity()
        {
            return affinity;
        }
    }

    /// <summary>
    /// 好感度事件记录
    /// </summary>
    public class AffinityEvent : IExposable
    {
        public int tick;
        public float delta;
        public string reason = "";
        public float newValue;
        public MoodState moodAtTime;

        public void ExposeData()
        {
            Scribe_Values.Look(ref tick, "tick", 0);
            Scribe_Values.Look(ref delta, "delta", 0f);
            Scribe_Values.Look(ref reason, "reason", "");
            Scribe_Values.Look(ref newValue, "newValue", 0f);
            Scribe_Values.Look(ref moodAtTime, "moodAtTime", MoodState.Neutral);
        }
    }

    /// <summary>
    /// 特质修正值
    /// </summary>
    public class TraitModifiers
    {
        public float positiveEventBonus = 0f;
        public float negativeEventPenalty = 0f;
        public float affinityGainMultiplier = 1f;
        public float eventRandomness = 0f;
        public float dialogueManipulation = 0f;
    }

    /// <summary>
    /// 人格特质扩展方法
    /// </summary>
    public static class PersonalityTraitExtensions
    {
        /// <summary>
        /// 获取特质的中文名称
        /// </summary>
        public static string GetChineseName(this PersonalityTrait trait)
        {
            return trait switch
            {
                PersonalityTrait.Benevolent => "仁慈",
                PersonalityTrait.Sadistic => "虐待",
                PersonalityTrait.Chaotic => "混沌",
                PersonalityTrait.Strategic => "战略",
                PersonalityTrait.Protective => "守护",
                PersonalityTrait.Manipulative => "操控",
                _ => "未知"
            };
        }
        
        /// <summary>
        /// 获取特质的详细描述
        /// </summary>
        public static string GetChineseDescription(this PersonalityTrait trait)
        {
            return trait switch
            {
                PersonalityTrait.Benevolent => "关心殖民者的福祉，偏好正面事件",
                PersonalityTrait.Sadistic => "享受观察凡人挣扎时的某种乐趣",
                PersonalityTrait.Chaotic => "热爱随机性和惊喜",
                PersonalityTrait.Strategic => "看重大局，平衡挑战与奖励",
                PersonalityTrait.Protective => "像守护神一样呵护殖民地",
                PersonalityTrait.Manipulative => "善于微妙地影响和引导",
                _ => "以独特的视角观察殖民地"
            };
        }
    }

    /// <summary>
    /// 情绪状态扩展方法
    /// </summary>
    public static class MoodStateExtensions
    {
        /// <summary>
        /// 获取情绪的中文名称
        /// </summary>
        public static string GetChineseName(this MoodState mood)
        {
            return mood switch
            {
                MoodState.Joyful => "喜悦",
                MoodState.Content => "满足",
                MoodState.Neutral => "平静",
                MoodState.Irritated => "烦躁",
                MoodState.Angry => "愤怒",
                MoodState.Melancholic => "忧郁",
                MoodState.Excited => "兴奋",
                MoodState.Bored => "无聊",
                _ => "未知"
            };
        }
    }
}
