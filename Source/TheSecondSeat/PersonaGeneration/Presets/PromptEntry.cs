using System;
using Verse;

namespace TheSecondSeat.PersonaGeneration.Presets
{
    public enum PromptRole
    {
        System,
        User,
        Assistant
    }

    public class PromptEntry : IExposable
    {
        public string Id;
        public string Name;
        public string Content;
        public bool Enabled = true;
        public PromptRole Role = PromptRole.System;
        public string CustomRole; // For custom role names if needed

        public PromptEntry()
        {
            Id = Guid.NewGuid().ToString();
        }

        public PromptEntry(string name, string content, PromptRole role = PromptRole.System) : this()
        {
            Name = name;
            Content = content;
            Role = role;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref Id, "id", Guid.NewGuid().ToString());
            Scribe_Values.Look(ref Name, "name", "New Entry");
            Scribe_Values.Look(ref Content, "content", "");
            Scribe_Values.Look(ref Enabled, "enabled", true);
            Scribe_Values.Look(ref Role, "role", PromptRole.System);
            Scribe_Values.Look(ref CustomRole, "customRole");
        }

        public PromptEntry Clone()
        {
            return new PromptEntry
            {
                Id = Guid.NewGuid().ToString(),
                Name = Name,
                Content = Content,
                Enabled = Enabled,
                Role = Role,
                CustomRole = CustomRole
            };
        }
    }
}