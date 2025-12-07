# 诊断日志脚本
$logPath = "$env:USERPROFILE\AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Player.log"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "RimWorld 日志诊断工具" -ForegroundColor Yellow
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

if (-not (Test-Path $logPath)) {
    Write-Host "错误: 未找到日志文件" -ForegroundColor Red
    Write-Host "路径: $logPath" -ForegroundColor Gray
    exit 1
}

$logSize = (Get-Item $logPath).Length / 1MB
Write-Host "日志文件大小: $([math]::Round($logSize, 2)) MB" -ForegroundColor Cyan
Write-Host ""

# 1. 查找模组加载信息
Write-Host "1. 模组加载状态:" -ForegroundColor Yellow
Write-Host "----------------------------------------" -ForegroundColor Gray
Get-Content $logPath | Select-String "The Second Seat" | Select-Object -First 5 | ForEach-Object {
    Write-Host $_.Line -ForegroundColor White
}
Write-Host ""

# 2. 查找错误
Write-Host "2. 错误信息 (最近 10 条):" -ForegroundColor Yellow
Write-Host "----------------------------------------" -ForegroundColor Gray
Get-Content $logPath | Select-String -Pattern "error|Error|ERROR|Exception|exception" | 
    Where-Object { $_.Line -match "Second Seat|TSS_|Multimodal" } | 
    Select-Object -Last 10 | ForEach-Object {
    Write-Host $_.Line -ForegroundColor Red
}
Write-Host ""

# 3. 查找 Vision API 相关
Write-Host "3. Vision API 调用记录:" -ForegroundColor Yellow
Write-Host "----------------------------------------" -ForegroundColor Gray
Get-Content $logPath | Select-String -Pattern "Vision|MultimodalAnalysis|Gemini Vision" | 
    Select-Object -Last 15 | ForEach-Object {
    Write-Host $_.Line -ForegroundColor Cyan
}
Write-Host ""

# 4. 查找 JSON 解析错误
Write-Host "4. JSON 解析错误:" -ForegroundColor Yellow
Write-Host "----------------------------------------" -ForegroundColor Gray
Get-Content $logPath | Select-String -Pattern "JSON.*错误|JSON.*失败|Unexpected.*JSON|deserializing" | 
    Select-Object -Last 5 | ForEach-Object {
    Write-Host $_.Line -ForegroundColor Red
}
Write-Host ""

# 5. 最后 20 行完整日志
Write-Host "5. 最后 20 行完整日志:" -ForegroundColor Yellow
Write-Host "----------------------------------------" -ForegroundColor Gray
Get-Content $logPath -Tail 20 | ForEach-Object {
    if ($_ -match "error|Error|exception|Exception") {
        Write-Host $_ -ForegroundColor Red
    } elseif ($_ -match "The Second Seat|MultimodalAnalysis") {
        Write-Host $_ -ForegroundColor Cyan
    } else {
        Write-Host $_ -ForegroundColor Gray
    }
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "诊断完成" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
