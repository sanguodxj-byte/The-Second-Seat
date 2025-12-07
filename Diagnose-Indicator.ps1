# 诊断右下角图标指示灯问题

Write-Host "`n=== 诊断右下角图标指示灯 ===" -ForegroundColor Cyan

# 1. 检查当前代码配置
Write-Host "`n1. 当前代码配置：" -ForegroundColor Yellow
$code = Get-Content "C:\Users\Administrator\Desktop\rim mod\The Second Seat\Source\TheSecondSeat\UI\NarratorScreenButton.cs" -Raw

if ($code -match 'IndicatorSize\s*=\s*(\d+)') {
    Write-Host "  指示灯尺寸: $($matches[1])px" -ForegroundColor Cyan
    if ([int]$matches[1] -gt 8) {
        Write-Host "  ??  太大了！应该是 6-8px" -ForegroundColor Yellow
    }
}

if ($code -match 'inRect\.xMax\s*-\s*IndicatorSize\s*-\s*IndicatorOffset') {
    Write-Host "  ? 位置代码：右上角（xMax - size - offset）" -ForegroundColor Green
} else {
    Write-Host "  ? 位置代码有问题" -ForegroundColor Red
}

# 2. 检查 DLL 版本
Write-Host "`n2. DLL 版本检查：" -ForegroundColor Yellow
$dll = Get-Item "D:\steam\steamapps\common\RimWorld\Mods\TheSecondSeat\Assemblies\TheSecondSeat.dll"
Write-Host "  编译时间: $($dll.LastWriteTime)" -ForegroundColor Cyan
Write-Host "  大小: $([math]::Round($dll.Length/1KB, 2)) KB" -ForegroundColor Cyan

# 3. 检查游戏是否运行
Write-Host "`n3. 游戏状态：" -ForegroundColor Yellow
$game = Get-Process -Name "RimWorldWin64" -ErrorAction SilentlyContinue
if ($game) {
    Write-Host "  ??  游戏正在运行" -ForegroundColor Yellow
    Write-Host "  启动时间: $($game.StartTime)" -ForegroundColor Gray
    
    if ($game.StartTime -lt $dll.LastWriteTime) {
        Write-Host "`n  ? 游戏加载的是旧版本 DLL！" -ForegroundColor Red
        Write-Host "  必须重启游戏才能看到修复！" -ForegroundColor Yellow
    } else {
        Write-Host "  ? 游戏已加载新版本" -ForegroundColor Green
    }
} else {
    Write-Host "  游戏未运行" -ForegroundColor Gray
}

# 4. 建议修改
Write-Host "`n4. 建议修改：" -ForegroundColor Yellow
Write-Host @"
  当前配置：
  - IndicatorSize = 12f  ← 太大
  - IndicatorOffset = 4f
  
  建议配置：
  - IndicatorSize = 6f   ← 小圆点
  - IndicatorOffset = 2f
  
  这样会更像小指示灯
"@ -ForegroundColor Cyan

Write-Host "`n5. 快速修复：" -ForegroundColor Yellow
Write-Host "  运行以下命令自动修复：" -ForegroundColor Cyan
Write-Host @"
  cd "C:\Users\Administrator\Desktop\rim mod\The Second Seat"
  (Get-Content "Source\TheSecondSeat\UI\NarratorScreenButton.cs") ``
    -replace 'IndicatorSize = 12f', 'IndicatorSize = 6f' ``
    -replace 'IndicatorOffset = 4f', 'IndicatorOffset = 2f' ``
    | Set-Content "Source\TheSecondSeat\UI\NarratorScreenButton.cs"
  
  dotnet build -c Release --nologo Source\TheSecondSeat\TheSecondSeat.csproj
  
  Copy-Item "Source\TheSecondSeat\bin\Release\net472\TheSecondSeat.dll" ``
    "D:\steam\steamapps\common\RimWorld\Mods\TheSecondSeat\Assemblies\" -Force
"@ -ForegroundColor Gray

Write-Host ""
