# 测试指南和使用示例

## 目录
1. [开发环境设置](#1-开发环境设置)
2. [单元测试](#2-单元测试)
3. [集成测试](#3-集成测试)
4. [实际使用场景](#4-实际使用场景)
5. [性能测试](#5-性能测试)
6. [故障排除](#6-故障排除)

---

## 1. 开发环境设置

### 1.1 编译模组

```powershell
# 在 Source/TheSecondSeat 目录下
dotnet build TheSecondSeat.csproj --configuration Release

# 或使用 Visual Studio
# 打开 TheSecondSeat.csproj -> 构建 -> 生成解决方案
```

### 1.2 配置 RimWorld 路径

编辑 `TheSecondSeat.csproj` 中的游戏路径：

```xml
<GameFolder>C:\Program Files (x86)\Steam\steamapps\common\RimWorld</GameFolder>
```

### 1.3 启用调试模式

1. 启动 RimWorld
2. 选项 → 模组设置 → The Second Seat
3. 勾选"调试模式"
4. 查看日志：`%AppData%\..\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Player.log`

---

## 2. 单元测试

### 2.1 测试人格系统

**测试文件**: 在开发控制台中运行

```csharp
// 打开 RimWorld 开发模式（F12 → Developer mode）
// 按 ~ 键打开控制台

// 测试好感度变化
var narrator = Current.Game.GetComponent<TheSecondSeat.Narrator.NarratorManager>();
narrator.ModifyFavorability(10f, "测试：增加好感度");
narrator.ModifyFavorability(-15f, "测试：降低好感度");

// 查看当前状态
Log.Message($"当前好感度: {narrator.Favorability}");
Log.Message($"当前等级: {narrator.CurrentTier}");

// 测试StorytellerAgent
var agent = narrator.GetStorytellerAgent();
Log.Message($"人格特质: {agent.primaryTrait}");
Log.Message($"当前情绪: {agent.currentMood}");

// 手动触发情绪变化
agent.ModifyAffinity(30f, "测试情绪系统");
Log.Message($"情绪变化后: {agent.currentMood}");
```

### 2.2 测试记忆系统

```csharp
using TheSecondSeat.Integration;

// 添加测试记忆
MemoryContextBuilder.RecordConversation("Player", "你好，这是测试消息", importance: MemoryImportance.High);
MemoryContextBuilder.RecordEvent("测试事件：殖民地遭到袭击", MemoryImportance.Critical);

// 检查记忆是否保存
var adapter = RimTalkMemoryAdapter.Instance;
Log.Message($"RimTalk 可用: {adapter.IsRimTalkAvailable()}");

// 检索相关记忆
var memories = adapter.GetRelevantMemories("袭击", maxTokens: 500);
Log.Message($"找到 {memories.Count} 条相关记忆");
foreach (var memory in memories)
{
    Log.Message($"  - {memory.content}");
}
```

### 2.3 测试命令解析

```csharp
using TheSecondSeat.NaturalLanguage;

// 测试自然语言解析
var testQueries = new[]
{
    "帮我把地图上所有的枯萎作物都砍了",
    "武装所有殖民者",
    "优先修复受损的建筑",
    "让所有人撤退"
};

foreach (var query in testQueries)
{
    var parsed = NaturalLanguageParser.Parse(query);
    if (parsed != null)
    {
        Log.Message($"查询: {query}");
        Log.Message($"  动作: {parsed.action}");
        Log.Message($"  目标: {parsed.parameters.target}");
        Log.Message($"  范围: {parsed.parameters.scope}");
        Log.Message($"  置信度: {parsed.confidence:P0}");
    }
}
```

### 2.4 测试命令执行

```csharp
using TheSecondSeat.Execution;
using TheSecondSeat.NaturalLanguage;

// 创建测试命令
var command = new ParsedCommand
{
    action = "BatchHarvest",
    parameters = new AdvancedCommandParams
    {
        target = "Mature",
        scope = "Map"
    },
    confidence = 1f
};

// 执行命令
var result = GameActionExecutor.Execute(command);

Log.Message($"执行结果: {(result.success ? "成功" : "失败")}");
Log.Message($"消息: {result.message}");
Log.Message($"影响数量: {result.affectedCount}");
```

---

## 3. 集成测试

### 3.1 完整对话流程测试

**步骤**:
1. 开始新游戏或加载存档
2. 选择任意殖民者
3. 点击"AI 旁白"按钮
4. 在输入框输入："你好，请介绍一下自己"
5. 点击"与旁白交谈"

**预期结果**:
- 几秒后收到 AI 回复
- 回复中包含人格描述
- 好感度保持不变（初次对话）

### 3.2 命令执行流程测试

**场景**: 收获成熟作物

**步骤**:
1. 种植一些作物并等待成熟
2. 打开旁白窗口
3. 输入："帮我收获所有成熟的作物"
4. 点击发送

**预期结果**:
- AI 回复确认执行
- 地图上的成熟作物被指定收获
- 好感度 +2
- 消息提示："已指定 X 株植物收获"

### 3.3 事件系统测试

**场景**: 手动触发事件

```csharp
using TheSecondSeat.Events;

var map = Find.CurrentMap;
var agent = Current.Game.GetComponent<TheSecondSeat.Narrator.NarratorManager>().GetStorytellerAgent();

// 调整好感度到高级别
agent.ModifyAffinity(70f, "测试");

// 选择并触发事件
var eventDef = AffinityDrivenEventGenerator.Instance.SelectEvent(agent, map);
if (eventDef != null)
{
    bool success = AffinityDrivenEventGenerator.Instance.TriggerEvent(eventDef, map, agent, out string comment);
    Log.Message($"事件触发: {eventDef.defName}, 评论: {comment}");
}
```

**预期结果**:
- 高好感度倾向触发正面事件（贸易、加入者）
- 显示叙事者评论
- 事件强度根据好感度调整

### 3.4 自主行为测试

**场景**: 测试主动建议

**步骤**:
1. 将好感度提升到 60 以上
2. 种植大量作物并等待成熟（至少10株）
3. 等待 5 分钟

**预期结果**:
- 收到系统消息："叙事者建议执行：BatchHarvest - 检测到成熟的作物等待收获"
- 如果好感度 ≥ 85，可能自动执行

```csharp
// 手动触发检查（调试用）
var autonomous = Current.Game.GetComponent<TheSecondSeat.Autonomous.AutonomousBehaviorSystem>();
// 使用反射调用私有方法 CheckForProactiveSuggestions
var method = typeof(TheSecondSeat.Autonomous.AutonomousBehaviorSystem)
    .GetMethod("CheckForProactiveSuggestions", 
        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
method?.Invoke(autonomous, null);
```

---

## 4. 实际使用场景

### 4.1 场景 1：新手玩家求助

**背景**: 玩家不熟悉游戏机制

**对话**:
```
玩家: "我的殖民者一直在外面晃悠不干活"
AI: "让我看看...你的殖民者可能没有被分配工作，或者工作优先级设置有问题。
     我建议你打开工作标签页（Work），检查每个殖民者的工作分配。"
```

**系统处理**:
1. GameStateObserver 捕获殖民者状态
2. LLM 分析发现殖民者 currentJob = "Idle"
3. 生成建议（无具体命令）

### 4.2 场景 2：紧急情况处理

**背景**: 大型袭击来临

**对话**:
```
玩家: "救命！30个机械族来袭击了！"
AI (好感度 75，Protective): "别慌！我马上帮你做好准备。"
[自动执行] BatchEquip - 所有殖民者装备武器
[自动执行] EmergencyRetreat - 征召所有战斗人员
AI: "我已经武装了你的殖民者并征召了他们。躲在防御工事后面，利用掩体优势！"
```

**系统处理**:
1. 检测到 threats.raidActive = true, raidStrength = 30
2. LLM 判断为紧急情况
3. 返回两个命令
4. 好感度高，自动执行
5. 记录到记忆系统

### 4.3 场景 3：好感度变化演示

**从中性到温暖**:

```
// 初始：好感度 0，中性
玩家: "帮我收获作物"
AI: "收到。我已指定所有成熟作物收获。"

// 执行成功，+2
// 重复几次...

// 好感度 35，温暖
玩家: "帮我收获作物"
AI: "当然！我注意到你的农田管理得很好。收获指令已下达~"

// 好感度 65，忠诚
玩家: "帮我收获作物"
AI: "亲爱的，我早就注意到作物成熟了！我已经帮你安排好了，你真是太忙了，要多注意休息哦~"
```

### 4.4 场景 4：低好感度惩罚

**从中性到敌对**:

```
// 玩家多次忽略建议，好感度 -55

AI (自主触发袭击): "呵呵，这次的机械族可不简单...祝你好运~"
[触发 MechanoidRaid，强度 × 1.3]

玩家: "帮我武装殖民者！"
AI: "啊？刚才你不是不听我的吗？自己想办法吧。"
[拒绝执行]
```

---

## 5. 性能测试

### 5.1 测试 Token 使用量

```csharp
using TheSecondSeat.Observer;
using Newtonsoft.Json;

var snapshot = GameStateObserver.CaptureSnapshot();
var json = GameStateObserver.SnapshotToJson(snapshot);

// 估算 token 数量
int chineseChars = json.Count(c => c >= 0x4E00 && c <= 0x9FFF);
int otherChars = json.Length - chineseChars;
int estimatedTokens = chineseChars * 2 + otherChars / 4;

Log.Message($"游戏状态 JSON 长度: {json.Length} 字符");
Log.Message($"估算 Token 数量: {estimatedTokens}");
Log.Message($"JSON 内容:\n{json}");
```

**预期结果**:
- 10 个殖民者：~400-600 tokens
- 完整请求（含 System Prompt + 记忆）：~1800-2200 tokens
- 成本（GPT-4）：~$0.01/次

### 5.2 测试异步性能

```csharp
using System.Diagnostics;

var stopwatch = Stopwatch.StartNew();

// 触发更新
var controller = Current.Game.GetComponent<TheSecondSeat.Core.NarratorController>();
controller.TriggerNarratorUpdate("性能测试");

stopwatch.Stop();
Log.Message($"触发更新耗时: {stopwatch.ElapsedMilliseconds} ms");

// 游戏应该不会卡顿（主线程耗时 < 50ms）
```

**预期结果**:
- 主线程耗时：10-50ms（仅状态捕获）
- 总响应时间：2-5秒（网络延迟）
- 游戏帧率：不受影响

### 5.3 记忆系统性能

```csharp
using TheSecondSeat.Integration;
using System.Diagnostics;

// 添加100条记忆
for (int i = 0; i < 100; i++)
{
    MemoryContextBuilder.RecordConversation("Player", $"测试消息 {i}", MemoryImportance.Medium);
}

// 测试检索速度
var sw = Stopwatch.StartNew();
var memories = RimTalkMemoryAdapter.Instance.GetRelevantMemories("测试", maxTokens: 1000);
sw.Stop();

Log.Message($"检索 100 条记忆耗时: {sw.ElapsedMilliseconds} ms");
Log.Message($"返回相关记忆: {memories.Count} 条");
```

**预期结果**:
- 检索耗时：< 50ms
- 相关性排序正确
- Token 限制生效

---

## 6. 故障排除

### 6.1 常见问题

#### 问题：AI 不回复

**检查清单**:
1. API 密钥是否正确？
2. 网络连接是否正常？
3. 查看日志中的错误信息

**解决方法**:
```csharp
// 测试连接
var service = TheSecondSeat.LLM.LLMService.Instance;
var success = await service.TestConnectionAsync();
Log.Message($"连接测试: {(success ? "成功" : "失败")}");

// 检查配置
var settings = LoadedModManager.GetMod<TheSecondSeat.Settings.TheSecondSeatMod>().GetSettings<TheSecondSeat.Settings.TheSecondSeatSettings>();
Log.Message($"API 端点: {settings.apiEndpoint}");
Log.Message($"API 密钥: {settings.apiKey.Substring(0, 10)}...");
```

#### 问题：命令执行失败

**检查**:
```csharp
// 查看详细错误
var command = new ParsedCommand { action = "BatchHarvest", ... };
var result = GameActionExecutor.Execute(command);

if (!result.success)
{
    Log.Error($"命令失败: {result.message}");
    
    // 检查前置条件
    var map = Find.CurrentMap;
    var plants = map.listerThings.ThingsInGroup(ThingRequestGroup.HarvestablePlant);
    Log.Message($"可收获植物数量: {plants.Count()}");
}
```

#### 问题：好感度不变化

**检查**:
```csharp
var narrator = Current.Game.GetComponent<TheSecondSeat.Narrator.NarratorManager>();
Log.Message($"当前好感度: {narrator.Favorability}");

// 手动修改测试
narrator.ModifyFavorability(10f, "手动测试");
Log.Message($"修改后好感度: {narrator.Favorability}");

// 检查是否保存
Current.Game.Save("test_save");
// 加载后检查
```

### 6.2 日志分析

**关键日志标识**:
```
[The Second Seat] - 通用信息
[StorytellerAgent] - 人格系统
[Memory] - 记忆系统
[EventGenerator] - 事件系统
[GameActionExecutor] - 命令执行
[NarratorController] - 主控制器
[NLParser] - 自然语言解析
```

**示例日志分析**:
```
[The Second Seat] AI Narrator Assistant initialized successfully
[StorytellerAgent] 好感度变化: +2.0 (命令成功) -> 32.0
[Memory] 添加记忆: [对话] Player: 帮我收获作物
[NLParser] 解析结果: Action=BatchHarvest, Target=Mature, Confidence=95%
[GameActionExecutor] 执行命令: BatchHarvest (Target=Mature, Scope=Map)
[GameActionExecutor] 已指定 15 株植物收获
[NarratorController] 命令成功: 已指定 15 株植物收获
```

### 6.3 重置和清理

**完全重置模组状态**:
```csharp
// 警告：这会删除所有数据！

// 1. 重置好感度
var narrator = Current.Game.GetComponent<TheSecondSeat.Narrator.NarratorManager>();
narrator.ModifyFavorability(-narrator.Favorability, "重置");

// 2. 清空记忆
var adapter = RimTalkMemoryAdapter.Instance;
adapter.PruneOldMemories(0);

// 3. 重置 StorytellerAgent
var agent = narrator.GetStorytellerAgent();
agent.affinity = 0f;
agent.currentMood = MoodState.Neutral;
agent.totalConversations = 0;
agent.commandsExecuted = 0;

Log.Message("模组状态已重置");
```

---

## 7. 压力测试

### 7.1 连续对话测试

```csharp
// 测试连续100次对话
var controller = Current.Game.GetComponent<TheSecondSeat.Core.NarratorController>();

for (int i = 0; i < 100; i++)
{
    controller.TriggerNarratorUpdate($"测试消息 {i}");
    System.Threading.Thread.Sleep(5000); // 等待5秒
    Log.Message($"完成第 {i+1} 次对话");
}
```

### 7.2 大量记忆测试

```csharp
// 添加 1000 条记忆
for (int i = 0; i < 1000; i++)
{
    MemoryContextBuilder.RecordEvent($"测试事件 {i}", MemoryImportance.Medium);
}

// 测试检索
var sw = Stopwatch.StartNew();
var result = MemoryContextBuilder.BuildMemoryContext("测试", maxTokens: 1000);
sw.Stop();

Log.Message($"检索 1000 条记忆耗时: {sw.ElapsedMilliseconds} ms");
```

---

## 8. 自动化测试脚本

**创建测试场景文件**: `Tests/AutoTest.xml`

```xml
<?xml version="1.0" encoding="utf-8"?>
<TestScenario>
  <name>The Second Seat - Auto Test</name>
  <description>Automated testing scenario</description>
  
  <steps>
    <li>
      <action>InitializeNarrator</action>
      <affinity>0</affinity>
    </li>
    <li>
      <action>TestConversation</action>
      <message>Hello, can you hear me?</message>
      <expectedResponse>Contains dialogue</expectedResponse>
    </li>
    <li>
      <action>TestCommand</action>
      <command>BatchHarvest</command>
      <expectedResult>success=true</expectedResult>
    </li>
    <li>
      <action>TestAffinityChange</action>
      <delta>10</delta>
      <expectedValue>10</expectedValue>
    </li>
  </steps>
</TestScenario>
```

---

**版本**: 1.0.0  
**最后更新**: 2024  
**维护者**: TheSecondSeat 开发团队
