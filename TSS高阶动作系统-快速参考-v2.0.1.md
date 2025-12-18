# TSS高阶动作系统 - 快速参考 v2.0.1

## ? 新增功能总览

### ?? 4个高阶动作类
1. **StrikeLightningAction** ? - 天罚雷击
2. **GiveHediffAction** ?? - 健康状态操控
3. **StartIncidentAction** ?? - 强制触发事件
4. **NarratorSpeakAction** ?? - 叙事者语音（?核心联动）

---

## ?? NarratorSpeakAction - 叙事者语音

### 基础用法
```xml
<li Class="TheSecondSeat.Framework.Actions.NarratorSpeakAction">
  <text>你做得很好！继续加油~</text>
  <showDialogue>true</showDialogue>
</li>
```

### 使用翻译键
```xml
<li Class="TheSecondSeat.Framework.Actions.NarratorSpeakAction">
  <textKey>TSS_Event_DivinePunishment</textKey>
  <text>你的所作所为让我不得不采取措施了...</text>  <!-- 翻译失败时的备用文本 -->
  <showDialogue>true</showDialogue>
</li>
```

### 指定人格声线
```xml
<li Class="TheSecondSeat.Framework.Actions.NarratorSpeakAction">
  <text>我是Sideria，很高兴见到你~</text>
  <personaDefName>Sideria_Default</personaDefName>  <!-- 使用特定人格的语音配置 -->
  <showDialogue>true</showDialogue>
</li>
```

### 仅播放语音（不显示对话框）
```xml
<li Class="TheSecondSeat.Framework.Actions.NarratorSpeakAction">
  <text>背景旁白...</text>
  <showDialogue>false</showDialogue>  <!-- 静默播放 -->
</li>
```

---

## ? StrikeLightningAction - 天罚雷击

### 随机雷击
```xml
<li Class="TheSecondSeat.Framework.Actions.StrikeLightningAction">
  <strikeMode>Random</strikeMode>
  <strikeCount>3</strikeCount>
  <damageAmount>100</damageAmount>
  <radius>5</radius>
</li>
```

### 雷击模式
- `Random` - 随机位置
- `MapCenter` - 地图中心
- `NearestEnemy` - 最近的敌人
- `NearestColonist` - 最近的殖民者（慎用！）
- `Specific` - 指定坐标

### 高级配置
```xml
<li Class="TheSecondSeat.Framework.Actions.StrikeLightningAction">
  <strikeMode>NearestEnemy</strikeMode>
  <strikeCount>10</strikeCount>
  <damageAmount>150</damageAmount>
  <radius>8</radius>
  <causesFire>true</causesFire>  <!-- 点燃火焰 -->
  <playSound>true</playSound>  <!-- 播放雷鸣音效 -->
</li>
```

---

## ?? GiveHediffAction - 健康状态操控

### 给殖民者治疗
```xml
<li Class="TheSecondSeat.Framework.Actions.GiveHediffAction">
  <hediffDef>Luciferium</hediffDef>
  <targetMode>Weakest</targetMode>
  <severity>0.01</severity>
  <targetCount>1</targetCount>
</li>
```

### 目标选择模式
- `Random` - 随机殖民者
- `AllColonists` - 所有殖民者
- `Healthiest` - 最健康的
- `Weakest` - 最虚弱的
- `RandomEnemy` - 随机敌人

### 瘟疫诅咒
```xml
<li Class="TheSecondSeat.Framework.Actions.GiveHediffAction">
  <hediffDef>Flu</hediffDef>
  <targetMode>Random</targetMode>
  <severity>0.5</severity>
  <targetCount>3</targetCount>
  <showNotification>true</showNotification>
</li>
```

### 常用Hediff
- **疾病**: `Flu`, `Plague`, `WoundInfection`
- **增益**: `PsychicHarmonizer`, `PsychicReader`
- **义体**: `BionicEye`, `BionicArm`
- **特殊**: `Luciferium`（魔鬼素依赖）

---

## ?? StartIncidentAction - 强制触发事件

### 召唤商队
```xml
<li Class="TheSecondSeat.Framework.Actions.StartIncidentAction">
  <incidentDef>TraderCaravanArrival</incidentDef>
  <forced>true</forced>
</li>
```

### 触发袭击
```xml
<li Class="TheSecondSeat.Framework.Actions.StartIncidentAction">
  <incidentDef>RaidEnemy</incidentDef>
  <points>500</points>  <!-- 袭击点数 -->
  <forced>true</forced>
  <targetFaction>Pirate</targetFaction>  <!-- 指定派系 -->
</li>
```

### 常用事件
- **袭击**: `RaidEnemy`, `RaidFriendly`, `MechCluster`
- **商队**: `TraderCaravanArrival`, `OrbitalTraderArrival`
- **自然**: `Tornado`, `Flashstorm`, `Eclipse`
- **动物**: `ManhunterPack`, `FarmAnimalsWanderIn`
- **特殊**: `WandererJoin`, `RefugeeChased`

---

## ?? 完整示例：多动作组合

### 示例1: 天罚降临（语音 + 雷击）
```xml
<TheSecondSeat.Framework.NarratorEventDef>
  <defName>DivinePunishment</defName>
  <eventLabel>天罚降临</eventLabel>
  <category>Punishment</category>
  <priority>70</priority>
  <chance>0.3</chance>
  <cooldownTicks>108000</cooldownTicks>
  
  <triggers>
    <li Class="TheSecondSeat.Framework.Triggers.AffinityRangeTrigger">
      <minAffinity>-100</minAffinity>
      <maxAffinity>-70</maxAffinity>
    </li>
  </triggers>
  
  <actions>
    <!-- 1. 语音警告 -->
    <li Class="TheSecondSeat.Framework.Actions.NarratorSpeakAction">
      <text>你的所作所为...让我不得不采取措施了。</text>
      <showDialogue>true</showDialogue>
    </li>
    
    <!-- 2. 延迟3秒后降雷 -->
    <li Class="TheSecondSeat.Framework.Actions.StrikeLightningAction">
      <delayTicks>180</delayTicks>
      <strikeMode>Random</strikeMode>
      <strikeCount>5</strikeCount>
      <damageAmount>80</damageAmount>
    </li>
    
    <!-- 3. 降低好感度 -->
    <li Class="TheSecondSeat.Framework.Actions.ModifyAffinityAction">
      <delayTicks>180</delayTicks>
      <delta>-10</delta>
      <reason>天罚惩罚</reason>
    </li>
  </actions>
  
  <showNotification>true</showNotification>
  <customNotificationText>? 天罚降临！</customNotificationText>
</TheSecondSeat.Framework.NarratorEventDef>
```

### 示例2: 神秘商队（语音 + 音效 + 事件）
```xml
<TheSecondSeat.Framework.NarratorEventDef>
  <defName>MysteriousTrader</defName>
  <eventLabel>神秘商队</eventLabel>
  <category>Reward</category>
  
  <actions>
    <!-- 1. 语音通知 -->
    <li Class="TheSecondSeat.Framework.Actions.NarratorSpeakAction">
      <text>我叫了个朋友来看看你~</text>
      <showDialogue>true</showDialogue>
    </li>
    
    <!-- 2. 音效 -->
    <li Class="TheSecondSeat.Framework.Actions.PlaySoundAction">
      <delayTicks>60</delayTicks>
      <sound>TradeShip_Ambience</sound>
    </li>
    
    <!-- 3. 触发商队 -->
    <li Class="TheSecondSeat.Framework.Actions.StartIncidentAction">
      <delayTicks>300</delayTicks>
      <incidentDef>TraderCaravanArrival</incidentDef>
      <forced>true</forced>
    </li>
  </actions>
</TheSecondSeat.Framework.NarratorEventDef>
```

---

## ?? 使用注意事项

### NarratorSpeakAction
- ? TTS服务需要正确配置
- ? 文本长度建议控制在50字以内
- ? `personaDefName` 留空则使用当前人格
- ? 异步播放，不会阻塞游戏

### StrikeLightningAction
- ?? 伤害量建议 50-150
- ?? 不要对殖民者使用（除非故意惩罚）
- ?? `causesFire=true` 可能引发火灾

### GiveHediffAction
- ?? `severity` 0.5+ 会造成明显影响
- ?? `AllColonists` 模式影响所有人
- ?? 某些Hediff（如Luciferium）造成永久依赖

### StartIncidentAction
- ?? `forced=true` 忽略冷却和条件
- ?? `points` 过高可能无法应对
- ?? 建议检查好感度再触发

---

## ?? 文件位置

```
Source/TheSecondSeat/Framework/Actions/
├── BasicActions.cs         # 基础动作（含PlaySoundAction）
└── AdvancedActions.cs      # 高阶动作（4个）

Defs/
└── NarratorEventDefs.xml   # 示例事件（10个）
```

---

## ?? 快速测试

### 1. 测试语音播放
```xml
<li Class="TheSecondSeat.Framework.Actions.NarratorSpeakAction">
  <text>测试语音播放~</text>
  <showDialogue>true</showDialogue>
</li>
```

### 2. 测试雷击效果
```xml
<li Class="TheSecondSeat.Framework.Actions.StrikeLightningAction">
  <strikeMode>MapCenter</strikeMode>
  <strikeCount>1</strikeCount>
</li>
```

### 3. 测试事件触发
```xml
<li Class="TheSecondSeat.Framework.Actions.StartIncidentAction">
  <incidentDef>TraderCaravanArrival</incidentDef>
  <forced>true</forced>
</li>
```

---

## ?? 相关文档

- **完整实现报告**: `TSS高阶动作系统-完成报告-v2.0.1.md`
- **TSS事件系统**: `Docs/TSS事件系统-快速参考.md`
- **降临系统**: `叙事者降临模式-快速参考-v2.0.0.md`

---

**版本**: v2.0.1  
**更新时间**: 2025-01-XX  
**状态**: ? 开发完成，编译通过
