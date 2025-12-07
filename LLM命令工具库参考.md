# RimWorld LLM 集成命令工具库
## The Second Seat - AI 可调用命令完整参考

> 版本: 1.0.0  
> 最后更新: 2025-12-05

---

## ?? 目录

1. [概述](#概述)
2. [命令格式规范](#命令格式规范)
3. [殖民者与单位管理](#1-殖民者与单位管理)
4. [资源与物品管理](#2-资源与物品管理)
5. [建筑与区域管理](#3-建筑与区域管理)
6. [工作与任务管理](#4-工作与任务管理)
7. [事件与叙事控制](#5-事件与叙事控制)
8. [批量操作命令](#6-批量操作命令)
9. [查询与信息获取](#7-查询与信息获取)
10. [DefName 词汇表](#defname-词汇表)
11. [错误处理](#错误处理)

---

## 概述

本文档定义了 The Second Seat 模组中所有可供 LLM 调用的游戏操作命令。

### 调用方式

LLM 通过在回复的 `command` 字段中返回 JSON 格式的命令来执行游戏操作：

```json
{
  "thought": "玩家需要收获作物，我来帮他标记",
  "dialogue": "好的，我已经标记了所有成熟的作物进行收获。",
  "command": {
    "action": "BatchHarvest"
  }
}
```

### 命令执行流程

```
LLM 生成命令 → JSON 解析 → 参数验证 → 游戏主线程执行 → 返回结果
```

---

## 命令格式规范

### 基本结构

```json
{
  "action": "CommandId",
  "target": "可选目标",
  "parameters": {
    "param1": "value1",
    "param2": "value2"
  }
}
```

### 参数类型

| 类型 | 说明 | 示例 |
|------|------|------|
| `string` | 字符串 | `"张三"` |
| `int` | 整数 | `10` |
| `float` | 浮点数 | `0.5` |
| `bool` | 布尔值 | `true` / `false` |
| `IntVec3` | 坐标 | `{"x": 10, "z": 20}` |

---

## 1. 殖民者与单位管理

### 1.1 DraftPawn - 征召殖民者

将殖民者设为征召状态，可进行手动战斗控制。

| 参数 | 类型 | 必需 | 默认值 | 说明 |
|------|------|------|--------|------|
| pawnName | string | 否 | - | 殖民者名字（空=全部） |
| drafted | bool | 否 | true | true=征召, false=解除 |

**示例:**
```json
{ "action": "DraftPawn", "pawnName": "张三", "drafted": true }
```

---

### 1.2 MovePawn - 移动殖民者

命令已征召的殖民者移动到指定位置。

| 参数 | 类型 | 必需 | 说明 |
|------|------|------|------|
| pawnName | string | 是 | 殖民者名字 |
| x | int | 是 | 目标X坐标 |
| z | int | 是 | 目标Z坐标 |

**示例:**
```json
{ "action": "MovePawn", "pawnName": "张三", "x": 50, "z": 50 }
```

?? **注意:** 殖民者必须处于征召状态

---

### 1.3 HealPawn - 治疗殖民者

优先为指定殖民者安排医疗救治。

| 参数 | 类型 | 必需 | 默认值 | 说明 |
|------|------|------|--------|------|
| pawnName | string | 否 | - | 殖民者名字（空=所有伤员） |
| priority | bool | 否 | true | 是否设为紧急优先 |

**示例:**
```json
{ "action": "HealPawn", "pawnName": "李四" }
```

---

### 1.4 SetWorkPriority - 设置工作优先级

调整殖民者特定工作类型的优先级。

| 参数 | 类型 | 必需 | 说明 |
|------|------|------|------|
| pawnName | string | 是 | 殖民者名字 |
| workType | string | 是 | 工作类型 (见下表) |
| priority | int | 是 | 优先级 (1-4, 0=禁用) |

**工作类型 (workType):**
- `Firefighter` - 灭火
- `Doctor` - 医疗
- `Warden` - 看守
- `Handling` - 驯獸
- `Cooking` - 烹饪
- `Hunting` - 狩猎
- `Construction` - 建造
- `Growing` - 种植
- `Mining` - 采矿
- `PlantCutting` - 伐木
- `Smithing` - 锻造
- `Tailoring` - 裁缝
- `Art` - 艺术
- `Crafting` - 制作
- `Hauling` - 搬运
- `Cleaning` - 清洁
- `Research` - 研究

**示例:**
```json
{ "action": "SetWorkPriority", "pawnName": "张三", "workType": "Doctor", "priority": 1 }
```

---

### 1.5 EquipWeapon - 装备武器

命令殖民者装备指定武器。

| 参数 | 类型 | 必需 | 说明 |
|------|------|------|------|
| pawnName | string | 是 | 殖民者名字 |
| weaponDef | string | 否 | 武器DefName（空=自动选择最佳） |

**示例:**
```json
{ "action": "EquipWeapon", "pawnName": "张三", "weaponDef": "Gun_AssaultRifle" }
```

---

## 2. 资源与物品管理

### 2.1 ForbidItem - 禁止/解禁物品

设置物品的禁止状态。

| 参数 | 类型 | 必需 | 默认值 | 说明 |
|------|------|------|--------|------|
| thingDef | string | 是 | - | 物品DefName |
| forbidden | bool | 否 | true | true=禁止, false=解禁 |
| scope | string | 否 | All | All/Selected/Area |

**示例:**
```json
{ "action": "ForbidItem", "thingDef": "Steel", "forbidden": false }
```

---

### 2.2 HaulToStorage - 搬运到储存区

将物品搬运到指定储存区。

| 参数 | 类型 | 必需 | 说明 |
|------|------|------|------|
| thingDef | string | 否 | 物品DefName（空=所有可搬运物品） |
| storageZone | string | 否 | 目标储存区名称 |

**示例:**
```json
{ "action": "HaulToStorage", "thingDef": "MealSimple" }
```

---

## 3. 建筑与区域管理

### 3.1 DesignateBuild - 指定建造

在指定位置规划建造建筑。

| 参数 | 类型 | 必需 | 默认值 | 说明 |
|------|------|------|--------|------|
| buildingDef | string | 是 | - | 建筑DefName |
| x | int | 是 | - | X坐标 |
| z | int | 是 | - | Z坐标 |
| rotation | string | 否 | North | North/South/East/West |
| stuffDef | string | 否 | - | 材料DefName |

**示例:**
```json
{ "action": "DesignateBuild", "buildingDef": "Wall", "x": 10, "z": 10, "stuffDef": "BlocksGranite" }
```

---

### 3.2 Deconstruct - 拆除建筑

指定拆除建筑物。

| 参数 | 类型 | 必需 | 说明 |
|------|------|------|------|
| x | int | 是 | X坐标 |
| z | int | 是 | Z坐标 |

**示例:**
```json
{ "action": "Deconstruct", "x": 10, "z": 10 }
```

---

### 3.3 CreateZone - 创建区域

创建储存区、种植区或其他区域。

| 参数 | 类型 | 必需 | 说明 |
|------|------|------|------|
| zoneType | string | 是 | Stockpile/Growing/Dumping |
| x1 | int | 是 | 起始X坐标 |
| z1 | int | 是 | 起始Z坐标 |
| x2 | int | 是 | 结束X坐标 |
| z2 | int | 是 | 结束Z坐标 |
| zoneName | string | 否 | 区域名称 |

**示例:**
```json
{ "action": "CreateZone", "zoneType": "Stockpile", "x1": 10, "z1": 10, "x2": 20, "z2": 20 }
```

---

## 4. 工作与任务管理

### 4.1 DesignateMine - 指定采矿

指定矿物进行开采。

| 参数 | 类型 | 必需 | 默认值 | 说明 |
|------|------|------|--------|------|
| target | string | 否 | all | all/metal/stone/components |
| x | int | 否 | - | 特定X坐标 |
| z | int | 否 | - | 特定Z坐标 |

**target 说明:**
- `all` - 所有可采矿资源
- `metal` - 钢铁、金、银、铀、玉等金属矿
- `stone` - 石料
- `components` - 组件矿

**示例:**
```json
{ "action": "DesignateMine", "target": "metal" }
```

---

### 4.2 DesignateCut - 指定砍伐

指定植物进行砍伐。

| 参数 | 类型 | 必需 | 默认值 | 说明 |
|------|------|------|--------|------|
| target | string | 否 | trees | trees/blighted/wild/all |

**target 说明:**
- `trees` - 成熟树木 (≥90%)
- `blighted` - 枯萎植物
- `wild` - 野生植物
- `all` - 所有植物

**示例:**
```json
{ "action": "DesignateCut", "target": "trees" }
```

---

### 4.3 DesignateHarvest - 指定收获

指定成熟作物进行收获。

| 参数 | 类型 | 必需 | 说明 |
|------|------|------|------|
| plantDef | string | 否 | 特定植物DefName（空=所有成熟作物） |

**示例:**
```json
{ "action": "DesignateHarvest" }
```

---

### 4.4 AddBill - 添加生产账单

在工作台添加生产任务。

| 参数 | 类型 | 必需 | 默认值 | 说明 |
|------|------|------|--------|------|
| workbenchDef | string | 是 | - | 工作台DefName |
| recipeDef | string | 是 | - | 配方DefName |
| count | int | 否 | -1 | 生产数量 (-1=无限) |
| targetCount | int | 否 | - | 目标库存数量 |

**示例:**
```json
{ "action": "AddBill", "workbenchDef": "ElectricStove", "recipeDef": "CookMealSimple", "count": 10 }
```

---

## 5. 事件与叙事控制

> ?? 以下命令仅在 **对弈者模式** 下可用

### 5.1 TriggerEvent - 触发事件

立即触发游戏事件。

| 参数 | 类型 | 必需 | 说明 |
|------|------|------|------|
| eventType | string | 是 | 事件类型 (见下表) |
| comment | string | 否 | AI评论 |

**事件类型 (eventType):**
| 值 | 中文 | 说明 |
|----|------|------|
| `raid` | 袭击 | 敌人入侵 |
| `trader` | 商队 | 商队到访 |
| `wanderer` | 流浪者 | 流浪者加入 |
| `disease` | 疾病 | 疫病爆发 |
| `resource` | 资源 | 资源空投 |
| `eclipse` | 日蚀 | 日食事件 |
| `toxic` | 毒尘 | 有毒沉降 |

**示例:**
```json
{ "action": "TriggerEvent", "eventType": "raid", "comment": "来吧，展现你的能力" }
```

---

### 5.2 ScheduleEvent - 安排事件

在未来某时刻触发事件。

| 参数 | 类型 | 必需 | 默认值 | 说明 |
|------|------|------|--------|------|
| eventType | string | 是 | - | 事件类型 |
| delayMinutes | int | 否 | 10 | 延迟时间（游戏分钟） |
| comment | string | 否 | - | AI评论 |

**示例:**
```json
{ "action": "ScheduleEvent", "eventType": "raid", "delayMinutes": 30 }
```

---

### 5.3 ChangeWeather - 修改天气

改变当前地图的天气。

| 参数 | 类型 | 必需 | 说明 |
|------|------|------|------|
| weatherDef | string | 是 | 天气DefName |

**天气类型:**
- `Clear` - 晴朗
- `Rain` - 雨天
- `RainyThunderstorm` - 雷暴雨
- `DryThunderstorm` - 干雷暴
- `FoggyRain` - 雾雨
- `Fog` - 雾
- `SnowGentle` - 小雪
- `SnowHard` - 大雪

**示例:**
```json
{ "action": "ChangeWeather", "weatherDef": "Rain" }
```

---

## 6. 批量操作命令

### 6.1 BatchHarvest - 批量收获

指定所有成熟作物进行收获。

```json
{ "action": "BatchHarvest" }
```

---

### 6.2 BatchEquip - 批量装备

为所有无武器殖民者装备最佳武器。

```json
{ "action": "BatchEquip" }
```

---

### 6.3 BatchMine - 批量采矿

指定所有可采矿资源。

| 参数 | 类型 | 必需 | 默认值 | 说明 |
|------|------|------|--------|------|
| target | string | 否 | all | all/metal/stone/components |

```json
{ "action": "BatchMine", "target": "metal" }
```

---

### 6.4 BatchLogging - 批量伐木

指定所有成熟树木进行砍伐（≥90%成熟）。

```json
{ "action": "BatchLogging" }
```

---

### 6.5 BatchCapture - 批量俘获

指定所有倒地敌人进行俘获。

```json
{ "action": "BatchCapture" }
```

---

### 6.6 EmergencyRetreat - 紧急撤退

征召所有殖民者准备撤退。

```json
{ "action": "EmergencyRetreat" }
```

---

### 6.7 PriorityRepair - 优先修复

指定所有受损建筑进行修复。

```json
{ "action": "PriorityRepair" }
```

---

## 7. 查询与信息获取

### 7.1 GetColonists - 获取殖民者列表

获取所有殖民者的信息。

| 参数 | 类型 | 必需 | 默认值 | 说明 |
|------|------|------|--------|------|
| includeDetails | bool | 否 | false | 是否包含详细信息 |

```json
{ "action": "GetColonists", "includeDetails": true }
```

---

### 7.2 GetResources - 获取资源统计

获取殖民地资源库存。

| 参数 | 类型 | 必需 | 说明 |
|------|------|------|------|
| category | string | 否 | all/food/medicine/weapons/materials |

```json
{ "action": "GetResources", "category": "food" }
```

---

### 7.3 GetThreats - 获取威胁评估

获取当前地图上的威胁信息。

```json
{ "action": "GetThreats" }
```

---

### 7.4 GetColonyStatus - 获取殖民地状态

获取殖民地整体状态概览。

```json
{ "action": "GetColonyStatus" }
```

---

## DefName 词汇表

> ?? LLM 必须使用 DefName 而非游戏显示的中文名称

### 资源 (Resources)

| DefName | 中文名 |
|---------|--------|
| `Silver` | 白银 |
| `Steel` | 钢铁 |
| `Gold` | 黄金 |
| `Plasteel` | 玻璃钢 |
| `Uranium` | 铀 |
| `Jade` | 玉 |
| `ComponentIndustrial` | 零部件 |
| `ComponentSpacer` | 高级零部件 |
| `MedicineHerbal` | 草药 |
| `MedicineIndustrial` | 医药 |
| `MedicineUltratech` | 超级医药 |
| `WoodLog` | 木材 |
| `Cloth` | 布料 |
| `Leather_Plain` | 普通皮革 |

### 武器 (Weapons)

| DefName | 中文名 |
|---------|--------|
| `Gun_Autopistol` | 自动手枪 |
| `Gun_MachinePistol` | 冲锋枪 |
| `Gun_AssaultRifle` | 突击步枪 |
| `Gun_BoltActionRifle` | 栓动步枪 |
| `Gun_SniperRifle` | 狙击步枪 |
| `Gun_ChargeLance` | 荷电长矛枪 |
| `Gun_ChargeRifle` | 荷电步枪 |
| `MeleeWeapon_Knife` | 小刀 |
| `MeleeWeapon_LongSword` | 长剑 |
| `MeleeWeapon_Mace` | 钉头锤 |

### 建筑 (Buildings)

| DefName | 中文名 |
|---------|--------|
| `Wall` | 墙壁 |
| `Door` | 门 |
| `Autodoor` | 自动门 |
| `Bed` | 床 |
| `DoubleBed` | 双人床 |
| `HospitalBed` | 医疗床 |
| `TableStoneTwo` | 石桌 |
| `DiningChair` | 餐椅 |
| `SolarGenerator` | 太阳能发电机 |
| `WindTurbine` | 风力发电机 |
| `GeothermalGenerator` | 地热发电机 |
| `Battery` | 蓄电池 |
| `ElectricStove` | 电炉 |
| `FueledStove` | 燃料炉 |
| `Turret_MiniTurret` | 迷你炮塔 |

### 作物 (Plants)

| DefName | 中文名 |
|---------|--------|
| `Plant_Potato` | 土豆 |
| `Plant_Rice` | 水稻 |
| `Plant_Corn` | 玉米 |
| `Plant_Strawberry` | 草莓 |
| `Plant_Healroot` | 草药根 |
| `Plant_Devilstrand` | 恶魔织物 |
| `Plant_Cotton` | 棉花 |
| `Plant_Psychoid` | 迷幻叶 |
| `Plant_Smokeleaf` | 烟叶草 |

### 食物 (Food)

| DefName | 中文名 |
|---------|--------|
| `MealSimple` | 简单食物 |
| `MealFine` | 精制食物 |
| `MealLavish` | 奢华食物 |
| `Pemmican` | 干肉饼 |
| `Kibble` | 饲料 |
| `RawPotatoes` | 生土豆 |
| `RawRice` | 生米 |
| `Meat_Human` | 人肉 |

### 事件 (Incidents)

| DefName | 中文名 |
|---------|--------|
| `RaidEnemy` | 敌人袭击 |
| `RaidFriendly` | 友军支援 |
| `TraderCaravanArrival` | 商队到访 |
| `VisitorGroup` | 访客 |
| `WandererJoin` | 流浪者加入 |
| `RefugeePodCrash` | 逃生舱坠落 |
| `ResourcePodCrash` | 资源舱坠落 |
| `Eclipse` | 日食 |
| `ToxicFallout` | 有毒沉降 |
| `VolcanicWinter` | 火山冬天 |
| `ColdSnap` | 寒潮 |
| `HeatWave` | 热浪 |
| `Disease_Plague` | 瘟疫 |

---

## 错误处理

### 常见错误码

| 错误 | 说明 | 解决方案 |
|------|------|---------|
| `INVALID_COMMAND` | 未知命令 | 检查 action 拼写 |
| `MISSING_PARAM` | 缺少必需参数 | 补充必需参数 |
| `INVALID_PARAM` | 参数值无效 | 检查参数类型和范围 |
| `TARGET_NOT_FOUND` | 目标不存在 | 检查 pawnName 或坐标 |
| `PERMISSION_DENIED` | 权限不足 | 检查难度模式设置 |
| `EXECUTION_FAILED` | 执行失败 | 查看详细错误信息 |

### 错误返回格式

```json
{
  "success": false,
  "error": "TARGET_NOT_FOUND",
  "message": "找不到名为'张三'的殖民者"
}
```

---

## 注意事项

1. **坐标系统**: RimWorld 使用 `(x, z)` 平面坐标，y 轴为高度层级
2. **线程安全**: 所有命令在游戏主线程执行
3. **DefName 区分大小写**: 必须精确匹配 XML 定义
4. **对弈者模式**: 事件控制命令仅在对弈者模式下可用
5. **资源检查**: 建造和生产命令会自动检查资源是否充足

---

*文档版本: 1.0.0 | 最后更新: 2025-12-05*
