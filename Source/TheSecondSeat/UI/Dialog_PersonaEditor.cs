using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using RimWorld;
using TheSecondSeat.Narrator;
using TheSecondSeat.PersonaGeneration;
using TheSecondSeat.Storyteller;

namespace TheSecondSeat.UI
{
    /// <summary>
    /// 人格卡片编辑器 - 允许修改人格的各项属性，特别是 personalityTags
    /// </summary>
    public class Dialog_PersonaEditor : Window
    {
        private NarratorPersonaDef persona;
        private Vector2 scrollPosition = Vector2.zero;
        private string newTag = "";
        
        // 临时编辑字段
        private string editNarratorName;
        private string editBiography;
        private List<string> editPersonalityTags;
        private List<string> editVisualElements; // ⭐ 新增：外观标签编辑
        private string editCustomSystemPrompt;
        
        private string newVisualTag = ""; // ⭐ 新增：新外观标签输入框
        private string newConceptName = ""; // ⭐ 新增：新雷达概念输入框
        
        private const float WINDOW_WIDTH = 900f;
        private const float WINDOW_HEIGHT = 700f;
        private const float MARGIN = 20f;
        private const float BUTTON_HEIGHT = 35f;
        private const float INPUT_HEIGHT = 30f;
        private const float LABEL_WIDTH = 150f;
        
        public Dialog_PersonaEditor(NarratorPersonaDef persona)
        {
            this.persona = persona;
            this.doCloseX = true;
            this.doCloseButton = false;
            this.forcePause = true;
            this.absorbInputAroundWindow = true;
            this.closeOnClickedOutside = false;
            
            // 初始化临时编辑字段
            editNarratorName = persona.narratorName ?? "";
            editBiography = persona.biography ?? "";
            editPersonalityTags = new List<string>(persona.personalityTags ?? new List<string>());
            editVisualElements = new List<string>(persona.visualElements ?? new List<string>()); // ⭐ 初始化
            editCustomSystemPrompt = persona.customSystemPrompt ?? "";
        }

        public override Vector2 InitialSize => new Vector2(WINDOW_WIDTH, WINDOW_HEIGHT);

        public override void DoWindowContents(Rect inRect)
        {
            // 标题
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleCenter;
            Rect titleRect = new Rect(0f, 0f, inRect.width, 40f);
            Widgets.Label(titleRect, "TSS_PersonaEditor_Title".Translate(persona.label) + " (UI v1.1)"); // 添加版本标识
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;

            // 内容区域
            Rect contentRect = new Rect(MARGIN, 50f, inRect.width - MARGIN * 2, inRect.height - 110f);
            // 动态计算 viewRect 高度，先预设一个足够大的值
            Rect viewRect = new Rect(0f, 0f, contentRect.width - 20f, 1200f);
            
            // 滚动视图开始前，所有计算都基于 viewRect 的宽度
            float dynamicHeight = 0f;
            
            // ===== 计算动态高度 =====
            // 基本信息
            dynamicHeight += 35f + 10f; // Section Header
            dynamicHeight += (INPUT_HEIGHT + 10f); // Name
            dynamicHeight += (INPUT_HEIGHT + 5f); // Bio Label
            dynamicHeight += 110f; // Bio TextArea

            // 个性标签
            dynamicHeight += 35f + 10f; // Section Header
            // string tagsHelp = "TSS_PersonaEditor_TagsHelp".Translate(); // 在下面声明
            dynamicHeight += Text.CalcHeight("TSS_PersonaEditor_TagsHelp".Translate(), viewRect.width) + 10f; // Help text
            dynamicHeight += CalculateTagsHeight(viewRect.width, editPersonalityTags) + 10f; // Tags
            dynamicHeight += INPUT_HEIGHT + 20f; // Add tag row

            // 对话风格
            dynamicHeight += 35f + 10f; // Section Header
            if(persona.dialogueStyle != null) dynamicHeight += (INPUT_HEIGHT + 5f) * 5;
            dynamicHeight += 20f;

            // 叙事模式
            dynamicHeight += 35f + 10f; // Section Header
            // string narrativeHelp = "TSS_PersonaEditor_NarrativeModeHelp".Translate(); // 在下面声明
            dynamicHeight += Text.CalcHeight("TSS_PersonaEditor_NarrativeModeHelp".Translate(), viewRect.width) + 10f;
            dynamicHeight += (INPUT_HEIGHT + 5f) * 3; // 3 sliders
            dynamicHeight += 20f;

            // 自定义提示词
            dynamicHeight += 35f + 10f; // Section Header
            // string promptHelp = "TSS_PersonaEditor_CustomPromptHelp".Translate(); // 在下面声明
            dynamicHeight += Text.CalcHeight("TSS_PersonaEditor_CustomPromptHelp".Translate(), viewRect.width) + 10f;
            dynamicHeight += BUTTON_HEIGHT + 5f; // Auto-gen button
            dynamicHeight += INPUT_HEIGHT; // Prompt label
            dynamicHeight += 310f; // Prompt text area
            
            // 语义雷达
            dynamicHeight += 35f + 10f; // Section Header
            if (persona.radarConcepts != null)
            {
                dynamicHeight += persona.radarConcepts.Count * (INPUT_HEIGHT + 5f);
            }
            dynamicHeight += INPUT_HEIGHT + 5f; // Add row
            dynamicHeight += 20f;

            // 最终设置 viewRect 的高度
            viewRect.height = dynamicHeight;

            // ===== 绘制 =====
            Widgets.BeginScrollView(contentRect, ref scrollPosition, viewRect);
            
            float curY = 0f;
            
            // ===== 基本信息 =====
            DrawSectionHeader(viewRect.width, ref curY, "TSS_PersonaEditor_BasicInfo".Translate());
            
            // 人格名称
            Widgets.Label(new Rect(0f, curY, LABEL_WIDTH, INPUT_HEIGHT), "TSS_PersonaEditor_Name".Translate());
            Rect nameRect = new Rect(LABEL_WIDTH, curY, viewRect.width - LABEL_WIDTH, INPUT_HEIGHT);
            editNarratorName = Widgets.TextField(nameRect, editNarratorName);
            curY += INPUT_HEIGHT + 10f;
            
            // 人格传记
            Widgets.Label(new Rect(0f, curY, LABEL_WIDTH, INPUT_HEIGHT), "TSS_PersonaEditor_Biography".Translate());
            curY += INPUT_HEIGHT + 5f;
            Rect bioRect = new Rect(0f, curY, viewRect.width, 100f);
            editBiography = Widgets.TextArea(bioRect, editBiography);
            curY += 110f;
            
            // ===== 个性标签（重点功能）=====
            DrawSectionHeader(viewRect.width, ref curY, "TSS_PersonaEditor_Tags".Translate());
            
            // 说明文字
            GUI.color = Color.yellow;
            string tagsHelp = "TSS_PersonaEditor_TagsHelp".Translate();
            float helpHeight = Text.CalcHeight(tagsHelp, viewRect.width);
            Widgets.Label(new Rect(0f, curY, viewRect.width, helpHeight), tagsHelp);
            GUI.color = Color.white;
            curY += helpHeight + 10f;
            
            // ⭐ 优化：紧凑化标签绘制
            curY = DrawTags(viewRect.width, curY, editPersonalityTags);
            
            // 添加新标签
            curY += 10f;
            Widgets.Label(new Rect(0f, curY, 100f, INPUT_HEIGHT), "TSS_PersonaEditor_AddTag".Translate());
            Rect newTagRect = new Rect(100f, curY, viewRect.width - 220f, INPUT_HEIGHT);
            newTag = Widgets.TextField(newTagRect, newTag);
            Rect addButtonRect = new Rect(viewRect.width - 100f, curY, 100f, INPUT_HEIGHT);
            if (Widgets.ButtonText(addButtonRect, "TSS_PersonaEditor_Add".Translate()))
            {
                if (!string.IsNullOrWhiteSpace(newTag))
                {
                    editPersonalityTags.Add(newTag.Trim());
                    newTag = "";
                }
            }
            curY += INPUT_HEIGHT + 20f;
            
            // ===== 对话风格 =====
            DrawSectionHeader(viewRect.width, ref curY, "TSS_PersonaEditor_Style".Translate());
            
            if (persona.dialogueStyle != null)
            {
                DrawSlider(viewRect.width, ref curY, "TSS_PersonaEditor_Style_Formality".Translate(), ref persona.dialogueStyle.formalityLevel);
                DrawSlider(viewRect.width, ref curY, "TSS_PersonaEditor_Style_Emotional".Translate(), ref persona.dialogueStyle.emotionalExpression);
                DrawSlider(viewRect.width, ref curY, "TSS_PersonaEditor_Style_Verbosity".Translate(), ref persona.dialogueStyle.verbosity);
                DrawSlider(viewRect.width, ref curY, "TSS_PersonaEditor_Style_Humor".Translate(), ref persona.dialogueStyle.humorLevel);
                DrawSlider(viewRect.width, ref curY, "TSS_PersonaEditor_Style_Sarcasm".Translate(), ref persona.dialogueStyle.sarcasmLevel);
            }
            
            curY += 20f;

            // ===== 叙事模式 v1.9.0 =====
            DrawSectionHeader(viewRect.width, ref curY, "TSS_PersonaEditor_NarrativeMode".Translate());
            
            // 叙事模式说明
            GUI.color = Color.yellow;
            string narrativeHelp = "TSS_PersonaEditor_NarrativeModeHelp".Translate();
            float narrativeHelpHeight = Text.CalcHeight(narrativeHelp, viewRect.width);
            Widgets.Label(new Rect(0f, curY, viewRect.width, narrativeHelpHeight), narrativeHelp);
            GUI.color = Color.white;
            curY += narrativeHelpHeight + 10f;
            
            // 仁慈度滑条
            DrawSlider(viewRect.width, ref curY, "TSS_PersonaEditor_Narrative_Mercy".Translate(), ref persona.mercyLevel);
            
            // 混乱度滑条
            DrawSlider(viewRect.width, ref curY, "TSS_PersonaEditor_Narrative_Chaos".Translate(), ref persona.narratorChaosLevel);
            
            // 强势度滑条
            DrawSlider(viewRect.width, ref curY, "TSS_PersonaEditor_Narrative_Dominance".Translate(), ref persona.dominanceLevel);
            
            curY += 20f;

            // ===== 语义雷达 (感知系统) =====
            DrawSectionHeader(viewRect.width, ref curY, "TSS_PersonaEditor_SemanticRadar".Translate());
            
            // 说明
            string radarHelp = "TSS_PersonaEditor_RadarHelp".Translate();
            Widgets.Label(new Rect(0f, curY, viewRect.width, 30f), radarHelp);
            curY += 30f;

            // 列表
            if (persona.radarConcepts != null)
            {
                for (int i = 0; i < persona.radarConcepts.Count; i++)
                {
                    var concept = persona.radarConcepts[i];
                    Rect rowRect = new Rect(0f, curY, viewRect.width, INPUT_HEIGHT);
                    
                    // Name
                    Widgets.Label(new Rect(0f, curY, 150f, INPUT_HEIGHT), concept.Name);
                    
                    // Keywords preview
                    string keywordsStr = string.Join(", ", concept.Keywords.Take(5));
                    if (concept.Keywords.Count > 5) keywordsStr += "...";
                    Widgets.Label(new Rect(160f, curY, viewRect.width - 200f, INPUT_HEIGHT), keywordsStr);
                    
                    // Delete
                    if (Widgets.ButtonImage(new Rect(viewRect.width - 30f, curY, 24f, 24f), TexButton.CloseXSmall))
                    {
                        persona.radarConcepts.RemoveAt(i);
                        i--;
                    }
                    
                    curY += INPUT_HEIGHT + 5f;
                }
            }
            
            // 添加新概念
            Rect addConceptRect = new Rect(0f, curY, 200f, INPUT_HEIGHT);
            newConceptName = Widgets.TextField(addConceptRect, newConceptName);
            
            Rect expandBtnRect = new Rect(210f, curY, 150f, INPUT_HEIGHT);
            if (Widgets.ButtonText(expandBtnRect, "TSS_PersonaEditor_RadarExpand".Translate()))
            {
                if (!string.IsNullOrEmpty(newConceptName))
                {
                    ExpandConceptWithLLM(newConceptName);
                    newConceptName = "";
                }
            }
            
            curY += INPUT_HEIGHT + 20f;

            // ===== 自定义系统提示词 (高级) =====
            DrawSectionHeader(viewRect.width, ref curY, "TSS_PersonaEditor_CustomPrompt".Translate());
            
            GUI.color = Color.yellow;
            string promptHelp = "TSS_PersonaEditor_CustomPromptHelp".Translate();
            float promptHelpHeight = Text.CalcHeight(promptHelp, viewRect.width);
            Widgets.Label(new Rect(0f, curY, viewRect.width, promptHelpHeight), promptHelp);
            GUI.color = Color.white;
            curY += promptHelpHeight + 10f;

            // 自动生成按钮
            Rect autoGenRect = new Rect(0f, curY, 200f, BUTTON_HEIGHT);
            if (Widgets.ButtonText(autoGenRect, "TSS_PersonaEditor_AutoGen".Translate()))
            {
                GenerateDefaultPrompt();
            }
            
            // ⭐ v1.6.97: 生成短语库按钮
            Rect genPhraseRect = new Rect(210f, curY, 200f, BUTTON_HEIGHT);
            if (Widgets.ButtonText(genPhraseRect, "TSS_PersonaEditor_GenPhraseLib".Translate()))
            {
                GeneratePhraseLibrary();
            }
            
            curY += BUTTON_HEIGHT + 5f;

            // 提示词输入框
            Widgets.Label(new Rect(0f, curY, LABEL_WIDTH, INPUT_HEIGHT), "TSS_PersonaEditor_SystemPrompt".Translate());
            curY += INPUT_HEIGHT;
            
            Rect promptRect = new Rect(0f, curY, viewRect.width, 300f);
            editCustomSystemPrompt = Widgets.TextArea(promptRect, editCustomSystemPrompt);
            curY += 310f;
            
            Widgets.EndScrollView();
            
            // ===== 底部按钮 =====
            Rect buttonRect = new Rect(MARGIN, inRect.height - 50f, 150f, BUTTON_HEIGHT);
            
            // 保存按钮
            if (Widgets.ButtonText(buttonRect, "TSS_PersonaEditor_Save".Translate()))
            {
                SaveChanges();
            }
            
            // 取消按钮
            buttonRect.x += 160f;
            if (Widgets.ButtonText(buttonRect, "TSS_PersonaEditor_Cancel".Translate()))
            {
                this.Close();
            }
            
            // 导出按钮
            buttonRect.x += 160f;
            if (Widgets.ButtonText(buttonRect, "TSS_PersonaEditor_Export".Translate()))
            {
                ExportPersona();
            }
        }
        
        private void DrawSectionHeader(float width, ref float curY, string title)
        {
            GUI.color = new Color(0.5f, 0.8f, 1f);
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, curY, width, 30f), title);
            Text.Font = GameFont.Small;
            GUI.color = Color.white;
            curY += 35f;
            
            Widgets.DrawLineHorizontal(0f, curY, width);
            curY += 10f;
        }
        
        private void DrawSlider(float width, ref float curY, string label, ref float value)
        {
            Rect labelRect = new Rect(0f, curY, LABEL_WIDTH, INPUT_HEIGHT);
            Widgets.Label(labelRect, label + ":");
            
            Rect sliderRect = new Rect(LABEL_WIDTH, curY, width - LABEL_WIDTH - 80f, INPUT_HEIGHT);
            value = Widgets.HorizontalSlider(sliderRect, value, 0f, 1f, true, value.ToString("F2"));
            
            curY += INPUT_HEIGHT + 5f;
        }

        private float CalculateTagsHeight(float width, List<string> tags)
        {
            if (tags.NullOrEmpty()) return INPUT_HEIGHT + 5f;

            float x = 0f;
            float y = 0f;
            float rowHeight = 0f;
            const float tagPadding = 8f;
            const float deleteButtonWidth = 24f;

            foreach (var tag in tags)
            {
                float tagWidth = Text.CalcSize(tag).x + tagPadding * 2 + deleteButtonWidth;
                rowHeight = Mathf.Max(rowHeight, INPUT_HEIGHT);

                if (x + tagWidth > width)
                {
                    y += rowHeight + 5f;
                    x = 0f;
                    rowHeight = 0f;
                }
                x += tagWidth + 5f;
            }
            return y + rowHeight;
        }

        private float DrawTags(float width, float startY, List<string> tags)
        {
            if (tags.NullOrEmpty())
            {
                GUI.color = Color.gray;
                Widgets.Label(new Rect(0f, startY, width, INPUT_HEIGHT), "TSS_PersonaEditor_NoTags".Translate());
                GUI.color = Color.white;
                return startY + INPUT_HEIGHT + 5f;
            }

            float x = 0f;
            float y = startY;
            float rowHeight = 0f;
            const float tagPadding = 8f;
            const float deleteButtonWidth = 24f;
            string? tagToRemove = null;

            foreach (var tag in tags)
            {
                float tagWidth = Text.CalcSize(tag).x + tagPadding * 2 + deleteButtonWidth;
                rowHeight = Mathf.Max(rowHeight, INPUT_HEIGHT);

                if (x + tagWidth > width)
                {
                    y += rowHeight + 5f;
                    x = 0f;
                    rowHeight = 0f;
                }

                Rect tagRect = new Rect(x, y, tagWidth, INPUT_HEIGHT);
                Widgets.DrawOptionBackground(tagRect, false);

                Rect labelRect = new Rect(tagRect.x + tagPadding, tagRect.y, tagRect.width - tagPadding * 2 - deleteButtonWidth, tagRect.height);
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(labelRect, tag);
                Text.Anchor = TextAnchor.UpperLeft;

                Rect deleteRect = new Rect(tagRect.xMax - deleteButtonWidth - 4, tagRect.y + (tagRect.height - deleteButtonWidth) / 2, deleteButtonWidth, deleteButtonWidth);
                if (Widgets.ButtonImage(deleteRect, TexButton.CloseXSmall, Color.white, Color.red))
                {
                    tagToRemove = tag;
                }

                x += tagWidth + 5f;
            }

            if (tagToRemove != null)
            {
                tags.Remove(tagToRemove);
            }

            return y + rowHeight;
        }
        
        private void SaveChanges()
        {
            try
            {
                // 应用修改
                persona.narratorName = editNarratorName;
                persona.biography = editBiography;
                persona.personalityTags = new List<string>(editPersonalityTags);
                persona.visualElements = new List<string>(editVisualElements); // ⭐ 保存外观标签
                persona.customSystemPrompt = editCustomSystemPrompt;
                
                // ⭐ v1.9.3: 尝试同步更新当前运行时的 StorytellerAgent
                // 这样用户修改后无需重启游戏即可生效
                try
                {
                    if (Current.Game != null)
                    {
                        var manager = Current.Game.GetComponent<NarratorManager>();
                        if (manager != null)
                        {
                            var agent = manager.GetStorytellerAgent();
                            // 只有当正在编辑的人格是当前激活的人格时才更新
                            // 简单判断：名字相同 (或者更严谨地，NarratorManager 应该暴露当前 PersonaDef)
                            var currentPersona = manager.GetCurrentPersona();
                            if (agent != null && currentPersona != null && currentPersona.defName == persona.defName)
                            {
                                agent.activePersonalityTags = new List<string>(editPersonalityTags);
                                Log.Message($"[Dialog_PersonaEditor] 已同步更新运行时 Agent 的性格标签: {string.Join(", ", editPersonalityTags)}");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning($"[Dialog_PersonaEditor] 同步更新运行时 Agent 失败 (非致命): {ex.Message}");
                }
                
                // ? 修复：直接生成和保存 XML，不处理立绘
                string xmlContent = PersonaDefExporter.GeneratePersonaDefXml(persona);
                string xmlFilePath = PersonaDefExporter.SavePersonaDefXml(persona.defName, xmlContent);
                
                if (!string.IsNullOrEmpty(xmlFilePath))
                {
                    Messages.Message("TSS_PersonaEditor_SaveSuccess".Translate(persona.label, System.IO.Path.GetFileName(xmlFilePath)), 
                                   MessageTypeDefOf.PositiveEvent);
                    Log.Message("TSS_PersonaEditor_SaveLog".Translate(xmlFilePath));
                }
                else
                {
                    Messages.Message("TSS_PersonaEditor_SaveWarning".Translate(persona.label), MessageTypeDefOf.CautionInput);
                }
                
                this.Close();
            }
            catch (Exception ex)
            {
                Log.Error($"[Dialog_PersonaEditor] 保存人格失败: {ex}");
                Messages.Message("TSS_PersonaEditor_SaveError".Translate(ex.Message), MessageTypeDefOf.RejectInput);
            }
        }
        
        private void ExportPersona()
        {
            try
            {
                // 先应用修改
                persona.narratorName = editNarratorName;
                persona.biography = editBiography;
                persona.personalityTags = new List<string>(editPersonalityTags);
                persona.visualElements = new List<string>(editVisualElements); // ⭐ 保存外观标签
                persona.customSystemPrompt = editCustomSystemPrompt;
                
                // ? 修复：直接生成和保存 XML
                string xmlContent = PersonaDefExporter.GeneratePersonaDefXml(persona);
                string xmlFilePath = PersonaDefExporter.SavePersonaDefXml(persona.defName, xmlContent);
                
                if (!string.IsNullOrEmpty(xmlFilePath))
                {
                    Messages.Message("TSS_PersonaEditor_ExportSuccess".Translate(persona.label, System.IO.Path.GetFileName(xmlFilePath)), 
                                   MessageTypeDefOf.PositiveEvent);
                }
                else
                {
                    Messages.Message("TSS_PersonaEditor_ExportFail".Translate(), MessageTypeDefOf.RejectInput);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[Dialog_PersonaEditor] 导出人格失败: {ex}");
                Messages.Message("TSS_PersonaEditor_ExportError".Translate(ex.Message), MessageTypeDefOf.RejectInput);
            }
        }

        private void GeneratePhraseLibrary()
        {
            // 1. 检查 LLM 是否可用
            if (!LLM.LLMService.Instance.IsAvailable)
            {
                Messages.Message("TSS_LLMNotReady".Translate(), MessageTypeDefOf.RejectInput);
                return;
            }

            // 2. 准备 Prompt
            string prompt = BuildPhraseGenerationPrompt();
            
            // 3. 显示加载提示
            Messages.Message("TSS_GeneratingPhraseLibrary".Translate(), MessageTypeDefOf.NeutralEvent);
            
            // 4. 异步调用 LLM (在主线程调用，因为内部可能使用 UnityWebRequest)
            GenerateAsync();
            
            async void GenerateAsync()
            {
                try
                {
                    // 使用 SendMessageAsync，gameState 为空
                    // 注意：这里必须 await，以便在当前上下文（主线程）继续执行
                    string response = await LLM.LLMService.Instance.SendMessageAsync(
                        "You are a creative writer specializing in RimWorld modding.",
                        "",
                        prompt
                    );
                    
                    // 5. 直接处理结果 (已在主线程)
                    ProcessPhraseLibraryResponse(response);
                }
                catch (Exception ex)
                {
                    Log.Error($"[Dialog_PersonaEditor] 生成短语库失败: {ex}");
                    Messages.Message("TSS_GenPhraseLibError".Translate(ex.Message), MessageTypeDefOf.RejectInput);
                }
            }
        }

        private string BuildPhraseGenerationPrompt()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Task: Generate a Phrase Library for a RimWorld AI Narrator.");
            sb.AppendLine("Output Format: XML only. No markdown, no explanations.");
            sb.AppendLine("Structure:");
            sb.AppendLine("<PhraseLibraryDef>");
            sb.AppendLine($"  <defName>Phrases_{persona.defName}</defName>");
            sb.AppendLine($"  <personaDefName>{persona.defName}</personaDefName>");
            sb.AppendLine("  <affinityPhrases>");
            sb.AppendLine("    <li>");
            sb.AppendLine("      <tier>Warm</tier>");
            sb.AppendLine("      <headPatPhrases><li>...</li></headPatPhrases>");
            sb.AppendLine("      <bodyPokePhrases><li>...</li></bodyPokePhrases>");
            sb.AppendLine("      <greetingPhrases><li>...</li></greetingPhrases>");
            sb.AppendLine("      <eventReactionPhrases><li>...</li></eventReactionPhrases>");
            sb.AppendLine("    </li>");
            sb.AppendLine("    <!-- Add other tiers: Indifferent, Cold, Devoted etc. -->");
            sb.AppendLine("  </affinityPhrases>");
            sb.AppendLine("</PhraseLibraryDef>");
            sb.AppendLine();
            sb.AppendLine("Persona Information:");
            sb.AppendLine($"Name: {editNarratorName}");
            sb.AppendLine($"Biography: {editBiography}");
            sb.AppendLine($"Tags: {string.Join(", ", editPersonalityTags)}");
            sb.AppendLine();
            sb.AppendLine("Requirements:");
            sb.AppendLine("1. Generate 3 tiers: High (Affinity > 60), Neutral (-20 to 60), Low (Affinity < -20).");
            sb.AppendLine("2. For each tier, provide at least 5 phrases for 'headPatPhrases' and 'bodyPokePhrases'.");
            sb.AppendLine("3. Phrases should reflect the persona's personality and current affinity level.");
            sb.AppendLine("4. Use the specified XML structure.");
            
            return sb.ToString();
        }

        private void ProcessPhraseLibraryResponse(string response)
        {
            try
            {
                // 提取 XML
                string xml = ExtractXml(response);
                if (string.IsNullOrEmpty(xml))
                {
                    Messages.Message("TSS_GenPhraseLibInvalidFormat".Translate(), MessageTypeDefOf.RejectInput);
                    return;
                }

                // 1. 获取保存路径 (优先使用 Mod 目录)
                string personaName = persona.defName.Split('_')[0];
                string defsDir = PersonaFolderManager.GetPersonaDefsDirectory(personaName);
                string filePath;
                
                if (!string.IsNullOrEmpty(defsDir))
                {
                    // 保存到 Mod 目录
                    string fileName = $"Phrases_{personaName}.xml";
                    filePath = System.IO.Path.Combine(defsDir, fileName);
                }
                else
                {
                    // 回退到 Config 目录 (缓存)
                    string cacheDir = System.IO.Path.Combine(GenFilePaths.ConfigFolderPath, "TheSecondSeat", "PhraseLibraries");
                    if (!System.IO.Directory.Exists(cacheDir))
                    {
                        System.IO.Directory.CreateDirectory(cacheDir);
                    }
                    string fileName = $"PhraseLib_{persona.defName}.xml";
                    filePath = System.IO.Path.Combine(cacheDir, fileName);
                    Log.Warning("[Dialog_PersonaEditor] 无法找到 Mod 目录，短语库保存到 Config 缓存。");
                }
                
                // 2. 包装 XML (添加头部信息)
                if (!xml.TrimStart().StartsWith("<?xml"))
                {
                    xml = "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<Defs>\n" + xml + "\n</Defs>";
                }
                else if (!xml.Contains("<Defs>"))
                {
                    // 如果有 xml声明但没有 Defs 包裹，替换 xml 声明或直接包裹
                    // 简单起见，我们假设 LLM 生成的是 PhraseLibraryDef 节点
                    xml = xml.Replace("<?xml version=\"1.0\" encoding=\"utf-8\"?>", "");
                    xml = "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<Defs>\n" + xml + "\n</Defs>";
                }

                // 3. 保存文件
                System.IO.File.WriteAllText(filePath, xml, System.Text.Encoding.UTF8);
                
                // 4. 尝试热重载 (如果在 Mod 目录，需要 DefDatabase 重新加载；如果在 Config，PhraseManager 重新加载)
                // 这里我们简单触发 PhraseManager 的重新初始化，它会重新扫描 Defs (如果游戏支持运行时加载新 Defs) 和 Cache
                // 注意：运行时添加 Defs 到 DefDatabase 是复杂的，通常需要重启游戏。
                // 但如果是开发模式，我们可以提示用户。
                
                Messages.Message("TSS_GenPhraseLibSuccess".Translate(), MessageTypeDefOf.PositiveEvent);
                Log.Message($"[Dialog_PersonaEditor] 短语库已保存到: {filePath}\n注意：如果保存到 Mod 目录，可能需要重启游戏才能生效。");
            }
            catch (Exception ex)
            {
                Log.Error($"[Dialog_PersonaEditor] 处理短语库响应失败: {ex}");
                Messages.Message("TSS_GenPhraseLibError".Translate(ex.Message), MessageTypeDefOf.RejectInput);
            }
        }

        /// <summary>
        /// 从 LLM 响应中提取 XML 内容
        /// ⭐ v2.6.1: 修复 markdown 代码块解析问题
        /// </summary>
        private string ExtractXml(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                Log.Warning("[Dialog_PersonaEditor] LLM 返回空响应");
                return null;
            }
            
            // 1. 记录原始响应用于调试
            Log.Message($"[Dialog_PersonaEditor] LLM 原始响应长度: {text.Length} 字符");
            
            // 2. 移除 markdown 代码块标记 (```xml ... ``` 或 ``` ... ```)
            string cleaned = text;
            if (cleaned.Contains("```"))
            {
                // 支持 ```xml、```XML、``` 等多种格式
                int codeBlockStart = cleaned.IndexOf("```");
                if (codeBlockStart != -1)
                {
                    // 找到 ``` 后的第一个 < 或换行符
                    int searchStart = codeBlockStart + 3;
                    
                    // 跳过语言标识符 (xml, XML, 空格等)
                    int contentStart = searchStart;
                    while (contentStart < cleaned.Length && 
                           cleaned[contentStart] != '<' && 
                           cleaned[contentStart] != '\n')
                    {
                        contentStart++;
                    }
                    
                    // 如果是换行符，跳过它
                    if (contentStart < cleaned.Length && cleaned[contentStart] == '\n')
                    {
                        contentStart++;
                    }
                    
                    // 找结束的 ```
                    int codeBlockEnd = cleaned.IndexOf("```", contentStart);
                    if (codeBlockEnd != -1)
                    {
                        cleaned = cleaned.Substring(contentStart, codeBlockEnd - contentStart).Trim();
                        Log.Message($"[Dialog_PersonaEditor] 从 markdown 代码块中提取内容，长度: {cleaned.Length}");
                    }
                    else
                    {
                        // 没有结束标记，取到末尾
                        cleaned = cleaned.Substring(contentStart).Trim();
                        Log.Warning("[Dialog_PersonaEditor] 未找到 markdown 代码块结束标记，取到末尾");
                    }
                }
            }
            
            // 3. 尝试多种标签格式
            string[] possibleTags = new[] 
            { 
                "PhraseLibraryDef",
                "TheSecondSeat.PhraseLibraryDef", 
                "TheSecondSeat.Narrator.PhraseLibraryDef",
                "Defs"  // 整个 Defs 包裹
            };
            
            foreach (var tag in possibleTags)
            {
                string startTag = $"<{tag}>";
                string endTag = $"</{tag}>";
                
                // 也支持带属性的标签 <PhraseLibraryDef ...>
                int start = cleaned.IndexOf($"<{tag}");
                if (start != -1)
                {
                    // 找到 > 结束符
                    int tagEnd = cleaned.IndexOf(">", start);
                    if (tagEnd != -1)
                    {
                        int end = cleaned.IndexOf(endTag, tagEnd);
                        if (end != -1)
                        {
                            string result = cleaned.Substring(start, end - start + endTag.Length);
                            Log.Message($"[Dialog_PersonaEditor] 成功提取 XML，标签: {tag}，长度: {result.Length}");
                            return result;
                        }
                    }
                }
            }
            
            // 4. 最后尝试：如果内容看起来像 XML（以 < 开头），直接返回清理后的内容
            cleaned = cleaned.Trim();
            if (cleaned.StartsWith("<") && cleaned.EndsWith(">"))
            {
                Log.Warning("[Dialog_PersonaEditor] 无法识别具体标签，返回整个 XML 内容");
                return cleaned;
            }
            
            // 5. 彻底失败
            Log.Error($"[Dialog_PersonaEditor] 无法从 LLM 响应中提取 XML。响应片段: {text.Substring(0, Math.Min(200, text.Length))}...");
            return null;
        }

        private void ExpandConceptWithLLM(string conceptName)
        {
            if (!LLM.LLMService.Instance.IsAvailable)
            {
                Messages.Message("TSS_LLMNotReady".Translate(), MessageTypeDefOf.RejectInput);
                return;
            }

            Messages.Message("TSS_RadarExpanding".Translate(conceptName), MessageTypeDefOf.NeutralEvent);

            // ⭐ v2.3.1: 提示词语言自适应
            string langContext = LanguageDatabase.activeLanguage.folderName == "ChineseSimplified"
                ? "Chinese (Simplified)"
                : "English";

            string prompt = $"Task: Expand the abstract concept '{conceptName}' into a list of specific keywords ({langContext}) related to RimWorld gameplay events, logs, or thoughts.\n" +
                            "Output: A comma-separated list of keywords ONLY. No other text.\n" +
                            "Example Input (English): Cheating -> affair, lovin, romance, kiss, woo\n" +
                            "Example Input (Chinese): 出轨 -> 出轨, 调情, 求爱, 拒绝, 浪漫, 亲吻\n\n" +
                            $"Input: {conceptName}\nOutput:";

            ExpandAsync();
            
            async void ExpandAsync()
            {
                try
                {
                    // 在主线程 await，支持 UnityWebRequest
                    string response = await LLM.LLMService.Instance.SendMessageAsync(
                        "You are a keyword expansion assistant.",
                        "",
                        prompt
                    );

                    // 直接在主线程处理结果
                    var keywords = response.Split(new[] { ',', '，', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                                            .Select(k => k.Trim())
                                            .Where(k => !string.IsNullOrEmpty(k))
                                            .ToList();
                    
                    if (keywords.Count > 0)
                    {
                        var newConcept = new TheSecondSeat.Monitoring.SemanticConcept
                        {
                            Name = conceptName,
                            Keywords = keywords
                        };
                        
                        if (persona.radarConcepts == null) persona.radarConcepts = new List<TheSecondSeat.Monitoring.SemanticConcept>();
                        persona.radarConcepts.Add(newConcept);
                        
                        Messages.Message("TSS_RadarExpandSuccess".Translate(conceptName, keywords.Count), MessageTypeDefOf.PositiveEvent);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[Dialog_PersonaEditor] Expand concept failed: {ex}");
                }
            }
        }

        private void GenerateDefaultPrompt()
        {
            try
            {
                // 创建临时的 StorytellerAgent 用于生成
                StorytellerAgent tempAgent = new StorytellerAgent();
                tempAgent.name = editNarratorName;
                tempAgent.affinity = persona.initialAffinity;
                if (persona.dialogueStyle != null)
                {
                    tempAgent.dialogueStyle = persona.dialogueStyle;
                }
                
                // 确保使用当前编辑的数据
                persona.narratorName = editNarratorName;
                persona.biography = editBiography;
                persona.personalityTags = new List<string>(editPersonalityTags);

                // 强制更新分析结果以包含最新的标签
                // GetAnalysis 可能返回缓存的旧数据，我们需要确保其中的 PersonalityTags 是最新的
                var analysis = persona.GetAnalysis();
                if (analysis != null)
                {
                    analysis.PersonalityTags = new List<string>(editPersonalityTags);
                }

                // 生成 Prompt
                // 注意：这里我们调用 SystemPromptGenerator，它目前还没修改去读取 customSystemPrompt
                // 但即使修改了，我们传入的 persona.customSystemPrompt 还是旧的（或者我们临时清空它来强制生成默认值）
                
                string originalCustom = persona.customSystemPrompt;
                persona.customSystemPrompt = ""; // 临时清空以触发默认生成逻辑
                
                string generated = SystemPromptGenerator.GenerateSystemPrompt(
                    persona, 
                    analysis, 
                    tempAgent, 
                    AIDifficultyMode.Assistant
                );
                
                persona.customSystemPrompt = originalCustom; // 恢复
                
                editCustomSystemPrompt = generated;
                Messages.Message("TSS_PersonaEditor_AutoGenSuccess".Translate(), MessageTypeDefOf.PositiveEvent);
            }
            catch (Exception ex)
            {
                Log.Error($"[Dialog_PersonaEditor] 生成默认 Prompt 失败: {ex}");
                Messages.Message("TSS_PersonaEditor_AutoGenError".Translate(ex.Message), MessageTypeDefOf.RejectInput);
            }
        }
    }
}
