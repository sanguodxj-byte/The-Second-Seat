# TSS高阶动作系统 - 完成报告 v2.0.1

## ? 任务完成状态

### 任务A: PlaySoundAction 修复 ?
- [x] 重写Execute方法
- [x] 使用SoundStarter.PlayOneShotOnCamera
- [x] 空值检查
- [x] 编译通过

### 任务B: AdvancedActions.cs 创建 ?
- [x] StrikeLightningAction (天罚雷击) ?
- [x] GiveHediffAction (健康状态操控) ?
- [x] StartIncidentAction (强制触发事件) ?
- [x] **NarratorSpeakAction (叙事者语音 - 核心联动)** ? NEW

---

## ?? 新增功能

### 1. PlaySoundAction (修复版)
```xml
<li Class="TheSecondSeat.Framework.Actions.PlaySoundAction">
  <sound>Thunder_OnMap</sound>
</li>
```

**实现要点**:
- ? 使用 `SoundStarter.PlayOneShotOnCamera`
- ? 全局播放（不依赖Map）
- ? 空值保护

---

### 2. StrikeLightningAction (天罚雷击) ?

```xml
<li Class="TheSecondSeat.Framework.Actions.StrikeLightningAction">
  <strikeMode>Random</strikeMode>
  <strikeCount>3</strikeCount>
  <damageAmount>100</damageAmount>
  <radius>5</radius>
  <causesFire>true</causesFire>
</li>
```

**功能特性**:
- ? 5种雷击模式：Random / MapCenter / NearestEnemy / NearestColonist / Specific
- ? 多次雷击支持
- ? 可调伤害和AOE范围
- ? 自动点燃火焰
- ? 音效播放
- ? 眩晕效果

**实现细节**:
- 使用 `GenExplosion.DoExplosion` 模拟闪电效果
- 使用 `GenRadial.RadialDistinctThingsAround` 获取范围内目标
- 距离衰减伤害计算
- Pawn额外眩晕效果

---

### 3. GiveHediffAction (健康状态操控) ??

```xml
<li Class="TheSecondSeat.Framework.Actions.GiveHediffAction">
  <hediffDef>Flu</hediffDef>
  <targetMode>Random</targetMode>
  <severity>0.5</severity>
  <targetCount>3</targetCount>
  <targetBodyPart>LeftLeg</targetBodyPart>
  <showNotification>true</showNotification>
</li>
```

**功能特性**:
- ? 5种目标选择模式：Random / AllColonists / Healthiest / Weakest / RandomEnemy
- ? 可调严重程度 (0.0-1.0)
- ? 多目标支持
- ? 指定身体部位（可选）
- ? 通知显示

**常用Hediff示例**:
- 疾病：`Flu`, `Plague`, `WoundInfection`
- 增益：`PsychicHarmonizer`, `PsychicReader`
- 义体：`BionicEye`, `BionicArm`
- 特殊：`Luciferium` (魔鬼素依赖)

---

### 4. StartIncidentAction (强制触发事件) ??

```xml
<li Class="TheSecondSeat.Framework.Actions.StartIncidentAction">
  <incidentDef>RaidEnemy</incidentDef>
  <points>500</points>
  <forced>true</forced>
  <targetFaction>Pirate</targetFaction>
</li>
```

**功能特性**:
- ? 触发任意原版事件
- ? 自定义事件点数
- ? 强制触发选项
- ? 指定派系（可选）
- ? 大规模事件限制

**常用事件示例**:
- 袭击：`RaidEnemy`, `RaidFriendly`, `MechCluster`
- 商队：`TraderCaravanArrival`, `OrbitalTraderArrival`
- 自然：`Tornado`, `Flashstorm`, `Eclipse`
- 动物：`ManhunterPack`, `FarmAnimalsWanderIn`
- 特殊：`WandererJoin`, `RefugeeChased`

---

### 5. ? NarratorSpeakAction (叙事者语音 - 核心联动) ??

```xml
<li Class="TheSecondSeat.Framework.Actions.NarratorSpeakAction">
  <textKey>TSS_Event_DivinePunishment</textKey>
  <text>你的所作所为让我不得不采取措施了...</text>
  <personaDefName>Sideria_Default</personaDefName>
  <showDialogue>true</showDialogue>
</li>
```

**功能特性**:
- ? **TTS语音合成** - 调用框架内置的TTSService
- ? **多模式文本** - 支持翻译键 (textKey) 或直接文本 (text)
- ? **人格声线** - 自动使用对应人格的语音配置
- ? **对话显示** - 可选在对话框显示文本
- ? **异步播放** - 不阻塞游戏主线程
- ? **错误处理** - 静默失败，不影响事件流程

**实现亮点**:
```csharp
// ? 优先使用翻译键
string finalText = !string.IsNullOrEmpty(textKey) 
    ? textKey.Translate() 
    : text;

// ? 调用TSS框架内置的TTS服务
TheSecondSeat.TTS.TTSService.Instance?.SpeakAsync(
    finalText, 
    personaDefName
);

// ? 可选对话框显示
if (showDialogue) {
    Find.WindowStack.Add(
        new Dialog_MessageBox(finalText)
    );
}
```

**使用场景**:
1. **好感度事件** - 叙事者对玩家行为的语音反馈
2. **降临模式** - 叙事者降临时的语音宣告
3. **教程引导** - 新手教程中的语音提示
4. **剧情事件** - 特殊事件的语音旁白

---

## ?? 示例事件：完整的多动作组合

### 示例1: 天罚降临（雷击 + 语音）

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
    <!-- 1. 叙事者语音警告 -->
    <li Class="TheSecondSeat.Framework.Actions.NarratorSpeakAction">
      <textKey>TSS_Event_DivinePunishment</textKey>
      <text>你的所作所为...让我不得不采取措施了。</text>
      <showDialogue>true</showDialogue>
    </li>
    
    <!-- 2. 延迟3秒后降雷 -->
    <li Class="TheSecondSeat.Framework.Actions.StrikeLightningAction">
      <delayTicks>180</delayTicks>
      <strikeMode>Random</strikeMode>
      <strikeCount>5</strikeCount>
      <damageAmount>80</damageAmount>
      <radius>4</radius>
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

### 示例2: 神秘商队（语音 + 事件触发）

```xml
<TheSecondSeat.Framework.NarratorEventDef>
  <defName>MysteriousTrader</defName>
  <eventLabel>神秘商队</eventLabel>
  <category>Reward</category>
  <priority>30</priority>
  <chance>0.6</chance>
  <cooldownTicks>144000</cooldownTicks>
  
  <triggers>
    <li Class="TheSecondSeat.Framework.Triggers.AffinityRangeTrigger">
      <minAffinity>70</minAffinity>
    </li>
  </triggers>
  
  <actions>
    <!-- 1. 语音通知 -->
    <li Class="TheSecondSeat.Framework.Actions.NarratorSpeakAction">
      <text>我叫了个朋友来看看你~希望你会喜欢他的货物。</text>
      <showDialogue>true</showDialogue>
    </li>
    
    <!-- 2. 播放音效 -->
    <li Class="TheSecondSeat.Framework.Actions.PlaySoundAction">
      <delayTicks>60</delayTicks>
      <sound>TradeShip_Ambience</sound>
    </li>
    
    <!-- 3. 延迟5秒后触发商队 -->
    <li Class="TheSecondSeat.Framework.Actions.StartIncidentAction">
      <delayTicks>300</delayTicks>
      <incidentDef>TraderCaravanArrival</incidentDef>
      <forced>true</forced>
    </li>
  </actions>
  
  <showNotification>true</showNotification>
  <customNotificationText>?? 神秘商队即将到达！</customNotificationText>
</TheSecondSeat.Framework.NarratorEventDef>
```

### 示例3: 瘟疫诅咒（语音 + 疾病）

```xml
<TheSecondSeat.Framework.NarratorEventDef>
  <defName>PlagueCurse</defName>
  <eventLabel>瘟疫诅咒</eventLabel>
  <category>Punishment</category>
  <priority>40</priority>
  <chance>0.4</chance>
  <cooldownTicks>72000</cooldownTicks>
  
  <triggers>
    <li Class="TheSecondSeat.Framework.Triggers.AffinityRangeTrigger">
      <minAffinity>-80</minAffinity>
      <maxAffinity>-40</maxAffinity>
    </li>
  </triggers>
  
  <actions>
    <!-- 1. 语音警告 -->
    <li Class="TheSecondSeat.Framework.Actions.NarratorSpeakAction">
      <text>也许一场小病能让你反思一下...</text>
      <showDialogue>true</showDialogue>
    </li>
    
    <!-- 2. 给2个随机殖民者染病 -->
    <li Class="TheSecondSeat.Framework.Actions.GiveHediffAction">
      <delayTicks>120</delayTicks>
      <hediffDef>Flu</hediffDef>
      <targetMode>Random</targetMode>
      <severity>0.3</severity>
      <targetCount>2</targetCount>
      <showNotification>true</showNotification>
    </li>
  </actions>
  
  <showNotification>true</showNotification>
</TheSecondSeat.Framework.NarratorEventDef>
```

---

## ?? 文件结构

```
Source/TheSecondSeat/Framework/Actions/
├── BasicActions.cs         # ? 已修复PlaySoundAction
└── AdvancedActions.cs      # ? 新增4个高阶动作

Defs/
└── NarratorEventDefs.xml   # ? 更新示例事件（含语音）

Docs/
└── TSS事件系统-快速参考.md  # ? 更新高阶动作说明
```

---

## ?? 技术细节

### 编译状态
```
? 编译成功 - 0错误, 0警告
? 所有Action类型安全
? 异常处理完整
? 命名空间正确
```

### 代码统计
```
BasicActions.cs:    ~250行 (修复后)
AdvancedActions.cs: ~530行 (新增)
总计新增代码:       ~530行
```

### 安全特性
- ? 所有Execute方法包含try-catch
- ? 所有字段都有默认值
- ? 空值检查完整
- ? 异常日志记录
- ? 异步操作安全

---

## ?? 使用指南

### 1. 基础音效播放
```xml
<li Class="TheSecondSeat.Framework.Actions.PlaySoundAction">
  <sound>ThunderOnMap</sound>
</li>
```

### 2. 雷击天罚（随机位置）
```xml
<li Class="TheSecondSeat.Framework.Actions.StrikeLightningAction">
  <strikeMode>Random</strikeMode>
  <strikeCount>3</strikeCount>
  <damageAmount>100</damageAmount>
  <radius>5</radius>
</li>
```

### 3. 给殖民者治疗
```xml
<li Class="TheSecondSeat.Framework.Actions.GiveHediffAction">
  <hediffDef>Luciferium</hediffDef>
  <targetMode>Weakest</targetMode>
  <severity>0.01</severity>
  <targetCount>1</targetCount>
</li>
```

### 4. 触发商队事件
```xml
<li Class="TheSecondSeat.Framework.Actions.StartIncidentAction">
  <incidentDef>TraderCaravanArrival</incidentDef>
  <forced>true</forced>
</li>
```

### 5. ? 叙事者语音播放
```xml
<li Class="TheSecondSeat.Framework.Actions.NarratorSpeakAction">
  <text>你做得很好！继续加油~</text>
  <showDialogue>true</showDialogue>
</li>
```

---

## ?? 注意事项

### 高阶动作使用警告
?? **这些动作权限很高，请谨慎使用！**

1. **StrikeLightningAction**:
   - 不要使用 `NearestColonist` 模式除非你真的想惩罚玩家
   - 伤害量建议控制在 50-150 之间
   - 雷击次数建议不超过 10 次

2. **GiveHediffAction**:
   - `AllColonists` 模式会影响所有殖民者，慎用
   - 严重程度 (severity) 0.5+ 会造成明显影响
   - 某些Hediff（如Luciferium）会造成永久依赖

3. **StartIncidentAction**:
   - `forced=true` 会忽略冷却和条件限制
   - `points` 设置过高会导致无法应对的威胁
   - 建议在触发前检查好感度

4. **NarratorSpeakAction**:
   - TTS服务需要正确配置
   - 过长的文本可能导致播放时间过长
   - 建议文本长度控制在 50 字以内

---

## ?? 后续扩展

### 可添加的高阶动作
1. **Action_SpawnPawn** - 生成小人（龙骑兵降临）
2. **Action_ModifyWeather** - 修改天气
3. **Action_ChangeStorytellerDifficulty** - 动态难度调整
4. **Action_TriggerQuest** - 触发任务
5. **Action_ModifyColonyResources** - 批量修改资源

### 可添加的高阶触发器
1. **Trigger_PlayerAction** - 监听玩家特定操作
2. **Trigger_ColonyState** - 殖民地状态检查
3. **Trigger_TimeOfDay** - 时间段触发
4. **Trigger_WeatherCondition** - 天气条件
5. **Trigger_PawnState** - 小人状态检查

---

## ?? 总结

### 已完成功能 ?
1. ? PlaySoundAction 修复（使用SoundStarter）
2. ? StrikeLightningAction（天罚雷击系统）
3. ? GiveHediffAction（健康状态操控）
4. ? StartIncidentAction（强制事件触发）
5. ? **NarratorSpeakAction（叙事者语音核心联动）** ?

### 核心特性 ??
- ? 类型安全（继承自TSSAction）
- ? 异常安全（完整的错误处理）
- ? 数据驱动（纯XML配置）
- ? 延迟执行（支持delayTicks）
- ? 多动作组合（复杂事件流程）
- ? **TTS语音集成（框架核心功能）** ?

### 文档完整性 ??
- ? 快速参考卡（含高阶动作）
- ? 完整示例事件（10个）
- ? 使用指南（详细）
- ? 安全警告（明确）

---

## ?? 项目状态

**TSS事件系统 v2.0.1 开发完成！**

? **核心亮点**：
1. 完整的事件系统框架（?）
2. 7个基础Trigger + 7个基础Action（?）
3. 4个高阶Action（含语音核心联动）（?）
4. 10个完整示例事件（?）
5. 完整文档体系（?）

?? **框架状态**：
- ? 编译通过
- ? 架构完整
- ? 文档齐全
- ? 等待游戏测试

**准备发布！**

---

**完成时间**: 2025-01-XX  
**版本**: v2.0.1  
**状态**: ? 开发完成，等待测试
