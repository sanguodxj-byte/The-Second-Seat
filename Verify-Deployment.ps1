# 快速验证脚本
# 检查所有关键修复是否生效

Write-Host "`n=== The Second Seat 部署验证 ===" -ForegroundColor Cyan -BackgroundColor Black

# 1. 检查 DLL
Write-Host "`n1??  检查 DLL 状态..." -ForegroundColor Yellow
$dll = Get-Item "D:\steam\steamapps\common\RimWorld\Mods\TheSecondSeat\Assemblies\TheSecondSeat.dll"
$expectedTime = Get-Date "2025-12-02 10:31:33"
if ($dll.LastWriteTime -eq $expectedTime) {
    Write-Host "  ? DLL 时间戳正确: $($dll.LastWriteTime)" -ForegroundColor Green
} else {
    Write-Host "  ??  DLL 时间戳不匹配" -ForegroundColor Yellow
    Write-Host "     预期: $expectedTime" -ForegroundColor Gray
    Write-Host "     实际: $($dll.LastWriteTime)" -ForegroundColor Gray
}

# 2. 检查游戏进程
Write-Host "`n2??  检查游戏状态..." -ForegroundColor Yellow
$game = Get-Process -Name "RimWorldWin64" -ErrorAction SilentlyContinue
if ($game) {
    Write-Host "  ? 游戏正在运行" -ForegroundColor Green
    Write-Host "     启动时间: $($game.StartTime)" -ForegroundColor Gray
    
    if ($game.StartTime -gt $dll.LastWriteTime) {
        Write-Host "  ? 游戏在 DLL 更新后启动 - 修复已加载" -ForegroundColor Green
    } else {
        Write-Host "  ? 游戏在 DLL 更新前启动 - 需要重启！" -ForegroundColor Red
    }
} else {
    Write-Host "  ??  游戏未运行" -ForegroundColor Yellow
    Write-Host "     请启动游戏进行测试" -ForegroundColor Gray
}

# 3. 检查日志
Write-Host "`n3??  检查最新日志..." -ForegroundColor Yellow
$logPath = "$env:LOCALAPPDATA\..\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Player.log"

if (Test-Path $logPath) {
    $recentConfig = Get-Content $logPath | Select-String "LLM configured" | Select-Object -Last 1
    
    if ($recentConfig) {
        Write-Host "  ?? 最新 API 配置:" -ForegroundColor Cyan
        Write-Host "     $recentConfig" -ForegroundColor Gray
        
        # 检查是否包含正确的模型名
        if ($recentConfig -match "model=gemini|model=deepseek|model=local-model") {
            Write-Host "  ? 模型名正确传递（非硬编码 gpt-4）" -ForegroundColor Green
        } elseif ($recentConfig -match "model=gpt-4") {
            Write-Host "  ??  仍然使用 gpt-4（可能是配置选择）" -ForegroundColor Yellow
        }
    } else {
        Write-Host "  ??  日志中未找到配置记录" -ForegroundColor Yellow
    }
    
    # 检查错误
    $recentErrors = Get-Content $logPath | Select-String "error|Error|failed|Failed|Exception" | Select-Object -Last 3
    if ($recentErrors) {
        Write-Host "`n  ??  最近的错误/警告:" -ForegroundColor Yellow
        $recentErrors | ForEach-Object { Write-Host "     $_" -ForegroundColor Red }
    } else {
        Write-Host "  ? 无最近错误" -ForegroundColor Green
    }
} else {
    Write-Host "  ? 日志文件不存在" -ForegroundColor Red
}

# 4. 检查关键文件
Write-Host "`n4??  检查关键文件..." -ForegroundColor Yellow
$files = @(
    "D:\steam\steamapps\common\RimWorld\Mods\TheSecondSeat\Assemblies\TheSecondSeat.dll",
    "D:\steam\steamapps\common\RimWorld\Mods\TheSecondSeat\About\About.xml",
    "D:\steam\steamapps\common\RimWorld\Mods\TheSecondSeat\Defs\NarratorPersonaDefs.xml"
)

$allExist = $true
foreach ($file in $files) {
    if (Test-Path $file) {
        Write-Host "  ? $(Split-Path $file -Leaf)" -ForegroundColor Green
    } else {
        Write-Host "  ? $(Split-Path $file -Leaf) - 缺失！" -ForegroundColor Red
        $allExist = $false
    }
}

# 总结
Write-Host "`n=== 验证总结 ===" -ForegroundColor Cyan
Write-Host ""

$allGood = $true

if ($dll.LastWriteTime -ne $expectedTime) { $allGood = $false }
if ($game -and ($game.StartTime -lt $dll.LastWriteTime)) { $allGood = $false }
if (-not $allExist) { $allGood = $false }

if ($allGood) {
    Write-Host "? 所有检查通过！" -ForegroundColor Green -BackgroundColor Black
    Write-Host ""
    Write-Host "现在可以在游戏中测试：" -ForegroundColor Yellow
    Write-Host "1. 查看右上角 AI 按钮" -ForegroundColor White
    Write-Host "2. 测试 ESC 键关闭窗口" -ForegroundColor White
    Write-Host "3. 点击'测试连接'验证 API" -ForegroundColor White
    Write-Host "4. 观察指示灯是否在右上角" -ForegroundColor White
} else {
    Write-Host "??  部分检查未通过" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "建议操作：" -ForegroundColor Yellow
    Write-Host "1. 重启 RimWorld" -ForegroundColor White
    Write-Host "2. 重新运行此脚本验证" -ForegroundColor White
    Write-Host "3. 查看 Player.log 了解详情" -ForegroundColor White
}

Write-Host ""
