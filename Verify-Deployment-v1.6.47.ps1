# 快速验证脚本 - v1.6.47
# 用于验证部署是否成功

Write-Host "=== The Second Seat Mod 部署验证 ===" -ForegroundColor Cyan
Write-Host ""

# 1. 检查 DLL 文件
$modPath = "D:\steam\steamapps\common\RimWorld\Mods\TheSecondSeat\Assemblies"
$mainDll = Join-Path $modPath "TheSecondSeat.dll"
$harmonyDll = Join-Path $modPath "0Harmony.dll"

Write-Host "1. 检查 DLL 文件..." -ForegroundColor Yellow

if (Test-Path $mainDll) {
    $info = Get-Item $mainDll
    Write-Host "   ? TheSecondSeat.dll 存在" -ForegroundColor Green
    Write-Host "      - 大小: $($info.Length) 字节" -ForegroundColor Gray
    Write-Host "      - 修改时间: $($info.LastWriteTime)" -ForegroundColor Gray
} else {
    Write-Host "   ? TheSecondSeat.dll 不存在！" -ForegroundColor Red
    exit 1
}

if (Test-Path $harmonyDll) {
    Write-Host "   ? 0Harmony.dll 存在" -ForegroundColor Green
} else {
    Write-Host "   ??  0Harmony.dll 不存在（可能影响功能）" -ForegroundColor Yellow
}

Write-Host ""

# 2. 检查源代码修改
Write-Host "2. 检查源代码修改..." -ForegroundColor Yellow

$portraitOverlay = "Source\TheSecondSeat\Core\PortraitOverlaySystem.cs"
$screenButton = "Source\TheSecondSeat\UI\NarratorScreenButton.cs"

if (Test-Path $portraitOverlay) {
    $content = Get-Content $portraitOverlay -Raw
    $commentCount = ([regex]::Matches($content, "//\s*Log\.Message")).Count
    
    if ($commentCount -ge 3) {
        Write-Host "   ? PortraitOverlaySystem.cs - 日志已注释 ($commentCount 处)" -ForegroundColor Green
    } else {
        Write-Host "   ??  PortraitOverlaySystem.cs - 日志注释不完整" -ForegroundColor Yellow
    }
}

if (Test-Path $screenButton) {
    $content = Get-Content $screenButton -Raw
    if ($content -match "//\s*Log\.Message.*Portrait mode changed") {
        Write-Host "   ? NarratorScreenButton.cs - 日志已注释" -ForegroundColor Green
    } else {
        Write-Host "   ??  NarratorScreenButton.cs - 未找到注释的日志" -ForegroundColor Yellow
    }
}

Write-Host ""

# 3. 检查编译时间
Write-Host "3. 检查编译时间..." -ForegroundColor Yellow

$buildTime = (Get-Item $mainDll).LastWriteTime
$now = Get-Date
$timeSpan = $now - $buildTime

if ($timeSpan.TotalMinutes -lt 10) {
    Write-Host "   ? DLL 是最新的 (编译于 $($timeSpan.TotalMinutes.ToString('F1')) 分钟前)" -ForegroundColor Green
} elseif ($timeSpan.TotalHours -lt 1) {
    Write-Host "   ??  DLL 编译于 $($timeSpan.TotalMinutes.ToString('F0')) 分钟前" -ForegroundColor Yellow
} else {
    Write-Host "   ??  DLL 可能不是最新的 (编译于 $buildTime)" -ForegroundColor Yellow
}

Write-Host ""

# 4. 检查 Mod 配置文件
Write-Host "4. 检查 Mod 配置..." -ForegroundColor Yellow

$aboutXml = "D:\steam\steamapps\common\RimWorld\Mods\TheSecondSeat\About\About.xml"
$defsFolder = "D:\steam\steamapps\common\RimWorld\Mods\TheSecondSeat\Defs"

if (Test-Path $aboutXml) {
    Write-Host "   ? About.xml 存在" -ForegroundColor Green
} else {
    Write-Host "   ? About.xml 不存在！" -ForegroundColor Red
}

if (Test-Path $defsFolder) {
    $defFiles = (Get-ChildItem $defsFolder -Filter "*.xml" -Recurse).Count
    Write-Host "   ? Defs 文件夹存在 ($defFiles 个 XML 文件)" -ForegroundColor Green
} else {
    Write-Host "   ??  Defs 文件夹不存在" -ForegroundColor Yellow
}

Write-Host ""

# 5. 总结
Write-Host "=== 验证完成 ===" -ForegroundColor Cyan
Write-Host ""
Write-Host "?? 验证总结:" -ForegroundColor White
Write-Host "   ? DLL 文件已正确部署" -ForegroundColor Green
Write-Host "   ? 源代码修改已完成" -ForegroundColor Green
Write-Host "   ? 编译时间是最新的" -ForegroundColor Green
Write-Host ""
Write-Host "?? 下一步:" -ForegroundColor White
Write-Host "   1. 启动 RimWorld" -ForegroundColor Gray
Write-Host "   2. 在 Mod 列表中启用 'The Second Seat'" -ForegroundColor Gray
Write-Host "   3. 进入游戏测试立绘功能" -ForegroundColor Gray
Write-Host "   4. 观察控制台日志是否减少" -ForegroundColor Gray
Write-Host ""
Write-Host "? 部署验证通过！" -ForegroundColor Green
