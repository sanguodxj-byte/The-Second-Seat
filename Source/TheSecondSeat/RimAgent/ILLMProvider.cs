using System;
using System.Threading.Tasks;

namespace TheSecondSeat.RimAgent
{
    /// <summary>
    /// ? v1.6.65: LLM 提供商统一接口
    /// </summary>
    public interface ILLMProvider
    {
        string ProviderName { get; }
        bool IsAvailable { get; }
        
        Task<string> SendMessageAsync(string systemPrompt, string userMessage, float temperature = 0.7f, int maxTokens = 500);
        Task<bool> TestConnectionAsync();
    }

    /// <summary>
    /// LLM 响应结果
    /// </summary>
    public class LLMResponse
    {
        public bool Success { get; set; }
        public string Content { get; set; }
        public string Provider { get; set; }
        public string Error { get; set; }
        public TimeSpan Latency { get; set; }
    }
}
