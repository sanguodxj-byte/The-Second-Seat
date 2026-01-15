using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Verse;

namespace TheSecondSeat.RimAgent
{
    /// <summary>
    /// ⭐ v1.9.5: RimAgent - 统一智能体核心类
    /// 
    /// 架构合并说明：
    /// - 外壳(RimAgent)：生命周期管理、统计数据、ILLMProvider引用、对话历史
    /// - 内核(ReActAgent)：工具注册与管理、ReAct循环、正则解析、Pull模式
    /// 
    /// 核心能力：
    /// - ReAct 循环：Thought -> Action -> Observation
    /// - Pull 模式：不推送大量 gameState，工具按需拉取
    /// - 正则解析：[THOUGHT]:, [ACTION]:, [ANSWER]:
    /// - 工具直接执行：无需通过静态库中转
    /// </summary>
    public class RimAgent
    {
        // ========== 外壳：生命周期与统计 ==========
        
        /// <summary>Agent 唯一标识符</summary>
        public string AgentId { get; private set; }
        
        /// <summary>系统提示词（可动态更新）</summary>
        public string SystemPrompt { get; set; }
        
        /// <summary>LLM 提供者</summary>
        public ILLMProvider Provider { get; private set; }
        
        /// <summary>当前状态</summary>
        public AgentState State { get; private set; } = AgentState.Idle;
        
        /// <summary>对话历史</summary>
        public List<AgentMessage> ConversationHistory { get; private set; } = new List<AgentMessage>();
        
        /// <summary>总请求数</summary>
        public int TotalRequests { get; private set; }
        
        /// <summary>成功请求数</summary>
        public int SuccessfulRequests { get; private set; }
        
        /// <summary>失败请求数</summary>
        public int FailedRequests { get; private set; }
        
        /// <summary>并发锁</summary>
        private readonly SemaphoreSlim executionLock = new SemaphoreSlim(1, 1);

        // ========== 调试与监控 ==========
        
        /// <summary>所有活跃的 Agent 实例</summary>
        public static List<RimAgent> ActiveAgents = new List<RimAgent>();

        /// <summary>最近一次发送的完整 Prompt</summary>
        public string LastPrompt { get; private set; }

        /// <summary>最近一次接收的响应内容</summary>
        public string LastResponseContent { get; private set; }
        
        // ========== 内核：工具管理与 ReAct 循环 ==========
        
        /// <summary>已注册的工具实例</summary>
        private Dictionary<string, ITool> tools = new Dictionary<string, ITool>(StringComparer.OrdinalIgnoreCase);
        
        /// <summary>ReAct 最大迭代次数（可配置）</summary>
        public int MaxIterations { get; set; } = 10;
        
        /// <summary>对话历史最大条数</summary>
        public int MaxHistorySize { get; set; } = 10;

        /// <summary>当前对话摘要</summary>
        public string Summary { get; private set; }

        /// <summary>上下文最大 Token 数</summary>
        public int MaxContextTokens { get; set; } = 4000;
        
        /// <summary>思考解析正则</summary>
        private static readonly Regex ThoughtPattern = new Regex(
            @"\[THOUGHT\]:\s*(.+?)(?=\[ACTION\]|\[ANSWER\]|$)",
            RegexOptions.Singleline | RegexOptions.IgnoreCase);
        
        /// <summary>动作解析正则 - 支持多种格式</summary>
        private static readonly Regex ActionPattern = new Regex(
            @"\[ACTION\]:\s*(\w+)\s*\((.*)?\)",
            RegexOptions.Singleline | RegexOptions.IgnoreCase);
        
        /// <summary>答案解析正则</summary>
        private static readonly Regex AnswerPattern = new Regex(
            @"\[ANSWER\]:\s*(.+)$",
            RegexOptions.Singleline | RegexOptions.IgnoreCase);
        
        /// <summary>参数解析正则 - key=value 或 key="value"</summary>
        private static readonly Regex ParamPattern = new Regex(
            @"(\w+)\s*=\s*(?:""([^""]*)""|'([^']*)'|(\S+))",
            RegexOptions.Singleline);

        // ========== 构造函数 ==========
        
        /// <summary>
        /// 创建新的 RimAgent 实例
        /// </summary>
        /// <param name="agentId">Agent 唯一标识符</param>
        /// <param name="systemPrompt">系统提示词</param>
        /// <param name="provider">LLM 提供者</param>
        public RimAgent(string agentId, string systemPrompt, ILLMProvider provider)
        {
            AgentId = agentId ?? throw new ArgumentNullException(nameof(agentId));
            SystemPrompt = systemPrompt ?? "";
            Provider = provider ?? throw new ArgumentNullException(nameof(provider));
            
            // 注册到全局列表
            ActiveAgents.Add(this);
        }

        // ========== 工具管理（内核能力） ==========
        
        /// <summary>
        /// 注册工具实例
        /// </summary>
        public void RegisterTool(ITool tool)
        {
            if (tool == null)
            {
                Log.Warning($"[RimAgent] {AgentId}: Attempted to register null tool");
                return;
            }
            
            tools[tool.Name] = tool;
            Log.Message($"[RimAgent] {AgentId}: Registered tool '{tool.Name}'");
        }
        
        /// <summary>
        /// 通过工具名称注册（兼容旧代码，从全局库获取）
        /// </summary>
        public void RegisterTool(string toolName)
        {
            if (string.IsNullOrEmpty(toolName))
            {
                Log.Warning($"[RimAgent] {AgentId}: Attempted to register empty tool name");
                return;
            }
            
            // 从全局工具库获取
            var tool = RimAgentTools.GetTool(toolName);
            if (tool != null)
            {
                tools[toolName] = tool;
                Log.Message($"[RimAgent] {AgentId}: Registered tool '{toolName}' from global registry");
            }
            else
            {
                Log.Warning($"[RimAgent] {AgentId}: Tool '{toolName}' not found in global registry");
            }
        }
        
        /// <summary>
        /// 获取工具实例
        /// </summary>
        public ITool GetTool(string name)
        {
            return tools.TryGetValue(name, out var tool) ? tool : null;
        }

        // ========== 核心执行方法（内核能力） ==========
        
        /// <summary>
        /// 执行 ReAct 循环
        /// </summary>
        /// <param name="userInput">用户输入</param>
        /// <param name="temperature">采样温度</param>
        /// <param name="maxTokens">最大 Token 数</param>
        /// <param name="cancellationToken">取消令牌</param>
        public async Task<AgentResponse> ExecuteAsync(
            string userInput,
            float temperature = 0.7f,
            int maxTokens = 500,
            CancellationToken cancellationToken = default)
        {
            await executionLock.WaitAsync(cancellationToken);
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    State = AgentState.Running;
                    TotalRequests++;
                    
                    // 添加用户消息到历史
                    AddToHistory(new AgentMessage
                    {
                        Role = "user",
                        Content = userInput,
                        Timestamp = DateTime.Now
                    });
                    
                    // 构建上下文
                    string context = BuildContext();
                    string toolsDescription = BuildToolsDescription();
                
                // ReAct 循环
                string finalAnswer = null;
                var observations = new List<string>();
                
                for (int iteration = 0; iteration < MaxIterations; iteration++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    // 构建完整提示
                    string fullPrompt = BuildFullPrompt(userInput, toolsDescription, observations, context);
                    
                    // 记录调试信息
                    LastPrompt = $"--- System Prompt ---\n{SystemPrompt}\n\n--- User Prompt ---\n{fullPrompt}";

                    // 调用 LLM
                    string llmResponse = await Provider.SendMessageAsync(
                        SystemPrompt,
                        "", // gameState 通过工具按需获取（Pull 模式）
                        fullPrompt,
                        temperature,
                        maxTokens
                    );
                    
                    if (string.IsNullOrEmpty(llmResponse))
                    {
                        return HandleError("LLM returned empty response");
                    }

                    // 记录原始响应
                    LastResponseContent = llmResponse;
                    
                    // 解析响应
                    var parsed = ParseLLMResponse(llmResponse);
                    
                    // 处理最终答案
                    if (!string.IsNullOrEmpty(parsed.Answer))
                    {
                        finalAnswer = parsed.Answer;
                        break;
                    }
                    
                    // 处理工具调用
                    if (!string.IsNullOrEmpty(parsed.ActionName))
                    {
                        string observation = await ExecuteToolAsync(parsed.ActionName, parsed.ActionParams);
                        observations.Add($"[Observation]: {observation}");
                    }
                    else
                    {
                        // 无工具调用也无最终答案，可能是思考过程
                        if (!string.IsNullOrEmpty(parsed.Thought))
                        {
                            observations.Add($"[Thought]: {parsed.Thought}");
                        }
                        else
                        {
                            // 无法解析，直接使用 LLM 响应作为最终答案
                            finalAnswer = llmResponse;
                            break;
                        }
                    }
                }
                
                // 如果循环结束仍无答案，使用最后的观察
                if (string.IsNullOrEmpty(finalAnswer))
                {
                    finalAnswer = observations.LastOrDefault() ?? "Unable to generate response";
                }
                
                // 添加助手消息到历史
                AddToHistory(new AgentMessage
                {
                    Role = "assistant",
                    Content = finalAnswer,
                    Timestamp = DateTime.Now
                });
                
                // 尝试压缩历史记录
                await CompressHistoryAsync();

                State = AgentState.Idle;
                SuccessfulRequests++;
                
                return new AgentResponse
                {
                    Success = true,
                    Content = finalAnswer
                };
            }
            catch (Exception ex)
            {
                return HandleError(ex.Message);
            }
            finally
            {
                executionLock.Release();
            }
        }
        
        /// <summary>
        /// 处理错误
        /// </summary>
        private AgentResponse HandleError(string error)
        {
            State = AgentState.Error;
            FailedRequests++;
            Log.Error($"[RimAgent] {AgentId}: {error}");
            
            return new AgentResponse
            {
                Success = false,
                Error = error
            };
        }

        // ========== 解析方法（内核能力） ==========
        
        /// <summary>
        /// 解析 LLM 响应
        /// </summary>
        private ParsedResponse ParseLLMResponse(string response)
        {
            var result = new ParsedResponse();
            
            // 尝试解析 [THOUGHT]:
            var thoughtMatch = ThoughtPattern.Match(response);
            if (thoughtMatch.Success)
            {
                result.Thought = thoughtMatch.Groups[1].Value.Trim();
            }
            
            // 尝试解析 [ACTION]:
            var actionMatch = ActionPattern.Match(response);
            if (actionMatch.Success)
            {
                result.ActionName = actionMatch.Groups[1].Value.Trim();
                string paramsString = actionMatch.Groups[2].Value.Trim();
                result.ActionParams = ParseParams(paramsString);
            }
            
            // 尝试解析 [ANSWER]:
            var answerMatch = AnswerPattern.Match(response);
            if (answerMatch.Success)
            {
                result.Answer = answerMatch.Groups[1].Value.Trim();
            }
            
            return result;
        }
        
        /// <summary>
        /// 解析参数字符串
        /// </summary>
        private Dictionary<string, object> ParseParams(string paramsString)
        {
            var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            
            if (string.IsNullOrEmpty(paramsString))
                return result;
            
            // 匹配 key=value 或 key="value" 格式
            var matches = ParamPattern.Matches(paramsString);
            foreach (Match match in matches)
            {
                string key = match.Groups[1].Value;
                // 优先使用带引号的值，否则使用裸值
                string value = match.Groups[2].Success ? match.Groups[2].Value :
                              match.Groups[3].Success ? match.Groups[3].Value :
                              match.Groups[4].Value;
                result[key] = value;
            }
            
            return result;
        }

        // ========== 工具执行（内核能力） ==========
        
        /// <summary>
        /// 执行工具
        /// </summary>
        private async Task<string> ExecuteToolAsync(string toolName, Dictionary<string, object> parameters)
        {
            try
            {
                // 首先尝试从本地注册的工具获取
                if (tools.TryGetValue(toolName, out var tool))
                {
                    var result = await tool.ExecuteAsync(parameters);
                    if (result.Success)
                    {
                        return result.Data?.ToString() ?? "Tool executed successfully";
                    }
                    else
                    {
                        return $"Error: {result.Error}";
                    }
                }
                
                // 回退到全局工具库
                var globalTool = RimAgentTools.GetTool(toolName);
                if (globalTool != null)
                {
                    var result = await globalTool.ExecuteAsync(parameters);
                    if (result.Success)
                    {
                        return result.Data?.ToString() ?? "Tool executed successfully";
                    }
                    else
                    {
                        return $"Error: {result.Error}";
                    }
                }
                
                return $"Error: Tool '{toolName}' not found";
            }
            catch (Exception ex)
            {
                Log.Error($"[RimAgent] {AgentId}: Tool execution error: {ex.Message}");
                return $"Error: {ex.Message}";
            }
        }

        // ========== 提示构建（内核能力） ==========
        
        /// <summary>
        /// 构建工具描述
        /// </summary>
        private string BuildToolsDescription()
        {
            if (tools.Count == 0)
                return "No tools available.";
            
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("## Available Tools:");
            sb.AppendLine();
            
            foreach (var kvp in tools)
            {
                sb.AppendLine($"- **{kvp.Key}**: {kvp.Value.Description}");
            }
            
            return sb.ToString();
        }
        
        /// <summary>
        /// 构建对话上下文 (基于 Token 的滑动窗口)
        /// </summary>
        private string BuildContext()
        {
            var sb = new System.Text.StringBuilder();
            
            // 包含摘要
            if (!string.IsNullOrEmpty(Summary))
            {
                sb.AppendLine("## Conversation Summary:");
                sb.AppendLine(Summary);
                sb.AppendLine();
            }

            int currentTokens = 0;
            var selectedHistory = new List<AgentMessage>();

            // 从后往前遍历历史，直到达到 Token 限制
            for (int i = ConversationHistory.Count - 1; i >= 0; i--)
            {
                var msg = ConversationHistory[i];
                // 粗略估算 Token 数 (1 token ≈ 3-4 chars)
                int estimatedTokens = (msg.Content?.Length ?? 0) / 3;
                
                // 加上一些元数据开销
                estimatedTokens += 10; 

                if (currentTokens + estimatedTokens > MaxContextTokens) break;
                
                selectedHistory.Insert(0, msg);
                currentTokens += estimatedTokens;
            }

            if (selectedHistory.Count > 0)
            {
                sb.AppendLine("## Recent Conversation:");
                foreach (var msg in selectedHistory)
                {
                    sb.AppendLine($"[{msg.Role}]: {msg.Content}");
                }
                sb.AppendLine();
            }
            
            return sb.ToString();
        }
        
        /// <summary>
        /// 添加消息到历史
        /// </summary>
        private void AddToHistory(AgentMessage message)
        {
            ConversationHistory.Add(message);
        }

        /// <summary>
        /// 压缩历史记录（生成摘要）
        /// </summary>
        private async Task CompressHistoryAsync()
        {
            // 如果历史记录未超过限制，无需压缩
            if (ConversationHistory.Count <= MaxHistorySize) return;

            // 每次压缩最早的 2 条消息 (通常是一问一答)
            int countToCompress = 2;
            if (ConversationHistory.Count < countToCompress) return;

            var messagesToCompress = ConversationHistory.Take(countToCompress).ToList();
            
            // 构建压缩 Prompt
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Summarize the following conversation lines into a concise paragraph to retain key information.");
            if (!string.IsNullOrEmpty(Summary))
            {
                sb.AppendLine($"Existing summary: {Summary}");
            }
            sb.AppendLine("\nNew lines to merge:");
            foreach (var msg in messagesToCompress)
            {
                sb.AppendLine($"[{msg.Role}]: {msg.Content}");
            }

            try
            {
                // 调用 LLM 生成摘要 (使用较小的 maxTokens)
                string newSummary = await Provider.SendMessageAsync(
                    "You are a summarization assistant.",
                    "",
                    sb.ToString(),
                    0.5f,
                    200
                );

                if (!string.IsNullOrEmpty(newSummary))
                {
                    Summary = newSummary.Trim();
                    // 移除已压缩的消息
                    ConversationHistory.RemoveRange(0, countToCompress);
                    Log.Message($"[RimAgent] {AgentId}: Compressed history. New summary length: {Summary.Length}");
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[RimAgent] {AgentId}: Failed to compress history: {ex.Message}");
                // 压缩失败，回退到简单裁剪以防止内存溢出
                ConversationHistory.RemoveRange(0, countToCompress);
            }
        }
        
        /// <summary>
        /// 构建完整提示
        /// </summary>
        private string BuildFullPrompt(string userInput, string toolsDescription, List<string> observations, string context)
        {
            var sb = new System.Text.StringBuilder();
            
            // 指令
            sb.AppendLine("You are a ReAct agent. Think step by step and use tools when needed.");
            sb.AppendLine();
            sb.AppendLine("Response format:");
            sb.AppendLine("[THOUGHT]: Your reasoning");
            sb.AppendLine("[ACTION]: toolName(param1=value1, param2=value2)");
            sb.AppendLine("OR");
            sb.AppendLine("[ANSWER]: Your final response to the user");
            sb.AppendLine();
            
            // 工具描述
            sb.AppendLine(toolsDescription);
            sb.AppendLine();
            
            // 上下文
            if (!string.IsNullOrEmpty(context))
            {
                sb.AppendLine(context);
            }
            
            // 之前的观察
            if (observations.Count > 0)
            {
                sb.AppendLine("## Previous Steps:");
                foreach (var obs in observations)
                {
                    sb.AppendLine(obs);
                }
                sb.AppendLine();
            }
            
            // 用户输入
            sb.AppendLine($"## User Request:");
            sb.AppendLine(userInput);
            
            return sb.ToString();
        }

        // ========== 生命周期管理（外壳能力） ==========
        
        /// <summary>
        /// 重置 Agent 状态
        /// </summary>
        public void Reset()
        {
            State = AgentState.Idle;
            ConversationHistory.Clear();
            Summary = null;
            TotalRequests = 0;
            SuccessfulRequests = 0;
            FailedRequests = 0;
        }
        
        /// <summary>
        /// 清空对话历史
        /// </summary>
        public void ClearHistory()
        {
            ConversationHistory.Clear();
            Summary = null;
        }
        
        /// <summary>
        /// 获取调试信息
        /// </summary>
        public string GetDebugInfo()
        {
            return $"Agent: {AgentId}\n" +
                   $"State: {State}\n" +
                   $"Tools: {string.Join(", ", tools.Keys)}\n" +
                   $"History: {ConversationHistory.Count} messages\n" +
                   $"Summary: {(string.IsNullOrEmpty(Summary) ? "None" : $"{Summary.Length} chars")}\n" +
                   $"Stats: {SuccessfulRequests}/{TotalRequests} successful";
        }

        // ========== 内部类 ==========
        
        /// <summary>
        /// 解析结果
        /// </summary>
        private class ParsedResponse
        {
            public string Thought { get; set; }
            public string ActionName { get; set; }
            public Dictionary<string, object> ActionParams { get; set; } = new Dictionary<string, object>();
            public string Answer { get; set; }
        }
    }

    // ========== 支持类型 ==========
    
    /// <summary>
    /// Agent 状态枚举
    /// </summary>
    public enum AgentState
    {
        Idle,
        Running,
        Error,
        Stopped
    }
    
    /// <summary>
    /// Agent 消息
    /// </summary>
    public class AgentMessage
    {
        public string Role { get; set; }
        public string Content { get; set; }
        public DateTime Timestamp { get; set; }
    }
    
    /// <summary>
    /// Agent 响应
    /// </summary>
    public class AgentResponse
    {
        public bool Success { get; set; }
        public string Content { get; set; }
        public string Error { get; set; }
    }
}
