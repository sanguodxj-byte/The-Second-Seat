# 眨眼和张嘴动画系统完整诊断脚本 v1.6.33
# 检查所有可能导致动画无效的原因

Write-Host "================================" -ForegroundColor Cyan
Write-Host "眨眼和张嘴动画系统诊断 v1.6.33" -ForegroundColor Cyan
Write-Host "================================" -ForegroundColor Cyan
Write-Host ""

$modPath = "D:\steam\steamapps\common\RimWorld\Mods\TheSecondSeat"
$layeredPath = "$modPath\Textures\UI\Narrators\9x16\Layered\Sideria"

# 1. 检查 DLL 是否最新
Write-Host "1?? 检查 DLL 部署状态..." -ForegroundColor Yellow
$dllPath = "$modPath\1.6\Assemblies\TheSecondSeat.dll"
if (Test-Path $dllPath) {
    $dllInfo = Get-Item $dllPath
    Write-Host "? DLL 存在" -ForegroundColor Green
    Write-Host "   路径: $dllPath" -ForegroundColor Gray
    Write-Host "   大小: $($dllInfo.Length) 字节" -ForegroundColor Gray
    Write-Host "   修改时间: $($dllInfo.LastWriteTime)" -ForegroundColor Gray
    
    $timeDiff = (Get-Date) - $dllInfo.LastWriteTime
    if ($timeDiff.TotalMinutes -lt 10) {
        Write-Host "   ? DLL 是最近部署的（$([math]::Round($timeDiff.TotalMinutes, 1)) 分钟前）" -ForegroundColor Green
    } else {
        Write-Host "   ?? DLL 不是最近部署的（$([math]::Round($timeDiff.TotalHours, 1)) 小时前）" -ForegroundColor Yellow
        Write-Host "   建议重新编译并部署" -ForegroundColor Yellow
    }
} else {
    Write-Host "? DLL 不存在！" -ForegroundColor Red
    Write-Host "   请运行编译和部署" -ForegroundColor Red
}

Write-Host ""

# 2. 检查分层立绘纹理文件
Write-Host "2?? 检查分层立绘纹理文件..." -ForegroundColor Yellow

if (!(Test-Path $layeredPath)) {
    Write-Host "? 分层立绘文件夹不存在！" -ForegroundColor Red
    Write-Host "   路径: $layeredPath" -ForegroundColor Red
    exit
}

# 必需文件清单
$requiredFiles = @{
    "base_body.png" = "基础身体层"
    "closed_eyes.png" = "闭眼层（眨眼时）"
    "happy_eyes.png" = "开心眼睛层（默认睁眼）"
    "sad_eyes.png" = "悲伤眼睛层"
    "angry_eyes.png" = "愤怒眼睛层"
    "confused_eyes.png" = "疑惑眼睛层"
    "small_mouth.png" = "小张嘴层（说话时）"
    "medium_mouth.png" = "中等张嘴层（说话时）"
    "larger_mouth.png" = "大张嘴层（说话时）"
    "happy_mouth.png" = "开心嘴型层（表情）"
    "sad_mouth.png" = "悲伤嘴型层（表情）"
    "angry_mouth.png" = "愤怒嘴型层（表情）"
}

$missingFiles = @()
$existingFiles = @()

foreach ($file in $requiredFiles.Keys) {
    $filePath = Join-Path $layeredPath $file
    if (Test-Path $filePath) {
        $size = (Get-Item $filePath).Length
        $existingFiles += $file
        Write-Host "   ? $file ($([math]::Round($size/1KB, 2)) KB) - $($requiredFiles[$file])" -ForegroundColor Green
    } else {
        $missingFiles += $file
        Write-Host "   ? $file - $($requiredFiles[$file]) - 缺失" -ForegroundColor Red
    }
}

Write-Host ""
Write-Host "   统计: $($existingFiles.Count) / $($requiredFiles.Count) 个文件存在" -ForegroundColor $(if ($missingFiles.Count -eq 0) { "Green" } else { "Yellow" })

if ($missingFiles.Count -gt 0) {
    Write-Host ""
    Write-Host "   ?? 缺失文件列表:" -ForegroundColor Red
    foreach ($file in $missingFiles) {
        Write-Host "      - $file" -ForegroundColor Red
    }
}

Write-Host ""

# 3. 检查 NarratorPersonaDef 配置
Write-Host "3?? 检查 NarratorPersonaDef 配置..." -ForegroundColor Yellow
$defsPath = "$modPath\Defs\NarratorPersonaDefs.xml"

if (Test-Path $defsPath) {
    Write-Host "   ? NarratorPersonaDefs.xml 存在" -ForegroundColor Green
    
    $xmlContent = Get-Content $defsPath -Raw
    
    # 检查 Sideria 是否启用分层立绘
    if ($xmlContent -match '<defName>Sideria_Default</defName>[\s\S]*?<useLayeredPortrait>(.*?)</useLayeredPortrait>') {
        $useLayered = $matches[1]
        if ($useLayered -eq "true") {
            Write-Host "   ? Sideria 已启用分层立绘（useLayeredPortrait=true）" -ForegroundColor Green
        } else {
            Write-Host "   ? Sideria 未启用分层立绘（useLayeredPortrait=$useLayered）" -ForegroundColor Red
            Write-Host "   修复：在 Sideria_Default 中添加 <useLayeredPortrait>true</useLayeredPortrait>" -ForegroundColor Yellow
        }
    } else {
        Write-Host "   ?? 未找到 useLayeredPortrait 配置" -ForegroundColor Yellow
        Write-Host "   建议添加：<useLayeredPortrait>true</useLayeredPortrait>" -ForegroundColor Yellow
    }
} else {
    Write-Host "   ? NarratorPersonaDefs.xml 不存在！" -ForegroundColor Red
}

Write-Host ""

# 4. 检查 LayeredPortraitCompositor 调用
Write-Host "4?? 检查 LayeredPortraitCompositor 是否被调用..." -ForegroundColor Yellow
$compositorPath = "C:\Users\Administrator\Desktop\rim mod\The Second Seat\Source\TheSecondSeat\PersonaGeneration\LayeredPortraitCompositor.cs"

if (Test-Path $compositorPath) {
    $compositorCode = Get-Content $compositorPath -Raw
    
    # 检查是否有眨眼系统调用
    if ($compositorCode -match 'BlinkAnimationSystem\.GetEyeLayerName') {
        Write-Host "   ? LayeredPortraitCompositor 调用了 BlinkAnimationSystem" -ForegroundColor Green
    } else {
        Write-Host "   ? LayeredPortraitCompositor 未调用 BlinkAnimationSystem" -ForegroundColor Red
    }
    
    # 检查是否有张嘴系统调用
    if ($compositorCode -match 'MouthAnimationSystem\.GetMouthLayerName') {
        Write-Host "   ? LayeredPortraitCompositor 调用了 MouthAnimationSystem" -ForegroundColor Green
    } else {
        Write-Host "   ? LayeredPortraitCompositor 未调用 MouthAnimationSystem" -ForegroundColor Red
    }
} else {
    Write-Host "   ?? LayeredPortraitCompositor.cs 不存在（无法检查源码）" -ForegroundColor Yellow
}

Write-Host ""

# 5. 检查立绘面板是否启用
Write-Host "5?? 检查立绘面板配置..." -ForegroundColor Yellow
Write-Host "   请在游戏中确认以下设置：" -ForegroundColor Cyan
Write-Host "   1. Mod设置 → The Second Seat → ? 使用立绘模式" -ForegroundColor Gray
Write-Host "   2. 点击 AI 按钮后，立绘面板是否显示" -ForegroundColor Gray
Write-Host "   3. DevMode (F11) → 查看日志是否有 [LayeredPortraitCompositor] 字样" -ForegroundColor Gray

Write-Host ""

# 6. 检查呼吸动画系统
Write-Host "6?? 检查呼吸动画系统..." -ForegroundColor Yellow
$expressionSystemPath = "C:\Users\Administrator\Desktop\rim mod\The Second Seat\Source\TheSecondSeat\PersonaGeneration\ExpressionSystem.cs"

if (Test-Path $expressionSystemPath) {
    $expressionCode = Get-Content $expressionSystemPath -Raw
    
    if ($expressionCode -match 'GetBreathingOffset') {
        Write-Host "   ? ExpressionSystem 包含呼吸动画逻辑" -ForegroundColor Green
    } else {
        Write-Host "   ? ExpressionSystem 不包含呼吸动画逻辑" -ForegroundColor Red
    }
} else {
    Write-Host "   ?? ExpressionSystem.cs 不存在（无法检查源码）" -ForegroundColor Yellow
}

Write-Host ""

# 7. 生成修复建议
Write-Host "================================" -ForegroundColor Cyan
Write-Host "7?? 修复建议" -ForegroundColor Yellow
Write-Host "================================" -ForegroundColor Cyan

if ($missingFiles.Count -gt 0) {
    Write-Host ""
    Write-Host "?? 缺失纹理文件修复：" -ForegroundColor Yellow
    Write-Host "   方法1：从源项目复制" -ForegroundColor Gray
    Write-Host "   Copy-Item 'C:\Users\Administrator\Desktop\rim mod\The Second Seat\Textures\UI\Narrators\9x16\Layered\Sideria\*' '$layeredPath' -Force" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "   方法2：重新部署完整纹理文件夹" -ForegroundColor Gray
    Write-Host "   .\Deploy-v1.6.33-Complete.ps1" -ForegroundColor Cyan
}

Write-Host ""
Write-Host "?? 通用修复步骤：" -ForegroundColor Yellow
Write-Host "   1. 完全退出 RimWorld" -ForegroundColor Gray
Write-Host "   2. 重新编译并部署 DLL：" -ForegroundColor Gray
Write-Host "      dotnet build Source\TheSecondSeat\TheSecondSeat.csproj -c Release" -ForegroundColor Cyan
Write-Host "      Copy-Item Source\TheSecondSeat\bin\Release\net472\TheSecondSeat.dll D:\steam\steamapps\common\RimWorld\Mods\TheSecondSeat\1.6\Assemblies\" -ForegroundColor Cyan
Write-Host "   3. 重新启动 RimWorld" -ForegroundColor Gray
Write-Host "   4. 加载存档" -ForegroundColor Gray
Write-Host "   5. 启用立绘模式（Mod设置）" -ForegroundColor Gray
Write-Host "   6. 点击 AI 按钮观察立绘面板" -ForegroundColor Gray

Write-Host ""
Write-Host "?? DevMode 调试步骤：" -ForegroundColor Yellow
Write-Host "   1. 按 F11 启用 DevMode" -ForegroundColor Gray
Write-Host "   2. 查看日志文件：%AppData%\..\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Player.log" -ForegroundColor Gray
Write-Host "   3. 搜索关键词：" -ForegroundColor Gray
Write-Host "      - [LayeredPortraitCompositor]" -ForegroundColor Cyan
Write-Host "      - [BlinkAnimationSystem]" -ForegroundColor Cyan
Write-Host "      - [MouthAnimationSystem]" -ForegroundColor Cyan
Write-Host "      - [ExpressionSystem]" -ForegroundColor Cyan

Write-Host ""
Write-Host "================================" -ForegroundColor Cyan
Write-Host "诊断完成" -ForegroundColor Green
Write-Host "================================" -ForegroundColor Cyan
