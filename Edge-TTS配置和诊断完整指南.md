# Edge TTS 配置和诊断完整指南

**版本**: v1.7.3  
**日期**: 2025-12-06  
**状态**: ?? **配置指南和诊断工具**

---

## ?? Edge TTS 当前状态

### ? Edge TTS 已集成到代码中

根据 `Source\TheSecondSeat\TTS\TTSService.cs` 的实现：

```csharp
// Edge TTS 端点配置
string edgeEndpoint = "http://localhost:5000/tts";

// 请求格式
{
    "text": "要转换的文本",
    "voice": "zh-CN-XiaoxiaoNeural",
    "rate": 1.0,
    "volume": 1.0
}
```

---

## ? 当前问题

### Edge TTS **不能直接使用**

**原因**：Edge TTS 需要运行一个**本地 HTTP 服务器**作为中间层

**错误提示**：
```
[TTSService] Edge TTS service not available. Please run edge-tts HTTP service.
```

---

## ?? 解决方案：搭建 Edge TTS 本地服务

### 方案 1：使用 Python Flask 服务器（推荐）

#### 步骤 1：安装依赖

```bash
# 安装 Python（如果没有）
# 下载：https://www.python.org/downloads/

# 安装 edge-tts 和 Flask
pip install edge-tts flask
```

#### 步骤 2：创建服务器脚本

创建文件 `edge-tts-server.py`：

```python
from flask import Flask, request, send_file
import edge_tts
import asyncio
import io

app = Flask(__name__)

@app.route('/tts', methods=['POST'])
def tts():
    try:
        data = request.json
        text = data.get('text', '')
        voice = data.get('voice', 'zh-CN-XiaoxiaoNeural')
        rate = data.get('rate', 1.0)
        volume = data.get('volume', 1.0)
        
        if not text:
            return {'error': 'Text is required'}, 400
        
        # 计算速率字符串
        rate_str = f"+{int((rate - 1) * 100)}%" if rate >= 1.0 else f"{int((rate - 1) * 100)}%"
        
        # 生成音频
        audio_data = asyncio.run(generate_audio(text, voice, rate_str, volume))
        
        # 返回音频数据
        return send_file(
            io.BytesIO(audio_data),
            mimetype='audio/wav',
            as_attachment=True,
            download_name='tts.wav'
        )
    except Exception as e:
        return {'error': str(e)}, 500

async def generate_audio(text, voice, rate, volume):
    """生成 TTS 音频"""
    communicate = edge_tts.Communicate(
        text,
        voice,
        rate=rate,
        volume=f"+{int((volume - 1) * 100)}%" if volume >= 1.0 else f"{int((volume - 1) * 100)}%"
    )
    
    audio_data = b""
    async for chunk in communicate.stream():
        if chunk["type"] == "audio":
            audio_data += chunk["data"]
    
    return audio_data

@app.route('/test', methods=['GET'])
def test():
    return {'status': 'ok', 'message': 'Edge TTS server is running'}

if __name__ == '__main__':
    print("=" * 60)
    print(" Edge TTS HTTP 服务器")
    print("=" * 60)
    print(" 监听地址: http://localhost:5000")
    print(" 测试端点: http://localhost:5000/test")
    print(" TTS 端点: http://localhost:5000/tts (POST)")
    print("=" * 60)
    app.run(host='0.0.0.0', port=5000, debug=False)
```

#### 步骤 3：启动服务器

```bash
python edge-tts-server.py
```

**预期输出**：
```
============================================================
 Edge TTS HTTP 服务器
============================================================
 监听地址: http://localhost:5000
 测试端点: http://localhost:5000/test
 TTS 端点: http://localhost:5000/tts (POST)
============================================================
 * Running on http://0.0.0.0:5000
```

#### 步骤 4：测试服务器

**方法 1：浏览器测试**
```
打开浏览器访问：http://localhost:5000/test
预期响应：{"status": "ok", "message": "Edge TTS server is running"}
```

**方法 2：PowerShell 测试**
```powershell
$body = @{
    text = "你好，这是测试"
    voice = "zh-CN-XiaoxiaoNeural"
    rate = 1.0
    volume = 1.0
} | ConvertTo-Json

Invoke-RestMethod -Uri "http://localhost:5000/tts" -Method POST -Body $body -ContentType "application/json" -OutFile "test.wav"

# 播放音频
Start-Process "test.wav"
```

---

### 方案 2：使用现成的 Edge TTS 工具（简单）

#### 选项 A：edge-tts 命令行工具

```bash
# 安装
pip install edge-tts

# 生成音频
edge-tts --voice zh-CN-XiaoxiaoNeural --text "你好，这是测试" --write-media test.mp3

# 列出所有可用语音
edge-tts --list-voices
```

**注意**：命令行工具需要在游戏中通过 `Process.Start` 调用，性能较差。

#### 选项 B：使用 Docker 容器（推荐给服务器用户）

```bash
# 拉取镜像
docker pull ghcr.io/hassio-addons/edge-tts

# 运行容器
docker run -d -p 5000:5000 ghcr.io/hassio-addons/edge-tts
```

---

## ??? RimWorld 中配置 Edge TTS

### 步骤 1：确保服务器运行

```bash
# 启动 edge-tts-server.py
python edge-tts-server.py

# 验证运行状态
Invoke-RestMethod -Uri "http://localhost:5000/test"
```

### 步骤 2：在游戏中配置

1. 打开 RimWorld
2. **选项** → **模组设置** → **The Second Seat**
3. 展开 **"语音合成（TTS）设置"**
4. 配置：
   - ? 启用语音合成（TTS）
   - ?? **Edge TTS** (免费)
   - 声音：`zh-CN-XiaoxiaoNeural`
   - 语速：`1.00x`
   - 音量：`100%`

### 步骤 3：测试 TTS

1. 点击 **"测试 TTS"** 按钮
2. **预期结果**：
   - ? 生成 WAV 文件
   - ? 自动打开文件资源管理器
   - ? 显示 "TTS 测试成功！音频文件已保存。"

---

## ?? 诊断工具

### PowerShell 诊断脚本

创建文件 `Diagnose-EdgeTTS.ps1`：

```powershell
# Edge TTS 诊断脚本
Write-Host "=" * 60 -ForegroundColor Cyan
Write-Host " Edge TTS 诊断工具" -ForegroundColor Yellow
Write-Host "=" * 60 -ForegroundColor Cyan

# 1. 检查 Python
Write-Host "`n[1] 检查 Python 安装..." -ForegroundColor White
try {
    $pythonVersion = python --version 2>&1
    Write-Host "   ? Python 已安装: $pythonVersion" -ForegroundColor Green
} catch {
    Write-Host "   ? Python 未安装或不在 PATH 中" -ForegroundColor Red
    Write-Host "   下载地址: https://www.python.org/downloads/" -ForegroundColor Yellow
}

# 2. 检查 edge-tts 包
Write-Host "`n[2] 检查 edge-tts 包..." -ForegroundColor White
try {
    $edgeTtsVersion = pip show edge-tts 2>&1 | Select-String "Version"
    if ($edgeTtsVersion) {
        Write-Host "   ? edge-tts 已安装: $edgeTtsVersion" -ForegroundColor Green
    } else {
        Write-Host "   ? edge-tts 未安装" -ForegroundColor Red
        Write-Host "   安装命令: pip install edge-tts flask" -ForegroundColor Yellow
    }
} catch {
    Write-Host "   ? 无法检查 edge-tts（pip 不可用）" -ForegroundColor Red
}

# 3. 检查 Flask 包
Write-Host "`n[3] 检查 Flask 包..." -ForegroundColor White
try {
    $flaskVersion = pip show flask 2>&1 | Select-String "Version"
    if ($flaskVersion) {
        Write-Host "   ? Flask 已安装: $flaskVersion" -ForegroundColor Green
    } else {
        Write-Host "   ? Flask 未安装" -ForegroundColor Red
        Write-Host "   安装命令: pip install flask" -ForegroundColor Yellow
    }
} catch {
    Write-Host "   ? 无法检查 Flask" -ForegroundColor Red
}

# 4. 检查端口占用
Write-Host "`n[4] 检查端口 5000..." -ForegroundColor White
$port5000 = Get-NetTCPConnection -LocalPort 5000 -State Listen -ErrorAction SilentlyContinue
if ($port5000) {
    Write-Host "   ? 端口 5000 已被占用（服务器可能正在运行）" -ForegroundColor Green
    Write-Host "   进程: $($port5000.OwningProcess)" -ForegroundColor Cyan
} else {
    Write-Host "   ?? 端口 5000 未被占用（服务器未运行）" -ForegroundColor Yellow
}

# 5. 测试 HTTP 端点
Write-Host "`n[5] 测试 HTTP 端点..." -ForegroundColor White
try {
    $response = Invoke-RestMethod -Uri "http://localhost:5000/test" -Method GET -TimeoutSec 5 -ErrorAction Stop
    if ($response.status -eq "ok") {
        Write-Host "   ? Edge TTS 服务器正常运行" -ForegroundColor Green
        Write-Host "   响应: $($response.message)" -ForegroundColor Cyan
    }
} catch {
    Write-Host "   ? 无法连接到 Edge TTS 服务器" -ForegroundColor Red
    Write-Host "   错误: $($_.Exception.Message)" -ForegroundColor Yellow
    Write-Host "   请运行: python edge-tts-server.py" -ForegroundColor Yellow
}

# 6. 测试 TTS 生成
Write-Host "`n[6] 测试 TTS 音频生成..." -ForegroundColor White
try {
    $body = @{
        text = "测试音频"
        voice = "zh-CN-XiaoxiaoNeural"
        rate = 1.0
        volume = 1.0
    } | ConvertTo-Json

    $tempFile = "$env:TEMP\edge-tts-test.wav"
    Invoke-RestMethod -Uri "http://localhost:5000/tts" -Method POST -Body $body -ContentType "application/json" -OutFile $tempFile -TimeoutSec 10

    if (Test-Path $tempFile) {
        $size = (Get-Item $tempFile).Length
        Write-Host "   ? TTS 音频生成成功" -ForegroundColor Green
        Write-Host "   文件大小: $size 字节" -ForegroundColor Cyan
        Write-Host "   文件路径: $tempFile" -ForegroundColor Cyan
        
        # 可选：播放音频
        $play = Read-Host "`n   是否播放测试音频？(Y/N)"
        if ($play -eq 'Y' -or $play -eq 'y') {
            Start-Process $tempFile
        }
    } else {
        Write-Host "   ? 音频文件未生成" -ForegroundColor Red
    }
} catch {
    Write-Host "   ? TTS 生成失败" -ForegroundColor Red
    Write-Host "   错误: $($_.Exception.Message)" -ForegroundColor Yellow
}

# 总结
Write-Host "`n" + "=" * 60 -ForegroundColor Cyan
Write-Host " 诊断完成" -ForegroundColor Yellow
Write-Host "=" * 60 -ForegroundColor Cyan
```

运行诊断：
```powershell
.\Diagnose-EdgeTTS.ps1
```

---

## ?? Edge TTS vs Azure TTS 对比

| 特性 | Edge TTS | Azure TTS |
|------|----------|-----------|
| **价格** | ? 完全免费 | ? 付费 ($15/100万字符) |
| **配置难度** | ?? 需要本地服务器 | ? 仅需 API Key |
| **音质** | ? 高质量 | ? 高质量 |
| **延迟** | ? 2-3 秒 | ? 1-2 秒 |
| **语音选择** | ? 300+ 语音 | ? 400+ 语音 |
| **网络依赖** | ?? 本地运行（需初次下载模型） | ? 需要网络连接 |
| **稳定性** | ? 高 | ? 高 |

---

## ?? 推荐配置

### 个人用户（推荐 Edge TTS）
1. 安装 Python 和 edge-tts
2. 启动本地服务器
3. 在游戏中配置 Edge TTS
4. **优势**：完全免费，无需 API Key

### 服务器/商业用户（推荐 Azure TTS）
1. 注册 Azure 账号
2. 获取 API Key
3. 直接在游戏中配置
4. **优势**：无需维护本地服务器，更稳定

---

## ?? 常见问题

### Q1: Edge TTS 端点是否正确？
**A**: 是的，端点 `http://localhost:5000/tts` 是正确的，但需要您先启动服务器。

### Q2: 为什么我无法使用 Edge TTS？
**A**: Edge TTS 不是 RimWorld 可以直接调用的服务，需要：
1. 安装 Python
2. 安装 edge-tts 和 Flask
3. 运行 `edge-tts-server.py`

### Q3: 有没有更简单的方法？
**A**: 可以使用 Azure TTS（付费），无需本地服务器：
1. 注册 Azure 账号：https://azure.microsoft.com/
2. 创建 Speech Services 资源
3. 获取 API Key
4. 在游戏中配置 Azure TTS

### Q4: Edge TTS 服务器需要一直运行吗？
**A**: 是的，只要想使用 TTS 功能，服务器就需要运行。可以：
- 手动启动：每次使用前运行 `python edge-tts-server.py`
- 自动启动：添加到 Windows 启动项或使用任务计划程序

### Q5: 可以更改端口吗？
**A**: 可以，修改两处：
1. `edge-tts-server.py` 中的 `port=5000`
2. `TTSService.cs` 中的 `http://localhost:5000/tts`

---

## ?? 快速开始清单

- [ ] 安装 Python
- [ ] 安装 edge-tts：`pip install edge-tts flask`
- [ ] 下载 `edge-tts-server.py` 脚本
- [ ] 启动服务器：`python edge-tts-server.py`
- [ ] 测试端点：访问 `http://localhost:5000/test`
- [ ] 在 RimWorld 中配置 Edge TTS
- [ ] 点击"测试 TTS"按钮验证

---

## ?? 总结

### Edge TTS 是否可用？
? **可用**，但需要额外配置本地 HTTP 服务器

### 端点是否正确？
? **正确**：`http://localhost:5000/tts`

### 下一步
1. **如果想使用免费的 Edge TTS**：按照本指南搭建本地服务器
2. **如果想要更简单的方案**：使用 Azure TTS（付费，但无需额外配置）

---

**创建时间**: 2025-12-06 12:00  
**文档版本**: v1.7.3  
**适用于**: The Second Seat Mod v1.7.0+
