# GameActionExecutor 重构完成报告 - v1.6.39

## ? 重构概述

将 `GameActionExecutor.cs` 从**直接实现命令逻辑**重构为**命令路由器**，委托执行逻辑给 `ConcreteCommands.cs` 中的命令类。

---

## ?? 核心改进

### 1. **命令路由器架构**

#### 旧架构（v1.6.38之前）
```csharp
// ? GameActionExecutor 直接实现所有命令逻辑
private static ExecutionResult ExecuteBatchHarvest(AdvancedCommandParams parameters)
{
    // 100+ lines of implementation...
}

private static ExecutionResult ExecuteBatchMine(AdvancedCommandParams parameters)
{
    // 100+ lines of implementation...
}
```

**问题**：
- 单文件过长（1000+ 行）
- 职责不清晰（路由 + 实现混合）
- 难以扩展（修改一个命令影响整个文件）

#### 新架构（v1.6.39）
```csharp
// ? GameActionExecutor 只负责路由
bool success = command.action switch
{
    "BatchHarvest" => new BatchHarvestCommand().Execute(command.parameters.target, paramsDict),
    "BatchMine" => new BatchMineCommand().Execute(command.parameters.target, paramsDict),
    // ...
};
```

**优势**：
- 关注点分离（路由 vs 实现）
- 易于测试（每个命令类独立）
- 易于扩展（新增命令只需添加一行case）

---

### 2. **ConvertParams 辅助方法**

#### 功能
将 `AdvancedCommandParams` 转换为 `Dictionary<string, object>`，合并多个参数源。

#### 实现
```csharp
private static Dictionary<string, object> ConvertParams(AdvancedCommandParams p)
{
    var dict = new Dictionary<string, object>();

    // 1. 添加 target
    if (!string.IsNullOrEmpty(p.target))
        dict["target"] = p.target;

    // 2. 添加 scope
    if (!string.IsNullOrEmpty(p.scope))
        dict["scope"] = p.scope;

    // 3. 合并 filters 中的所有键值对
    if (p.filters != null)
    {
        foreach (var kvp in p.filters)
            dict[kvp.Key] = kvp.Value;
    }

    // 4. ? 映射 p.count → "limit"
    if (p.count != null && p.count > 0)
        dict["limit"] = p.count;

    // 5. ? 自动解析 scope 中的 key=value 对
    if (!string.IsNullOrEmpty(p.scope) && p.scope.Contains("="))
    {
        var parts = p.scope.Split('=');
        if (parts.Length == 2)
        {
            string key = parts[0].Trim().ToLower();
            string value = parts[1].Trim();
            
            if (key != "scope")
                dict[key] = value;
        }
    }

    return dict;
}
```

#### 示例转换

**输入参数**：
```csharp
AdvancedCommandParams
{
    target = "metal",
    scope = "comment=AI评论",
    count = 20,
    filters = { ["nearFocus"] = true }
}
```

**输出字典**：
```csharp
{
    "target" = "metal",
    "scope" = "comment=AI评论",
    "limit" = 20,
    "nearFocus" = true,
    "comment" = "AI评论"
}
```

---

### 3. **支持的新命令类**

| 命令ID | 命令类 | 新参数支持 | 状态 |
|--------|--------|-----------|------|
| `BatchHarvest` | `BatchHarvestCommand` | `limit`, `nearFocus` | ? 已迁移 |
| `BatchEquip` | `BatchEquipCommand` | - | ? 已迁移 |
| `BatchCapture` | `BatchCaptureCommand` | - | ? 已迁移 |
| `BatchMine` | `BatchMineCommand` | `limit`, `nearFocus` | ? 已迁移 |
| `BatchLogging` | `BatchLoggingCommand` | `limit`, `nearFocus` | ? 已迁移 |
| `PriorityRepair` | `PriorityRepairCommand` | - | ? 已迁移 |
| `EmergencyRetreat` | `EmergencyRetreatCommand` | - | ? 已迁移 |
| `DesignatePlantCut` | `DesignatePlantCutCommand` | `limit`, `nearFocus` | ? 已迁移 |
| `TriggerEvent` | `TriggerEventCommand` | `comment` | ? 已迁移 |
| `ScheduleEvent` | `ScheduleEventCommand` | `delay`, `comment` | ? 已迁移 |

---

### 4. **保留旧逻辑（待迁移）**

以下命令暂时保留在 `GameActionExecutor.cs` 中（未来会迁移到 `ConcreteCommands.cs`）：

| 命令ID | 状态 | 原因 |
|--------|------|------|
| `DraftPawn` | ?? 旧逻辑 | 殖民者操作，优先级低 |
| `MovePawn` | ?? 旧逻辑 | 殖民者操作，优先级低 |
| `HealPawn` | ?? 旧逻辑 | 殖民者操作，优先级低 |
| `SetWorkPriority` | ?? 旧逻辑 | 殖民者操作，优先级低 |
| `EquipWeapon` | ?? 旧逻辑 | 殖民者操作，优先级低 |
| `ForbidItems` | ?? 旧逻辑 | 资源管理，优先级低 |
| `AllowItems` | ?? 旧逻辑 | 资源管理，优先级低 |
| `ChangePolicy` | ?? 旧逻辑 | 政策修改，优先级低 |

**迁移计划**：
- ? **已完成**：批量操作命令、对弈者模式事件命令
- ? **下一步**：殖民者操作命令（DraftPawn, MovePawn等）
- ? **未来**：资源管理命令（ForbidItems, AllowItems）

---

## ? 参数支持

### 新增参数

| 参数名 | 类型 | 描述 | 示例 |
|--------|------|------|------|
| `limit` | `int` | 限制操作数量 | `limit=20` |
| `nearFocus` | `bool` | 优先处理焦点附近的目标 | `nearFocus=true` |
| `delay` | `int` | 事件延迟时间（游戏分钟） | `delay=30` |
| `comment` | `string` | AI评论 | `comment=AI评论` |

### 焦点位置优先级

`nearFocus=true` 时，使用以下优先级确定焦点位置：

1. **鼠标位置**（最高优先级）
2. **镜头中心**
3. **地图中心**（最低优先级）

```csharp
// 在 BatchCommandHelpers.GetSmartFocusPoint(map) 中实现
IntVec3 mouseCell = Verse.UI.MouseCell();
if (mouseCell.IsValid && mouseCell.InBounds(map))
{
    return mouseCell; // 优先使用鼠标位置
}

IntVec3 cameraCell = Find.CameraDriver.MapPosition;
if (cameraCell.IsValid && cameraCell.InBounds(map))
{
    return cameraCell; // 回退到镜头位置
}

return map.Center; // 最后使用地图中心
```

---

## ? 技术优势

### 1. **关注点分离**

| 组件 | 职责 | 文件 |
|------|------|------|
| `GameActionExecutor` | 路由命令到对应的命令类 | `GameActionExecutor.cs` |
| `BatchHarvestCommand` | 实现批量收割逻辑 | `ConcreteCommands.cs` |
| `BatchMineCommand` | 实现批量采矿逻辑 | `ConcreteCommands.cs` |

**好处**：
- 职责清晰（路由 vs 实现）
- 易于维护（修改命令逻辑不影响路由器）
- 易于测试（可以单独测试命令类）

---

### 2. **易于扩展**

#### 添加新命令（旧架构）
```csharp
// ? 需要修改 GameActionExecutor.cs 的多个位置
1. 在 switch 中添加 case
2. 实现 100+ 行的命令逻辑
3. 添加辅助方法（如果需要）
4. 更新测试代码
```

#### 添加新命令（新架构）
```csharp
// ? 只需2个步骤

// 步骤1: 在 ConcreteCommands.cs 添加命令类
public class NewCommand : BaseAICommand
{
    public override string ActionName => "NewCommand";
    
    public override bool Execute(string? target = null, object? parameters = null)
    {
        // 实现逻辑...
        return true;
    }
}

// 步骤2: 在 GameActionExecutor.cs 添加一行case
bool success = command.action switch
{
    // ...existing cases...
    "NewCommand" => new NewCommand().Execute(command.parameters.target, paramsDict),
    // ...
};
```

**好处**：
- 新增命令**不影响**现有命令
- 命令逻辑**独立封装**
- **减少代码冲突**（多人协作）

---

### 3. **参数灵活性**

#### 旧架构
```csharp
// ? 硬编码参数
private static ExecutionResult ExecuteBatchHarvest(AdvancedCommandParams parameters)
{
    // 只能用 parameters.count
    // 无法支持 nearFocus
}
```

#### 新架构
```csharp
// ? 灵活解析参数
public override bool Execute(string? target = null, object? parameters = null)
{
    if (parameters is Dictionary<string, object> paramsDict)
    {
        // 可以解析任意参数
        if (paramsDict.TryGetValue("limit", out var limitObj))
            int.TryParse(limitObj?.ToString(), out int limit);
        
        if (paramsDict.TryGetValue("nearFocus", out var focusObj))
            bool.TryParse(focusObj?.ToString(), out bool nearFocus);
    }
}
```

**好处**：
- 支持**任意参数**
- **向后兼容**（不影响旧参数）
- **易于扩展**（添加新参数不需要修改接口）

---

### 4. **向后兼容**

| 类型 | 状态 | 说明 |
|------|------|------|
| **旧命令** | ? 仍然工作 | 保留在 `GameActionExecutor.cs` 中 |
| **旧参数格式** | ? 仍然支持 | `ConvertParams` 自动兼容 |
| **新命令** | ? 新架构 | 使用 `ConcreteCommands.cs` |
| **新参数** | ? 新功能 | `limit`, `nearFocus` 等 |

**迁移策略**：
1. 新命令使用新架构（`ConcreteCommands.cs`）
2. 旧命令保留旧逻辑（`GameActionExecutor.cs`）
3. 逐步迁移旧命令到新架构
4. 不破坏现有功能

---

## ? 测试建议

### 1. **测试批量命令**

#### 测试用例

| 命令 | 参数 | 预期结果 |
|------|------|----------|
| 批量收割附近10个成熟作物 | `limit=10, nearFocus=true` | 收割距离鼠标最近的10个成熟作物 |
| 批量采矿附近20个金属 | `target=metal, limit=20, nearFocus=true` | 采矿距离鼠标最近的20个金属矿 |
| 批量伐木附近15棵树 | `limit=15, nearFocus=true` | 伐木距离鼠标最近的15棵成熟树 |

#### 测试步骤

```markdown
1. 启动 RimWorld
2. 开始游戏（或加载存档）
3. 打开 The Second Seat 对话窗口
4. 输入命令："批量收割附近10个成熟作物"
5. 检查结果：
   - ? 是否收割了10个作物
   - ? 是否优先收割距离鼠标最近的
   - ? 是否显示成功消息
```

---

### 2. **测试参数传递**

#### 测试 `limit` 参数

| 命令 | limit值 | 预期结果 |
|------|---------|----------|
| 批量收割 | 无 | 收割所有成熟作物 |
| 批量收割 | `limit=10` | 只收割10个 |
| 批量收割 | `limit=100` | 收割所有（最多100个） |

#### 测试 `nearFocus` 参数

| 命令 | nearFocus值 | 预期结果 |
|------|-------------|----------|
| 批量采矿 | 无 | 按默认顺序采矿 |
| 批量采矿 | `nearFocus=true` | 优先采矿距离鼠标最近的 |
| 批量采矿 | `nearFocus=false` | 按默认顺序采矿 |

#### 测试对弈者模式事件

| 命令 | 参数 | 预期结果 |
|------|------|----------|
| 触发袭击 | `comment=来袭！` | 立即触发袭击，显示AI评论 |
| 安排袭击 | `delay=30, comment=30分钟后袭击` | 30分钟后触发袭击 |

---

### 3. **测试向后兼容**

#### 测试旧命令

| 命令 | 状态 | 预期结果 |
|------|------|----------|
| 征召殖民者 | ?? 旧逻辑 | ? 仍然工作 |
| 移动殖民者 | ?? 旧逻辑 | ? 仍然工作 |
| 治疗殖民者 | ?? 旧逻辑 | ? 仍然工作 |

#### 测试参数格式

| 参数格式 | 状态 | 预期结果 |
|---------|------|----------|
| `p.count` | ? 旧格式 | 自动映射为 `limit` |
| `p.filters["nearFocus"]` | ? 旧格式 | 正常工作 |
| `limit=10` | ? 新格式 | 正常工作 |

---

## ?? 文件变更

| 文件 | 变更类型 | 行数变化 | 说明 |
|------|---------|---------|------|
| `Source/TheSecondSeat/Execution/GameActionExecutor.cs` | 重构 | ~1000 → ~500 | 移除直接实现，添加路由逻辑 |

---

## ?? 总结

### 核心成就

1. ? **命令路由器架构**：职责清晰，易于扩展
2. ? **ConvertParams 辅助方法**：参数合并，灵活转换
3. ? **10个新命令类支持**：limit、nearFocus 参数
4. ? **向后兼容**：旧命令仍然工作

### 技术优势

| 优势 | 说明 |
|------|------|
| 关注点分离 | 路由 vs 实现 |
| 易于扩展 | 新增命令只需2步 |
| 参数灵活性 | 支持任意参数 |
| 向后兼容 | 不破坏现有功能 |

### 未来计划

1. ? 迁移殖民者操作命令到 `ConcreteCommands.cs`
2. ? 迁移资源管理命令到 `ConcreteCommands.cs`
3. ? 完全移除 `GameActionExecutor.cs` 中的直接实现

---

**部署完成时间**: 2025-01-XX  
**版本**: v1.6.39  
**状态**: ? 已编译、已部署、已推送

?? **GameActionExecutor 重构完成！命令路由器架构已实现！** ??
