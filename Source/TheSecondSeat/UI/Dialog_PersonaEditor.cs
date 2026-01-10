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
        private string editCustomSystemPrompt;
        
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
            editCustomSystemPrompt = persona.customSystemPrompt ?? "";
        }

        public override Vector2 InitialSize => new Vector2(WINDOW_WIDTH, WINDOW_HEIGHT);

        public override void DoWindowContents(Rect inRect)
        {
            // 标题
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleCenter;
            Rect titleRect = new Rect(0f, 0f, inRect.width, 40f);
            Widgets.Label(titleRect, "TSS_PersonaEditor_Title".Translate(persona.label));
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;

            // 内容区域
            Rect contentRect = new Rect(MARGIN, 50f, inRect.width - MARGIN * 2, inRect.height - 110f);
            Rect viewRect = new Rect(0f, 0f, contentRect.width - 20f, 800f);
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
            
            // 现有标签列表
            if (editPersonalityTags.Count > 0)
            {
                for (int i = 0; i < editPersonalityTags.Count; i++)
                {
                    Rect tagRowRect = new Rect(0f, curY, viewRect.width, INPUT_HEIGHT);
                    
                    // 标签输入框
                    Rect tagInputRect = new Rect(0f, curY, viewRect.width - 100f, INPUT_HEIGHT);
                    editPersonalityTags[i] = Widgets.TextField(tagInputRect, editPersonalityTags[i]);
                    
                    // 删除按钮
                    Rect deleteButtonRect = new Rect(viewRect.width - 90f, curY, 80f, INPUT_HEIGHT);
                    if (Widgets.ButtonText(deleteButtonRect, "TSS_PersonaEditor_Delete".Translate()))
                    {
                        editPersonalityTags.RemoveAt(i);
                        break;
                    }
                    
                    curY += INPUT_HEIGHT + 5f;
                }
            }
            else
            {
                GUI.color = Color.gray;
                Widgets.Label(new Rect(0f, curY, viewRect.width, INPUT_HEIGHT), "TSS_PersonaEditor_NoTags".Translate());
                GUI.color = Color.white;
                curY += INPUT_HEIGHT + 5f;
            }
            
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
        
        private void SaveChanges()
        {
            try
            {
                // 应用修改
                persona.narratorName = editNarratorName;
                persona.biography = editBiography;
                persona.personalityTags = new List<string>(editPersonalityTags);
                persona.customSystemPrompt = editCustomSystemPrompt;
                
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
