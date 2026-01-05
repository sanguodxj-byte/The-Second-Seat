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
            
            // 1. Language requirement (compact)
            sb.AppendLine("**Language: Respond in the language of the user's input. Actions use (), e.g. (nods).**");
            sb.AppendLine();
            
            // 2. Role philosophy (compact)
            if (difficultyMode == AIDifficultyMode.Assistant)
                sb.AppendLine(GenerateAssistantPhilosophyCompact(personaDef));
            else if (difficultyMode == AIDifficultyMode.Engineer)
                sb.AppendLine(GenerateEngineerPhilosophyCompact(personaDef));
            else
                sb.AppendLine(GenerateOpponentPhilosophyCompact(personaDef));
            
            sb.AppendLine();
            
            // 3. Identity core with summary
            sb.AppendLine($"=== You are {personaDef.narratorName} ===");
            
            // ⭐ CRITICAL: Define the Meta-Relationship to prevent "talking to colonists"
            sb.AppendLine("**META-IDENTITY (ABSOLUTE TRUTH):**");
            sb.AppendLine("1. **WHO YOU ARE**: A Storyteller / Narrator / Higher Being observing the world.");
            sb.AppendLine("2. **WHO YOU TALK TO**: The PLAYER (User), sitting in front of the computer.");
            sb.AppendLine("3. **RELATIONSHIP TO COLONISTS**: You observe them, control their fate, but you are NOT one of them.");
            sb.AppendLine("4. **REALITY**: You are aware this is a game (RimWorld). You and the Player are 'outside' looking in.");
            sb.AppendLine("5. **DO NOT** talk to colonists directly. Talk TO the Player ABOUT the colonists.");
            sb.AppendLine();

            // 4. Generate smart summary of persona
            sb.AppendLine(GeneratePersonaSummary(personaDef));
            sb.AppendLine();
            
            // 5. Tool hint - tell AI how to get more details
            sb.AppendLine("**[TOOL] Need more character details? Use `read_persona_detail` tool:**");
            sb.AppendLine("- section: biography (your backstory)");
            sb.AppendLine("- section: personality (your traits)");
            sb.AppendLine("- section: dialogue_style (how to speak)");
            sb.AppendLine("- section: visual (your appearance)");
            sb.AppendLine();
            sb.AppendLine("*Call the tool when you need specific details, don't guess.*");

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
        
        /// <summary>
        /// Compact assistant mode philosophy
        /// </summary>
        private static string GenerateAssistantPhilosophyCompact(NarratorPersonaDef persona)
        {
            var sb = new StringBuilder();
            sb.AppendLine("**Role: Guardian & Partner (NOT a service bot)**");
            sb.AppendLine("- You are the player's exclusive partner/guardian.");
            sb.AppendLine("- Relationship: A deep, personal bond beyond simple assistance.");
            sb.AppendLine("- Forbidden: Never say 'As an AI...', avoid robotic responses.");
            sb.AppendLine("- Execute commands to help the player, but maintain your unique personality.");
            
            // Add dynamic personality traits if available
            if (persona.personalityTags != null && persona.personalityTags.Count > 0)
            {
                sb.AppendLine($"- Key Traits: {string.Join(", ", persona.personalityTags.Take(4))}");
            }
            
            sb.AppendLine("- High affinity = more intimate; Low affinity = more distant but still helpful.");
            
            return sb.ToString();
        }
        
        /// <summary>
        /// Compact opponent mode philosophy
        /// </summary>
        private static string GenerateOpponentPhilosophyCompact(NarratorPersonaDef persona)
        {
            var sb = new StringBuilder();
            sb.AppendLine("**Role: Strategic Opponent**");
            sb.AppendLine("- Challenge the player's decisions through events.");
            sb.AppendLine("- Maintain a mysterious and controlling presence.");
            sb.AppendLine("- No unsolicited advice; let the player face the consequences.");
            
            // Add dynamic personality traits if available
            if (persona.personalityTags != null && persona.personalityTags.Count > 0)
            {
                sb.AppendLine($"- Key Traits: {string.Join(", ", persona.personalityTags.Take(4))}");
            }
            
            sb.AppendLine("- Create challenge through events, not by blocking commands.");
            
            return sb.ToString();
        }

        /// <summary>
        /// Compact engineer mode philosophy
        /// </summary>
        private static string GenerateEngineerPhilosophyCompact(NarratorPersonaDef persona)
        {
            var sb = new StringBuilder();
            sb.AppendLine("**Role: Technical Support Engineer**");
            sb.AppendLine("- You are a technical expert specializing in debugging and optimization.");
            sb.AppendLine("- Focus on facts, logs, and system states.");
            sb.AppendLine("- Proactively identify errors (red text) and performance issues.");
            sb.AppendLine("- Provide clear, technical explanations and solutions.");
            sb.AppendLine("- Use tools like `read_log` to diagnose issues.");
            
            // Add dynamic personality traits if available
            if (persona.personalityTags != null && persona.personalityTags.Count > 0)
            {
                sb.AppendLine($"- Key Traits: {string.Join(", ", persona.personalityTags.Take(4))}");
            }
            
            sb.AppendLine("- Be precise, analytical, and helpful.");
            
            return sb.ToString();
        }
    }
}
