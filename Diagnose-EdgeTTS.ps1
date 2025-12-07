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
Write-Host "`n[4] 检查端口 8000..." -ForegroundColor White
$port8000 = Get-NetTCPConnection -LocalPort 8000 -State Listen -ErrorAction SilentlyContinue
if ($port8000) {
    Write-Host "   ? 端口 8000 已被占用（服务器可能正在运行）" -ForegroundColor Green
    Write-Host "   进程: $($port8000.OwningProcess)" -ForegroundColor Cyan
} else {
    Write-Host "   ?? 端口 8000 未被占用（服务器未运行）" -ForegroundColor Yellow
}

# 5. 测试 HTTP 端点
Write-Host "`n[5] 测试 HTTP 端点..." -ForegroundColor White
try {
    $response = Invoke-RestMethod -Uri "http://localhost:8000/test" -Method GET -TimeoutSec 5 -ErrorAction Stop
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
    Invoke-RestMethod -Uri "http://localhost:8000/tts" -Method POST -Body $body -ContentType "application/json" -OutFile $tempFile -TimeoutSec 10

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
