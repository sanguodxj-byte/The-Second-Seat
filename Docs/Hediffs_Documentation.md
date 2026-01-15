# Hediff 扩展功能文档

本文档介绍了 `The Second Seat` 提供的 Hediff 相关的高级 XML 扩展功能。这些功能通过 `DefModExtension` 或自定义 `HediffComp` 实现，允许子 Mod 开发者通过 XML 配置复杂的 C# 逻辑。

## 1. 神性躯体 (Divine Body)
**类名**: `TheSecondSeat.DefModExtension_DivineBody`
**适用 Def**: `HediffDef`

### 功能概述
神性躯体系统允许通过 XML 配置，使特定的 Pawn 获得对非战斗伤害的免疫能力。该系统设计为通用模块，适用于任何需要免疫环境伤害（如火焰、毒素、爆炸等）但仍受常规武器伤害影响的特殊实体。

### 参数说明
| 参数名 | 类型 | 默认值 | 说明 |
| :--- | :--- | :--- | :--- |
| `allowedDamageCategories` | List\<string\> | `Sharp`, `Blunt` | 允许穿透免疫的伤害类别（通常是武器伤害类型）。 |
| `allowWeaponDamage` | bool | `true` | 如果为 true，任何来自武器（`dinfo.Weapon != null`）的伤害都将穿透免疫。 |
| `allowMedicalDamage` | bool | `true` | 如果为 true，手术和处决伤害将穿透免疫（防止无法进行医疗操作）。 |
| `allowPawnInstigatorDamage` | bool | `true` | 如果为 true，来自 Pawn 的徒手攻击将穿透免疫（前提是伤害类型在 `allowedDamageCategories` 中）。 |

### 示例
```xml
<HediffDef>
  <defName>TSS_Example_DivineBody</defName>
  <label>神性躯体</label>
  <modExtensions>
    <li Class="TheSecondSeat.DefModExtension_DivineBody">
      <allowedDamageCategories>
        <li>Sharp</li>
        <li>Blunt</li>
      </allowedDamageCategories>
      <allowWeaponDamage>true</allowWeaponDamage>
      <allowMedicalDamage>true</allowMedicalDamage>
      <allowPawnInstigatorDamage>true</allowPawnInstigatorDamage>
    </li>
  </modExtensions>
</HediffDef>
```

---

## 2. 神性护盾 (Divine Shield)
**类名**: `TheSecondSeat.DefModExtension_DivineShield`
**适用 Def**: `HediffDef`

### 功能概述
为拥有该 Hediff 的 Pawn 提供一个可堆叠的护盾。护盾可以吸收伤害，直到耗尽。

### 参数说明
| 参数名 | 类型 | 默认值 | 说明 |
| :--- | :--- | :--- | :--- |
| `maxStacks` | int | `1` | 护盾的最大堆叠层数。 |
| `shieldPerStack` | float | `100f` | 每层堆叠提供的护盾值。 |
| `absorbAllDamage` | bool | `false` | 如果为 true，护盾将吸收所有类型的伤害（包括非物理伤害）。 |
| `removeOnDeplete` | bool | `true` | 如果为 true，当护盾值耗尽时，移除该 Hediff。 |

### 示例
```xml
<HediffDef>
  <defName>TSS_Example_Shield</defName>
  <label>神性护盾</label>
  <modExtensions>
    <li Class="TheSecondSeat.DefModExtension_DivineShield">
      <maxStacks>3</maxStacks>
      <shieldPerStack>200</shieldPerStack>
      <absorbAllDamage>true</absorbAllDamage>
      <removeOnDeplete>true</removeOnDeplete>
    </li>
  </modExtensions>
</HediffDef>
```

---

## 3. 猩红印记 (Crimson Mark)
**类名**: `TheSecondSeat.DefModExtension_CrimsonMark`
**适用 Def**: `HediffDef`

### 功能概述
定义一种可堆叠的印记 Hediff。当堆叠达到最大层数时，触发爆炸或其他效果。

### 参数说明
| 参数名 | 类型 | 默认值 | 说明 |
| :--- | :--- | :--- | :--- |
| `maxStacks` | int | `5` | 触发效果所需的最大堆叠层数。 |
| `damagePerStack` | float | `20f` | 每层堆叠提供的伤害加成（或爆炸时的基础伤害）。 |
| `explodeOnMaxStacks` | bool | `true` | 如果为 true，达到最大堆叠时触发爆炸。 |
| `explosionRadius` | float | `3f` | 爆炸半径。 |

### 示例
```xml
<HediffDef>
  <defName>TSS_Example_Mark</defName>
  <modExtensions>
    <li Class="TheSecondSeat.DefModExtension_CrimsonMark">
      <maxStacks>3</maxStacks>
      <damagePerStack>50</damagePerStack>
      <explodeOnMaxStacks>true</explodeOnMaxStacks>
      <explosionRadius>4.9</explosionRadius>
    </li>
  </modExtensions>
</HediffDef>
```

---

## 4. Hediff 组件 (HediffComp)

### 4.1 龙之消散 (Dragon Dissipation)
**类名**: `TheSecondSeat.HediffCompProperties_DragonDissipation`
**适用 Def**: `HediffDef`

#### 功能概述
用于召唤物（如灵龙）。当持续时间结束后，使 Pawn 播放消散特效并消失（而不是死亡）。

#### 参数说明
| 参数名 | 类型 | 默认值 | 说明 |
| :--- | :--- | :--- | :--- |
| `dissipationTicks` | int | `15000` | 消散前的持续时间（Tick）。 |

#### 示例
```xml
<HediffDef>
  <defName>TSS_DragonDissipation</defName>
  <comps>
    <li Class="TheSecondSeat.HediffCompProperties_DragonDissipation">
      <dissipationTicks>15000</dissipationTicks>
    </li>
  </comps>
</HediffDef>
```

### 4.2 猩红绽放标记 (Crimson Bloom Mark)
**类名**: `TheSecondSeat.HediffCompProperties_CrimsonBloom`
**适用 Def**: `HediffDef`

#### 功能概述
配合 `CompAbilityEffect_CrimsonBloom` 使用，用于跟踪标记层数并在达到阈值时触发效果。

#### 参数说明
无特定 XML 参数，逻辑由 `CompAbilityEffect_CrimsonBloom` 控制。

#### 示例
```xml
<HediffDef>
  <defName>TSS_CrimsonMark</defName>
  <comps>
    <li Class="TheSecondSeat.HediffCompProperties_CrimsonBloom" />
  </comps>
</HediffDef>
```

### 4.3 光环效果 (Aura)
**类名**: `TheSecondSeat.Hediffs.HediffCompProperties_Aura`
**适用 Def**: `HediffDef`

#### 功能概述
使拥有该 Hediff 的 Pawn 获得一个光环，周期性地对周围的单位施加指定的 Hediff 效果。支持区分敌我、自身生效、以及叠层或刷新持续时间。

#### 参数说明
| 参数名 | 类型 | 默认值 | 说明 |
| :--- | :--- | :--- | :--- |
| `radius` | float | `9.9` | 光环的作用半径。 |
| `checkInterval` | int | `60` | 扫描周围单位的频率（Tick）。默认 60 Tick（1秒）。 |
| `effectHediff` | HediffDef | `null` | 光环施加的 Hediff Def。 |
| `affectEnemies` | bool | `true` | 是否对敌人（HostileTo）生效。 |
| `affectAllies` | bool | `false` | 是否对盟友（!HostileTo）生效。 |
| `affectSelf` | bool | `false` | 是否对自身生效。 |
| `addsStacks` | bool | `false` | 如果为 true，每次生效增加 Hediff 的 Severity；如果为 false，仅刷新持续时间。 |
| `severityAmount` | float | `1.0` | 每次生效增加的 Severity 值（或初始 Severity）。 |
| `maxSeverity` | float | `MaxValue` | 叠层的最大 Severity 上限（仅当 `addsStacks` 为 true 时有效）。 |
| `activeEffect` | EffecterDef | `null` | 光环激活时在施法者身上持续播放的视觉特效。 |

#### 示例
```xml
<HediffDef>
  <defName>TSS_Example_Aura</defName>
  <label>光环</label>
  <hediffClass>HediffWithComps</hediffClass>
  <comps>
    <li Class="TheSecondSeat.Hediffs.HediffCompProperties_Aura">
      <radius>9.9</radius>
      <checkInterval>60</checkInterval>
      <effectHediff>TargetDebuffDefName</effectHediff>
      
      <!-- 目标配置 -->
      <affectEnemies>true</affectEnemies>
      <affectAllies>false</affectAllies>
      <affectSelf>false</affectSelf>
      
      <!-- 叠层配置 -->
      <addsStacks>true</addsStacks>
      <severityAmount>0.1</severityAmount>
      <maxSeverity>1.0</maxSeverity>
      
      <!-- 可选视觉效果 -->
      <activeEffect>Power_Halo</activeEffect>
    </li>
  </comps>
</HediffDef>
```

### 4.4 时停效果 (Time Stop)
**类名**: `TheSecondSeat.Hediffs.HediffCompProperties_TimeStop`
**适用 Def**: `HediffDef`

#### 功能概述
使拥有该 Hediff 的 Pawn 触发**全局时停**（仅限当前地图）。
在时停期间：
1.  **生物冻结**：除了拥有此 Hediff 的施法者外，当前地图上的所有 Pawn 停止行动。
2.  **投射物冻结**：当前地图上的所有投射物（包括施法者的）悬停在空中。
3.  **火势冻结**：当前地图上的火势停止蔓延。
4.  **多施法者兼容**：如果场上有多个拥有此 Hediff 的 Pawn，他们都可以自由行动。

#### 参数说明
无参数。时停的持续时间由 Hediff 自身的持续时间（如 `CompDisappears`）控制。

#### 示例
```xml
<HediffDef>
  <defName>TSS_TimeStop</defName>
  <label>时停</label>
  <hediffClass>HediffWithComps</hediffClass>
  <comps>
    <!-- 时停触发器 -->
    <li Class="TheSecondSeat.Hediffs.HediffCompProperties_TimeStop" />
    
    <!-- 持续时间控制 (例如 10 秒) -->
    <li Class="HediffCompProperties_Disappears">
      <disappearsAfterTicks>
        <min>600</min>
        <max>600</max>
      </disappearsAfterTicks>
    </li>
  </comps>
</HediffDef>
