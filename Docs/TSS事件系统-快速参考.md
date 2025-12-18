# TSS事件系统 - 快速参考

## ?? 概述

**The Second Seat (TSS) 事件系统**是一个完全数据驱动的叙事者事件框架。所有事件逻辑都通过XML定义，无需编写C#代码。

### 核心概念

```
事件 (NarratorEventDef)
├── 触发器 (TSSTrigger)  ← "何时触发"
└── 行动 (TSSAction)     ← "做什么"
```

---

## ?? 快速开始

### 1. 创建简单事件

```xml
<TheSecondSeat.Framework.NarratorEventDef>
  <defName>MyFirstEvent</defName>
  <eventLabel>我的第一个事件</eventLabel>
  
  <!-- 触发条件：好感度>50 -->
  <triggers>
    <li Class="TheSecondSeat.Framework.Triggers.AffinityRangeTrigger">
      <minAffinity>50</minAffinity>
    </li>
  </triggers>
  
  <!-- 执行动作：显示消息 -->
  <actions>
    <li Class="TheSecondSeat.Framework.Actions.ShowDialogueAction">
      <dialogueText>你好！</dialogueText>
    </li>
  </actions>
</TheSecondSeat.Framework.NarratorEventDef>
```

---

## ?? 可用触发器

### AffinityRangeTrigger - 好感度范围
```xml
<li Class="TheSecondSeat.Framework.Triggers.AffinityRangeTrigger">
  <minAffinity>60</minAffinity>
  <maxAffinity>100</maxAffinity>
</li>
```

### ColonistCountTrigger - 殖民者数量
```xml
<li Class="TheSecondSeat.Framework.Triggers.ColonistCountTrigger">
  <minCount>5</minCount>
  <maxCount>20</maxCount>
</li>
```

### WealthRangeTrigger - 财富范围
```xml
<li Class="TheSecondSeat.Framework.Triggers.WealthRangeTrigger">
  <minWealth>50000</minWealth>
  <checkTotal>true</checkTotal>
</li>
```

### SeasonTrigger - 季节
```xml
<li Class="TheSecondSeat.Framework.Triggers.SeasonTrigger">
  <allowedSeasons>
    <li>Spring</li>
    <li>Summer</li>
  </allowedSeasons>
</li>
```

### TimeRangeTrigger - 时间段
```xml
<li Class="TheSecondSeat.Framework.Triggers.TimeRangeTrigger">
  <minHour>6</minHour>
  <maxHour>18</maxHour>
</li>
```

---

## ? 可用行动

### ModifyAffinityAction - 修改好感度
```xml
<li Class="TheSecondSeat.Framework.Actions.ModifyAffinityAction">
  <delta>10</delta>
  <reason>完成任务</reason>
</li>
```

### ShowDialogueAction - 显示对话
```xml
<li Class="TheSecondSeat.Framework.Actions.ShowDialogueAction">
  <dialogueText>欢迎回来！</dialogueText>
  <messageType>PositiveEvent</messageType>
</li>
```

### SpawnResourceAction - 生成资源
```xml
<li Class="TheSecondSeat.Framework.Actions.SpawnResourceAction">
  <resourceType>Steel</resourceType>
  <amount>100</amount>
  <dropNearPlayer>true</dropNearPlayer>
</li>
```

### TriggerEventAction - 触发其他事件
```xml
<li Class="TheSecondSeat.Framework.Actions.TriggerEventAction">
  <targetEventDefName>AnotherEvent</targetEventDefName>
</li>
```

---

## ?? 事件配置参数

### 基本参数
```xml
<defName>MyEvent</defName>           <!-- 唯一ID -->
<eventLabel>我的事件</eventLabel>    <!-- 显示名称 -->
<category>Reward</category>          <!-- 分类 -->
<priority>50</priority>              <!-- 优先级（越高越频繁检查） -->
```

### 触发控制
```xml
<chance>0.5</chance>                 <!-- 触发概率 (0.0-1.0) -->
<cooldownTicks>36000</cooldownTicks> <!-- 冷却时间（Tick） -->
<triggerOnce>true</triggerOnce>      <!-- 是否只触发一次 -->
<minIntervalTicks>3600</minIntervalTicks> <!-- 最小触发间隔 -->
```

### 时间换算
- 1秒 = 60 Tick
- 1分钟 = 3600 Tick
- 1游戏小时 = 2500 Tick
- 1游戏日 = 60000 Tick

---

## ?? 组合触发器

### AND逻辑（默认）
所有触发器都必须满足：
```xml
<triggers>
  <li Class="...AffinityRangeTrigger">...</li>
  <li Class="...ColonistCountTrigger">...</li>
  <!-- 两者都满足才触发 -->
</triggers>
```

### OR逻辑
使用CompositeTrigger：
```xml
<triggers>
  <li Class="TheSecondSeat.Framework.CompositeTrigger">
    <combineMode>Any</combineMode>
    <subTriggers>
      <li Class="...AffinityRangeTrigger">...</li>
      <li Class="...WealthRangeTrigger">...</li>
      <!-- 任意一个满足即可 -->
    </subTriggers>
  </li>
</triggers>
```

---

## ?? 延迟执行

### 单个Action延迟
```xml
<li Class="...ShowDialogueAction">
  <delayTicks>180</delayTicks>  <!-- 延迟3秒 -->
  <dialogueText>延迟消息</dialogueText>
</li>
```

### 串行执行（累加延迟）
```xml
<parallelExecution>false</parallelExecution>
<actions>
  <li Class="...ShowDialogueAction">
    <delayTicks>60</delayTicks>   <!-- 1秒后 -->
    <dialogueText>第一条消息</dialogueText>
  </li>
  <li Class="...ShowDialogueAction">
    <delayTicks>60</delayTicks>   <!-- 再延迟1秒（总共2秒） -->
    <dialogueText>第二条消息</dialogueText>
  </li>
</actions>
```

### 并行执行
```xml
<parallelExecution>true</parallelExecution>
<actions>
  <li Class="...">...</li>
  <li Class="...">...</li>
  <!-- 所有Action同时开始执行 -->
</actions>
```

---

## ?? 扩展方法

### 创建自定义Trigger

1. 创建C#类：
```csharp
namespace YourMod.Triggers
{
    public class CustomTrigger : TheSecondSeat.Framework.TSSTrigger
    {
        public float customValue;
        
        public override bool IsSatisfied(Map map, Dictionary<string, object> context)
        {
            // 你的检查逻辑
            return true;
        }
    }
}
```

2. 在XML中使用：
```xml
<li Class="YourMod.Triggers.CustomTrigger">
  <customValue>42</customValue>
</li>
```

### 创建自定义Action

1. 创建C#类：
```csharp
namespace YourMod.Actions
{
    public class CustomAction : TheSecondSeat.Framework.TSSAction
    {
        public string customParameter;
        
        public override void Execute(Map map, Dictionary<string, object> context)
        {
            // 你的执行逻辑
        }
    }
}
```

2. 在XML中使用：
```xml
<li Class="YourMod.Actions.CustomAction">
  <customParameter>value</customParameter>
</li>
```

---

## ?? 调试技巧

### 1. 启用开发者模式
游戏中按 `F12` 启用DevMode，查看详细日志

### 2. 手动触发事件
开发者控制台执行：
```csharp
TheSecondSeat.Framework.NarratorEventManager.Instance.ForceTriggerEvent("MyEvent");
```

### 3. 重置所有事件状态
```csharp
TheSecondSeat.Framework.NarratorEventManager.Instance.ResetAllEventStates();
```

### 4. 查看事件执行统计
```csharp
var stats = TheSecondSeat.Framework.NarratorEventManager.Instance.GetAllEventStats();
```

---

## ?? 示例文件

完整示例参见：`Defs/NarratorEventDefs.xml`

包含以下示例事件：
- `HighAffinityReward` - 高好感度奖励
- `LowAffinityWarning` - 低好感度警告
- `SpringGreeting` - 季节性问候
- `WealthMilestone` - 财富里程碑
- `EventChainStart` - 链式事件触发

---

## ?? 性能提示

1. **优先级设置**：高优先级事件（≥100）检查更频繁，谨慎使用
2. **冷却时间**：设置合理的冷却时间避免频繁触发
3. **触发器数量**：单个事件建议不超过5个触发器
4. **延迟执行**：大量Action建议使用延迟执行分散负载

---

## ? 常见问题

**Q: 事件没有触发？**
A: 检查日志中的 `[NarratorEventDef]` 和 `[TSSTrigger]` 消息

**Q: 如何禁用某个事件？**
A: 在事件Def中添加 `<enabled>false</enabled>`

**Q: 触发器的invert参数是什么？**
A: 反转条件，例如好感度>50变为好感度≤50

**Q: 可以在运行时动态创建事件吗？**
A: 不支持。所有事件必须通过XML定义

---

## ?? 技术支持

- 完整文档：查看代码注释
- 示例事件：`Defs/NarratorEventDefs.xml`
- 基础Trigger：`Framework/Triggers/BasicTriggers.cs`
- 基础Action：`Framework/Actions/BasicActions.cs`

---

**框架版本**: v1.0.0  
**最后更新**: 2025-01-XX
