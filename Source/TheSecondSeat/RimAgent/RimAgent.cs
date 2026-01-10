using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Text;
using Newtonsoft.Json;
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
        
        public RimAgent(string AgentId, string SystemPrompt, ILLMProvider provider)
        {
            AgentId = AgentId ?? throw new ArgumentNullException(nameof(AgentId));
            Log.Message($"[RimAgent] Agent created: {AgentId}, SystemPrompt length: {SystemPrompt.Length}");
            SystemPrompt = SystemPrompt ?? throw new ArgumentNullException(nameof(SystemPrompt));
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
        
        /// <summary>
        /// ⭐ v1.6.77: 执行 Agent 逻辑（支持 ReAct 循环）
        /// </summary>
        public async Task<AgentResponse> ExecuteAsync(string userMessage, string gameState = "", float temperature = 0.7f, int maxTokens = 500)
        {
            if (State == AgentState.Running)
            {
                return new AgentResponse { Success = false, Error = "Agent is busy" };
            }
            
            try
            {
                Log.Message($"[RimAgent] {AgentId}: ExecuteAsync called, message: {userMessage}");
                State = AgentState.Running;
                TotalRequests++;
                
                // 记录初始用户消息
                ConversationHistory.Add(new AgentMessage
                {
                    Role = "user",
                    Content = userMessage,
                    Timestamp = DateTime.Now
                });

                // ReAct 循环变量
                int maxIterations = 5;
                int currentIteration = 0;
                string currentPrompt = userMessage; // 初始 Prompt 为用户输入
                StringBuilder conversationBuilder = new StringBuilder();
                conversationBuilder.Append(userMessage);

                while (currentIteration < maxIterations)
                {
                    currentIteration++;
                    
                    // 调用 LLM (注意：Provider.SendMessageAsync 会自动处理 gameState 的拼接，但不支持 history，所以我们需要手动构建 prompt)
                    // 为了避免 Provider 重复拼接 gameState，我们在后续循环中可能需要技巧，
                    // 但目前 Provider 接口是固定的。
                    // 策略：我们将完整的 conversation history 作为 userMessage 传递。
                    // 缺点：gameState 会被重复拼接在最前面。这是 ReAct 的标准做法（每次请求都带上完整状态）。
                    
                    string llmResponseRaw = await Provider.SendMessageAsync(SystemPrompt, gameState, conversationBuilder.ToString(), temperature, maxTokens);
                    
                    // 解析响应
                    ReActResponse response = ParseReActResponse(llmResponseRaw);
                    
                    if (response == null)
                    {
                        // 解析失败，假设是普通文本响应
                        SuccessfulRequests++;
                        State = AgentState.Idle;
                        return new AgentResponse { Success = true, Content = llmResponseRaw, AgentId = AgentId };
                    }

                    // 记录 Thought
                    if (!string.IsNullOrEmpty(response.thought))
                    {
                        conversationBuilder.AppendLine($"\nThought: {response.thought}");
                    }

                    // 检查是否有 Action
                    if (response.action != null && !string.IsNullOrEmpty(response.action.name))
                    {
                        Log.Message($"[RimAgent] ReAct Action: {response.action.name}");
                        conversationBuilder.AppendLine($"\nAction: {response.action.name}");
                        
                        // 执行工具
                        var toolResult = await RimAgentTools.ExecuteAsync(response.action.name, response.action.args);
                        
                        // 记录 Observation
                        string observation = toolResult.Success ? 
                            (toolResult.Data?.ToString() ?? "Success") : 
                            $"Error: {toolResult.Error}";
                            
                        Log.Message($"[RimAgent] ReAct Observation: {observation}");
                        conversationBuilder.AppendLine($"\nObservation: {observation}");
                        
                        // 继续下一轮循环
                        continue; 
                    }

                    // 如果有 Final Response，结束循环
                    if (!string.IsNullOrEmpty(response.response))
                    {
                        ConversationHistory.Add(new AgentMessage
                        {
                            Role = "assistant",
                            Content = response.response,
                            Timestamp = DateTime.Now
                        });
                        
                        SuccessfulRequests++;
                        State = AgentState.Idle;
                        return new AgentResponse { Success = true, Content = response.response, AgentId = AgentId };
                    }
                    
                    // 如果既没有 Action 也没有 Response，可能出错了，或者需要继续思考
                    // 这里为了防止死循环，如果没有任何有效输出，就退出
                    if (string.IsNullOrEmpty(response.thought))
                    {
                         Log.Warning("[RimAgent] Empty response from ReAct agent.");
                         break;
                    }
                }
                
                // 超过最大迭代次数或异常退出
                State = AgentState.Idle;
                return new AgentResponse { Success = false, Error = "ReAct loop limit reached or no final response", AgentId = AgentId };
            }
            catch (Exception ex)
            {
                FailedRequests++;
                State = AgentState.Error;
                Log.Error($"[RimAgent] {AgentId}: {ex.Message}");
                return new AgentResponse { Success = false, Error = ex.Message, AgentId = AgentId };
            }
        }
        
        /// <summary>
        /// 解析 ReAct JSON 响应
        /// </summary>
        private ReActResponse ParseReActResponse(string raw)
        {
            try
            {
                // 尝试提取 JSON (处理可能存在的 markdown 代码块)
                string json = ExtractJson(raw);
                return JsonConvert.DeserializeObject<ReActResponse>(json);
            }
            catch
            {
                return null;
            }
        }

        private string ExtractJson(string content)
        {
            if (content.Contains("```json"))
            {
                int start = content.IndexOf("```json") + 7;
                int end = content.IndexOf("```", start);
                if (end > start) return content.Substring(start, end - start).Trim();
            }
            if (content.Contains("```")) // 只有 ``` 的情况
            {
                int start = content.IndexOf("```") + 3;
                int end = content.IndexOf("```", start);
                if (end > start) return content.Substring(start, end - start).Trim();
            }
            return content;
        }

        // 内部类：用于解析 ReAct JSON
        private class ReActResponse
        {
            public string thought { get; set; }
            public ReActAction action { get; set; }
            public string response { get; set; }
        }

        private class ReActAction
        {
            public string name { get; set; }
            public Dictionary<string, object> args { get; set; }
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
