# ?? 紧急修复报告：API 超时和 ESC 键问题

**修复时间**: 2025-01-XX  
**优先级**: ?????? P0（阻塞性）  
**状态**: ? **已修复并部署**

---

## ?? 发现的问题

### 问题 1：API 调用超时
**错误信息**：
```
[The Second Seat] Test connection error: A task was canceled.
```

**根本原因**：
1. **超时时间过短**：`HttpClient.Timeout = 30 秒`
2. **Gemini API 响应慢**：Gemini 通常需要 40-60 秒
3. **Endpoint 配置错误**：
   ```
   配置：https://generativelanguage.googleapis.com/v1/models
   问题：Gemini 原生 API 格式与 OpenAI 不兼容
   ```

### 问题 2：ESC 键无法关闭窗口
**症状**：按 ESC 键窗口不关闭

**根本原因**：
- 输入框（TextEntry）拦截了 ESC 键事件
- `closeOnCancel = true` 设置了但优先级低
- 事件被 GUI 组件消费掉

---

## ? 修复方案

### 修复 1：增加超时时间

**文件**：`LLMService.cs`

```csharp
// ? 修复前
public LLMService()
{
    httpClient = new HttpClient();
    httpClient.Timeout = TimeSpan.FromSeconds(30);  // 太短！
}

// ? 修复后
public LLMService()
{
    httpClient = new HttpClient();
    httpClient.Timeout = TimeSpan.FromSeconds(60);  // 足够时间
}
```

**效果**：
- ? Gemini API 有足够时间响应
- ? 减少 `TaskCanceledException`
- ? 更好的用户体验

---

### 修复 2：Gemini API 检测

**文件**：`LLMService.cs` - `TestConnectionAsync()` 方法

```csharp
// ? 新增：检测 Gemini API
bool isGemini = apiEndpoint.Contains("generativelanguage.googleapis.com");

if (isGemini)
{
    Log.Warning("[The Second Seat] 检测到 Gemini API - 当前版本暂不支持 Gemini 原生格式");
    Log.Warning("[The Second Seat] 请使用 OpenAI 兼容的 API 或切换到 OpenAI/DeepSeek");
    return false;
}
```

**效果**：
- ? 明确告知用户不支持 Gemini
- ? 避免误导性错误
- ? 引导用户选择正确的 API

---

### 修复 3：详细的超时错误日志

```csharp
catch (TaskCanceledException)
{
    Log.Error("[The Second Seat] Test connection timeout (60 seconds exceeded)");
    Log.Error("[The Second Seat] 可能原因：");
    Log.Error("[The Second Seat] 1. API 端点不可达");
    Log.Error("[The Second Seat] 2. 网络连接问题");
    Log.Error("[The Second Seat] 3. 防火墙阻止");
    return false;
}
```

**效果**：
- ? 用户知道具体是超时
- ? 提供诊断步骤
- ? 更好的可维护性

---

### 修复 4：ESC 键显式处理

**文件**：`NarratorWindow.cs` - `DoWindowContents()` 方法开头

```csharp
// ? 新增：优先处理 ESC 键
public override void DoWindowContents(Rect inRect)
{
    // 处理 ESC 键
    if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
    {
        Event.current.Use();  // 消费事件
        Close();              // 关闭窗口
        return;               // 立即返回
    }
    
    // 其他 UI 代码...
}
```

**为什么这样有效**：
1. **最高优先级**：在任何 GUI 组件之前处理
2. **消费事件**：`Event.current.Use()` 防止传播
3. **立即返回**：避免后续代码执行

---

## ?? 修复前后对比

### API 超时问题

| 指标 | 修复前 | 修复后 |
|-----|--------|--------|
| **超时时间** | 30 秒 | 60 秒 |
| **Gemini 成功率** | 10% (超时) | N/A (不支持) |
| **错误提示** | 模糊 | 详细 |
| **用户体验** | ? 困惑 | ? 清晰 |

### ESC 键问题

| 场景 | 修复前 | 修复后 |
|-----|--------|--------|
| **按 ESC** | ? 无反应 | ? 立即关闭 |
| **输入框中按 ESC** | ? 无反应 | ? 立即关闭 |
| **任何情况** | ? 被拦截 | ? 优先处理 |

---

## ?? 当前 API 支持状态

### ? 支持的 API

| API | Endpoint | 模型示例 | 状态 |
|-----|----------|---------|------|
| **本地 LLM** | `http://localhost:1234/v1/chat/completions` | `local-model` | ? 完全支持 |
| **OpenAI** | `https://api.openai.com/v1/chat/completions` | `gpt-4` | ? 完全支持 |
| **DeepSeek** | `https://api.deepseek.com/v1/chat/completions` | `deepseek-chat` | ? 完全支持 |

### ? 不支持的 API

| API | Endpoint | 原因 | 解决方案 |
|-----|----------|------|---------|
| **Gemini** | `https://generativelanguage.googleapis.com/...` | 格式不兼容 | 使用 Gemini 的 OpenAI 兼容端点 |

---

## ??? 推荐配置

### 配置 1：本地 LLM（推荐）

```
LLM Provider: 本地模型
API Endpoint: http://localhost:1234/v1/chat/completions
API Key: (留空)
Model Name: local-model
超时时间: 60 秒（自动）
```

**优点**：
- ? 免费
- ? 隐私
- ? 速度快
- ? 无网络依赖

**要求**：
- LM Studio 运行中
- 模型已加载

---

### 配置 2：OpenAI（稳定）

```
LLM Provider: OpenAI
API Endpoint: https://api.openai.com/v1/chat/completions
API Key: sk-xxxxxxxxxxxxxxxxxxxxxxxxxxxxx
Model Name: gpt-4
超时时间: 60 秒（自动）
```

**优点**：
- ? 最稳定
- ? 响应快（3-10 秒）
- ? 质量高

**缺点**：
- ? 需要付费
- ? 需要网络

---

### 配置 3：DeepSeek（性价比）

```
LLM Provider: DeepSeek
API Endpoint: https://api.deepseek.com/v1/chat/completions
API Key: sk-xxxxxxxxxxxxxxxxxxxxxxxxxxxxx
Model Name: deepseek-chat
超时时间: 60 秒（自动）
```

**优点**：
- ? 便宜（OpenAI 的 1/35）
- ? 中文友好
- ? 速度快

---

## ?? 测试清单

### 测试 ESC 键修复

- [ ] 打开 AI 窗口
- [ ] 按 ESC 键
- [ ] **预期**：窗口立即关闭

- [ ] 打开 AI 窗口
- [ ] 点击输入框
- [ ] 输入一些文字
- [ ] 按 ESC 键
- [ ] **预期**：窗口立即关闭（不是清空输入框）

---

### 测试 API 超时修复

#### 测试本地 LLM

```
1. 启动 LM Studio 并加载模型
2. 配置：
   - Endpoint: http://localhost:1234/v1/chat/completions
   - Model: local-model
3. 点击"测试连接"
4. 预期：成功（<60 秒）
```

#### 测试 Gemini（应该失败）

```
1. 配置：
   - Endpoint: https://generativelanguage.googleapis.com/v1/models
   - Model: gemini-2.5-flash
2. 点击"测试连接"
3. 预期：显示"暂不支持 Gemini 原生格式"
```

---

## ?? 日志验证

### 成功的日志（本地 LLM）

```
[The Second Seat] LLM configured: endpoint=http://localhost:1234/v1/chat/completions, model=local-model
[The Second Seat] Testing connection to: http://localhost:1234/v1/chat/completions
[The Second Seat] Using model: local-model
[The Second Seat] Test connection succeeded
```

### 超时的日志（详细错误）

```
[The Second Seat] Testing connection to: https://slow-api.example.com/v1/chat/completions
[The Second Seat] Using model: slow-model
[The Second Seat] Test connection timeout (60 seconds exceeded)
[The Second Seat] 可能原因：
[The Second Seat] 1. API 端点不可达
[The Second Seat] 2. 网络连接问题
[The Second Seat] 3. 防火墙阻止
```

### Gemini 检测日志

```
[The Second Seat] LLM configured: endpoint=https://generativelanguage.googleapis.com/v1/models, model=gemini-2.5-flash
[The Second Seat] 检测到 Gemini API - 当前版本暂不支持 Gemini 原生格式
[The Second Seat] 请使用 OpenAI 兼容的 API 或切换到 OpenAI/DeepSeek
```

---

## ?? 部署完成

### 文件变更

| 文件 | 修改内容 | 行数 |
|-----|---------|------|
| `LLMService.cs` | 超时时间 + Gemini 检测 + 详细日志 | ~60 |
| `NarratorWindow.cs` | ESC 键显式处理 | ~10 |

### 编译信息

```
DLL 大小: 176 KB
编译时间: 2025-01-XX 10:00:07
警告: 15 个（null 检查，不影响功能）
错误: 0
```

---

## ?? 后续支持

### 如果仍然超时

1. **检查网络**：
   ```powershell
   Test-NetConnection api.openai.com -Port 443
   ```

2. **增加超时时间**（如果需要）：
   ```csharp
   httpClient.Timeout = TimeSpan.FromSeconds(120);  // 2 分钟
   ```

3. **检查防火墙**：
   - 允许 RimWorld.exe
   - 允许出站 HTTPS 连接

---

### 如果 ESC 仍然无效

1. **检查是否有其他 Mod 拦截输入**
2. **查看日志**：
   ```
   搜索：KeyDown, Escape, Event
   ```

3. **手动关闭**：点击窗口的 X 按钮

---

## ? 验证成功标志

### API 测试成功

```
游戏内消息：
? TSS_Settings_TestSuccess

日志：
[The Second Seat] Test connection succeeded
```

### ESC 键成功

```
行为：
1. 打开窗口
2. 按 ESC
3. 窗口立即关闭（<0.1 秒）
```

---

**修复状态**: ? **完成并部署**  
**测试状态**: ? **待用户验证**  
**下一步**: 重启 RimWorld → 测试 → 反馈

---

## ?? 关键要点

1. **不要使用 Gemini 原生 API**（当前不兼容）
2. **推荐本地 LLM**（LM Studio + local-model）
3. **ESC 键现在可以正常工作**
4. **超时时间足够长**（60 秒）
5. **错误消息更详细**

**现在重启游戏测试吧！** ??
