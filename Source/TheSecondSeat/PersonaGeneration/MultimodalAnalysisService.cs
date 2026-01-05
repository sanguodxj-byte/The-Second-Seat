using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using Verse;
using TheSecondSeat.LLM;

namespace TheSecondSeat.PersonaGeneration
{
    /// <summary>
    /// 多模态分析服务 - 使用 Vision API 分析图像
    /// </summary>
    public class MultimodalAnalysisService
    {
        private static MultimodalAnalysisService? instance;
        public static MultimodalAnalysisService Instance => instance ??= new MultimodalAnalysisService();

        private string apiProvider = "openai"; // "openai", "deepseek", "gemini"
        private string apiKey = "";
        private string visionModel = "gpt-4-vision-preview";
        private string textModel = "gpt-4";

        public MultimodalAnalysisService()
        {
        }

        /// <summary>
        /// 配置多模态分析服务
        /// ? 添加日志输出确认配置
        /// </summary>
        public void Configure(string provider, string key, string visionModelName = "", string textModelName = "")
        {
            apiProvider = provider.ToLower();
            apiKey = key;

            // 设置默认模型
            if (!string.IsNullOrEmpty(visionModelName))
                visionModel = visionModelName;
            else
            {
                visionModel = apiProvider switch
                {
                    "openai" => "gpt-4-vision-preview",
                    "deepseek" => "deepseek-vl",
                    "gemini" => "gemini-1.5-flash",
                    _ => "gpt-4-vision-preview"
                };
            }

            if (!string.IsNullOrEmpty(textModelName))
                textModel = textModelName;
            else
            {
                textModel = apiProvider switch
                {
                    "openai" => "gpt-4",
                    "deepseek" => "deepseek-chat",
                    "gemini" => "gemini-1.5-flash",
                    _ => "gpt-4"
                };
            }
            
            // ? 新增：日志输出确认配置
            Log.Message($"[MultimodalAnalysis] 配置完成: provider={apiProvider}, vision={visionModel}, text={textModel}");
        }

        /// <summary>
        /// 从 Unity Texture2D 分析图像
        /// </summary>
        public async Task<VisionAnalysisResult?> AnalyzeTextureAsync(Texture2D texture)
        {
            if (texture == null)
            {
                Log.Error("[MultimodalAnalysis] Texture is null");
                return null;
            }

            try
            {
                string provider = apiProvider.ToLower();
                
                if (provider == "gemini")
                {
                    // ? Gemini Vision API
                    return await AnalyzeWithGeminiAsync(texture);
                }
                else if (provider == "openai" || provider == "deepseek")
                {
                    // ? OpenAI/DeepSeek Vision API
                    return await AnalyzeWithOpenAICompatibleAsync(texture);
                }
                else
                {
                    Log.Error($"[MultimodalAnalysis] 不支持的 API 提供商: {provider}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[MultimodalAnalysis] Error analyzing texture: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 使用 Gemini API 分析图片
        /// </summary>
        private async Task<VisionAnalysisResult?> AnalyzeWithGeminiAsync(Texture2D texture)
        {
            Log.Message("[MultimodalAnalysis] 使用 Gemini Vision API");
            
            string prompt = GetVisionPrompt();

            var geminiResponse = await LLM.GeminiApiClient.SendVisionRequestAsync(
                visionModel,
                apiKey,
                prompt,
                texture,
                0.3f,
                8192  // ? 从4096增加到8192，确保完整JSON返回
            );
            
            if (geminiResponse == null || geminiResponse.Candidates == null || geminiResponse.Candidates.Count == 0)
            {
                Log.Error("[MultimodalAnalysis] Gemini Vision 返回空响应");
                return null;
            }
            
            string content = geminiResponse.Candidates[0].Content.Parts[0].Text;
            return ParseVisionJson(content);
        }

        /// <summary>
        /// 使用 OpenAI 兼容 API 分析图片
        /// ? 传递 provider 参数以支持 DeepSeek 特殊格式
        /// </summary>
        private async Task<VisionAnalysisResult?> AnalyzeWithOpenAICompatibleAsync(Texture2D texture)
        {
            Log.Message($"[MultimodalAnalysis] 使用 {apiProvider} Vision API");
            
            string endpoint = GetVisionEndpoint();
            string prompt = GetVisionPrompt();

            var response = await LLM.OpenAICompatibleClient.SendVisionRequestAsync(
                endpoint,
                apiKey,
                visionModel,
                prompt,
                texture,
                0.3f,
                4096,  // max_tokens
                apiProvider  // ? 新增：传递 provider 参数
            );
            
            if (response == null || response.choices == null || response.choices.Length == 0)
            {
                Log.Error($"[MultimodalAnalysis] {apiProvider} Vision 返回空响应");
                return null;
            }
            
            string content = response.choices[0].message?.content;
            if (string.IsNullOrEmpty(content))
            {
                Log.Error("[MultimodalAnalysis] Vision 响应内容为空");
                return null;
            }
            
            return ParseVisionJson(content);
        }

        /// <summary>
        /// Get Vision Analysis Prompt (Generic English Version)
        /// </summary>
        private string GetVisionPrompt()
        {
            return @"Analyze this character portrait in detail and provide a comprehensive JSON response.

**CRITICAL: The characterDescription field MUST be written in English!**

{
  ""dominantColors"": [
    {""hex"": ""#RRGGBB"", ""percentage"": 0-100, ""name"": ""color name in English""}
  ],
  ""visualElements"": [""element1"", ""element2"", ""element3""],
  ""characterDescription"": ""Detailed 300-500 word appearance description and personality inference in English"",
  ""mood"": ""overall mood/atmosphere in English"",
  ""suggestedPersonality"": ""Benevolent/Sadistic/Chaotic/Strategic/Protective/Manipulative"",
  ""styleKeywords"": [""keyword1"", ""keyword2"", ""keyword3""]
}

**CRITICAL REQUIREMENTS for characterDescription (MUST be in English!):**

**Part 1: Detailed Appearance Description (40%)**

Describe all visible details:
- **Race**: Human? Elf? Dragon-kin? Orc? Android?
- **Hair**: Color, length, style, texture (e.g., ""Silky silver hair cascading down like a waterfall, tied with a crimson ribbon"")
- **Eyes**: Color, shape, expression (e.g., ""Crimson vertical slit pupils, revealing wisdom and danger"")
- **Facial Features**: Expression, age, scars, markings
- **Body**: Build, posture, stance
- **Clothing & Armor**:
  * Main attire
  * Armor pieces
  * Accessories
  * Material and condition
- **Special Features**: Wings, tails, horns, weapons, magical effects
- **Overall Impression**: Mood conveyed by posture, lighting, composition

**Part 2: Personality Inference from Appearance (40%)**

Infer traits from visual cues:

**From Expression & Body Language**:
- Stern face → Reserved, disciplined, self-controlled
- Confident stance → Decisive, experienced, leadership qualities
- Guarded posture → Cautious, defensive, possible past trauma
- Relaxed expression → Approachable, friendly, trusting

**From Clothing & Armor**:
- Heavy armor → Values protection, combat-ready, disciplined
- Dark colors → Mysterious, serious, introverted or secretive
- Intricate designs → Detail-oriented, perhaps vain or status-conscious
- Simple utilitarian gear → Pragmatic, values function over form

**From Weapons & Equipment**:
- Obvious weapons → Conflict-ready, decisive, potentially aggressive
- Concealed weapons → Strategic, cautious, prefers surprise
- Magical artifacts → Knowledgeable, academic, connected to ancient wisdom
- No weapons → Peaceful, trusting, or relies on other advantages

**Part 3: Dialogue & Behavior Prediction (20%)**

Predict based on visual analysis:

**Speaking Style**:
- ""She might speak with a [calm/passionate/stern/gentle] tone""
- ""Her expression suggests [formal/casual/professional/poetic] language""
- ""She likely communicates with [concise commands/rich descriptions/military jargon]""

**Emotional Expression**:
- ""Rarely shows strong emotion publicly"" or ""Wears heart on sleeve""
- ""Carefully controlled reactions"" or ""Impulsive responses""

**Interaction Style**:
- ""Keeps distance from strangers"" or ""Immediately warm and friendly""
- ""Observes before speaking"" or ""Initiates conversation""

**REMEMBER**:
- characterDescription MUST be in English!
- Be specific: Don't just say ""armor"" - describe material, condition, design
- Infer personality from every detail
- Predict behavior: How would they speak? React? Interact?
- 300-500 words
- Return ONLY valid JSON

Focus on:
- Top 3-4 dominant colors with accurate percentages
- All visual elements visible in the portrait
- Detailed appearance analysis (IN ENGLISH!)
- Personality inference from visual cues (IN ENGLISH!)
- Behavioral predictions (IN ENGLISH!)
- Style keywords for System Prompt (in English)";
        }

        /// <summary>
        /// 解析 Vision API 返回的 JSON
        /// ? 修复：完整输出JSON内容用于诊断
        /// </summary>
        private VisionAnalysisResult? ParseVisionJson(string jsonContent)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(jsonContent))
                {
                    Log.Error("[MultimodalAnalysis] Vision 响应内容为空");
                    return null;
                }

                // ? 先输出原始响应（前500字符）
                Log.Message($"[MultimodalAnalysis] 原始响应: {jsonContent.Substring(0, Math.Min(500, jsonContent.Length))}...");

                // 提取 JSON（有时 AI 会在 markdown 代码块中返回）
                string extractedJson = ExtractJsonFromMarkdown(jsonContent);
                
                // ? 输出提取后的JSON（前500字符）
                if (extractedJson != jsonContent)
                {
                    Log.Message($"[MultimodalAnalysis] 提取后的 JSON: {extractedJson.Substring(0, Math.Min(500, extractedJson.Length))}...");
                }

                // ? 验证 JSON 是否以 { 开头
                extractedJson = extractedJson.Trim();
                if (!extractedJson.StartsWith("{"))
                {
                    Log.Error($"[MultimodalAnalysis] JSON 格式错误，不以 {{ 开头。前 50 字符: {extractedJson.Substring(0, Math.Min(50, extractedJson.Length))}");
                    return null;
                }

                // ? 尝试解析JSON
                var result = JsonConvert.DeserializeObject<VisionAnalysisResult>(extractedJson);
                
                if (result == null)
                {
                    Log.Error("[MultimodalAnalysis] JSON 反序列化返回 null");
                    // ? 输出完整JSON用于诊断
                    Log.Error($"[MultimodalAnalysis] 完整JSON内容:\n{extractedJson}");
                    return null;
                }

                Log.Message($"[MultimodalAnalysis] 成功解析 Vision 结果: {result.dominantColors?.Count ?? 0} 个颜色");
                return result;
            }
            catch (JsonException jsonEx)
            {
                Log.Error($"[MultimodalAnalysis] JSON 解析错误: {jsonEx.Message}");
                // ? 输出完整响应内容
                Log.Error($"[MultimodalAnalysis] 完整响应内容:\n{jsonContent}");
                return null;
            }
            catch (Exception ex)
            {
                Log.Error($"[MultimodalAnalysis] Vision 响应解析失败: {ex.Message}");
                Log.Error($"[MultimodalAnalysis] 响应内容: {jsonContent}");
                return null;
            }
        }

        /// <summary>
        /// 从 markdown 代码块中提取 JSON
        /// ? 改进提取逻辑，确保提取完整JSON
        /// </summary>
        private string ExtractJsonFromMarkdown(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return content;
            }

            // 尝试 1: 提取 ```json ... ``` 代码块
            if (content.Contains("```json"))
            {
                int startIndex = content.IndexOf("```json") + 7;
                
                // 跳过换行符
                while (startIndex < content.Length && (content[startIndex] == '\n' || content[startIndex] == '\r'))
                {
                    startIndex++;
                }
                
                int endIndex = content.IndexOf("```", startIndex);
                if (endIndex > startIndex)
                {
                    string extracted = content.Substring(startIndex, endIndex - startIndex).Trim();
                    Log.Message("[MultimodalAnalysis] 从 ```json 代码块中提取 JSON");
                    return extracted;
                }
            }
            
            // 尝试 2: 提取 ``` ... ``` 代码块（无 json 标记）
            if (content.Contains("```"))
            {
                int startIndex = content.IndexOf("```") + 3;
                
                // 跳过可能的语言标记（如 "json\n"）
                while (startIndex < content.Length && content[startIndex] != '\n' && content[startIndex] != '\r' && content[startIndex] != '{')
                {
                    startIndex++;
                }
                
                // 跳过换行符
                while (startIndex < content.Length && (content[startIndex] == '\n' || content[startIndex] == '\r'))
                {
                    startIndex++;
                }
                
                int endIndex = content.IndexOf("```", startIndex);
                if (endIndex > startIndex)
                {
                    string extracted = content.Substring(startIndex, endIndex - startIndex).Trim();
                    Log.Message("[MultimodalAnalysis] 从 ``` 代码块中提取 JSON");
                    return extracted;
                }
            }
            
            // 尝试 3: 查找第一个 { 和最后一个 }
            int firstBrace = content.IndexOf('{');
            int lastBrace = content.LastIndexOf('}');
            if (firstBrace >= 0 && lastBrace > firstBrace)
            {
                string extracted = content.Substring(firstBrace, lastBrace - firstBrace + 1).Trim();
                if (extracted != content.Trim())
                {
                    Log.Message("[MultimodalAnalysis] 通过查找 { } 提取 JSON");
                    return extracted;
                }
            }
            
            // 无法提取，返回原始内容
            return content.Trim();
        }

        /// <summary>
        /// 分析 Base64 编码的图像
        /// </summary>
        public async Task<VisionAnalysisResult?> AnalyzeImageBase64Async(string base64Image)
        {
            try
            {
                string endpoint = GetVisionEndpoint();
                var requestBody = BuildVisionRequest(base64Image);

                var jsonContent = JsonConvert.SerializeObject(requestBody);
                
                // 使用 OpenAICompatibleClient
                var response = await OpenAICompatibleClient.SendOpenAIRawRequestAsync(endpoint, apiKey, jsonContent);

                if (response == null)
                {
                    Log.Error($"[MultimodalAnalysis] Vision API returned null response");
                    return null;
                }

                return ParseVisionResponse(response);
            }
            catch (Exception ex)
            {
                Log.Error($"[MultimodalAnalysis] Error: {ex.Message}\n{ex.StackTrace}");
                return null;
            }
        }

        /// <summary>
        /// 深度分析文本（使用 GPT-4）
        /// </summary>
        public async Task<TextAnalysisResult?> AnalyzeTextDeepAsync(string text)
        {
            try
            {
                string endpoint = GetTextEndpoint();
                var requestBody = BuildTextRequest(text);

                var jsonContent = JsonConvert.SerializeObject(requestBody);
                
                // 使用 OpenAICompatibleClient
                var response = await OpenAICompatibleClient.SendOpenAIRawRequestAsync(endpoint, apiKey, jsonContent);

                if (response == null)
                {
                    Log.Error($"[MultimodalAnalysis] Text API returned null response");
                    return null;
                }

                return ParseTextResponse(response);
            }
            catch (Exception ex)
            {
                Log.Error($"[MultimodalAnalysis] Error: {ex.Message}\n{ex.StackTrace}");
                return null;
            }
        }

        // ========== Private Helper Methods ==========

        private string GetVisionEndpoint()
        {
            return apiProvider switch
            {
                "openai" => "https://api.openai.com/v1/chat/completions",
                "deepseek" => "https://api.deepseek.com/v1/chat/completions",
                "gemini" => $"https://generativelanguage.googleapis.com/v1beta/models/{visionModel}:generateContent",
                _ => "https://api.openai.com/v1/chat/completions"
            };
        }

        private string GetTextEndpoint()
        {
            return apiProvider switch
            {
                "openai" => "https://api.openai.com/v1/chat/completions",
                "deepseek" => "https://api.deepseek.com/v1/chat/completions",
                "gemini" => $"https://generativelanguage.googleapis.com/v1beta/models/{textModel}:generateContent",
                _ => "https://api.openai.com/v1/chat/completions"
            };
        }

        private object BuildVisionRequest(string base64Image)
        {
            string prompt = @"Analyze this character portrait and provide a JSON response (no extra text):
{
  ""dominantColors"": [
    {""hex"": ""#RRGGBB"", ""percentage"": 0-100, ""name"": ""color name""}
  ],
  ""visualElements"": [""element1"", ""element2""],
  ""characterDescription"": ""Brief description (max 200 chars)"",
  ""mood"": ""overall mood"",
  ""suggestedPersonality"": ""Benevolent/Sadistic/Chaotic/Strategic/Protective/Manipulative"",
  ""styleKeywords"": [""keyword1"", ""keyword2"", ""keyword3""],
  ""personalityTags"": [""Tag1"", ""Tag2"", ""Tag3"", ...]
}

Focus on:
- Top 3-4 dominant colors with percentages
- Key visual elements (armor, weapons, creatures)
- Brief character appearance
- Overall mood/atmosphere
- Personality suggestion based on visual cues

Keep characterDescription under 200 characters. Return ONLY valid JSON.";

            if (apiProvider == "openai")
            {
                return new
                {
                    model = visionModel,
                    messages = new[]
                    {
                        new
                        {
                            role = "user",
                            content = new object[]
                            {
                                new { type = "text", text = prompt },
                                new
                                {
                                    type = "image_url",
                                    image_url = new
                                    {
                                        url = $"data:image/png;base64,{base64Image}"
                                    }
                                }
                            }
                        }
                    },
                    max_tokens = 1000,
                    temperature = 0.3f
                };
            }
            else
            {
                // DeepSeek / Gemini 类似结构
                return new
                {
                    model = visionModel,
                    messages = new[]
                    {
                        new { role = "user", content = prompt },
                        new { role = "user", content = $"Image: data:image/png;base64,{base64Image}" }
                    },
                    max_tokens = 1000
                };
            }
        }

        private object BuildTextRequest(string text)
        {
            string prompt = $@"Analyze this character biography and provide deep personality insights in JSON format:
{{
  ""personality_traits"": [""trait1"", ""trait2"", ...],
  ""dialogue_style"": {{
    ""formality"": 0.0-1.0,
    ""emotional_expression"": 0.0-1.0,
    ""verbosity"": 0.0-1.0,
    ""humor"": 0.0-1.0,
    ""sarcasm"": 0.0-1.0
  }},
  ""tone_tags"": [""tag1"", ""tag2"", ...],
  ""event_preferences"": {{
    ""positive_bias"": -1.0 to 1.0,
    ""negative_bias"": -1.0 to 1.0,
    ""chaos_level"": 0.0-1.0,
    ""intervention_frequency"": 0.0-1.0
  }},
  ""forbidden_words"": [""word1"", ""word2"", ...]
}}

Biography:
{text}";
            return new
            {
                model = textModel,
                messages = new[]
                {
                    new
                    {
                        role = "system",
                        content = "You are an expert in character personality analysis and narrative design."
                    },
                    new { role = "user", content = prompt }
                },
                max_tokens = 800,
                temperature = 0.5f,
                response_format = new { type = "json_object" }
            };
        }

        private VisionAnalysisResult? ParseVisionResponse(OpenAIResponse response)
        {
            try
            {
                var content = response?.choices?[0]?.message?.content;

                if (string.IsNullOrEmpty(content))
                {
                    Log.Error("[MultimodalAnalysis] Empty vision response");
                    return null;
                }

                // 尝试解析 JSON 内容
                var result = JsonConvert.DeserializeObject<VisionAnalysisResult>(content);
                return result;
            }
            catch (Exception ex)
            {
                Log.Error($"[MultimodalAnalysis] Error parsing vision response: {ex.Message}");
                return null;
            }
        }

        private TextAnalysisResult? ParseTextResponse(OpenAIResponse response)
        {
            try
            {
                var content = response?.choices?[0]?.message?.content;

                if (string.IsNullOrEmpty(content))
                {
                    Log.Error("[MultimodalAnalysis] Empty text response");
                    return null;
                }

                var result = JsonConvert.DeserializeObject<TextAnalysisResult>(content);
                return result;
            }
            catch (Exception ex)
            {
                Log.Error($"[MultimodalAnalysis] Error parsing text response: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// 📌 v1.6.62: 分析人格图片（支持特质和用户补充）
        /// </summary>
        /// <param name="texture">立绘纹理</param>
        /// <param name="personaName">人格名称</param>
        /// <param name="selectedTraits">用户选择的特质</param>
        /// <param name="userSupplement">用户补充描述</param>
        /// <returns>人格分析结果（包含个性标签）</returns>
        public PersonaAnalysisResult AnalyzePersonaImageWithTraits(
            Texture2D texture,
            string personaName,
            List<string> selectedTraits,
            string userSupplement)
        {
            try
            {
                // 1. 调用异步方法进行多模态分析
                var visionTask = AnalyzeTextureWithTraitsAsync(texture, selectedTraits, userSupplement);
                visionTask.Wait();  // 同步等待（RimWorld 主线程）
                
                var visionResult = visionTask.Result;
                
                if (visionResult == null)
                {
                    Log.Warning($"[MultimodalAnalysis] Vision 分析失败，返回默认结果");
                    return CreateDefaultAnalysisResult(userSupplement);
                }
                
                // 2. 构建 PersonaAnalysisResult
                var result = new PersonaAnalysisResult
                {
                    VisualTags = visionResult.visualElements ?? new List<string>(),
                    ToneTags = visionResult.styleKeywords ?? new List<string>(),
                    ConfidenceScore = 0.9f  // 因为有用户输入，置信度更高
                };
                
                // 3. 📌 提取个性标签（来自AI分析）
                if (visionResult.personalityTags != null && visionResult.personalityTags.Count > 0)
                {
                    result.PersonalityTags = visionResult.personalityTags;
                }
                else
                {
                    // 如果AI没有返回，至少使用用户选择的特质
                    result.PersonalityTags = selectedTraits.ToList();
                }
                
                // 4. 解析人格类型
                if (!string.IsNullOrEmpty(visionResult.suggestedPersonality))
                {
                    if (Enum.TryParse<Storyteller.PersonalityTrait>(visionResult.suggestedPersonality, true, out var trait))
                    {
                        result.SuggestedPersonality = trait;
                    }
                }
                
                // 5. 📌 生成增强版 biography（结合用户输入和AI分析）
                result.GeneratedBiography = visionResult.characterDescription;
                result.VisualDescription = visionResult.characterDescription;
                
                // 6. 📌 生成对话风格（基于用户描述 + 图片分析）
                result.SuggestedDialogueStyle = GenerateDialogueStyleFromAnalysis(visionResult, userSupplement);
                
                if (Prefs.DevMode)
                {
                    Log.Message($"[MultimodalAnalysis] AnalyzePersonaImageWithTraits 完成:");
                    Log.Message($"  - Visual Tags: {result.VisualTags.Count}");
                    Log.Message($"  - Tone Tags: {result.ToneTags.Count}");
                    Log.Message($"  - Personality Tags: {result.PersonalityTags.Count}");
                    Log.Message($"  - Personality: {result.SuggestedPersonality}");
                }
                
                return result;
            }
            catch (Exception ex)
            {
                Log.Error($"[MultimodalAnalysis] AnalyzePersonaImageWithTraits 失败: {ex.Message}");
                return CreateDefaultAnalysisResult(userSupplement);
            }
        }
        
        /// <summary>
        /// 📌 v1.6.62: 增强版 AnalyzeTextureAsync（支持特质和用户补充）
        /// </summary>
        private async Task<VisionAnalysisResult?> AnalyzeTextureWithTraitsAsync(
            Texture2D texture,
            List<string> selectedTraits,
            string userSupplement)
        {
            if (texture == null)
            {
                Log.Error("[MultimodalAnalysis] Texture is null");
                return null;
            }

            try
            {
                string provider = apiProvider.ToLower();
                
                if (provider == "gemini")
                {
                    return await AnalyzeWithGeminiTraitsAsync(texture, selectedTraits, userSupplement);
                }
                else if (provider == "openai" || provider == "deepseek")
                {
                    return await AnalyzeWithOpenAICompatibleTraitsAsync(texture, selectedTraits, userSupplement);
                }
                else
                {
                    Log.Error($"[MultimodalAnalysis] 不支持的 API 提供商: {provider}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[MultimodalAnalysis] Error analyzing texture with traits: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// 📌 v1.6.62: Gemini 分析（支持特质）
        /// </summary>
        private async Task<VisionAnalysisResult?> AnalyzeWithGeminiTraitsAsync(
            Texture2D texture,
            List<string> selectedTraits,
            string userSupplement)
        {
            Log.Message("[MultimodalAnalysis] 使用 Gemini Vision API (with traits)");
            
            string prompt = GetVisionPromptWithTraits(selectedTraits, userSupplement);

            var geminiResponse = await LLM.GeminiApiClient.SendVisionRequestAsync(
                visionModel,
                apiKey,
                prompt,
                texture,
                0.3f,
                8192
            );
            
            if (geminiResponse == null || geminiResponse.Candidates == null || geminiResponse.Candidates.Count == 0)
            {
                Log.Error("[MultimodalAnalysis] Gemini Vision 返回空响应");
                return null;
            }
            
            string content = geminiResponse.Candidates[0].Content.Parts[0].Text;
            return ParseVisionJson(content);
        }
        
        /// <summary>
        /// 📌 v1.6.62: OpenAI 兼容分析（支持特质）
        /// </summary>
        private async Task<VisionAnalysisResult?> AnalyzeWithOpenAICompatibleTraitsAsync(
            Texture2D texture,
            List<string> selectedTraits,
            string userSupplement)
        {
            Log.Message($"[MultimodalAnalysis] 使用 {apiProvider} Vision API (with traits)");
            
            string endpoint = GetVisionEndpoint();
            string prompt = GetVisionPromptWithTraits(selectedTraits, userSupplement);

            var response = await LLM.OpenAICompatibleClient.SendVisionRequestAsync(
                endpoint,
                apiKey,
                visionModel,
                prompt,
                texture,
                0.3f,
                4096,
                apiProvider
            );
            
            if (response == null || response.choices == null || response.choices.Length == 0)
            {
                Log.Error($"[MultimodalAnalysis] {apiProvider} Vision 返回空响应");
                return null;
            }
            
            string content = response.choices[0].message?.content;
            if (string.IsNullOrEmpty(content))
            {
                Log.Error("[MultimodalAnalysis] Vision 响应内容为空");
                return null;
            }
            
            return ParseVisionJson(content);
        }
        
        /// <summary>
        /// 📌 v1.6.62: Enhanced Vision Prompt (Supports Traits & Supplement) - Generic English
        /// </summary>
        private string GetVisionPromptWithTraits(List<string> selectedTraits, string userSupplement)
        {
            var sb = new StringBuilder();
            
            sb.AppendLine("Analyze this character portrait in detail and provide a comprehensive JSON response.");
            sb.AppendLine();
            sb.AppendLine("**CRITICAL: The characterDescription field MUST be written in English!**");
            sb.AppendLine();
            
            // 📌 Add user selected traits and supplement
            if (selectedTraits != null && selectedTraits.Count > 0)
            {
                sb.AppendLine("**USER SELECTED TRAITS:**");
                sb.AppendLine("---");
                sb.AppendLine(string.Join(", ", selectedTraits));
                sb.AppendLine("---");
                sb.AppendLine();
            }
            
            if (!string.IsNullOrEmpty(userSupplement))
            {
                sb.AppendLine("**USER PROVIDED CONTEXT:**");
                sb.AppendLine("---");
                sb.AppendLine(userSupplement);
                sb.AppendLine("---");
                sb.AppendLine();
                sb.AppendLine("**CRITICAL INSTRUCTIONS:**");
                sb.AppendLine("1. The user description above provides the CHARACTER'S PERSONALITY and BEHAVIOR.");
                sb.AppendLine("2. You MUST analyze the visual image to describe PHYSICAL APPEARANCE.");
                sb.AppendLine("3. In your characterDescription, COMBINE:");
                sb.AppendLine("   - Visual details (from image): hair color, eye color, clothing, posture, etc.");
                sb.AppendLine("   - Personality traits (from user): use the user's description");
                sb.AppendLine("4. DO NOT contradict the user's personality description!");
                sb.AppendLine("5. Your job is to ADD visual details to their personality, not replace it.");
                sb.AppendLine();
                sb.AppendLine("**PERSONALITY TAGS REQUIREMENT:**");
                sb.AppendLine("6. Based on the image and user description, suggest 3-6 personality tags in English.");
                sb.AppendLine("7. Examples: \"Kind\", \"Strong\", \"Clingy\", \"Yandere\", \"Tsundere\", \"Gentle\", \"Cold\"");
                sb.AppendLine("8. Include the user's selected traits if they match the analysis.");
                sb.AppendLine();
            }
            
            sb.AppendLine(@"{
  ""dominantColors"": [
    {""hex"": ""#RRGGBB"", ""percentage"": 0-100, ""name"": ""color name in English""}
  ],
  ""visualElements"": [""element1"", ""element2"", ""element3""],
  ""characterDescription"": ""Detailed 300-500 word appearance description and personality inference in English"",
  ""mood"": ""overall mood/atmosphere in English"",
  ""suggestedPersonality"": ""Benevolent/Sadistic/Chaotic/Strategic/Protective/Manipulative"",
  ""styleKeywords"": [""keyword1"", ""keyword2"", ""keyword3""],
  ""personalityTags"": [""Tag1"", ""Tag2"", ""Tag3"", ...]
}");
            
            sb.AppendLine();
            sb.AppendLine("**REMEMBER**:");
            sb.AppendLine("- characterDescription MUST be in English!");
            sb.AppendLine("- personalityTags MUST be in English!");
            if (!string.IsNullOrEmpty(userSupplement))
            {
                sb.AppendLine("- RESPECT the user's personality description - ADD visual details, don't replace!");
            }
            sb.AppendLine("- Suggest 3-6 personality tags that match the character");
            sb.AppendLine("- Focus on visual appearance first, then personality inference");
            sb.AppendLine("- 300-500 words in English");
            sb.AppendLine("- Return ONLY valid JSON");
            
            return sb.ToString();
        }
        
        /// <summary>
        /// ? Generate dialogue style from analysis (Generic English support)
        /// </summary>
        private DialogueStyleDef GenerateDialogueStyleFromAnalysis(VisionAnalysisResult visionResult, string userBio = null)
        {
            var style = new DialogueStyleDef();
            
            // Try to extract dialogue style from user bio if available
            if (!string.IsNullOrEmpty(userBio))
            {
                var lowerBio = userBio.ToLower();
                
                // Formality
                if (lowerBio.Contains("formal") || lowerBio.Contains("professional") || lowerBio.Contains("正式") || lowerBio.Contains("专业"))
                    style.formalityLevel = 0.8f;
                else if (lowerBio.Contains("casual") || lowerBio.Contains("relaxed") || lowerBio.Contains("playful") || lowerBio.Contains("随意") || lowerBio.Contains("轻松"))
                    style.formalityLevel = 0.3f;
                else
                    style.formalityLevel = 0.5f;
                
                // Emotional Expression
                if (lowerBio.Contains("emotional") || lowerBio.Contains("passionate") || lowerBio.Contains("gentle") || lowerBio.Contains("情感") || lowerBio.Contains("热情"))
                    style.emotionalExpression = 0.8f;
                else if (lowerBio.Contains("calm") || lowerBio.Contains("rational") || lowerBio.Contains("cold") || lowerBio.Contains("冷静") || lowerBio.Contains("理性"))
                    style.emotionalExpression = 0.3f;
                else
                    style.emotionalExpression = 0.6f;
                
                // Verbosity
                if (lowerBio.Contains("concise") || lowerBio.Contains("brief") || lowerBio.Contains("简洁") || lowerBio.Contains("言简意赅"))
                    style.verbosity = 0.3f;
                else if (lowerBio.Contains("detailed") || lowerBio.Contains("chatty") || lowerBio.Contains("talkative") || lowerBio.Contains("详细") || lowerBio.Contains("喜欢聊天"))
                    style.verbosity = 0.7f;
                else
                    style.verbosity = 0.5f;
                
                // Humor
                if (lowerBio.Contains("humorous") || lowerBio.Contains("funny") || lowerBio.Contains("witty") || lowerBio.Contains("幽默") || lowerBio.Contains("有趣"))
                    style.humorLevel = 0.7f;
                else if (lowerBio.Contains("serious") || lowerBio.Contains("stern") || lowerBio.Contains("严肃") || lowerBio.Contains("认真"))
                    style.humorLevel = 0.2f;
                else
                    style.humorLevel = 0.4f;
            }
            else
            {
                // 没有用户简介，使用默认值
                style.formalityLevel = 0.5f;
                style.emotionalExpression = 0.6f;
                style.verbosity = 0.5f;
                style.humorLevel = 0.4f;
                style.sarcasmLevel = 0.3f;
            }
            
            // 根据视觉风格调整
            if (visionResult.styleKeywords != null)
            {
                foreach (var keyword in visionResult.styleKeywords)
                {
                    var lower = keyword.ToLower();
                    if (lower.Contains("elegant") || lower.Contains("refined"))
                        style.formalityLevel = Math.Max(style.formalityLevel, 0.7f);
                    if (lower.Contains("playful") || lower.Contains("cheerful"))
                        style.humorLevel = Math.Max(style.humorLevel, 0.6f);
                    if (lower.Contains("serious") || lower.Contains("stern"))
                        style.formalityLevel = Math.Max(style.formalityLevel, 0.6f);
                }
            }
            
            style.useEmoticons = true;
            style.useEllipsis = true;
            style.useExclamation = true;
            
            return style;
        }
        
        /// <summary>
        /// ? 创建默认分析结果（当 API 失败时）
        /// </summary>
        private PersonaAnalysisResult CreateDefaultAnalysisResult(string userBio = null)
        {
            return new PersonaAnalysisResult
            {
                VisualTags = new List<string>(),
                ToneTags = new List<string>(),
                SuggestedPersonality = Storyteller.PersonalityTrait.Strategic,
                ConfidenceScore = 0.3f,
                GeneratedBiography = userBio ?? "一个神秘的角色。",
                VisualDescription = "未能分析图片，请确保立绘文件存在。",
                SuggestedDialogueStyle = new DialogueStyleDef
                {
                    formalityLevel = 0.5f,
                    emotionalExpression = 0.6f,
                    verbosity = 0.5f,
                    humorLevel = 0.4f,
                    sarcasmLevel = 0.3f,
                    useEmoticons = true,
                    useEllipsis = true,
                    useExclamation = true
                }
            };
        }
    }

    // ========== Data Structures ==========

    /// <summary>
    /// Vision 分析结果
    /// 📌 v1.6.62: 添加 personalityTags 字段
    /// </summary>
    public class VisionAnalysisResult
    {
        public List<ColorInfo> dominantColors { get; set; } = new List<ColorInfo>();
        public List<string> visualElements { get; set; } = new List<string>();
        public string characterDescription { get; set; } = "";
        public string mood { get; set; } = "";
        public string suggestedPersonality { get; set; } = "";
        public List<string> styleKeywords { get; set; } = new List<string>();
        
        /// <summary>
        /// 📌 v1.6.62: 个性标签（如：善良、坚强、爱撒娇、病娇等）
        /// </summary>
        public List<string> personalityTags { get; set; } = new List<string>();

        /// <summary>
        /// 获取主色调（占比最高的颜色）
        /// </summary>
        public Color GetPrimaryColor()
        {
            if (dominantColors == null || dominantColors.Count == 0)
                return Color.white;

            var primary = dominantColors.OrderByDescending(c => c.percentage).First();
            return HexToColor(primary.hex);
        }

        /// <summary>
        /// 获取重音色（占比第二的颜色）
        /// </summary>
        public Color GetAccentColor()
        {
            if (dominantColors == null || dominantColors.Count < 2)
                return Color.gray;

            var accent = dominantColors.OrderByDescending(c => c.percentage).Skip(1).First();
            return HexToColor(accent.hex);
        }

        private Color HexToColor(string hex)
        {
            hex = hex.Replace("#", "");

            if (hex.Length != 6)
                return Color.white;

            try
            {
                byte r = Convert.ToByte(hex.Substring(0, 2), 16);
                byte g = Convert.ToByte(hex.Substring(2, 2), 16);
                byte b = Convert.ToByte(hex.Substring(4, 2), 16);

                return new Color(r / 255f, g / 255f, b / 255f);
            }
            catch
            {
                return Color.white;
            }
        }
    }

    public class ColorInfo
    {
        public string hex { get; set; } = "";
        public int percentage { get; set; } = 0;
        public string name { get; set; } = "";
    }

    /// <summary>
    /// 文本深度分析结果
    /// </summary>
    public class TextAnalysisResult
    {
        public List<string> personality_traits { get; set; } = new List<string>();
        public DialogueStyleAnalysis dialogue_style { get; set; } = new DialogueStyleAnalysis();
        public List<string> tone_tags { get; set; } = new List<string>();
        public EventPreferencesAnalysis event_preferences { get; set; } = new EventPreferencesAnalysis();
        public List<string> forbidden_words { get; set; } = new List<string>();
    }

    public class DialogueStyleAnalysis
    {
        public float formality { get; set; } = 0.5f;
        public float emotional_expression { get; set; } = 0.5f;
        public float verbosity { get; set; } = 0.5f;
        public float humor { get; set; } = 0.3f;
        public float sarcasm { get; set; } = 0.2f;
    }

    public class EventPreferencesAnalysis
    {
        public float positive_bias { get; set; } = 0f;
        public float negative_bias { get; set; } = 0f;
        public float chaos_level { get; set; } = 0f;
        public float intervention_frequency { get; set; } = 0.5f;
    }
}
