using System;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Verse;
using TheSecondSeat.RimAgent;
using UnityEngine.Networking;
using System.Collections.Generic;

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
            
            // ⭐ v1.6.85: 如果有原始响应内容，优先返回（用于 ReAct Agent 解析）
            // 无论是 JSON 还是 Tag 格式，RimAgent 现在的解析器都能处理
            if (!string.IsNullOrEmpty(response.rawContent))
            {
                return response.rawContent;
            }
            
            // 回退逻辑（理论上不应到达这里，除非 rawContent 为空）
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
        /// ⭐ v2.7.0: 添加请求日志记录
        /// ⭐ v2.9.0: 使用 TaskCompletionSource 替代 busy wait 模式
        /// </summary>
        private async Task<LLMResponse?> SendToOpenAICompatibleAsync(string systemPrompt, string gameStateJson, string userMessage, System.Threading.CancellationToken cancellationToken)
        {
            // ⭐ v1.6.86: 线程安全快照（避免在异步过程中配置发生变化）
            string currentEndpoint = this.apiEndpoint;
            string currentKey = this.apiKey;
            string currentModel = this.modelName;

            // ⭐ v2.7.0: 创建请求日志
            var log = new RequestLog
            {
                Timestamp = DateTime.Now,
                Endpoint = currentEndpoint,
                Model = currentModel,
                RequestType = "Chat"  // 默认类型
            };

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

            // 使用 Newtonsoft.Json 序列化请求，确保特殊字符正确转义
            // ⭐ 移至后台线程，避免阻塞主线程
            string jsonContent = await Task.Run(() => JsonConvert.SerializeObject(request, new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                Formatting = Formatting.None
            }));

            log.RequestJson = jsonContent;

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
                // ⭐ v2.9.0: 使用 TaskCompletionSource 实现真正的异步等待
                // 替代 busy wait 的 while(!isDone) 循环
                var tcs = new System.Threading.Tasks.TaskCompletionSource<bool>();
                
                // 发送请求
                var asyncOperation = webRequest.SendWebRequest();
                
                // 注册完成回调（非阻塞）
                asyncOperation.completed += (op) => 
                {
                    try
                    {
                        tcs.TrySetResult(true);
                    }
                    catch
                    {
                        // 静默处理
                    }
                };
                
                // 使用 CancellationToken 注册取消
                using (cancellationToken.Register(() => 
                {
                    try
                    {
                        webRequest.Abort();
                        tcs.TrySetCanceled();
                    }
                    catch
                    {
                        // 静默处理
                    }
                }))
                {
                    // 等待完成或取消
                    try
                    {
                        await tcs.Task;
                    }
                    catch (System.Threading.Tasks.TaskCanceledException)
                    {
                        Log.Message("[The Second Seat] LLM request cancelled by user/system.");
                        log.Success = false;
                        log.ErrorMessage = "Cancelled";
                        log.DurationSeconds = (float)(DateTime.Now - log.Timestamp).TotalSeconds;
                        LLMRequestHistory.Add(log);
                        return null;
                    }
                }

                log.DurationSeconds = (float)(DateTime.Now - log.Timestamp).TotalSeconds;

                // 检查响应
                if (webRequest.result == UnityWebRequest.Result.Success)
                {
                    string responseText = webRequest.downloadHandler.text;
                    log.ResponseJson = responseText;
                    log.Success = true;

                    // ⭐ 移至后台线程，避免阻塞主线程
                    var openAIResponse = await Task.Run(() => JsonConvert.DeserializeObject<OpenAIResponse>(responseText));

                    // ⭐ v2.7.0: 记录 Token 使用量
                    if (openAIResponse?.usage != null)
                    {
                        log.PromptTokens = openAIResponse.usage.prompt_tokens;
                        log.CompletionTokens = openAIResponse.usage.completion_tokens;
                        log.TotalTokens = openAIResponse.usage.total_tokens;
                    }

                    LLMRequestHistory.Add(log);

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
                    log.Success = false;
                    log.ErrorMessage = $"{webRequest.responseCode} - {webRequest.error}";
                    log.ResponseJson = webRequest.downloadHandler.text;
                    LLMRequestHistory.Add(log);

                    Log.Error($"[The Second Seat] API 错误: {webRequest.responseCode} - {webRequest.error}");
                    Log.Error($"[The Second Seat] 响应内容: {webRequest.downloadHandler.text}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                log.Success = false;
                log.ErrorMessage = ex.Message;
                log.DurationSeconds = (float)(DateTime.Now - log.Timestamp).TotalSeconds;
                LLMRequestHistory.Add(log);

                Log.Error($"[The Second Seat] OpenAI 兼容 API 异常: {ex.Message}\n{ex.StackTrace}");
                return null;
            }
        }

        /// <summary>
        /// 解析 LLM 响应
        /// ⭐ v2.8.8: 委托给 LLMResponseParser
        /// </summary>
        private LLMResponse? ParseLLMResponse(string messageContent)
        {
            return LLMResponseParser.Parse(messageContent);
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
                var tcs = new TaskCompletionSource<bool>();
                asyncOperation.completed += _ => tcs.TrySetResult(true);

                await tcs.Task;

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
