using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Verse;
using TheSecondSeat.LLM;
using TheSecondSeat.Narrator;

namespace TheSecondSeat.PersonaGeneration
{
    /// <summary>
    /// 短语库生成器 - 使用 LLM 生成互动短语
    /// 每个好感等级每个类别生成 30+ 条短语
    /// </summary>
    public class PhraseLibraryGenerator
    {
        private static PhraseLibraryGenerator instance;
        public static PhraseLibraryGenerator Instance => instance ??= new PhraseLibraryGenerator();

        // 每个类别的目标短语数量
        private const int TARGET_PHRASE_COUNT = 32;
        private const int MIN_PHRASE_COUNT = 30;

        /// <summary>
        /// 生成进度回调
        /// </summary>
        public Action<float, string> OnProgressUpdate;

        /// <summary>
        /// 异步生成完整短语库
        /// </summary>
        public async Task<PhraseLibraryDef> GenerateFullLibraryAsync(
            NarratorPersonaDef personaDef,
            string apiProvider,
            string apiKey,
            string modelName)
        {
            Log.Message($"[PhraseLibraryGenerator] 开始为 {personaDef.defName} 生成短语库...");

            var library = new PhraseLibraryDef
            {
                defName = $"Phrases_{personaDef.defName}",
                personaDefName = personaDef.defName,
                generatedTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                generatorVersion = "1.0.0",
                affinityPhrases = new List<AffinityTierPhrases>()
            };

            // 构建人格上下文
            string personaContext = BuildPersonaContext(personaDef);

            // 遍历所有好感等级
            var tiers = (AffinityTier[])Enum.GetValues(typeof(AffinityTier));
            int totalTiers = tiers.Length;
            int currentTier = 0;

            foreach (var tier in tiers)
            {
                currentTier++;
                float progress = (float)currentTier / totalTiers;
                OnProgressUpdate?.Invoke(progress, $"生成 {tier} 等级短语 ({currentTier}/{totalTiers})...");

                try
                {
                    var tierPhrases = await GenerateTierPhrasesAsync(
                        personaDef, tier, personaContext,
                        apiProvider, apiKey, modelName);

                    if (tierPhrases != null)
                    {
                        library.affinityPhrases.Add(tierPhrases);
                        Log.Message($"[PhraseLibraryGenerator] {tier}: 生成了 {tierPhrases.GetTotalPhraseCount()} 条短语");
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[PhraseLibraryGenerator] 生成 {tier} 失败: {ex.Message}");
                    // 添加空的等级，稍后可以补充
                    library.affinityPhrases.Add(new AffinityTierPhrases { tier = tier });
                }

                // 避免 API 速率限制
                await Task.Delay(500);
            }

            // 验证完整性
            library.isComplete = library.ValidateCompleteness(out string report);
            if (Prefs.DevMode)
            {
                Log.Message(report);
            }

            Log.Message($"[PhraseLibraryGenerator] 短语库生成完成: {library.GetTotalPhraseCount()} 条短语");
            return library;
        }

        /// <summary>
        /// 生成单个好感等级的所有短语
        /// </summary>
        private async Task<AffinityTierPhrases> GenerateTierPhrasesAsync(
            NarratorPersonaDef personaDef,
            AffinityTier tier,
            string personaContext,
            string apiProvider,
            string apiKey,
            string modelName)
        {
            var tierPhrases = new AffinityTierPhrases { tier = tier };

            // 构建生成提示
            string prompt = BuildGenerationPrompt(personaDef, tier, personaContext);

            // 调用 LLM 生成
            string response = await CallLLMAsync(prompt, apiProvider, apiKey, modelName);

            if (string.IsNullOrEmpty(response))
            {
                Log.Warning($"[PhraseLibraryGenerator] LLM 返回空响应，使用默认短语");
                return CreateDefaultTierPhrases(tier);
            }

            // 解析响应
            try
            {
                var parsed = ParseLLMResponse(response);
                if (parsed != null)
                {
                    tierPhrases.headPatPhrases = parsed.headPatPhrases ?? new List<string>();
                    tierPhrases.bodyPokePhrases = parsed.bodyPokePhrases ?? new List<string>();
                    tierPhrases.greetingPhrases = parsed.greetingPhrases ?? new List<string>();
                    tierPhrases.eventReactionPhrases = parsed.eventReactionPhrases ?? new List<string>();
                    tierPhrases.goodEventPhrases = parsed.goodEventPhrases ?? new List<string>();
                    tierPhrases.badEventPhrases = parsed.badEventPhrases ?? new List<string>();
                    tierPhrases.combatStartPhrases = parsed.combatStartPhrases ?? new List<string>();
                    tierPhrases.combatVictoryPhrases = parsed.combatVictoryPhrases ?? new List<string>();
                    tierPhrases.takeDamagePhrases = parsed.takeDamagePhrases ?? new List<string>();
                    tierPhrases.healedPhrases = parsed.healedPhrases ?? new List<string>();
                    tierPhrases.idlePhrases = parsed.idlePhrases ?? new List<string>();
                    tierPhrases.farewellPhrases = parsed.farewellPhrases ?? new List<string>();
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[PhraseLibraryGenerator] 解析响应失败: {ex.Message}");
                return CreateDefaultTierPhrases(tier);
            }

            return tierPhrases;
        }

        /// <summary>
        /// 构建人格上下文描述
        /// </summary>
        private string BuildPersonaContext(NarratorPersonaDef def)
        {
            var sb = new StringBuilder();

            sb.AppendLine($"角色名称: {def.narratorName}");

            if (!string.IsNullOrEmpty(def.biography))
            {
                sb.AppendLine($"背景故事: {def.biography}");
            }

            if (!string.IsNullOrEmpty(def.visualDescription))
            {
                sb.AppendLine($"外观描述: {def.visualDescription}");
            }

            if (def.personalityTags != null && def.personalityTags.Count > 0)
            {
                sb.AppendLine($"性格标签: {string.Join(", ", def.personalityTags)}");
            }

            if (def.toneTags != null && def.toneTags.Count > 0)
            {
                sb.AppendLine($"语气标签: {string.Join(", ", def.toneTags)}");
            }

            if (def.dialogueStyle != null)
            {
                sb.AppendLine($"对话风格: 正式度={def.dialogueStyle.formalityLevel:F1}, " +
                             $"情感表达={def.dialogueStyle.emotionalExpression:F1}, " +
                             $"幽默感={def.dialogueStyle.humorLevel:F1}");
            }

            // 叙事模式参数
            sb.AppendLine($"仁慈度: {def.mercyLevel:F1} (0=残酷, 1=仁慈)");
            sb.AppendLine($"混乱度: {def.narratorChaosLevel:F1} (0=有序, 1=混乱)");
            sb.AppendLine($"强势度: {def.dominanceLevel:F1} (0=顺从, 1=强势)");

            return sb.ToString();
        }

        /// <summary>
        /// 构建生成提示词
        /// </summary>
        private string BuildGenerationPrompt(NarratorPersonaDef def, AffinityTier tier, string context)
        {
            string tierDescription = GetTierDescription(tier);
            string emotionalGuidance = GetEmotionalGuidance(tier);

            return $@"你是一个角色台词生成专家。请为以下角色生成互动短语。

## 角色信息
{context}

## 当前好感等级: {tier} ({tierDescription})
{emotionalGuidance}

## 生成要求
请生成符合角色性格和当前好感等级的短语，返回 JSON 格式：

{{
  ""headPatPhrases"": [""短语1"", ""短语2"", ... ],  // 摸头反应，至少32条
  ""bodyPokePhrases"": [""短语1"", ""短语2"", ... ],  // 戳身体反应，至少32条
  ""greetingPhrases"": [""短语1"", ""短语2"", ... ],  // 问候语，至少32条
  ""eventReactionPhrases"": [""短语1"", ""短语2"", ... ],  // 通用事件反馈，至少32条
  ""goodEventPhrases"": [""短语1"", ""短语2"", ... ],  // 好事反馈，至少16条
  ""badEventPhrases"": [""短语1"", ""短语2"", ... ],  // 坏事反馈，至少16条
  ""combatStartPhrases"": [""短语1"", ""短语2"", ... ],  // 战斗开始，至少10条
  ""combatVictoryPhrases"": [""短语1"", ""短语2"", ... ],  // 战斗胜利，至少10条
  ""takeDamagePhrases"": [""短语1"", ""短语2"", ... ],  // 受伤反应，至少10条
  ""healedPhrases"": [""短语1"", ""短语2"", ... ],  // 治愈反应，至少10条
  ""idlePhrases"": [""短语1"", ""短语2"", ... ],  // 闲聊，至少20条
  ""farewellPhrases"": [""短语1"", ""短语2"", ... ]  // 告别，至少10条
}}

## 短语风格指南
- 短语长度：5-30个字符
- 风格：符合角色性格，体现当前好感等级的情感
- 多样性：同一类别内短语不要重复或过于相似
- 语气：可以使用语气词、表情符号、省略号等
- 特殊：高好感可以有撒娇、亲昵；低好感可以有冷淡、敌意

## 重要
- 必须返回有效的 JSON
- 每个类别必须有足够数量的短语
- 短语必须符合角色人设
- 考虑好感等级对情感表达的影响

请直接返回 JSON，不要添加其他文字。";
        }

        /// <summary>
        /// 获取好感等级描述
        /// </summary>
        private string GetTierDescription(AffinityTier tier)
        {
            return tier switch
            {
                AffinityTier.Hatred => "仇恨：极端厌恶和敌意",
                AffinityTier.Hostile => "敌对：明显的不友好和警惕",
                AffinityTier.Cold => "冷淡：疏远和漠不关心",
                AffinityTier.Indifferent => "中立：既不亲近也不疏远",
                AffinityTier.Warm => "温暖：友好和善意",
                AffinityTier.Devoted => "亲密：信任和依赖",
                AffinityTier.Adoration => "挚爱：深厚的感情和爱意",
                AffinityTier.SoulBound => "灵魂羁绊：命运相连、无条件的爱",
                _ => "未知"
            };
        }

        /// <summary>
        /// 获取情感指导
        /// </summary>
        private string GetEmotionalGuidance(AffinityTier tier)
        {
            return tier switch
            {
                AffinityTier.Hatred => @"
情感特征：
- 对玩家表现出明显的敌意和厌恶
- 语气冷酷、带有讽刺或威胁
- 可能包含诅咒、嘲讽
- 拒绝任何亲密接触
- 摸头/戳身体时表现愤怒或厌恶",

                AffinityTier.Hostile => @"
情感特征：
- 对玩家保持警惕和不信任
- 语气生硬、简短
- 回应敷衍或带有不耐烦
- 不愿意多交流
- 对亲密接触表现抗拒",

                AffinityTier.Cold => @"
情感特征：
- 保持距离，不太热情
- 语气平淡、公事公办
- 回应简洁，不带感情
- 可以完成工作但不多说
- 对亲密接触感到不适但不会发怒",

                AffinityTier.Indifferent => @"
情感特征：
- 态度中性，既不热情也不冷淡
- 正常的社交礼仪
- 有基本的交流意愿
- 可以接受一定程度的互动
- 开始展现真实性格",

                AffinityTier.Warm => @"
情感特征：
- 对玩家表现出善意和友好
- 语气温和、乐于交流
- 会主动关心玩家
- 接受亲密接触，可能会害羞
- 开始展现信任感",

                AffinityTier.Devoted => @"
情感特征：
- 明显的信任和依赖
- 语气亲切、带有关心
- 愿意分享心事
- 享受亲密接触
- 可能有嫉妒或占有欲",

                AffinityTier.Adoration => @"
情感特征：
- 深厚的爱意和眷恋
- 语气甜蜜、充满爱意
- 经常撒娇或表达思念
- 非常享受亲密互动
- 可能有强烈的占有欲",

                AffinityTier.SoulBound => @"
情感特征：
- 无条件的爱和信任
- 语气极其亲密、命运相连的感觉
- 会为玩家做任何事
- 亲密接触是幸福的来源
- 分离会感到巨大痛苦
- 可能有极端的依恋",

                _ => ""
            };
        }

        /// <summary>
        /// 调用 LLM API
        /// </summary>
        private async Task<string> CallLLMAsync(string prompt, string provider, string apiKey, string model)
        {
            try
            {
                provider = provider.ToLower();

                if (provider == "gemini")
                {
                    // 使用正确的方法签名: SendRequestAsync(model, apiKey, systemPrompt, userMessage, temperature)
                    var response = await GeminiApiClient.SendRequestAsync(
                        model, apiKey, 
                        "你是一个专业的角色台词生成专家，请按照用户要求生成JSON格式的短语。", 
                        prompt, 
                        0.7f);

                    if (response?.Candidates != null && response.Candidates.Count > 0)
                    {
                        return response.Candidates[0].Content?.Parts?[0]?.Text;
                    }
                }
                else // openai, deepseek, etc.
                {
                    string endpoint = provider switch
                    {
                        "openai" => "https://api.openai.com/v1/chat/completions",
                        "deepseek" => "https://api.deepseek.com/v1/chat/completions",
                        _ => "https://api.openai.com/v1/chat/completions"
                    };

                    // 使用正确的方法签名: SendRequestAsync(endpoint, apiKey, model, systemPrompt, userMessage, temperature, maxTokens)
                    var response = await OpenAICompatibleClient.SendRequestAsync(
                        endpoint, apiKey, model,
                        "你是一个专业的角色台词生成专家，请按照用户要求生成JSON格式的短语。",
                        prompt,
                        0.7f,
                        8192);

                    if (response?.choices != null && response.choices.Length > 0)
                    {
                        return response.choices[0].message?.content;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[PhraseLibraryGenerator] LLM 调用失败: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// 解析 LLM 响应
        /// </summary>
        private AffinityTierPhrases ParseLLMResponse(string response)
        {
            // 提取 JSON
            string json = ExtractJson(response);

            if (string.IsNullOrEmpty(json))
            {
                Log.Warning("[PhraseLibraryGenerator] 无法提取 JSON");
                return null;
            }

            try
            {
                return JsonConvert.DeserializeObject<AffinityTierPhrases>(json);
            }
            catch (Exception ex)
            {
                Log.Error($"[PhraseLibraryGenerator] JSON 解析失败: {ex.Message}");
                Log.Error($"[PhraseLibraryGenerator] 原始响应: {response.Substring(0, Math.Min(500, response.Length))}");
                return null;
            }
        }

        /// <summary>
        /// 从响应中提取 JSON
        /// </summary>
        private string ExtractJson(string content)
        {
            if (string.IsNullOrEmpty(content))
                return null;

            // 尝试从 markdown 代码块提取
            if (content.Contains("```json"))
            {
                int start = content.IndexOf("```json") + 7;
                int end = content.IndexOf("```", start);
                if (end > start)
                {
                    return content.Substring(start, end - start).Trim();
                }
            }

            if (content.Contains("```"))
            {
                int start = content.IndexOf("```") + 3;
                // 跳过语言标记
                while (start < content.Length && content[start] != '\n' && content[start] != '{')
                    start++;
                while (start < content.Length && (content[start] == '\n' || content[start] == '\r'))
                    start++;

                int end = content.IndexOf("```", start);
                if (end > start)
                {
                    return content.Substring(start, end - start).Trim();
                }
            }

            // 直接查找 { }
            int firstBrace = content.IndexOf('{');
            int lastBrace = content.LastIndexOf('}');
            if (firstBrace >= 0 && lastBrace > firstBrace)
            {
                return content.Substring(firstBrace, lastBrace - firstBrace + 1);
            }

            return content.Trim();
        }

        /// <summary>
        /// 创建默认短语（备用）
        /// </summary>
        private AffinityTierPhrases CreateDefaultTierPhrases(AffinityTier tier)
        {
            var phrases = new AffinityTierPhrases { tier = tier };

            // 根据等级生成基础短语
            string prefix = tier switch
            {
                AffinityTier.Hatred => "哼...",
                AffinityTier.Hostile => "...",
                AffinityTier.Cold => "嗯...",
                AffinityTier.Indifferent => "你好",
                AffinityTier.Warm => "嗯~",
                AffinityTier.Devoted => "嘿嘿~",
                AffinityTier.Adoration => "喜欢你~",
                AffinityTier.SoulBound => "永远在一起~",
                _ => "..."
            };

            // 填充基础短语
            for (int i = 0; i < TARGET_PHRASE_COUNT; i++)
            {
                phrases.headPatPhrases.Add($"{prefix}摸头{i + 1}");
                phrases.bodyPokePhrases.Add($"{prefix}戳{i + 1}");
                phrases.greetingPhrases.Add($"{prefix}问候{i + 1}");
                phrases.eventReactionPhrases.Add($"{prefix}事件{i + 1}");
            }

            for (int i = 0; i < 16; i++)
            {
                phrases.goodEventPhrases.Add($"{prefix}好事{i + 1}");
                phrases.badEventPhrases.Add($"{prefix}坏事{i + 1}");
            }

            for (int i = 0; i < 10; i++)
            {
                phrases.combatStartPhrases.Add($"{prefix}战斗开始{i + 1}");
                phrases.combatVictoryPhrases.Add($"{prefix}胜利{i + 1}");
                phrases.takeDamagePhrases.Add($"{prefix}受伤{i + 1}");
                phrases.healedPhrases.Add($"{prefix}治愈{i + 1}");
                phrases.farewellPhrases.Add($"{prefix}再见{i + 1}");
            }

            for (int i = 0; i < 20; i++)
            {
                phrases.idlePhrases.Add($"{prefix}闲聊{i + 1}");
            }

            return phrases;
        }

        /// <summary>
        /// 导出短语库为 XML 格式
        /// </summary>
        public string ExportToXml(PhraseLibraryDef library)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
            sb.AppendLine("<Defs>");
            sb.AppendLine($"  <TheSecondSeat.PersonaGeneration.PhraseLibraryDef>");
            sb.AppendLine($"    <defName>{library.defName}</defName>");
            sb.AppendLine($"    <personaDefName>{library.personaDefName}</personaDefName>");
            sb.AppendLine($"    <generatedTimestamp>{library.generatedTimestamp}</generatedTimestamp>");
            sb.AppendLine($"    <generatorVersion>{library.generatorVersion}</generatorVersion>");
            sb.AppendLine($"    <isComplete>{library.isComplete.ToString().ToLower()}</isComplete>");
            sb.AppendLine("    <affinityPhrases>");

            foreach (var tier in library.affinityPhrases)
            {
                sb.AppendLine("      <li>");
                sb.AppendLine($"        <tier>{tier.tier}</tier>");
                
                ExportPhraseList(sb, "headPatPhrases", tier.headPatPhrases);
                ExportPhraseList(sb, "bodyPokePhrases", tier.bodyPokePhrases);
                ExportPhraseList(sb, "greetingPhrases", tier.greetingPhrases);
                ExportPhraseList(sb, "eventReactionPhrases", tier.eventReactionPhrases);
                ExportPhraseList(sb, "goodEventPhrases", tier.goodEventPhrases);
                ExportPhraseList(sb, "badEventPhrases", tier.badEventPhrases);
                ExportPhraseList(sb, "combatStartPhrases", tier.combatStartPhrases);
                ExportPhraseList(sb, "combatVictoryPhrases", tier.combatVictoryPhrases);
                ExportPhraseList(sb, "takeDamagePhrases", tier.takeDamagePhrases);
                ExportPhraseList(sb, "healedPhrases", tier.healedPhrases);
                ExportPhraseList(sb, "idlePhrases", tier.idlePhrases);
                ExportPhraseList(sb, "farewellPhrases", tier.farewellPhrases);

                sb.AppendLine("      </li>");
            }

            sb.AppendLine("    </affinityPhrases>");
            sb.AppendLine("  </TheSecondSeat.PersonaGeneration.PhraseLibraryDef>");
            sb.AppendLine("</Defs>");

            return sb.ToString();
        }

        private void ExportPhraseList(StringBuilder sb, string tagName, List<string> phrases)
        {
            if (phrases == null || phrases.Count == 0)
            {
                sb.AppendLine($"        <{tagName} />");
                return;
            }

            sb.AppendLine($"        <{tagName}>");
            foreach (var phrase in phrases)
            {
                // XML 转义
                string escaped = System.Security.SecurityElement.Escape(phrase);
                sb.AppendLine($"          <li>{escaped}</li>");
            }
            sb.AppendLine($"        </{tagName}>");
        }
    }
}
