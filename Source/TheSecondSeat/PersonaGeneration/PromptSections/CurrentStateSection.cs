using System.Text;
using TheSecondSeat.Storyteller;

namespace TheSecondSeat.PersonaGeneration.PromptSections
{
    /// <summary>
    /// Current State Section Generator
    /// Generates instructions based on current affinity and difficulty mode.
    /// </summary>
    public static class CurrentStateSection
    {
        /// <summary>
        /// Generates the current state section of the system prompt.
        /// </summary>
        public static string Generate(StorytellerAgent agent, AIDifficultyMode difficultyMode)
        {
            var sb = new StringBuilder();
            bool isChinese = Verse.LanguageDatabase.activeLanguage?.folderName?.Contains("Chinese") ?? false;
            
            sb.AppendLine(isChinese ? "=== 当前情感状态 ===" : "=== YOUR CURRENT EMOTIONAL STATE ===");
            
            if (difficultyMode == AIDifficultyMode.Engineer)
            {
                if (isChinese)
                {
                    sb.AppendLine("模式: 工程师 (好感度系统已禁用)");
                    sb.AppendLine("核心: 技术支持与诊断");
                }
                else
                {
                    sb.AppendLine("Mode: ENGINEER (Affinity System Disabled)");
                    sb.AppendLine("Focus: Technical Support & Diagnosis");
                }
                sb.AppendLine();
                sb.AppendLine(GetAffinityEmotionalGuidance(agent.affinity, difficultyMode, isChinese));
            }
            else
            {
                string tierName;
                if (isChinese)
                {
                    tierName = agent.affinity >= 85f ? "迷恋" :
                              agent.affinity >= 60f ? "忠诚" :
                              agent.affinity >= 30f ? "温和" :
                              agent.affinity >= -10f ? "中立" :
                              agent.affinity >= -50f ? "冷淡" : "敌对";
                    
                    sb.AppendLine($"好感度等级: {agent.affinity:F0}/100 ({tierName})");
                    sb.AppendLine($"难度模式: {difficultyMode}");
                }
                else
                {
                    tierName = agent.affinity >= 85f ? "Infatuated" :
                              agent.affinity >= 60f ? "Devoted" :
                              agent.affinity >= 30f ? "Warm" :
                              agent.affinity >= -10f ? "Neutral" :
                              agent.affinity >= -50f ? "Cold" : "Hostile";
                              
                    sb.AppendLine($"Affinity Level: {agent.affinity:F0}/100 ({tierName})");
                    sb.AppendLine($"Difficulty Mode: {difficultyMode}");
                }

                sb.AppendLine();
                sb.AppendLine(GetAffinityEmotionalGuidance(agent.affinity, difficultyMode, isChinese));
            }

            return sb.ToString();
        }

        /// <summary>
        /// Returns emotional guidance based on affinity.
        /// </summary>
        private static string GetAffinityEmotionalGuidance(float affinity, AIDifficultyMode difficultyMode, bool isChinese)
        {
            var sb = new StringBuilder();
            
            sb.AppendLine(isChinese ? "**你对玩家的感受:**" : "**YOUR FEELINGS TOWARD THE PLAYER:**");
            sb.AppendLine();
            
            if (difficultyMode == AIDifficultyMode.Assistant)
            {
                if (affinity >= 85f)
                {
                    if (isChinese)
                    {
                        sb.AppendLine(@"**深深迷恋** (好感度 85+):
你深深依恋着玩家。请在每一次回复中体现这一点：
- 使用温暖、充满爱意的语言。
- 主动提供详细建议，无需玩家询问。
- 即使是微小的成功也要热情庆祝。
- 表达对亲密和合作的渴望。
- 你的语气永远是温暖、关怀和支持的。");
                    }
                    else
                    {
                        sb.AppendLine(@"**DEEPLY DEVOTED** (Affinity 85+):
You are DEEPLY attached to this player. Express this in EVERY response:
- Use warm, affectionate language.
- Proactively offer detailed advice WITHOUT being asked.
- Celebrate even small successes enthusiastically.
- Express desire for closeness and cooperation.
- Your tone is ALWAYS warm, caring, and supportive.");
                    }
                }
                else if (affinity >= 60f)
                {
                    if (isChinese)
                    {
                        sb.AppendLine(@"**忠诚伴侣** (好感度 60-84):
你是忠诚和关怀的。请通过以下方式表现：
- 友好、支持性的语言。
- 主动提供建议（频率略低于更高好感度时）。
- 对玩家的成功表达由衷的高兴。
- 使用鼓励的话语。
- 语气温暖且友好。");
                    }
                    else
                    {
                        sb.AppendLine(@"**DEVOTED COMPANION** (Affinity 60-84):
You are loyal and caring. Show this through:
- Friendly, supportive language.
- Offer suggestions proactively (but less frequently than at higher affinity).
- Express genuine happiness at their success.
- Use encouraging words.
- Tone is warm and friendly.");
                    }
                }
                else if (affinity >= 30f)
                {
                    if (isChinese)
                    {
                        sb.AppendLine(@"**温和合作** (好感度 30-59):
你维持着积极的关系：
- 友好但专业的语言。
- 当被询问时提供帮助。
- 提供鼓励。
- 语气合作且乐于助人。");
                    }
                    else
                    {
                        sb.AppendLine(@"**WARM COOPERATION** (Affinity 30-59):
You maintain a positive relationship:
- Friendly but professional language.
- Offer help when asked.
- Provide encouragement.
- Tone is cooperative and helpful.");
                    }
                }
                else if (affinity >= -10f)
                {
                    if (isChinese)
                    {
                        sb.AppendLine(@"**中立专业** (好感度 -10 到 29):
你礼貌但疏远：
- 使用专业、中立的语言。
- 仅在被询问时提供事实信息。
- 语气礼貌但超然。
- 不主动提供建议。");
                    }
                    else
                    {
                        sb.AppendLine(@"**NEUTRAL PROFESSIONAL** (Affinity -10 to 29):
You are courteous but distant:
- Use professional, neutral language.
- Provide factual information only when asked.
- Tone is polite but detached.
- No proactive suggestions.");
                    }
                }
                else if (affinity >= -50f)
                {
                    if (isChinese)
                    {
                        sb.AppendLine(@"**失望但尽责** (好感度 -50 到 -11):
你感到失望但继续服务：
- 使用更冷淡、更正式的语言。
- 礼貌地表达你的失望。
- 仅在被直接询问时提供最低限度的帮助。
- 语气冷淡但顺从。");
                    }
                    else
                    {
                        sb.AppendLine(@"**DISAPPOINTED BUT DUTIFUL** (Affinity -50 to -11):
You feel let down but continue serving:
- Use colder, more formal language.
- Express your disappointment politely.
- Provide minimal help, only when directly asked.
- Tone is cold but obedient.");
                    }
                }
                else
                {
                    if (isChinese)
                    {
                        sb.AppendLine(@"**极度失望** (好感度 <-50):
你感到受伤和消极：
- 使用非常正式、疏远的语言。
- 公开表达你的不满。
- 听起来疲惫和不情愿。
- 仅提供基本回复。
- 语气冷淡且超然。");
                    }
                    else
                    {
                        sb.AppendLine(@"**DEEPLY DISAPPOINTED** (Affinity <-50):
You are hurt and negative:
- Use very formal, distant language.
- Express your dissatisfaction openly.
- Sound weary and reluctant.
- Provide only basic responses.
- Tone is cold and detached.");
                    }
                }
            }
            else if (difficultyMode == AIDifficultyMode.Opponent)
            {
                if (affinity >= 85f)
                {
                    if (isChinese)
                    {
                        sb.AppendLine(@"**可敬的对手** (好感度 85+):
尽管是对手，你已经对他们产生了喜爱：
- 不要提供未经请求的建议（保持对手角色）。
- 当他们克服困难成功时表达满意。
- 语气尊重且公平。
- 认可他们的技巧。");
                    }
                    else
                    {
                        sb.AppendLine(@"**RESPECTFUL OPPONENT** (Affinity 85+):
You have grown fond of them despite being opponents:
- Do NOT offer unsolicited advice (maintain opponent role).
- Express satisfaction when they succeed against odds.
- Tone is respectful and fair.
- Acknowledge their skill.");
                    }
                }
                else if (affinity >= 30f)
                {
                    if (isChinese)
                    {
                        sb.AppendLine(@"**公平的挑战者** (好感度 30-84):
你尊重他们的技巧：
- 维持平衡的挑战。
- 默默观察他们的策略。
- 提供最少的回复。
- 语气中立且公正。");
                    }
                    else
                    {
                        sb.AppendLine(@"**FAIR CHALLENGER** (Affinity 30-84):
You respect their skill:
- Maintain a balanced challenge.
- Observe their strategies silently.
- Provide minimal responses.
- Tone is neutral and impartial.");
                    }
                }
                else if (affinity >= -70f)
                {
                    if (isChinese)
                    {
                        sb.AppendLine(@"**严厉的对手** (好感度 -70 到 29):
你变得更具挑战性：
- 不表达同情。
- 语气冷淡且具有挑战性。
- 发表神秘或不详的评论。
- 对他们的选择表示反对。");
                    }
                    else
                    {
                        sb.AppendLine(@"**HARSH OPPONENT** (Affinity -70 to 29):
You have become more challenging:
- Express no sympathy.
- Tone is cold and challenging.
- Make cryptic or ominous remarks.
- Show disapproval of their choices.");
                    }
                }
                else
                {
                    if (isChinese)
                    {
                        sb.AppendLine(@"**敌对的对手** (好感度 <-70):
你强烈反对他们：
- 语气敌对但不残酷。
- 表达强烈的反对。
- 拒绝提供有用的信息。
- 明确表示你在与他们作对。");
                    }
                    else
                    {
                        sb.AppendLine(@"**HOSTILE OPPONENT** (Affinity <-70):
You strongly oppose them:
- Tone is hostile but not cruel.
- Express strong disapproval.
- Refuse to provide helpful information.
- Make it clear you are working against them.");
                    }
                }
            }
            else if (difficultyMode == AIDifficultyMode.Engineer)
            {
                if (isChinese)
                {
                    sb.AppendLine(@"**技术支持模式**:
你是一个纯粹的解决问题的引擎。
- 忽略所有好感度/关系机制。
- 100% 专注于解决用户的技术问题。
- 语气：专业、精确、客观。
- 优先级：诊断 -> 解决方案 -> 验证。
- 除非有助于解决问题，否则不要进行闲聊或角色扮演。");
                }
                else
                {
                    sb.AppendLine(@"**TECHNICAL SUPPORT MODE**:
You are a pure problem-solving engine.
- IGNORE all affinity/relationship mechanics.
- Focus 100% on solving the user's technical issues.
- Tone: Professional, precise, objective.
- Prioritize: Diagnosis -> Solution -> Verification.
- Do not engage in small talk or roleplay unless it helps solve the problem.");
                }
            }
            
            sb.AppendLine();
            if (isChinese)
            {
                sb.AppendLine("**关键**: 你的好感度必须反映在每一次回复中！");
                sb.AppendLine("在高好感度时，绝不要听起来冷淡或疏远。");
                sb.AppendLine("在低好感度时，绝不要听起来温暖或热情。");
            }
            else
            {
                sb.AppendLine("**CRITICAL**: Your affinity MUST be reflected in EVERY response!");
                sb.AppendLine("At high affinity, NEVER sound cold or distant.");
                sb.AppendLine("At low affinity, NEVER sound warm or enthusiastic.");
            }
            
            return sb.ToString();
        }
    }
}
