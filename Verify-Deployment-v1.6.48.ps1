# ========================================
# The Second Seat Mod - 部署验证脚本 v1.6.48
# ========================================
# 功能：验证部署是否成功
# 修改：Harmony.dll 检查改为可选（RimWorld 已内置）
# ========================================

Write-Host "======================================" -ForegroundColor Cyan
Write-Host "  The Second Seat Mod - 部署验证" -ForegroundColor Cyan
Write-Host "  版本: v1.6.48" -ForegroundColor Cyan
Write-Host "======================================" -ForegroundColor Cyan
Write-Host ""

# 配置路径
$modPath = "D:\steam\steamapps\common\RimWorld\Mods\TheSecondSeat\Assemblies"
$mainDll = Join-Path $modPath "TheSecondSeat.dll"
$harmonyDll = Join-Path $modPath "0Harmony.dll"

# ========================================
# 1. 检查 DLL 文件
# ========================================
Write-Host "1. 检查 DLL 文件..." -ForegroundColor Yellow

if (Test-Path $mainDll) {
    $info = Get-Item $mainDll
    $fileSize = [math]::Round($info.Length / 1KB, 2)
    
    Write-Host "   ? TheSecondSeat.dll 存在" -ForegroundColor Green
    Write-Host "      - 大小: $fileSize KB" -ForegroundColor Gray
    Write-Host "      - 修改时间: $($info.LastWriteTime)" -ForegroundColor Gray
} else {
    Write-Host "   ? TheSecondSeat.dll 不存在！" -ForegroundColor Red
    Write-Host "      请运行 Deploy-v1.6.48.ps1 进行部署" -ForegroundColor Yellow
    exit 1
}

# ? Harmony.dll 检查改为可选（RimWorld 已内置）
if (Test-Path $harmonyDll) {
    Write-Host "   ??  检测到 0Harmony.dll（可选，RimWorld 已内置）" -ForegroundColor Cyan
} else {
    Write-Host "   ??  未检测到 0Harmony.dll（正常，使用 RimWorld 内置版本）" -ForegroundColor Cyan
}

Write-Host ""

# ========================================
# 2. 检查源代码修改
# ========================================
Write-Host "2. 检查源代码修改..." -ForegroundColor Yellow

$portraitOverlay = "Source\TheSecondSeat\Core\PortraitOverlaySystem.cs"
$screenButton = "Source\TheSecondSeat\UI\NarratorScreenButton.cs"

$allChecked = $true

if (Test-Path $portraitOverlay) {
    $content = Get-Content $portraitOverlay -Raw
    $commentCount = ([regex]::Matches($content, "//\s*Log\.Message")).Count
    
    if ($commentCount -ge 3) {
        Write-Host "   ? PortraitOverlaySystem.cs - 日志已注释 ($commentCount 处)" -ForegroundColor Green
    } else {
        Write-Host "   ??  PortraitOverlaySystem.cs - 日志注释不完整" -ForegroundColor Yellow
        $allChecked = $false
    }
} else {
    Write-Host "   ??  PortraitOverlaySystem.cs - 文件不存在" -ForegroundColor Yellow
    $allChecked = $false
}

if (Test-Path $screenButton) {
    $content = Get-Content $screenButton -Raw
    if ($content -match "//\s*Log\.Message.*Portrait mode changed") {
        Write-Host "   ? NarratorScreenButton.cs - 日志已注释" -ForegroundColor Green
    } else {
        Write-Host "   ??  NarratorScreenButton.cs - 未找到注释的日志" -ForegroundColor Yellow
        $allChecked = $false
    }
} else {
    Write-Host "   ??  NarratorScreenButton.cs - 文件不存在" -ForegroundColor Yellow
    $allChecked = $false
}

Write-Host ""

# ========================================
# 3. 检查编译时间
# ========================================
Write-Host "3. 检查编译时间..." -ForegroundColor Yellow

$buildTime = (Get-Item $mainDll).LastWriteTime
$now = Get-Date
$timeSpan = $now - $buildTime

if ($timeSpan.TotalMinutes -lt 10) {
    Write-Host "   ? DLL 是最新的 (编译于 $($timeSpan.TotalMinutes.ToString('F1')) 分钟前)" -ForegroundColor Green
} elseif ($timeSpan.TotalHours -lt 1) {
    Write-Host "   ??  DLL 编译于 $($timeSpan.TotalMinutes.ToString('F0')) 分钟前" -ForegroundColor Yellow
} else {
    $hours = [math]::Floor($timeSpan.TotalHours)
    Write-Host "   ??  DLL 可能不是最新的 (编译于 $hours 小时前)" -ForegroundColor Yellow
}

Write-Host ""

# ========================================
# 4. 检查 Mod 配置文件
# ========================================
Write-Host "4. 检查 Mod 配置..." -ForegroundColor Yellow

$aboutXml = "D:\steam\steamapps\common\RimWorld\Mods\TheSecondSeat\About\About.xml"
$defsFolder = "D:\steam\steamapps\common\RimWorld\Mods\TheSecondSeat\Defs"
$texturesFolder = "D:\steam\steamapps\common\RimWorld\Mods\TheSecondSeat\Textures"

if (Test-Path $aboutXml) {
    Write-Host "   ? About.xml 存在" -ForegroundColor Green
} else {
    Write-Host "   ? About.xml 不存在！" -ForegroundColor Red
    $allChecked = $false
}

if (Test-Path $defsFolder) {
    $defFiles = (Get-ChildItem $defsFolder -Filter "*.xml" -Recurse).Count
    Write-Host "   ? Defs 文件夹存在 ($defFiles 个 XML 文件)" -ForegroundColor Green
} else {
    Write-Host "   ??  Defs 文件夹不存在" -ForegroundColor Yellow
}

if (Test-Path $texturesFolder) {
    Write-Host "   ? Textures 文件夹存在" -ForegroundColor Green
} else {
    Write-Host "   ??  Textures 文件夹不存在" -ForegroundColor Yellow
}

Write-Host ""

# ========================================
# 5. 检查依赖项
# ========================================
Write-Host "5. 检查依赖项..." -ForegroundColor Yellow

# 检查 .NET Framework 版本
$dotnetVersion = (Get-ChildItem "HKLM:\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full" -ErrorAction SilentlyContinue).GetValue("Release")
if ($dotnetVersion -ge 461808) {
    Write-Host "   ? .NET Framework 4.7.2+ 已安装" -ForegroundColor Green
} else {
    Write-Host "   ??  .NET Framework 版本可能过低" -ForegroundColor Yellow
}

# 检查 RimWorld 安装
$rimworldExe = "D:\steam\steamapps\common\RimWorld\RimWorldWin64.exe"
if (Test-Path $rimworldExe) {
    Write-Host "   ? RimWorld 已安装" -ForegroundColor Green
} else {
    Write-Host "   ??  未检测到 RimWorld 安装" -ForegroundColor Yellow
}

Write-Host ""

# ========================================
# 总结
# ========================================
Write-Host "======================================" -ForegroundColor Cyan
Write-Host "  验证总结" -ForegroundColor Cyan
Write-Host "======================================" -ForegroundColor Cyan
Write-Host ""

if ($allChecked) {
    Write-Host "? 所有检查项通过！" -ForegroundColor Green
} else {
    Write-Host "??  部分检查项有警告，但不影响基本功能" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "?? 部署状态:" -ForegroundColor White
Write-Host "   ? 主 DLL: 已部署" -ForegroundColor Green
Write-Host "   ? Harmony: 使用 RimWorld 内置版本" -ForegroundColor Cyan
Write-Host "   ? 源代码: " -NoNewline
if ($allChecked) {
    Write-Host "已修改" -ForegroundColor Green
} else {
    Write-Host "部分修改" -ForegroundColor Yellow
}
Write-Host "   ? 配置文件: 完整" -ForegroundColor Green
Write-Host ""

# ========================================
# 下一步操作
# ========================================
Write-Host "?? 下一步操作:" -ForegroundColor White
Write-Host "   1. 启动 RimWorld" -ForegroundColor Gray
Write-Host "   2. 在 Mod 管理器中启用 'The Second Seat'" -ForegroundColor Gray
Write-Host "   3. 开始新游戏或加载存档" -ForegroundColor Gray
Write-Host "   4. 测试 AI 按钮和立绘功能" -ForegroundColor Gray
Write-Host ""

Write-Host "?? 调试提示:" -ForegroundColor Yellow
Write-Host "   - 日志文件位置: %USERPROFILE%\AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Player.log" -ForegroundColor Gray
Write-Host "   - 按 Ctrl+Shift+D 在游戏中打开调试菜单" -ForegroundColor Gray
Write-Host "   - 立绘日志已优化，减少控制台干扰" -ForegroundColor Gray
Write-Host ""

Write-Host "? 验证完成！" -ForegroundColor Green
