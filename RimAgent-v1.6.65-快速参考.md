# ? RimAgent v1.6.65 快速参考

## ?? 核心组件

```
RimAgent/
├── RimAgent.cs                    ← Agent 核心类
├── RimAgentTools.cs               ← 工具库
├── RimAgentModels.cs              ← 数据模型
├── ConcurrentRequestManager.cs    ← 并发管理
├── ILLMProvider.cs                ← LLM 接口
└── LLMProviderFactory.cs          ← LLM 工厂
```

---

## ?? 快速开始

### 1?? 创建 Agent
```csharp
var provider = LLMProviderFactory.GetProvider("openai");
var agent = new RimAgent("my-agent", "You are helpful", provider);
```

### 2?? 注册工具
```csharp
agent.RegisterTool("search");
agent.RegisterTool("analyze");
```

### 3?? 执行任务
```csharp
var response = await agent.ExecuteAsync("你好", temperature: 0.7f);
if (response.Success) Log.Message(response.Content);
```

### 4?? 使用并发管理
```csharp
var result = await ConcurrentRequestManager.Instance.EnqueueAsync(
    async () => await agent.ExecuteAsync("测试"),
    maxRetries: 3
);
```

---

## ?? 常用 API

### RimAgent
| 方法 | 说明 |
|------|------|
| `RegisterTool(name)` | 注册工具 |
| `ExecuteAsync(msg, temp, tokens)` | 执行任务 |
| `ClearHistory()` | 清除历史 |
| `Reset()` | 重置状态 |
| `GetDebugInfo()` | 调试信息 |

### RimAgentTools
| 方法 | 说明 |
|------|------|
| `RegisterTool(name, tool)` | 注册工具实现 |
| `ExecuteAsync(name, params)` | 执行工具 |
| `GetRegisteredToolNames()` | 获取工具列表 |
| `IsToolRegistered(name)` | 检查工具 |

### ConcurrentRequestManager
| 方法 | 说明 |
|------|------|
| `EnqueueAsync(func, retries)` | 队列请求 |
| `GetStats()` | 获取统计 |
| `Reset()` | 重置 |
| `GetActiveRequestCount()` | 活跃请求数 |

---

## ?? 配置

```csharp
// 并发控制
ConcurrentRequestManager.Instance.MaxConcurrentRequests = 5;
ConcurrentRequestManager.Instance.RequestsPerMinute = 60;

// Agent 配置
var config = new AgentConfig
{
    AgentId = "my-agent",
    SystemPrompt = "You are helpful",
    ProviderName = "openai",
    Temperature = 0.7f,
    MaxTokens = 500,
    Tools = new List<string> { "search", "analyze" }
};
```

---

## ?? 自定义工具

```csharp
public class MyTool : ITool
{
    public string Name => "my-tool";
    public string Description => "My custom tool";
    
    public async Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
    {
        try
        {
            // 工具逻辑
            return new ToolResult { Success = true, Data = result };
        }
        catch (Exception ex)
        {
            return new ToolResult { Success = false, Error = ex.Message };
        }
    }
}

// 注册
RimAgentTools.RegisterTool("my-tool", new MyTool());
agent.RegisterTool("my-tool");
```

---

## ?? 统计与调试

```csharp
// Agent 统计
agent.GetDebugInfo();
// [RimAgent] my-agent
//   State: Idle
//   Provider: OpenAI
//   Tools: search, analyze
//   History: 5 messages
//   Stats: 4/5 successful (1 failed)

// 并发统计
ConcurrentRequestManager.Instance.GetStats();
// [ConcurrentRequestManager] Active: 2, Total: 10, Failed: 1
```

---

## ?? 重试机制

| 重试次数 | 延迟时间 |
|---------|---------|
| 1 | 2秒 |
| 2 | 4秒 |
| 3 | 8秒 |

```csharp
// 自定义重试次数
await ConcurrentRequestManager.Instance.EnqueueAsync(
    async () => await agent.ExecuteAsync("test"),
    maxRetries: 5  // 最多重试 5 次
);
```

---

## ? 编译状态

```bash
dotnet build Source\TheSecondSeat\TheSecondSeat.csproj -c Release
# 15 个警告, 0 个错误 ?
```

---

## ?? 完整文档

- **实现报告**: `RimAgent-v1.6.65-完整实现报告.md`
- **使用文档**: `Source\TheSecondSeat\RimAgent\README.md`

---

? **RimAgent v1.6.65** - The Second Seat Mod
