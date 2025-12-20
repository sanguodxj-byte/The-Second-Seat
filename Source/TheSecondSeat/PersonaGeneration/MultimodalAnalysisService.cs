using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using Verse;

namespace TheSecondSeat.PersonaGeneration
{
    /// <summary>
    /// 多模态分析服务 - 使用 Vision API 分析图像
    /// </summary>
    public class MultimodalAnalysisService
    {
        private static MultimodalAnalysisService? instance;
        public static MultimodalAnalysisService Instance => instance ??= new MultimodalAnalysisService();

        private readonly HttpClient httpClient;
        private string apiProvider = "openai"; // "openai", "deepseek", "gemini"
        private string apiKey = "";
        private string visionModel = "gpt-4-vision-preview";
        private string textModel = "gpt-4";

        public MultimodalAnalysisService()
        {
            httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(60);  // 多模态分析可能需要更长时间
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
                    "gemini" => "gemini-pro-vision",
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
                    "gemini" => "gemini-pro",
                    _ => "gpt-4"
                };
            }

            // 设置 Authorization Header（与 LLMService 相同方式）
            if (!string.IsNullOrEmpty(apiKey))
            {
                httpClient.DefaultRequestHeaders.Clear();
                httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
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
        /// 获取 Vision 分析的提示词（优化版 - 要求返回中文）
        /// </summary>
        private string GetVisionPrompt()
        {
            return @"Analyze this character portrait in detail and provide a comprehensive JSON response.

**CRITICAL: The characterDescription field MUST be written in Simplified Chinese (简体中文)!**

{
  ""dominantColors"": [
    {""hex"": ""#RRGGBB"", ""percentage"": 0-100, ""name"": ""color name in English""}
  ],
  ""visualElements"": [""element1"", ""element2"", ""element3""],
  ""characterDescription"": ""【必须用简体中文写】详细的300-500字外观描述和性格推断"",
  ""mood"": ""overall mood/atmosphere in English"",
  ""suggestedPersonality"": ""Benevolent/Sadistic/Chaotic/Strategic/Protective/Manipulative"",
  ""styleKeywords"": [""keyword1"", ""keyword2"", ""keyword3""]
}

**CRITICAL REQUIREMENTS for characterDescription (必须用简体中文!):**

**第一部分：详细的外观描述 (40%)**

用中文描述所有可见细节：
- **种族**: 人类？精灵？龙人？兽人？机械生命？
- **发型**: 颜色、长度、风格、质地（例如：""银白色长发如瀑布般倾泻而下，用深红色丝带束起""）
- **眼睛**: 颜色、形状、表情（例如：""猩红色竖瞳眼眸，流露出智慧与危险的气息""）
- **面部特征**: 表情、年龄、伤疤、纹饰
- **体型**: 身形、姿态、站姿
- **服装与护甲**: 
  * 主要服饰
  * 护甲配件
  * 配饰
  * 材质和状态
- **特殊特征**: 翅膀、尾巴、角、武器、魔法效果
- **整体印象**: 姿态、光线、构图传达的情绪

**第二部分：从外观推断性格 (40%)**

用中文从外观推断特质：

**从表情和肢体语言推断**:
- 冷峻的面容 → 情感内敛、自律、自制
- 自信的姿态 → 果断、经验丰富、领导气质
- 警惕的站姿 → 谨慎、防备、可能经历过创伤
- 放松的表情 → 和蔼可亲、友善、容易信任

**从服装和护甲推断**:
- 厚重护甲 → 重视防护、随时准备战斗、纪律严明
- 深色系 → 神秘、严肃、可能内向或有秘密
- 精致设计 → 注重细节、可能自负或在意身份地位
- 简单实用的装备 → 务实、注重功能而非形式

**从武器和装备推断**:
- 明显武器 → 随时准备冲突、果断、可能具有攻击性
- 隐藏武器 → 具有战略眼光、谨慎、喜欢出其不意
- 魔法 artefacts → 知识渊博、爱好研究、与古老智慧相连
- 无武器 → 和平、信任他人、或依赖其他优势

**从种族/人种特征推断**:
- 龙族特征 → 骄傲、强大、可能傲慢或有领地意识
- 精灵特征 → 优雅、长寿视角、可能超然物外
- 兽人特征 → 原始本能、热情、直接的沟通方式

**第三部分：对话和行为预测 (20%)**

基于视觉分析，用中文预测：

**说话风格**:
- ""她可能用[冷静/热情/严厉/温柔]的语气说话""
- ""她的表情暗示使用[正式/随意/专业/诗意]的语言""
- ""她可能用[简洁的命令/丰富的描述/军事用语]进行交流""

**情感表达**:
- ""很少公开表露强烈情感"" 或 ""心直口快""
- ""谨慎控制反应"" 或 ""冲动反应""

**互动风格**:
- ""与陌生人保持距离"" 或 ""立即热情友好""
- ""观察后发言"" 或 ""主动与人交谈""
- ""重视行动而非言辞"" 或 ""信仰言辞外交""

**EXAMPLE of GOOD characterDescription (用简体中文!):**

""这是一位拥有龙族血统的少女。她有一头银白色的长发如瀑布般倾泻至肩下，与深邃的猩红色竖瞳眼眸形成鲜明对比。她的太阳穴处长着两只小巧的弯曲角，长袍下延伸出一条覆盖着红色鳞片的长尾，在光线下闪烁着微光。

她身穿一件由高品质布料制成的深色连帽长袍，优雅地环绕着她的身形。袍子下是棕色皮革护甲，关键部位经过加固——肩甲上可见战斗的痕迹，但保养得当，功能完好。护甲上装饰着精致的深红色图案，呼应着她天然鳞片的颜色，暗示着个人定制或文化意义。

她的面部表情明显冷峻而沉着，眉间隐约可见的纹路暗示着多年的自律或磨难。她的身姿笔挺而警觉，带有军人训练的痕迹。她的举止透露出源于经验而非傲慢的自信。她锐利的目光表明高度的智慧和对周围环境的持续警觉。

从这些视觉线索判断，她可能具有战略型或守护型的性格。军人般的气质和实用的护甲表明纪律和准备。她冷峻的表情暗示情感控制和压力下的沉着。她可能用沉稳、深思熟虑的语气说话，谨慎地选择措辞。她的对话会简洁直接，偏好清晰而非华丽的语言。

在交谈中，她可能最初保持专业距离，先观察他人再决定是否信任。她重视能力和可靠性胜于魅力。她的情感表达在公共场合会有所克制，尽管她信任的人可能会看到更柔软的一面。她对逻辑和实际考虑的反应超出情感诉求。

龙族特征暗示着一种自豪和自力更生的倾向。她可能对个人空间和价值观有所领地意识。她的装备维护良好，显示出对细节的关注和自给自足的能力。贯穿她外表的深红色调暗示着平静外表下的受控激情——她有坚定的信念，但通过纪律而非情绪爆发来表达。""

**REMEMBER**:
- characterDescription 必须用简体中文写!
- 要具体：不要只说""护甲""——描述材质、状态、设计
- 从每个细节推断性格
- 预测行为：他们会如何说话？反应？互动？
- 300-500字
- 只返回有效的JSON

Focus on:
- Top 3-4 dominant colors with accurate percentages
- All visual elements visible in the portrait
- Detailed appearance analysis (IN CHINESE!)
- Personality inference from visual cues (IN CHINESE!)
- Behavioral predictions (IN CHINESE!)
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
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                var response = await httpClient.PostAsync(endpoint, content);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Log.Error($"[MultimodalAnalysis] Vision API error: {response.StatusCode} - {errorContent}");
                    return null;
                }

                var responseJson = await response.Content.ReadAsStringAsync();
                return ParseVisionResponse(responseJson);
            }
            catch (TaskCanceledException)
            {
                Log.Warning("[MultimodalAnalysis] Vision API request timeout");
                return null;
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
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                var response = await httpClient.PostAsync(endpoint, content);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Log.Error($"[MultimodalAnalysis] Text API error: {response.StatusCode} - {errorContent}");
                    return null;
                }

                var responseJson = await response.Content.ReadAsStringAsync();
                return ParseTextResponse(responseJson);
            }
            catch (TaskCanceledException)
            {
                Log.Warning("[MultimodalAnalysis] Text API request timeout");
                return null;
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
                "gemini" => "https://generativelanguage.googleapis.com/v1/models/gemini-pro-vision:generateContent",
                _ => "https://api.openai.com/v1/chat/completions"
            };
        }

        private string GetTextEndpoint()
        {
            return apiProvider switch
            {
                "openai" => "https://api.openai.com/v1/chat/completions",
                "deepseek" => "https://api.deepseek.com/v1/chat/completions",
                "gemini" => "https://generativelanguage.googleapis.com/v1/models/gemini-pro:generateContent",
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
  ""personalityTags"": [""中文个性标签1"", ""中文个性标签2"", ""中文个性标签3"", ...]
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

        private VisionAnalysisResult? ParseVisionResponse(string responseJson)
        {
            try
            {
                var response = JsonConvert.DeserializeObject<OpenAIResponse>(responseJson);
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

        private TextAnalysisResult? ParseTextResponse(string responseJson)
        {
            try
            {
                var response = JsonConvert.DeserializeObject<OpenAIResponse>(responseJson);
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

        // OpenAI Response Structure
        private class OpenAIResponse
        {
            public Choice[]? choices { get; set; }

            public class Choice
            {
                public Message? message { get; set; }
            }

            public class Message
            {
                public string? content { get; set; }
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
        /// 📌 v1.6.62: 增强版 Vision Prompt（支持特质和用户补充）
        /// </summary>
        private string GetVisionPromptWithTraits(List<string> selectedTraits, string userSupplement)
        {
            var sb = new StringBuilder();
            
            sb.AppendLine("Analyze this character portrait in detail and provide a comprehensive JSON response.");
            sb.AppendLine();
            sb.AppendLine("**CRITICAL: The characterDescription field MUST be written in Simplified Chinese (简体中文)!**");
            sb.AppendLine();
            
            // 📌 添加用户选择的特质和补充描述
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
                sb.AppendLine("6. Based on the image and user description, suggest 3-6 personality tags in Chinese.");
                sb.AppendLine("7. Examples: \"善良\", \"坚强\", \"爱撒娇\", \"病娇\", \"傲娇\", \"温柔\", \"冷酷\"");
                sb.AppendLine("8. Include the user's selected traits if they match the分析.");
                sb.AppendLine();
            }
            
            sb.AppendLine(@"{
  ""dominantColors"": [
    {""hex"": ""#RRGGBB"", ""percentage"": 0-100, ""name"": ""color name in English""}
  ],
  ""visualElements"": [""element1"", ""element2"", ""element3""],
  ""characterDescription"": ""必须用简体中文书写的详细描述（300-500字），包含外貌和性格推断"",
  ""mood"": ""overall mood/atmosphere in English"",
  ""suggestedPersonality"": ""Benevolent/Sadistic/Chaotic/Strategic/Protective/Manipulative"",
  ""styleKeywords"": [""keyword1"", ""keyword2"", ""keyword3""],
  ""personalityTags"": [""中文个性标签1"", ""中文个性标签2"", ""中文个性标签3"", ...]
}");
            
            sb.AppendLine();
            sb.AppendLine("**REMEMBER**:");
            sb.AppendLine("- characterDescription 必须用简体中文书写!");
            sb.AppendLine("- personalityTags 必须用简体中文!");
            if (!string.IsNullOrEmpty(userSupplement))
            {
                sb.AppendLine("- RESPECT the user's personality description - ADD visual details, don't replace!");
            }
            sb.AppendLine("- Suggest 3-6 personality tags that match the character");
            sb.AppendLine("- Focus on visual appearance first, then personality inference");
            sb.AppendLine("- 300-500 characters in Chinese");
            sb.AppendLine("- Return ONLY valid JSON");
            
            return sb.ToString();
        }
        
        /// <summary>
        /// ? 根据分析结果生成对话风格
        /// </summary>
        private DialogueStyleDef GenerateDialogueStyleFromAnalysis(VisionAnalysisResult visionResult, string userBio = null)
        {
            var style = new DialogueStyleDef();
            
            // 如果有用户简介，尝试从简介中提取对话风格
            if (!string.IsNullOrEmpty(userBio))
            {
                var lowerBio = userBio.ToLower();
                
                // 检测正式程度
                if (lowerBio.Contains("正式") || lowerBio.Contains("专业"))
                    style.formalityLevel = 0.8f;
                else if (lowerBio.Contains("随意") || lowerBio.Contains("轻松") || lowerBio.Contains("俏皮"))
                    style.formalityLevel = 0.3f;
                else
                    style.formalityLevel = 0.5f;
                
                // 检测情感表达
                if (lowerBio.Contains("情感") || lowerBio.Contains("热情") || lowerBio.Contains("温柔"))
                    style.emotionalExpression = 0.8f;
                else if (lowerBio.Contains("冷静") || lowerBio.Contains("理性"))
                    style.emotionalExpression = 0.3f;
                else
                    style.emotionalExpression = 0.6f;
                
                // 检测话语量
                if (lowerBio.Contains("简洁") || lowerBio.Contains("言简意赅"))
                    style.verbosity = 0.3f;
                else if (lowerBio.Contains("详细") || lowerBio.Contains("喜欢聊天"))
                    style.verbosity = 0.7f;
                else
                    style.verbosity = 0.5f;
                
                // 检测幽默感
                if (lowerBio.Contains("幽默") || lowerBio.Contains("有趣") || lowerBio.Contains("搞笑"))
                    style.humorLevel = 0.7f;
                else if (lowerBio.Contains("严肃") || lowerBio.Contains("认真"))
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
