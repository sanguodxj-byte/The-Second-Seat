using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Verse;
using TheSecondSeat.NaturalLanguage;
using Newtonsoft.Json;

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
    public class RimAgent : IDisposable
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
        
        /// <summary>答案解析正则 (兼容 DIALOGUE 和 RESPONSE)</summary>
        private static readonly Regex AnswerPattern = new Regex(
            @"\[(?:ANSWER|DIALOGUE|RESPONSE)\]:\s*(.+)$",
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
        /// ⭐ v2.9.8: 简单执行（无 ReAct 循环，无对话历史）
        /// 适用于叙事者等只需要单次 LLM 调用的场景
        /// </summary>
        /// <param name="userInput">用户输入</param>
        /// <param name="temperature">采样温度</param>
        /// <param name="maxTokens">最大 Token 数</param>
        public async Task<AgentResponse> SimpleExecuteAsync(
            string userInput,
            float temperature = 0.7f,
            int maxTokens = 500)
        {
            await executionLock.WaitAsync();
            try
            {
                State = AgentState.Running;
                TotalRequests++;
                
                // 直接调用 LLM，不走 ReAct 循环，不累积对话历史
                LastPrompt = $"--- System Prompt ---\n{SystemPrompt}\n\n--- User Input ---\n{userInput}";
                
                string llmResponse = await Provider.SendMessageAsync(
                    SystemPrompt,
                    "", // gameState 已经嵌入在 userInput 中
                    userInput,
                    temperature,
                    maxTokens
                );
                
                LastResponseContent = llmResponse;
                
                if (string.IsNullOrEmpty(llmResponse))
                {
                    return HandleError("LLM returned empty response");
                }
                
                State = AgentState.Idle;
                SuccessfulRequests++;
                
                return new AgentResponse
                {
                    Success = true,
                    Content = llmResponse
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
        /// 执行 ReAct 循环（用于需要工具调用的 Agent）
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
                        // ⭐ 传递 Metadata (如好感度)
                        if (parsed.Metadata != null && parsed.Metadata.Count > 0)
                        {
                            // 这需要在循环中累积或覆盖，这里简单覆盖
                            // 实际上，我们应该把这些元数据传递给最终的 AgentResponse
                        }
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
                            // 强制引导模型进入下一步，防止纯思考循环
                            observations.Add("(系统提示: 你必须立即提供 [ACTION] 来使用工具，或者提供 [ANSWER] 来回复用户。)");
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
                    finalAnswer = observations.LastOrDefault() ?? "无法生成响应";
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
                
                // ⭐ 捕获最后一次解析的元数据
                var metadata = new Dictionary<string, object>();
                try
                {
                    var parsed = ParseLLMResponse(LastResponseContent);
                    if (parsed.Metadata != null) metadata = parsed.Metadata;
                }
                catch { }

                return new AgentResponse
                {
                    Success = true,
                    Content = finalAnswer,
                    Metadata = metadata
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
        /// 解析 LLM 响应 - 增强版 (优先支持 JSON，兼容 ReAct)
        /// </summary>
        private ParsedResponse ParseLLMResponse(string response)
        {
            var result = new ParsedResponse();

            // 1. ⭐ 优先尝试解析 JSON (TSS 3.0 Standard Format)
            try
            {
                // 提取 JSON 块 (如果存在 markdown 代码块)
                string jsonString = response;
                int jsonStart = response.IndexOf("{");
                int jsonEnd = response.LastIndexOf("}");
                
                // ⭐ JSON 容错处理：如果缺失开头的 { 但看起来像 JSON 内容 (e.g. "thought": ...)
                if (jsonStart == -1 && response.TrimStart().StartsWith("\""))
                {
                    Log.Warning($"[RimAgent] 检测到可能缺失 '{{' 的 JSON，尝试修复...");
                    string fixedJson = "{" + response;
                    if (!fixedJson.TrimEnd().EndsWith("}")) fixedJson += "}";
                    
                    jsonString = fixedJson;
                    // 重新计算索引以便后续处理（虽然这里直接用 fixedJson 即可）
                    jsonStart = 0;
                }
                else if (jsonStart >= 0 && jsonEnd > jsonStart)
                {
                    jsonString = response.Substring(jsonStart, jsonEnd - jsonStart + 1);
                }

                var payload = JsonConvert.DeserializeObject<AgentJsonPayload>(jsonString);
                if (payload != null)
                {
                    result.Thought = payload.thought;
                    result.Answer = payload.response;
                    
                    if (payload.action != null && !string.IsNullOrEmpty(payload.action.name))
                    {
                        result.ActionName = payload.action.name;
                        result.ActionParams = payload.action.parameters ?? new Dictionary<string, object>();
                    }
                    
                    // ⭐ 提取元数据
                    if (payload.affinity_impact != null)
                    {
                        result.Metadata["affinity_impact"] = payload.affinity_impact;
                    }
                    if (!string.IsNullOrEmpty(payload.emotion))
                    {
                        result.Metadata["emotion"] = payload.emotion;
                    }
                    
                    return result;
                }
            }
            catch (Exception)
            {
                // JSON 解析失败，回退到其他方法
            }

            // 2. 尝试使用 ReAct 正则解析 (兼容旧格式)
            bool reactMatched = false;
            
            // 尝试解析 [THOUGHT]:
            var thoughtMatch = ThoughtPattern.Match(response);
            if (thoughtMatch.Success)
            {
                result.Thought = thoughtMatch.Groups[1].Value.Trim();
                reactMatched = true;
            }
            
            // 尝试解析 [ACTION]:
            var actionMatch = ActionPattern.Match(response);
            if (actionMatch.Success)
            {
                result.ActionName = actionMatch.Groups[1].Value.Trim();
                string paramsString = actionMatch.Groups[2].Value.Trim();
                result.ActionParams = ParseParams(paramsString);
                reactMatched = true;
            }
            
            // 尝试解析 [ANSWER]:
            var answerMatch = AnswerPattern.Match(response);
            if (answerMatch.Success)
            {
                result.Answer = answerMatch.Groups[1].Value.Trim();
                reactMatched = true;
            }

            // 如果正则解析成功提取到了关键信息，直接返回
            if (reactMatched && (!string.IsNullOrEmpty(result.ActionName) || !string.IsNullOrEmpty(result.Answer)))
            {
                return result;
            }

            // 3. 第一道防线：尝试解析结构化 JSON (针对弱模型或 JSON 模式 - 旧版逻辑)
            var jsonCmd = NaturalLanguageParser.ParseFromLLMResponse(response);
            if (jsonCmd != null)
            {
                Log.Message($"[RimAgent] 解析到 JSON 命令 (Legacy): {jsonCmd.action}");
                result.ActionName = jsonCmd.action;
                result.ActionParams = ConvertParamsToDict(jsonCmd.parameters);
                if (string.IsNullOrEmpty(result.Thought)) result.Thought = "Parsed from JSON";
                return result;
            }

            // 4. 第二道防线：如果 JSON 解析失败，尝试 NLP 解析 (针对自然语言指令)
            var nlpCmd = NaturalLanguageParser.Parse(response);
            if (nlpCmd != null && nlpCmd.confidence > 0.6f)
            {
                Log.Message($"[RimAgent] JSON 格式错误，但通过 NLP 成功识别意图: {nlpCmd.action} (置信度: {nlpCmd.confidence:P0})");
                result.ActionName = nlpCmd.action;
                result.ActionParams = ConvertParamsToDict(nlpCmd.parameters);
                if (string.IsNullOrEmpty(result.Thought)) result.Thought = "Parsed from Natural Language";
                return result;
            }

            return result;
        }

        private Dictionary<string, object> ConvertParamsToDict(AdvancedCommandParams advancedParams)
        {
            var dict = new Dictionary<string, object>();
            if (advancedParams == null) return dict;

            // 使用 JSON 序列化再反序列化为字典，处理最全面
            try
            {
                var json = JsonConvert.SerializeObject(advancedParams);
                dict = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
            }
            catch
            {
                // 手动回退映射
                if (advancedParams.target != null) dict["target"] = advancedParams.target;
                if (advancedParams.scope != null) dict["scope"] = advancedParams.scope;
                if (advancedParams.count != null) dict["count"] = advancedParams.count;
                if (advancedParams.priority != null) dict["priority"] = advancedParams.priority;
            }
            
            return dict;
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
                        return result.Data?.ToString() ?? "工具执行成功";
                    }
                    else
                    {
                        return $"错误: {result.Error}";
                    }
                }
                
                // 回退到全局工具库
                var globalTool = RimAgentTools.GetTool(toolName);
                if (globalTool != null)
                {
                    var result = await globalTool.ExecuteAsync(parameters);
                    if (result.Success)
                    {
                        return result.Data?.ToString() ?? "工具执行成功";
                    }
                    else
                    {
                        return $"错误: {result.Error}";
                    }
                }
                
                return $"错误: 未找到工具 '{toolName}'";
            }
            catch (Exception ex)
            {
                Log.Error($"[RimAgent] {AgentId}: Tool execution error: {ex.Message}");
                return $"错误: {ex.Message}";
            }
        }

        // ========== 提示构建（内核能力） ==========
        
        /// <summary>
        /// 构建工具描述
        /// </summary>
        private string BuildToolsDescription()
        {
            if (tools.Count == 0)
                return "当前无可用工具。";
            
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("## 可用工具列表:");
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
                sb.AppendLine("## 历史对话摘要（仅供参考）:");
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
                sb.AppendLine("## 最近对话记录:");
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
            sb.AppendLine("请将以下对话内容总结为一个简洁的段落，保留关键信息。直接输出总结内容，不要包含任何前缀或解释。");
            if (!string.IsNullOrEmpty(Summary))
            {
                sb.AppendLine($"已有总结: {Summary}");
            }
            sb.AppendLine("\n需要合并的新对话:");
            foreach (var msg in messagesToCompress)
            {
                sb.AppendLine($"[{msg.Role}]: {msg.Content}");
            }

            try
            {
                // 调用 LLM 生成摘要 (使用较小的 maxTokens)
                string newSummary = await Provider.SendMessageAsync(
                    "你是一个专业的对话总结助手。请用简体中文简要总结对话内容，保留关键信息。",
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
            // ⭐ v2.3.0: 移除硬编码的格式指令，完全由 System Prompt 控制输出格式 (JSON/ReAct)
            // 这解决了与 OutputFormat_Structure.txt 的冲突
            // sb.AppendLine("You are a ReAct agent. Think step by step and use tools when needed.");
            
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
                sb.AppendLine("## 已执行步骤:");
                foreach (var obs in observations)
                {
                    sb.AppendLine(obs);
                }
                sb.AppendLine();
            }
            
            // 用户输入
            sb.AppendLine($"## 用户请求:");
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
        /// 释放资源并从全局列表移除
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        
        /// <summary>
        /// 释放资源
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                // 从全局列表移除
                ActiveAgents.Remove(this);
                
                // 释放信号量
                executionLock?.Dispose();
                
                // 清理历史
                ConversationHistory.Clear();
                tools.Clear();
                
                Log.Message($"[RimAgent] {AgentId}: Disposed and removed from ActiveAgents");
            }
        }
        
        /// <summary>
        /// 析构函数（防止资源泄漏）
        /// </summary>
        ~RimAgent()
        {
            Dispose(false);
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
            public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();
        }

        // ⭐ v3.0: 标准 JSON Payload 定义
        private class AgentJsonPayload
        {
            public string thought { get; set; }
            public AgentActionPayload action { get; set; }
            public string response { get; set; }
            public string emotion { get; set; } // ⭐ v3.2: 捕获情绪标签
            public object affinity_impact { get; set; } // ⭐ v3.1: 捕获好感度影响数据
        }

        private class AgentActionPayload
        {
            public string name { get; set; }
            public Dictionary<string, object> parameters { get; set; }
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
        
        /// <summary>
        /// ⭐ v3.1: 附加元数据 (如好感度变化、意图等)
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();
    }
}
