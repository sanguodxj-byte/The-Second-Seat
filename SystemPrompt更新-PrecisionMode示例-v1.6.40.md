# System Prompt 更新 - Precision Mode 示例 - v1.6.40

## ?? 更新内容

在 `SystemPromptGenerator.cs` 的 `GenerateOutputFormat()` 方法中，**新增 Example 2** 以演示 **Precision Mode** 参数的使用。

---

## ? 新增示例

### Example 2 - Execute a command (Precision Mode)

```json
{
  "dialogue": "(微笑) 好的，我这就帮你砍掉附近的几棵树。",
  "command": {
    "action": "BatchLogging",
    "target": "All",
    "parameters": {
      "limit": 5,
      "nearFocus": true
    }
  }
}
```

**展示的关键参数**：
- `limit`: 限制操作数量（只砍 5 棵树）
- `nearFocus`: 优先处理焦点附近的对象（鼠标位置/镜头位置）

---

## ?? 示例列表更新

| 示例编号 | 类型 | 说明 |
|---------|------|------|
| Example 1 | 简单对话 | 不带命令的对话 + 表情 |
| **Example 2** | **Precision Mode** | **演示 limit + nearFocus 参数** ? 新增 |
| Example 3 | 带命令 | 基础命令 + 表情 |
| Example 4 | 失望表情 | 悲伤情绪表达 |
| Example 5 | 愤怒表情 | 生气情绪表达 |

---

## ?? 为什么需要这个示例？

### 1. **教育 AI 使用新参数**

在 v1.6.40 中，我们为批量命令添加了 `limit` 和 `nearFocus` 参数：

| 参数 | 类型 | 说明 |
|------|------|------|
| `limit` | int | 限制操作数量（避免过度操作） |
| `nearFocus` | bool | 优先处理焦点附近的对象（提升用户体验） |

**问题**：AI 不知道这些参数的存在，因此无法自动使用。

**解决**：通过示例明确告诉 AI 如何使用这些参数。

---

### 2. **减少过度操作**

#### 旧行为（无 limit）
```
用户: "帮我砍几棵树"
AI: 执行 BatchLogging（target: "All"）
结果: 砍掉地图上所有树（可能几百棵）?
```

#### 新行为（有 limit）
```
用户: "帮我砍几棵树"
AI: 执行 BatchLogging（limit: 5, nearFocus: true）
结果: 砍掉附近 5 棵树 ?
```

---

### 3. **提升 nearFocus 使用率**

`nearFocus` 参数极大提升了用户体验：
- 优先处理**鼠标位置**附近的对象
- 回退到**镜头位置**
- 最后才是**地图中心**

**示例作用**：让 AI 知道可以使用 `nearFocus: true` 来提升精准度。

---

## ?? 技术细节

### 示例插入位置

```csharp
// GenerateOutputFormat() 方法中
sb.AppendLine("**EXAMPLE RESPONSES:**");
sb.AppendLine();
sb.AppendLine("Example 1 - Simple dialogue with expression:");
// ... Example 1 内容 ...
sb.AppendLine();

// ? 新增 Example 2 - Precision Mode
sb.AppendLine("Example 2 - Execute a command (Precision Mode):");
sb.AppendLine("```json");
sb.AppendLine("{");
sb.AppendLine("  \"dialogue\": \"(微笑) 好的，我这就帮你砍掉附近的几棵树。\",");
sb.AppendLine("  \"command\": {");
sb.AppendLine("    \"action\": \"BatchLogging\",");
sb.AppendLine("    \"target\": \"All\",");
sb.AppendLine("    \"parameters\": {");
sb.AppendLine("      \"limit\": 5,");
sb.AppendLine("      \"nearFocus\": true");
sb.AppendLine("    }");
sb.AppendLine("  }");
sb.AppendLine("}");
sb.AppendLine("```");
sb.AppendLine();

// 原 Example 2 变为 Example 3
sb.AppendLine("Example 3 - Execute a command with expression:");
// ... 继续 ...
```

---

### 参数传递流程

```
AI 生成 JSON
    ↓
{
  "command": {
    "action": "BatchLogging",
    "parameters": {
      "limit": 5,
      "nearFocus": true
    }
  }
}
    ↓
NarratorController.ExecuteAdvancedCommand()
    ↓
构造 ParsedCommand {
  parameters: {
    count: 5,           ← limit 映射到 count
    filters: {
      "limit": 5,
      "nearFocus": true
    }
  }
}
    ↓
GameActionExecutor.ConvertParams()
    ↓
Dictionary<string, object> {
  "limit": 5,
  "nearFocus": true
}
    ↓
BatchLoggingCommand.Execute(target, paramsDict)
    ↓
if (paramsDict.TryGetValue("limit", out var limitObj))
    limit = int.Parse(limitObj.ToString());

if (paramsDict.TryGetValue("nearFocus", out var focusObj))
    nearFocus = bool.Parse(focusObj.ToString());
```

---

## ?? 预期效果

### 测试场景 1: "砍几棵树"

**用户输入**: "帮我砍附近的几棵树"

**AI 预期输出**:
```json
{
  "dialogue": "(点头) 好的，我帮你砍掉附近的5棵树。",
  "command": {
    "action": "BatchLogging",
    "target": "All",
    "parameters": {
      "limit": 5,
      "nearFocus": true
    }
  }
}
```

**执行结果**: 
- ? 只砍 5 棵树（而非所有）
- ? 优先砍鼠标位置附近的树

---

### 测试场景 2: "收获附近的作物"

**用户输入**: "收获附近的成熟作物"

**AI 预期输出**:
```json
{
  "dialogue": "(微笑) 我这就帮你收获附近的作物。",
  "command": {
    "action": "BatchHarvest",
    "target": "Mature",
    "parameters": {
      "limit": 10,
      "nearFocus": true
    }
  }
}
```

**执行结果**:
- ? 只收获 10 棵（而非全图）
- ? 优先收获鼠标附近的

---

### 测试场景 3: "挖矿"

**用户输入**: "帮我挖附近的矿石"

**AI 预期输出**:
```json
{
  "dialogue": "(点头) 好的，我帮你标记附近的矿石。",
  "command": {
    "action": "BatchMine",
    "target": "metal",
    "parameters": {
      "limit": 15,
      "nearFocus": true
    }
  }
}
```

---

## ?? 关键改进

| 方面 | 旧行为 | 新行为 |
|------|--------|--------|
| **数量控制** | ? 全选（可能几百个） | ? 限制数量（5-20个） |
| **位置精准** | ? 随机顺序 | ? 焦点优先 |
| **用户体验** | ? 过度操作 | ? 精准控制 |
| **AI 理解** | ? 不知道参数 | ? 示例明确 |

---

## ?? 后续工作

### ?? 建议的增强

1. **添加更多示例**
   - 采矿场景（`BatchMine`）
   - 收获场景（`BatchHarvest`）
   - 修理场景（`PriorityRepair`）

2. **参数说明**
   ```csharp
   sb.AppendLine("**PRECISION MODE PARAMETERS:**");
   sb.AppendLine("- **limit**: Restrict operation count (e.g., limit: 5)");
   sb.AppendLine("- **nearFocus**: Prioritize objects near mouse/camera (e.g., nearFocus: true)");
   ```

3. **AI 自动推断**
   - 当用户说"附近"时，自动添加 `nearFocus: true`
   - 当用户说"几个"时，自动添加 `limit: 5-10`

---

## ?? 部署状态

| 项目 | 状态 |
|------|------|
| **代码修改** | ? 完成 |
| **编译** | ? 成功（0 错误，2 警告） |
| **部署** | ? 已部署到 RimWorld Mods 文件夹 |
| **测试** | ?? 待游戏内测试 |

---

## ?? 总结

### 核心成就

? **新增 Precision Mode 示例**
- AI 现在知道如何使用 `limit` 和 `nearFocus` 参数
- 示例清晰、实用、易理解

? **改进示例编号**
- Example 1: 简单对话
- **Example 2: Precision Mode** ? 新增
- Example 3: 基础命令
- Example 4-5: 情绪表达

? **预期效果**
- AI 会主动使用 `limit` 参数（避免过度操作）
- AI 会使用 `nearFocus` 参数（提升精准度）
- 用户体验显著提升

---

**版本**: v1.6.40  
**状态**: ? 已编译、已部署

?? **System Prompt 更新完成！AI 现在支持 Precision Mode！** ??
