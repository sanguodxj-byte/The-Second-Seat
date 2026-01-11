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
    public class LLMService : ILLMProvider
    {
        private static LLMService? instance;
        public static LLMService Instance => instance ??= new LLMService();

        private string apiEndpoint = "https://api.openai.com/v1/chat/completions";
        private string apiKey = "";
        private string modelName = "gpt-4";
        private string provider = "openai"; // ? 新增：记录 provider 类型
        
        // ⭐ v1.6.86: 可配置的上下文长度限制
        public static int MaxGameStateLength { get; set; } = 12000; 

        public LLMService()
        {
            // 不再使用 HttpClient，全部使用 UnityWebRequest
        }

        /// <summary>
        /// Configure the LLM endpoint and API key
        /// ? 新增：providerType 参数
        /// </summary>
        public string ProviderName => provider;
        public bool IsAvailable => !string.IsNullOrEmpty(apiEndpoint);

        public void Configure(string endpoint, string key, string model = "gpt-4", string providerType = "openai")
        {
            apiEndpoint = endpoint;
            apiKey = key;
            modelName = model;
            provider = providerType.ToLower(); // ? 新增：记录 provider
            
            // 静默配置 LLM
        }

        /// <summary>
        /// Implement ILLMProvider.SendMessageAsync
        /// ⭐ v1.7.0: 支持 CancellationToken (虽然接口未变，但底层支持)
        /// </summary>
        public async Task<string> SendMessageAsync(string systemPrompt, string gameState, string userMessage, float temperature = 0.7f, int maxTokens = 500)
        {
            // 创建一个默认的 CancellationToken (未来可以从外部传入)
            var cts = new System.Threading.CancellationTokenSource();
            // 设定一个合理的超时，例如 60 秒
            cts.CancelAfter(TimeSpan.FromSeconds(60));

            var response = await SendStateAndGetActionAsync(systemPrompt, gameState, userMessage, cts.Token);
            
            if (response == null) return "Error: No response from LLM";
            
            // ⭐ v1.6.85: 如果有原始 JSON 内容，优先返回（用于 ReAct Agent 解析）
            // 检查是否为有效的 JSON 对象
            if (!string.IsNullOrEmpty(response.rawContent))
            {
                string trimmed = response.rawContent.Trim();
                if (trimmed.StartsWith("{") && trimmed.EndsWith("}"))
                {
                    return response.rawContent;
                }
            }
            
            // If there's a thought, include it (ReAct agent might parse it)
            if (!string.IsNullOrEmpty(response.thought))
            {
                return $"[THOUGHT]: {response.thought}\n{response.dialogue}";
            }
            
            return response.dialogue;
        }

        /// <summary>
        /// Send game state to LLM and receive AI response asynchronously
        /// ⭐ v1.6.65: 使用 ConcurrentRequestManager 管理并发
        /// ⭐ v1.7.0: 添加 CancellationToken
        /// </summary>
        public async Task<LLMResponse> SendStateAndGetActionAsync(
            string systemPrompt, 
            string gameStateJson, 
            string userMessage = "",
            System.Threading.CancellationToken cancellationToken = default)
        {
            try
            {
                // ⭐ v1.6.65: 使用 ConcurrentRequestManager 包装请求
                return await ConcurrentRequestManager.Instance.EnqueueAsync(
                    async () => {
                        // ? 使用 provider 字段判断
                        if (provider == "gemini")
                        {
                            // Gemini 暂时不支持取消令牌，这里简单透传
                            return await SendToGeminiAsync(systemPrompt, gameStateJson, userMessage);
                        }
                        else
                        {
                            return await SendToOpenAICompatibleAsync(systemPrompt, gameStateJson, userMessage, cancellationToken);
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
        /// ⭐ v1.7.0: 修复僵尸任务和内存泄漏，支持 CancellationToken
        /// </summary>
        private async Task<LLMResponse?> SendToOpenAICompatibleAsync(string systemPrompt, string gameStateJson, string userMessage, System.Threading.CancellationToken cancellationToken)
        {
            // ⭐ v1.6.86: 线程安全快照（避免在异步过程中配置发生变化）
            string currentEndpoint = this.apiEndpoint;
            string currentKey = this.apiKey;
            string currentModel = this.modelName;

            // ✅ 修复：限制 gameState 大小（防止 JSON 过大）
            // 使用静态属性 MaxGameStateLength
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
                model = currentModel,
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

            // 使用 UnityWebRequest
            using var webRequest = new UnityWebRequest(currentEndpoint, "POST");
            webRequest.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(jsonContent));
            webRequest.downloadHandler = new DownloadHandlerBuffer();
            webRequest.SetRequestHeader("Content-Type", "application/json");
            webRequest.timeout = 60; // 60秒超时

            if (!string.IsNullOrEmpty(currentKey))
            {
                webRequest.SetRequestHeader("Authorization", $"Bearer {currentKey}");
            }

            try
            {
                // 异步发送请求
                var asyncOperation = webRequest.SendWebRequest();

                // ⭐ v1.7.0: 循环等待，检查 CancellationToken 和请求状态
                while (!asyncOperation.isDone)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        webRequest.Abort(); // 中止请求
                        Log.Message("[The Second Seat] LLM request cancelled by user/system.");
                        return null;
                    }
                    
                    // 移除 Current.Game 的不安全访问
                    await Task.Delay(50, cancellationToken);
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
                if (llmResponse != null)
                {
                    // ⭐ v1.6.85: 保存原始响应内容
                    llmResponse.rawContent = jsonContent;
                    
                    // 如果 dialogue 不为空，或者是有效的 ReAct 响应（即使 dialogue 为空），都返回
                    // 注意：ReAct 响应可能没有 dialogue 字段，只有 thought 和 action
                    return llmResponse;
                }
                
                // 如果反序列化为 null，返回纯文本
                return new LLMResponse
                {
                    thought = "",
                    dialogue = messageContent, // 使用原始内容
                    command = null,
                    rawContent = messageContent
                };
            }
            catch
            {
                // 如果不是 JSON，作为纯文本对话
                return new LLMResponse
                {
                    thought = "",
                    dialogue = messageContent,
                    command = null,
                    rawContent = messageContent
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
            // ⭐ v1.6.86: 线程安全快照
            string currentEndpoint = this.apiEndpoint;
            string currentKey = this.apiKey;
            string currentModel = this.modelName;

            var testRequest = new OpenAIRequest
            {
                model = currentModel,
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
                using var webRequest = new UnityWebRequest(currentEndpoint, "POST");
                webRequest.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(jsonContent));
                webRequest.downloadHandler = new DownloadHandlerBuffer();
                webRequest.SetRequestHeader("Content-Type", "application/json");
                webRequest.timeout = 60;

                if (!string.IsNullOrEmpty(currentKey))
                {
                    webRequest.SetRequestHeader("Authorization", $"Bearer {currentKey}");
                }

                var asyncOperation = webRequest.SendWebRequest();

                while (!asyncOperation.isDone)
                {
                    // ⭐ v1.6.86: 移除 Current.Game 不安全访问
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
