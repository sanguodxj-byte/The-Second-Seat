# ?? CommandToolLibrary 重构完成总结 - v1.6.27

## ?? 任务目标

将 `CommandToolLibrary.cs` "上帝类"拆分为 partial 类，并为批量命令添加精确控制参数。

---

## ? 完成的工作

### 1. CommandToolLibrary 拆分（Partial Classes）

#### **主文件**: `CommandToolLibrary.cs`
- 保留核心结构和注册逻辑
- 保留所有数据结构（`CommandDefinition`, `ParameterDef`, `CommandResult`）
- 保留公共 API（`Register()`, `GetAllCommands()`, 等）
- 保留1-3、5、7类命令的注册方法

#### **批量命令文件**: `CommandToolLibrary_Batch.cs`
- 移动 `RegisterBatchCommands()` 方法
- 为所有批量命令添加 `limit` 和 `nearFocus` 参数

#### **工作命令文件**: `CommandToolLibrary_Work.cs`
- 移动 `RegisterWorkCommands()` 方法
- 为工作命令添加 `limit` 和 `nearFocus` 参数

### 2. ConcreteCommands.cs 批量命令升级

#### **新增辅助类**: `BatchCommandHelpers`
```csharp
public static class BatchCommandHelpers
{
    public static IntVec3 GetSmartFocusPoint(Map map)
    {
        // 优先级: 鼠标位置 > 镜头位置 > 地图中心
    }
}
```

#### **升级的命令**（4个）
1. `BatchHarvestCommand` - 批量收获
2. `BatchMineCommand` - 批量采矿
3. `BatchLoggingCommand` - 批量伐木
4. `DesignatePlantCutCommand` - 批量砍伐植物

#### **新增参数**
| 参数 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `limit` | int | -1 | 限制数量（-1=全部）|
| `nearFocus` | bool | false | 优先选择靠近焦点的目标 |

#### **新增视觉反馈**
- 每个指派的目标会显示绿色图标（`FleckDefOf.FeedbackGoto`）
- 玩家可以清楚地看到哪些目标被选中

---

## ?? 使用示例

### 命令定义（CommandToolLibrary）

```csharp
// 批量收获（CommandToolLibrary_Batch.cs）
Register(new CommandDefinition
{
    commandId = "BatchHarvest",
    category = "Batch",
    displayName = "批量收获",
    description = "指派所有成熟的作物进行收获",
    parameters = new List<ParameterDef>
    {
        new ParameterDef { name = "limit", type = "int", required = false, defaultValue = "-1", description = "限制数量（-1=全部）" },
        new ParameterDef { name = "nearFocus", type = "bool", required = false, defaultValue = "false", description = "优先选择靠近鼠标/镜头的目标" }
    },
    example = "{ \"action\": \"BatchHarvest\", \"limit\": 10, \"nearFocus\": true }",
    notes = "自动选择最近的10个成熟作物收获"
});
```

### AI 调用示例

```json
// 收获最近的10个作物
{
  "action": "BatchHarvest",
  "parameters": {
    "limit": 10,
    "nearFocus": true
  }
}

// 采矿最近的5个金属矿
{
  "action": "BatchMine",
  "target": "metal",
  "parameters": {
    "limit": 5,
    "nearFocus": true
  }
}

// 砍伐最近的20棵成熟树木
{
  "action": "BatchLogging",
  "parameters": {
    "limit": 20,
    "nearFocus": true
  }
}
```

---

## ?? 技术细节

### GetSmartFocusPoint 焦点逻辑

```csharp
public static IntVec3 GetSmartFocusPoint(Map map)
{
    // 1. 优先使用鼠标位置
    IntVec3 mouseCell = Verse.UI.MouseCell();
    if (mouseCell.IsValid && mouseCell.InBounds(map))
        return mouseCell;

    // 2. 回退到镜头位置
    IntVec3 cameraCell = Find.CameraDriver.MapPosition;
    if (cameraCell.IsValid && cameraCell.InBounds(map))
        return cameraCell;

    // 3. 最后使用地图中心
    return map.Center;
}
```

### 参数解析

```csharp
int limit = -1;
bool nearFocus = false;

if (parameters is Dictionary<string, object> paramsDict)
{
    if (paramsDict.TryGetValue("limit", out var limitObj))
        int.TryParse(limitObj?.ToString(), out limit);
    
    if (paramsDict.TryGetValue("nearFocus", out var focusObj))
        bool.TryParse(focusObj?.ToString(), out nearFocus);
}
```

### 排序与限制

```csharp
// 如果启用 nearFocus，按距离排序
if (nearFocus)
{
    IntVec3 focusPoint = BatchCommandHelpers.GetSmartFocusPoint(map);
    items = items.OrderBy(item => item.Position.DistanceTo(focusPoint)).ToList();
}

// 如果指定 limit，只取前N个
if (limit > 0)
{
    items = items.Take(limit).ToList();
}
```

### 视觉反馈

```csharp
foreach (var item in items)
{
    // 执行指派...
    
    // 显示绿色图标
    FleckMaker.ThrowMetaIcon(item.Position, map, FleckDefOf.FeedbackGoto);
}
```

---

## ?? 文件结构对比

### Before（原始结构）
```
Commands/
├── CommandToolLibrary.cs (1500+ lines, "God Class")
└── Implementations/
    └── ConcreteCommands.cs
```

### After（重构后）
```
Commands/
├── CommandToolLibrary.cs (核心结构, 600 lines)
├── CommandToolLibrary_Batch.cs (批量命令, 150 lines)
├── CommandToolLibrary_Work.cs (工作命令, 100 lines)
└── Implementations/
    └── ConcreteCommands.cs (升级版，支持 limit 和 nearFocus)
```

---

## ?? 游戏内效果

### 场景 1: 收获最近的作物
**AI命令**: "收获最近的10个成熟作物"
```json
{ "action": "BatchHarvest", "parameters": { "limit": 10, "nearFocus": true } }
```
**效果**:
- 只指派镜头附近的10个作物
- 每个作物上显示绿色图标
- 消息提示："已指派10个植物进行收获"

### 场景 2: 采矿最近的金属
**AI命令**: "采矿最近的5个金属矿"
```json
{ "action": "BatchMine", "target": "metal", "parameters": { "limit": 5, "nearFocus": true } }
```
**效果**:
- 只指派距离最近的5个金属矿
- 绿色图标标记每个矿石
- 消息提示："已指派5个可采矿资源进行开采 (metal)"

### 场景 3: 砍伐附近的树木
**AI命令**: "砍伐最近的20棵树"
```json
{ "action": "BatchLogging", "parameters": { "limit": 20, "nearFocus": true } }
```
**效果**:
- 只指派最近的20棵成熟树木
- 每棵树上显示绿色图标
- 消息提示："已指派20棵成熟树木进行伐木"

---

## ? 优势总结

### 代码架构优势
1. **可维护性提升**: 拆分为 partial 类，每个文件职责单一
2. **扩展性增强**: 新增命令时只需修改对应的 partial 文件
3. **代码复用**: `BatchCommandHelpers` 可被所有批量命令共享

### 功能优势
1. **精确控制**: `limit` 参数避免指派过多目标
2. **上下文感知**: `nearFocus` 让AI能执行"附近"的指令
3. **视觉反馈**: 玩家能清楚看到哪些目标被选中
4. **向后兼容**: 所有参数都是可选的，不影响现有AI调用

---

## ?? 下一步建议

### 短期优化
1. 为其他批量命令（如 `BatchCapture`）添加相同参数
2. 添加更多焦点模式（如 `nearColonist`, `nearBase`）
3. 添加优先级参数（按资源价值排序）

### 长期扩展
1. 支持区域指派（`inArea="Kitchen"`）
2. 支持条件过滤（`condition="health < 50%"`）
3. 添加撤销批量指派的命令（`UndoBatchCommand`）

---

## ?? 部署状态

| 项目 | 状态 |
|------|------|
| 编译 | ? 成功 |
| 部署 | ? 完成 |
| 测试 | ? 待游戏内验证 |

**DLL位置**: `D:\steam\steamapps\common\RimWorld\Mods\TheSecondSeat\1.6\Assemblies\TheSecondSeat.dll`

---

## ?? 相关文件

| 文件 | 说明 |
|------|------|
| `Source/TheSecondSeat/Commands/CommandToolLibrary.cs` | 主文件，核心结构 |
| `Source/TheSecondSeat/Commands/CommandToolLibrary_Batch.cs` | 批量命令partial类 |
| `Source/TheSecondSeat/Commands/CommandToolLibrary_Work.cs` | 工作命令partial类 |
| `Source/TheSecondSeat/Commands/Implementations/ConcreteCommands.cs` | 命令实现，升级版 |

---

**完成时间**: 2025-12-13 09:52
**版本**: v1.6.27
**开发者**: GitHub Copilot + 用户协作

?? **重构完成，代码架构更清晰，功能更强大！**
