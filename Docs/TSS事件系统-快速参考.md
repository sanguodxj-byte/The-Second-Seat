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

### PlaySoundAction - 播放音效
```xml
<li Class="TheSecondSeat.Framework.Actions.PlaySoundAction">
  <sound>ThunderOnMap</sound>
  <volume>1.0</volume>
</li>
```

### ? NarratorSpeakAction - 叙事者语音（核心联动）
```xml
<li Class="TheSecondSeat.Framework.Actions.NarratorSpeakAction">
  <textKey>TSS_Event_DivinePunishment</textKey>  <!-- 翻译键（优先） -->
  <text>你的所作所为让我不得不采取措施了...</text>  <!-- 直接文本（备用） -->
  <personaDefName>Sideria_Default</personaDefName>  <!-- 可选：指定人格 -->
  <showDialogue>true</showDialogue>  <!-- 是否显示对话框 -->
</li>
```

**功能特性**:
- ? **TTS语音合成** - 调用框架内置的TTSService
- ? **多模式文本** - 支持翻译键或直接文本
- ? **人格声线** - 自动使用对应人格的语音配置
- ? **对话显示** - 可选在对话框显示
- ? **异步播放** - 不阻塞游戏主线程

**使用场景**:
- 好感度事件的语音反馈
- 降临模式的语音宣告
- 教程引导的语音提示
- 特殊事件的语音旁白

---

## ?? 高阶动作（上帝级）

?? **警告：这些动作权限很高，请谨慎使用！**

### StrikeLightningAction - 降下雷劈
```xml
<li Class="TheSecondSeat.Framework.Actions.StrikeLightningAction">
  <strikeMode>Random</strikeMode>        <!-- Random/MapCenter/NearestEnemy/NearestColonist/Specific -->
  <strikeCount>3</strikeCount>           <!-- 雷击次数 -->
  <damageAmount>100</damageAmount>       <!-- 伤害量 -->
  <radius>5</radius>                     <!-- AOE范围 -->
  <causesFire>true</causesFire>          <!-- 是否造成火灾 -->
  <playSound>true</playSound>            <!-- 是否播放音效 -->
</li>
```

**雷击模式**:
- `Random` - 随机位置
- `MapCenter` - 地图中心
- `NearestEnemy` - 最近的敌人
- `NearestColonist` - 最近的殖民者（慎用！）
- `Specific` - 指定坐标（需设置targetCell）

### GiveHediffAction - 添加健康状态
```xml
<li Class="TheSecondSeat.Framework.Actions.GiveHediffAction">
  <hediffDef>Flu</hediffDef>             <!-- 疾病/增益Def -->
  <targetMode>Random</targetMode>         <!-- Random/AllColonists/Healthiest/Weakest/RandomEnemy -->
  <severity>0.5</severity>                <!-- 严重程度 (0.0-1.0) -->
  <targetCount>3</targetCount>            <!-- 目标数量 -->
  <targetBodyPart>LeftLeg</targetBodyPart><!-- 身体部位（可选） -->
  <showNotification>true</showNotification>
</li>
```

**目标选择模式**:
- `Random` - 随机殖民者
- `AllColonists` - 所有殖民者
- `Healthiest` - 最健康的殖民者
- `Weakest` - 最虚弱的殖民者
- `RandomEnemy` - 随机敌人

**常用HediffDef**:
- 疾病: `Flu`, `Plague`, `WoundInfection`
- 增益: `PsychicHarmonizer`, `PsychicReader`
- 义体: `BionicEye`, `BionicArm`
- 特殊: `Luciferium` (魔鬼素依赖)

### StartIncidentAction - 强制触发原版事件
```xml
<li Class="TheSecondSeat.Framework.Actions.StartIncidentAction">
  <incidentDef>RaidEnemy</incidentDef>   <!-- 事件Def -->
  <points>500</points>                    <!-- 事件点数（-1表示默认） -->
  <forced>true</forced>                   <!-- 是否强制触发 -->
  <targetFaction>Pirate</targetFaction>   <!-- 目标派系（可选） -->
  <allowBigThreat>true</allowBigThreat>   <!-- 是否允许大规模事件 -->
</li>
```

**常用IncidentDef**:
- 袭击: `RaidEnemy`, `RaidFriendly`, `MechCluster`
- 商队: `TraderCaravanArrival`, `OrbitalTraderArrival`
- 自然: `Tornado`, `Flashstorm`, `Eclipse`
- 动物: `ManhunterPack`, `FarmAnimalsWanderIn`
- 特殊: `WandererJoin`, `RefugeeChased`, `QuestThreat`

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
