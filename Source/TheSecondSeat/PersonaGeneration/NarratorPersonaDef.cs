using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using RimWorld;
using TheSecondSeat.Utils;
using TheSecondSeat.Core;

namespace TheSecondSeat.PersonaGeneration
{
    /// <summary>
    /// 降临姿态配置类
    /// ⭐ v3.1.0: 重构为动态字典，支持无限扩展姿态
    ///
    /// XML 配置示例:
    /// <descentPostures>
    ///   <postures>
    ///     <li><key>standing</key><value>Sideria/standing</value></li>
    ///     <li><key>floating</key><value>Sideria/floating</value></li>
    ///     <li><key>combat</key><value>Sideria/combat</value></li>
    ///     <li><key>casting</key><value>Sideria/casting</value></li>
    ///     <li><key>riding</key><value>Sideria/riding</value></li>
    ///     <li><key>sleeping</key><value>Sideria/sleeping</value></li>
    ///   </postures>
    /// </descentPostures>
    /// </summary>
    public class DescentPostures
    {
        /// <summary>
        /// 动态姿态字典
        /// Key: 姿态名称 (如 "standing", "floating", "riding")
        /// Value: 纹理路径
        /// </summary>
        public List<PostureEntry> postures = new List<PostureEntry>();
        
        // ============================================
        // 兼容性属性 - 保留旧版 API
        // ============================================
        
        /// <summary>站立姿态（兼容旧版）</summary>
        public string standing
        {
            get => GetPosture("standing");
            set => SetPosture("standing", value);
        }
        
        /// <summary>悬浮姿态（兼容旧版）</summary>
        public string floating
        {
            get => GetPosture("floating");
            set => SetPosture("floating", value);
        }
        
        /// <summary>战斗姿态（兼容旧版）</summary>
        public string combat
        {
            get => GetPosture("combat");
            set => SetPosture("combat", value);
        }
        
        /// <summary>施法姿态（兼容旧版）</summary>
        public string casting
        {
            get => GetPosture("casting");
            set => SetPosture("casting", value);
        }
        
        // ============================================
        // 动态 API
        // ============================================
        
        /// <summary>
        /// 获取指定姿态的纹理路径
        /// </summary>
        public string GetPosture(string postureName)
        {
            if (string.IsNullOrEmpty(postureName)) return "";
            
            var entry = postures.FirstOrDefault(p =>
                p.key?.Equals(postureName, StringComparison.OrdinalIgnoreCase) == true);
            
            return entry?.value ?? "";
        }
        
        /// <summary>
        /// 设置指定姿态的纹理路径
        /// </summary>
        public void SetPosture(string postureName, string texturePath)
        {
            if (string.IsNullOrEmpty(postureName)) return;
            
            var existing = postures.FirstOrDefault(p =>
                p.key?.Equals(postureName, StringComparison.OrdinalIgnoreCase) == true);
            
            if (existing != null)
            {
                existing.value = texturePath ?? "";
            }
            else
            {
                postures.Add(new PostureEntry { key = postureName, value = texturePath ?? "" });
            }
        }
        
        /// <summary>
        /// 检查是否存在指定姿态
        /// </summary>
        public bool HasPosture(string postureName)
        {
            if (string.IsNullOrEmpty(postureName)) return false;
            
            return postures.Any(p =>
                p.key?.Equals(postureName, StringComparison.OrdinalIgnoreCase) == true &&
                !string.IsNullOrEmpty(p.value));
        }
        
        /// <summary>
        /// 获取所有已配置的姿态名称
        /// </summary>
        public List<string> GetAllPostureNames()
        {
            return postures
                .Where(p => !string.IsNullOrEmpty(p.key) && !string.IsNullOrEmpty(p.value))
                .Select(p => p.key)
                .ToList();
        }
        
        /// <summary>
        /// 获取所有姿态作为字典
        /// </summary>
        public Dictionary<string, string> ToDictionary()
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in postures)
            {
                if (!string.IsNullOrEmpty(entry.key) && !string.IsNullOrEmpty(entry.value))
                {
                    dict[entry.key] = entry.value;
                }
            }
            return dict;
        }
        
        /// <summary>
        /// 从字典加载姿态
        /// </summary>
        public void FromDictionary(Dictionary<string, string> dict)
        {
            postures.Clear();
            if (dict == null) return;
            
            foreach (var kvp in dict)
            {
                postures.Add(new PostureEntry { key = kvp.Key, value = kvp.Value });
            }
        }
    }
    
    /// <summary>
    /// 姿态条目（用于 XML 序列化）
    /// </summary>
    public class PostureEntry
    {
        public string key;
        public string value;
    }

    /// <summary>
    /// 短语集合类 (XML兼容)
    /// </summary>
    public class PhraseSet
    {
        public string key;
        public List<string> phrases = new List<string>();
    }

    /// <summary>
    /// ⭐ v3.3.0: 关系轴配置
    /// </summary>
    public class RelationshipAxisConfig
    {
        public string key;          // 唯一标识符 (如 "Trust")
        public string label;        // 显示名称 (如 "信任")
        public float min = 0f;      // 最小值
        public float max = 100f;    // 最大值
        public float initial = 50f; // 初始值
        public string description;  // 描述
    }
    
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

        /// <summary>
        /// ⭐ API: 资源名称（用于加载纹理和音频的文件夹名称）
        /// 默认为空，此时使用 narratorName 的第一部分
        /// 设置此字段可实现多语言支持（narratorName 翻译，而文件夹名不变）
        /// </summary>
        public string resourceName = "";
        
        /// <summary>API: 本地化显示名称键</summary>
        public string displayNameKey = "";
        
        /// <summary>API: 本地化描述键</summary>
        public string descriptionKey = "";
        
        /// <summary>API: 人格传记/背景故事（影响AI行为）</summary>
        public string biography = "";

        // ============================================
        // ⭐ v1.7.0: 简易提示词 API (创作者友好)
        // ============================================

        /// <summary>
        /// API: 自定义系统提示词（最高优先级）
        /// 如果此字段不为空，将跳过 Identity/Personality/Style 等自动生成模块，
        /// 直接使用此内容作为核心 Prompt，仅追加必要的 JSON 格式指令。
        /// </summary>
        public string customSystemPrompt = "";
        
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
        
        /// <summary>
        /// ⭐ v1.11.0: 渲染树定义（表情/口型纹理映射配置）
        /// 通过 XML 定义表情变体和口型动画的纹理映射规则，
        /// 避免硬编码，支持不同角色使用不同的纹理映射方案。
        /// </summary>
        public RenderTreeDef renderTreeDef;
        
        // 运行时分层配置缓存（不从XML加载）
        [Unsaved]
        private LayeredPortraitConfig cachedLayeredConfig;
        
        // ============================================
        // 互动短语系统 API
        // ============================================
        
        /// <summary>
        /// API: 互动短语库
        /// 包含各类互动场景的台词（如：HeadPat, BodyPoke, Greeting等）
        /// </summary>
        public List<PhraseSet> phraseLibrary = new List<PhraseSet>();
        
        /// <summary>
        /// 获取随机短语
        /// </summary>
        public string GetRandomPhrase(string key)
        {
            if (phraseLibrary == null) return "";
            
            var set = phraseLibrary.FirstOrDefault(s => s.key == key);
            if (set != null && set.phrases.Count > 0)
            {
                return set.phrases.RandomElement();
            }
            return "";
        }

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
        
        /// <summary>
        /// ⭐ v1.6.75: TTS 语音名称（如 "zh-CN-XiaoxiaoNeural"）
        /// </summary>
        public string ttsVoiceName = "";
        
        /// <summary>
        /// ⭐ v1.6.75: TTS 语音音调（0.5-2.0，1.0为正常）
        /// </summary>
        public float ttsVoicePitch = 1.0f;
        
        /// <summary>
        /// ⭐ v1.6.75: TTS 语音速度（0.5-2.0，1.0为正常）
        /// </summary>
        public float ttsVoiceSpeed = 1.0f;
        
        /// <summary>
        /// ⭐ v1.6.75: TTS 语音速度（XML兼容别名，与ttsVoiceSpeed相同）
        /// </summary>
        public float ttsVoiceRate = 1.0f;
        
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

        /// <summary>
        /// ⭐ v3.3.0: 自定义关系轴列表
        /// </summary>
        public List<RelationshipAxisConfig> relationshipAxes = new List<RelationshipAxisConfig>();
        
        // ============================================
        // ⭐ v1.9.0: 叙事模式参数 API
        // ============================================
        
        /// <summary>
        /// API: 仁慈度（0.0-1.0）
        /// 影响事件的残酷程度：0=残酷无情，1=仁慈宽容
        /// </summary>
        public float mercyLevel = 0.5f;
        
        /// <summary>
        /// API: 混乱度（0.0-1.0）
        /// 影响事件的合理性：0=逻辑严谨，1=随机混乱
        /// </summary>
        public float narratorChaosLevel = 0.5f;
        
        /// <summary>
        /// API: 强势度（0.0-1.0）
        /// 影响玩家交流对事件的影响程度：0=顺从玩家，1=无视玩家意见
        /// </summary>
        public float dominanceLevel = 0.5f;
        
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
        
        /// <summary>
        /// 📌 v1.6.62: 个性标签（如：善良、坚强、爱撒娇、病娇等）
        /// 来自AI分析或用户标注，可在人格卡片上显示和修改
        /// </summary>
        public List<string> personalityTags = new List<string>();
        
        /// <summary>
        /// 📌 v1.6.62: 用户选择的特质（创建人格时选择的3个特质）
        /// </summary>
        public List<string> selectedTraits = new List<string>();

        /// <summary>
        /// ⭐ v2.3.0: 语义雷达关注点配置
        /// 存储 "抽象概念 -> 关键词列表" 的映射
        /// </summary>
        public List<TheSecondSeat.Monitoring.SemanticConcept> radarConcepts = new List<TheSecondSeat.Monitoring.SemanticConcept>();
        
        // ============================================
        // ⭐ v1.6.63: 通用降临系统配置 API
        // ============================================
        
        /// <summary>
        /// ⭐ API: 降临实体的 PawnKindDef 名称
        /// 示例: "YourPersona_Descent" 或 "Human"
        /// 如果为空，该叙事者不支持实体化降临
        /// </summary>
        [NoTranslate]
        public string descentPawnKind = "";
        
        /// <summary>
        /// ⭐ API: 空投舱/特效物的 ThingDef 名称
        /// 示例: "DropPodIncoming" 或自定义的 "MagicalPortal"
        /// 如果为空，使用默认的 DropPodIncoming
        /// </summary>
        [NoTranslate]
        public string descentSkyfallerDef = "";
        
        /// <summary>
        /// ⭐ API: 伴随生物（如龙）的 PawnKindDef 名称（可选）
        /// 示例: "YourPersona_Companion"
        /// 如果为空，不生成伴随生物
        /// </summary>
        [NoTranslate]
        public string companionPawnKind = "";
        
        /// <summary>
        /// ⭐ API: 降临时的特殊姿态图片名称
        /// 不含路径，仅文件名，例如 "body_arrival"
        /// 对应纹理路径: UI/Narrators/Descent/Postures/{descentPosturePath}
        /// </summary>
        [NoTranslate]
        public string descentPosturePath = "";
        
        /// <summary>
        /// ⭐ API: 降临时的特效图片名称
        /// 不含路径，仅文件名，例如 "glitch_circle"
        /// 对应纹理路径: UI/Narrators/Descent/Effects/{descentEffectPath}
        /// </summary>
        [NoTranslate]
        public string descentEffectPath = "";
        
        /// <summary>
        /// ⭐ API: 降临音效 SoundDef 名称
        /// 示例: "Thunder_OnMap" 或自定义的 "Descent_Arrival"
        /// 如果为空，不播放音效
        /// </summary>
        [NoTranslate]
        public string descentSound = "";
        
        /// <summary>
        /// ⭐ API: 降临信件标题（本地化键）
        /// 如果为空，使用通用标题 "{narratorName} 降临了"
        /// </summary>
        public string descentLetterLabel = "";
        
        /// <summary>
        /// ⭐ API: 降临信件内容（本地化键）
        /// 如果为空，使用通用内容
        /// </summary>
        public string descentLetterText = "";
        
        /// <summary>
        /// ⭐ v1.6.78: 是否启用降临模式
        /// </summary>
        public bool hasDescentMode = false;
        
        /// <summary>
        /// ⭐ v1.6.78: 降临持续时间（秒）
        /// </summary>
        public float descentDuration = 300f;
        
        /// <summary>
        /// ⭐ v1.6.78: 降临冷却时间（秒）
        /// </summary>
        public float descentCooldown = 600f;
        
        /// <summary>
        /// ⭐ v1.6.78: 降临特效路径列表
        /// </summary>
        public List<string> descentEffects = new List<string>();

        /// <summary>
        /// ⭐ v1.6.95: 降临实体必须拥有的 Hediff 列表
        /// 例如: Sideria_BloodBloom, Sideria_DivineBody
        /// </summary>
        public List<string> requiredHediffs = new List<string>();

        /// <summary>
        /// ⭐ v1.8.7: 降临实体初始获得的技能列表 (DefName)
        /// 用于解决 XML 中无法直接给 PawnKind 赋予技能的问题
        /// </summary>
        public List<string> abilitiesToGrant = new List<string>();

        /// <summary>
        /// ⭐ v1.8.8: 降临实体生成时添加的 Hediff 列表 (DefName)
        /// 例如: Sideria_DivineBody（包含技能赋予）
        /// </summary>
        public List<string> hediffsToGrant = new List<string>();
        
        /// <summary>
        /// ⭐ v1.6.78: 降临姿态路径字典
        /// </summary>
        public DescentPostures descentPostures = new DescentPostures();
        
        /// <summary>
        /// ⭐ v1.6.81: 降临动画类型
        /// 可选值:
        /// - "DropPod" (默认): 使用空投仓动画
        /// - "DragonFlyby": 使用实体飞掠动画 (适用于飞行生物)
        /// - "Portal": 使用传送门动画 (未来扩展)
        /// - "Lightning": 使用闪电降临动画 (未来扩展)
        /// </summary>
        [NoTranslate]
        public string descentAnimationType = "DropPod";
        
        /// <summary>
        /// ⭐ v1.6.81: 实体阴影纹理路径（仅 DragonFlyby 类型使用）
        /// ⭐ v1.6.90: 如果留空，将根据 resourceName 自动生成
        /// 相对于 Textures/ 文件夹，例如 "Narrators/Descent/Effects/YourPersona/DragonShadow"
        /// </summary>
        [NoTranslate]
        public string dragonShadowTexturePath = "";
        
        // ============================================
        // ⭐ v1.6.90: 路径自动生成API
        // ============================================
        
        /// <summary>
        /// ⭐ 获取立绘路径（自动生成或使用配置值）
        /// </summary>
        public string GetPortraitPath()
        {
            if (!string.IsNullOrEmpty(portraitPath))
                return portraitPath;
            
            string resourceName = GetResourceName();
            return TSSFrameworkConfig.Portrait.GetPortraitPath(resourceName);
        }
        
        /// <summary>
        /// ⭐ 获取降临姿态完整路径（自动生成或使用配置值）
        /// </summary>
        public string GetDescentPostureFullPath()
        {
            string resourceName = GetResourceName();
            string postureName = descentPosturePath;
            
            return TSSFrameworkConfig.Descent.GetPosturePath(resourceName, postureName);
        }
        
        /// <summary>
        /// ⭐ 获取降临特效完整路径（自动生成或使用配置值）
        /// </summary>
        public string GetDescentEffectFullPath()
        {
            string resourceName = GetResourceName();
            string effectName = descentEffectPath;
            
            return TSSFrameworkConfig.Descent.GetEffectPath(resourceName, effectName);
        }
        
        /// <summary>
        /// ⭐ 获取龙影纹理完整路径（自动生成或使用配置值）
        /// </summary>
        public string GetDragonShadowFullPath()
        {
            if (!string.IsNullOrEmpty(dragonShadowTexturePath))
                return dragonShadowTexturePath;
            
            string resourceName = GetResourceName();
            return TSSFrameworkConfig.Descent.GetShadowPath(resourceName);
        }
        
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
            if (VanillaStorytellers.Contains(defName))
            {
                useLayeredPortrait = false;
                cachedLayeredConfig = null;
                return null;
            }
            
            if (!useLayeredPortrait) return null;
            
            if (cachedLayeredConfig == null)
            {
                if (!string.IsNullOrEmpty(layeredConfigPath))
                {
                    Log.Warning($"[NarratorPersonaDef] Layered config loading from file not implemented yet: {layeredConfigPath}");
                }
                
                string resourceName = GetResourceName();
                cachedLayeredConfig = LayeredPortraitConfig.CreateDefault(defName, resourceName);
                
                if (Prefs.DevMode)
                {
                    Log.Message($"[NarratorPersonaDef] Created default layered config for {defName} (resource: {resourceName})");
                    Log.Message(cachedLayeredConfig.GetDebugInfo());
                }
            }
            
            return cachedLayeredConfig;
        }
        
        /// <summary>
        /// 获取资源名称（用于文件路径）
        /// </summary>
        public string GetResourceName()
        {
            // 1. 优先使用显式配置的资源名称
            if (!string.IsNullOrEmpty(resourceName))
            {
                return resourceName;
            }

            // 2. 其次尝试从显示名称推断 (兼容旧逻辑)
            if (!string.IsNullOrEmpty(narratorName))
            {
                return narratorName.Split(' ')[0].Trim();
            }
            
            // 3. 最后尝试从 defName 推断
            // 🏗️ 使用配置类的后缀列表
            var suffix = TSSFrameworkConfig.Persona.NameSuffixesToRemove
                .FirstOrDefault(s => defName.EndsWith(s));
            
            return suffix != null
                ? defName.Substring(0, defName.Length - suffix.Length)
                : defName;
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
                    VisualDescription = visualDescription ?? "",
                    ToneTags = toneTags != null ? new List<string>(toneTags) : new List<string>(),
                    PersonalityTags = personalityTags != null ? new List<string>(personalityTags) : new List<string>(),
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
        
        // 🏗️ 使用配置类的原版叙事者列表
        private static HashSet<string> VanillaStorytellers => TSSFrameworkConfig.Persona.VanillaStorytellers;
        
        /// <summary>
        /// 存档兼容性处理，防止 NullReferenceException
        /// 🛡️ v1.6.79: 增强自动补全逻辑
        /// ⚠️ v1.6.82: 移除纹理加载，避免线程问题
        /// </summary>
        public override void ResolveReferences()
        {
            base.ResolveReferences();
            
            // 强制禁用原版叙事者的分层立绘
            if (VanillaStorytellers.Contains(defName))
            {
                useLayeredPortrait = false;
                cachedLayeredConfig = null;
            }
            
            // 🛡️ 第一阶段：确保所有字段不为 null
            InitializeNullFields();
            
            // ⚠️ v1.6.82: 移除 AutoFillMissingResources 和 PreloadLayeredPortraitAsync
            // 这些方法会调用 ContentFinder 导致线程问题
            // 纹理加载延迟到首次使用时（在主线程的 OnGUI 中）
            
            if (Prefs.DevMode)
            {
                Log.Message($"[NarratorPersonaDef] ResolveReferences completed for {defName}, " +
                           $"useLayeredPortrait={useLayeredPortrait}, hasPortrait={!string.IsNullOrEmpty(portraitPath)}");
            }
        }
        
        /// <summary>初始化所有可能为 null 的字段</summary>
        private void InitializeNullFields()
        {
            // 集合字段
            visualElements ??= new List<string>();
            specialAbilities ??= new List<string>();
            toneTags ??= new List<string>();
            forbiddenWords ??= new List<string>();
            personalityTags ??= new List<string>();
            selectedTraits ??= new List<string>();
            radarConcepts ??= new List<TheSecondSeat.Monitoring.SemanticConcept>();
            descentEffects ??= new List<string>();
            requiredHediffs ??= new List<string>();
            abilitiesToGrant ??= new List<string>();
            hediffsToGrant ??= new List<string>();
            
            // 嵌套对象
            dialogueStyle ??= new DialogueStyleDef();
            eventPreferences ??= new EventPreferencesDef();
            descentPostures ??= new DescentPostures();
            phraseLibrary ??= new List<PhraseSet>();
            relationshipAxes ??= new List<RelationshipAxisConfig>();
            
            // 🏗️ 字符串字段使用配置类默认值
            narratorName ??= TSSFrameworkConfig.Persona.DefaultNarratorName;
            resourceName ??= "";
            displayNameKey ??= "";
            descriptionKey ??= "";
            biography ??= "";
            portraitPath ??= "";
            customPortraitPath ??= "";
            portraitPathBlink ??= "";
            portraitPathSpeaking ??= "";
            layeredConfigPath ??= "";
            visualDescription ??= "";
            visualMood ??= "";
            personalityType ??= "";
            overridePersonality ??= "";
            defaultVoice ??= "";
            voicePitch ??= "+0Hz";
            voiceRate ??= "+0%";
            ttsVoiceName ??= "";
            descentPawnKind ??= "";
            descentSkyfallerDef ??= "";
            companionPawnKind ??= "";
            descentPosturePath ??= "";
            descentEffectPath ??= "";
            descentSound ??= "";
            descentLetterLabel ??= "";
            descentLetterText ??= "";
            customSystemPrompt ??= "";
            
            // 🏗️ 数值字段使用配置类默认值
            if (ttsVoicePitch <= 0f) ttsVoicePitch = TSSFrameworkConfig.TTS.DefaultPitch;
            if (ttsVoiceSpeed <= 0f) ttsVoiceSpeed = TSSFrameworkConfig.TTS.DefaultSpeechRate;
            if (ttsVoiceRate <= 0f) ttsVoiceRate = TSSFrameworkConfig.TTS.DefaultSpeechRate;
            if (descentDuration <= 0f) descentDuration = TSSFrameworkConfig.Descent.DefaultDuration;
            if (descentCooldown <= 0f) descentCooldown = TSSFrameworkConfig.Descent.DefaultCooldown;
        }
        
        /// <summary>预加载分层立绘（必须在主线程调用）</summary>
        private void PreloadLayeredPortraitAsync()
        {
            if (!useLayeredPortrait) return;
            
            var config = GetLayeredConfig();
            if (config == null) return;
            
            // ⚠️ 修复：Unity资源加载必须在主线程执行，移除Task.Run
            try
            {
                LayeredPortraitCompositor.PreloadAllExpressions(config);
            }
            catch (Exception ex)
            {
                if (Prefs.DevMode)
                    Log.Warning($"[NarratorPersonaDef] 预加载表情失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 🛡️ 智能补全缺失资源
        /// 如果子 Mod 的 XML 缺少某些字段，自动填补"不打算做"的功能缺口
        /// </summary>
        private void AutoFillMissingResources()
        {
            // 获取资源名称（用于路径查找）
            string resourceName = GetResourceName();
            
            // -----------------------------------------
            // 🛡️ 立绘路径补全
            // -----------------------------------------
            if (string.IsNullOrEmpty(portraitPath))
            {
                // 检查是否有实际的立绘文件
                if (TSS_AssetLoader.HasPortrait(resourceName))
                {
                    // 有立绘，但路径未配置 - 使用默认路径格式
                    portraitPath = $"UI/Narrators/9x16/{resourceName}/base";
                }
                else
                {
                    // 子 Mod 不打算做立绘，指向主 Mod 的占位图
                    portraitPath = TSS_AssetLoader.DefaultPlaceholderPath;
                    
                    // 同时禁用分层立绘（没有基础立绘就不需要分层）
                    useLayeredPortrait = false;
                }
            }
            
            // -----------------------------------------
            // 🛡️ 降临音效补全
            // -----------------------------------------
            // 如果不想做声音，descentSound 保持为空
            // 播放逻辑中会检查空值并静默处理
            // 这里不需要特别处理，保持空值即可
            
            // -----------------------------------------
            // 🛡️ 降临模式自动检测
            // -----------------------------------------
            // 如果配置了降临相关资源但未启用 hasDescentMode，自动启用
            if (!hasDescentMode)
            {
                bool hasDescentConfig =
                    !string.IsNullOrEmpty(descentPawnKind) ||
                    !string.IsNullOrEmpty(descentPosturePath) ||
                    TSS_AssetLoader.HasDescentResources(resourceName);
                
                if (hasDescentConfig)
                {
                    hasDescentMode = true;
                    
                    if (Prefs.DevMode)
                    {
                        Log.Message($"[NarratorPersonaDef] Auto-enabled descent mode for {defName}");
                    }
                }
            }
            
            // -----------------------------------------
            // 🛡️ 降临姿态路径补全
            // -----------------------------------------
            if (hasDescentMode && descentPostures != null)
            {
                // 如果启用了降临但没有配置姿态，检查是否有默认姿态
                if (string.IsNullOrEmpty(descentPostures.standing))
                {
                    // 尝试查找默认姿态
                    if (TSS_AssetLoader.TextureExists($"UI/Narrators/Descent/Postures/{resourceName}/standing"))
                    {
                        descentPostures.standing = $"{resourceName}/standing";
                    }
                }
                
                if (string.IsNullOrEmpty(descentPostures.floating))
                {
                    if (TSS_AssetLoader.TextureExists($"UI/Narrators/Descent/Postures/{resourceName}/floating"))
                    {
                        descentPostures.floating = $"{resourceName}/floating";
                    }
                }
                
                if (string.IsNullOrEmpty(descentPostures.combat))
                {
                    if (TSS_AssetLoader.TextureExists($"UI/Narrators/Descent/Postures/{resourceName}/combat"))
                    {
                        descentPostures.combat = $"{resourceName}/combat";
                    }
                }
            }
            
            // -----------------------------------------
            // 🛡️ TTS 语音名称补全
            // -----------------------------------------
            if (string.IsNullOrEmpty(ttsVoiceName) && string.IsNullOrEmpty(defaultVoice))
            {
                // 🏗️ 使用配置类的默认语音
                ttsVoiceName = TSSFrameworkConfig.TTS.DefaultVoiceName;
            }
            else if (string.IsNullOrEmpty(ttsVoiceName) && !string.IsNullOrEmpty(defaultVoice))
            {
                // 兼容旧版：将 defaultVoice 复制到 ttsVoiceName
                ttsVoiceName = defaultVoice;
            }
            
            // -----------------------------------------
            // 🛡️ 信件内容补全（降临模式）
            // -----------------------------------------
            if (hasDescentMode)
            {
                if (string.IsNullOrEmpty(descentLetterLabel))
                {
                    // 使用叙事者名称生成默认标题
                    descentLetterLabel = $"TSS_Descent_LetterLabel_{defName}";
                }
                
                if (string.IsNullOrEmpty(descentLetterText))
                {
                    descentLetterText = $"TSS_Descent_LetterText_{defName}";
                }
            }
        }
    }
}
