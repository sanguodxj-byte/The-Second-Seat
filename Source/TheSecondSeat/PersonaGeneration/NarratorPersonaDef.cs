using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using RimWorld;

namespace TheSecondSeat.PersonaGeneration
{
    public class NarratorPersonaDef : Def
    {
        public string narratorName = "Unknown";
        public string displayNameKey = "";
        public string descriptionKey = "";
        public string biography = "";
        public string portraitPath = "";
        public bool useCustomPortrait = false;
        public string customPortraitPath = "";
        public Color primaryColor = Color.white;
        public Color accentColor = Color.gray;
        public string visualDescription = "";
        public List<string> visualElements = new List<string>();
        public string visualMood = "";
        // ✅ 移除未定义的 PersonalityTrait，改用 string
        public string personalityType = "";
        public string dialogueStyleDef = "";
        public string eventPreferencesDef = "";
        public float initialAffinity = 0f;
        public AIDifficultyMode difficultyMode = AIDifficultyMode.Assistant;
        public bool enabled = true;
        public List<string> specialAbilities = new List<string>();
        
        [Unsaved]
        private PersonaAnalysisResult cachedAnalysis;
        
        public PersonaAnalysisResult GetAnalysis()
        {
            if (cachedAnalysis == null)
            {
                cachedAnalysis = new PersonaAnalysisResult
                {
                    VisualTags = new List<string>(visualElements),
                    ToneTags = new List<string>(),
                    SuggestedPersonality = null,  // ✅ 改为 null
                    ConfidenceScore = 0.5f
                };
            }
            return cachedAnalysis;
        }
        
        public void SetAnalysis(PersonaAnalysisResult analysis)
        {
            cachedAnalysis = analysis;
        }
    }
}
