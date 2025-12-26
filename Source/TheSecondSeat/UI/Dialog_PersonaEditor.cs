using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using RimWorld;
using TheSecondSeat.Narrator;
using TheSecondSeat.PersonaGeneration;

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
        }

        public override Vector2 InitialSize => new Vector2(WINDOW_WIDTH, WINDOW_HEIGHT);

        public override void DoWindowContents(Rect inRect)
        {
            // 标题
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleCenter;
            Rect titleRect = new Rect(0f, 0f, inRect.width, 40f);
            Widgets.Label(titleRect, $"编辑人格: {persona.label}");
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;

            // 内容区域
            Rect contentRect = new Rect(MARGIN, 50f, inRect.width - MARGIN * 2, inRect.height - 110f);
            Rect viewRect = new Rect(0f, 0f, contentRect.width - 20f, 800f);
            Widgets.BeginScrollView(contentRect, ref scrollPosition, viewRect);
            
            float curY = 0f;
            
            // ===== 基本信息 =====
            DrawSectionHeader(viewRect.width, ref curY, "基本信息");
            
            // 人格名称
            Widgets.Label(new Rect(0f, curY, LABEL_WIDTH, INPUT_HEIGHT), "人格名称:");
            Rect nameRect = new Rect(LABEL_WIDTH, curY, viewRect.width - LABEL_WIDTH, INPUT_HEIGHT);
            editNarratorName = Widgets.TextField(nameRect, editNarratorName);
            curY += INPUT_HEIGHT + 10f;
            
            // 人格传记
            Widgets.Label(new Rect(0f, curY, LABEL_WIDTH, INPUT_HEIGHT), "人格传记:");
            curY += INPUT_HEIGHT + 5f;
            Rect bioRect = new Rect(0f, curY, viewRect.width, 100f);
            editBiography = Widgets.TextArea(bioRect, editBiography);
            curY += 110f;
            
            // ===== 个性标签（重点功能）=====
            DrawSectionHeader(viewRect.width, ref curY, "个性标签 (Personality Tags)");
            
            // 说明文字
            GUI.color = Color.yellow;
            string tagsHelp = "个性标签用于定义 AI 的性格特征，每个标签会影响对话风格和行为模式。\n" +
                            "例如: cheerful, serious, sarcastic, gentle, protective 等";
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
                    if (Widgets.ButtonText(deleteButtonRect, "删除"))
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
                Widgets.Label(new Rect(0f, curY, viewRect.width, INPUT_HEIGHT), "(暂无标签)");
                GUI.color = Color.white;
                curY += INPUT_HEIGHT + 5f;
            }
            
            // 添加新标签
            curY += 10f;
            Widgets.Label(new Rect(0f, curY, 100f, INPUT_HEIGHT), "添加标签:");
            Rect newTagRect = new Rect(100f, curY, viewRect.width - 220f, INPUT_HEIGHT);
            newTag = Widgets.TextField(newTagRect, newTag);
            Rect addButtonRect = new Rect(viewRect.width - 100f, curY, 100f, INPUT_HEIGHT);
            if (Widgets.ButtonText(addButtonRect, "添加"))
            {
                if (!string.IsNullOrWhiteSpace(newTag))
                {
                    editPersonalityTags.Add(newTag.Trim());
                    newTag = "";
                }
            }
            curY += INPUT_HEIGHT + 20f;
            
            // ===== 对话风格 =====
            DrawSectionHeader(viewRect.width, ref curY, "对话风格");
            
            if (persona.dialogueStyle != null)
            {
                DrawSlider(viewRect.width, ref curY, "正式程度", ref persona.dialogueStyle.formalityLevel);
                DrawSlider(viewRect.width, ref curY, "情感表达", ref persona.dialogueStyle.emotionalExpression);
                DrawSlider(viewRect.width, ref curY, "话语详细度", ref persona.dialogueStyle.verbosity);
                DrawSlider(viewRect.width, ref curY, "幽默程度", ref persona.dialogueStyle.humorLevel);
                DrawSlider(viewRect.width, ref curY, "讽刺程度", ref persona.dialogueStyle.sarcasmLevel);
            }
            
            curY += 20f;
            
            Widgets.EndScrollView();
            
            // ===== 底部按钮 =====
            Rect buttonRect = new Rect(MARGIN, inRect.height - 50f, 150f, BUTTON_HEIGHT);
            
            // 保存按钮
            if (Widgets.ButtonText(buttonRect, "保存修改"))
            {
                SaveChanges();
            }
            
            // 取消按钮
            buttonRect.x += 160f;
            if (Widgets.ButtonText(buttonRect, "取消"))
            {
                this.Close();
            }
            
            // 导出按钮
            buttonRect.x += 160f;
            if (Widgets.ButtonText(buttonRect, "导出为文件"))
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
                
                // ? 修复：直接生成和保存 XML，不处理立绘
                string xmlContent = PersonaDefExporter.GeneratePersonaDefXml(persona);
                string xmlFilePath = PersonaDefExporter.SavePersonaDefXml(persona.defName, xmlContent);
                
                if (!string.IsNullOrEmpty(xmlFilePath))
                {
                    Messages.Message($"人格 '{persona.label}' 已成功保存到 XML 文件！\n" +
                                   $"文件位置: {System.IO.Path.GetFileName(xmlFilePath)}", 
                                   MessageTypeDefOf.PositiveEvent);
                    Log.Message($"[Dialog_PersonaEditor] 人格已保存: {xmlFilePath}");
                }
                else
                {
                    Messages.Message($"人格 '{persona.label}' 修改已应用，但未能保存到文件", MessageTypeDefOf.CautionInput);
                }
                
                this.Close();
            }
            catch (Exception ex)
            {
                Log.Error($"[Dialog_PersonaEditor] 保存人格失败: {ex}");
                Messages.Message($"保存失败: {ex.Message}", MessageTypeDefOf.RejectInput);
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
                
                // ? 修复：直接生成和保存 XML
                string xmlContent = PersonaDefExporter.GeneratePersonaDefXml(persona);
                string xmlFilePath = PersonaDefExporter.SavePersonaDefXml(persona.defName, xmlContent);
                
                if (!string.IsNullOrEmpty(xmlFilePath))
                {
                    Messages.Message($"人格 '{persona.label}' 已导出到 Defs 文件夹！\n" +
                                   $"文件位置: {System.IO.Path.GetFileName(xmlFilePath)}", 
                                   MessageTypeDefOf.PositiveEvent);
                }
                else
                {
                    Messages.Message("导出失败", MessageTypeDefOf.RejectInput);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[Dialog_PersonaEditor] 导出人格失败: {ex}");
                Messages.Message($"导出失败: {ex.Message}", MessageTypeDefOf.RejectInput);
            }
        }
    }
}
