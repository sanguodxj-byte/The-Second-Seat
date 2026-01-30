using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Verse;
using Newtonsoft.Json;

namespace TheSecondSeat.PersonaGeneration.Presets
{
    public static class PromptPresetManager
    {
        private static List<PromptPreset> presets = new List<PromptPreset>();
        public static List<PromptPreset> Presets => presets;

        private static string ConfigPath => Path.Combine(GenFilePaths.ConfigFolderPath, "TheSecondSeat_Presets");
        private static string FilePath => Path.Combine(ConfigPath, "Presets.json");

        static PromptPresetManager()
        {
            if (!Directory.Exists(ConfigPath))
            {
                Directory.CreateDirectory(ConfigPath);
            }
        }

        public static void Initialize()
        {
            LoadPresets();
            if (presets.Count == 0)
            {
                CreateDefaultPreset();
            }
            
            // ⭐ v3.0: Automatically create presets for detected TSS sub-mods
            CheckAndCreateSubModPresets();
        }

        /// <summary>
        /// Scans for NarratorPersonaDefs from mods with "TSS" or "The Second Seat" in their package ID,
        /// and creates a default preset for each if one doesn't exist.
        /// </summary>
        private static void CheckAndCreateSubModPresets()
        {
            try
            {
                // Ensure DefDatabase is available
                if (DefDatabase<NarratorPersonaDef>.DefCount == 0) return;

                foreach (var def in DefDatabase<NarratorPersonaDef>.AllDefs)
                {
                    if (def.modContentPack == null) continue;

                    string pkgId = def.modContentPack.PackageId.ToLower();
                    // Filter for TSS related mods
                    if (pkgId.Contains("tss") || pkgId.Contains("thesecondseat"))
                    {
                        // Avoid duplicates
                        string presetName = $"{def.narratorName} Default";
                        if (presets.Any(p => p.Name == presetName)) continue;

                        CreatePresetForPersona(def, presetName);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[The Second Seat] Error checking sub-mod presets: {ex.Message}");
            }
        }

        private static void CreatePresetForPersona(NarratorPersonaDef def, string presetName)
        {
            try
            {
                var preset = new PromptPreset(presetName)
                {
                    Description = $"Auto-generated default preset for {def.narratorName} (from {def.modContentPack.Name}).",
                    IsActive = false
                };

                // Helper to load content directly from the mod's directory
                string LoadModContent(string filename)
                {
                    string root = def.modContentPack.RootDir;
                    string activeLang = LanguageDatabase.activeLanguage?.folderName ?? "English";
                    
                    // 1. Try Active Language
                    string path = Path.Combine(root, "Languages", activeLang, "Prompts", filename + ".txt");
                    if (File.Exists(path)) return File.ReadAllText(path);

                    // 2. Try ChineseSimplified (Common fallback for TSS)
                    if (activeLang != "ChineseSimplified")
                    {
                        path = Path.Combine(root, "Languages", "ChineseSimplified", "Prompts", filename + ".txt");
                        if (File.Exists(path)) return File.ReadAllText(path);
                    }

                    // 3. Try English
                    if (activeLang != "English")
                    {
                        path = Path.Combine(root, "Languages", "English", "Prompts", filename + ".txt");
                        if (File.Exists(path)) return File.ReadAllText(path);
                    }

                    return null;
                }

                // 1. Identity (Load Identity_Core)
                string identity = LoadModContent("Identity_Core");
                if (string.IsNullOrEmpty(identity))
                {
                    // Fallback: Use standard snippets
                    identity = "{{ snippets.identity_section }}\n\n{{ snippets.personality_section }}";
                }
                preset.Entries.Add(new PromptEntry("Identity", identity) { Role = PromptRole.System });

                // 2. Character Card (Context)
                // Using PascalCase for property access to match C# class structure
                preset.Entries.Add(new PromptEntry("Character Card",
                    "=== CHARACTER STATE CARD ===\n" +
                    "[Identity]\n" +
                    "Name: {{ card.Name }}\n" +
                    "Role: {{ card.Role }}\n" +
                    "Personality: {{ card.Identity.PersonalityType }}\n" +
                    "\n" +
                    "[BioRhythm]\n" +
                    "Energy: {{ card.Bio.EnergyLevel }}\n" +
                    "Time: {{ card.Bio.TimeOfDay }} ({{ get_season() }})\n" +
                    "\n" +
                    "[Psycho-Social]\n" +
                    "Affinity: {{ card.Mind.AffinityValue }} ({{ card.Mind.AffinityTier }})\n" +
                    "Mood: {{ card.Mind.CurrentEmotion }}\n" +
                    "\n" +
                    "[Visual]\n" +
                    "Expression: {{ card.Appearance.Consistency.CurrentExpression }}\n" +
                    "\n" +
                    "[Descent Status]\n" +
                    "Form: {{ card.Descent.CurrentForm }}\n" +
                    "{{ if card.Descent.IsDescentActive }}\n[⚠️ PHYSICAL MANIFESTATION ACTIVE]\nYou are currently present in the world as a physical entity.\n{{ end }}" +
                    "\n" +
                    "[Environment]\n" +
                    "Weather: {{ get_weather() }}\n" +
                    "Colony: {{ get_colonist_count() }} colonists\n" +
                    "{{ if is_under_attack() }}\n⚠️ THREAT: The colony is UNDER ATTACK!\n{{ end }}")
                {
                    Role = PromptRole.System
                });

                // 3. Knowledge (SmartPrompt)
                preset.Entries.Add(new PromptEntry("Knowledge", "{{ load_smart_modules(user_input) }}") { Role = PromptRole.System });

                // 4. Philosophy (Based on DifficultyMode)
                string phiFilename = $"Philosophy_{def.difficultyMode}";
                string philosophy = LoadModContent(phiFilename);
                if (string.IsNullOrEmpty(philosophy))
                {
                    // Fallback
                    philosophy = "{{ snippets.philosophy }}";
                }
                preset.Entries.Add(new PromptEntry("Philosophy", philosophy) { Role = PromptRole.System });

                // 5. Output Format (Force JSON)
                preset.Entries.Add(new PromptEntry("Output Format", "{{ include 'OutputFormat_JSON_Scriban' }}") { Role = PromptRole.System });

                presets.Add(preset);
                SavePresets();
                Log.Message($"[The Second Seat] Created default preset for {def.narratorName}.");
            }
            catch (Exception ex)
            {
                Log.Warning($"[The Second Seat] Failed to create preset for {def.narratorName}: {ex.Message}");
            }
        }

        public static void SavePresets()
        {
            try
            {
                string json = JsonConvert.SerializeObject(presets, Formatting.Indented);
                File.WriteAllText(FilePath, json);
            }
            catch (Exception ex)
            {
                Log.Error($"[TheSecondSeat] Failed to save prompt presets: {ex.Message}");
            }
        }

        public static void LoadPresets()
        {
            if (!File.Exists(FilePath)) return;

            try
            {
                string json = File.ReadAllText(FilePath);
                presets = JsonConvert.DeserializeObject<List<PromptPreset>>(json);
                
                // Ensure non-null
                presets ??= new List<PromptPreset>();
            }
            catch (Exception ex)
            {
                Log.Error($"[TheSecondSeat] Failed to load prompt presets: {ex.Message}");
                presets = new List<PromptPreset>();
            }
        }
        
        public static PromptPreset GetActivePreset()
        {
            return presets.FirstOrDefault(p => p.IsActive) ?? presets.FirstOrDefault();
        }

        public static void SetActivePreset(string id)
        {
            foreach (var p in presets)
            {
                p.IsActive = (p.Id == id);
            }
            SavePresets();
        }

        public static void AddPreset(PromptPreset preset)
        {
            presets.Add(preset);
            SavePresets();
        }

        public static void RemovePreset(string id)
        {
            var preset = presets.FirstOrDefault(p => p.Id == id);
            if (preset != null)
            {
                presets.Remove(preset);
                SavePresets();
            }
        }

        public static PromptPreset DuplicatePreset(string id)
        {
            var source = presets.FirstOrDefault(p => p.Id == id);
            if (source == null) return null;
            
            var clone = source.Clone();
            presets.Add(clone);
            SavePresets();
            return clone;
        }

        private static void CreateDefaultPreset()
        {
            var defaultPreset = new PromptPreset("Default TSS Preset (JSON)")
            {
                IsActive = true,
                Description = "Standard system prompt configuration with JSON output format. Includes Identity, Context, and Rules."
            };

            // 1. Identity (Persona & Role)
            defaultPreset.Entries.Add(new PromptEntry("Identity",
                "{{ snippets.identity_section }}\n\n{{ snippets.personality_section }}")
            {
                Role = PromptRole.System,
                Enabled = true
            });

            // 2. Character Card (Context)
            // Using PascalCase to match NarratorStateCard C# properties
            defaultPreset.Entries.Add(new PromptEntry("Character Card",
                "=== CHARACTER STATE CARD ===\n" +
                "[Identity]\n" +
                "Name: {{ card.Name }}\n" +
                "Role: {{ card.Role }}\n" +
                "Personality: {{ card.Identity.PersonalityType }}\n" +
                "\n" +
                "[BioRhythm]\n" +
                "Energy: {{ card.Bio.EnergyLevel }}\n" +
                "Time: {{ card.Bio.TimeOfDay }} ({{ get_season() }})\n" +
                "\n" +
                "[Psycho-Social]\n" +
                "Affinity: {{ card.Mind.AffinityValue }} ({{ card.Mind.AffinityTier }})\n" +
                "Mood: {{ card.Mind.CurrentEmotion }}\n" +
                "\n" +
                "[Visual]\n" +
                "Expression: {{ card.Appearance.Consistency.CurrentExpression }}\n" +
                "\n" +
                "[Descent Status]\n" +
                "Form: {{ card.Descent.CurrentForm }}\n" +
                "{{ if card.Descent.IsDescentActive }}\n[⚠️ PHYSICAL MANIFESTATION ACTIVE]\nYou are currently present in the world as a physical entity.\n{{ end }}" +
                "\n" +
                "[Environment]\n" +
                "Weather: {{ get_weather() }}\n" +
                "Colony: {{ get_colonist_count() }} colonists\n" +
                "{{ if is_under_attack() }}\n⚠️ THREAT: The colony is UNDER ATTACK!\n{{ end }}")
            {
                Role = PromptRole.System,
                Enabled = true
            });

            // 3. Knowledge (SmartPrompt)
            defaultPreset.Entries.Add(new PromptEntry("Knowledge",
                "{{ load_smart_modules(user_input) }}")
            {
                Role = PromptRole.System,
                Enabled = true
            });

            // 4. Philosophy (Behavior Rules)
            defaultPreset.Entries.Add(new PromptEntry("Philosophy",
                "{{ snippets.philosophy }}")
            {
                Role = PromptRole.System,
                Enabled = true
            });

            // 5. Output Format (JSON)
            defaultPreset.Entries.Add(new PromptEntry("Output Format",
                "{{ include 'OutputFormat_JSON_Scriban' }}")
            {
                Role = PromptRole.System,
                Enabled = true
            });

            presets.Add(defaultPreset);
            SavePresets();
        }
    }
}