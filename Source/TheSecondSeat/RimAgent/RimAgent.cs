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
            return await ExecuteAsync(userMessage, () => gameState, temperature, maxTokens);
        }

        /// <summary>
        /// ⭐ v1.6.85: 执行 Agent 逻辑（支持动态 GameState 获取）
        /// </summary>
        public async Task<AgentResponse> ExecuteAsync(string userMessage, Func<string> gameStateProvider, float temperature = 0.7f, int maxTokens = 500)
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
                // ⭐ v1.6.86: 优化上下文管理，避免 Token 爆炸
                // 我们不再每次都追加整个 conversationBuilder，而是只保留最近的 N 轮交互，或者只传递必要的增量信息。
                // 但由于 LLMService 每次都是无状态调用，我们必须传递完整的上下文。
                // 这里的优化是：限制 conversationBuilder 的最大长度。
                StringBuilder conversationBuilder = new StringBuilder();
                conversationBuilder.Append(userMessage);

                while (currentIteration < maxIterations)
                {
                    currentIteration++;
                    
                    // 动态获取最新的游戏状态
                    // ⭐ 修复：强制在主线程获取状态，避免多线程访问游戏对象导致的崩溃
                    string currentGameState = "";
                    var tcs = new TaskCompletionSource<bool>();
                    
                    Verse.LongEventHandler.ExecuteWhenFinished(() => {
                        try
                        {
                            currentGameState = gameStateProvider?.Invoke() ?? "";
                            tcs.SetResult(true);
                        }
                        catch (Exception ex)
                        {
                            Log.Error($"[RimAgent] Error getting game state: {ex.Message}");
                            tcs.SetResult(false);
                        }
                    });
                    
                    // 等待主线程执行完毕
                    await tcs.Task;

                    // ⭐ v1.6.86: 上下文长度截断保护
                    if (conversationBuilder.Length > 8000)
                    {
                        // 如果上下文过长，保留开头（用户原始请求）和结尾（最近的思考过程）
                        string fullContext = conversationBuilder.ToString();
                        string start = fullContext.Substring(0, 1000);
                        string end = fullContext.Substring(fullContext.Length - 6000);
                        conversationBuilder.Clear();
                        conversationBuilder.Append(start).Append("\n...[Context Truncated]...\n").Append(end);
                    }

                    string llmResponseRaw = await Provider.SendMessageAsync(SystemPrompt, currentGameState, conversationBuilder.ToString(), temperature, maxTokens);
                    
                    // 解析响应
                    ReActResponse response = ParseReActResponse(llmResponseRaw);
                    
                    // ⭐ v1.6.86: 解析失败重试逻辑（简单的回退）
                    if (response == null)
                    {
                        // 尝试再次解析，也许是 JSON 格式有细微错误
                        // 这里我们简单地将非 JSON 响应视为最终回复（假设模型放弃了 ReAct 格式直接回答）
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
                            
                        // 限制 Observation 长度，防止输出过大导致下一次请求 Token 爆炸
                        if (observation.Length > 1000)
                        {
                            observation = observation.Substring(0, 1000) + "...[Observation Truncated]";
                        }
                            
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
