using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Networking;
using Verse;
using Newtonsoft.Json;

namespace TheSecondSeat.LLM
{
    // 引入一个扩展类来支持 await UnityWebRequest
    public static class UnityWebRequestExtensions
    {
        public static TaskAwaiter<UnityWebRequest> GetAwaiter(this UnityWebRequestAsyncOperation reqOp)
        {
            var tcs = new TaskCompletionSource<UnityWebRequest>();
            reqOp.completed += op => tcs.SetResult(((UnityWebRequestAsyncOperation)op).webRequest);
            return tcs.Task.GetAwaiter();
        }
    }

    /// <summary>
    /// OpenAI 兼容 API 客户端（支持 OpenAI、DeepSeek 等）
    /// 支持文本和图片（Vision）分析
    /// </summary>
    public class OpenAICompatibleClient
    {
        /// <summary>
        /// 发送纯文本请求到 OpenAI 兼容 API
        /// </summary>
        public static async Task<OpenAIResponse?> SendRequestAsync(
            string endpoint,
            string apiKey,
            string model,
            string systemPrompt,
            string userMessage,
            float temperature = 0.7f,
            int maxTokens = 500)
        {
            var request = new OpenAIRequest
            {
                model = model,
                temperature = temperature,
                max_tokens = maxTokens,
                messages = new[]
                {
                    new Message { role = "system", content = systemPrompt },
                    new Message { role = "user", content = userMessage }
                }
            };

            string jsonContent = JsonConvert.SerializeObject(request, new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore
            });
            return await SendOpenAIRawRequestAsync(endpoint, apiKey, jsonContent);
        }

        /// <summary>
        /// 发送包含图片的 Vision 请求到 OpenAI 兼容 API
        /// ? 支持 DeepSeek 特殊格式（不使用 image_url）
        /// </summary>
        public static async Task<OpenAIResponse?> SendVisionRequestAsync(
            string endpoint,
            string apiKey,
            string model,
            string textPrompt,
            Texture2D imageTexture,
            float temperature = 0.7f,
            int maxTokens = 1000,
            string provider = "openai")  // ? 新增：provider 参数
        {
            if (imageTexture == null)
            {
                Log.Error("[OpenAICompatible] 图片纹理为空");
                return null;
            }

            try
            {
                // 1?? 将 Texture2D 转换为 Base64
                string base64Image = TextureToBase64(imageTexture);
                if (string.IsNullOrEmpty(base64Image))
                {
                    Log.Error("[OpenAICompatible] 图片 Base64 编码失败");
                    return null;
                }


                // 2?? 根据 provider 构建不同格式的请求
                string jsonContent;
                
                if (provider.ToLower() == "deepseek")
                {
                    // DeepSeek 特殊格式：不使用 image_url，只发送文本提示
                    var request = new
                    {
                        model = model,
                        temperature = temperature,
                        max_tokens = maxTokens,
                        messages = new[]
                        {
                            new
                            {
                                role = "user",
                                content = textPrompt + "\n\n[注意：DeepSeek 当前不支持图像分析，将仅基于文本提示返回通用结果]"
                            }
                        }
                    };
                    
                    jsonContent = JsonConvert.SerializeObject(request, new JsonSerializerSettings
                    {
                        NullValueHandling = NullValueHandling.Ignore
                    });
                }
                else
                {
                    // OpenAI 标准格式（支持 image_url）
                    var request = new
                    {
                        model = model,
                        temperature = temperature,
                        max_tokens = maxTokens,
                        messages = new[]
                        {
                            new
                            {
                                role = "user",
                                content = new object []
                                {
                                    new { type = "text", text = textPrompt },
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
                        }
                    };

                    jsonContent = JsonConvert.SerializeObject(request, new JsonSerializerSettings
                    {
                        NullValueHandling = NullValueHandling.Ignore
                    });
                }

                return await SendOpenAIRawRequestAsync(endpoint, apiKey, jsonContent, "Vision");  // ⭐ v2.7.0: 标记为 Vision 请求
            }
            catch (Exception ex)
            {
                Log.Error($"[OpenAICompatible] Vision 请求异常: {ex.Message}\n{ex.StackTrace}");
                return null;
            }
        }

        /// <summary>
        /// 将 Texture2D 转换为 Base64 编码的 PNG/JPG 字符串
        /// ? 优化：压缩大图片以减少 Base64 大小
        /// </summary>
        public static string TextureToBase64(Texture2D texture)
        {
            try
            {
                // 确保纹理可读
                if (!texture.isReadable)
                {
                    texture = MakeTextureReadable(texture);
                }

                // ? 如果图片太大，先缩小
                Texture2D textureToEncode = texture;
                bool needsResize = texture.width > 1024 || texture.height > 1024;
                
                if (needsResize)
                {
                    int maxSize = 1024;
                    float scale = Math.Min((float)maxSize / texture.width, (float)maxSize / texture.height);
                    int newWidth = (int)(texture.width * scale);
                    int newHeight = (int)(texture.height * scale);
                    
                    textureToEncode = ResizeTexture(texture, newWidth, newHeight);
                }

                // ? 使用 JPG 编码（更小的文件）
                byte[] imageBytes = textureToEncode.EncodeToJPG(75); // 质量 75%
                
                if (imageBytes == null || imageBytes.Length == 0)
                {
                    // 如果 JPG 失败，回退到 PNG
                    imageBytes = textureToEncode.EncodeToPNG();
                }
                
                if (imageBytes == null || imageBytes.Length == 0)
                {
                    Log.Error("[OpenAICompatible] 图片编码失败");
                    return string.Empty;
                }

                // 转换为 Base64
                string base64 = Convert.ToBase64String(imageBytes);
                
                // 清理临时纹理
                if (needsResize && textureToEncode != texture)
                {
                    UnityEngine.Object.Destroy(textureToEncode);
                }
                
                return base64;
            }
            catch (Exception ex)
            {
                Log.Error($"[OpenAICompatible] 纹理转 Base64 异常: {ex.Message}");
                return string.Empty;
            }
        }

        /// <summary>
        /// ? 调整纹理大小
        /// </summary>
        private static Texture2D ResizeTexture(Texture2D source, int newWidth, int newHeight)
        {
            RenderTexture rt = RenderTexture.GetTemporary(newWidth, newHeight, 0, RenderTextureFormat.ARGB32);
            RenderTexture.active = rt;
            
            Graphics.Blit(source, rt);
            
            Texture2D result = new Texture2D(newWidth, newHeight, TextureFormat.RGB24, false);
            result.ReadPixels(new Rect(0, 0, newWidth, newHeight), 0, 0);
            result.Apply();
            
            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(rt);
            
            return result;
        }

        /// <summary>
        /// 创建纹理的可读副本
        /// </summary>
        private static Texture2D MakeTextureReadable(Texture2D source)
        {
            RenderTexture tmp = RenderTexture.GetTemporary(
                source.width,
                source.height,
                0,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.sRGB);

            Graphics.Blit(source, tmp);
            
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = tmp;
            
            Texture2D readable = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
            readable.ReadPixels(new Rect(0, 0, tmp.width, tmp.height), 0, 0);
            readable.Apply();
            
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(tmp);
            
            return readable;
        }

        /// <summary>
        /// 发送原始 JSON 请求到 OpenAI 兼容端点
        /// ⭐ v2.7.0: 添加 requestType 参数
        /// </summary>
        public static async Task<OpenAIResponse?> SendOpenAIRawRequestAsync(string endpoint, string apiKey, string jsonContent, string requestType = "API")
        {
            var log = new RequestLog
            {
                Timestamp = DateTime.Now,
                Endpoint = endpoint,
                RequestJson = jsonContent,
                RequestType = requestType  // ⭐ v2.7.0: 设置请求类型
            };

            // 尝试从 JSON 中提取模型名称，用于日志记录
            try
            {
                var modelMatch = System.Text.RegularExpressions.Regex.Match(jsonContent, @"""model""\s*:\s*""([^""]+)""");
                if (modelMatch.Success)
                {
                    log.Model = modelMatch.Groups[1].Value;
                }
            }
            catch { /* Ignore regex errors */ }

            try
            {
                using var webRequest = new UnityWebRequest(endpoint, "POST");
                webRequest.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(jsonContent));
                webRequest.downloadHandler = new DownloadHandlerBuffer();
                webRequest.SetRequestHeader("Content-Type", "application/json");
                webRequest.timeout = 60;

                // 添加 Authorization 头
                if (!string.IsNullOrEmpty(apiKey))
                {
                    webRequest.SetRequestHeader("Authorization", $"Bearer {apiKey}");
                }

                // 使用扩展方法进行异步等待，避免忙等待
                await webRequest.SendWebRequest();

                log.DurationSeconds = (float)(DateTime.Now - log.Timestamp).TotalSeconds;

                if (webRequest.result == UnityWebRequest.Result.Success)
                {
                    string responseText = webRequest.downloadHandler.text;
                    log.ResponseJson = responseText;
                    log.Success = true;

                    var response = JsonConvert.DeserializeObject<OpenAIResponse>(responseText);
                    
                    if (response?.usage != null)
                    {
                        log.PromptTokens = response.usage.prompt_tokens;
                        log.CompletionTokens = response.usage.completion_tokens;
                        log.TotalTokens = response.usage.total_tokens;
                    }

                    LLMRequestHistory.Add(log);
                    return response;
                }
                else
                {
                    log.Success = false;
                    log.ErrorMessage = $"{webRequest.responseCode} - {webRequest.error}";
                    log.ResponseJson = webRequest.downloadHandler.text;
                    LLMRequestHistory.Add(log);

                    Log.Error($"[OpenAICompatible] API 错误: {webRequest.responseCode} - {webRequest.error}");
                    Log.Error($"[OpenAICompatible] 响应内容: {webRequest.downloadHandler.text}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                log.Success = false;
                log.ErrorMessage = ex.Message;
                log.DurationSeconds = (float)(DateTime.Now - log.Timestamp).TotalSeconds;
                LLMRequestHistory.Add(log);

                Log.Error($"[OpenAICompatible] API 异常: {ex.Message}\n{ex.StackTrace}");
                return null;
            }
        }
    }
}
