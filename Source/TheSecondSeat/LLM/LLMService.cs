using System;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Verse;
using TheSecondSeat.RimAgent;
using UnityEngine.Networking;

namespace TheSecondSeat.LLM
{
    /// <summary>
    /// Handles asynchronous communication with LLM endpoints
    /// 统一使用 UnityWebRequest（RimWorld 兼容）
    /// 支持：OpenAI, DeepSeek, Gemini, 本地 LLM
    /// </summary>
    public class LLMService
    {
        private static LLMService? instance;
        public static LLMService Instance => instance ??= new LLMService();

        private string apiEndpoint = "https://api.openai.com/v1/chat/completions";
        private string apiKey = "";
        private string modelName = "gpt-4";
        private string provider = "openai"; // ? 新增：记录 provider 类型

        public LLMService()
        {
            // 不再使用 HttpClient，全部使用 UnityWebRequest
        }

        /// <summary>
        /// Configure the LLM endpoint and API key
        /// ? 新增：providerType 参数
        /// </summary>
        public void Configure(string endpoint, string key, string model = "gpt-4", string providerType = "openai")
        {
            apiEndpoint = endpoint;
            apiKey = key;
            modelName = model;
            provider = providerType.ToLower(); // ? 新增：记录 provider
            
            // 静默配置 LLM
        }

        /// <summary>
        /// Send game state to LLM and receive AI response asynchronously
        /// ⭐ v1.6.65: 使用 ConcurrentRequestManager 管理并发
        /// </summary>
        public async Task<LLMResponse> SendStateAndGetActionAsync(
            string systemPrompt, 
            string gameStateJson, 
            string userMessage = "")
        {
            try
            {
                // ⭐ v1.6.65: 使用 ConcurrentRequestManager 包装请求
                return await ConcurrentRequestManager.Instance.EnqueueAsync(
                    async () => {
                        // ? 使用 provider 字段判断
                        if (provider == "gemini")
                        {
                            return await SendToGeminiAsync(systemPrompt, gameStateJson, userMessage);
                        }
                        else
                        {
                            return await SendToOpenAICompatibleAsync(systemPrompt, gameStateJson, userMessage);
                        }
                    },
                    maxRetries: 3
                );
            }
            catch (Exception ex)
            {
                Log.Error($"[The Second Seat] ⭐ LLM service error after retries: {ex.Message}\n{ex.StackTrace}");
                return new LLMResponse
                {
                    dialogue = "抱歉，我现在无法回应。",
                    thought = $"Error: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Gemini API 专用请求
        /// </summary>
        private async Task<LLMResponse?> SendToGeminiAsync(string systemPrompt, string gameStateJson, string userMessage)
        {
            
            string fullMessage = $"Game State:\n{gameStateJson}\n\n{userMessage}";
            var geminiResponse = await GeminiApiClient.SendRequestAsync(
                modelName,
                apiKey,
                systemPrompt,
                fullMessage,
                0.7f
            );
            
            if (geminiResponse == null || geminiResponse.Candidates == null || geminiResponse.Candidates.Count == 0)
            {
                Log.Error("[The Second Seat] Gemini 返回空响应");
                return null;
            }
            
            string messageContent = geminiResponse.Candidates[0].Content.Parts[0].Text;
            return ParseLLMResponse(messageContent);
        }

        /// <summary>
        /// OpenAI 兼容格式（OpenAI、DeepSeek、本地 LLM）
        /// 使用 UnityWebRequest 替代 HttpClient
        /// </summary>
        private async Task<LLMResponse?> SendToOpenAICompatibleAsync(string systemPrompt, string gameStateJson, string userMessage)
        {
            
            // ✅ 修复：限制 gameState 大小（防止 JSON 过大）
            const int MaxGameStateLength = 8000;  // 8KB 限制
            string truncatedGameState = gameStateJson ?? "";
            if (truncatedGameState.Length > MaxGameStateLength)
            {
                truncatedGameState = truncatedGameState.Substring(0, MaxGameStateLength) + "\n[...游戏状态已截断...]";
            }
            
            // ✅ 修复：确保内容不为 null
            string safeSystemPrompt = systemPrompt ?? "You are an AI assistant.";
            string safeUserMessage = userMessage ?? "";
            
            // 构建用户消息
            string fullUserMessage = string.IsNullOrEmpty(truncatedGameState)
                ? safeUserMessage
                : $"Game State:\n{truncatedGameState}\n\n{safeUserMessage}";
            
            // 构建请求
            var request = new OpenAIRequest
            {
                model = modelName,
                temperature = 0.7f,
                max_tokens = 500,
                messages = new[]
                {
                    new Message { role = "system", content = safeSystemPrompt },
                    new Message { role = "user", content = fullUserMessage }
                }
            };

            // ✅ 修复：使用严格的 JSON 序列化设置
            string jsonContent = JsonConvert.SerializeObject(request, new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,  // 忽略 null 字段
                StringEscapeHandling = StringEscapeHandling.EscapeHtml,  // 转义特殊字符
                Formatting = Formatting.None  // 不格式化（减少大小）
            });

            try
            {
                // 使用 UnityWebRequest
                using var webRequest = new UnityWebRequest(apiEndpoint, "POST");
                webRequest.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(jsonContent));
                webRequest.downloadHandler = new DownloadHandlerBuffer();
                webRequest.SetRequestHeader("Content-Type", "application/json");
                webRequest.timeout = 60;

                // ? 添加 Authorization 头（如果有 API Key）
                if (!string.IsNullOrEmpty(apiKey))
                {
                    webRequest.SetRequestHeader("Authorization", $"Bearer {apiKey}");
                }

                // 异步发送请求
                var asyncOperation = webRequest.SendWebRequest();

                while (!asyncOperation.isDone)
                {
                    if (Current.Game == null) return null; // 游戏退出
                    await Task.Delay(100);
                }

                // 检查响应
                if (webRequest.result == UnityWebRequest.Result.Success)
                {
                    string responseText = webRequest.downloadHandler.text;
                    var openAIResponse = JsonConvert.DeserializeObject<OpenAIResponse>(responseText);

                    if (openAIResponse?.choices == null || openAIResponse.choices.Length == 0)
                    {
                        Log.Error("[The Second Seat] LLM 返回空响应");
                        return null;
                    }

                    string messageContent = openAIResponse.choices[0].message?.content;
                    if (string.IsNullOrEmpty(messageContent))
                    {
                        Log.Error("[The Second Seat] LLM 消息内容为空");
                        return null;
                    }

                    return ParseLLMResponse(messageContent);
                }
                else
                {
                    Log.Error($"[The Second Seat] API 错误: {webRequest.responseCode} - {webRequest.error}");
                    Log.Error($"[The Second Seat] 响应内容: {webRequest.downloadHandler.text}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[The Second Seat] OpenAI 兼容 API 异常: {ex.Message}\n{ex.StackTrace}");
                return null;
            }
        }

        /// <summary>
        /// 解析 LLM 响应（JSON 或纯文本）
        /// ? 修复：从 markdown 代码块中提取 JSON
        /// </summary>
        private LLMResponse? ParseLLMResponse(string messageContent)
        {
            try
            {
                // ? 提取 JSON（有时 AI 会在 markdown 代码块中返回）
                string jsonContent = ExtractJsonFromMarkdown(messageContent);
                
                // 尝试解析为 JSON
                var llmResponse = JsonConvert.DeserializeObject<LLMResponse>(jsonContent);
                
                // ? 验证是否成功解析
                if (llmResponse != null && !string.IsNullOrEmpty(llmResponse.dialogue))
                {
                    return llmResponse;
                }
                
                // 如果 dialogue 为空，可能解析失败，返回纯文本
                return new LLMResponse
                {
                    thought = "",
                    dialogue = messageContent, // 使用原始内容
                    command = null
                };
            }
            catch
            {
                // 如果不是 JSON，作为纯文本对话
                return new LLMResponse
                {
                    thought = "",
                    dialogue = messageContent,
                    command = null
                };
            }
        }

        /// <summary>
        /// 从 markdown 代码块中提取 JSON
        /// </summary>
        private string ExtractJsonFromMarkdown(string content)
        {
            // 如果包含 ```json 代码块，提取其中的内容
            if (content.Contains("```json"))
            {
                int startIndex = content.IndexOf("```json") + 7;
                int endIndex = content.IndexOf("```", startIndex);
                if (endIndex > startIndex)
                {
                    return content.Substring(startIndex, endIndex - startIndex).Trim();
                }
            }
            else if (content.Contains("```"))
            {
                int startIndex = content.IndexOf("```") + 3;
                int endIndex = content.IndexOf("```", startIndex);
                if (endIndex > startIndex)
                {
                    return content.Substring(startIndex, endIndex - startIndex).Trim();
                }
            }
            
            return content.Trim();
        }

        /// <summary>
        /// Test connection to the LLM endpoint
        /// ? 修改：使用 provider 字段判断
        /// </summary>
        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                // ? 使用 provider 字段判断
                if (provider == "gemini")
                {
                    // Gemini API 测试
                    return await TestGeminiConnectionAsync();
                }
                else
                {
                    // OpenAI 兼容格式测试
                    return await TestOpenAICompatibleConnectionAsync();
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[The Second Seat] Test connection error: {ex.Message}");
                Log.Error($"[The Second Seat] Stack trace: {ex.StackTrace}");
                return false;
            }
        }

        /// <summary>
        /// 测试 Gemini API 连接
        /// </summary>
        private async Task<bool> TestGeminiConnectionAsync()
        {
            var response = await GeminiApiClient.SendRequestAsync(
                modelName,
                apiKey,
                "You are a test assistant.",
                "Say 'Connection successful' if you receive this.",
                0.5f
            );
            
            if (response != null && response.Candidates != null && response.Candidates.Count > 0)
            {
                return true;
            }
            else
            {
                Log.Error("[The Second Seat] Gemini Test failed: No response");
                return false;
            }
        }

        /// <summary>
        /// 测试 OpenAI 兼容 API 连接
        /// </summary>
        private async Task<bool> TestOpenAICompatibleConnectionAsync()
        {
            var testRequest = new OpenAIRequest
            {
                model = modelName,
                temperature = 0.5f,
                max_tokens = 50,
                messages = new[]
                {
                    new Message { role = "user", content = "Say 'Connection successful' if you receive this." }
                }
            };

            string jsonContent = JsonConvert.SerializeObject(testRequest);

            try
            {
                // 使用 UnityWebRequest
                using var webRequest = new UnityWebRequest(apiEndpoint, "POST");
                webRequest.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(jsonContent));
                webRequest.downloadHandler = new DownloadHandlerBuffer();
                webRequest.SetRequestHeader("Content-Type", "application/json");
                webRequest.timeout = 60;

                if (!string.IsNullOrEmpty(apiKey))
                {
                    webRequest.SetRequestHeader("Authorization", $"Bearer {apiKey}");
                }

                var asyncOperation = webRequest.SendWebRequest();

                while (!asyncOperation.isDone)
                {
                    if (Current.Game == null) return false;
                    await Task.Delay(100);
                }

                if (webRequest.result == UnityWebRequest.Result.Success)
                {
                    return true;
                }
                else
                {
                    Log.Error($"[The Second Seat] Test failed: {webRequest.responseCode} - {webRequest.error}");
                    Log.Error($"[The Second Seat] Response: {webRequest.downloadHandler.text}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[The Second Seat] Test exception: {ex.Message}");
                return false;
            }
        }
    }
}
