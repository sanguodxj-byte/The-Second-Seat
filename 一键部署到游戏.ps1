#!/usr/bin/env pwsh
# TSS 一键部署+验证+启动游戏
# 最简单的部署方式

Write-Host "╔════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║  TSS 一键部署 v2.0.3                                       ║" -ForegroundColor Cyan
Write-Host "║  编译 → 部署 → 验证 → 启动                                ║" -ForegroundColor Cyan
Write-Host "╚════════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
Write-Host ""

# 1. 编译并部署
Write-Host "?? 步骤 1/3: 编译并部署..." -ForegroundColor Yellow
.\Deploy-To-Game-v2.0.3-FINAL.ps1

if ($LASTEXITCODE -ne 0) {
    Write-Host ""
    Write-Host "? 部署失败！" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "? 部署完成！" -ForegroundColor Green
Write-Host ""

# 等待用户确认
Read-Host "按 Enter 继续验证部署..."

# 2. 验证部署
Write-Host ""
Write-Host "? 步骤 2/3: 验证部署..." -ForegroundColor Yellow
.\Verify-Game-Deployment-v2.0.3.ps1

Write-Host ""

# 3. 询问是否启动游戏
Write-Host "?? 步骤 3/3: 启动游戏？" -ForegroundColor Yellow
$choice = Read-Host "是否立即启动 RimWorld？(Y/N)"

if ($choice -eq "Y" -or $choice -eq "y") {
    Write-Host ""
    Write-Host "?? 启动 RimWorld..." -ForegroundColor Green
    
    $rimworldExe = "D:\steam\steamapps\common\RimWorld\RimWorldWin64.exe"
    if (Test-Path $rimworldExe) {
        Start-Process $rimworldExe
        Write-Host "? 游戏已启动！" -ForegroundColor Green
        Write-Host ""
        Write-Host "?? 接下来的操作：" -ForegroundColor Cyan
        Write-Host "   1. 等待游戏加载" -ForegroundColor White
        Write-Host "   2. 点击 '选项' → 'Mod'" -ForegroundColor White
        Write-Host "   3. 启用 'The Second Seat'" -ForegroundColor White
        Write-Host "   4. 重启游戏" -ForegroundColor White
        Write-Host "   5. 在游戏中按 F12 测试功能" -ForegroundColor White
    } else {
        Write-Host "? 未找到 RimWorld 可执行文件" -ForegroundColor Red
        Write-Host "   请手动启动游戏" -ForegroundColor Yellow
    }
} else {
    Write-Host ""
    Write-Host "? 部署完成！请手动启动游戏。" -ForegroundColor Green
}

Write-Host ""
Write-Host "?? 所有步骤完成！" -ForegroundColor Cyan
Write-Host ""
