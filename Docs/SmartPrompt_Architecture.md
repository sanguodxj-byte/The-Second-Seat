# SmartPrompt 智能提示词系统架构文档

## 概述

SmartPrompt 是一个 **"Orchestrator-Worker（指挥官-执行者）"** 架构的智能提示词管理系统，结合 **"Config-time AI（配置态 AI）"** 与 **"Runtime High-Performance（运行态高性能）"** 的混合模式。

### 核心优势

1. **极度省流 (Token Efficiency)**: Prompt 永远只包含当前任务所需的最小集，Token 节省率预计 50% 以上
2. **零延迟响应 (Zero Latency)**: 意图识别在本地 CPU 完成（微秒级），没有额外的 HTTP 请求等待
3. **无限扩展性 (Scalability)**: 新增一个功能只需加一个 XML Def 和一次性词库生成，不污染核心代码
4. **智能且可控**: AI 决定的词库提供了泛化能力（懂人话），C# 的逻辑保证了执行的稳定性（不会幻觉）

---

## 架构分层

```
┌─────────────────────────────────────────────────────────────┐
│                    L4 执行层 (Executor)                      │
│                 RimAgent + 主模型 LLM                        │
│              LLM 生成回复与指令                               │
└─────────────────────────────────────────────────────────────┘
                              ▲
                              │ Final Prompt
┌─────────────────────────────────────────────────────────────┐
│                    L3 构建层 (Builder)                       │
│               SmartPromptBuilder + Scriban                   │
│                 渲染最终 Prompt                              │
└─────────────────────────────────────────────────────────────┘
                              ▲
                              │ Module List
┌─────────────────────────────────────────────────────────────┐
│                    L2 路由层 (Router)                        │
│                     IntentRouter                             │
│              决定加载哪些模块 + 依赖解析                       │
└─────────────────────────────────────────────────────────────┘
                              ▲
                              │ Match Results
┌─────────────────────────────────────────────────────────────┐
│                    L1 感知层 (Perception)                    │
│                 FlashMatcher (AC 自动机)                     │
│           实时捕捉意图与上下文 (微秒级)                        │
└─────────────────────────────────────────────────────────────┘
                              ▲
                              │ User Input
┌─────────────────────────────────────────────────────────────┐
│                    L0 配置层 (Configuration)                 │
│               PromptModuleDef (XML Defs)                     │
│           预计算、扩展语义、关键词库                           │
└─────────────────────────────────────────────────────────────┘
```

---

## 核心组件

### 1. PromptModuleDef (L0 配置层)

**文件位置**: `Source/TheSecondSeat/SmartPrompt/PromptModuleDef.cs`

智能技能包定义，不再是单纯的文本，而是包含：

```csharp
public class PromptModuleDef : Def
{
    // 基础信息
    public string content;            // 提示词内容 (支持 Scriban 模板)
    public bool useScriban = false;   // 是否启用模板渲染
    
    // 触发条件 (Gatekeeper)
    public List<string> triggerIntents; // 意图标签 (如 "Harvest")
    public bool requiresCombat;         // 强制环境要求
    
    // 依赖管理 (Dependency Chain)
    public List<string> dependencies;   // 依赖的其他模块
    
    // 预计算词库 (Pre-computed)
    public List<string> expandedKeywords; // 由 AI 预生成的 50+ 个关键词
}
```

**模块类型**:
- `Core`: 核心身份（始终加载）
- `Format`: 格式规范（作为依赖被加载）
- `Skill`: 技能模块（动态加载）
- `Context`: 情境模块
- `Memory`: 记忆模块
- `Extension`: 扩展模块

### 2. FlashMatcher (L1 感知层)

**文件位置**: `Source/TheSecondSeat/SmartPrompt/FlashMatcher.cs`

极速感知系统，解决 "如何不联网就知道玩家想收水稻" 的问题。

**技术原理**: Aho-Corasick (AC) 自动机

**工作流程**:
1. 游戏启动时读取 XML 中的 `expandedKeywords`
2. 构建内存中的 Trie 树 + 失败指针
3. 运行时 `Search(userInput)` 
4. 返回命中的 Module 及分数

**性能**: 微秒级 (0.05ms)，支持 50,000+ 关键词零延迟

```csharp
// 使用示例
var results = FlashMatcher.Instance.Search("帮我收割水稻");
// 返回: [{Module_Agriculture, Intent=Harvest, Score=2.0}]
```

### 3. IntentRouter (L2 路由层)

**文件位置**: `Source/TheSecondSeat/SmartPrompt/IntentRouter.cs`

决定加载哪些模块的路由器。

**核心逻辑**:
1. 接收 FlashMatcher 的匹配结果
2. 根据环境条件过滤（战斗/和平状态）
3. 添加 AlwaysActive 模块
4. 解析依赖链（递归）
5. 处理互斥关系
6. 按优先级排序

```csharp
// 使用示例
var result = IntentRouter.Instance.Route("帮我收割水稻");
// result.Modules = [Module_Identity_Core, Module_Command_Format, Module_Agriculture]
```

### 4. SmartPromptBuilder (L3 构建层)

**文件位置**: `Source/TheSecondSeat/SmartPrompt/SmartPromptBuilder.cs`

动态组装最终 Prompt。

**工作流程**:
1. 接收 IntentRouter 的模块列表
2. 按类型分组组织
3. 使用 Scriban 渲染模板化模块
4. 组装最终的 System Prompt

```csharp
// 使用示例
var result = SmartPromptBuilder.Instance.Build("帮我收割水稻");
string prompt = result.Prompt;
// prompt 只包含: 核心身份 + 指令格式 + 农业知识
// 不包含: 战斗、建造、狩猎等无关内容
```

---

## 工作流 (Workflow)

### 阶段一：模组开发/玩家配置 (Config Time)

```
输入：工具 HarvestTool 或 概念 Agriculture
    ↓
处理：调用 LLM (Gemini/GPT)
    "请生成 50 个与收割相关的中英文触发词"
    ↓
输出：PromptModuleDefs_Skills.xml
    (含 "harvest, reap, 割, 收菜, 种田" 等)
    ↓
状态：一次性成本���游戏运行时不消耗 API
```

### 阶段二：游戏运行 (Runtime)

```
玩家输入："帮我把这片水稻收了。"
    ↓
感知 (L1)：FlashMatcher 瞬间命中 "水稻"、"收"
    -> 激活 Module_Agriculture
    ↓
路由 (L2)：IntentRouter 解析依赖
    -> 加载 Identity_Core (常驻)
    -> 加载 Module_Agriculture (因命中)
    -> 加载 Module_Command_Format (因依赖)
    ↓
构建 (L3)：SmartPromptBuilder 渲染模块
    -> 组装最终 Prompt
    ↓
执行 (L4)：主模型接收到纯净的上下文
    -> 输出 {"action": "Harvest", ...}
```

---

## XML Def 示例

### 核心模块 (AlwaysActive)

```xml
<TheSecondSeat.SmartPrompt.PromptModuleDef>
    <defName>Module_Identity_Core</defName>
    <moduleType>Core</moduleType>
    <priority>1000</priority>
    <alwaysActive>true</alwaysActive>
    <content>你是一个 RimWorld 游戏中的智能 AI 助手...</content>
</TheSecondSeat.SmartPrompt.PromptModuleDef>
```

### 技能模块 (动态加载)

```xml
<TheSecondSeat.SmartPrompt.PromptModuleDef>
    <defName>Module_Agriculture</defName>
    <moduleType>Skill</moduleType>
    <priority>500</priority>
    <alwaysActive>false</alwaysActive>
    
    <triggerIntents>
        <li>Harvest</li>
        <li>Farm</li>
    </triggerIntents>
    
    <dependencies>
        <li>Module_Command_Format</li>
    </dependencies>
    
    <expandedKeywords>
        <li>harvest</li>
        <li>收割</li>
        <li>水稻</li>
        <li>种田</li>
    </expandedKeywords>
    
    <content>## 农业知识

收割操作...</content>
</TheSecondSeat.SmartPrompt.PromptModuleDef>
```

---

## 调试工具

在游戏中使用 Debug Actions (开发模式)：

| 命令 | 描述 |
|------|------|
| `Rebuild SmartPrompt` | 重建 AC 自动机 |
| `SmartPrompt Stats` | 显示统计信息 |
| `Test Intent Recognition` | 测试意图识别 |
| `Generate Test Prompt` | 生成测试 Prompt |

---

## 文件结构

```
The Second Seat/
├── Source/TheSecondSeat/SmartPrompt/
│   ├── PromptModuleDef.cs      # L0 模块定义
│   ├── FlashMatcher.cs         # L1 AC 自动机
│   ├── IntentRouter.cs         # L2 路由层
│   ├── SmartPromptBuilder.cs   # L3 构建层
│   └── SmartPromptInitializer.cs # 初始化 & 调试
├── Defs/PromptModuleDefs/
│   ├── PromptModuleDefs_Core.xml    # 核心模块
│   └── PromptModuleDefs_Skills.xml  # 技能模块
└── Docs/
    └── SmartPrompt_Architecture.md  # 本文档
```

---

## 性能指标

| 操作 | 耗时 | 说明 |
|------|------|------|
| AC 自动机构建 | ~50ms | 仅游戏启动时执行一次 |
| 意图识别 | ~0.05ms | 每次用户输入 |
| 路由解析 | ~0.1ms | 每次用户输入 |
| Prompt 构建 | ~0.5ms | 每次用户输入 |
| **总计** | **&lt;1ms** | 远低于 LLM 响应时间 |

---

## 版本历史

- **v1.0.0** (2026-01-25): 初始实现
  - 实现 Orchestrator-Worker 架构
  - AC 自动机意图识别
  - 模块化 Prompt 管理
  - Scriban 模板支持
