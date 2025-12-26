# LogReaderTool - 快速参考 v1.6.77

## ?? 一句话总结
**AI 叙事者现在可以主动读取 Player.log，自动诊断游戏报错问题。**

---

## ?? 文件清单

| 文件 | 路径 | 状态 |
|------|------|------|
| LogReaderTool.cs | Source/TheSecondSeat/RimAgent/Tools/ | ? 新增 |
| TheSecondSeatCore.cs | Source/TheSecondSeat/ | ? 修改 |
| SystemPromptGenerator.cs | Source/TheSecondSeat/PersonaGeneration/ | ? 修改 |

---

## ?? 功能说明

### LogReaderTool 特性
- ? **自动定位**：无需 AI 猜测日志路径
- ? **只读安全**：无任何副作用，不修改文件
- ? **智能摘要**：读取最后 50 行 + 统计错误/警告数量
- ? **线程安全**：支持文件共享读取，不会锁定日志

### AI 触发关键词
AI 会在用户提到以下关键词时自动调用 `read_log` 工具：
- **中文**：报错、错误、红字、崩溃、卡死、模组冲突、不正常、异常、Bug
- **英文**：Error、Exception、Crash、Bug、Conflict

---

## ?? 使用示例

### 示例 1：红字报错
```
玩家：游戏有红字报错
AI：让我看看日志...（调用 read_log）
AI：发现问题！日志显示纹理文件加载失败...
```

### 示例 2：游戏崩溃
```
玩家：游戏崩溃了
AI：让我查看日志...（调用 read_log）
AI：日志显示 NullReferenceException，这是...
```

### 示例 3：模组冲突
```
玩家：为什么有很多警告？
AI：我来检查一下...（调用 read_log）
AI：日志中有 147 个警告，主要是模组冲突...
```

---

## ?? 技术细节

### 日志文件路径
```csharp
// 自动定位 Player.log
string logPath = Path.Combine(GenFilePaths.ConfigFolderPath, "..", "Logs", "Player.log");
```

### 读取最后 50 行
```csharp
int linesToRead = 50;
int startLine = Math.Max(0, allLines.Length - linesToRead);
string tailContent = string.Join("\n", allLines.Skip(startLine));
```

### 统计错误和警告
```csharp
int errorCount = allLines.Count(line => line.Contains("Exception") || line.Contains("ERROR"));
int warningCount = allLines.Count(line => line.Contains("WARNING"));
```

---

## ?? 工具调用格式

### JSON 格式
```json
{
  "thought": "玩家提到游戏报错，我需要查看日志分析问题",
  "dialogue": "让我看看日志文件，诊断一下问题...",
  "command": {
    "action": "read_log",
    "target": null,
    "parameters": {}
  }
}
```

### 参数说明
- **action**：`"read_log"`（固定值）
- **target**：`null`（不需要目标）
- **parameters**：`{}`（空对象，无需参数）

---

## ? 编译状态
```
? 编译成功
   - 0 个错误
   - 18 个警告（正常）
```

---

## ?? 测试步骤

1. **启动游戏**：加载 RimWorld
2. **打开对话窗口**：点击 AI 叙事者按钮
3. **输入测试消息**：
   ```
   游戏有红字报错
   ```
4. **观察 AI 行为**：
   - AI 应自动调用 `read_log` 工具
   - AI 应分析日志并给出诊断结果
5. **验证日志内容**：
   - AI 应显示最后 50 行日志
   - AI 应统计错误和警告数量

---

## ?? 注意事项

### 工具注册
- ? 工具在游戏启动时自动注册（TheSecondSeatCore.静态构造函数）
- ? 无需手动配置

### System Prompt
- ? 日志诊断指令已自动添加到 System Prompt 末尾
- ? 使用 Recency Bias（后置）确保 AI 优先使用

### 安全性
- ? 只读操作，不修改任何文件
- ? 不需要玩家批准（无风险）
- ? 线程安全，不会导致文件锁定

---

## ?? 相关文档

- **完整报告**：`LogReaderTool-完成报告-v1.6.77.md`
- **SystemPromptGenerator 拆分**：`SystemPromptGenerator-大文件拆分-全部完成报告-v1.6.76.md`

---

**版本**：v1.6.77  
**日期**：2025-12-26  
**状态**：? 完成

---

**?? 快速开始：直接启动游戏测试即可！**
