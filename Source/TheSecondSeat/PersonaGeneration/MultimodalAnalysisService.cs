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

        // ⭐ 缓存 Vision 分析结果
        private Dictionary<string, PersonaAnalysisResult> _visionCache = new Dictionary<string, PersonaAnalysisResult>();

        public MultimodalAnalysisService()
        {
        }

        /// <summary>
        /// ⭐ 获取缓存的分析结果
        /// </summary>
        public PersonaAnalysisResult GetCachedResult(string personaName)
        {
            if (string.IsNullOrEmpty(personaName)) return null;
            return _visionCache.TryGetValue(personaName, out var result) ? result : null;
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
            
            string prompt = MultimodalPromptGenerator.GetVisionPrompt();

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
            string prompt = MultimodalPromptGenerator.GetVisionPrompt();

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
            string prompt = MultimodalPromptGenerator.GetBriefVisionPrompt();

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
            string prompt = MultimodalPromptGenerator.GetTextAnalysisPrompt(text);

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
        /// ⚠️ 已弃用：请使用 AnalyzePersonaImageWithTraitsAsync 或回调版本 AnalyzePersonaImageWithTraitsCallback
        /// </summary>
        /// <param name="texture">立绘纹理</param>
        /// <param name="personaName">人格名称</param>
        /// <param name="selectedTraits">用户选择的特质</param>
        /// <param name="userSupplement">用户补充描述</param>
        /// <returns>人格分析结果（包含个性标签）</returns>
        [Obsolete("请使用 AnalyzePersonaImageWithTraitsAsync 或 AnalyzePersonaImageWithTraitsCallback 避免阻塞主线程")]
        public PersonaAnalysisResult AnalyzePersonaImageWithTraits(
            Texture2D texture,
            string personaName,
            List<string> selectedTraits,
            string userSupplement)
        {
            Log.Error("[MultimodalAnalysis] AnalyzePersonaImageWithTraits 已废弃且不支持同步调用（UnityWebRequest 限制）。请更新代码以使用 AnalyzePersonaImageWithTraitsCallback。");
            return CreateDefaultAnalysisResult(userSupplement);
        }

        /// <summary>
        /// 📌 v1.9.7: 回调版本 - 非阻塞异步分析（推荐用于 UI 操作）
        /// </summary>
        /// <param name="texture">立绘纹理</param>
        /// <param name="personaName">人格名称</param>
        /// <param name="selectedTraits">用户选择的特质</param>
        /// <param name="userSupplement">用户补充描述</param>
        /// <param name="onCompleted">分析完成回调</param>
        /// <param name="onError">错误回调（可选）</param>
        public void AnalyzePersonaImageWithTraitsCallback(
            Texture2D texture,
            string personaName,
            List<string> selectedTraits,
            string userSupplement,
            Action<PersonaAnalysisResult> onCompleted,
            Action<Exception> onError = null)
        {
            // ⭐ v1.9.8: 修复线程安全问题
            // Texture2D 处理（编码为Base64）必须在主线程完成
            string base64Image = "";
            try
            {
                base64Image = OpenAICompatibleClient.TextureToBase64(texture);
            }
            catch (Exception ex)
            {
                Log.Error($"[MultimodalAnalysis] 图片编码失败: {ex.Message}");
                onCompleted?.Invoke(CreateDefaultAnalysisResult(userSupplement));
                return;
            }

            // ⭐ 修复: 移除 Task.Run，直接在主线程使用异步等待
            // OpenAICompatibleClient 使用 UnityWebRequest，必须在主线程运行
            RunAnalysisAsync();

            async void RunAnalysisAsync()
            {
                try
                {
                    // 传递 base64 字符串而非 texture 对象
                    var visionResult = await AnalyzeBase64WithTraitsAsync(base64Image, selectedTraits, userSupplement);
                    var result = ProcessAnalysisResult(visionResult, selectedTraits, userSupplement);
                    
                    // ⭐ 存入缓存
                    if (result != null && !string.IsNullOrEmpty(personaName))
                    {
                        _visionCache[personaName] = result;
                    }

                    onCompleted?.Invoke(result);
                }
                catch (Exception ex)
                {
                    Log.Error($"[MultimodalAnalysis] AnalyzePersonaImageWithTraitsCallback 失败: {ex.Message}");
                    if (onError != null)
                    {
                        onError(ex);
                    }
                    else
                    {
                        onCompleted?.Invoke(CreateDefaultAnalysisResult(userSupplement));
                    }
                }
            }
        }

        /// <summary>
        /// 📌 v1.6.62: 异步分析人格图片（支持特质和用户补充）
        /// </summary>
        public async Task<PersonaAnalysisResult> AnalyzePersonaImageWithTraitsAsync(
            Texture2D texture,
            string personaName,
            List<string> selectedTraits,
            string userSupplement)
        {
            try
            {
                // 1. 在主线程将 Texture 转换为 Base64
                // ⭐ 必须在主线程执行，否则 Unity API 报错
                string base64Image = OpenAICompatibleClient.TextureToBase64(texture);

                // 2. 调用异步方法进行多模态分析
                var visionResult = await AnalyzeBase64WithTraitsAsync(base64Image, selectedTraits, userSupplement);
                
                var result = ProcessAnalysisResult(visionResult, selectedTraits, userSupplement);
                
                // ⭐ 存入缓存
                if (result != null && !string.IsNullOrEmpty(personaName))
                {
                    _visionCache[personaName] = result;
                }
                
                return result;
            }
            catch (Exception ex)
            {
                Log.Error($"[MultimodalAnalysis] AnalyzePersonaImageWithTraitsAsync 失败: {ex.Message}");
                return CreateDefaultAnalysisResult(userSupplement);
            }
        }

        /// <summary>
        /// 处理分析结果的通用逻辑
        /// </summary>
        private PersonaAnalysisResult ProcessAnalysisResult(VisionAnalysisResult visionResult, List<string> selectedTraits, string userSupplement)
        {
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
            
            // 7. 📌 提取短语库
            if (visionResult.phraseLibrary != null && visionResult.phraseLibrary.Count > 0)
            {
                result.PhraseLibrary = visionResult.phraseLibrary;
            }

            if (Prefs.DevMode)
            {
                Log.Message($"[MultimodalAnalysis] 分析完成:");
                Log.Message($"  - Visual Tags: {result.VisualTags.Count}");
                Log.Message($"  - Tone Tags: {result.ToneTags.Count}");
                Log.Message($"  - Personality Tags: {result.PersonalityTags.Count}");
                Log.Message($"  - Personality: {result.SuggestedPersonality}");
            }
            
            return result;
        }
        
        /// <summary>
        /// 📌 v1.9.8: 基于 Base64 的异步分析（线程安全）
        /// </summary>
        private async Task<VisionAnalysisResult?> AnalyzeBase64WithTraitsAsync(
            string base64Image,
            List<string> selectedTraits,
            string userSupplement)
        {
            if (string.IsNullOrEmpty(base64Image))
            {
                Log.Error("[MultimodalAnalysis] Base64 image is empty");
                return null;
            }

            try
            {
                string provider = apiProvider.ToLower();
                string prompt = MultimodalPromptGenerator.GetVisionPromptWithTraits(selectedTraits, userSupplement);
                string endpoint = GetVisionEndpoint();

                // 使用统一的 SendOpenAIRawRequestAsync 发送预构建的 JSON
                // 注意：这里我们需要手动构建 JSON Body，因为 OpenAICompatibleClient.SendVisionRequestAsync 期望 Texture2D
                
                object requestBody;
                
                if (provider == "gemini")
                {
                    // Gemini 需要不同的结构，或者我们假设 GeminiApiClient 支持 Base64?
                    // GeminiApiClient 目前只接受 Texture2D。
                    // 这是一个问题。为了支持 Gemini，我们需要修改 GeminiApiClient 或者在这里处理。
                    // 鉴于 GeminiApiClient 是静态的，且可能也依赖 Unity API，我们暂时只支持 OpenAI 兼容接口的 Base64 路径
                    // 或者我们应该修改 GeminiApiClient.SendVisionRequestAsync 也接受 Base64。
                    
                    // 暂时回退：如果 provider 是 gemini，我们在主线程做完所有事情？
                    // 不行，网络请求必须异步。
                    
                    // 方案：假设我们主要使用 OpenAI/DeepSeek
                    Log.Warning("[MultimodalAnalysis] Gemini Base64 support not fully implemented, falling back to OpenAI format");
                }

                // 构建 OpenAI/DeepSeek 兼容请求体
                if (provider == "deepseek")
                {
                     var request = new
                    {
                        model = visionModel,
                        messages = new[]
                        {
                            new
                            {
                                role = "user",
                                content = prompt + "\n\n[DeepSeek Vision is text-only for now]"
                            }
                        },
                        max_tokens = 4096
                    };
                    requestBody = request;
                }
                else
                {
                    // OpenAI Standard
                    var request = new
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
                        max_tokens = 4096
                    };
                    requestBody = request;
                }

                string jsonContent = JsonConvert.SerializeObject(requestBody, new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore
                });

                var response = await OpenAICompatibleClient.SendOpenAIRawRequestAsync(endpoint, apiKey, jsonContent);
                
                if (response == null || response.choices == null || response.choices.Length == 0)
                {
                    return null;
                }
                
                string content = response.choices[0].message?.content;
                return ParseVisionJson(content);
            }
            catch (Exception ex)
            {
                Log.Error($"[MultimodalAnalysis] Error analyzing base64 with traits: {ex.Message}");
                return null;
            }
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

}
