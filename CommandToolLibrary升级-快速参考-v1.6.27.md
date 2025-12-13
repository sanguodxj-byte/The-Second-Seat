# ? CommandToolLibrary 拆分与批量命令升级 - 快速参考

## ?? 文件结构

```
Commands/
├── CommandToolLibrary.cs           # 主文件（核心结构）
├── CommandToolLibrary_Batch.cs     # partial - 批量命令
├── CommandToolLibrary_Work.cs      # partial - 工作命令
└── Implementations/
    └── ConcreteCommands.cs         # 命令实现（升级版）
```

---

## ?? 升级的批量命令（4个）

| 命令 | 命令ID | 新增参数 |
|------|--------|----------|
| 批量收获 | `BatchHarvest` | `limit`, `nearFocus` |
| 批量采矿 | `BatchMine` | `limit`, `nearFocus` |
| 批量伐木 | `BatchLogging` | `limit`, `nearFocus` |
| 批量砍伐植物 | `DesignatePlantCut` | `limit`, `nearFocus` |

---

## ?? 新增参数说明

### `limit` (int, 默认 -1)
- **作用**: 限制指派的目标数量
- **默认值**: -1（无限制，指派所有符合条件的目标）
- **示例值**: 5, 10, 20

### `nearFocus` (bool, 默认 false)
- **作用**: 优先选择靠近焦点的目标
- **焦点优先级**: 鼠标位置 > 镜头位置 > 地图中心
- **默认值**: false（不排序，按默认顺序指派）

---

## ?? AI 调用示例

### 1. 收获最近的10个作物
```json
{
  "action": "BatchHarvest",
  "parameters": {
    "limit": 10,
    "nearFocus": true
  }
}
```

### 2. 采矿最近的5个金属矿
```json
{
  "action": "BatchMine",
  "target": "metal",
  "parameters": {
    "limit": 5,
    "nearFocus": true
  }
}
```

### 3. 砍伐最近的20棵树
```json
{
  "action": "BatchLogging",
  "parameters": {
    "limit": 20,
    "nearFocus": true
  }
}
```

### 4. 砍伐附近所有枯萎植物
```json
{
  "action": "DesignatePlantCut",
  "target": "blighted",
  "parameters": {
    "nearFocus": true
  }
}
```

---

## ?? 焦点逻辑（GetSmartFocusPoint）

```
优先级1: 鼠标位置
    ↓ (无效)
优先级2: 镜头位置
    ↓ (无效)
优先级3: 地图中心
```

---

## ? 视觉反馈

每个指派的目标会显示 **绿色图标**（`FleckDefOf.FeedbackGoto`）

---

## ?? 参数组合效果

| limit | nearFocus | 效果 |
|-------|-----------|------|
| -1 | false | 指派所有目标（默认） |
| -1 | true | 指派所有目标，按距离排序 |
| 10 | false | 指派前10个目标 |
| 10 | true | 指派最近的10个目标 |

---

## ?? 游戏内测试指令

### 测试1: 收获最近5个作物
```
/say 收获最近的5个成熟作物
```
**预期**: AI生成带 `limit=5` 和 `nearFocus=true` 的命令

### 测试2: 采矿附近的金属
```
/say 采矿附近的金属矿
```
**预期**: AI生成带 `nearFocus=true` 的 `BatchMine` 命令

### 测试3: 砍伐最近10棵树
```
/say 砍伐最近的10棵树
```
**预期**: AI生成带 `limit=10` 和 `nearFocus=true` 的 `BatchLogging` 命令

---

## ?? 开发者提示

### 添加新的批量命令参数
```csharp
// 1. 在 CommandToolLibrary_Batch.cs 中注册参数
new ParameterDef { 
    name = "myParam", 
    type = "int", 
    required = false, 
    defaultValue = "0", 
    description = "参数说明" 
}

// 2. 在 ConcreteCommands.cs 中解析
if (parameters is Dictionary<string, object> paramsDict)
{
    if (paramsDict.TryGetValue("myParam", out var paramObj))
        int.TryParse(paramObj?.ToString(), out myParamValue);
}

// 3. 使用参数逻辑
if (myParamValue > 0)
{
    // 应用参数效果
}
```

---

## ?? 部署检查清单

- [x] 编译成功
- [x] DLL部署到游戏目录
- [ ] 游戏内启动测试
- [ ] AI调用测试
- [ ] 视觉反馈验证

---

**版本**: v1.6.27  
**完成时间**: 2025-12-13

?? **快速参考卡结束**
