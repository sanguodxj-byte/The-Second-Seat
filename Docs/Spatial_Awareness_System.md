# 空间感知系统设计文档

## 概述

空间感知系统 v2.0.0 为叙事者提供了区分物体、建筑和 Pawn 方位的能力，使其能够更自然地描述殖民地状态。

## 核心组件

### 1. 数据结构

#### SpatialInfo (空间位置信息)
```csharp
public class SpatialInfo
{
    public int x { get; set; }                    // X坐标
    public int z { get; set; }                    // Z坐标
    public string direction { get; set; }         // 方向：North, South, East, West, Northeast, Northwest, Southeast, Southwest, Center
    public int distanceFromCenter { get; set; }   // 距离殖民地中心的格子数
    public string distanceLevel { get; set; }     // 距离等级：VeryClose, Close, Medium, Far, VeryFar
    public string zone { get; set; }              // 区域名称
    public bool isInHomeArea { get; set; }        // 是否在家园区内
    public bool isIndoors { get; set; }           // 是否在室内
}
```

#### BuildingInfo (建筑信息)
```csharp
public class BuildingInfo
{
    public string name { get; set; }              // 建筑名称
    public string type { get; set; }              // 类型：Production, Power, Defense, Medical, Research, Recreation, Storage
    public string defName { get; set; }           // defName
    public SpatialInfo location { get; set; }     // 位置信息
    public int size { get; set; }                 // 尺寸
    public bool isOperational { get; set; }       // 是否运行中
    public string? currentWorker { get; set; }    // 当前工作者
}
```

#### ThreatEntityInfo (威胁实体信息)
```csharp
public class ThreatEntityInfo
{
    public string name { get; set; }              // 名称
    public string threatType { get; set; }        // 类型：Mechanoid, Insect, Animal, Pirate, Tribal, Raider
    public string? faction { get; set; }          // 派系
    public SpatialInfo location { get; set; }     // 位置
    public int threatLevel { get; set; }          // 威胁等级 1-5
    public bool isInCombat { get; set; }          // 是否在战斗
    public string? weapon { get; set; }           // 武器
}
```

### 2. 方位计算 (DirectionCalculator)

#### 核心方法

| 方法 | 描述 |
|------|------|
| `GetDirection(from, to)` | 计算从点A到点B的8方向 |
| `GetDistance(from, to)` | 计算两点之间的格子距离 |
| `GetDistanceLevel(distance)` | 将距离转换为等级 |
| `CalculateColonyCenterFromHomeArea(map)` | 基于家园区计算殖民地中心 |
| `GetSpatialInfo(position, center, map)` | 获取完整的空间信息 |
| `GetZoneName(map, pos)` | 获取区域名称 |
| `GroupByDirection<T>(entities, selector, center)` | 按方向分组 |

#### 距离等级划分

| 距离等级 | 格子数 |
|----------|--------|
| VeryClose | < 10 |
| Close | 10-29 |
| Medium | 30-59 |
| Far | 60-99 |
| VeryFar | ≥ 100 |

#### 方向计算逻辑

使用 `atan2` 计算角度，然后将 360° 分为 8 个扇区（每个 45°）：

```
        North (67.5° - 112.5°)
           |
Northwest  |  Northeast
(112.5°-157.5°) (22.5°-67.5°)
           |
  West ----+---- East (±22.5°)
(±157.5°-180°)  |
           |
Southwest  |  Southeast
(-157.5° - -112.5°) (-22.5° - -67.5°)
           |
        South (-67.5° - -112.5°)
```

### 3. AI工具 (SpatialQueryTool)

#### 工具名称
`spatial_query`

#### 查询类型

| 查询类型 | 描述 | 示例 |
|----------|------|------|
| `overview` | 殖民地空间概览 | `{"query_type": "overview"}` |
| `colonists` | 殖民者分布 | `{"query_type": "colonists", "direction": "North"}` |
| `buildings` | 建筑分布 | `{"query_type": "buildings", "direction": "East"}` |
| `threats` | 威胁位置 | `{"query_type": "threats"}` |
| `direction` | 特定方向所有实体 | `{"query_type": "direction", "direction": "Southwest"}` |

#### 返回结果示例

**概览查询**
```json
{
  "colony_center": {"x": 150, "z": 120},
  "total_colonists": 8,
  "total_buildings": 25,
  "active_threats": 0,
  "colonist_distribution": {
    "North": 2,
    "East": 3,
    "Center": 3
  },
  "spatial_summary": "Colony Center: (150, 120)\nColonist Distribution:\n  North: Alice, Bob\n  East: Carol, Dave, Eve"
}
```

**威胁查询**
```json
{
  "threat_count": 5,
  "summary": "WARNING: 5 hostile(s) detected! Nearest: Raider (Pirate) at 45 tiles to the Southwest.",
  "threats": [
    {
      "name": "Raider",
      "type": "Pirate",
      "threat_level": 3,
      "location": {
        "direction": "Southwest",
        "distance": 45
      }
    }
  ]
}
```

### 4. 增强的 GameStateSnapshot

`CaptureSnapshotUnsafe()` 方法现在会自动捕获：

1. **殖民地中心** (`colonyCenter`)
2. **殖民者位置** (每个殖民者的 `location` 字段)
3. **重要建筑** (`buildings` 列表)
4. **威胁实体** (`threatEntities` 列表)
5. **空间摘要** (`spatialSummary`)

## 使用场景

### 叙事者描述场景

**增强前**：
> "你的殖民者正在工作。"

**增强后**：
> "你的殖民者 Alice 正在北方的农田工作，而 Bob 在东南方向的采矿区挖掘钢铁。5 名敌人从西南方向接近，距离殖民地中心约 45 格。"

### 代理殖民地决策

叙事者可以使用空间信息：
- 分配殖民者到特定方向的工作站
- 警告玩家威胁来自哪个方向
- 描述殖民地布局和建筑分布
- 提供战略建议（如"加强东侧防御"）

## 性能考虑

1. **建筑限制**：只捕获最重要的 20 个建筑
2. **殖民者限制**：只捕获前 10 个殖民者
3. **威胁限制**：只捕获前 20 个威胁实体
4. **缓存**：使用现有的快照缓存机制

## 文件清单

| 文件 | 描述 |
|------|------|
| `Monitoring/SpatialInfo.cs` | 空间数据结构定义 |
| `Monitoring/DirectionCalculator.cs` | 方位计算工具类 |
| `Monitoring/GameStateSnapshot.cs` | 增强的快照捕获 |
| `RimAgent/Tools/SpatialQueryTool.cs` | AI工具实现 |

## 版本历史

- **v2.0.0** (2026-01-23): 初始版本，实现完整的空间感知系统
