# HediffComp_GiveAbility 文档

## 概述

`HediffComp_GiveAbility` 是一个通用的技能赋予组件，用于通过 Hediff 系统为任何 Pawn（包括动物）赋予技能。

该组件解决了 RimWorld 中动物类型的 Pawn 默认没有 `Pawn_AbilityTracker` 的问题，会在赋予技能前自动初始化该组件。

## 使用场景

- 为动物赋予技能（如降临实体 Sideria）
- 通过 Hediff 临时赋予/收回技能
- 任何需要在运行时动态赋予技能的情况

## XML 配置

### 基础用法

```xml
<HediffDef>
  <defName>MyCustomHediff</defName>
  <label>我的自定义状态</label>
  <hediffClass>HediffWithComps</hediffClass>
  <comps>
    <li Class="TheSecondSeat.Hediffs.HediffCompProperties_GiveAbility">
      <abilities>
        <li>MyAbility1</li>
        <li>MyAbility2</li>
      </abilities>
    </li>
  </comps>
</HediffDef>
```

### 完整示例

```xml
<HediffDef>
  <defName>DragonGuardPowers</defName>
  <label>龙卫之力</label>
  <description>赋予使用者强大的龙族技能。</description>
  <hediffClass>HediffWithComps</hediffClass>
  <isBad>false</isBad>
  <everCurableByItem>false</everCurableByItem>
  <initialSeverity>1.0</initialSeverity>
  <maxSeverity>1.0</maxSeverity>
  
  <comps>
    <!-- 使用 HediffComp_GiveAbility 赋予技能 -->
    <li Class="TheSecondSeat.Hediffs.HediffCompProperties_GiveAbility">
      <abilities>
        <li>Sideria_CalamityThrow</li>
        <li>Sideria_CrimsonBloom</li>
        <li>Sideria_DragonGate</li>
      </abilities>
    </li>
  </comps>
</HediffDef>
```

## 属性说明

### HediffCompProperties_GiveAbility

| 属性 | 类型 | 说明 |
|------|------|------|
| `abilities` | `List<AbilityDef>` | 要赋予的技能定义列表 |

## 工作原理

1. **Hediff 添加时**：
   - 检查 Pawn 是否有 `abilities` tracker
   - 如果没有，自动创建 `Pawn_AbilityTracker`
   - 遍历配置的技能列表，逐个赋予

2. **Hediff 移除时**：
   - 遍历已赋予的技能
   - 逐个从 Pawn 移除

3. **延迟初始化**：
   - 如果 Pawn 在生成时还未完全初始化
   - 组件会在 `CompPostTick` 中重试赋予技能

## 与 HediffComp_DivineBody 的区别

| 特性 | HediffComp_GiveAbility | HediffComp_DivineBody |
|------|------------------------|----------------------|
| 用途 | 通用技能赋予 | Sideria 专用，包含额外神躯属性 |
| 属性增益 | 无 | 包含移速、格挡等增益 |
| 神性躯体 | 不支持 | 支持 DefModExtension_DivineBody |
| 推荐使用 | 一般技能赋予 | Sideria 降临实体 |

## 代码结构

```
TheSecondSeat.Hediffs/
├── HediffComp_GiveAbility.cs        # 通用技能赋予
├── HediffComp_DivineBody.cs         # Sideria 专用神躯
└── HediffCompProperties_GiveAbility # 属性定义类
```

## 调试

在开发者模式下，组件会输出以下日志：

```
[HediffComp_GiveAbility] Initialized abilities tracker for {PawnName}
[HediffComp_GiveAbility] Granted ability '{AbilityDef}' to {PawnName}
[HediffComp_GiveAbility] Removed ability '{AbilityDef}' from {PawnName}
```

## 注意事项

1. **技能定义必须存在**：确保 `abilities` 列表中的技能 defName 在游戏中有效
2. **Hediff 持久性**：如果 Hediff 被移除，技能也会被收回
3. **存档兼容**：组件实现了 `CompExposeData`，技能状态会正确存档
4. **重复赋予**：组件会检查是否已有该技能，避免重复赋予

## 相关组件

- [PawnGenerator_GeneratePawn_Patch](../Source/TheSecondSeat/Patches/PawnGenerator_GeneratePawn_Patch.cs) - Pawn 生成时的技能/Hediff 赋予
- [DescentEntityRegistry](../Source/TheSecondSeat/Descent/DescentEntityRegistry.cs) - 降临实体注册系统
- [HediffComp_DivineBody](./DivineBody_Documentation.md) - Sideria 神躯系统

## 更新历史

- **2026-01-15**: 初始版本，支持动物技能赋予
