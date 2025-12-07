# TTS 快速测试脚本
Write-Host "=" * 60 -ForegroundColor Cyan
Write-Host " TTS 连接测试" -ForegroundColor Yellow
Write-Host "=" * 60 -ForegroundColor Cyan

# 测试端点连接
Write-Host "`n[1] 测试服务器连接..." -ForegroundColor White
try {
    $response = Invoke-RestMethod -Uri "http://127.0.0.1:8000/" -Method GET -TimeoutSec 5
    Write-Host "   ? 服务器响应正常" -ForegroundColor Green
    Write-Host "   响应内容: $($response | ConvertTo-Json -Compress)" -ForegroundColor Cyan
} catch {
    Write-Host "   ? 无法连接到服务器" -ForegroundColor Red
    Write-Host "   错误: $($_.Exception.Message)" -ForegroundColor Yellow
    exit
}

# 测试 TTS 生成
Write-Host "`n[2] 测试 TTS 音频生成..." -ForegroundColor White
try {
    $body = @{
        text = "你好，这是测试"
        voice = "zh-CN-XiaoxiaoNeural"
    } | ConvertTo-Json

    Write-Host "   请求体: $body" -ForegroundColor Cyan

    $tempFile = "$env:TEMP\tts_test_$(Get-Date -Format 'yyyyMMddHHmmss').wav"
    
    Invoke-RestMethod -Uri "http://127.0.0.1:8000/synthesize" `
        -Method POST `
        -Body $body `
        -ContentType "application/json" `
        -OutFile $tempFile `
        -TimeoutSec 30

    if (Test-Path $tempFile) {
        $size = (Get-Item $tempFile).Length
        Write-Host "   ? TTS 音频生成成功" -ForegroundColor Green
        Write-Host "   文件大小: $size 字节" -ForegroundColor Cyan
        Write-Host "   文件路径: $tempFile" -ForegroundColor Cyan
        
        # 询问是否播放
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
    Write-Host "   响应: $($_.Exception.Response)" -ForegroundColor Yellow
}

Write-Host "`n" + "=" * 60 -ForegroundColor Cyan
Write-Host " 测试完成" -ForegroundColor Yellow
Write-Host "=" * 60 -ForegroundColor Cyan
