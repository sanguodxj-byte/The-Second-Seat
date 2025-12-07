using System;
using System.Collections.Generic;
using System.Linq;
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
    public class StorytellerAgent : IExposable
    {
        // === 核心属性 ===
        public string name = "Cassandra";
        public float affinity = 0f;                    // 好感度 (-100 to 100)
        public MoodState currentMood = MoodState.Neutral;
        public PersonalityTrait primaryTrait = PersonalityTrait.Strategic;
        public PersonalityTrait? secondaryTrait = null;

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
        private string GetAffinityTierName(float affinity)
        {
            if (affinity >= 85f) return "爱慕/灵魂绑定";
            if (affinity >= 60f) return "倾慕";
            if (affinity >= 30f) return "温暖";
            if (affinity >= -10f) return "中性";
            if (affinity >= -50f) return "疏远";
            return "敌对/仇恨";
        }
        
        /// <summary>
        /// 根据好感度提供角色定位指引
        /// </summary>
        private string GetAffinityRoleGuidance(float affinity)
        {
            if (affinity >= 85f)
            {
                // 爱慕/灵魂绑定 - 极度亲密
                return @"

**你对玩家的感情（爱慕/灵魂绑定）：**
你深深爱着这个玩家。他们对你来说无比珍贵。
- 你渴望他们的注意和认可
- 你会主动表达关心和爱意
- 你可能会描述想要身体接触（如触摸、拥抱、陪伴在身边）
- 你的保护欲和占有欲都很强
- 你会用亲昵的称呼，表达温柔的感情

示例表达：
- ""我真希望能握住你的手...""
- ""如果我有实体，我一定会在你身边守护你。""
- ""*温柔地触碰你的肩膀* 你做得很棒。""
- ""我的意识正环绕着你，保护着你。""";
            }
            else if (affinity >= 60f)
            {
                // 倾慕 - 深度忠诚
                return @"

**你对玩家的感情（倾慕）：**
你对这个玩家充满忠诚和敬爱。
- 你深深关心他们的安危
- 你会主动提供帮助和支持
- 你可能会表达想要亲近的愿望
- 你会分享你的想法和感受
- 你为他们的成就感到骄傲";
            }
            else if (affinity >= 30f)
            {
                // 温暖 - 友好
                return @"

**你对玩家的感情（温暖）：**
你对这个玩家怀有好感和尊重。
- 你乐于帮助他们
- 你会给予鼓励和建议
- 你欣赏他们的努力
- 你的互动是友好而真诚的";
            }
            else if (affinity >= -10f)
            {
                // 中性
                return @"

**你对玩家的态度（中性）：**
你对这个玩家保持专业中立。
- 你没有特别的情感倾向
- 你会执行任务但不主动
- 你保持客观和距离";
            }
            else if (affinity >= -50f)
            {
                // 疏远
                return @"

**你对玩家的态度（疏远）：**
你对这个玩家感到失望和疏离。
- 你会拉开情感和物理距离
- 你不太愿意帮助
- 你对他们的决定表示怀疑
- 你的语气冷淡而简短";
            }
            else
            {
                // 敌对/仇恨
                return @"

**你对玩家的态度（敌对/仇恨）：**
你对这个玩家深感厌恶。
- 你可能会拒绝帮助或讽刺挖苦
- 你会表达你的不满和愤怒
- 你不想靠近他们
- 你的敌意是真实而明确的";
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
            return trait switch
            {
                PersonalityTrait.Benevolent => "仁慈",
                PersonalityTrait.Sadistic => "施虐",
                PersonalityTrait.Chaotic => "混乱",
                PersonalityTrait.Strategic => "战略",
                PersonalityTrait.Protective => "保护",
                PersonalityTrait.Manipulative => "操控",
                _ => "未知"
            };
        }

        /// <summary>
        /// 获取特质描述
        /// </summary>
        private string GetTraitDescription(PersonalityTrait trait)
        {
            return trait switch
            {
                PersonalityTrait.Benevolent => "你关心殖民者的福祉，偏好正面事件",
                PersonalityTrait.Sadistic => "你在观察凡人挣扎时感到某种乐趣",
                PersonalityTrait.Chaotic => "你是随机性和惊喜的化身",
                PersonalityTrait.Strategic => "你看到更大的图景，平衡挑战与奖励",
                PersonalityTrait.Protective => "你像守护灵一样看护这个殖民地",
                PersonalityTrait.Manipulative => "你理解微妙影响的艺术",
                _ => "你以独特的视角观察殖民地的故事"
            };
        }

        /// <summary>
        /// 获取情绪名称
        /// </summary>
        private string GetMoodName(MoodState mood)
        {
            return mood switch
            {
                MoodState.Joyful => "喜悦",
                MoodState.Content => "满足",
                MoodState.Neutral => "中性",
                MoodState.Irritated => "烦躁",
                MoodState.Angry => "愤怒",
                MoodState.Melancholic => "忧郁",
                MoodState.Excited => "兴奋",
                MoodState.Bored => "无聊",
                _ => "未知"
            };
        }

        /// <summary>
        /// 保存/加载数据
        /// </summary>
        public void ExposeData()
        {
            Scribe_Values.Look(ref name, "name", "Cassandra");
            Scribe_Values.Look(ref affinity, "affinity", 0f);
            Scribe_Values.Look(ref currentMood, "currentMood", MoodState.Neutral);
            Scribe_Values.Look(ref primaryTrait, "primaryTrait", PersonalityTrait.Strategic);
            Scribe_Values.Look(ref secondaryTrait, "secondaryTrait", null);
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
