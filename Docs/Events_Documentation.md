# 事件与降临系统文档

本文档介绍了 `The Second Seat` 提供的事件系统和降临机制的配置方法。

## 1. 降临系统 (Descent System)
降临系统允许叙事者实体化进入游戏世界。

### 配置方法
在 `NarratorPersonaDef` 中配置降临相关参数。

```xml
<NarratorPersonaDef>
  <defName>MyNarrator</defName>
  <!-- 启用降临模式 -->
  <hasDescentMode>true</hasDescentMode>
  <!-- 降临持续时间 (秒) -->
  <descentDuration>300</descentDuration>
  <!-- 降临冷却时间 (秒) -->
  <descentCooldown>600</descentCooldown>
  
  <!-- 降临实体 PawnKind -->
  <descentPawnKind>MyNarrator_DescentRace</descentPawnKind>
  
  <!-- 降临动画类型 -->
  <!-- 可选值: "DropPod" (默认), "Portal", "Lightning", "DragonFlyby" -->
  <descentAnimationType>Lightning</descentAnimationType>
  
  <!-- 降临音效 -->
  <descentSound>Thunder_OnMap</descentSound>
</NarratorPersonaDef>
```

### 动画类型说明
*   **DropPod**: 标准空投仓降临。
*   **Portal**: 传送门折跃降临，伴随雷电特效。
*   **Lightning**: 剧烈的闪电风暴，随后实体在雷击点生成。
*   **DragonFlyby**: 巨龙飞掠阴影，随后实体生成（需要配置 `dragonShadowTexturePath`）。

---

## 2. 召唤生物 (Summoned Creature)
**类名**: `TheSecondSeat.DefModExtension_SummonedCreature`
**适用 Def**: `ThingDef` (Pawn) 或 `PawnKindDef`

### 功能概述
定义召唤生物的生命周期和消散行为。

### 参数说明
| 参数名 | 类型 | 默认值 | 说明 |
| :--- | :--- | :--- | :--- |
| `isSpiritDragon` | bool | `false` | 标记该生物是否为灵体龙（可能触发特殊逻辑）。 |
| `dissipationHediffDefName` | string | `null` | 生物消散时应用的 Hediff DefName（通常用于播放消散动画或效果）。 |
| `lifetimeTicks` | int | `2500` | 生物的存活时间（Tick）。默认约 1 分钟。 |

### 示例
```xml
<ThingDef ParentName="AnimalThingBase">
  <defName>TSS_Example_Summon</defName>
  <modExtensions>
    <li Class="TheSecondSeat.DefModExtension_SummonedCreature">
      <lifetimeTicks>5000</lifetimeTicks>
      <dissipationHediffDefName>TSS_DissipationEffect</dissipationHediffDefName>
    </li>
  </modExtensions>
</ThingDef>
```

---

## 3. 闪电事件 (Lightning Event)
**类名**: `TheSecondSeat.Framework.Actions.StrikeLightningAction`
**适用 Def**: `NarratorEventDef` (作为 Action)

### 功能概述
在地图上触发雷击事件。

### 参数说明
| 参数名 | 类型 | 默认值 | 说明 |
| :--- | :--- | :--- | :--- |
| `strikeMode` | Enum | `Random` | 雷击模式：`Random` (随机), `MapCenter` (地图中心), `NearestEnemy` (最近敌人), `NearestColonist` (最近殖民者), `Specific` (指定位置)。 |
| `strikeCount` | int | `1` | 雷击次数。 |
| `damageAmount` | int | `10` | 雷击伤害（如果适用）。 |

### 示例
```xml
<NarratorEventDef>
  <defName>TSS_Event_LightningStrike</defName>
  <actions>
    <li Class="TheSecondSeat.Framework.Actions.StrikeLightningAction">
      <strikeMode>NearestEnemy</strikeMode>
      <strikeCount>3</strikeCount>
    </li>
  </actions>
</NarratorEventDef>
