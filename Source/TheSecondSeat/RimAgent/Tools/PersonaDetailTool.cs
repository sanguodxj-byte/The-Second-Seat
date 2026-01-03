using System.Threading.Tasks;
using System.Text;
using System.Collections.Generic;
using Verse;
using TheSecondSeat.Narrator;

namespace TheSecondSeat.RimAgent.Tools
{
    /// <summary>
    /// AI Agent 可调用的人格详情工具
    /// 允许 AI 按需获取传记的特定部分，而不是一次性加载全部
    ///
    /// 支持的 section 参数：
    /// - biography: 完整传记/背景故事
    /// - personality: 人格特质和性格标签
    /// - dialogue_style: 对话风格配置
    /// - visual: 外观和视觉描述
    /// - abilities: 特殊能力列表
    /// - all: 获取所有部分（调试用）
    /// </summary>
    public class PersonaDetailTool : ITool
    {
        public string Name => "read_persona_detail";
        
        public string Description => @"Read detailed persona information. Use this when you need specific details about your character.
Available sections:
- biography: Your full backstory and background
- personality: Your personality traits and tags
- dialogue_style: How you should speak (formality, emotion, humor levels)
- visual: Your appearance and visual description
- abilities: Your special abilities
- all: All sections (use sparingly)";
        
        public string ParameterSchema => @"{
  ""type"": ""object"",
  ""properties"": {
    ""section"": {
      ""type"": ""string"",
      ""description"": ""Which section to read: biography, personality, dialogue_style, visual, abilities, or all"",
      ""enum"": [""biography"", ""personality"", ""dialogue_style"", ""visual"", ""abilities"", ""all""]
    }
  },
  ""required"": [""section""]
}";
        
        public async Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
        {
            await Task.CompletedTask; // 保持异步签名
            
            try
            {
                // 解析参数
                string section = "biography";
                if (parameters != null && parameters.TryGetValue("section", out var sectionObj))
                {
                    section = sectionObj?.ToString()?.ToLower() ?? "biography";
                }
                
                // 获取当前人格
                var manager = Current.Game?.GetComponent<NarratorManager>();
                var persona = manager?.GetCurrentPersona();
                
                if (persona == null)
                {
                    return new ToolResult
                    {
                        Success = false,
                        Error = "No persona currently loaded."
                    };
                }
                
                // 根据 section 返回对应内容
                string content = section switch
                {
                    "biography" => GetBiographySection(persona),
                    "personality" => GetPersonalitySection(persona),
                    "dialogue_style" => GetDialogueStyleSection(persona),
                    "visual" => GetVisualSection(persona),
                    "abilities" => GetAbilitiesSection(persona),
                    "all" => GetAllSections(persona),
                    _ => $"Unknown section: {section}. Available: biography, personality, dialogue_style, visual, abilities, all"
                };
                
                return new ToolResult
                {
                    Success = true,
                    Data = content
                };
            }
            catch (System.Exception ex)
            {
                return new ToolResult
                {
                    Success = false,
                    Error = $"Error reading persona detail: {ex.Message}"
                };
            }
        }
        
        private string GetBiographySection(PersonaGeneration.NarratorPersonaDef persona)
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== BIOGRAPHY ===");
            
            if (!string.IsNullOrEmpty(persona.biography))
            {
                sb.AppendLine(persona.biography);
            }
            else
            {
                sb.AppendLine("(No biography defined)");
            }
            
            return sb.ToString();
        }
        
        private string GetPersonalitySection(PersonaGeneration.NarratorPersonaDef persona)
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== PERSONALITY ===");
            
            if (!string.IsNullOrEmpty(persona.personalityType))
            {
                sb.AppendLine($"Type: {persona.personalityType}");
            }
            
            if (!string.IsNullOrEmpty(persona.overridePersonality))
            {
                sb.AppendLine($"Override: {persona.overridePersonality}");
            }
            
            if (persona.personalityTags != null && persona.personalityTags.Count > 0)
            {
                sb.AppendLine($"Tags: {string.Join(", ", persona.personalityTags)}");
            }
            
            if (persona.toneTags != null && persona.toneTags.Count > 0)
            {
                sb.AppendLine($"Tone: {string.Join(", ", persona.toneTags)}");
            }
            
            if (persona.forbiddenWords != null && persona.forbiddenWords.Count > 0)
            {
                sb.AppendLine($"Forbidden words: {string.Join(", ", persona.forbiddenWords)}");
            }
            
            return sb.ToString();
        }
        
        private string GetDialogueStyleSection(PersonaGeneration.NarratorPersonaDef persona)
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== DIALOGUE STYLE ===");
            
            var style = persona.dialogueStyle;
            if (style != null)
            {
                sb.AppendLine($"Formality: {style.formalityLevel:F2} (0=casual, 1=formal)");
                sb.AppendLine($"Emotion: {style.emotionalExpression:F2} (0=calm, 1=expressive)");
                sb.AppendLine($"Verbosity: {style.verbosity:F2} (0=brief, 1=detailed)");
                sb.AppendLine($"Humor: {style.humorLevel:F2}");
                sb.AppendLine($"Sarcasm: {style.sarcasmLevel:F2}");
                
                if (style.useEmoticons) sb.AppendLine("Uses emoticons (~ ? etc.)");
                if (style.useEllipsis) sb.AppendLine("Uses ellipsis (...)");
                if (style.useExclamation) sb.AppendLine("Uses exclamation marks (!)");
            }
            
            return sb.ToString();
        }
        
        private string GetVisualSection(PersonaGeneration.NarratorPersonaDef persona)
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== VISUAL APPEARANCE ===");
            
            if (!string.IsNullOrEmpty(persona.visualDescription))
            {
                sb.AppendLine(persona.visualDescription);
            }
            
            if (persona.visualElements != null && persona.visualElements.Count > 0)
            {
                sb.AppendLine($"Elements: {string.Join(", ", persona.visualElements)}");
            }
            
            if (!string.IsNullOrEmpty(persona.visualMood))
            {
                sb.AppendLine($"Mood: {persona.visualMood}");
            }
            
            sb.AppendLine($"Theme colors: Primary={ColorToHex(persona.primaryColor)}, Accent={ColorToHex(persona.accentColor)}");
            
            return sb.ToString();
        }
        
        private string GetAbilitiesSection(PersonaGeneration.NarratorPersonaDef persona)
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== SPECIAL ABILITIES ===");
            
            if (persona.specialAbilities != null && persona.specialAbilities.Count > 0)
            {
                foreach (var ability in persona.specialAbilities)
                {
                    sb.AppendLine($"- {ability}");
                }
            }
            else
            {
                sb.AppendLine("(No special abilities defined)");
            }
            
            // 降临模式信息
            if (persona.hasDescentMode)
            {
                sb.AppendLine();
                sb.AppendLine("Descent Mode: ENABLED");
                sb.AppendLine($"- Duration: {persona.descentDuration}s");
                sb.AppendLine($"- Cooldown: {persona.descentCooldown}s");
            }
            
            return sb.ToString();
        }
        
        private string GetAllSections(PersonaGeneration.NarratorPersonaDef persona)
        {
            var sb = new StringBuilder();
            sb.AppendLine(GetBiographySection(persona));
            sb.AppendLine(GetPersonalitySection(persona));
            sb.AppendLine(GetDialogueStyleSection(persona));
            sb.AppendLine(GetVisualSection(persona));
            sb.AppendLine(GetAbilitiesSection(persona));
            return sb.ToString();
        }
        
        private string ColorToHex(UnityEngine.Color color)
        {
            return $"#{(int)(color.r * 255):X2}{(int)(color.g * 255):X2}{(int)(color.b * 255):X2}";
        }
    }
}