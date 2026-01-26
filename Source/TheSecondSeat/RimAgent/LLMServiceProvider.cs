using System;
using System.Threading.Tasks;
using TheSecondSeat.LLM;
using TheSecondSeat.Settings;
using Verse;

namespace TheSecondSeat.RimAgent
{
    /// <summary>
    /// ⭐ v2.9.6: LLMService 适配器，实现 ILLMProvider 接口
    /// 将现有的 LLMService 包装为 RimAgent 可用的 Provider
    /// 
    /// ⭐ v2.9.7: 修复 - 返回原始响应内容，而非只返回 dialogue
    /// 这样 RimAgent 可以正确解析完整的 JSON 响应
    /// </summary>
    public class LLMServiceProvider : ILLMProvider
    {
        public string ProviderName => "LLMService";
        
        public bool IsAvailable => !string.IsNullOrEmpty(TheSecondSeatMod.Settings?.apiKey);
        
        /// <summary>
        /// 请求类型（用于日志区分）
        /// </summary>
        public string RequestType { get; set; } = "Chat";
        
        /// <summary>
        /// 最近一次的完整 LLMResponse（用于获取 command、expression 等额外字段）
        /// </summary>
        public LLMResponse? LastFullResponse { get; private set; }
        
        /// <summary>
        /// 发送消息到 LLM
        /// 使用 LLMService.Instance.SendStateAndGetActionAsync
        /// ⭐ v2.9.7: 返回 rawContent（原始响应），而非只返回 dialogue
        /// </summary>
        public async Task<string> SendMessageAsync(
            string systemPrompt, 
            string gameState, 
            string userMessage, 
            float temperature = 0.7f, 
            int maxTokens = 500)
        {
            try
            {
                // 调用 LLMService (使用现有 API)
                var response = await LLMService.Instance.SendStateAndGetActionAsync(
                    systemPrompt: systemPrompt,
                    gameStateJson: gameState,
                    userMessage: userMessage
                );
                
                // ⭐ v2.9.7: 保存完整响应
                LastFullResponse = response;
                
                // ⭐ v2.9.7: 返回原始内容（如果有），否则返回对话内容
                // rawContent 包含完整的 JSON 响应，RimAgent 可以解析它
                if (!string.IsNullOrEmpty(response?.rawContent))
                {
                    return response.rawContent;
                }
                
                // 回退到 dialogue
                return response?.dialogue;
            }
            catch (Exception ex)
            {
                Log.Error($"[LLMServiceProvider] Error: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// 测试连接
        /// </summary>
        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                var response = await SendMessageAsync(
                    "You are a test assistant.",
                    "",
                    "Say 'Hello' if you can hear me.",
                    0.1f,
                    50
                );
                return !string.IsNullOrEmpty(response);
            }
            catch
            {
                return false;
            }
        }
    }
}
