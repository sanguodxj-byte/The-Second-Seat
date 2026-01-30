using System;
using TheSecondSeat.Narrator;
using TheSecondSeat.Storyteller;
using TheSecondSeat.CharacterCard;
using Verse;
using TheSecondSeat.PersonaGeneration;

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
        /// ⭐ v3.1.1: 支持从设置中读取难度模式
        /// </summary>
        public static PromptContext Build(NarratorManager manager)
        {
            if (manager == null)
            {
                throw new ArgumentNullException(nameof(manager));
            }
            
            var personaDef = manager.CurrentPersona;
            var storytellerAgent = manager.StorytellerAgent;
            
            // ⭐ v3.1.1: 从设置中获取难度模式
            var modSettings = LoadedModManager.GetMod<Settings.TheSecondSeatMod>()?.GetSettings<Settings.TheSecondSeatSettings>();
            var difficultyMode = modSettings?.difficultyMode ?? AIDifficultyMode.Assistant;
            
            // 获取角色卡
            var card = CharacterCardSystem.GetCurrentCard();
            // ⭐ v3.1.1: 确保 Card 永不为 null
            if (card == null)
            {
                card = new NarratorStateCard();
            }

            var context = new PromptContext
            {
                Card = card,
                Narrator = new NarratorInfo
                {
                    DefName = personaDef?.defName, // ⭐ v3.0: 传递 Persona DefName
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
                    DifficultyMode = difficultyMode.ToString(),
                    LanguageInstruction = PromptLoader.Load("Language_Instruction", personaDef?.defName) ?? "Respond in user's preferred language."
                }
            };
            
            // 加载 Mod 设置 (已在前面获取)
            if (modSettings != null && !string.IsNullOrWhiteSpace(modSettings.globalPrompt))
            {
                context.Meta.ModSettingsPrompt = modSettings.globalPrompt.Trim();
            }
            
            // ⭐ v2.7.0: 填充服装数据
            try
            {
                if (personaDef != null)
                {
                    context.AvailableOutfits = OutfitSystem.GetFormattedOutfitList(personaDef.defName);
                    context.CurrentOutfit = OutfitSystem.GetOutfitStatusForPrompt(personaDef.defName);
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[PromptContextBuilder] Failed to get outfit info: {ex.Message}");
                context.AvailableOutfits = "(Outfit system unavailable)";
                context.CurrentOutfit = "Current Outfit: Default";
            }

            // 准备 Snippets (生成各个 Section)
            // ⭐ v3.1.1: 根据设置中的难度模式生成对应的 Snippets
            try
            {
                context.Snippets["identity_section"] = PromptSections.IdentitySection.Generate(
                    personaDef, storytellerAgent, difficultyMode);
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
                context.Snippets["tool_box_section"] = PromptSections.OutputFormatSection.Generate(difficultyMode);
            }
            catch (Exception ex)
            {
                context.Snippets["tool_box_section"] = $"[Error: {ex.Message}]";
            }
            
            // Philosophy - 根据难度模式加载对应的哲学文件
            string philosophyFile = $"Philosophy_{difficultyMode}";
            string philosophy = PromptLoader.Load(philosophyFile, personaDef?.defName);
            if (string.IsNullOrEmpty(philosophy) || philosophy.StartsWith("[Error:"))
            {
                string behaviorFile = $"BehaviorRules_{difficultyMode}";
                philosophy = PromptLoader.Load(behaviorFile, personaDef?.defName);
            }
            context.Snippets["philosophy"] = philosophy?.StartsWith("[Error:") == true ? "" : philosophy ?? "";
            
            // ⭐ v2.5.0: 填充服装系统变量
            try
            {
                string personaDefName = personaDef?.defName ?? "";
                context.AvailableOutfits = OutfitDefManager.GetFormattedOutfitList(personaDefName);
                context.CurrentOutfit = OutfitSystem.GetCurrentOutfitTag(personaDefName);
            }
            catch (Exception ex)
            {
                context.AvailableOutfits = "（暂无可用服装）";
                context.CurrentOutfit = "Default";
                Verse.Log.Warning($"[PromptContextBuilder] 加载服装信息失败: {ex.Message}");
            }
            
            return context;
        }
    }
}
