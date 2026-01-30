using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using RimWorld;

namespace TheSecondSeat.Monitoring
{
    public class SemanticRadarSystem : GameComponent
    {
        private List<SemanticConcept> watchedConcepts = new List<SemanticConcept>();
        private const int SCAN_INTERVAL = 250;

        public SemanticRadarSystem(Game game) { }

        public override void ExposeData()
        {
            Scribe_Collections.Look(ref watchedConcepts, "watchedConcepts", LookMode.Deep);
        }

        public override void GameComponentTick()
        {
            if (Find.TickManager.TicksGame % SCAN_INTERVAL == 0) Scan();
        }

        private void Scan()
        {
            if (watchedConcepts == null || watchedConcepts.Count == 0) return;
            if (Find.Maps == null) return;

            foreach (var map in Find.Maps)
            {
                if (map.mapPawns == null) continue;
                foreach (var pawn in map.mapPawns.FreeColonists)
                {
                    ScanPawn(pawn);
                }
            }
        }

        private void ScanPawn(Pawn pawn)
        {
            if (pawn?.needs?.mood?.thoughts?.memories == null) return;

            var memories = pawn.needs.mood.thoughts.memories.Memories;
            for (int i = 0; i < memories.Count; i++)
            {
                var memory = memories[i];
                if (memory.age > SCAN_INTERVAL * 2) continue;

                string label = memory.LabelCap != null ? memory.LabelCap.ToString() : "";
                string desc = memory.Description ?? "";
                string text = (label + " " + desc).ToLower();

                foreach (var concept in watchedConcepts)
                {
                    if (concept.Matches(text))
                    {
                        NotifyNarrator(pawn, concept, memory);
                    }
                }
            }
        }

        private void NotifyNarrator(Pawn pawn, SemanticConcept concept, Thought_Memory memory)
        {
            string pawnName = pawn.Name != null ? pawn.Name.ToString() : pawn.LabelShort;
            string label = memory.LabelCap != null ? memory.LabelCap.ToString() : "Unknown";
            string desc = memory.Description ?? "";
            
            string eventText = string.Format("[Semantic Radar] Detected '{0}' from {1}: {2} ({3})",
                concept.conceptName, pawnName, label, desc);
            
            if (Verse.Prefs.DevMode) Log.Message(eventText);
            
            TheSecondSeat.Integration.MemoryContextBuilder.RecordEvent(eventText, TheSecondSeat.Integration.MemoryImportance.High);
        }
        
        public void AddConcept(string name, List<string> keywords)
        {
            var concept = new SemanticConcept { conceptName = name, keywords = keywords };
            watchedConcepts.Add(concept);
        }
    }

    public class SemanticConcept : IExposable
    {
        public string conceptName;
        
        // ⭐ XML 严格匹配：必须使用小写 keywords，因为 XML 中是 <keywords>
        // RimWorld 1.5+ 对大小写敏感，LoadAlias 在此处无效
        public List<string> keywords = new List<string>();

        public bool Matches(string text)
        {
            if (keywords == null) return false;
            foreach (var keyword in keywords)
            {
                if (!string.IsNullOrEmpty(keyword) && text.Contains(keyword.ToLower())) return true;
            }
            return false;
        }

        public void ExposeData()
        {
            // Scribe 保存时使用标准标签，但读取时为了兼容旧档可能需要处理
            Scribe_Values.Look(ref conceptName, "conceptName");
            if (string.IsNullOrEmpty(conceptName))
            {
                Scribe_Values.Look(ref conceptName, "name");
            }

            Scribe_Collections.Look(ref keywords, "keywords", LookMode.Value);
        }
    }
}