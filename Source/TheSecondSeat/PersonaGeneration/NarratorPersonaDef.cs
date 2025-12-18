using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using RimWorld;

namespace TheSecondSeat.PersonaGeneration
{
    /// <summary>
    /// 叙事者人格定义 - RimWorld Def类型
    /// 继承自 Verse.Def，可通过XML加载
    /// 
    /// API扩展指南：
    /// 1. 所有public字段都可以在XML中配置
    /// 2. 创建新Mod时，只需创建XML文件定义新人格
    /// 3. 纹理路径相对于Mod的Textures/文件夹
    /// 4. 支持分层立绘系统（useLayeredPortrait=true）
    /// 5. 对话风格和事件偏好通过嵌套对象配置
    /// </summary>
    public class NarratorPersonaDef : Def
    {
        // ============================================
        // 基本信息 API
        // ============================================
        
        /// <summary>API: 叙事者名称（显示用）</summary>
        public string narratorName = "Unknown";
        
        /// <summary>API: 本地化显示名称键</summary>
        public string displayNameKey = "";
        
        /// <summary>API: 本地化描述键</summary>
        public string descriptionKey = "";
        
        /// <summary>API: 人格传记/背景故事（影响AI行为）</summary>
        public string biography = "";
        
        // ============================================
        // 立绘系统 API
        // ============================================
        
        /// <summary>API: 头像路径（512x512，相对于Textures/）</summary>
        public string portraitPath = "";
        
        /// <summary>API: 是否使用自定义头像（已废弃，使用portraitPath）</summary>
        public bool useCustomPortrait = false;
        
        /// <summary>API: 自定义头像路径（已废弃）</summary>
        public string customPortraitPath = "";
        
        /// <summary>API: 闭眼纹理路径（用于眨眼动画）</summary>
        public string portraitPathBlink = "";
        
        /// <summary>API: 张嘴纹理路径（用于说话动画）</summary>
        public string portraitPathSpeaking = "";
        
        /// <summary>API: 是否启用分层立绘系统（支持多表情）</summary>
        public bool useLayeredPortrait = false;
        
        /// <summary>API: 分层配置文件路径（可选，留空使用默认配置）</summary>
        public string layeredConfigPath = "";
        
        // 运行时分层配置缓存（不从XML加载）
        [Unsaved]
        private LayeredPortraitConfig cachedLayeredConfig;
        
        // ============================================
        // 视觉主题 API
        // ============================================
        
        /// <summary>API: 主题色（用于UI边框、按钮等）</summary>
        public Color primaryColor = Color.white;
        
        /// <summary>API: 强调色（用于高亮、选中状态）</summary>
        public Color accentColor = Color.gray;
        
        // ============================================
        // 多模态分析 API
        // ============================================
        
        /// <summary>API: 视觉描述（用于多模态AI分析）</summary>
        public string visualDescription = "";
        
        /// <summary>API: 视觉元素标签列表</summary>
        public List<string> visualElements = new List<string>();
        
        /// <summary>API: 视觉氛围描述</summary>
        public string visualMood = "";
        
        // ============================================
        // 人格类型 API
        // ============================================
        
        /// <summary>API: 人格类型（自动推断）</summary>
        public string personalityType = "";
        
        /// <summary>API: 强制指定人格类型（覆盖自动推断）</summary>
        public string overridePersonality = "";
        
        // ============================================
        // 语音系统 API
        // ============================================
        
        /// <summary>API: 默认语音ID（TTS用）</summary>
        public string defaultVoice = "";
        
        /// <summary>API: 语音音调调整（如"+5Hz"、"-3Hz"）</summary>
        public string voicePitch = "+0Hz";
        
        /// <summary>API: 语音速度调整（如"+10%"、"-5%"）</summary>
        public string voiceRate = "+0%";
        
        // ============================================
        // 对话与事件配置 API
        // ============================================
        
        /// <summary>API: 对话风格配置（嵌套对象，从XML加载）</summary>
        public DialogueStyleDef dialogueStyle = new DialogueStyleDef();
        
        /// <summary>API: 事件偏好配置（嵌套对象，从XML加载）</summary>
        public EventPreferencesDef eventPreferences = new EventPreferencesDef();
        
        // ============================================
        // 好感度系统 API
        // ============================================
        
        /// <summary>API: 初始好感度（-100到100）</summary>
        public float initialAffinity = 0f;
        
        /// <summary>API: 基础好感度偏移（-1.0到1.0）</summary>
        public float baseAffinityBias = 0f;
        
        // ============================================
        // AI行为模式 API
        // ============================================
        
        /// <summary>API: AI难度模式（Assistant=助手, Opponent=对手）</summary>
        public AIDifficultyMode difficultyMode = AIDifficultyMode.Assistant;
        
        // ============================================
        // 其他配置 API
        // ============================================
        
        /// <summary>API: 是否启用此人格</summary>
        public bool enabled = true;
        
        /// <summary>API: 特殊能力列表</summary>
        public List<string> specialAbilities = new List<string>();
        
        /// <summary>API: 语气标签（用于LLM Prompt生成）</summary>
        public List<string> toneTags = new List<string>();
        
        /// <summary>API: 禁用词列表（AI不会使用这些词）</summary>
        public List<string> forbiddenWords = new List<string>();
        
        // ============================================
        // 运行时数据（不从XML加载）
        // ============================================
        
        [Unsaved]
        private PersonaAnalysisResult cachedAnalysis;
        
        /// <summary>
        /// 获取或创建分层立绘配置
        /// </summary>
        public LayeredPortraitConfig GetLayeredConfig()
        {
            // 强制禁用原版叙事者的分层立绘
            if (defName == "Cassandra_Classic" || 
                defName == "Phoebe_Chillax" || 
                defName == "Randy_Random" ||
                defName == "Igor_Invader" ||
                defName == "Luna_Protector")
            {
                useLayeredPortrait = false;
                cachedLayeredConfig = null;
                return null;
            }
            
            if (!useLayeredPortrait)
            {
                return null;
            }
            
            if (cachedLayeredConfig == null)
            {
                if (!string.IsNullOrEmpty(layeredConfigPath))
                {
                    Log.Warning($"[NarratorPersonaDef] Layered config loading from file not implemented yet: {layeredConfigPath}");
                }
                
                string personaName = GetPersonaName();
                cachedLayeredConfig = LayeredPortraitConfig.CreateDefault(defName, personaName);
                
                if (Prefs.DevMode)
                {
                    Log.Message($"[NarratorPersonaDef] Created default layered config for {defName} (persona: {personaName})");
                    Log.Message(cachedLayeredConfig.GetDebugInfo());
                }
            }
            
            return cachedLayeredConfig;
        }
        
        /// <summary>
        /// 获取人格名称（用于纹理路径）
        /// </summary>
        private string GetPersonaName()
        {
            if (!string.IsNullOrEmpty(narratorName))
            {
                return narratorName.Split(' ')[0].Trim();
            }
            
            string[] suffixesToRemove = new[] { 
                "_Default", "_Classic", "_Custom", "_Persona", 
                "_Chillax", "_Random", "_Invader", "_Protector" 
            };
            
            foreach (var suffix in suffixesToRemove)
            {
                if (defName.EndsWith(suffix))
                {
                    return defName.Substring(0, defName.Length - suffix.Length);
                }
            }
            
            return defName;
        }
        
        /// <summary>
        /// 获取人格分析结果（懒加载）
        /// </summary>
        public PersonaAnalysisResult GetAnalysis()
        {
            if (cachedAnalysis == null)
            {
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
        
        /// <summary>
        /// 存档兼容性处理，防止 NullReferenceException
        /// </summary>
        public override void ResolveReferences()
        {
            base.ResolveReferences();
            
            // 强制禁用原版叙事者的分层立绘
            if (defName == "Cassandra_Classic" || 
                defName == "Phoebe_Chillax" || 
                defName == "Randy_Random" ||
                defName == "Igor_Invader" ||
                defName == "Luna_Protector")
            {
                useLayeredPortrait = false;
                cachedLayeredConfig = null;
            }
            
            // 如果启用分层立绘，预加载所有表情
            if (useLayeredPortrait)
            {
                var config = GetLayeredConfig();
                if (config != null)
                {
                    System.Threading.Tasks.Task.Run(() =>
                    {
                        try
                        {
                            LayeredPortraitCompositor.PreloadAllExpressions(config);
                        }
                        catch (Exception ex)
                        {
                            if (Prefs.DevMode)
                            {
                                Log.Warning($"[NarratorPersonaDef] 预加载表情失败: {ex.Message}");
                            }
                        }
                    });
                }
            }
            
            // 确保所有集合字段都被初始化
            if (visualElements == null) visualElements = new List<string>();
            if (specialAbilities == null) specialAbilities = new List<string>();
            if (toneTags == null) toneTags = new List<string>();
            if (forbiddenWords == null) forbiddenWords = new List<string>();
            
            // 确保嵌套对象被初始化
            if (dialogueStyle == null) dialogueStyle = new DialogueStyleDef();
            if (eventPreferences == null) eventPreferences = new EventPreferencesDef();
            
            // 确保字符串字段不为 null
            if (narratorName == null) narratorName = "Unknown";
            if (displayNameKey == null) displayNameKey = "";
            if (descriptionKey == null) descriptionKey = "";
            if (biography == null) biography = "";
            if (portraitPath == null) portraitPath = "";
            if (customPortraitPath == null) customPortraitPath = "";
            if (portraitPathBlink == null) portraitPathBlink = "";
            if (portraitPathSpeaking == null) portraitPathSpeaking = "";
            if (layeredConfigPath == null) layeredConfigPath = "";
            if (visualDescription == null) visualDescription = "";
            if (visualMood == null) visualMood = "";
            if (personalityType == null) personalityType = "";
            if (overridePersonality == null) overridePersonality = "";
            if (defaultVoice == null) defaultVoice = "";
            if (voicePitch == null) voicePitch = "+0Hz";
            if (voiceRate == null) voiceRate = "+0%";
            
            if (Prefs.DevMode)
            {
                Log.Message($"[NarratorPersonaDef] ResolveReferences completed for {defName}, useLayeredPortrait={useLayeredPortrait}");
            }
        }
    }
}
