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
        
        // ✅ 动画系统纹理路径
        public string portraitPathBlink = "";      // 闭眼纹理路径
        public string portraitPathSpeaking = "";   // 张嘴纹理路径
        
        // ✅ 分层立绘系统配置
        public bool useLayeredPortrait = false;    // 是否使用分层立绘系统
        public string layeredConfigPath = "";      // 分层配置文件路径（可选）
        
        // 运行时分层配置缓存（不从XML加载）
        [Unsaved]
        private LayeredPortraitConfig cachedLayeredConfig;
        
        /// <summary>
        /// 获取或创建分层立绘配置
        /// </summary>
        public LayeredPortraitConfig GetLayeredConfig()
        {
            if (!useLayeredPortrait)
            {
                return null;
            }
            
            if (cachedLayeredConfig == null)
            {
                // 如果指定了配置文件路径，尝试加载
                if (!string.IsNullOrEmpty(layeredConfigPath))
                {
                    // TODO: 从文件加载配置（未来版本）
                    Log.Warning($"[NarratorPersonaDef] Layered config loading from file not implemented yet: {layeredConfigPath}");
                }
                
                // 否则创建默认配置
                cachedLayeredConfig = LayeredPortraitConfig.CreateDefault(defName);
                
                if (Prefs.DevMode)
                {
                    Log.Message($"[NarratorPersonaDef] Created default layered config for {defName}");
                    Log.Message(cachedLayeredConfig.GetDebugInfo());
                }
            }
            
            return cachedLayeredConfig;
        }
        
        /// <summary>
        /// 设置自定义分层配置
        /// </summary>
        public void SetLayeredConfig(LayeredPortraitConfig config)
        {
            cachedLayeredConfig = config;
            useLayeredPortrait = true;
        }
        
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
