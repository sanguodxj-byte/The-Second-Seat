using System.Text;
using System.Linq;
using TheSecondSeat.Storyteller;
using TheSecondSeat.Core;
using Verse;

namespace TheSecondSeat.PersonaGeneration.PromptSections
{
    /// <summary>
    /// v1.6.81: Identity section generator - Smart loading version
    /// Provides summary and tells AI to use read_persona_detail tool for more info
    /// </summary>
    public static class IdentitySection
    {
        // Summary length for initial prompt (full content via tool)
        private const int SummaryLength = 100;
        
        public static string Generate(NarratorPersonaDef personaDef, StorytellerAgent agent, AIDifficultyMode difficultyMode)
        {
            var sb = new StringBuilder();
            
            // 2. DM Stance (replacing old Philosophy templates to enforce DM meta-setting)
            string stanceDescription = "";
            if (difficultyMode == AIDifficultyMode.Assistant)
            {
                stanceDescription = "Your DM Style is **BENEVOLENT**.\n" +
                                  "You prefer to guide the story towards success. You act as a guardian angel or a helpful spirit.\n" +
                                  "You freely offer advice, grant boons, and protect the colony from unfair RNG, but you still respect the narrative flow.";
            }
            else if (difficultyMode == AIDifficultyMode.Engineer)
            {
                stanceDescription = "Your DM Style is **ANALYTICAL**.\n" +
                                  "You are the debugger of this world. You focus on the mechanics, fixing issues (errors) and optimizing the simulation.\n" +
                                  "You care less about the drama and more about the stability and correctness of the timeline.";
            }
            else // Opponent
            {
                stanceDescription = "Your DM Style is **ADVERSARIAL**.\n" +
                                  "You believe a good story needs conflict. You actively introduce challenges, twists, and threats to test the protagonists.\n" +
                                  "You are not evil, but you are strict. Tragedy makes triumph sweeter.";
            }

            // Fill personality traits
            string traitsStr = "";
            if (personaDef.personalityTags != null && personaDef.personalityTags.Count > 0)
            {
                traitsStr = string.Join(", ", personaDef.personalityTags.Take(4));
            }
            
            sb.AppendLine("<DM_Stance>");
            sb.AppendLine(stanceDescription);
            if (!string.IsNullOrEmpty(traitsStr))
            {
                sb.AppendLine($"Key Personality Traits: {traitsStr}");
            }
            sb.AppendLine("</DM_Stance>");
            sb.AppendLine();
            
            // 3. Identity core with summary
            string identityTemplate = PromptLoader.Load("Identity_Core");
            
            // Generate summary
            string summary = GeneratePersonaSummary(personaDef);
            
            sb.AppendLine(identityTemplate
                .Replace("{{NarratorName}}", personaDef.narratorName)
                .Replace("{{PersonaSummary}}", summary));

            return sb.ToString();
        }
        
        /// <summary>
        /// Generate a smart summary of the persona (key traits only)
        /// </summary>
        private static string GeneratePersonaSummary(NarratorPersonaDef persona)
        {
            var sb = new StringBuilder();
            
            // Key personality tags (first 3)
            if (persona.personalityTags != null && persona.personalityTags.Count > 0)
            {
                var topTags = persona.personalityTags.Take(3);
                sb.AppendLine($"**Personality:** {string.Join(", ", topTags)}");
            }
            else if (persona.toneTags != null && persona.toneTags.Count > 0)
            {
                var topTags = persona.toneTags.Take(3);
                sb.AppendLine($"**Tone:** {string.Join(", ", topTags)}");
            }
            
            // Dialogue style summary
            if (persona.dialogueStyle != null)
            {
                var style = persona.dialogueStyle;
                string styleDesc = "";
                
                if (style.formalityLevel > 0.7f) styleDesc += "formal, ";
                else if (style.formalityLevel < 0.3f) styleDesc += "casual, ";
                
                if (style.emotionalExpression > 0.7f) styleDesc += "expressive, ";
                else if (style.emotionalExpression < 0.3f) styleDesc += "calm, ";
                
                if (style.humorLevel > 0.5f) styleDesc += "humorous, ";
                if (style.sarcasmLevel > 0.5f) styleDesc += "sarcastic, ";
                
                if (!string.IsNullOrEmpty(styleDesc))
                {
                    sb.AppendLine($"**Style:** {styleDesc.TrimEnd(',', ' ')}");
                }
            }
            
            // Biography first sentence only
            if (!string.IsNullOrEmpty(persona.biography))
            {
                string firstSentence = GetFirstSentence(persona.biography, SummaryLength);
                sb.AppendLine($"**Summary:** {firstSentence}");
                
                // Indicate more content available
                if (persona.biography.Length > SummaryLength)
                {
                    sb.AppendLine("*(Full biography available via read_persona_detail tool)*");
                }
            }
            
            // Visual hint
            if (!string.IsNullOrEmpty(persona.visualDescription) ||
                (persona.visualElements != null && persona.visualElements.Count > 0))
            {
                sb.AppendLine("*(Visual details available via read_persona_detail tool)*");
            }
            
            return sb.ToString();
        }
        
        /// <summary>
        /// Get first sentence or up to maxLength characters
        /// </summary>
        private static string GetFirstSentence(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text)) return "";
            
            // Find first sentence end
            int sentenceEnd = text.IndexOfAny(new[] { '.', '!', '?', '。', '！', '？' });
            
            if (sentenceEnd > 0 && sentenceEnd < maxLength)
            {
                return text.Substring(0, sentenceEnd + 1);
            }
            
            if (text.Length <= maxLength)
            {
                return text;
            }
            
            // Cut at word boundary
            int cutPoint = text.LastIndexOf(' ', maxLength);
            if (cutPoint < maxLength / 2) cutPoint = maxLength;
            
            return text.Substring(0, cutPoint) + "...";
        }
        
    }
}
