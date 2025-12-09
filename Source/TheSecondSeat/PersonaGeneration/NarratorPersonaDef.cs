using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using RimWorld;

namespace TheSecondSeat.PersonaGeneration
{
    /// <summary>
    /// 叙事者人格定义 - RimWorld Def类型
    /// 继承自 Verse.Def，确保可以被RimWorld的Def系统识别和加载
    /// </summary>
    public class NarratorPersonaDef : Def
    {
        // === 基本信息 ===
        public string narratorName = "Unknown";
        public string displayNameKey = "";
        public string descriptionKey = "";
        public string biography = "";
        
        // === 立绘相关 ===
        public string portraitPath = "";
        public bool useCustomPortrait = false;
        public string customPortraitPath = "";
        
        // === 颜色主题 ===
        public Color primaryColor = Color.white;
        public Color accentColor = Color.gray;
        
        // === 视觉特征（用于多模态分析）===
        public string visualDescription = "";
        public List<string> visualElements = new List<string>();
        public string visualMood = "";
        
        // === 人格类型 ===
        public string personalityType = "";
        public string overridePersonality = "";  // 手动指定的人格类型
        
        // === 语音设置 ===
        public string defaultVoice = "";  // 默认语音ID
        public string voicePitch = "+0Hz";  // 语音音调调整
        public string voiceRate = "+0%";   // 语音速度调整
        
        // === 对话风格（嵌套对象，直接从XML加载）===
        public DialogueStyleDef dialogueStyle = new DialogueStyleDef();
        
        // === 事件偏好（嵌套对象，直接从XML加载）===
        public EventPreferencesDef eventPreferences = new EventPreferencesDef();
        
        // === 初始好感度 ===
        public float initialAffinity = 0f;
        public float baseAffinityBias = 0f;  // -1.0 ~ 1.0，影响初始好感度偏移
        
        // === AI难度模式 ===
        public AIDifficultyMode difficultyMode = AIDifficultyMode.Assistant;
        
        // === 其他 ===
        public bool enabled = true;
        public List<string> specialAbilities = new List<string>();
        public List<string> toneTags = new List<string>();  // 语气标签（用于 LLM Prompt）
        public List<string> forbiddenWords = new List<string>();  // 禁用词
        
        // === 运行时生成的对象（不从XML加载）===
        [Unsaved]
        private PersonaAnalysisResult cachedAnalysis;
        
        /// <summary>
        /// 获取人格分析结果（懒加载）
        /// </summary>
        public PersonaAnalysisResult GetAnalysis()
        {
            if (cachedAnalysis == null)
            {
                // 尝试将字符串转换为PersonalityTrait枚举
                Storyteller.PersonalityTrait? suggestedTrait = null;
                string personalityStr = !string.IsNullOrEmpty(overridePersonality) ? overridePersonality : personalityType;
                
                if (!string.IsNullOrEmpty(personalityStr))
                {
                    if (Enum.TryParse<Storyteller.PersonalityTrait>(personalityStr, true, out var trait))
                    {
                        suggestedTrait = trait;
                    }
                }
                
                cachedAnalysis = new PersonaAnalysisResult
                {
                    VisualTags = visualElements != null ? new List<string>(visualElements) : new List<string>(),
                    ToneTags = toneTags != null ? new List<string>(toneTags) : new List<string>(),
                    SuggestedPersonality = suggestedTrait,
                    ConfidenceScore = suggestedTrait.HasValue ? 1.0f : 0.5f
                };
            }
            return cachedAnalysis;
        }
        
        /// <summary>
        /// 设置人格分析结果（用于多模态分析）
        /// </summary>
        public void SetAnalysis(PersonaAnalysisResult analysis)
        {
            cachedAnalysis = analysis;
        }
        
        /// <summary>
        /// 获取本地化显示名称
        /// </summary>
        public string GetDisplayName()
        {
            if (!string.IsNullOrEmpty(displayNameKey))
            {
                return displayNameKey.Translate();
            }
            return narratorName;
        }
        
        /// <summary>
        /// 获取本地化描述
        /// </summary>
        public string GetDescription()
        {
            if (!string.IsNullOrEmpty(descriptionKey))
            {
                return descriptionKey.Translate();
            }
            return biography;
        }
    }
}
