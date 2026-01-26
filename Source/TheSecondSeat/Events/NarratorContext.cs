using System;
using Verse;
using RimWorld;
using TheSecondSeat.Narrator;
using TheSecondSeat.PersonaGeneration; // ⭐ v2.6.0: 添加 NarratorPersonaDef 所在的命名空间

namespace TheSecondSeat.Framework
{
    /// <summary>
    /// A strongly-typed, read-only struct to hold the context for Narrator Events.
    /// Replaces the Dictionary<string, object> to reduce GC pressure and improve performance.
    /// </summary>
    public readonly struct NarratorContext
    {
        public readonly string PersonaName;
        public readonly NarratorPersonaDef PersonaDef;
        public readonly float Affinity;
        public readonly string Mood;
        public readonly string DifficultyMode;
        
        public readonly int ColonistCount;
        public readonly int PrisonerCount;
        public readonly int AnimalCount;
        
        public readonly float WealthTotal;
        public readonly float WealthBuildings;
        public readonly float WealthItems;
        
        public readonly int GameTicks;
        public readonly int GameYear;
        public readonly Season GameSeason;

        public NarratorContext(
            string personaName,
            NarratorPersonaDef personaDef,
            float affinity,
            string mood,
            string difficultyMode,
            int colonistCount,
            int prisonerCount,
            int animalCount,
            float wealthTotal,
            float wealthBuildings,
            float wealthItems,
            int gameTicks,
            int gameYear,
            Season gameSeason)
        {
            PersonaName = personaName;
            PersonaDef = personaDef;
            Affinity = affinity;
            Mood = mood;
            DifficultyMode = difficultyMode;
            ColonistCount = colonistCount;
            PrisonerCount = prisonerCount;
            AnimalCount = animalCount;
            WealthTotal = wealthTotal;
            WealthBuildings = wealthBuildings;
            WealthItems = wealthItems;
            GameTicks = gameTicks;
            GameYear = gameYear;
            GameSeason = gameSeason;
        }

        public static NarratorContext Empty => new NarratorContext(
            "", null, 0f, "Neutral", "Normal", 
            0, 0, 0, 
            0f, 0f, 0f, 
            0, 0, Season.Undefined
        );
    }
}
