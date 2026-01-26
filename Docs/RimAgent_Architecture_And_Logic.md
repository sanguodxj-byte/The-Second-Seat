# RimAgent (The Second Seat) 架构与逻辑文档

本文档详细描述了 **The Second Seat** 模组中 RimAgent（AI 叙事者代理）的核心运作逻辑、组件交互及调用链。

---

## 1. 系统概览 (System Overview)

RimAgent 是一个集成在 RimWorld 中的 AI 代理系统，旨在通过大语言模型 (LLM) 扮演“第二席”叙事者（Co-Storyteller）。它不仅负责生成沉浸式的对话，还能根据游戏状态做出决策、执行指令，并拥有动态的情绪和人际关系系统。

### 核心设计理念
-   **人格化 (Persona-driven)**: 一切行为基于 `NarratorPersonaDef` 定义的人格特征。
-   **数据驱动 (Data-driven)**: 通过 `CharacterCard` 实时聚合游戏状态。
-   **模板化 (Templated)**: 使用 Scriban 引擎动态生成 System Prompt，支持高度可配置性。
-   **分层覆盖 (Layered Overrides)**: 支持从模组默认 -> 语言特定 -> 用户全局 -> Persona 专属的配置覆盖。

---

## 2. 核心组件 (Core Components)

### 2.1 控制与管理
*   **`NarratorManager`**: 单例管理器，负责协调 Agent 的生命周期、状态更新和主循环。
*   **`StorytellerAgent`**: 代表 AI 代理的运行时实例，维护好感度 (`Affinity`)、情绪 (`Mood`) 和对话风格 (`DialogueStyle`)。

### 2.2 Prompt 系统
*   **`SystemPromptGenerator`**: Prompt 生成的入口点。负责收集上下文数据，调用渲染引擎。
*   **`PromptLoader`**: 负责从文件系统加载 Prompt 模板。实现了多级优先级加载逻辑（Persona 专属 > 用户全局 > 模组默认）。
*   **`PromptRenderer` (Scriban)**: 基于 Scriban 的模板渲染引擎。支持模板继承 (`include`)、自定义函数（如获取天气、季节）和缓存编译。
*   **`SmartPrompt` / `PromptAutoLoader`**: 动态技能模块系统。自动扫描并注册 `.txt` 定义的技能模块 (`PromptModuleDef`)，无需硬编码 XML。

### 2.3 状态与数据
*   **`CharacterCardSystem`**: 负责生成和维护 `NarratorStateCard`。这是一个快照对象，包含了当前游戏的所有关键状态（殖民者、威胁、财富、天气等），供 Prompt 使用。
*   **`NarratorPersonaDef`**: XML 定义，描述了叙事者的静态属性（名字、背景、立绘路径、参数配置）。

---

## 3. 工作流程与调用链 (Workflows & Call Chains)

### 3.1 初始化流程 (Initialization)
1.  **游戏启动**: `StaticConstructorOnStartup` 触发各子系统初始化。
2.  **加载 Persona**: `NarratorManager` 读取所有 `NarratorPersonaDef`。
3.  **加载 Prompt**: `PromptAutoLoader` 扫描 `Prompts/` 目录，解析 Metadata，自动生成 `PromptModuleDef`。
4.  **初始化 Agent**: 当玩家选择叙事者或加载存档时，`NarratorManager` 实例化 `StorytellerAgent`。

### 3.2 System Prompt 生成调用链 (Prompt Generation Chain)
这是 Agent “思考”前的准备阶段，目的是构建发给 LLM 的核心指令。

1.  **触发**: `NarratorManager` 需要发送请求时，调用 `SystemPromptGenerator.GenerateSystemPrompt`。
2.  **构建上下文 (`PromptContextBuilder`)**:
    *   获取当前 `NarratorPersonaDef` (含 `DefName`)。
    *   调用 `CharacterCardSystem.GetCurrentCard()` 获取最新游戏状态。
    *   封装 `NarratorInfo`, `AgentInfo`, `MetaInfo`。
3.  **加载模板 (`PromptLoader`)**:
    *   `SystemPromptGenerator` 请求加载主模板 (如 `SystemPrompt_Master_Scriban`)。
    *   `PromptLoader.Load(templateName, personaName)` 被调用。
    *   **优先级检查**:
        1.  `Config/.../Prompts/{Persona}/{Language}/{file}`
        2.  `Config/.../Prompts/{Persona}/{file}`
        3.  `Config/.../Prompts/{Language}/{file}`
        4.  `Config/.../Prompts/{file}`
        5.  `Mod/Languages/{Language}/Prompts/{file}`
4.  **渲染模板 (`PromptRenderer`)**:
    *   Scriban 引擎编译模板（带缓存）。
    *   执行模板逻辑（替换变量、执行 `include`）。
    *   遇到 `{{ include 'Module' }}` 时，`ModPromptTemplateLoader` 会再次调用 `PromptLoader`，同样遵循 Persona 专属路径优先原则。
5.  **输出**: 返回最终的 System Prompt 字符串。

### 3.3 LLM 交互与指令执行 (Interaction & Execution)
1.  **发送请求**: `RimAgent` 将 System Prompt + 用户输入/游戏事件 发送给 LLM API。
2.  **接收响应**: LLM 返回 JSON 格式的响应（遵循 `OutputFormat_Structure.txt`）。
3.  **解析响应**: 系统解析 JSON，提取：
    *   `response`: 对话文本。
    *   `commands`: 待执行的指令列表。
    *   `emotion`: 情绪标签。
4.  **执行指令**:
    *   `CommandExecutor` 遍历指令列表。
    *   根据指令名称（如 `SpawnRaid`, `ChangeWeather`）调用相应的游戏逻辑。
    *   **SmartPrompt 集成**: 如果指令属于动态加载的模块，则根据模块定义执行通用逻辑。
5.  **反馈循环**: 执行结果被记录，可能作为下一轮对话的上下文。

---

## 4. 关键逻辑细节

### Persona 专属 Prompt 覆盖
为了支持不同叙事者的个性化，系统在加载任何 Prompt 文件（无论是主模板还是子模块）时，都会尝试寻找 Persona 专属版本。
*   **实现**: `PromptContext` 中携带了 `DefName`。Scriban 的 `ModPromptTemplateLoader` 从上下文中提取这个 `DefName`，并将其传递给 `PromptLoader`。
*   **效果**: 即使是通用的 `Module_Combat.txt`，也可以为 "Sideria" 创建一个 `Config/.../Sideria/Module_Combat.txt` 来覆盖默认行为，而不会影响其他叙事者。

### SmartPrompt (动态技能)
*   **原理**: 不再依赖硬编码的 C# 类来定义技能。
*   **流程**:
    1.  扫描 `.txt` 文件头部的 YAML Metadata (defName, priority, intents, keywords)。
    2.  在运行时生成 `PromptModuleDef`。
    3.  LLM 在 System Prompt 中看到这些技能的描述和用法。
    4.  LLM 输出对应的 JSON 指令。
    5.  C# 的通用处理器执行这些指令（通常映射到 DebugActions 或特定的 API）。

---

## 5. 目录结构参考

```
d:/rim mod/The Second Seat/
├── Source/TheSecondSeat/
│   ├── PersonaGeneration/
│   │   ├── PromptLoader.cs       (文件加载核心)
│   │   ├── SystemPromptGenerator.cs (Prompt 构建入口)
│   │   └── Scriban/              (模板引擎相关)
│   ├── SmartPrompt/              (动态技能模块)
│   └── UI/                       (界面，含 PromptManagementWindow)
├── Languages/
│   ├── English/Prompts/          (默认提示词)
│   └── ChineseSimplified/Prompts/(中文提示词)
└── Defs/                         (XML 定义)
```

用户配置目录 (AppData):
```
Config/TheSecondSeat/Prompts/
├── {PersonaName}/                (特定角色专属)
│   ├── {Language}/               (特定角色+特定语言)
│   └── (文件)                     (特定角色全局)
├── {Language}/                   (特定语言全局)
└── (文件)                         (全局覆盖)