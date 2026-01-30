using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using TheSecondSeat.PersonaGeneration;
using TheSecondSeat.PersonaGeneration.Presets;
using UnityEngine;
using Verse;

namespace TheSecondSeat.UI
{
    /// <summary>
    /// ⭐ v3.1: 现代化提示词预设管理窗口
    /// - 紧凑三栏布局
    /// - 预设删除功能（带确认）
    /// - 改进的视觉反馈
    /// </summary>
    public class PromptPresetsWindow : Window
    {
        // === UI Constants ===
        private const float HEADER_HEIGHT = 32f;
        private const float ROW_HEIGHT = 26f;
        private const float BUTTON_HEIGHT = 24f;
        private const float SMALL_BUTTON_WIDTH = 24f;
        private const float PADDING = 4f;
        private const float COLUMN_GAP = 8f;

        // === Colors ===
        private static readonly Color BgDark = new Color(0.12f, 0.12f, 0.14f, 0.95f);
        private static readonly Color BgPanel = new Color(0.16f, 0.16f, 0.18f, 0.9f);
        private static readonly Color AccentBlue = new Color(0.3f, 0.5f, 0.8f, 1f);
        private static readonly Color AccentGreen = new Color(0.3f, 0.7f, 0.4f, 1f);
        private static readonly Color AccentRed = new Color(0.8f, 0.3f, 0.3f, 1f);
        private static readonly Color TextMuted = new Color(0.6f, 0.6f, 0.6f, 1f);

        // === State ===
        private Vector2 _presetScrollPos;
        private Vector2 _entryScrollPos;
        private Vector2 _contentScrollPos;
        private string _selectedPresetId;
        private string _selectedEntryId;
        private string _editingContent;

        public override Vector2 InitialSize => new Vector2(960f, 640f);

        public PromptPresetsWindow()
        {
            doCloseX = true;
            forcePause = true;
            resizeable = true;
            draggable = true;
            closeOnClickedOutside = false;

            // Initialize with active preset
            var active = PromptPresetManager.GetActivePreset();
            if (active != null)
            {
                SelectPreset(active);
            }
        }

        private void SelectPreset(PromptPreset preset)
        {
            _selectedPresetId = preset.Id;
            _selectedEntryId = preset.Entries.FirstOrDefault()?.Id;
            UpdateEditingContent();
        }

        private void SelectEntry(PromptEntry entry)
        {
            _selectedEntryId = entry.Id;
            UpdateEditingContent();
        }

        private void UpdateEditingContent()
        {
            var preset = PromptPresetManager.Presets.FirstOrDefault(p => p.Id == _selectedPresetId);
            var entry = preset?.Entries.FirstOrDefault(e => e.Id == _selectedEntryId);
            _editingContent = entry?.Content ?? "";
        }

        private void SaveCurrentEntryContent()
        {
            var preset = PromptPresetManager.Presets.FirstOrDefault(p => p.Id == _selectedPresetId);
            var entry = preset?.Entries.FirstOrDefault(e => e.Id == _selectedEntryId);
            if (entry != null && entry.Content != _editingContent)
            {
                entry.Content = _editingContent;
                PromptPresetManager.SavePresets();
            }
        }

        public override void DoWindowContents(Rect inRect)
        {
            // Background
            Widgets.DrawBoxSolid(inRect, BgDark);

            // Header
            DrawHeader(new Rect(0, 0, inRect.width, HEADER_HEIGHT));

            // Main content area
            float topY = HEADER_HEIGHT + PADDING;
            Rect mainRect = new Rect(0, topY, inRect.width, inRect.height - topY - PADDING);

            // Three columns: Presets (180) | Entries (220) | Editor (flex)
            float col1Width = 180f;
            float col2Width = 220f;
            float col3Width = mainRect.width - col1Width - col2Width - (COLUMN_GAP * 2);

            Rect rectPresets = new Rect(0, mainRect.y, col1Width, mainRect.height);
            Rect rectEntries = new Rect(col1Width + COLUMN_GAP, mainRect.y, col2Width, mainRect.height);
            Rect rectEditor = new Rect(col1Width + col2Width + COLUMN_GAP * 2, mainRect.y, col3Width, mainRect.height);

            DrawPresetsPanel(rectPresets);
            DrawEntriesPanel(rectEntries);
            DrawEditorPanel(rectEditor);
        }

        private void DrawHeader(Rect rect)
        {
            Widgets.DrawBoxSolid(rect, BgPanel);

            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(PADDING * 2, 0, rect.width * 0.5f, rect.height), "TSS_PromptPresetsManager_Title".Translate());
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;

            // Quick stats on the right
            var activePreset = PromptPresetManager.GetActivePreset();
            if (activePreset != null)
            {
                GUI.color = TextMuted;
                Text.Anchor = TextAnchor.MiddleRight;
                Widgets.Label(new Rect(rect.width - 250f, 0, 240f, rect.height),
                    $"Active: {activePreset.Name} ({activePreset.Entries.Count(e => e.Enabled)} entries)");
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
            }
        }

        private void DrawPresetsPanel(Rect rect)
        {
            Widgets.DrawBoxSolid(rect, BgPanel);

            Rect innerRect = rect.ContractedBy(PADDING);

            // Panel Header with buttons
            Rect headerRow = new Rect(innerRect.x, innerRect.y, innerRect.width, BUTTON_HEIGHT);

            // Title
            Text.Font = GameFont.Tiny;
            GUI.color = TextMuted;
            Widgets.Label(new Rect(headerRow.x, headerRow.y, 60f, headerRow.height), "PRESETS");
            GUI.color = Color.white;
            Text.Font = GameFont.Small;

            // Buttons: + | Copy | Del
            float btnX = headerRow.xMax - (SMALL_BUTTON_WIDTH * 3 + PADDING * 2);

            // Add button
            GUI.color = AccentGreen;
            if (Widgets.ButtonText(new Rect(btnX, headerRow.y, SMALL_BUTTON_WIDTH, BUTTON_HEIGHT), "+"))
            {
                var newPreset = new PromptPreset("New Preset");
                PromptPresetManager.AddPreset(newPreset);
                SelectPreset(newPreset);
            }
            GUI.color = Color.white;
            TooltipHandler.TipRegion(new Rect(btnX, headerRow.y, SMALL_BUTTON_WIDTH, BUTTON_HEIGHT), "TSS_Tooltip_NewPreset".Translate());

            btnX += SMALL_BUTTON_WIDTH + PADDING;

            // Copy button
            GUI.color = AccentBlue;
            if (Widgets.ButtonText(new Rect(btnX, headerRow.y, SMALL_BUTTON_WIDTH, BUTTON_HEIGHT), "⎘"))
            {
                if (!string.IsNullOrEmpty(_selectedPresetId))
                {
                    var clone = PromptPresetManager.DuplicatePreset(_selectedPresetId);
                    if (clone != null) SelectPreset(clone);
                }
            }
            GUI.color = Color.white;
            TooltipHandler.TipRegion(new Rect(btnX, headerRow.y, SMALL_BUTTON_WIDTH, BUTTON_HEIGHT), "TSS_Tooltip_CopyPreset".Translate());

            btnX += SMALL_BUTTON_WIDTH + PADDING;

            // Delete button
            GUI.color = AccentRed;
            if (Widgets.ButtonText(new Rect(btnX, headerRow.y, SMALL_BUTTON_WIDTH, BUTTON_HEIGHT), "✕"))
            {
                TryDeleteSelectedPreset();
            }
            GUI.color = Color.white;
            TooltipHandler.TipRegion(new Rect(btnX, headerRow.y, SMALL_BUTTON_WIDTH, BUTTON_HEIGHT), "TSS_Tooltip_DeletePreset".Translate());

            // Preset List
            Rect listRect = new Rect(innerRect.x, headerRow.yMax + PADDING, innerRect.width, innerRect.height - headerRow.height - PADDING * 2);
            Rect viewRect = new Rect(0, 0, listRect.width - 16f, PromptPresetManager.Presets.Count * ROW_HEIGHT);

            Widgets.BeginScrollView(listRect, ref _presetScrollPos, viewRect);
            float y = 0f;
            foreach (var preset in PromptPresetManager.Presets)
            {
                Rect rowRect = new Rect(0, y, viewRect.width, ROW_HEIGHT - 2f);
                bool isSelected = _selectedPresetId == preset.Id;

                // Selection highlight
                if (isSelected)
                {
                    Widgets.DrawBoxSolid(rowRect, AccentBlue * 0.3f);
                }
                else if (Mouse.IsOver(rowRect))
                {
                    Widgets.DrawBoxSolid(rowRect, new Color(1f, 1f, 1f, 0.05f));
                }

                // Active indicator
                if (preset.IsActive)
                {
                    GUI.color = AccentGreen;
                    Widgets.Label(new Rect(rowRect.x + 2f, rowRect.y, 12f, rowRect.height), "●");
                    GUI.color = Color.white;
                }

                // Name
                Text.Anchor = TextAnchor.MiddleLeft;
                string displayName = preset.Name;
                if (displayName.Length > 18) displayName = displayName.Substring(0, 15) + "...";
                Widgets.Label(new Rect(rowRect.x + 16f, rowRect.y, rowRect.width - 20f, rowRect.height), displayName);
                Text.Anchor = TextAnchor.UpperLeft;

                // Click to select
                if (Widgets.ButtonInvisible(rowRect))
                {
                    SelectPreset(preset);
                }

                y += ROW_HEIGHT;
            }
            Widgets.EndScrollView();
        }

        private void TryDeleteSelectedPreset()
        {
            if (string.IsNullOrEmpty(_selectedPresetId)) return;

            var preset = PromptPresetManager.Presets.FirstOrDefault(p => p.Id == _selectedPresetId);
            if (preset == null) return;

            // Prevent deleting the last preset
            if (PromptPresetManager.Presets.Count <= 1)
            {
                Messages.Message("TSS_Error_CannotDeleteLastPreset".Translate(), MessageTypeDefOf.RejectInput, false);
                return;
            }

            // Show confirmation dialog
            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                "TSS_Confirm_DeletePreset".Translate(preset.Name),
                () =>
                {
                    bool wasActive = preset.IsActive;
                    PromptPresetManager.RemovePreset(_selectedPresetId);

                    // Select another preset
                    var nextPreset = PromptPresetManager.Presets.FirstOrDefault();
                    if (nextPreset != null)
                    {
                        SelectPreset(nextPreset);
                        if (wasActive)
                        {
                            PromptPresetManager.SetActivePreset(nextPreset.Id);
                        }
                    }
                    else
                    {
                        _selectedPresetId = null;
                        _selectedEntryId = null;
                    }
                },
                destructive: true
            ));
        }

        private void DrawEntriesPanel(Rect rect)
        {
            var preset = PromptPresetManager.Presets.FirstOrDefault(p => p.Id == _selectedPresetId);

            Widgets.DrawBoxSolid(rect, BgPanel);

            if (preset == null)
            {
                GUI.color = TextMuted;
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(rect, "Select a preset");
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
                return;
            }

            Rect innerRect = rect.ContractedBy(PADDING);

            // Preset name (editable) + Activate button
            Rect nameRow = new Rect(innerRect.x, innerRect.y, innerRect.width, BUTTON_HEIGHT);

            preset.Name = Widgets.TextField(new Rect(nameRow.x, nameRow.y, nameRow.width - 70f, BUTTON_HEIGHT), preset.Name);

            GUI.color = preset.IsActive ? AccentGreen : Color.gray;
            if (Widgets.ButtonText(new Rect(nameRow.xMax - 65f, nameRow.y, 65f, BUTTON_HEIGHT),
                preset.IsActive ? "✓ Active" : "Activate"))
            {
                PromptPresetManager.SetActivePreset(preset.Id);
            }
            GUI.color = Color.white;

            // Entry list header
            Rect entryHeader = new Rect(innerRect.x, nameRow.yMax + PADDING, innerRect.width, BUTTON_HEIGHT);

            Text.Font = GameFont.Tiny;
            GUI.color = TextMuted;
            Widgets.Label(new Rect(entryHeader.x, entryHeader.y, 60f, entryHeader.height), "ENTRIES");
            GUI.color = Color.white;
            Text.Font = GameFont.Small;

            // Add Entry button
            GUI.color = AccentGreen;
            if (Widgets.ButtonText(new Rect(entryHeader.xMax - 24f, entryHeader.y, 24f, BUTTON_HEIGHT), "+"))
            {
                var newEntry = new PromptEntry("New Entry", "");
                preset.Entries.Add(newEntry);
                SelectEntry(newEntry);
                PromptPresetManager.SavePresets();
            }
            GUI.color = Color.white;

            // Entry list
            Rect listRect = new Rect(innerRect.x, entryHeader.yMax + PADDING, innerRect.width, innerRect.height - nameRow.height - entryHeader.height - PADDING * 3);
            Rect viewRect = new Rect(0, 0, listRect.width - 16f, preset.Entries.Count * ROW_HEIGHT);

            Widgets.BeginScrollView(listRect, ref _entryScrollPos, viewRect);
            float y = 0f;
            for (int i = 0; i < preset.Entries.Count; i++)
            {
                var entry = preset.Entries[i];
                Rect rowRect = new Rect(0, y, viewRect.width, ROW_HEIGHT - 2f);
                bool isSelected = _selectedEntryId == entry.Id;

                // Selection highlight
                if (isSelected)
                {
                    Widgets.DrawBoxSolid(rowRect, AccentBlue * 0.3f);
                }
                else if (Mouse.IsOver(rowRect))
                {
                    Widgets.DrawBoxSolid(rowRect, new Color(1f, 1f, 1f, 0.05f));
                }

                // Enabled checkbox
                bool enabled = entry.Enabled;
                Widgets.Checkbox(new Vector2(rowRect.x + 2f, rowRect.y + 3f), ref enabled, 18f);
                if (enabled != entry.Enabled)
                {
                    entry.Enabled = enabled;
                    PromptPresetManager.SavePresets();
                }

                // Role indicator (colored dot)
                Color roleColor = GetRoleColor(entry.Role);
                GUI.color = roleColor;
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(new Rect(rowRect.x + 24f, rowRect.y, 16f, rowRect.height), "●");
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;

                // Name (click to select)
                Text.Anchor = TextAnchor.MiddleLeft;
                string entryName = entry.Name;
                if (entryName.Length > 14) entryName = entryName.Substring(0, 11) + "...";
                Rect labelRect = new Rect(rowRect.x + 42f, rowRect.y, rowRect.width - 110f, rowRect.height);
                Widgets.Label(labelRect, entryName);
                Text.Anchor = TextAnchor.UpperLeft;

                if (Widgets.ButtonInvisible(labelRect))
                {
                    SelectEntry(entry);
                }

                // Move Up/Down/Delete buttons (compact)
                float btnX = rowRect.xMax - 60f;

                if (i > 0)
                {
                    if (Widgets.ButtonText(new Rect(btnX, rowRect.y, 18f, ROW_HEIGHT - 2f), "↑"))
                    {
                        preset.Entries.RemoveAt(i);
                        preset.Entries.Insert(i - 1, entry);
                        PromptPresetManager.SavePresets();
                    }
                }
                btnX += 20f;

                if (i < preset.Entries.Count - 1)
                {
                    if (Widgets.ButtonText(new Rect(btnX, rowRect.y, 18f, ROW_HEIGHT - 2f), "↓"))
                    {
                        preset.Entries.RemoveAt(i);
                        preset.Entries.Insert(i + 1, entry);
                        PromptPresetManager.SavePresets();
                    }
                }
                btnX += 20f;

                GUI.color = AccentRed * 0.8f;
                if (Widgets.ButtonText(new Rect(btnX, rowRect.y, 18f, ROW_HEIGHT - 2f), "✕"))
                {
                    preset.Entries.RemoveAt(i);
                    PromptPresetManager.SavePresets();
                    if (_selectedEntryId == entry.Id)
                    {
                        _selectedEntryId = preset.Entries.FirstOrDefault()?.Id;
                        UpdateEditingContent();
                    }
                    break;
                }
                GUI.color = Color.white;

                y += ROW_HEIGHT;
            }
            Widgets.EndScrollView();
        }

        private Color GetRoleColor(PromptRole role)
        {
            return role switch
            {
                PromptRole.System => new Color(0.4f, 0.7f, 1f),      // Blue
                PromptRole.User => new Color(0.5f, 0.9f, 0.5f),      // Green
                PromptRole.Assistant => new Color(1f, 0.7f, 0.4f),   // Orange
                _ => Color.gray
            };
        }

        private void DrawEditorPanel(Rect rect)
        {
            var preset = PromptPresetManager.Presets.FirstOrDefault(p => p.Id == _selectedPresetId);
            var entry = preset?.Entries.FirstOrDefault(e => e.Id == _selectedEntryId);

            Widgets.DrawBoxSolid(rect, BgPanel);

            if (entry == null)
            {
                GUI.color = TextMuted;
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(rect, "Select an entry to edit");
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
                return;
            }

            Rect innerRect = rect.ContractedBy(PADDING);

            // Top row: Name + Role + Import
            Rect topRow = new Rect(innerRect.x, innerRect.y, innerRect.width, BUTTON_HEIGHT);

            // Entry Name
            Widgets.Label(new Rect(topRow.x, topRow.y, 50f, BUTTON_HEIGHT), "Name:");
            string newName = Widgets.TextField(new Rect(topRow.x + 55f, topRow.y, 150f, BUTTON_HEIGHT), entry.Name);
            if (newName != entry.Name)
            {
                entry.Name = newName;
                PromptPresetManager.SavePresets();
            }

            // Role dropdown
            Widgets.Label(new Rect(topRow.x + 215f, topRow.y, 40f, BUTTON_HEIGHT), "Role:");
            GUI.color = GetRoleColor(entry.Role);
            if (Widgets.ButtonText(new Rect(topRow.x + 260f, topRow.y, 80f, BUTTON_HEIGHT), entry.Role.ToString()))
            {
                List<FloatMenuOption> options = new List<FloatMenuOption>();
                foreach (PromptRole role in Enum.GetValues(typeof(PromptRole)))
                {
                    options.Add(new FloatMenuOption(role.ToString(), () =>
                    {
                        entry.Role = role;
                        PromptPresetManager.SavePresets();
                    }));
                }
                Find.WindowStack.Add(new FloatMenu(options));
            }
            GUI.color = Color.white;

            // Import button
            GUI.color = AccentBlue;
            if (Widgets.ButtonText(new Rect(topRow.xMax - 80f, topRow.y, 80f, BUTTON_HEIGHT), "📥 Import"))
            {
                ShowImportMenu();
            }
            GUI.color = Color.white;

            // Content Editor
            Rect editorRect = new Rect(innerRect.x, topRow.yMax + PADDING, innerRect.width, innerRect.height - topRow.height - PADDING * 2);

            // Background for editor
            Widgets.DrawBoxSolid(editorRect, new Color(0.08f, 0.08f, 0.1f, 1f));

            // Calculate scrollable area
            float contentHeight = Text.CalcHeight(_editingContent ?? "", editorRect.width - 20f) + 100f;
            if (contentHeight < editorRect.height) contentHeight = editorRect.height;

            Rect viewRect = new Rect(0, 0, editorRect.width - 16f, contentHeight);

            Widgets.BeginScrollView(editorRect, ref _contentScrollPos, viewRect);
            string newContent = Widgets.TextArea(viewRect, _editingContent ?? "");
            Widgets.EndScrollView();

            if (newContent != _editingContent)
            {
                _editingContent = newContent;
                SaveCurrentEntryContent();
            }

            // Character count at bottom
            GUI.color = TextMuted;
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.LowerRight;
            Widgets.Label(new Rect(editorRect.xMax - 100f, editorRect.yMax - 18f, 95f, 16f),
                $"{(_editingContent?.Length ?? 0)} chars");
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
            GUI.color = Color.white;
        }

        private void ShowImportMenu()
        {
            var options = new List<FloatMenuOption>();
            var promptNames = PromptLoader.GetAllPromptNames();

            foreach (var name in promptNames)
            {
                options.Add(new FloatMenuOption(name, () =>
                {
                    List<FloatMenuOption> subOptions = new List<FloatMenuOption>
                    {
                        new FloatMenuOption("{{ include }} reference", () =>
                        {
                            _editingContent += $"\n{{{{ include '{name}' }}}}";
                            SaveCurrentEntryContent();
                        }),
                        new FloatMenuOption("Copy content", () =>
                        {
                            string content = PromptLoader.Load(name, silent: true);
                            if (!string.IsNullOrEmpty(content))
                            {
                                if (_editingContent.Length > 0) _editingContent += "\n\n";
                                _editingContent += content;
                                SaveCurrentEntryContent();
                            }
                        })
                    };
                    Find.WindowStack.Add(new FloatMenu(subOptions));
                }));
            }

            if (options.Count == 0)
            {
                options.Add(new FloatMenuOption("No prompt files found", null));
            }

            Find.WindowStack.Add(new FloatMenu(options));
        }

        public override void PreClose()
        {
            base.PreClose();
            SaveCurrentEntryContent();
            PromptPresetManager.SavePresets();
        }
    }
}