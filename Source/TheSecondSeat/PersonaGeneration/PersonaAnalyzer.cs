using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Verse;
using UnityEngine;
using TheSecondSeat.Storyteller;

namespace TheSecondSeat.PersonaGeneration
{
    /// <summary>
    /// 叙事者人格定义 - 包含立绘、简介、人格特质等
    /// </summary>
    public class NarratorPersonaDef : Def
    {
        // ? 必需字段（继承自Def）
        // defName - 继承自 Def（必需）
        // label - 继承自 Def（必需，添加下面这行）
        
        // ? 显示信息
        public string narratorName = "Cassandra";  // 叙事者显示名称
        public string displayNameKey = "TSS_Narrator_Cassandra";  // 翻译键（可选）
        public string descriptionKey = "TSS_Narrator_Cassandra_Desc";  // 描述翻译键（可选）
        
        // 立绘资源
        public string portraitPath = "UI/Narrators/Cassandra";
        public string customPortraitPath = "";       // 用户自定义立绘路径（仅运行时使用）
        public bool useCustomPortrait = false;      // 是否使用自定义立绘（仅运行时使用）
        public Color primaryColor = Color.white;
        public Color accentColor = Color.gray;
        
        // 简介文本
        public string biography = "";
        
        // ? 【新增】Vision 分析结果（AI 对自身外观的理解）
        public string visualDescription = "";        // 外观描述（来自 Vision API）
        public List<string> visualElements = new List<string>();  // 视觉元素（如"armor", "sword", "dragon"）
        public string visualMood = "";               // 视觉氛围（如"menacing", "gentle", "mysterious"）
        
        // 自动生成的人格独有字段（可选）
        public string overridePersonality = null;
        public float baseAffinityBias = 0f; // -1.0 ~ 1.0，影响初始好感度偏移
        
        // 对话风格定义
        public DialogueStyleDef dialogueStyle = new DialogueStyleDef();
        
        // 事件偏好
        public EventPreferencesDef eventPreferences = new EventPreferencesDef();
        
        // 语气标签（用于 LLM Prompt）
        public List<string> toneTags = new List<string>();
        
        // 禁用词（防止 AI 使用不符合人设的词汇）
        public List<string> forbiddenWords = new List<string>();
    }

    /// <summary>
    /// 叙事者人格分析器 - 从立绘和简介生成人格
    /// </summary>
    public static class PersonaAnalyzer
    {
        /// <summary>
        /// 分析立绘颜色，推断人格特质
        /// </summary>
        public static PersonalityTrait AnalyzePortraitColor(Color primaryColor)
        {
            // 转换为 HSV
            Color.RGBToHSV(primaryColor, out float h, out float s, out float v);

            // 基于色相推断人格
            if (s < 0.2f && v > 0.7f)
            {
                // 低饱和度，高明度 → 中性、理性
                return PersonalityTrait.Strategic;
            }
            else if (h < 0.05f || h > 0.95f)
            {
                // 红色系 → 激情、冲突
                return v > 0.5f ? PersonalityTrait.Chaotic : PersonalityTrait.Sadistic;
            }
            else if (h >= 0.05f && h < 0.15f)
            {
                // 橙色系 → 温暖、保护
                return PersonalityTrait.Protective;
            }
            else if (h >= 0.15f && h < 0.45f)
            {
                // 黄绿色系 → 生机、仁慈
                return PersonalityTrait.Benevolent;
            }
            else if (h >= 0.45f && h < 0.75f)
            {
                // 蓝色系 → 冷静、战略
                return v > 0.5f ? PersonalityTrait.Strategic : PersonalityTrait.Manipulative;
            }
            else
            {
                // 紫红色系 → 神秘、操控
                return PersonalityTrait.Manipulative;
            }
        }

        /// <summary>
        /// 分析简介文本，提取人格特征
        /// </summary>
        public static PersonaAnalysisResult AnalyzeBiography(string biography)
        {
            var result = new PersonaAnalysisResult();
            
            if (string.IsNullOrEmpty(biography))
            {
                return result;
            }

            var lowerBio = biography.ToLower();

            // 分析关键词，推断人格特质
            var traitScores = new Dictionary<PersonalityTrait, int>
            {
                { PersonalityTrait.Benevolent, 0 },
                { PersonalityTrait.Sadistic, 0 },
                { PersonalityTrait.Chaotic, 0 },
                { PersonalityTrait.Strategic, 0 },
                { PersonalityTrait.Protective, 0 },
                { PersonalityTrait.Manipulative, 0 }
            };

            // 仁慈关键词
            var benevolentKeywords = new[] { "kindness", "仁慈", "善良", "帮助", "关怀", "compassion", "caring", "gentle" };
            traitScores[PersonalityTrait.Benevolent] += CountKeywords(lowerBio, benevolentKeywords);

            // 施虐关键词
            var sadisticKeywords = new[] { "cruel", "残忍", "痛苦", "折磨", "sadistic", "torment", "suffering" };
            traitScores[PersonalityTrait.Sadistic] += CountKeywords(lowerBio, sadisticKeywords);

            // 混乱关键词
            var chaoticKeywords = new[] { "chaos", "混乱", "随机", "unpredictable", "random", "wild", "狂野" };
            traitScores[PersonalityTrait.Chaotic] += CountKeywords(lowerBio, chaoticKeywords);

            // 战略关键词
            var strategicKeywords = new[] { "strategic", "战略", "计划", "理性", "rational", "calculated", "planning" };
            traitScores[PersonalityTrait.Strategic] += CountKeywords(lowerBio, strategicKeywords);

            // 保护关键词
            var protectiveKeywords = new[] { "protect", "保护", "守护", "guardian", "shield", "defend", "防御" };
            traitScores[PersonalityTrait.Protective] += CountKeywords(lowerBio, protectiveKeywords);

            // 操控关键词
            var manipulativeKeywords = new[] { "manipulate", "操控", "控制", "cunning", "狡猾", "scheme", "诡计" };
            traitScores[PersonalityTrait.Manipulative] += CountKeywords(lowerBio, manipulativeKeywords);

            // 选择得分最高的人格
            var maxScore = traitScores.Max(x => x.Value);
            if (maxScore > 0)
            {
                result.SuggestedPersonality = traitScores.First(x => x.Value == maxScore).Key;
            }

            // 分析对话风格
            result.DialogueStyle = AnalyzeDialogueStyle(lowerBio);

            // 提取语气标签
            result.ToneTags = ExtractToneTags(lowerBio);

            // 分析事件偏好
            result.EventPreferences = AnalyzeEventPreferences(lowerBio);

            return result;
        }

        /// <summary>
        /// 分析对话风格
        /// </summary>
        private static DialogueStyleDef AnalyzeDialogueStyle(string lowerBio)
        {
            var style = new DialogueStyleDef();

            // 正式程度
            var formalKeywords = new[] { "formal", "正式", "professional", "专业", "proper", "得体" };
            var casualKeywords = new[] { "casual", "随意", "friendly", "友好", "relaxed", "轻松" };
            
            int formalCount = CountKeywords(lowerBio, formalKeywords);
            int casualCount = CountKeywords(lowerBio, casualKeywords);
            style.formalityLevel = CalculateRatio(formalCount, casualCount, 0.5f);

            // 情感表达
            var emotionalKeywords = new[] { "emotional", "情绪", "passionate", "热情", "感性", "expressive" };
            var calmKeywords = new[] { "calm", "冷静", "stoic", "理性", "composed", "镇定" };
            
            int emotionalCount = CountKeywords(lowerBio, emotionalKeywords);
            int calmCount = CountKeywords(lowerBio, calmKeywords);
            style.emotionalExpression = CalculateRatio(emotionalCount, calmCount, 0.5f);

            // 话多程度
            var verboseKeywords = new[] { "talkative", "话多", "verbose", "冗长", "详细", "detailed" };
            var conciseKeywords = new[] { "concise", "简洁", "brief", "简短", "terse", "寡言" };
            
            int verboseCount = CountKeywords(lowerBio, verboseKeywords);
            int conciseCount = CountKeywords(lowerBio, conciseKeywords);
            style.verbosity = CalculateRatio(verboseCount, conciseCount, 0.5f);

            // 幽默感
            var humorKeywords = new[] { "humor", "幽默", "funny", "有趣", "witty", "机智", "playful", "俏皮" };
            style.humorLevel = Math.Min(1f, CountKeywords(lowerBio, humorKeywords) * 0.2f);

            // 讽刺程度
            var sarcasmKeywords = new[] { "sarcasm", "讽刺", "cynical", "愤世", "sardonic", "嘲讽" };
            style.sarcasmLevel = Math.Min(1f, CountKeywords(lowerBio, sarcasmKeywords) * 0.25f);

            // 标点符号偏好
            style.useEmoticons = lowerBio.Contains("emoticon") || lowerBio.Contains("表情");
            style.useEllipsis = lowerBio.Contains("...") || lowerBio.Contains("省略");
            style.useExclamation = lowerBio.Contains("!") || lowerBio.Contains("！");

            return style;
        }

        /// <summary>
        /// 提取语气标签
        /// </summary>
        private static List<string> ExtractToneTags(string lowerBio)
        {
            var tags = new List<string>();

            var toneMapping = new Dictionary<string[], string>
            {
                { new[] { "gentle", "温柔", "soft", "柔和" }, "gentle" },
                { new[] { "stern", "严厉", "strict", "严格" }, "stern" },
                { new[] { "playful", "俏皮", "mischievous", "淘气" }, "playful" },
                { new[] { "mysterious", "神秘", "enigmatic", "谜" }, "mysterious" },
                { new[] { "cheerful", "开朗", "upbeat", "乐观" }, "cheerful" },
                { new[] { "melancholic", "忧郁", "somber", "阴郁" }, "melancholic" },
                { new[] { "authoritative", "权威", "commanding", "威严" }, "authoritative" },
                { new[] { "nurturing", "慈爱", "motherly", "母性" }, "nurturing" }
            };

            foreach (var mapping in toneMapping)
            {
                if (CountKeywords(lowerBio, mapping.Key) > 0)
                {
                    tags.Add(mapping.Value);
                }
            }

            return tags;
        }

        /// <summary>
        /// 分析事件偏好
        /// </summary>
        private static EventPreferencesDef AnalyzeEventPreferences(string lowerBio)
        {
            var prefs = new EventPreferencesDef();

            // 正面事件倾向
            var positiveKeywords = new[] { "reward", "奖励", "gift", "礼物", "blessing", "祝福", "prosperity", "繁荣" };
            var negativeKeywords = new[] { "challenge", "挑战", "trial", "考验", "hardship", "困难", "struggle", "挣扎" };
            
            int positiveCount = CountKeywords(lowerBio, positiveKeywords);
            int negativeCount = CountKeywords(lowerBio, negativeKeywords);
            
            prefs.positiveEventBias = CalculateRatio(positiveCount, negativeCount, 0f);
            prefs.negativeEventBias = -prefs.positiveEventBias;

            // 混乱程度
            var chaosKeywords = new[] { "chaos", "混乱", "entropy", "熵", "disorder", "无序", "unpredictable", "不可预测" };
            prefs.chaosLevel = Math.Min(1f, CountKeywords(lowerBio, chaosKeywords) * 0.25f);

            // 干预频率
            var activeKeywords = new[] { "active", "主动", "intervene", "干预", "involved", "参与" };
            var passiveKeywords = new[] { "passive", "被动", "observe", "观察", "distant", "疏远" };
            
            int activeCount = CountKeywords(lowerBio, activeKeywords);
            int passiveCount = CountKeywords(lowerBio, passiveKeywords);
            prefs.interventionFrequency = CalculateRatio(activeCount, passiveCount, 0.5f);

            return prefs;
        }

        // 辅助方法
        private static int CountKeywords(string text, string[] keywords)
        {
            return keywords.Sum(keyword => 
                (text.Length - text.Replace(keyword, "").Length) / keyword.Length);
        }

        private static float CalculateRatio(int positiveCount, int negativeCount, float defaultValue)
        {
            int total = positiveCount + negativeCount;
            if (total == 0) return defaultValue;
            
            return Mathf.Clamp01((float)positiveCount / total);
        }

        /// <summary>
        /// 完整分析人格定义（支持多模态 AI）
        /// </summary>
        public static async Task<PersonaAnalysisResult> AnalyzePersonaDefAsync(NarratorPersonaDef def, Texture2D? portraitTexture = null)
        {
            var result = new PersonaAnalysisResult();

            // 1. 尝试使用多模态分析（如果启用且有纹理）
            VisionAnalysisResult? visionResult = null;
            if (portraitTexture != null)
            {
                var modSettings = LoadedModManager.GetMod<Settings.TheSecondSeatMod>()?.GetSettings<Settings.TheSecondSeatSettings>();
                if (modSettings?.enableMultimodalAnalysis == true)
                {
                    try
                    {
                        visionResult = await MultimodalAnalysisService.Instance.AnalyzeTextureAsync(portraitTexture);
                        
                        if (visionResult != null)
                        {
                            Log.Message($"[PersonaAnalyzer] 多模态分析成功: {visionResult.suggestedPersonality}");
                            
                            // 使用 AI 提取的颜色
                            def.primaryColor = visionResult.GetPrimaryColor();
                            def.accentColor = visionResult.GetAccentColor();
                            
                            // 如果没有简介，使用 AI 生成的描述
                            if (string.IsNullOrEmpty(def.biography))
                            {
                                def.biography = visionResult.characterDescription;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Warning($"[PersonaAnalyzer] 多模态分析失败，回退到本地分析: {ex.Message}");
                    }
                }
            }

            // 2. 分析立绘颜色（使用 AI 提取的或手动指定的）
            var colorPersonality = AnalyzePortraitColor(def.primaryColor);

            // 3. 分析简介文本
            var bioResult = AnalyzeBiography(def.biography);

            // 4. 整合结果（优先级：手动指定 > AI 建议 > 简介分析 > 颜色分析）
            if (!string.IsNullOrEmpty(def.overridePersonality))
            {
                result.SuggestedPersonality = ParsePersonality(def.overridePersonality);
            }
            else if (visionResult != null && !string.IsNullOrEmpty(visionResult.suggestedPersonality))
            {
                result.SuggestedPersonality = ParsePersonality(visionResult.suggestedPersonality);
            }
            else if (bioResult.SuggestedPersonality.HasValue)
            {
                result.SuggestedPersonality = bioResult.SuggestedPersonality;
            }
            else
            {
                result.SuggestedPersonality = colorPersonality;
            }

            result.DialogueStyle = bioResult.DialogueStyle;
            result.ToneTags = bioResult.ToneTags;
            result.EventPreferences = bioResult.EventPreferences;

            // 5. 如果有 Vision 分析结果，合并关键词
            if (visionResult != null && visionResult.styleKeywords.Count > 0)
            {
                result.ToneTags = result.ToneTags.Union(visionResult.styleKeywords).ToList();
            }

            // 6. 根据人格调整参数
            AdjustByPersonality(result);

            return result;
        }

        /// <summary>
        /// 同步版本（兼容旧代码）
        /// </summary>
        public static PersonaAnalysisResult AnalyzePersonaDef(NarratorPersonaDef def)
        {
            var result = new PersonaAnalysisResult();

            // 1. 分析立绘颜色
            var colorPersonality = AnalyzePortraitColor(def.primaryColor);

            // 2. 分析简介文本
            var bioResult = AnalyzeBiography(def.biography);

            // 3. 整合结果（简介优先，颜色作为参考）
            result.SuggestedPersonality = def.overridePersonality != null && !string.IsNullOrEmpty(def.overridePersonality)
                ? ParsePersonality(def.overridePersonality)
                : bioResult.SuggestedPersonality 
                ?? colorPersonality;

            result.DialogueStyle = bioResult.DialogueStyle;
            result.ToneTags = bioResult.ToneTags;
            result.EventPreferences = bioResult.EventPreferences;

            // 4. 根据人格调整参数
            AdjustByPersonality(result);

            return result;
        }

        private static PersonalityTrait? ParsePersonality(string personality)
        {
            if (string.IsNullOrEmpty(personality)) return null;
            return Enum.TryParse<PersonalityTrait>(personality, true, out var result) ? result : null;
        }

        /// <summary>
        /// 根据人格特质微调参数
        /// </summary>
        private static void AdjustByPersonality(PersonaAnalysisResult result)
        {
            switch (result.SuggestedPersonality)
            {
                case PersonalityTrait.Benevolent:
                    result.EventPreferences.positiveEventBias = Math.Max(0.3f, result.EventPreferences.positiveEventBias);
                    result.DialogueStyle.emotionalExpression = Math.Max(0.6f, result.DialogueStyle.emotionalExpression);
                    break;

                case PersonalityTrait.Sadistic:
                    result.EventPreferences.negativeEventBias = Math.Max(0.3f, result.EventPreferences.negativeEventBias);
                    result.DialogueStyle.sarcasmLevel = Math.Max(0.4f, result.DialogueStyle.sarcasmLevel);
                    break;

                case PersonalityTrait.Chaotic:
                    result.EventPreferences.chaosLevel = Math.Max(0.7f, result.EventPreferences.chaosLevel);
                    result.DialogueStyle.humorLevel = Math.Max(0.5f, result.DialogueStyle.humorLevel);
                    break;

                case PersonalityTrait.Strategic:
                    result.DialogueStyle.formalityLevel = Math.Max(0.6f, result.DialogueStyle.formalityLevel);
                    result.DialogueStyle.verbosity = Math.Max(0.6f, result.DialogueStyle.verbosity);
                    break;

                case PersonalityTrait.Protective:
                    result.EventPreferences.positiveEventBias = Math.Max(0.2f, result.EventPreferences.positiveEventBias);
                    result.EventPreferences.interventionFrequency = Math.Max(0.7f, result.EventPreferences.interventionFrequency);
                    break;

                case PersonalityTrait.Manipulative:
                    result.DialogueStyle.sarcasmLevel = Math.Max(0.3f, result.DialogueStyle.sarcasmLevel);
                    result.EventPreferences.interventionFrequency = Math.Max(0.6f, result.EventPreferences.interventionFrequency);
                    break;
            }
        }
    }

    /// <summary>
    /// 人格分析结果
    /// </summary>
    public class PersonaAnalysisResult
    {
        public PersonalityTrait? SuggestedPersonality { get; set; }
        public DialogueStyleDef DialogueStyle { get; set; } = new DialogueStyleDef();
        public List<string> ToneTags { get; set; } = new List<string>();
        public EventPreferencesDef EventPreferences { get; set; } = new EventPreferencesDef();
    }
}
