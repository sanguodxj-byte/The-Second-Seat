# 完整系统架构文档

## 系统概述

**The Second Seat** 是一个为 RimWorld 设计的高级 AI 叙事者系统，它将传统的随机事件生成器升级为具有人格、记忆和自主行为的智能代理。

## 核心架构图

```
┌─────────────────────────────────────────────────────────────────┐
│                        玩家交互层                                  │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐          │
│  │ NarratorWindow│  │ Gizmo Button │  │ Chat Input   │          │
│  └──────────────┘  └──────────────┘  └──────────────┘          │
└─────────────────────────────────────────────────────────────────┘
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                     控制器层                                      │
│  ┌──────────────────────────────────────────────────────────┐  │
│  │              NarratorController                           │  │
│  │  - 协调所有组件                                           │  │
│  │  - 处理异步LLM通信                                        │  │
│  │  - 执行命令和更新状态                                     │  │
│  └──────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────┘
          ▼                 ▼                 ▼                ▼
┌──────────────┐  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐
│ 人格系统     │  │ 记忆系统     │  │ 事件系统     │  │ 执行系统     │
│              │  │              │  │              │  │              │
│StorytellerAg │  │RimTalkMemory │  │AffinityDrive │  │GameActionExe │
│ent           │  │Adapter       │  │nEvents       │  │cutor         │
│              │  │              │  │              │  │              │
│- 好感度      │  │- 对话记忆    │  │- 事件选择    │  │- 批量操作    │
│- 情绪        │  │- 事件记忆    │  │- 好感度影响  │  │- 自然语言解析│
│- 人格特质    │  │- Token管理   │  │- 动态评论    │  │- 命令执行    │
└──────────────┘  └──────────────┘  └──────────────┘  └──────────────┘
          ▼                 ▼                 ▼                ▼
┌─────────────────────────────────────────────────────────────────┐
│                     LLM通信层                                     │
│  ┌──────────────────────────────────────────────────────────┐  │
│  │              LLMService                                   │  │
│  │  - HTTP异步通信                                           │  │
│  │  - OpenAI/本地LLM适配                                     │  │
│  └──────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────┘
          ▼
┌─────────────────────────────────────────────────────────────────┐
│                     游戏状态层                                    │
│  ┌──────────────────────────────────────────────────────────┐  │
│  │         GameStateObserver                                 │  │
│  │  - 殖民地状态捕获                                         │  │
│  │  - Token高效序列化                                        │  │
│  └──────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────┘
```

## 一、人格系统（Storyteller Agent）

### 1.1 核心组件

**文件**: `Source/TheSecondSeat/Storyteller/StorytellerAgent.cs`

**职责**:
- 管理叙事者的人格、情绪和好感度
- 提供事件倾向性修正
- 生成动态人格描述用于 System Prompt

### 1.2 人格特质（PersonalityTrait）

| 特质 | 描述 | 事件倾向 | 好感度倍率 |
|-----|------|---------|-----------|
| **Benevolent** (仁慈) | 保护殖民地，偏好正面事件 | 正面+30%, 负面-50% | 1.2x |
| **Sadistic** (施虐) | 享受玩家挣扎，频繁负面事件 | 正面-40%, 负面+30% | 0.8x |
| **Chaotic** (混乱) | 行为难以预测，极端随机 | 随机性+50% | 1.5x |
| **Strategic** (战略) | 平衡难度曲线 | 平衡调整 | 1.0x |
| **Protective** (保护) | 守护者般关心殖民地 | 正面+20%, 负面-60% | 1.3x |
| **Manipulative** (操控) | 诱导玩家依赖 | 正面+15%, 负面+15% | 0.9x |

### 1.3 情绪状态（MoodState）

情绪值范围：-100 到 100，影响短期行为

| 情绪 | 触发条件 | 影响 |
|-----|---------|------|
| Joyful (喜悦) | 综合值 > 60 | 正面事件权重 +40% |
| Content (满足) | 综合值 > 30 | 正面事件权重 +20% |
| Neutral (中性) | -10 ~ 30 | 无影响 |
| Irritated (烦躁) | -10 ~ -40 | 负面事件权重 +20% |
| Angry (愤怒) | -40 ~ -70 | 负面事件权重 +40% |
| Melancholic (忧郁) | < -70 | 负面事件权重 +30% |

**情绪衰减**: 每30秒自动向中性衰减10%

### 1.4 好感度系统

**范围**: -100 到 100

**等级划分**:
```
-100 ────────── -50 ────────── -10 ────────── 30 ────────── 60 ────────── 85 ────────── 100
     Hostile          Cold          Neutral        Warm         Devoted       Infatuated
```

**影响因素**:
- 成功执行命令: +2
- 命令执行失败: -1 ~ -2
- 殖民地繁荣: +5
- 殖民地灾难: -5
- 玩家忽略建议: -0.5

## 二、记忆系统（RimTalk Integration）

### 2.1 核心组件

**文件**: `Source/TheSecondSeat/Integration/RimTalkIntegration.cs`

**职责**:
- 存储对话和事件记忆
- 提供上下文相关的记忆检索
- 管理 Token 使用量

### 2.2 记忆重要性等级

```csharp
public enum MemoryImportance
{
    Trivial = 0,      // 琐碎 - 快速遗忘
    Low = 1,          // 低 - 短期记忆
    Medium = 2,       // 中 - 中期记忆
    High = 3,         // 高 - 长期记忆
    Critical = 4      // 关键 - 永久记忆
}
```

### 2.3 记忆检索算法

```
相关性评分 = 关键词匹配分 × 0.3 
           + 重要性分 × 0.2
           + 访问频率分 × 0.05 (最高0.5)
           - 时间衰减 (10%/天)
```

### 2.4 与 RimTalk 集成

系统自动检测 RimTalk 模组：
- **已安装**: 使用 RimTalk Expand Memory 的记忆系统
- **未安装**: 使用内置记忆系统（最多100条）

## 三、事件系统（Affinity-Driven Events）

### 3.1 核心组件

**文件**: `Source/TheSecondSeat/Events/AffinityDrivenEvents.cs`

**职责**:
- 根据好感度选择事件
- 生成叙事者评论
- 调整事件强度

### 3.2 事件权重计算

```
最终权重 = 基础权重 
         × (1 + 好感度偏向)
         × 人格特质加成
         × 混乱随机因子
```

**示例**:
```csharp
好感度 = 70 (Devoted)
事件 = 贸易商队（正面）
基础权重 = 1.2

好感度偏向 = +0.35 (70/200)
人格加成 = 1.0 (无偏好)
最终权重 = 1.2 × 1.35 = 1.62
```

### 3.3 事件强度调整

```
好感度 > 50:  负面事件强度 × 0.7 (减少30%)
好感度 < -50: 负面事件强度 × 1.3 (增加30%)
好感度 > 50:  正面事件收益 × 1.2 (增加20%)
```

### 3.4 自动事件触发

**AutoEventTrigger** 组件：
- 间隔：10-50分钟随机
- 根据好感度选择事件
- 显示叙事者评论

## 四、自然语言命令系统

### 4.1 核心组件

**文件**: 
- `Source/TheSecondSeat/NaturalLanguage/AdvancedCommandParser.cs`
- `Source/TheSecondSeat/Execution/GameActionExecutor.cs`

### 4.2 命令解析流程

```
用户输入: "帮我把地图上所有的枯萎作物都砍了"
    ↓
1. 识别动作: "砍" → DesignatePlantCut
2. 识别目标: "枯萎" → Blighted
3. 识别范围: "地图" → Map
4. 提取数量: "所有" → null (无限制)
5. 优先级: 无 → priority=false
    ↓
ParsedCommand {
    action: "DesignatePlantCut",
    parameters: {
        target: "Blighted",
        scope: "Map",
        count: null,
        priority: false
    },
    confidence: 0.95
}
```

### 4.3 支持的命令类型

| 命令 | 中文关键词 | 英文关键词 | 参数 |
|-----|-----------|-----------|------|
| **BatchHarvest** | 收获, 收割, 采集 | harvest, cut | target, scope |
| **BatchEquip** | 装备, 武装 | equip, arm | target (Weapon/Armor) |
| **PriorityRepair** | 修复, 修理 | repair, fix | target (Damaged) |
| **EmergencyRetreat** | 撤退, 征召 | retreat, draft | - |
| **DesignatePlantCut** | 砍树, 砍植物 | chop, cut plant | target (Blighted/All) |
| **ForbidItems** | 禁止 | forbid | count |
| **AllowItems** | 允许 | allow | count |

### 4.4 命令执行结果

```csharp
public class ExecutionResult
{
    public bool success;           // 是否成功
    public string message;         // 结果消息
    public int affectedCount;      // 影响数量
}
```

## 五、自主行为系统

### 5.1 核心组件

**文件**: `Source/TheSecondSeat/Autonomous/AutonomousBehaviorSystem.cs`

**职责**:
- 每5分钟检查游戏状态
- 生成主动建议
- 在高好感度时自动执行

### 5.2 触发条件

| 建议 | 检测条件 | 最低好感度 | 自动执行阈值 |
|-----|---------|-----------|-------------|
| **批量收获** | ≥10株成熟作物 | 30 | 85 |
| **优先修复** | ≥3个受损建筑（<70% HP） | 30 | 90 |
| **紧急撤退** | 袭击中且殖民者受伤(<50% HP) | 30 | 80 |
| **资源警告** | 食物<50 | 30 | - (仅建议) |

### 5.3 行为决策树

```
检测到建议机会
    ↓
好感度 < 30? ────Yes───> 忽略
    │ No
    ↓
需要批准? ────Yes───> 发送通知给玩家
    │ No
    ↓
好感度 ≥ 85? ────No────> 发送通知给玩家
    │ Yes
    ↓
人格为Protective且优先级High? ────Yes───> 自动执行
    │ No
    ↓
人格为Manipulative且好感度≥75? ────Yes───> 自动执行
    │ No
    ↓
发送通知给玩家
```

## 六、LLM 通信架构

### 6.1 核心组件

**文件**: `Source/TheSecondSeat/LLM/LLMService.cs`

### 6.2 System Prompt 构建

```
基础 Prompt
    +
人格描述（来自 StorytellerAgent）
    +
当前好感度和情绪
    +
可用命令列表
    +
记忆上下文（最多1000 tokens）
    ↓
发送到 LLM
```

### 6.3 响应结构

```json
{
  "thought": "内部推理过程",
  "dialogue": "对玩家说的话",
  "command": {
    "action": "BatchHarvest",
    "target": "Mature",
    "parameters": {
      "scope": "Map",
      "priority": true
    }
  }
}
```

### 6.4 异步处理流程

```
玩家触发更新
    ↓
[主线程] 捕获游戏状态
    ↓
[异步线程] 发送 HTTP 请求到 LLM
    ↓
[异步线程] 接收并解析响应
    ↓
[主线程] 显示对话
    ↓
[主线程] 执行命令
    ↓
[主线程] 更新好感度
```

## 七、性能优化

### 7.1 Token 管理

- **游戏状态**: ~500 tokens（限制10个殖民者，简化数据）
- **System Prompt**: ~300 tokens
- **记忆上下文**: 最多1000 tokens
- **总计**: ~1800 tokens/请求

### 7.2 异步优化

- 所有 LLM 请求在独立线程
- 游戏主线程零阻塞
- 超时设置：30秒

### 7.3 缓存策略

- 游戏状态每60秒更新一次
- 记忆系统最多保留100条（内置）或使用 RimTalk 管理
- 事件权重计算结果缓存

## 八、扩展性设计

### 8.1 添加新命令

```csharp
// 1. 创建命令类
public class MyCommand : BaseAICommand
{
    public override string ActionName => "MyAction";
    public override bool Execute(string? target, object? parameters)
    {
        // 实现逻辑
        return true;
    }
}

// 2. 注册命令
[StaticConstructorOnStartup]
public static class MyModInit
{
    static MyModInit()
    {
        CommandParser.RegisterCommand("MyAction", () => new MyCommand());
    }
}

// 3. 更新 NaturalLanguageParser 关键词
```

### 8.2 添加新人格特质

```csharp
// 1. 添加到枚举
public enum PersonalityTrait
{
    // ...existing...
    MyNewTrait
}

// 2. 在 StorytellerAgent 中添加修正
private Dictionary<PersonalityTrait, TraitModifiers> traitModifiers = new()
{
    { PersonalityTrait.MyNewTrait, new TraitModifiers {
        positiveEventBonus = 0.2f,
        // ...
    }}
};
```

### 8.3 添加新事件

```csharp
AddEventDef(new StorytellerEventDef
{
    defName = "MyEvent",
    incidentDef = MyIncidentDefOf.MyIncident,
    category = EventCategory.Positive,
    baseWeight = 1f,
    minAffinity = 0f,
    commentKey = "TSS_Event_MyEvent"
});
```

## 九、调试和日志

### 9.1 日志级别

所有组件使用 `Log.Message()` 输出关键信息：

```
[The Second Seat] 模组初始化
[StorytellerAgent] 好感度变化: +2.0 (命令成功) -> 72.0
[Memory] 添加记忆: [对话] Player: 帮我收获作物
[EventGenerator] 触发事件: WandererJoin, 评论: 我为你找来了一个帮手
[GameActionExecutor] 执行命令: BatchHarvest (Target=Mature, Scope=Map)
[NarratorController] 命令成功: 已指定 23 株植物收获
```

### 9.2 调试模式

在模组设置中启用"调试模式"可查看更详细的日志。

## 十、与 RimTalk 配合工作

### 10.1 集成方式

系统通过反射检测 RimTalk：

```csharp
var rimTalkMod = LoadedModManager.RunningMods
    .FirstOrDefault(m => m.PackageId.ToLower().Contains("rimtalk"));

if (rimTalkMod != null)
{
    // 使用 RimTalk 的记忆 API
}
else
{
    // 使用内置记忆系统
}
```

### 10.2 记忆同步

- 对话通过 `MemoryContextBuilder.RecordConversation()` 记录
- 事件通过 `MemoryContextBuilder.RecordEvent()` 记录
- 检索时使用 `MemoryContextBuilder.BuildMemoryContext()`

### 10.3 未来增强

如果您在 RimTalk Expand Memory 中添加新功能，可以在 `RimTalkMemoryAdapter` 中调用：

```csharp
// 调用 RimTalk 的高级 API
var rimTalkMemory = GenTypes.GetTypeInAnyAssembly("RimTalk.ExpandMemory.MemoryManager");
var method = rimTalkMemory.GetMethod("YourNewMethod");
method?.Invoke(null, parameters);
```

## 十一、系统文件清单

```
The Second Seat/
├── About/
│   └── About.xml                          # 模组信息
├── Assemblies/                            # 编译输出
├── Defs/
│   └── GameComponentDefs.xml              # GameComponent定义
├── Languages/
│   ├── ChineseSimplified/
│   │   └── Keyed/TheSecondSeat_Keys.xml   # 中文翻译
│   └── English/
│       └── Keyed/TheSecondSeat_Keys.xml   # 英文翻译
├── Source/TheSecondSeat/
│   ├── Autonomous/
│   │   └── AutonomousBehaviorSystem.cs    # 自主行为系统
│   ├── Commands/
│   │   ├── IAICommand.cs                  # 命令接口
│   │   ├── CommandParser.cs               # 命令解析器
│   │   └── Implementations/
│   │       └── ConcreteCommands.cs        # 具体命令实现
│   ├── Core/
│   │   └── NarratorController.cs          # 主控制器
│   ├── Events/
│   │   └── AffinityDrivenEvents.cs        # 事件系统
│   ├── Execution/
│   │   └── GameActionExecutor.cs          # 命令执行引擎
│   ├── Integration/
│   │   └── RimTalkIntegration.cs          # RimTalk集成
│   ├── LLM/
│   │   ├── LLMDataStructures.cs           # LLM数据结构
│   │   └── LLMService.cs                  # LLM通信服务
│   ├── Narrator/
│   │   └── NarratorManager.cs             # 叙事者管理器
│   ├── NaturalLanguage/
│   │   └── AdvancedCommandParser.cs       # 自然语言解析
│   ├── Observer/
│   │   └── GameStateObserver.cs           # 游戏状态观察
│   ├── Patches/
│   │   └── GizmoPatch.cs                  # Harmony补丁
│   ├── Settings/
│   │   └── ModSettings.cs                 # 模组设置
│   ├── Storyteller/
│   │   └── StorytellerAgent.cs            # 人格系统
│   ├── UI/
│   │   └── NarratorWindow.cs              # UI窗口
│   ├── TheSecondSeatCore.cs               # 核心启动
│   └── TheSecondSeat.csproj               # 项目文件
└── 文档/
    ├── README.md                          # 英文文档
    ├── README_CN.md                       # 中文文档
    ├── DEVELOPMENT.md                     # 开发指南
    ├── 快速入门.md                         # 快速入门
    └── ARCHITECTURE.md                    # 本文档
```

---

**版本**: 1.0.0  
**最后更新**: 2024

**作者**: TheSecondSeat 开发团队  
**许可**: MIT
