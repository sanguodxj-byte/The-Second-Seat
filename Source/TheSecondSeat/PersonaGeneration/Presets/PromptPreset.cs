using System;
using System.Collections.Generic;
using Verse;

namespace TheSecondSeat.PersonaGeneration.Presets
{
    public class PromptPreset : IExposable
    {
        public string Id = Guid.NewGuid().ToString();
        public string Name = "Default Preset";
        public string Description = "";
        public List<PromptEntry> Entries = new List<PromptEntry>();
        public bool IsActive;

        public PromptPreset() { }

        public PromptPreset(string name)
        {
            Name = name;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref Id, "id", Guid.NewGuid().ToString());
            Scribe_Values.Look(ref Name, "name", "Default Preset");
            Scribe_Values.Look(ref Description, "description", "");
            Scribe_Collections.Look(ref Entries, "entries", LookMode.Deep);
            Scribe_Values.Look(ref IsActive, "isActive", false);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                Entries ??= new List<PromptEntry>();
            }
        }

        public PromptPreset Clone()
        {
            var clone = new PromptPreset
            {
                Id = Guid.NewGuid().ToString(),
                Name = Name + " (Copy)",
                Description = Description,
                IsActive = false
            };

            foreach (var entry in Entries)
            {
                clone.Entries.Add(entry.Clone());
            }

            return clone;
        }
    }
}