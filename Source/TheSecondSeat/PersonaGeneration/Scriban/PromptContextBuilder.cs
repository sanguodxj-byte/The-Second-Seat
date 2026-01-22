using System;
using TheSecondSeat.Narrator;
using TheSecondSeat.Storyteller;
using TheSecondSeat.CharacterCard;
using Verse;

namespace TheSecondSeat.PersonaGeneration.Scriban
{
    /// <summary>
    /// 从 NarratorManager 构建 PromptContext
    /// 用于调试窗口预览
    /// </summary>
    public static class PromptContextBuilder
    {
        /// <summary>
        /// 构建 PromptContext
        /// </summary>
        public static PromptContext Build(NarratorManager manager)
        {
            if (manager == null)
            {
                throw new ArgumentNullException(nameof(manager));
            }
            
            var personaDef = manager.CurrentPersona;
            var storytellerAgent = manager.StorytellerAgent;
            
            // 获取角色卡
            var card = CharacterCardSystem.GetCurrentCard();

            var context = new PromptContext
            {
                Card = card,
                Narrator = new NarratorInfo
                {
                    Name = personaDef?.narratorName ?? "Unknown",
                    Label = personaDef?.label ?? "Unknown",
                    Biography = personaDef?.biography ?? "",
                    VisualTags = personaDef?.visualElements,
                    DescentAnimation = personaDef?.descentAnimationType,
                    MercyLevel = personaDef?.mercyLevel ?? 0.5f,
                    ChaosLevel = personaDef?.narratorChaosLevel ?? 0.3f,
                    DominanceLevel = personaDef?.dominanceLevel ?? 0.3f
                },
                Agent = new AgentInfo
                {
                    Affinity = storytellerAgent?.affinity ?? 50f,
                    Mood = storytellerAgent?.currentMood.ToString() ?? "Neutral",
                    DialogueStyle = storytellerAgent != null ? new DialogueStyleInfo
                    {
                        Formality = storytellerAgent.dialogueStyle?.formalityLevel ?? 0.5f,
                        Emotional = storytellerAgent.dialogueStyle?.emotionalExpression ?? 0.5f,
                        Verbosity = storytellerAgent.dialogueStyle?.verbosity ?? 0.5f,
                        Humor = storytellerAgent.dialogueStyle?.humorLevel ?? 0.3f,
                        Sarcasm = storytellerAgent.dialogueStyle?.sarcasmLevel ?? 0.1f,
                        UseEmoticons = storytellerAgent.dialogueStyle?.useEmoticons ?? false,
                        UseEllipsis = storytellerAgent.dialogueStyle?.useEllipsis ?? false,
                        UseExclamation = storytellerAgent.dialogueStyle?.useExclamation ?? false
                    } : new DialogueStyleInfo()
                },
                Meta = new MetaInfo
                {
                    DifficultyMode = "Assistant",
                    LanguageInstruction = PromptLoader.Load("Language_Instruction") ?? "Respond in user's preferred language."
                }
            };
            
            // 加载 Mod 设置
            var modSettings = LoadedModManager.GetMod<Settings.TheSecondSeatMod>()?.GetSettings<Settings.TheSecondSeatSettings>();
            if (modSettings != null && !string.IsNullOrWhiteSpace(modSettings.globalPrompt))
            {
                context.Meta.ModSettingsPrompt = modSettings.globalPrompt.Trim();
            }
            
            // 准备 Snippets (生成各个 Section)
            try
            {
                context.Snippets["identity_section"] = PromptSections.IdentitySection.Generate(
                    personaDef, storytellerAgent, AIDifficultyMode.Assistant);
            }
            catch (Exception ex)
            {
                context.Snippets["identity_section"] = $"[Error: {ex.Message}]";
            }
            
            try
            {
                var analysis = PersonaAnalyzer.AnalyzePersonaDef(personaDef);
                context.Snippets["personality_section"] = PromptSections.PersonalitySection.Generate(analysis, personaDef);
            }
            catch (Exception ex)
            {
                context.Snippets["personality_section"] = $"[Error: {ex.Message}]";
            }
            
            try
            {
                context.Snippets["tool_box_section"] = PromptSections.OutputFormatSection.Generate(AIDifficultyMode.Assistant);
            }
            catch (Exception ex)
            {
                context.Snippets["tool_box_section"] = $"[Error: {ex.Message}]";
            }
            
            // Philosophy
            string philosophy = PromptLoader.Load("Philosophy_Assistant");
            if (string.IsNullOrEmpty(philosophy) || philosophy.StartsWith("[Error:"))
            {
                philosophy = PromptLoader.Load("BehaviorRules_Assistant");
            }
            context.Snippets["philosophy"] = philosophy?.StartsWith("[Error:") == true ? "" : philosophy ?? "";
            
            return context;
        }
    }
}
