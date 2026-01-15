# 技能与 Job 扩展文档

本文档介绍了 `The Second Seat` 提供的技能和 Job 相关的高级 XML 扩展功能。

## 1. 抓取机制 (Grab Job)
**类名**: `TheSecondSeat.DefModExtension_GrabJob`
**适用 Def**: `JobDef`

### 功能概述
用于定义抓取/投掷类 Job 中，被抓取目标应获得的 Hediff。这允许自定义抓取技能的效果（如眩晕、浮空、被束缚等）。

### 参数说明
| 参数名 | 类型 | 默认值 | 说明 |
| :--- | :--- | :--- | :--- |
| `grabbedHediffDefName` | string | `null` | 当 Pawn 被此 Job 抓取时，赋予目标的 Hediff DefName。 |

### 示例
```xml
<JobDef>
  <defName>TSS_Example_GrabJob</defName>
  <driverClass>TheSecondSeat.Jobs.JobDriver_CalamityHold</driverClass>
  <reportString>grabbing TargetA.</reportString>
  <modExtensions>
    <li Class="TheSecondSeat.DefModExtension_GrabJob">
      <grabbedHediffDefName>TSS_BeingHeld</grabbedHediffDefName>
    </li>
  </modExtensions>
</JobDef>
```

---

## 2. 技能效果组件 (CompAbilityEffect)

### 2.1 唤龙之门 (Dragon Gate)
**类名**: `TheSecondSeat.CompProperties_AbilityDragonGate`
**适用 Def**: `AbilityDef`

#### 功能概述
召唤一只来自灵界的巨龙（或其他生物）。召唤物会在一段时间后消散。

#### 参数说明
| 参数名 | 类型 | 默认值 | 说明 |
| :--- | :--- | :--- | :--- |
| `dragonDurationTicks` | int | `15000` | 召唤物存在时间（Tick）。 |
| `summonRadius` | float | `5f` | 召唤范围半径。 |
| `dragonKindDefNames` | List\<string\> | - | 可召唤的 PawnKindDef 名称列表（随机选择）。 |
| `dissipationHediffDefName` | string | - | 召唤物消散时应用的 Hediff DefName（用于控制消散逻辑）。 |

#### 示例
```xml
<AbilityDef>
  <defName>TSS_DragonGate</defName>
  <comps>
    <li Class="TheSecondSeat.CompProperties_AbilityDragonGate">
      <dragonDurationTicks>15000</dragonDurationTicks>
      <dragonKindDefNames>
        <li>TSS_SpiritDragon</li>
      </dragonKindDefNames>
      <dissipationHediffDefName>TSS_DragonDissipation</dissipationHediffDefName>
    </li>
  </comps>
</AbilityDef>
```

### 2.2 猩红绽放 (Crimson Bloom)
**类名**: `TheSecondSeat.CompProperties_AbilityCrimsonBloom`
**适用 Def**: `AbilityDef`

#### 功能概述
每次攻击施加一层标记，达到指定层数后触发“绽放”，摧毁目标的核心身体部件。

#### 参数说明
| 参数名 | 类型 | 默认值 | 说明 |
| :--- | :--- | :--- | :--- |
| `stacksToBloom` | int | `3` | 触发绽放所需的层数。 |
| `markDuration` | float | `30f` | 标记持续时间（秒）。 |
| `markHediffDefName` | string | - | 用于标记的 Hediff DefName。 |

#### 示例
```xml
<AbilityDef>
  <defName>TSS_CrimsonBloom</defName>
  <comps>
    <li Class="TheSecondSeat.CompProperties_AbilityCrimsonBloom">
      <stacksToBloom>3</stacksToBloom>
      <markHediffDefName>TSS_CrimsonMark</markHediffDefName>
    </li>
  </comps>
</AbilityDef>
```

### 2.3 灾厄投掷 (Calamity Throw)
**类名**: `TheSecondSeat.CompProperties_AbilityEffect_CalamityThrow`
**适用 Def**: `AbilityDef`

#### 功能概述
直接抓取目标并进入持有状态，准备进行投掷或猛击。

#### 参数说明
| 参数名 | 类型 | 默认值 | 说明 |
| :--- | :--- | :--- | :--- |
| `damageMultiplier` | float | `2.0` | 投掷/猛击造成的伤害倍率。 |
| `bypassGrappleCheck` | bool | `true` | 是否跳过原版抓取判定（100%成功）。 |
| `holdJobDefName` | string | - | 持有状态的 JobDef 名称。 |
| `damageMultiplierHediffDefName` | string | - | 用于存储伤害倍率的 Hediff DefName。 |

#### 示例
```xml
<AbilityDef>
  <defName>TSS_CalamityThrow</defName>
  <comps>
    <li Class="TheSecondSeat.CompProperties_AbilityEffect_CalamityThrow">
      <holdJobDefName>TSS_CalamityHold</holdJobDefName>
      <damageMultiplierHediffDefName>TSS_DamageMultiplier</damageMultiplierHediffDefName>
    </li>
  </comps>
</AbilityDef>
```

---

## 3. 灾厄持有动作 (Calamity Hold Actions)
**类名**: `TheSecondSeat.CompProperties_CalamityHoldActions`
**适用 Def**: `ThingDef` (Pawn)

### 功能概述
当 Pawn 处于持有目标状态（CalamityHold）时，提供“投掷”和“猛击”的操作按钮。

### 参数说明
| 参数名 | 类型 | 默认值 | 说明 |
| :--- | :--- | :--- | :--- |
| `throwJobDefName` | string | `Sideria_CalamityThrow` | 投掷动作的 JobDef 名称。 |
| `slamJobDefName` | string | `Sideria_CalamitySlam` | 猛击动作的 JobDef 名称。 |

### 示例
```xml
<ThingDef ParentName="BasePawn">
  <comps>
    <li Class="TheSecondSeat.CompProperties_CalamityHoldActions">
      <throwJobDefName>TSS_ThrowJob</throwJobDefName>
      <slamJobDefName>TSS_SlamJob</slamJobDefName>
    </li>
  </comps>
</ThingDef>
```

---

## 4. Hediff 组件 (HediffComp)

### 4.1 通用光环组件 (Apply Aura)
**类名**: `TheSecondSeat.Hediffs.HediffCompProperties_ApplyAura`
**适用 Def**: `HediffDef` (需要使用 `HediffWithComps`)

#### 功能概述
通用光环效果组件，可以周期性地对范围内的目标应用 Hediff。支持多种叠层模式，灵活配置目标筛选。

#### 参数说明
| 参数名 | 类型 | 默认值 | 说明 |
| :--- | :--- | :--- | :--- |
| `radius` | float | `9.9` | 光环作用半径。 |
| `checkInterval` | int | `60` | 检查间隔（Tick）。60 Tick = 1 秒。 |
| `hediffToApply` | HediffDef | - | 要应用的 HediffDef（引用）。 |
| `affectEnemies` | bool | `true` | 是否影响敌对目标。 |
| `affectAllies` | bool | `false` | 是否影响友方目标。 |
| `affectSelf` | bool | `false` | 是否影响自身。 |
| `stackMode` | StackMode | `None` | 叠层模式：None（不叠加）、Severity（增加严重度）、CustomMethod（调用自定义方法）。 |
| `stackMethodName` | string | `null` | 自定义叠层方法名（仅 CustomMethod 模式）。方法签名：`void MethodName(Pawn applier)` |
| `initialSeverity` | float | `0.5` | 初始严重度（首次应用时）。 |
| `severityIncrease` | float | `0.1` | 每次叠加增加的严重度（仅 Severity 模式）。 |
| `maxSeverity` | float | `1.0` | 最大严重度（仅 Severity 模式）。 |

#### StackMode 枚举说明
- **None**: 仅首次应用 Hediff，不叠加。
- **Severity**: 每次检查增加严重度，直到达到最大值。
- **CustomMethod**: 调用 Hediff 上的自定义方法（通过反射）。

#### 示例 1：简单光环（每秒对敌人施加 Hediff）
```xml
<HediffDef ParentName="HediffBase">
  <defName>TSS_AuraEffect</defName>
  <hediffClass>HediffWithComps</hediffClass>
  <comps>
    <li Class="TheSecondSeat.Hediffs.HediffCompProperties_ApplyAura">
      <radius>9.9</radius>
      <checkInterval>60</checkInterval>
      <hediffToApply>TSS_TargetDebuff</hediffToApply>
      <affectEnemies>true</affectEnemies>
      <affectAllies>false</affectAllies>
      <stackMode>None</stackMode>
    </li>
  </comps>
</HediffDef>
```

#### 示例 2：叠层光环（通过 Severity 模式逐渐增加）
```xml
<HediffDef ParentName="HediffBase">
  <defName>TSS_StackingAura</defName>
  <hediffClass>HediffWithComps</hediffClass>
  <comps>
    <li Class="TheSecondSeat.Hediffs.HediffCompProperties_ApplyAura">
      <radius>5.0</radius>
      <checkInterval>120</checkInterval>
      <hediffToApply>TSS_PoisonMark</hediffToApply>
      <affectEnemies>true</affectEnemies>
      <stackMode>Severity</stackMode>
      <initialSeverity>0.1</initialSeverity>
      <severityIncrease>0.1</severityIncrease>
      <maxSeverity>1.0</maxSeverity>
    </li>
  </comps>
</HediffDef>
```

#### 示例 3：自定义方法叠层（如猩红绽放）
```xml
<HediffDef ParentName="HediffBase">
  <defName>TSS_CrimsonDomainEffect</defName>
  <hediffClass>HediffWithComps</hediffClass>
  <comps>
    <li Class="TheSecondSeat.Hediffs.HediffCompProperties_ApplyAura">
      <radius>9.9</radius>
      <checkInterval>60</checkInterval>
      <hediffToApply>TSS_CrimsonMark</hediffToApply>
      <affectEnemies>true</affectEnemies>
      <affectAllies>false</affectAllies>
      <affectSelf>false</affectSelf>
      <stackMode>CustomMethod</stackMode>
      <stackMethodName>AddStack</stackMethodName>
      <initialSeverity>0.33</initialSeverity>
    </li>
  </comps>
</HediffDef>
```
**注意**：自定义方法需要在目标 Hediff 的 HediffComp 中定义，方法签名为 `void AddStack(Pawn applier)`。

### 4.2 猩红绽放标记 (Crimson Bloom Mark)
**类名**: `TheSecondSeat.HediffCompProperties_CrimsonBloom`
**适用 Def**: `HediffDef`

#### 功能概述
配合猩红绽放技能使用。标记可叠加层数，达到指定层数后触发"绽放"效果，摧毁目标核心器官。

#### 自定义方法
该组件提供 `AddStack(Pawn applier)` 方法，可被 `HediffComp_ApplyAura` 调用以增加层数。
