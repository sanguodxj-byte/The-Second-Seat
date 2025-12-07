using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using Verse;
using Newtonsoft.Json;

namespace TheSecondSeat.LLM
{
    /// <summary>
    /// Gemini API 专用客户端
    /// 支持文本和图片（Vision）分析
    /// 参考 RimWorld 文档和 Gemini API 规范
    /// </summary>
    public class GeminiApiClient
    {
        private const string BaseUrl = "https://generativelanguage.googleapis.com/v1beta";
        
        /// <summary>
        /// 发送纯文本请求到 Gemini API
        /// </summary>
        public static async Task<GeminiResponse?> SendRequestAsync(
            string model,
            string apiKey,
            string systemPrompt,
            string userMessage,
            float temperature = 0.7f)
        {
            if (string.IsNullOrEmpty(apiKey))
            {
                Log.Error("[The Second Seat] Gemini API Key 缺失");
                return null;
            }

            // 构建 URL（API Key 在 URL 参数中）
            string url = $"{BaseUrl}/models/{model}:generateContent?key={apiKey}";

            // 构建请求体（Gemini 格式）
            var request = new GeminiRequest
            {
                SystemInstruction = new SystemInstruction
                {
                    Parts = new List<Part> { new Part { Text = systemPrompt } }
                },
                Contents = new List<Content>
                {
                    new Content
                    {
                        Role = "user",
                        Parts = new List<Part> { new Part { Text = userMessage } }
                    }
                },
                GenerationConfig = new GenerationConfig
                {
                    Temperature = temperature,
                    TopK = 40,
                    TopP = 0.95f,
                    MaxOutputTokens = 2048
                }
            };

            return await SendGeminiRequestAsync(url, request);
        }

        /// <summary>
        /// 发送包含图片的多模态请求到 Gemini Vision API
        /// </summary>
        public static async Task<GeminiResponse?> SendVisionRequestAsync(
            string model,
            string apiKey,
            string textPrompt,
            Texture2D imageTexture,
            float temperature = 0.7f,
            int maxTokens = 2048)
        {
            if (string.IsNullOrEmpty(apiKey))
            {
                Log.Error("[The Second Seat] Gemini API Key 缺失");
                return null;
            }

            if (imageTexture == null)
            {
                Log.Error("[The Second Seat] 图片纹理为空");
                return null;
            }

            try
            {
                // 1?? 将 Texture2D 转换为 Base64
                string base64Image = TextureToBase64(imageTexture);
                if (string.IsNullOrEmpty(base64Image))
                {
                    Log.Error("[The Second Seat] 图片 Base64 编码失败");
                    return null;
                }

                Log.Message($"[The Second Seat] 图片已编码为 Base64 ({base64Image.Length} 字符)");

                // 2?? 构建多模态请求体
                string url = $"{BaseUrl}/models/{model}:generateContent?key={apiKey}";

                var request = new GeminiRequest
                {
                    Contents = new List<Content>
                    {
                        new Content
                        {
                            Role = "user",
                            Parts = new List<Part>
                            {
                                // 先放文本提示
                                new Part { Text = textPrompt },
                                // 再放图片数据
                                new Part
                                {
                                    InlineData = new InlineData
                                    {
                                        MimeType = "image/png",
                                        Data = base64Image
                                    }
                                }
                            }
                        }
                    },
                    GenerationConfig = new GenerationConfig
                    {
                        Temperature = temperature,
                        TopK = 40,
                        TopP = 0.95f,
                        MaxOutputTokens = maxTokens  // ? 使用参数而不是硬编码
                    }
                };

                return await SendGeminiRequestAsync(url, request);
            }
            catch (Exception ex)
            {
                Log.Error($"[The Second Seat] Gemini Vision 请求异常: {ex.Message}\n{ex.StackTrace}");
                return null;
            }
        }

        /// <summary>
        /// 将 Texture2D 转换为 Base64 编码的 PNG/JPG 字符串
        /// ? 优化：压缩大图片以减少 Base64 大小
        /// </summary>
        private static string TextureToBase64(Texture2D texture)
        {
            try
            {
                // 确保纹理可读
                if (!texture.isReadable)
                {
                    Log.Warning("[The Second Seat] 纹理不可读，尝试创建可读副本");
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
                    Log.Message($"[The Second Seat] 图片已缩小：{texture.width}x{texture.height} → {newWidth}x{newHeight}");
                }

                // ? 使用 JPG 编码（更小的文件）
                byte[] imageBytes = textureToEncode.EncodeToJPG(75); // 质量 75%
                
                if (imageBytes == null || imageBytes.Length == 0)
                {
                    // 如果 JPG 失败，回退到 PNG
                    Log.Warning("[The Second Seat] JPG 编码失败，回退到 PNG");
                    imageBytes = textureToEncode.EncodeToPNG();
                }
                
                if (imageBytes == null || imageBytes.Length == 0)
                {
                    Log.Error("[The Second Seat] 图片编码失败");
                    return string.Empty;
                }

                // 转换为 Base64
                string base64 = Convert.ToBase64String(imageBytes);
                
                Log.Message($"[The Second Seat] 图片编码成功：{textureToEncode.width}x{textureToEncode.height}, " +
                          $"{imageBytes.Length / 1024}KB → Base64 {base64.Length} 字符");
                
                // 清理临时纹理
                if (needsResize && textureToEncode != texture)
                {
                    UnityEngine.Object.Destroy(textureToEncode);
                }
                
                return base64;
            }
            catch (Exception ex)
            {
                Log.Error($"[The Second Seat] 纹理转 Base64 异常: {ex.Message}");
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
        /// 统一的 Gemini API 请求发送方法
        /// </summary>
        private static async Task<GeminiResponse?> SendGeminiRequestAsync(string url, GeminiRequest request)
        {
            string jsonContent = JsonConvert.SerializeObject(request, new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                Formatting = Formatting.None
            });

            try
            {
                Log.Message($"[The Second Seat] Gemini API 请求: {url}");
                Log.Message($"[The Second Seat] 请求体大小: {jsonContent.Length} 字符");

                // ? 使用 UnityWebRequest
                using var webRequest = new UnityWebRequest(url, "POST");
                webRequest.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(jsonContent));
                webRequest.downloadHandler = new DownloadHandlerBuffer();
                webRequest.SetRequestHeader("Content-Type", "application/json");
                webRequest.timeout = 60; // 60 秒超时

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
                    Log.Message($"[The Second Seat] Gemini API 响应成功: {responseText.Substring(0, Math.Min(500, responseText.Length))}...");

                    var response = JsonConvert.DeserializeObject<GeminiResponse>(responseText);
                    return response;
                }
                else
                {
                    Log.Error($"[The Second Seat] Gemini API 错误: {webRequest.responseCode} - {webRequest.error}");
                    Log.Error($"[The Second Seat] 响应内容: {webRequest.downloadHandler.text}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[The Second Seat] Gemini API 异常: {ex.Message}\n{ex.StackTrace}");
                return null;
            }
        }
    }

    // ===== Gemini API 数据结构 =====

    public class GeminiRequest
    {
        [JsonProperty("system_instruction")]
        public SystemInstruction SystemInstruction { get; set; }

        [JsonProperty("contents")]
        public List<Content> Contents { get; set; }

        [JsonProperty("generationConfig")]
        public GenerationConfig GenerationConfig { get; set; }
    }

    public class SystemInstruction
    {
        [JsonProperty("parts")]
        public List<Part> Parts { get; set; }
    }

    public class Content
    {
        [JsonProperty("role")]
        public string Role { get; set; }

        [JsonProperty("parts")]
        public List<Part> Parts { get; set; }
    }

    public class Part
    {
        [JsonProperty("text")]
        public string Text { get; set; }

        [JsonProperty("inlineData")]
        public InlineData InlineData { get; set; }
    }

    /// <summary>
    /// 图片数据（Base64 编码）
    /// </summary>
    public class InlineData
    {
        [JsonProperty("mimeType")]
        public string MimeType { get; set; }

        [JsonProperty("data")]
        public string Data { get; set; }
    }

    public class GenerationConfig
    {
        [JsonProperty("temperature")]
        public float Temperature { get; set; }

        [JsonProperty("topK")]
        public int TopK { get; set; }

        [JsonProperty("topP")]
        public float TopP { get; set; }

        [JsonProperty("maxOutputTokens")]
        public int MaxOutputTokens { get; set; }
    }

    public class GeminiResponse
    {
        [JsonProperty("candidates")]
        public List<Candidate> Candidates { get; set; }

        [JsonProperty("usageMetadata")]
        public UsageMetadata UsageMetadata { get; set; }
    }

    public class Candidate
    {
        [JsonProperty("content")]
        public Content Content { get; set; }

        [JsonProperty("finishReason")]
        public string FinishReason { get; set; }
    }

    public class UsageMetadata
    {
        [JsonProperty("promptTokenCount")]
        public int PromptTokenCount { get; set; }

        [JsonProperty("totalTokenCount")]
        public int TotalTokenCount { get; set; }
    }
}
