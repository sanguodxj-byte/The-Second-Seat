using System;
using System.Threading.Tasks;

namespace TheSecondSeat.RimAgent
{
    /// <summary>
    /// ? v1.6.65: LLM 提供商统一接口（支持 gameState 传递）
    /// </summary>
    public interface ILLMProvider
    {
        string ProviderName { get; }
        bool IsAvailable { get; }
        
        /// <summary>
        /// 发送消息到 LLM（支持游戏状态上下文）
        /// </summary>
        /// <param name="systemPrompt">系统提示词</param>
        /// <param name="gameState">游戏状态 JSON（可选）</param>
        /// <param name="userMessage">用户消息</param>
        /// <param name="temperature">温度参数</param>
        /// <param name="maxTokens">最大 token 数</param>
        Task<string> SendMessageAsync(string systemPrompt, string gameState, string userMessage, float temperature = 0.7f, int maxTokens = 500);
        
        Task<bool> TestConnectionAsync();
    }
    
    // 注意: LLMResponse 类已移至 TheSecondSeat.LLM.LLMDataStructures
    // RimAgent 模块应使用 AgentResponse 类 (定义在 RimAgent.cs 中)
}
