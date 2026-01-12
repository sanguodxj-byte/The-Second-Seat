using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using TheSecondSeat.PersonaGeneration;

namespace TheSecondSeat.UI
{
    public class PromptManagementWindow : Window
    {
        private Vector2 scrollPositionLeft = Vector2.zero;
        private string selectedPromptFile = null;
        private string currentFileContent = "";
        private bool isModified = false;
        
        private List<string> promptFiles = new List<string>();
        
        public override Vector2 InitialSize => new Vector2(900f, 700f);

        public PromptManagementWindow()
        {
            doCloseX = true;
            forcePause = true;
            resizeable = true;
            draggable = true;
            
            RefreshFileList();
        }
        
        private void RefreshFileList()
        {
            promptFiles.Clear();
            
            // Ensure directory exists
            PromptLoader.EnsureConfigDirectory();
            
            string configPromptsPath = Path.Combine(GenFilePaths.ConfigFolderPath, "TheSecondSeat", "Prompts");
            string activeLangFolder = LanguageDatabase.activeLanguage.folderName;
            string targetFolder = Path.Combine(configPromptsPath, activeLangFolder);
            
            if (Directory.Exists(targetFolder))
            {
                string[] files = Directory.GetFiles(targetFolder, "*.txt");
                foreach (string file in files)
                {
                    promptFiles.Add(Path.GetFileNameWithoutExtension(file));
                }
            }
            
            // Also check global folder
            if (Directory.Exists(configPromptsPath))
            {
                string[] globalFiles = Directory.GetFiles(configPromptsPath, "*.txt");
                foreach (string file in globalFiles)
                {
                    string name = Path.GetFileNameWithoutExtension(file);
                    if (!promptFiles.Contains(name) && name != "README")
                    {
                        promptFiles.Add(name);
                    }
                }
            }
            
            promptFiles.Sort();
        }
        
        private void LoadFile(string fileName)
        {
            if (isModified)
            {
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                    "当前文件已修改，是否放弃更改？", 
                    () => LoadFileInternal(fileName),
                    true));
            }
            else
            {
                LoadFileInternal(fileName);
            }
        }
        
        private void LoadFileInternal(string fileName)
        {
            selectedPromptFile = fileName;
            currentFileContent = PromptLoader.Load(fileName);
            isModified = false;
        }
        
        private void SaveCurrentFile()
        {
            if (string.IsNullOrEmpty(selectedPromptFile)) return;
            
            try
            {
                string configPromptsPath = Path.Combine(GenFilePaths.ConfigFolderPath, "TheSecondSeat", "Prompts");
                string activeLangFolder = LanguageDatabase.activeLanguage.folderName;
                string targetFolder = Path.Combine(configPromptsPath, activeLangFolder);
                
                if (!Directory.Exists(targetFolder))
                {
                    Directory.CreateDirectory(targetFolder);
                }
                
                string filePath = Path.Combine(targetFolder, selectedPromptFile + ".txt");
                File.WriteAllText(filePath, currentFileContent);
                
                isModified = false;
                PromptLoader.ClearCache();
                Messages.Message("保存成功", MessageTypeDefOf.PositiveEvent, false);
            }
            catch (Exception ex)
            {
                Log.Error($"[The Second Seat] Failed to save prompt file: {ex.Message}");
                Messages.Message("保存失败: " + ex.Message, MessageTypeDefOf.RejectInput, false);
            }
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 35f), "提示词管理");
            Text.Font = GameFont.Small;
            
            float topMargin = 45f;
            float bottomMargin = 50f;
            float leftWidth = 250f;
            float gap = 10f;
            float rightWidth = inRect.width - leftWidth - gap;
            float height = inRect.height - topMargin - bottomMargin;
            
            // Left Panel: File List
            Rect leftRect = new Rect(inRect.x, inRect.y + topMargin, leftWidth, height);
            Widgets.DrawMenuSection(leftRect);
            
            Rect viewRectLeft = new Rect(0, 0, leftWidth - 16f, promptFiles.Count * 30f);
            Widgets.BeginScrollView(leftRect, ref scrollPositionLeft, viewRectLeft);
            
            float y = 0f;
            foreach (string file in promptFiles)
            {
                Rect rowRect = new Rect(0, y, viewRectLeft.width, 30f);
                
                // Highlight selected
                if (selectedPromptFile == file)
                {
                    Widgets.DrawHighlightSelected(rowRect);
                }
                
                if (Widgets.ButtonText(rowRect, file, true, false, selectedPromptFile == file))
                {
                    LoadFile(file);
                }
                y += 30f;
            }
            
            Widgets.EndScrollView();
            
            // Right Panel: Editor
            Rect rightRect = new Rect(inRect.x + leftWidth + gap, inRect.y + topMargin, rightWidth, height);
            
            if (!string.IsNullOrEmpty(selectedPromptFile))
            {
                string newContent = Widgets.TextArea(rightRect, currentFileContent, false);
                if (newContent != currentFileContent)
                {
                    currentFileContent = newContent;
                    isModified = true;
                }
            }
            else
            {
                Widgets.Label(rightRect, "请从左侧选择一个提示词文件进行编辑。\n\n如果没有文件，请先在设置中点击“初始化提示词”。");
            }
            
            // Bottom Buttons
            float btnWidth = 120f;
            float btnHeight = 40f;
            float btnY = inRect.height - btnHeight;
            
            if (Widgets.ButtonText(new Rect(inRect.width - btnWidth, btnY, btnWidth, btnHeight), "关闭"))
            {
                Close();
            }
            
            if (isModified)
            {
                GUI.color = Color.green;
            }
            if (Widgets.ButtonText(new Rect(inRect.width - btnWidth * 2 - 10f, btnY, btnWidth, btnHeight), "保存"))
            {
                SaveCurrentFile();
            }
            GUI.color = Color.white;
            
            if (Widgets.ButtonText(new Rect(inRect.x, btnY, btnWidth, btnHeight), "打开文件夹"))
            {
                PromptLoader.OpenConfigFolder();
            }
            
            if (Widgets.ButtonText(new Rect(inRect.x + btnWidth + 10f, btnY, btnWidth, btnHeight), "刷新列表"))
            {
                RefreshFileList();
            }
        }
        
        public override void Close(bool doCloseSound = true)
        {
            if (isModified)
            {
                Find.WindowStack.Add(new Dialog_MessageBox(
                    "当前文件已修改，是否保存？",
                    "保存",
                    () => {
                        SaveCurrentFile();
                        base.Close(doCloseSound);
                    },
                    "不保存",
                    () => base.Close(doCloseSound),
                    null,
                    false,
                    null,
                    null
                ));
            }
            else
            {
                base.Close(doCloseSound);
            }
        }
    }
}
