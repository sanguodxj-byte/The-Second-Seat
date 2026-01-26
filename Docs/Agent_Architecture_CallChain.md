# The Second Seat - Agent 架构与调用链完整文档

## 概述

The Second Seat 使用 **RimAgent** 作为核心 AI Agent 框架，采用 **ReAct (Reasoning + Acting)** 循环模式，结合 **Pull 模式**（工具按需获取状态）实现高效的 AI 叙事者系统。

---

## 架构分层

```
┌─────────────────────────────────────────────────────────────────┐
│                     用户界面层 (UI Layer)                        │
│                    NarratorWindow (聊天窗口)                     │
└─────────────────────────────────────────────────────────────────┘
                              │ userMessage
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                  控制器层 (Controller Layer)                     │
│         NarratorController (GameComponent, 生命周期管理)          │
└─────────────────────────────────────────────────────────────────┘
                              │ TriggerNarratorUpdate()
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                  服务层 (Service Layer)                          │
│     NarratorUpdateService (状态捕获、Prompt构建、响应处理)         │
└─────────────────────────────────────────────────────────────────┘
                              │ ExecuteAsync()
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                  Agent 层 (Agent Layer)                          │
│        RimAgent (ReAct循环、工具管理、历史压缩)                    │
└─────────────────────────────────────────────────────────────────┘
                              │ SendMessageAsync()
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                  LLM 提供者层 (Provider Layer)                   │
│       LLMServiceProvider / GeminiApiClient / OpenAI              │
└─────────────────────────────────────────────────────────────────┘
                              │ HTTP Request
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                  外部 LLM API                                    │
│           Google Gemini / OpenAI GPT / DeepSeek                  │
└─────────────────────────────────────────────────────────────────┘
```

---

## 核心组件

### 1. NarratorController (控制器)

**文件**: `Source/TheSecondSeat/Core/NarratorController.cs`

**职责**:
- 游戏生命周期管理 (GameComponent)
- 初始化子组件
- 转发用户消息到 UpdateService
- 处理运行时错误回调

**关键方法**:
```csharp
// 手动触发叙事者更新
public void TriggerNarratorUpdate(string userMessage = "")

// 运行时错误回调 (来自 LogListenerService)
public void NotifyRuntimeError(string condition, string stackTrace)
```

### 2. NarratorUpdateService (核心服务)

**文件**: `Source/TheSecondSeat/Core/Components/NarratorUpdateService.cs`

**职责**:
- 捕获游戏状态 (GameStateSnapshot)
- 构建 System Prompt
- 管理 RimAgent 实例
- 处理 LLM 响应
- 执行命令

**关键方法**:
```csharp
// 初始化叙事者 Agent
public void InitializeNarratorAgent()

// 触发更新 (主入口)
public void TriggerNarratorUpdate(string userMessage, bool hasGreetedOnLoad)

// 异步处理核心逻辑
private async Task ProcessNarratorUpdateAsync(...)
```

### 3. RimAgent (Agent 核心)

**文件**: `Source/TheSecondSeat/RimAgent/RimAgent.cs`

**职责**:
- ReAct 循环 (Thought → Action → Observation)
- 工具注册与执行
- 对话历史管理
- 响应解析 (JSON / NLP)

**关键配置**:
```csharp
narratorAgent.MaxIterations = 3;    // ReAct 最大循环次数
narratorAgent.MaxHistorySize = 5;   // 对话历史大小限制
narratorAgent.MaxContextTokens = 4000; // 上下文 Token 限制
```

**关键方法**:
```csharp
// 简单执行 (无 ReAct 循环)
public async Task<AgentResponse> SimpleExecuteAsync(string userInput, ...)

// ReAct 模式执行
public async Task<AgentResponse> ExecuteAsync(string userInput, ...)
```

### 4. LLMServiceProvider (LLM 提供者)

**文件**: `Source/TheSecondSeat/RimAgent/LLMServiceProvider.cs`

**职责**:
- 实现 ILLMProvider 接口
- 调用 LLMService 发送请求
- 缓存完整响应

---

## 完整调用链

### 用户发送消息的完整流程

```
1. 用户在 NarratorWindow 输入消息并发送
   │
   ▼
2. NarratorController.TriggerNarratorUpdate(userMessage)
   │
   ▼
3. NarratorUpdateService.TriggerNarratorUpdate(userMessage, hasGreeted)
   │
   ├── 捕获游戏状态 (GameStateSnapshot)
   ├── 捕获玩家选中物体
   ├── 设置思考表情
   ├── 更新角色卡 (CharacterCard)
   │
   ▼
4. NarratorUpdateService.ProcessNarratorUpdateAsync()
   │
   ├── 缓存 GameStateSnapshot (供工具按需获取)
   ├── 获取动态 System Prompt (NarratorManager.GetDynamicSystemPrompt())
   ├── 注入记忆上下文 (SimpleRimTalkIntegration)
   ├── 初始化/更新 RimAgent
   ├── 检查联网搜索需求 (WebSearchService)
   ├── 构建增强用户消息
   │
   ▼
5. RimAgent.ExecuteAsync(enhancedUserMessage)
   │
   ├── 添加用户消息到历史
   ├── 构建上下文 (BuildContext)
   ├── 构建工具描述 (BuildToolsDescription)
   │
   ▼ ReAct 循环 (最多 3 次)
   │
   ├── 6.1 构建完整 Prompt (BuildFullPrompt)
   ├── 6.2 调用 LLM (Provider.SendMessageAsync)
   ├── 6.3 解析响应 (ParseLLMResponse)
   │     ├── ReAct 正则 ([THOUGHT], [ACTION], [ANSWER])
   │     ├── JSON 解析 (NaturalLanguageParser)
   │     └── NLP 回退
   │
   ├── 6.4 如果有 [ANSWER]: 跳出循环
   ├── 6.5 如果有 [ACTION]: 执行工具
   │       └── ExecuteToolAsync(toolName, params)
   │           └── 工具返回 Observation
   │
   └── 循环继续直到获得最终答案
   │
   ▼
7. LLMServiceProvider.SendMessageAsync()
   │
   ├── LLMService.SendRequestAsync()
   ├── GeminiApiClient / OpenAI API
   │
   ▼
8. LLM API 返回响应
   │
   ▼
9. RimAgent 返回 AgentResponse
   │
   ▼
10. NarratorUpdateService.ProcessResponse()
    │
    ├── 解析响应 (LLMResponseParser)
    ├── 更新对话历史
    ├── 更新表情 (ExpressionController)
    ├── 记录到 RimTalk 记忆
    ├── 处理好感度变化
    ├── 处理角色卡更新
    ├── 播放 TTS
    ├── 执行命令 (GameActionExecutor)
    │
    ▼
11. 更新 NarratorWindow 显示对话
```

---

## 已注册的工具 (Tools)

### 叙事者 Agent 工具

| 工具名 | 文件 | 描述 |
|--------|------|------|
| `get_game_state` | `GameStateTool.cs` | 获取游戏状态快照 (Pull 模式) |
| `analyze_last_error` | `AnalyzeTool.cs` | 分析最近的运行时错误 |
| `patch_file` | `FilePatcherTool.cs` | 修补 XML 配置文件 |
| `search_items` | `SearchTool.cs` | 搜索游戏物品/殖民者 |
| `execute_command` | `CommandTool.cs` | 执行游戏指令 |
| `spatial_query` | `SpatialQueryTool.cs` | 空间位置查询 |
| `issue_quest` | `QuestIssueTool.cs` | 发布任务/事件 |

### 工具注册方式

```csharp
// 在 InitializeNarratorAgent() 中
narratorAgent.RegisterTool(new GameStateTool());
```

---

## SmartPrompt 集成点

### 当前架构中的 System Prompt 生成

```csharp
// NarratorUpdateService.ProcessNarratorUpdateAsync()
var systemPrompt = narratorManager?.GetDynamicSystemPrompt() ?? GetDefaultSystemPrompt();
```

### SmartPrompt 集成方案

SmartPrompt 应该集成到 **System Prompt 生成流程**：

```csharp
// 方案 A: 替换现有的 GetDynamicSystemPrompt()
var routeResult = SmartPromptBuilder.Instance.Build(userMessage);
string systemPrompt = routeResult.Prompt;

// 方案 B: 作为补充层
var basePrompt = narratorManager?.GetDynamicSystemPrompt();
var smartResult = SmartPromptBuilder.Instance.Build(userMessage);
string systemPrompt = basePrompt + "\n" + smartResult.Prompt;
```

### 推荐集成位置

修改 `NarratorUpdateService.ProcessNarratorUpdateAsync()`：

```csharp
// 原有代码
var systemPrompt = narratorManager?.GetDynamicSystemPrompt() ?? GetDefaultSystemPrompt();

// 使用 SmartPrompt 优化
var smartResult = SmartPromptBuilder.Instance.Build(userMessage, promptContext);
var systemPrompt = smartResult.Prompt; // 只包含相关模块
```

---

## 数据流图

```
┌─────────────┐     ┌─────────────────┐     ┌─────────────────┐
│   用户输入   │ ──► │ NarratorController │ ──► │ UpdateService   │
└─────────────┘     └─────────────────┘     └─────────────────┘
                                                      │
                                                      ▼
┌─────────────┐     ┌─────────────────┐     ┌─────────────────┐
│ 游戏状态缓存 │ ◄── │ GameStateTool   │ ◄── │   RimAgent      │
└─────────────┘     └─────────────────┘     └─────────────────┘
                                                      │
                                                      ▼
┌─────────────┐     ┌─────────────────┐     ┌─────────────────┐
│  LLM 响应   │ ◄── │ LLMServiceProvider │ ◄── │  LLM API       │
└─────────────┘     └─────────────────┘     └─────────────────┘
                                                      │
                                                      ▼
┌─────────────┐     ┌─────────────────┐     ┌─────────────────┐
│  UI 更新    │ ◄── │ ProcessResponse │ ◄── │ 命令执行        │
└─────────────┘     └─────────────────┘     └─────────────────┘
```

---

## 文件结构

```
The Second Seat/Source/TheSecondSeat/
│
├── Core/
│   ├── NarratorController.cs      # 主控制器 (GameComponent)
│   └── Components/
│       ├── NarratorUpdateService.cs  # 核心服务
│       ├── NarratorExpressionController.cs
│       └── NarratorTTSHandler.cs
│
├── RimAgent/
│   ├── RimAgent.cs                # Agent 核心 (ReAct)
│   ├── ILLMProvider.cs            # 提供者接口
│   ├── LLMServiceProvider.cs      # 提供者实现
│   ├── RimAgentTools.cs           # 全局工具注册表
│   └── Tools/
│       ├── GameStateTool.cs       # 状态查询工具
│       ├── CommandTool.cs         # 命令执行工具
│       ├── SpatialQueryTool.cs    # 空间查询工具
│       └── ...                    # 其他工具
│
├── SmartPrompt/                   # 新增: 智能 Prompt 系统
│   ├── PromptModuleDef.cs         # L0 模块定义
│   ├── FlashMatcher.cs            # L1 AC自动机
│   ├── IntentRouter.cs            # L2 路由层
│   └── SmartPromptBuilder.cs      # L3 构建层
│
├── PersonaGeneration/
│   ├── SystemPromptGenerator.cs   # 系统提示词生成
│   ├── PromptLoader.cs            # 提示词加载
│   └── PromptSections/
│       ├── IdentitySection.cs
│       ├── PersonalitySection.cs
│       └── ...
│
├── LLM/
│   ├── LLMService.cs              # LLM 请求服务
│   ├── GeminiApiClient.cs         # Gemini API
│   └── LLMResponseParser.cs       # 响应解析
│
└── Narrator/
    ├── NarratorManager.cs         # 叙事者管理
    └── NarratorPersonaDef.cs      # 人格定义
```

---

## 版本历史

- **v2.9.8**: 引入 Pull 模式，工具按需获取状态
- **v2.9.6**: 重构为 RimAgent 架构，替代直接 LLMService 调用
- **v2.3.0**: 移除硬编码格式指令，完全由 System Prompt 控制

---

## SmartPrompt 优势

将 SmartPrompt 集成后:

1. **Token 节省 50%+**: 农业问题不加载战斗知识
2. **零延迟识别**: FlashMatcher AC自动机 ~0.05ms
3. **模块化扩展**: 添加功能只需 XML Def
4. **智能依赖**: 自动加载依赖模块
