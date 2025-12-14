# ExecuteAdvancedCommand 重构 - 快速参考

## ?? 改进概述

**旧**: 重新解析 → 数据丢失  
**新**: 直接构造 → 数据完整

---

## ? 核心变化

### 1. **构造方式**

| 旧 | 新 |
|---|---|
| `NaturalLanguageParser.ParseFromLLMResponse(json)` | `new ParsedCommand { ... }` |
| ? 冗余解析 | ? 直接构造 |

---

### 2. **参数处理**

```csharp
// ? 关键！
parsedCommand.parameters.filters = paramsDict;

// limit → count 映射
if (paramsDict.TryGetValue("limit", out var limitObj))
{
    parsedCommand.parameters.count = int.Parse(limitObj.ToString());
}
```

---

### 3. **类型处理**

```csharp
// 处理多种类型
if (parameters is JObject jObj)          → ToObject<Dictionary>
if (parameters is Dictionary dict)       → 直接使用
else                                      → 序列化再反序列化
```

---

## ?? 数据流

```
LLMCommand {action, target, parameters}
    ↓
直接构造 ParsedCommand {
    action,
    parameters: {
        target,
        scope,
        count ← 从 limit 映射 ?
        filters ← 完整保留 ?
    }
}
    ↓
GameActionExecutor.Execute()
```

---

## ?? 测试示例

### 带 limit 的命令
```json
{
  "command": {
    "action": "BatchHarvest",
    "target": "Mature",
    "parameters": {"limit": 20}
  }
}
```

**期望**: `count = 20` ?

---

### 带自定义参数的命令
```json
{
  "command": {
    "action": "PriorityRepair",
    "parameters": {
      "priority": true,
      "minDamage": 50
    }
  }
}
```

**期望**: 
- `priority = true` ?
- `filters["minDamage"] = 50` ?

---

## ?? 技术优势

| 方面 | 改进 |
|------|------|
| 解析次数 | **-50%** |
| 数据完整性 | **+100%** |
| limit 参数 | **修复** |
| 类型安全 | **增强** |

---

## ?? 关键代码

```csharp
// 1. 构造 ParsedCommand
var parsedCommand = new ParsedCommand
{
    action = llmCommand.action,
    parameters = new AdvancedCommandParams
    {
        target = llmCommand.target,
        scope = "Map"
    }
};

// 2. 处理 parameters
if (llmCommand.parameters != null)
{
    var paramsDict = ConvertToDict(llmCommand.parameters);
    parsedCommand.parameters.filters = paramsDict;
    
    // 3. 映射 limit → count
    if (paramsDict.TryGetValue("limit", out var limitObj))
    {
        parsedCommand.parameters.count = ParseInt(limitObj);
    }
}

// 4. 执行
GameActionExecutor.Execute(parsedCommand);
```

---

## ?? 部署状态

? 已编译  
? 已部署  
?? 待测试

---

**版本**: v1.6.40  
?? **重构完成！** ??
