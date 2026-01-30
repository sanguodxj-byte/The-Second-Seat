using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using RimWorld;
using TheSecondSeat.PersonaGeneration;
using TheSecondSeat.Settings;
using TheSecondSeat.SmartPrompt;

namespace TheSecondSeat.UI
{
    [StaticConstructorOnStartup]
    public class PromptManagementWindow : Window
    {
        private List<string> _promptFiles;
        private string _selectedPromptId;
        private string _currentEditBuffer;
        private Vector2 _scrollPositionLeft;
        private Vector2 _scrollPositionRight;
        private bool _isDirty;
        
        // ⭐ v3.0: 支持 Persona 专属 Prompt
        private string _personaName;
        
        // ⭐ v3.2.0: 缓存提示词描述
        private Dictionary<string, string> _promptDescriptions = new Dictionary<string, string>();

        // UI Constants
        private const float LeftPanelWidth = 320f;  // ⭐ v3.2.0: 增加宽度以显示描述
        private const float ToolbarHeight = 30f;
        private const float WindowMargin = 10f;

        public override Vector2 InitialSize => new Vector2(1000f, 700f);

        public PromptManagementWindow(string personaName = null)
        {
            this.doCloseX = true;
            this.forcePause = true;
            this.absorbInputAroundWindow = true;
            this.resizeable = true;
            this.draggable = true;
            
            this._personaName = personaName;

            // ⭐ v2.9.6: 延迟初始化，避免在窗口构造时就访问可能未初始化的资源
            _promptFiles = new List<string>();
            _selectedPromptId = null;
            _currentEditBuffer = null;
        }
        
        public override void PreOpen()
        {
            base.PreOpen();
            RefreshFileList();
        }

        private void RefreshFileList()
        {
            _promptFiles = PromptLoader.GetAllPromptNames(_personaName).OrderBy(x => x).ToList();
            
            // ⭐ v3.2.0: 刷新描述缓存
            RefreshDescriptions();
            
            // Auto-select first if nothing selected
            if (string.IsNullOrEmpty(_selectedPromptId) && _promptFiles.Count > 0)
            {
                SelectPrompt(_promptFiles[0]);
            }
            // If selected file no longer exists
            else if (!string.IsNullOrEmpty(_selectedPromptId) && !_promptFiles.Contains(_selectedPromptId))
            {
                _selectedPromptId = null;
                _currentEditBuffer = null;
                _isDirty = false;
            }
        }
        
        /// <summary>
        /// ⭐ v3.2.0: 刷新描述缓存
        /// </summary>
        private void RefreshDescriptions()
        {
            _promptDescriptions.Clear();
            
            foreach (var promptId in _promptFiles)
            {
                // 尝试从 PromptModuleDef 获取描述
                var def = DefDatabase<PromptModuleDef>.GetNamedSilentFail(promptId);
                if (def != null)
                {
                    string desc = def.GetDisplayDescription();
                    if (!string.IsNullOrEmpty(desc))
                    {
                        _promptDescriptions[promptId] = desc;
                    }
                }
            }
        }

        private void SelectPrompt(string promptId)
        {
            if (_isDirty)
            {
                // Simple confirmation could be added here, but for now we just warn visually
                // or we could auto-save? Let's just discard for now to be safe from accidental overwrites.
            }

            _selectedPromptId = promptId;
            // Load content
            // Note: PromptLoader.Load returns empty string if disabled, but we want to see content even if disabled to edit it.
            // We might need a way to bypass the "IsDisabled" check in PromptLoader.Load.
            // Looking at PromptLoader.Load:
            // if (IsDisabled(promptName)) return "";
            // We can temporarily enable it to load, or we need a raw load method.
            // Since PromptLoader.Load is designed for runtime usage, we should probably read the file directly or use a workaround.
            // Actually, PromptLoader has no "LoadRaw" method publicly exposed.
            // However, we can check IsDisabled, temporarily enable, load, then disable back.
            
            bool wasDisabled = PromptLoader.IsDisabled(promptId);
            if (wasDisabled)
            {
                TheSecondSeatMod.Settings.disabledPrompts.Remove(promptId);
            }

            _currentEditBuffer = PromptLoader.Load(promptId, _personaName, silent: true);

            if (wasDisabled)
            {
                TheSecondSeatMod.Settings.disabledPrompts.Add(promptId);
            }

            _isDirty = false;
            _scrollPositionRight = Vector2.zero;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            string title = "TSS_PromptManager_Title".Translate();
            if (!string.IsNullOrEmpty(_personaName))
            {
                title += $" ({_personaName})";
            }
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 30f), title);
            Text.Font = GameFont.Small;

            Rect contentRect = new Rect(inRect.x, inRect.y + 40f, inRect.width, inRect.height - 80f); // Reserve bottom for buttons

            // Split into Left (List) and Right (Editor)
            Rect leftRect = new Rect(contentRect.x, contentRect.y, LeftPanelWidth, contentRect.height);
            Rect rightRect = new Rect(contentRect.x + LeftPanelWidth + WindowMargin, contentRect.y, contentRect.width - LeftPanelWidth - WindowMargin, contentRect.height);

            // Draw Backgrounds
            Widgets.DrawMenuSection(leftRect);
            Widgets.DrawMenuSection(rightRect);

            // --- Left Panel: Prompt List ---
            DrawLeftPanel(leftRect);

            // --- Right Panel: Editor ---
            DrawRightPanel(rightRect);

            // --- Bottom Buttons ---
            DrawBottomButtons(inRect);
        }

        private void DrawLeftPanel(Rect rect)
        {
            // ⭐ v3.2.0: 增加行高以容纳描述
            float rowHeight = 40f;
            Rect viewRect = new Rect(0, 0, rect.width - 16f, _promptFiles.Count * rowHeight);
            Widgets.BeginScrollView(rect, ref _scrollPositionLeft, viewRect);

            float curY = 0f;
            foreach (var promptId in _promptFiles)
            {
                Rect rowRect = new Rect(0, curY, viewRect.width, rowHeight);
                
                // Highlight selected
                if (promptId == _selectedPromptId)
                {
                    Widgets.DrawHighlightSelected(rowRect);
                }
                else if (Mouse.IsOver(rowRect))
                {
                    Widgets.DrawHighlight(rowRect);
                }

                // Checkbox (Enable/Disable)
                bool isDisabled = TheSecondSeatMod.Settings.disabledPrompts.Contains(promptId);
                bool isEnabled = !isDisabled;
                bool newEnabled = isEnabled;
                
                Rect checkRect = new Rect(rowRect.x + 5f, rowRect.y + 8f, 24f, 24f);
                Widgets.Checkbox(checkRect.x, checkRect.y, ref newEnabled);

                if (newEnabled != isEnabled)
                {
                    if (newEnabled)
                        TheSecondSeatMod.Settings.disabledPrompts.Remove(promptId);
                    else
                        TheSecondSeatMod.Settings.disabledPrompts.Add(promptId);
                    
                    // ⭐ v3.1.0: 通知 SmartPrompt 系统重建，使禁用/启用立即生效
                    SmartPromptInitializer.RebuildSystem();
                }

                // Label Area (Click to select)
                Rect labelRect = new Rect(rowRect.x + 35f, rowRect.y, rowRect.width - 35f, rowRect.height);
                
                if (Widgets.ButtonInvisible(labelRect))
                {
                    SelectPrompt(promptId);
                }
                
                // ⭐ v3.2.0: 绘制文件名（第一行）
                Color labelColor = isDisabled ? Color.gray : Color.white;
                GUI.color = labelColor;
                Text.Anchor = TextAnchor.UpperLeft;
                Text.Font = GameFont.Small;
                Rect nameRect = new Rect(labelRect.x, labelRect.y + 2f, labelRect.width, 18f);
                Widgets.Label(nameRect, promptId);
                
                // ⭐ v3.2.0: 绘制描述（第二行，灰色小字）
                if (_promptDescriptions.TryGetValue(promptId, out string description))
                {
                    GUI.color = new Color(0.7f, 0.7f, 0.7f);
                    Text.Font = GameFont.Tiny;
                    Rect descRect = new Rect(labelRect.x, labelRect.y + 20f, labelRect.width, 16f);
                    string truncatedDesc = description.Length > 35 ? description.Substring(0, 32) + "..." : description;
                    Widgets.Label(descRect, truncatedDesc);
                }
                
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;

                curY += rowHeight;
            }

            Widgets.EndScrollView();
        }

        private void DrawRightPanel(Rect rect)
        {
            if (string.IsNullOrEmpty(_selectedPromptId))
            {
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(rect, "Select a prompt to edit");
                Text.Anchor = TextAnchor.UpperLeft;
                return;
            }

            // Inner padding
            Rect innerRect = rect.ContractedBy(10f);

            // --- Toolbar ---
            float toolbarY = innerRect.y;
            
            // Filename Title
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(innerRect.x, toolbarY, 300f, 30f), _selectedPromptId);
            Text.Font = GameFont.Small;

            // Save Button
            if (Widgets.ButtonText(new Rect(innerRect.xMax - 220f, toolbarY, 100f, 30f), "TSS_Prompt_Save".Translate()))
            {
                SaveCurrentPrompt();
            }

            // Reset Button
            if (Widgets.ButtonText(new Rect(innerRect.xMax - 110f, toolbarY, 100f, 30f), "TSS_Prompt_Reset".Translate()))
            {
                ResetCurrentPrompt();
            }

            toolbarY += 35f;

            // Status Line
            string overrideStatus = GetOverrideStatus();
            string statusText = overrideStatus;
            if (_isDirty) statusText += " (" + "TSS_Prompt_Unsaved".Translate() + ")";
            
            bool isOverride = overrideStatus != "TSS_Prompt_Status_Default".Translate();
            GUI.color = isOverride ? new Color(1f, 0.8f, 0.4f) : Color.gray;
            Widgets.Label(new Rect(innerRect.x, toolbarY, innerRect.width, 20f), statusText);
            GUI.color = Color.white;

            toolbarY += 25f;

            // --- Text Editor ---
            Rect editorRect = new Rect(innerRect.x, toolbarY, innerRect.width, innerRect.height - (toolbarY - innerRect.y));
            
            string newText = _currentEditBuffer ?? "";
            // ⭐ v2.9.5: TextAreaScrollable 在 RimWorld 1.6 中不可用
            // 使用 Widgets.TextArea + 滚动视图替代
            
            // 计算文本高度
            float textHeight = Math.Max(Text.CalcHeight(newText, editorRect.width - 20f), editorRect.height);
            Rect viewRect = new Rect(0, 0, editorRect.width - 16f, textHeight + 20f);
            
            Widgets.BeginScrollView(editorRect, ref _scrollPositionRight, viewRect);
            
            Rect textAreaRect = new Rect(0, 0, viewRect.width, textHeight);
            newText = Widgets.TextArea(textAreaRect, newText);
            
            Widgets.EndScrollView();

            if (newText != _currentEditBuffer)
            {
                _currentEditBuffer = newText;
                _isDirty = true;
            }
        }

        private void DrawBottomButtons(Rect inRect)
        {
            float buttonWidth = 140f;
            float buttonHeight = 35f;
            float y = inRect.height - buttonHeight;
            float x = inRect.x;

            if (Widgets.ButtonText(new Rect(x, y, buttonWidth, buttonHeight), "TSS_Prompt_OpenFolder".Translate()))
            {
                PromptLoader.OpenConfigFolder(_personaName);
            }

            x += buttonWidth + 10f;

            if (Widgets.ButtonText(new Rect(x, y, buttonWidth, buttonHeight), "TSS_Prompt_Reload".Translate()))
            {
                PromptLoader.ClearCache();
                RefreshFileList();
                if (!string.IsNullOrEmpty(_selectedPromptId))
                {
                    SelectPrompt(_selectedPromptId);
                }
                
                // ⭐ v3.1.0: 同时重建 SmartPrompt 系统
                SmartPrompt.SmartPromptInitializer.RebuildSystem();
                Messages.Message("TSS_Prompt_Reloaded".Translate(), MessageTypeDefOf.PositiveEvent, false);
            }
            
            x += buttonWidth + 10f;

            // Initialize Button (Enhanced: Support Global and Persona selection)
            if (Widgets.ButtonText(new Rect(x, y, buttonWidth, buttonHeight), "TSS_Prompt_Initialize".Translate()))
            {
                var options = new List<FloatMenuOption>();
                
                // Option 1: Initialize Current/Global
                string label = string.IsNullOrEmpty(_personaName) ? "Global" : _personaName;
                options.Add(new FloatMenuOption($"Initialize {label}", () =>
                {
                    Action initAction = () =>
                    {
                        PromptLoader.InitializeUserPrompts(_personaName);
                        PromptLoader.ClearCache();
                        RefreshFileList();
                        Messages.Message("TSS_Prompt_InitializedFor".Translate(label), MessageTypeDefOf.PositiveEvent, false);
                    };
                    Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("TSS_Prompt_InitializeConfirm".Translate(label), initAction, destructive: true));
                }));

                // Option 2: Initialize Specific Persona (if in Global mode)
                if (string.IsNullOrEmpty(_personaName))
                {
                    foreach (var persona in DefDatabase<NarratorPersonaDef>.AllDefs)
                    {
                        options.Add(new FloatMenuOption($"Initialize {persona.narratorName} ({persona.defName})", () =>
                        {
                            Action initAction = () =>
                            {
                                PromptLoader.InitializeUserPrompts(persona.defName);
                                PromptLoader.ClearCache();
                                Messages.Message("TSS_Prompt_InitializedFor".Translate(persona.narratorName), MessageTypeDefOf.PositiveEvent, false);
                            };
                            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("TSS_Prompt_InitializeConfirm".Translate(persona.narratorName), initAction, destructive: true));
                        }));
                    }
                }

                Find.WindowStack.Add(new FloatMenu(options));
            }

            // Close button at far right
            if (Widgets.ButtonText(new Rect(inRect.xMax - buttonWidth, y, buttonWidth, buttonHeight), "CloseButton".Translate()))
            {
                Close();
            }
        }

        private string GetOverrideStatus()
        {
            if (string.IsNullOrEmpty(_selectedPromptId)) return "";
            
            string configRoot = GenFilePaths.ConfigFolderPath;
            string fileName = _selectedPromptId + ".txt";
            string activeLangFolder = LanguageDatabase.activeLanguage.folderName;
            
            // Check Persona Specific
            if (!string.IsNullOrEmpty(_personaName))
            {
                 // Check Persona Lang
                string personaLangPath = System.IO.Path.Combine(configRoot, "TheSecondSeat", "Prompts", _personaName, activeLangFolder, fileName);
                if (System.IO.File.Exists(personaLangPath)) return "TSS_Prompt_Override_Persona_Lang".Translate();

                // Check Persona Global
                string personaGlobalPath = System.IO.Path.Combine(configRoot, "TheSecondSeat", "Prompts", _personaName, fileName);
                if (System.IO.File.Exists(personaGlobalPath)) return "TSS_Prompt_Override_Persona_Global".Translate();
            }

            // Check Lang specific
            string userLangPath = System.IO.Path.Combine(configRoot, "TheSecondSeat", "Prompts", activeLangFolder, fileName);
            if (System.IO.File.Exists(userLangPath)) return "TSS_Prompt_Override_User_Lang".Translate();

            // Check Global
            string userGlobalPath = System.IO.Path.Combine(configRoot, "TheSecondSeat", "Prompts", fileName);
            if (System.IO.File.Exists(userGlobalPath)) return "TSS_Prompt_Override_User_Global".Translate();

            return "TSS_Prompt_Status_Default".Translate();
        }

        private void SaveCurrentPrompt()
        {
            if (string.IsNullOrEmpty(_selectedPromptId)) return;
            PromptLoader.SaveUserOverride(_selectedPromptId, _currentEditBuffer, _personaName);
            _isDirty = false;
            Messages.Message("Saved " + _selectedPromptId, MessageTypeDefOf.PositiveEvent, false);
        }

        private void ResetCurrentPrompt()
        {
            if (string.IsNullOrEmpty(_selectedPromptId)) return;
            
            Action resetAction = () =>
            {
                PromptLoader.DeleteUserOverride(_selectedPromptId, _personaName);
                PromptLoader.ClearCache(); // Ensure we load from default
                SelectPrompt(_selectedPromptId); // Reload
                Messages.Message("Reset " + _selectedPromptId, MessageTypeDefOf.PositiveEvent, false);
            };

            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("Confirm Reset?", resetAction, destructive: true));
        }
    }
}
