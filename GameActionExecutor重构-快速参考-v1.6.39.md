# GameActionExecutor 路由器架构 - 快速参考 v1.6.39

## ?? 核心改进一览

```
旧架构: GameActionExecutor 直接实现命令逻辑（1000+ 行）
新架构: GameActionExecutor 路由到 ConcreteCommands.cs（500 行）
```

---

## ?? ConvertParams 方法

**功能**: 将 `AdvancedCommandParams` 转换为 `Dictionary<string, object>`

**输入**:
```csharp
AdvancedCommandParams
{
    target = "metal",
    scope = "comment=AI评论",
    count = 20,
    filters = { ["nearFocus"] = true }
}
```

**输出**:
```csharp
{
    "target" = "metal",
    "scope" = "comment=AI评论",
    "limit" = 20,           // ? count → limit
    "nearFocus" = true,
    "comment" = "AI评论"    // ? 自动解析
}
```

---

## ?? 支持的命令

### 已迁移到新架构（ConcreteCommands.cs）

| 命令 | 新参数 | 状态 |
|------|--------|------|
| `BatchHarvest` | `limit`, `nearFocus` | ? |
| `BatchMine` | `limit`, `nearFocus` | ? |
| `BatchLogging` | `limit`, `nearFocus` | ? |
| `DesignatePlantCut` | `limit`, `nearFocus` | ? |
| `BatchEquip` | - | ? |
| `BatchCapture` | - | ? |
| `PriorityRepair` | - | ? |
| `EmergencyRetreat` | - | ? |
| `TriggerEvent` | `comment` | ? |
| `ScheduleEvent` | `delay`, `comment` | ? |

### 保留旧逻辑（待迁移）

| 命令 | 状态 |
|------|------|
| `DraftPawn` | ?? 旧逻辑 |
| `MovePawn` | ?? 旧逻辑 |
| `HealPawn` | ?? 旧逻辑 |
| `SetWorkPriority` | ?? 旧逻辑 |
| `EquipWeapon` | ?? 旧逻辑 |
| `ForbidItems` | ?? 旧逻辑 |
| `AllowItems` | ?? 旧逻辑 |
| `ChangePolicy` | ?? 旧逻辑 |

---

## ?? 添加新命令（2步）

### 步骤1: 在 ConcreteCommands.cs 添加命令类

```csharp
public class NewCommand : BaseAICommand
{
    public override string ActionName => "NewCommand";
    
    public override bool Execute(string? target = null, object? parameters = null)
    {
        // 解析参数
        if (parameters is Dictionary<string, object> paramsDict)
        {
            if (paramsDict.TryGetValue("limit", out var limitObj))
                int.TryParse(limitObj?.ToString(), out int limit);
        }
        
        // 实现逻辑
        // ...
        
        return true;
    }
}
```

### 步骤2: 在 GameActionExecutor.cs 添加一行

```csharp
bool success = command.action switch
{
    // ...existing cases...
    "NewCommand" => new NewCommand().Execute(command.parameters.target, paramsDict),
    _ => throw new NotImplementedException($"未知命令: {command.action}")
};
```

---

## ?? 测试命令示例

### 批量命令测试

```
"批量收割附近10个成熟作物"
→ BatchHarvestCommand, limit=10, nearFocus=true

"批量采矿附近20个金属"
→ BatchMineCommand, target=metal, limit=20, nearFocus=true

"批量伐木附近15棵树"
→ BatchLoggingCommand, limit=15, nearFocus=true
```

### 对弈者模式事件测试

```
"触发袭击"
→ TriggerEventCommand, eventType=raid

"30分钟后触发袭击"
→ ScheduleEventCommand, eventType=raid, delay=30
```

---

## ?? 技术优势

| 优势 | 说明 |
|------|------|
| **关注点分离** | 路由器 vs 实现 |
| **易于扩展** | 新增命令只需2步 |
| **参数灵活** | 支持任意参数 |
| **向后兼容** | 旧命令仍工作 |

---

## ?? 参数优先级

### nearFocus 焦点位置

```
1. 鼠标位置（最高优先级）
   ↓ 如果无效
2. 镜头中心
   ↓ 如果无效
3. 地图中心（兜底）
```

### 参数合并顺序

```
1. target → dict["target"]
2. scope → dict["scope"]
3. filters → dict[key] = value
4. count → dict["limit"]
5. scope 解析 → dict[key] = value
```

---

## ?? 迁移路线图

```
? v1.6.39: 批量操作命令 + 对弈者模式事件
? v1.6.40: 殖民者操作命令（DraftPawn, MovePawn等）
? v1.6.41: 资源管理命令（ForbidItems, AllowItems）
? v1.6.42: 完全移除旧逻辑
```

---

**快速参考版本**: v1.6.39  
**更新时间**: 2025-01-XX
