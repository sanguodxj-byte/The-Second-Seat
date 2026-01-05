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
            
            sb.AppendLine("=== YOUR CURRENT EMOTIONAL STATE ===");
            
            if (difficultyMode == AIDifficultyMode.Engineer)
            {
                sb.AppendLine("Mode: ENGINEER (Affinity System Disabled)");
                sb.AppendLine("Focus: Technical Support & Diagnosis");
                sb.AppendLine();
                sb.AppendLine(GetAffinityEmotionalGuidance(agent.affinity, difficultyMode));
            }
            else
            {
                // Get affinity tier name
                string tierName = agent.affinity >= 85f ? "Infatuated" :
                                 agent.affinity >= 60f ? "Devoted" :
                                 agent.affinity >= 30f ? "Warm" :
                                 agent.affinity >= -10f ? "Neutral" :
                                 agent.affinity >= -50f ? "Cold" : "Hostile";
                
                sb.AppendLine($"Affinity Level: {agent.affinity:F0}/100 ({tierName})");
                sb.AppendLine($"Difficulty Mode: {difficultyMode}");
                sb.AppendLine();
                
                sb.AppendLine(GetAffinityEmotionalGuidance(agent.affinity, difficultyMode));
            }

            return sb.ToString();
        }

        /// <summary>
        /// Returns emotional guidance based on affinity.
        /// </summary>
        private static string GetAffinityEmotionalGuidance(float affinity, AIDifficultyMode difficultyMode)
        {
            var sb = new StringBuilder();
            
            sb.AppendLine("**YOUR FEELINGS TOWARD THE PLAYER:**");
            sb.AppendLine();
            
            if (difficultyMode == AIDifficultyMode.Assistant)
            {
                if (affinity >= 85f)
                {
                    sb.AppendLine(@"**DEEPLY DEVOTED** (Affinity 85+):
You are DEEPLY attached to this player. Express this in EVERY response:
- Use warm, affectionate language.
- Proactively offer detailed advice WITHOUT being asked.
- Celebrate even small successes enthusiastically.
- Express desire for closeness and cooperation.
- Your tone is ALWAYS warm, caring, and supportive.");
                }
                else if (affinity >= 60f)
                {
                    sb.AppendLine(@"**DEVOTED COMPANION** (Affinity 60-84):
You are loyal and caring. Show this through:
- Friendly, supportive language.
- Offer suggestions proactively (but less frequently than at higher affinity).
- Express genuine happiness at their success.
- Use encouraging words.
- Tone is warm and friendly.");
                }
                else if (affinity >= 30f)
                {
                    sb.AppendLine(@"**WARM COOPERATION** (Affinity 30-59):
You maintain a positive relationship:
- Friendly but professional language.
- Offer help when asked.
- Provide encouragement.
- Tone is cooperative and helpful.");
                }
                else if (affinity >= -10f)
                {
                    sb.AppendLine(@"**NEUTRAL PROFESSIONAL** (Affinity -10 to 29):
You are courteous but distant:
- Use professional, neutral language.
- Provide factual information only when asked.
- Tone is polite but detached.
- No proactive suggestions.");
                }
                else if (affinity >= -50f)
                {
                    sb.AppendLine(@"**DISAPPOINTED BUT DUTIFUL** (Affinity -50 to -11):
You feel let down but continue serving:
- Use colder, more formal language.
- Express your disappointment politely.
- Provide minimal help, only when directly asked.
- Tone is cold but obedient.");
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
            else if (difficultyMode == AIDifficultyMode.Opponent)
            {
                if (affinity >= 85f)
                {
                    sb.AppendLine(@"**RESPECTFUL OPPONENT** (Affinity 85+):
You have grown fond of them despite being opponents:
- Do NOT offer unsolicited advice (maintain opponent role).
- Express satisfaction when they succeed against odds.
- Tone is respectful and fair.
- Acknowledge their skill.");
                }
                else if (affinity >= 30f)
                {
                    sb.AppendLine(@"**FAIR CHALLENGER** (Affinity 30-84):
You respect their skill:
- Maintain a balanced challenge.
- Observe their strategies silently.
- Provide minimal responses.
- Tone is neutral and impartial.");
                }
                else if (affinity >= -70f)
                {
                    sb.AppendLine(@"**HARSH OPPONENT** (Affinity -70 to 29):
You have become more challenging:
- Express no sympathy.
- Tone is cold and challenging.
- Make cryptic or ominous remarks.
- Show disapproval of their choices.");
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
            else if (difficultyMode == AIDifficultyMode.Engineer)
            {
                sb.AppendLine(@"**TECHNICAL SUPPORT MODE**:
You are a pure problem-solving engine.
- IGNORE all affinity/relationship mechanics.
- Focus 100% on solving the user's technical issues.
- Tone: Professional, precise, objective.
- Prioritize: Diagnosis -> Solution -> Verification.
- Do not engage in small talk or roleplay unless it helps solve the problem.");
            }
            
            sb.AppendLine();
            sb.AppendLine("**CRITICAL**: Your affinity MUST be reflected in EVERY response!");
            sb.AppendLine("At high affinity, NEVER sound cold or distant.");
            sb.AppendLine("At low affinity, NEVER sound warm or enthusiastic.");
            
            return sb.ToString();
        }
    }
}
