# ?? API 链接失败诊断指南

## ?? 快速诊断

### 步骤 1：检查基础配置

**打开模组设置**：
```
游戏主菜单 → 选项 → 模组设置 → The Second Seat
```

**必须填写的字段**：
- ? **API Endpoint**: http://localhost:1234/v1/chat/completions
- ? **Model Name**: local-model
- ?? **API Key**: 本地模型可留空，远程API必须填写

---

## ?? 常见问题排查

### 问题 1：本地 LLM 连接失败

**症状**：
```
TSS_Settings_TestFailed
连接超时
```

**检查清单**：

1. **LM Studio 是否运行？**
   ```
   打开 LM Studio
   → Server 标签
   → 查看是否显示 "Server running on port 1234"
   ```

2. **端口是否正确？**
   ```
   默认端口：1234
   如果修改过，确保模组设置中的 Endpoint 与 LM Studio 一致
   ```

3. **模型是否加载？**
   ```
   LM Studio → 顶部工具栏
   → 确保有模型被选中（不是 "No model loaded"）
   ```

4. **防火墙是否阻止？**
   ```
   Windows 防火墙 → 允许的应用
   → 确保 LM Studio 和 RimWorld 都被允许
   ```

**修复步骤**：
```powershell
# 测试本地连接
curl http://localhost:1234/v1/models

# 如果失败，重启 LM Studio
# 然后重新加载模型
```

---

### 问题 2：OpenAI API 连接失败

**症状**：
```
API call failed: 401 Unauthorized
Invalid API Key
```

**检查清单**：

1. **API Key 格式正确？**
   ```
   正确格式：sk-xxxxxxxxxxxxxxxxxxxxxxxxxxxxxx
   长度：51 字符
   开头：sk-
   ```

2. **API Key 是否有效？**
   ```
   登录 https://platform.openai.com/api-keys
   查看 Key 状态：Active
   ```

3. **余额是否充足？**
   ```
   https://platform.openai.com/account/billing/overview
   确保有可用余额
   ```

4. **Endpoint 是否正确？**
   ```
   正确：https://api.openai.com/v1/chat/completions
   错误：http://... (缺少 s)
   错误：/v1/completions (缺少 chat)
   ```

**修复步骤**：
```
1. 生成新的 API Key
2. 复制完整的 Key（包括 sk-）
3. 粘贴到模组设置
4. 点击"应用"按钮
5. 点击"测试连接"
```

---

### 问题 3：DeepSeek API 连接失败

**症状**：
```
Connection timeout
无法访问 API
```

**检查清单**：

1. **Endpoint 是否正确？**
   ```
   正确：https://api.deepseek.com/v1/chat/completions
   ```

2. **API Key 格式？**
   ```
   格式：sk-xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx
   ```

3. **网络是否可达？**
   ```powershell
   # PowerShell 测试
   Test-NetConnection api.deepseek.com -Port 443
   ```

4. **地区限制？**
   ```
   DeepSeek 在某些地区可能需要代理
   ```

---

### 问题 4：多模态分析失败

**症状**：
```
Multimodal analysis failed
Vision API error
```

**检查清单**：

1. **是否启用多模态分析？**
   ```
   模组设置 → 多模态人格分析设置
   → 勾选 "启用多模态分析"
   ```

2. **API Key 是否填写？**
   ```
   multimodalApiKey 必须填写
   不能使用 LLM 的 API Key（它们是分开的）
   ```

3. **模型名称是否正确？**
   ```
   OpenAI: gpt-4-vision-preview
   DeepSeek: deepseek-vl
   Gemini: gemini-pro-vision
   ```

4. **立绘文件是否存在？**
   ```
   检查路径：Textures/UI/Narrators/[人格名].png
   ```

---

## ??? 手动测试 API

### 测试 LM Studio

```powershell
# PowerShell
$body = @{
    model = "local-model"
    messages = @(
        @{
            role = "user"
            content = "Hello"
        }
    )
} | ConvertTo-Json

Invoke-RestMethod -Uri "http://localhost:1234/v1/chat/completions" `
    -Method Post `
    -Body $body `
    -ContentType "application/json"
```

**预期输出**：
```json
{
  "choices": [
    {
      "message": {
        "content": "Hello! How can I help you?"
      }
    }
  ]
}
```

---

### 测试 OpenAI API

```powershell
$headers = @{
    "Authorization" = "Bearer YOUR_API_KEY"
    "Content-Type" = "application/json"
}

$body = @{
    model = "gpt-4"
    messages = @(
        @{ role = "user"; content = "Hello" }
    )
} | ConvertTo-Json

Invoke-RestMethod -Uri "https://api.openai.com/v1/chat/completions" `
    -Method Post `
    -Headers $headers `
    -Body $body
```

---

## ?? 诊断工具

### 自动诊断脚本

创建 `Diagnose-API.ps1`：

```powershell
# API 连接诊断脚本

Write-Host "=== The Second Seat API 诊断工具 ===" -ForegroundColor Cyan
Write-Host ""

# 1. 检查本地 LLM
Write-Host "1. 检查本地 LLM (localhost:1234)..." -ForegroundColor Yellow
try {
    $response = Invoke-WebRequest -Uri "http://localhost:1234/v1/models" -TimeoutSec 5
    if ($response.StatusCode -eq 200) {
        Write-Host "   ? 本地 LLM 运行正常" -ForegroundColor Green
    }
} catch {
    Write-Host "   ? 本地 LLM 未运行或端口错误" -ForegroundColor Red
    Write-Host "   请检查 LM Studio 是否启动" -ForegroundColor Gray
}

Write-Host ""

# 2. 检查网络连接
Write-Host "2. 检查网络连接..." -ForegroundColor Yellow

$endpoints = @(
    @{ Name = "OpenAI"; Url = "https://api.openai.com" },
    @{ Name = "DeepSeek"; Url = "https://api.deepseek.com" },
    @{ Name = "Google (Gemini)"; Url = "https://generativelanguage.googleapis.com" }
)

foreach ($endpoint in $endpoints) {
    try {
        $test = Test-NetConnection -ComputerName ($endpoint.Url -replace "https://", "") -Port 443 -WarningAction SilentlyContinue
        if ($test.TcpTestSucceeded) {
            Write-Host "   ? $($endpoint.Name) 可访问" -ForegroundColor Green
        } else {
            Write-Host "   ? $($endpoint.Name) 不可访问" -ForegroundColor Red
        }
    } catch {
        Write-Host "   ? $($endpoint.Name) 连接测试失败" -ForegroundColor Red
    }
}

Write-Host ""

# 3. 检查配置文件
Write-Host "3. 检查模组配置..." -ForegroundColor Yellow
$configPath = "$env:LOCALAPPDATA\..\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Config\ModSettings_TheSecondSeat.xml"

if (Test-Path $configPath) {
    Write-Host "   ? 配置文件存在" -ForegroundColor Green
    $config = Get-Content $configPath -Raw
    
    # 检查关键配置
    if ($config -match "<apiEndpoint>(.*?)</apiEndpoint>") {
        Write-Host "   API Endpoint: $($matches[1])" -ForegroundColor Gray
    }
    if ($config -match "<modelName>(.*?)</modelName>") {
        Write-Host "   Model Name: $($matches[1])" -ForegroundColor Gray
    }
    if ($config -match "<enableMultimodalAnalysis>(.*?)</enableMultimodalAnalysis>") {
        $enabled = $matches[1]
        if ($enabled -eq "True") {
            Write-Host "   ? 多模态分析已启用" -ForegroundColor Green
        } else {
            Write-Host "   ?? 多模态分析未启用" -ForegroundColor Yellow
        }
    }
} else {
    Write-Host "   ?? 配置文件不存在（首次运行正常）" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "=== 诊断完成 ===" -ForegroundColor Cyan
Write-Host ""
Write-Host "建议：" -ForegroundColor Yellow
Write-Host "1. 如果本地 LLM 失败 → 启动 LM Studio 并加载模型" -ForegroundColor Gray
Write-Host "2. 如果远程 API 失败 → 检查 API Key 和网络" -ForegroundColor Gray
Write-Host "3. 查看游戏日志：RimWorld\Player.log" -ForegroundColor Gray
```

**运行**：
```powershell
cd "C:\Users\Administrator\Desktop\rim mod\The Second Seat"
.\Diagnose-API.ps1
```

---

## ?? 日志分析

### 查找错误日志

```powershell
# 打开日志文件
notepad "D:\steam\steamapps\common\RimWorld\Player.log"

# 搜索关键词
Ctrl+F "The Second Seat"
Ctrl+F "[MultimodalAnalysis]"
Ctrl+F "LLM API error"
Ctrl+F "Connection"
```

### 常见错误信息

| 错误信息 | 原因 | 解决方法 |
|---------|------|---------|
| `Connection refused` | LLM 未运行 | 启动 LM Studio |
| `401 Unauthorized` | API Key 错误 | 检查 Key 格式 |
| `404 Not Found` | Endpoint 错误 | 检查 URL 路径 |
| `Timeout` | 网络慢/防火墙 | 检查网络/防火墙 |
| `Empty response` | 模型未加载 | 加载 LLM 模型 |

---

## ? 成功验证

### 本地 LLM 成功标志

**日志中应该看到**：
```
[The Second Seat] LLM service initialized
[The Second Seat] API Endpoint: http://localhost:1234/v1/chat/completions
TSS_Settings_TestSuccess
```

**LM Studio 中应该看到**：
```
POST /v1/chat/completions
Status: 200 OK
```

---

### OpenAI API 成功标志

**日志中应该看到**：
```
[The Second Seat] LLM service initialized
[The Second Seat] API Endpoint: https://api.openai.com/v1/chat/completions
TSS_Settings_TestSuccess
```

---

## ?? 重置配置

如果所有方法都失败，尝试重置：

```powershell
# 删除配置文件
Remove-Item "$env:LOCALAPPDATA\..\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Config\ModSettings_TheSecondSeat.xml"

# 重启游戏
# 重新配置
```

---

## ?? 获取帮助

如果仍然无法解决：

1. **收集信息**：
   ```
   - RimWorld 版本
   - LLM 类型（本地/OpenAI/DeepSeek）
   - Player.log 最后 50 行
   - 配置截图
   ```

2. **提交 Issue**：
   ```
   GitHub Issues
   标题：[API 连接] 具体错误描述
   内容：包含上述信息
   ```

---

## ?? 最佳实践

### 推荐配置（本地）

```
LLM Provider: 本地模型
API Endpoint: http://localhost:1234/v1/chat/completions
API Key: (留空)
Model Name: local-model
Temperature: 0.7
Max Tokens: 2000
```

### 推荐配置（OpenAI）

```
LLM Provider: OpenAI
API Endpoint: https://api.openai.com/v1/chat/completions
API Key: sk-xxxxxxxxxxxxxxxxxxxxxxxxxxxxx
Model Name: gpt-4
Temperature: 0.7
Max Tokens: 2000
```

---

**版本**: 1.0.0  
**更新日期**: 2025-01-XX  
**作者**: The Second Seat 开发团队
