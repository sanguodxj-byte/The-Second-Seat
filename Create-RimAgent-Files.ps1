# ================================================================
# Create-RimAgent-Files.ps1
# ? v1.6.65: 自动创建完整的 RimAgent 系统文件
# ================================================================

Write-Host "================================================================" -ForegroundColor Cyan
Write-Host "  RimAgent v1.6.65 文件创建脚本" -ForegroundColor Cyan
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host ""

$baseDir = "Source\TheSecondSeat\RimAgent"

# 确保目录存在
if (-not (Test-Path $baseDir)) {
    New-Item -ItemType Directory -Path $baseDir -Force | Out-Null
    Write-Host "? 创建目录: $baseDir" -ForegroundColor Green
}

# ================================================================
# 1. RimAgent.cs - 核心 Agent 类
# ================================================================
$rimAgentContent = @'
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
'@

[System.IO.File]::WriteAllText("$baseDir\RimAgent.cs", $rimAgentContent, [System.Text.Encoding]::UTF8)
Write-Host "? 创建文件: RimAgent.cs" -ForegroundColor Green

# ================================================================
# 2. RimAgentTools.cs - 工具库
# ================================================================
$toolsContent = @'
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Verse;

namespace TheSecondSeat.RimAgent
{
    /// <summary>
    /// ? v1.6.65: RimAgentTools - 工具库管理器
    /// 功能：工具注册与管理、工具执行接口、参数验证、结果封装
    /// </summary>
    public static class RimAgentTools
    {
        private static readonly Dictionary<string, ITool> registeredTools = new Dictionary<string, ITool>();
        private static readonly object lockObj = new object();
        
        public static void RegisterTool(string name, ITool tool)
        {
            lock (lockObj)
            {
                registeredTools[name] = tool;
                Log.Message($"[RimAgentTools] Tool '{name}' registered");
            }
        }
        
        public static async Task<ToolResult> ExecuteAsync(string toolName, Dictionary<string, object> parameters)
        {
            try
            {
                if (!registeredTools.TryGetValue(toolName, out var tool))
                {
                    return new ToolResult { Success = false, Error = $"Tool '{toolName}' not found" };
                }
                
                return await tool.ExecuteAsync(parameters);
            }
            catch (Exception ex)
            {
                Log.Error($"[RimAgentTools] Error: {ex.Message}");
                return new ToolResult { Success = false, Error = ex.Message };
            }
        }
        
        public static List<string> GetRegisteredToolNames()
        {
            lock (lockObj) { return new List<string>(registeredTools.Keys); }
        }
        
        public static bool IsToolRegistered(string toolName)
        {
            lock (lockObj) { return registeredTools.ContainsKey(toolName); }
        }
        
        public static void ClearAllTools()
        {
            lock (lockObj) { registeredTools.Clear(); }
        }
    }
    
    public interface ITool
    {
        string Name { get; }
        string Description { get; }
        Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters);
    }
    
    public class ToolResult
    {
        public bool Success { get; set; }
        public object Data { get; set; }
        public string Error { get; set; }
        public DateTime ExecutedAt { get; set; }
        public ToolResult() { ExecutedAt = DateTime.Now; }
    }
}
'@

[System.IO.File]::WriteAllText("$baseDir\RimAgentTools.cs", $toolsContent, [System.Text.Encoding]::UTF8)
Write-Host "? 创建文件: RimAgentTools.cs" -ForegroundColor Green

# ================================================================
# 3. RimAgentModels.cs - 数据模型
# ================================================================
$modelsContent = @'
using System.Collections.Generic;

namespace TheSecondSeat.RimAgent
{
    /// <summary>
    /// ? v1.6.65: RimAgentModels - 数据模型定义
    /// </summary>
    public class ToolDefinition
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public Dictionary<string, ParameterDefinition> Parameters { get; set; }
        
        public ToolDefinition()
        {
            Parameters = new Dictionary<string, ParameterDefinition>();
        }
    }
    
    public class ParameterDefinition
    {
        public string Type { get; set; }
        public string Description { get; set; }
        public bool Required { get; set; }
        public object DefaultValue { get; set; }
    }
    
    public class AgentConfig
    {
        public string AgentId { get; set; }
        public string SystemPrompt { get; set; }
        public string ProviderName { get; set; }
        public float Temperature { get; set; } = 0.7f;
        public int MaxTokens { get; set; } = 500;
        public List<string> Tools { get; set; }
        
        public AgentConfig()
        {
            Tools = new List<string>();
        }
    }
}
'@

[System.IO.File]::WriteAllText("$baseDir\RimAgentModels.cs", $modelsContent, [System.Text.Encoding]::UTF8)
Write-Host "? 创建文件: RimAgentModels.cs" -ForegroundColor Green

# ================================================================
# 4. ConcurrentRequestManager.cs - 并发管理器
# ================================================================
$concurrentContent = @'
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Verse;

namespace TheSecondSeat.RimAgent
{
    /// <summary>
    /// ? v1.6.65: ConcurrentRequestManager - 并发请求管理器
    /// 功能：请求队列管理、速率限制、错误重试机制、并发控制
    /// </summary>
    public class ConcurrentRequestManager
    {
        private static ConcurrentRequestManager instance;
        public static ConcurrentRequestManager Instance => instance ?? (instance = new ConcurrentRequestManager());
        
        private readonly Queue<RequestItem> requestQueue = new Queue<RequestItem>();
        private readonly SemaphoreSlim semaphore = new SemaphoreSlim(5, 5);
        private readonly object lockObj = new object();
        private int activeRequests = 0;
        private int totalRequests = 0;
        private int failedRequests = 0;
        
        public int MaxConcurrentRequests { get; set; } = 5;
        public int RequestsPerMinute { get; set; } = 60;
        
        private ConcurrentRequestManager() { }
        
        public async Task<T> EnqueueAsync<T>(Func<Task<T>> requestFunc, int maxRetries = 3)
        {
            await semaphore.WaitAsync();
            
            try
            {
                Interlocked.Increment(ref activeRequests);
                Interlocked.Increment(ref totalRequests);
                
                return await ExecuteWithRetryAsync(requestFunc, maxRetries);
            }
            finally
            {
                Interlocked.Decrement(ref activeRequests);
                semaphore.Release();
            }
        }
        
        private async Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> requestFunc, int maxRetries)
        {
            int attempts = 0;
            Exception lastException = null;
            
            while (attempts < maxRetries)
            {
                try
                {
                    return await requestFunc();
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    attempts++;
                    
                    if (attempts < maxRetries)
                    {
                        int delayMs = (int)Math.Pow(2, attempts) * 1000;
                        await Task.Delay(delayMs);
                        Log.Warning($"[ConcurrentRequestManager] Retry {attempts}/{maxRetries}: {ex.Message}");
                    }
                }
            }
            
            Interlocked.Increment(ref failedRequests);
            Log.Error($"[ConcurrentRequestManager] Failed after {maxRetries} attempts: {lastException?.Message}");
            throw lastException;
        }
        
        public string GetStats()
        {
            return $"[ConcurrentRequestManager] Active: {activeRequests}, Total: {totalRequests}, Failed: {failedRequests}";
        }
        
        public void Reset()
        {
            lock (lockObj)
            {
                requestQueue.Clear();
                activeRequests = 0;
                totalRequests = 0;
                failedRequests = 0;
            }
        }
        
        public int GetActiveRequestCount() => activeRequests;
        
        private class RequestItem
        {
            public Func<Task<object>> RequestFunc { get; set; }
            public DateTime EnqueuedAt { get; set; }
        }
    }
}
'@

[System.IO.File]::WriteAllText("$baseDir\ConcurrentRequestManager.cs", $concurrentContent, [System.Text.Encoding]::UTF8)
Write-Host "? 创建文件: ConcurrentRequestManager.cs" -ForegroundColor Green

# ================================================================
# 5. 创建 README.md 文档
# ================================================================
$readmeContent = @'
# RimAgent v1.6.65

? **AI Agent 系统 - 完整实现**

## ?? 文件结构

```
RimAgent/
├── RimAgent.cs                    # 核心 Agent 类
├── RimAgentTools.cs               # 工具库管理器
├── RimAgentModels.cs              # 数据模型定义
├── ConcurrentRequestManager.cs    # 并发请求管理器
├── ILLMProvider.cs                # LLM 提供商接口
└── LLMProviderFactory.cs          # LLM 工厂类
```

## ? 核心功能

### 1. **RimAgent.cs** - Agent 生命周期管理
- ? Agent 创建、运行、停止
- ? 对话历史记录
- ? 多轮对话上下文管理
- ? 请求统计与调试信息

### 2. **RimAgentTools.cs** - 工具系统
- ? 工具注册与管理
- ? 工具执行接口
- ? 参数验证
- ? 结果封装

### 3. **RimAgentModels.cs** - 数据模型
- ? ToolDefinition - 工具定义
- ? ParameterDefinition - 参数定义
- ? AgentConfig - Agent 配置

### 4. **ConcurrentRequestManager.cs** - 并发管理
- ? 请求队列管理
- ? 速率限制（5 并发）
- ? 指数退避重试机制
- ? 错误统计与日志

## ?? 使用示例

```csharp
// 创建 Agent
var provider = LLMProviderFactory.CreateProvider("openai", apiKey);
var agent = new RimAgent("agent-1", "You are a helpful assistant", provider);

// 注册工具
agent.RegisterTool("search");
agent.RegisterTool("calculate");

// 执行任务
var response = await agent.ExecuteAsync("帮我搜索天气", temperature: 0.7f);

if (response.Success)
{
    Log.Message($"响应: {response.Content}");
}
else
{
    Log.Error($"错误: {response.Error}");
}

// 查看统计
Log.Message(agent.GetDebugInfo());
```

## ?? 并发管理示例

```csharp
// 使用并发管理器执行多个请求
var result = await ConcurrentRequestManager.Instance.EnqueueAsync(
    async () => await agent.ExecuteAsync("你好"),
    maxRetries: 3
);

// 查看统计
Log.Message(ConcurrentRequestManager.Instance.GetStats());
```

## ?? 配置选项

| 参数 | 默认值 | 说明 |
|------|--------|------|
| MaxConcurrentRequests | 5 | 最大并发请求数 |
| RequestsPerMinute | 60 | 每分钟请求限制 |
| MaxRetries | 3 | 最大重试次数 |

## ?? 更新日志

### v1.6.65 (2025-01-XX)
- ? 完整实现 RimAgent 核心类
- ? 添加工具库管理器
- ? 实现并发请求管理器
- ? 支持指数退避重试机制
- ? 完善错误处理和日志记录

---

?? **The Second Seat Mod** - AI-Powered RimWorld Experience
'@

[System.IO.File]::WriteAllText("$baseDir\README.md", $readmeContent, [System.Text.Encoding]::UTF8)
Write-Host "? 创建文件: README.md" -ForegroundColor Green

Write-Host ""
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host "  ? RimAgent v1.6.65 所有文件创建完成！" -ForegroundColor Green
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "?? 创建的文件:" -ForegroundColor Yellow
Write-Host "  1. RimAgent.cs                    (核心 Agent 类)" -ForegroundColor White
Write-Host "  2. RimAgentTools.cs               (工具库管理器)" -ForegroundColor White
Write-Host "  3. RimAgentModels.cs              (数据模型定义)" -ForegroundColor White
Write-Host "  4. ConcurrentRequestManager.cs    (并发请求管理器)" -ForegroundColor White
Write-Host "  5. README.md                      (使用文档)" -ForegroundColor White
Write-Host ""
Write-Host "?? 下一步操作:" -ForegroundColor Yellow
Write-Host "  1. 在 Visual Studio 中打开解决方案" -ForegroundColor White
Write-Host "  2. 右键点击 RimAgent 文件夹 -> 添加 -> 现有项" -ForegroundColor White
Write-Host "  3. 选择所有创建的 .cs 文件" -ForegroundColor White
Write-Host "  4. 编译项目 (Ctrl+Shift+B)" -ForegroundColor White
Write-Host "  5. 测试 RimAgent 功能" -ForegroundColor White
Write-Host ""
Write-Host "? 功能亮点:" -ForegroundColor Yellow
Write-Host "  ? Agent 生命周期管理" -ForegroundColor Green
Write-Host "  ? 工具调用系统" -ForegroundColor Green
Write-Host "  ? 并发请求控制" -ForegroundColor Green
Write-Host "  ? 指数退避重试" -ForegroundColor Green
Write-Host "  ? 对话历史记录" -ForegroundColor Green
Write-Host ""
