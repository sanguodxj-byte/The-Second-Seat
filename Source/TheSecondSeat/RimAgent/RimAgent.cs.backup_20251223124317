using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Verse;

namespace TheSecondSeat.RimAgent
{
    /// <summary>
    /// ? v1.6.65: RimAgent - AI Agent 核心类
    /// 功能：Agent 生命周期管理、工具调用、多轮对话上下文管理、错误处理和重试机制
    /// </summary>
    public class RimAgent
    {
        public string AgentId { get; private set; }
        public string SystemPrompt { get; set; }
        public List<string> AvailableTools { get; private set; }
        public ILLMProvider Provider { get; private set; }
        
        public AgentState State { get; private set; }
        public List<AgentMessage> ConversationHistory { get; private set; }
        public AgentTask? CurrentTask { get; private set; }
        
        public int TotalRequests { get; private set; }
        public int SuccessfulRequests { get; private set; }
        public int FailedRequests { get; private set; }
        
        public RimAgent(string agentId, string systemPrompt, ILLMProvider provider)
        {
            AgentId = agentId ?? throw new ArgumentNullException(nameof(agentId));
            SystemPrompt = systemPrompt ?? throw new ArgumentNullException(nameof(systemPrompt));
            Provider = provider ?? throw new ArgumentNullException(nameof(provider));
            AvailableTools = new List<string>();
            ConversationHistory = new List<AgentMessage>();
            State = AgentState.Idle;
        }
        
        public void RegisterTool(string toolName)
        {
            if (string.IsNullOrEmpty(toolName)) return;
            if (!AvailableTools.Contains(toolName))
            {
                AvailableTools.Add(toolName);
                Log.Message($"[RimAgent] {AgentId}: Tool '{toolName}' registered");
            }
        }
        
        public async Task<AgentResponse> ExecuteAsync(string userMessage, float temperature = 0.7f, int maxTokens = 500)
        {
            if (State == AgentState.Running)
            {
                return new AgentResponse { Success = false, Error = "Agent is busy" };
            }
            
            try
            {
                State = AgentState.Running;
                TotalRequests++;
                
                ConversationHistory.Add(new AgentMessage
                {
                    Role = "user",
                    Content = userMessage,
                    Timestamp = DateTime.Now
                });
                
                string response = await Provider.SendMessageAsync(SystemPrompt, userMessage, temperature, maxTokens);
                
                ConversationHistory.Add(new AgentMessage
                {
                    Role = "assistant",
                    Content = response,
                    Timestamp = DateTime.Now
                });
                
                SuccessfulRequests++;
                State = AgentState.Idle;
                
                return new AgentResponse { Success = true, Content = response, AgentId = AgentId };
            }
            catch (Exception ex)
            {
                FailedRequests++;
                State = AgentState.Error;
                Log.Error($"[RimAgent] {AgentId}: {ex.Message}");
                return new AgentResponse { Success = false, Error = ex.Message, AgentId = AgentId };
            }
        }
        
        public void ClearHistory() => ConversationHistory.Clear();
        
        public void Reset()
        {
            State = AgentState.Idle;
            CurrentTask = null;
            ClearHistory();
            TotalRequests = 0;
            SuccessfulRequests = 0;
            FailedRequests = 0;
        }
        
        public string GetDebugInfo() =>
            $"[RimAgent] {AgentId}\n" +
            $"  State: {State}\n" +
            $"  Provider: {Provider.ProviderName}\n" +
            $"  Tools: {string.Join(", ", AvailableTools)}\n" +
            $"  History: {ConversationHistory.Count} messages\n" +
            $"  Stats: {SuccessfulRequests}/{TotalRequests} ({FailedRequests} failed)";
    }
    
    public enum AgentState { Idle, Running, Error, Stopped }
    
    public class AgentMessage
    {
        public string Role { get; set; }
        public string Content { get; set; }
        public DateTime Timestamp { get; set; }
    }
    
    public class AgentTask
    {
        public string TaskId { get; set; }
        public string Description { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public bool IsCompleted { get; set; }
    }
    
    public class AgentResponse
    {
        public bool Success { get; set; }
        public string Content { get; set; }
        public string Error { get; set; }
        public string AgentId { get; set; }
        public List<ToolCall> ToolCalls { get; set; }
        public AgentResponse() { ToolCalls = new List<ToolCall>(); }
    }
    
    public class ToolCall
    {
        public string ToolName { get; set; }
        public Dictionary<string, object> Parameters { get; set; }
        public object Result { get; set; }
        public bool Success { get; set; }
        public string Error { get; set; }
    }
}