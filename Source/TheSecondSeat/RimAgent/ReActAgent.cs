using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using RimWorld;
using Verse;
using TheSecondSeat.RimAgent.Tools;
using TheSecondSeat.Monitoring;
using TheSecondSeat.Integration;
using TheSecondSeat.Commands; // 引入命名空间

namespace TheSecondSeat.RimAgent
{
    /// <summary>
    /// ⭐ v1.9.0: ReAct (Reason + Act) 模式的 Agent
    /// 
    /// 核心理念：
    /// 1. Pull 模式：只给 AI 极简信息，让它主动调用工具获取数据
    /// 2. 多轮推理：Thought -> Action -> Observation 循环
    /// 3. Token 节省：减少 60-80% 的 Token 消耗
    /// 
    /// 对话格式：
    /// - AI 输出 [THOUGHT]: 思考过程
    /// - AI 输出 [ACTION]: tool_name(param1, param2)
    /// - 系统注入 [OBSERVATION]: 工具返回结果
    /// - 如果 AI 输出 [ANSWER]: 表示最终回答
    /// </summary>
    public class ReActAgent
    {
        // ========== 配置 ==========
        private const int MAX_ITERATIONS = 10;         // 最大推理循环次数 (增加以支持更复杂的任务)
        private const int MAX_HISTORY_LENGTH = 15;     // 最大历史长度
        
        // ========== 核心组件 ==========
        public string AgentId { get; private set; }
        public ILLMProvider Provider { get; private set; }
        private Dictionary<string, ITool> tools = new Dictionary<string, ITool>();
        
        // ========== 状态 ==========
        public AgentState State { get; private set; } = AgentState.Idle;
        private List<ReActMessage> history = new List<ReActMessage>();
        private List<ActionResult> lastActionResults = new List<ActionResult>();
        
        // ========== 正则表达式 ==========
        private static readonly Regex ThoughtPattern = new Regex(@"\[THOUGHT\]:\s*(.+?)(?=\[ACTION\]|\[ANSWER\]|$)", RegexOptions.Singleline);
        // ⭐ 修复：使用非贪婪匹配 (.*?) 避免吞噬后续内容，配合 SplitParams 处理参数
        private static readonly Regex ActionPattern = new Regex(@"\[ACTION\]:\s*(\w+)\((.*?)\)");
        private static readonly Regex AnswerPattern = new Regex(@"\[ANSWER\]:\s*(.+)$", RegexOptions.Singleline);
        
        // ========== 系统提示词模板 (Fallback) ==========
        private const string REACT_SYSTEM_PROMPT_FALLBACK = @"你是一个智能AI助手，使用ReAct模式进行推理。

你可以使用以下工具：
{TOOLS}

相关记忆与知识：
{MEMORY}

输出格式规则：
1. 如果需要思考，输出 [THOUGHT]: 你的思考过程
2. 如果需要使用工具，输出 [ACTION]: tool_name(arg1, arg2, key=value)
   - 支持位置参数：直接写值
   - 支持命名参数：key=value (例如: limit=10, radius=5)
3. 当你准备好回答时，输出 [ANSWER]: 你的最终回答

示例1 (基础查询)：
用户：检查殖民地状态
[THOUGHT]: 用户想了解殖民地状态，我需要查询游戏数据
[ACTION]: get_colony_state()
[OBSERVATION]: {""colonists"":8,""mood"":0.65,""combat"":false,""food"":450}
[ANSWER]: 殖民地状况良好！目前有8名殖民者，平均心情65%，储备食物450份。

示例2 (复杂命令)：
用户：让大家去砍伐附近的树木，限制5个
[THOUGHT]: 我需要使用command工具执行批量砍伐命令
[ACTION]: command(BatchChopWood, null, limit=5, radius=20)
[OBSERVATION]: 已发布砍伐命令，目标数: 5
[ANSWER]: 好的，已安排砍伐附近的5棵树。

重要：
- 每次只能执行一个ACTION
- 工具返回结果会作为OBSERVATION注入
- command工具的参数格式：command(ActionName, TargetName, key=value...)
  - 第1个参数是 ActionName (如 BatchMine, MovePawn)
  - 第2个参数是 TargetName (如果没有特定目标可用 null 或 "")
  - 后续参数使用 key=value 格式指定详细参数

{PERSONA}";

        public ReActAgent(string agentId, ILLMProvider provider)
        {
            AgentId = agentId;
            Provider = provider;
            
            // 注册默认工具
            RegisterDefaultTools();
        }
        
        /// <summary>
        /// 注册默认工具
        /// </summary>
        private void RegisterDefaultTools()
        {
            RegisterTool(new GetColonyStateTool());
            RegisterTool(new GetInventoryTool());
            RegisterTool(new GetColonistsTool());
            RegisterTool(new CheckThreatsTool());
            RegisterTool(new LogReaderTool());
            // ⭐ 修复：注册命令工具，赋予 Agent 执行能力
            RegisterTool(new CommandTool());
        }
        
        /// <summary>
        /// 注册自定义工具
        /// </summary>
        public void RegisterTool(ITool tool)
        {
            if (tool == null) return;
            tools[tool.Name] = tool;
        }

        /// <summary>
        /// 获取系统提示词模板
        /// </summary>
        private string GetSystemPromptTemplate()
        {
            try
            {
                var def = DefDatabase<TheSecondSeat.Defs.AgentPromptDef>.GetNamedSilentFail("TSS_ReActSystemPrompt");
                if (def != null && !string.IsNullOrEmpty(def.text))
                {
                    return def.text;
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[ReActAgent] Failed to load prompt from XML: {ex.Message}. Using fallback.");
            }
            
            return REACT_SYSTEM_PROMPT_FALLBACK;
        }
        
        /// <summary>
        /// ⭐ 核心方法：执行 ReAct 推理循环
        /// </summary>
        public async Task<ReActResponse> ExecuteAsync(string userMessage, string personaContext = "")
        {
            if (State == AgentState.Running)
            {
                return new ReActResponse { Success = false, Error = "Agent is busy" };
            }
            
            try
            {
                State = AgentState.Running;
                lastActionResults.Clear();
                
                // 构建工具描述
                string toolsDescription = BuildToolsDescription();

                // 获取相关记忆
                string memoryContext = MemoryContextBuilder.BuildMemoryContext(userMessage);
                
                // 获取系统提示模板
                string promptTemplate = GetSystemPromptTemplate();

                // 构建系统提示
                string systemPrompt = promptTemplate
                    .Replace("{TOOLS}", toolsDescription)
                    .Replace("{MEMORY}", memoryContext)
                    .Replace("{PERSONA}", personaContext);
                
                // 添加用户消息到历史
                AddToHistory("user", userMessage);
                
                // ReAct 循环
                string finalAnswer = null;
                int iterations = 0;
                
                while (iterations < MAX_ITERATIONS)
                {
                    iterations++;
                    
                    // 构建完整上下文
                    string fullContext = BuildContext();
                    
                    // 调用 LLM
                    string llmResponse = await Provider.SendMessageAsync(
                        systemPrompt, 
                        "", // 不推送大量 gameState
                        fullContext, 
                        0.7f, 
                        500
                    );
                    
                    // 解析响应
                    var parsed = ParseLLMResponse(llmResponse);
                    
                    // 记录思考过程
                    if (!string.IsNullOrEmpty(parsed.Thought))
                    {
                        AddToHistory("assistant", $"[THOUGHT]: {parsed.Thought}");
                    }
                    
                    // 检查是否有最终答案
                    if (!string.IsNullOrEmpty(parsed.Answer))
                    {
                        finalAnswer = parsed.Answer;
                        AddToHistory("assistant", $"[ANSWER]: {parsed.Answer}");
                        break;
                    }
                    
                    // 执行工具调用
                    if (!string.IsNullOrEmpty(parsed.ActionName))
                    {
                        AddToHistory("assistant", $"[ACTION]: {parsed.ActionName}({string.Join(", ", parsed.ActionParams)})");
                        
                        string observation = await ExecuteToolAsync(parsed.ActionName, parsed.ActionParams);
                        AddToHistory("system", $"[OBSERVATION]: {observation}");
                        
                        lastActionResults.Add(new ActionResult
                        {
                            ToolName = parsed.ActionName,
                            Result = observation,
                            Success = !observation.StartsWith("Error:")
                        });
                    }
                    else if (string.IsNullOrEmpty(parsed.Answer))
                    {
                        // 没有动作也没有答案，强制结束
                        finalAnswer = llmResponse;
                        break;
                    }
                }
                
                State = AgentState.Idle;

                // 记录对话到记忆系统
                if (!string.IsNullOrEmpty(finalAnswer))
                {
                    MemoryContextBuilder.RecordConversation("Player", userMessage);
                    MemoryContextBuilder.RecordConversation(AgentId, finalAnswer);
                }
                
                return new ReActResponse
                {
                    Success = true,
                    FinalAnswer = finalAnswer ?? "无法生成回答",
                    Iterations = iterations,
                    ActionResults = new List<ActionResult>(lastActionResults)
                };
            }
            catch (Exception ex)
            {
                State = AgentState.Error;
                Log.Error($"[ReActAgent] {AgentId}: {ex.Message}");
                return new ReActResponse { Success = false, Error = ex.Message };
            }
        }
        
        /// <summary>
        /// 构建工具描述
        /// </summary>
        private string BuildToolsDescription()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var tool in tools.Values)
            {
                sb.AppendLine($"- {tool.Name}: {tool.Description}");
                
                // ⭐ 注入具体的命令列表
                if (tool.Name == "command")
                {
                    sb.AppendLine("  可用命令列表:");
                    // 调用 CommandToolLibrary 生成精简列表
                    string commandList = CommandToolLibrary.GenerateCompactCommandList();
                    // 缩进处理，使其看起来像 command 工具的子项
                    var lines = commandList.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var line in lines)
                    {
                        sb.AppendLine($"  {line}");
                    }
                }
            }
            return sb.ToString();
        }
        
        /// <summary>
        /// 构建上下文（只包含最近的历史）
        /// </summary>
        private string BuildContext()
        {
            var sb = new System.Text.StringBuilder();
            
            // 只取最近的历史
            int start = Math.Max(0, history.Count - MAX_HISTORY_LENGTH);
            for (int i = start; i < history.Count; i++)
            {
                var msg = history[i];
                sb.AppendLine(msg.Content);
            }
            
            return sb.ToString();
        }
        
        /// <summary>
        /// 解析 LLM 响应
        /// </summary>
        private ParsedResponse ParseLLMResponse(string response)
        {
            var result = new ParsedResponse();
            
            // 解析思考
            var thoughtMatch = ThoughtPattern.Match(response);
            if (thoughtMatch.Success)
            {
                result.Thought = thoughtMatch.Groups[1].Value.Trim();
            }
            
            // 解析动作
            var actionMatch = ActionPattern.Match(response);
            if (actionMatch.Success)
            {
                result.ActionName = actionMatch.Groups[1].Value.Trim();
                string paramsStr = actionMatch.Groups[2].Value.Trim();
                
                if (!string.IsNullOrEmpty(paramsStr))
                {
                    // ⭐ 修复：使用正则分割参数，忽略引号内的逗号
                    // 解决 command(TriggerEvent, raid, comment="Let's fight, warrior!") 被错误分割的问题
                    string[] rawParams = Regex.Split(paramsStr, ",(?=(?:[^\"]*\"[^\"]*\")*[^\"]*$)");
                    
                    result.ActionParams = new string[rawParams.Length];
                    for (int i = 0; i < rawParams.Length; i++)
                    {
                        result.ActionParams[i] = rawParams[i].Trim().Trim('"', '\'');
                    }
                }
            }
            
            // 解析答案
            var answerMatch = AnswerPattern.Match(response);
            if (answerMatch.Success)
            {
                result.Answer = answerMatch.Groups[1].Value.Trim();
            }
            
            return result;
        }
        
        /// <summary>
        /// 执行工具调用
        /// </summary>
        private async Task<string> ExecuteToolAsync(string toolName, string[] parameters)
        {
            try
            {
                if (!tools.TryGetValue(toolName, out var tool))
                {
                    return $"Error: Unknown tool '{toolName}'";
                }
                
                // 转换参数: string[] -> Dictionary<string, object>
                // 支持 key=value 格式和位置参数智能映射
                var paramDict = new Dictionary<string, object>();
                var positionalParams = new List<string>();

                // 1. 第一遍：解析所有参数，分离命名参数和位置参数
                foreach (var param in parameters)
                {
                    var trimmedParam = param.Trim();
                    if (string.IsNullOrEmpty(trimmedParam)) continue;

                    if (trimmedParam.Contains("="))
                    {
                        var parts = trimmedParam.Split(new[] { '=' }, 2);
                        string key = parts[0].Trim();
                        string value = parts[1].Trim().Trim('"', '\'');
                        paramDict[key] = value;
                    }
                    else
                    {
                        positionalParams.Add(trimmedParam.Trim('"', '\''));
                    }
                }

                // 2. 特殊处理 command 工具的位置参数映射
                if (toolName == "command")
                {
                    // 尝试从位置参数或命名参数中获取 action
                    string actionName = "";
                    
                    if (positionalParams.Count > 0)
                    {
                        actionName = positionalParams[0];
                        paramDict["action"] = actionName;
                    }
                    else if (paramDict.ContainsKey("action"))
                    {
                        actionName = paramDict["action"].ToString();
                    }

                    // 如果有第2个位置参数，设为 target
                    if (positionalParams.Count > 1)
                    {
                        string targetVal = positionalParams[1];
                        if (targetVal != "null" && targetVal != "")
                        {
                            paramDict["target"] = targetVal;
                        }
                    }

                    // 映射剩余的位置参数 (index >= 2)
                    if (!string.IsNullOrEmpty(actionName) && positionalParams.Count > 2)
                    {
                        var cmdDef = CommandToolLibrary.GetCommand(actionName);
                        if (cmdDef != null)
                        {
                            for (int i = 2; i < positionalParams.Count; i++)
                            {
                                int paramIndex = i - 2;
                                if (paramIndex < cmdDef.parameters.Count)
                                {
                                    string paramName = cmdDef.parameters[paramIndex].name;
                                    // 仅当该命名参数未被设置时才使用位置参数覆盖
                                    if (!paramDict.ContainsKey(paramName))
                                    {
                                        paramDict[paramName] = positionalParams[i];
                                    }
                                }
                                else
                                {
                                    paramDict[$"arg{i}"] = positionalParams[i];
                                }
                            }
                        }
                        else
                        {
                            // 未知命令，保留 argN
                            for (int i = 2; i < positionalParams.Count; i++)
                            {
                                paramDict[$"arg{i}"] = positionalParams[i];
                            }
                        }
                    }
                }
                else
                {
                    // 普通工具：直接映射 argN
                    for (int i = 0; i < positionalParams.Count; i++)
                    {
                        paramDict[$"arg{i}"] = positionalParams[i];
                    }
                }
                
                var result = await tool.ExecuteAsync(paramDict);
                
                if (result.Success)
                {
                    if (result.Data is string strData) return strData;
                    return Newtonsoft.Json.JsonConvert.SerializeObject(result.Data);
                }
                else
                {
                    return $"Error: {result.Error}";
                }
            }
            catch (Exception ex)
            {
                return $"Error: {ex.Message}";
            }
        }
        
        /// <summary>
        /// 添加消息到历史
        /// </summary>
        private void AddToHistory(string role, string content)
        {
            history.Add(new ReActMessage
            {
                Role = role,
                Content = content,
                Timestamp = DateTime.Now
            });
            
            // 限制历史长度
            while (history.Count > MAX_HISTORY_LENGTH * 2)
            {
                history.RemoveAt(0);
            }
        }
        
        /// <summary>
        /// 清除历史
        /// </summary>
        public void ClearHistory()
        {
            history.Clear();
            lastActionResults.Clear();
        }
    }
    
    /// <summary>
    /// ReAct 消息
    /// </summary>
    public class ReActMessage
    {
        public string Role { get; set; }
        public string Content { get; set; }
        public DateTime Timestamp { get; set; }
    }
    
    /// <summary>
    /// ReAct 响应
    /// </summary>
    public class ReActResponse
    {
        public bool Success { get; set; }
        public string FinalAnswer { get; set; }
        public string Error { get; set; }
        public int Iterations { get; set; }
        public List<ActionResult> ActionResults { get; set; } = new List<ActionResult>();
    }
    
    /// <summary>
    /// 动作结果
    /// </summary>
    public class ActionResult
    {
        public string ToolName { get; set; }
        public string Result { get; set; }
        public bool Success { get; set; }
    }
    
    /// <summary>
    /// 解析结果
    /// </summary>
    internal class ParsedResponse
    {
        public string Thought { get; set; }
        public string ActionName { get; set; }
        public string[] ActionParams { get; set; } = new string[0];
        public string Answer { get; set; }
    }
    
    // ========== ReAct 专用工具 (已废弃，使用 RimAgent.Tools.ITool) ==========
    
    /// <summary>
    /// 获取殖民地状态工具
    /// </summary>
    public class GetColonyStateTool : ITool
    {
        public string Name => "get_colony_state";
        public string Description => "获取殖民地基本状态（殖民者数量、心情、战斗状态、食物）";
        
        public Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
        {
            try
            {
                string result = "";
                var observer = Current.Game?.GetComponent<GameStateObserver>();
                if (observer != null)
                {
                    result = observer.GetCompactStateJson();
                }
                else
                {
                    // 降级：直接查询
                    var map = Find.CurrentMap;
                    if (map == null) result = "{\"error\":\"no_map\"}";
                    else
                    {
                        int colonists = map.mapPawns.FreeColonistsSpawnedCount;
                        result = $"{{\"colonists\":{colonists}}}";
                    }
                }
                return Task.FromResult(new ToolResult { Success = true, Data = result });
            }
            catch (Exception ex)
            {
                return Task.FromResult(new ToolResult { Success = false, Error = ex.Message });
            }
        }
    }
    
    /// <summary>
    /// 获取库存工具
    /// </summary>
    public class GetInventoryTool : ITool
    {
        public string Name => "get_inventory";
        public string Description => "获取关键物资库存（食物、药品、钢铁等）";
        
        public Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
        {
            try
            {
                var map = Find.CurrentMap;
                if (map == null) return Task.FromResult(new ToolResult { Success = false, Error = "no_map" });
                
                // 简化查询，只返回关键数据
                int food = map.resourceCounter.GetCount(RimWorld.ThingDefOf.MealSimple) +
                           map.resourceCounter.GetCount(RimWorld.ThingDefOf.MealFine);
                int medicine = map.resourceCounter.GetCount(RimWorld.ThingDefOf.MedicineHerbal) +
                               map.resourceCounter.GetCount(RimWorld.ThingDefOf.MedicineIndustrial);
                int steel = map.resourceCounter.GetCount(RimWorld.ThingDefOf.Steel);
                
                string json = $"{{\"food\":{food},\"medicine\":{medicine},\"steel\":{steel}}}";
                return Task.FromResult(new ToolResult { Success = true, Data = json });
            }
            catch (Exception ex)
            {
                return Task.FromResult(new ToolResult { Success = false, Error = ex.Message });
            }
        }
    }
    
    /// <summary>
    /// 获取殖民者列表工具
    /// </summary>
    public class GetColonistsTool : ITool
    {
        public string Name => "get_colonists";
        public string Description => "获取殖民者列表及状态";
        
        public Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
        {
            try
            {
                var map = Find.CurrentMap;
                if (map == null) return Task.FromResult(new ToolResult { Success = true, Data = "[]" });
                
                var sb = new System.Text.StringBuilder("[");
                bool first = true;
                
                foreach (var pawn in map.mapPawns.FreeColonistsSpawned)
                {
                    if (!first) sb.Append(",");
                    first = false;
                    
                    float mood = pawn.needs?.mood?.CurLevelPercentage ?? 0;
                    float health = pawn.health?.summaryHealth?.SummaryHealthPercent ?? 1;
                    string job = pawn.CurJob?.def?.defName ?? "Idle";
                    
                    sb.Append($"{{\"name\":\"{pawn.LabelShort}\",\"mood\":{mood:F2},\"health\":{health:F2},\"job\":\"{job}\"}}");
                }
                
                sb.Append("]");
                return Task.FromResult(new ToolResult { Success = true, Data = sb.ToString() });
            }
            catch (Exception ex)
            {
                return Task.FromResult(new ToolResult { Success = false, Error = ex.Message });
            }
        }
    }
    
    /// <summary>
    /// 检查威胁工具
    /// </summary>
    public class CheckThreatsTool : ITool
    {
        public string Name => "check_threats";
        public string Description => "检查当前威胁（敌人、火灾等）";
        
        public Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
        {
            try
            {
                var map = Find.CurrentMap;
                if (map == null) return Task.FromResult(new ToolResult { Success = true, Data = "{\"threats\":[]}" });
                
                var threats = new List<string>();
                
                // 检查敌人
                int enemies = 0;
                foreach (var p in map.mapPawns.AllPawnsSpawned)
                {
                    if (p.HostileTo(Faction.OfPlayer)) enemies++;
                }
                if (enemies > 0)
                {
                    threats.Add($"敌人:{enemies}");
                }
                
                // 检查火灾
                int fires = map.listerThings.ThingsOfDef(RimWorld.ThingDefOf.Fire).Count;
                if (fires > 0)
                {
                    threats.Add($"火灾:{fires}");
                }
                
                string json = $"{{\"active\":{(threats.Count > 0 ? "true" : "false")},\"threats\":[\"{string.Join("\",\"", threats)}\"]}}";
                return Task.FromResult(new ToolResult { Success = true, Data = json });
            }
            catch (Exception ex)
            {
                return Task.FromResult(new ToolResult { Success = false, Error = ex.Message });
            }
        }
    }
}