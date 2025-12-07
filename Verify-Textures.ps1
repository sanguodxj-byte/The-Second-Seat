Write-Host ""
Write-Host "=== UI 纹理文件验证工具 ===" -ForegroundColor Cyan
Write-Host ""

$projectDir = Get-Location
$textureDir = Join-Path $projectDir "Textures\UI"
$deployDir = "D:\steam\steamapps\common\RimWorld\Mods\TheSecondSeat\Textures\UI"

# 必需的纹理文件（支持多种尺寸）
$requiredTextures = @(
    @{Name="就绪按钮"; File="NarratorButton_Ready.png"; Sizes=@("256x256", "512x512", "532x532"); Priority="高"},
    @{Name="处理中按钮"; File="NarratorButton_Processing.png"; Sizes=@("256x256", "512x512", "532x532"); Priority="高"},
    @{Name="错误按钮"; File="NarratorButton_Error.png"; Sizes=@("256x256", "512x512", "532x532"); Priority="高"},
    @{Name="禁用按钮"; File="NarratorButton_Disabled.png"; Sizes=@("256x256", "512x512", "532x532"); Priority="中"}
)

# 可选的纹理文件
$optionalTextures = @(
    @{Name="状态面板"; File="Narrator\StatusPanel.png"; Size="512x512"; Priority="低"},
    @{Name="主窗口"; File="Narrator\MainWindow.png"; Size="1024x1024"; Priority="低"},
    @{Name="在线指示"; File="StatusIcons\Online.png"; Size="64x64"; Priority="低"},
    @{Name="同步环"; File="StatusIcons\Sync.png"; Size="128x128"; Priority="低"},
    @{Name="连接齿轮"; File="StatusIcons\Link.png"; Size="128x128"; Priority="低"},
    @{Name="错误标志"; File="StatusIcons\Error.png"; Size="64x64"; Priority="低"}
)

Write-Host "?? 纹理目录：" -ForegroundColor Yellow
Write-Host "  项目：$textureDir" -ForegroundColor White
if (Test-Path $textureDir) {
    Write-Host "  ? 存在" -ForegroundColor Green
} else {
    Write-Host "  ? 不存在" -ForegroundColor Red
    Write-Host ""
    Write-Host "正在创建纹理目录..." -ForegroundColor Yellow
    New-Item -Path $textureDir -ItemType Directory -Force | Out-Null
    Write-Host "  ? 已创建" -ForegroundColor Green
}

Write-Host ""
Write-Host "?? 必需纹理文件：" -ForegroundColor Yellow
$foundRequired = 0
$totalRequired = $requiredTextures.Count

foreach ($texture in $requiredTextures) {
    $path = Join-Path $textureDir $texture.File
    $status = if (Test-Path $path) { 
        $foundRequired++
        "?" 
    } else { 
        "?" 
    }
    
    $priority = switch ($texture.Priority) {
        "高" { "??" }
        "中" { "??" }
        "低" { "??" }
    }
    
    Write-Host "  $status $priority $($texture.Name) ($($texture.Sizes -join ', '))" -ForegroundColor $(if ($status -eq "?") { "Green" } else { "Yellow" })
    Write-Host "     文件：$($texture.File)" -ForegroundColor DarkGray
    
    if (Test-Path $path) {
        $fileInfo = Get-Item $path
        $sizeKB = [math]::Round($fileInfo.Length / 1KB, 2)
        Write-Host "     大小：$sizeKB KB" -ForegroundColor DarkGray
        
        # 验证文件格式
        if ($fileInfo.Extension -ne ".png") {
            Write-Host "     ?? 警告：不是 PNG 格式" -ForegroundColor Red
        }
    }
}

Write-Host ""
Write-Host "?? 必需文件统计：$foundRequired / $totalRequired" -ForegroundColor $(if ($foundRequired -eq $totalRequired) { "Green" } else { "Yellow" })

Write-Host ""
Write-Host "?? 可选纹理文件：" -ForegroundColor Yellow
$foundOptional = 0

foreach ($texture in $optionalTextures) {
    $path = Join-Path $textureDir $texture.File
    if (Test-Path $path) {
        $foundOptional++
        Write-Host "  ? $($texture.Name) ($($texture.Size))" -ForegroundColor Green
        
        $fileInfo = Get-Item $path
        $sizeKB = [math]::Round($fileInfo.Length / 1KB, 2)
        Write-Host "     大小：$sizeKB KB" -ForegroundColor DarkGray
    } else {
        Write-Host "  ? $($texture.Name) ($($texture.Size)) - 未添加" -ForegroundColor DarkGray
    }
}

Write-Host ""
Write-Host "?? 可选文件统计：$foundOptional / $($optionalTextures.Count)" -ForegroundColor Cyan

# 检查部署目录
Write-Host ""
Write-Host "?? 部署目录检查：" -ForegroundColor Yellow
if (Test-Path $deployDir) {
    Write-Host "  ? 部署目录存在" -ForegroundColor Green
    
    # 检查已部署的纹理
    $deployedCount = 0
    foreach ($texture in $requiredTextures) {
        $deployPath = Join-Path $deployDir $texture.File
        if (Test-Path $deployPath) {
            $deployedCount++
        }
    }
    
    Write-Host "  ?? 已部署必需纹理：$deployedCount / $totalRequired" -ForegroundColor $(if ($deployedCount -eq $foundRequired) { "Green" } else { "Yellow" })
    
    if ($deployedCount -lt $foundRequired) {
        Write-Host "  ?? 部署目录纹理不完整，建议重新部署" -ForegroundColor Yellow
    }
} else {
    Write-Host "  ? 部署目录不存在（未部署）" -ForegroundColor Yellow
}

# 生成报告
Write-Host ""
Write-Host "=" * 60 -ForegroundColor Cyan
Write-Host "?? 验证总结" -ForegroundColor Cyan
Write-Host "=" * 60 -ForegroundColor Cyan

$completionPercent = [math]::Round(($foundRequired / $totalRequired) * 100, 0)

Write-Host ""
Write-Host "完成度：$completionPercent%" -ForegroundColor $(
    if ($completionPercent -eq 100) { "Green" }
    elseif ($completionPercent -ge 75) { "Yellow" }
    else { "Red" }
)

Write-Host ""
if ($foundRequired -eq 0) {
    Write-Host "状态：? 未添加任何纹理" -ForegroundColor Red
    Write-Host ""
    Write-Host "?? 下一步：" -ForegroundColor Yellow
    Write-Host "  1. 参考 'UI纹理文件完整指南.md'" -ForegroundColor White
    Write-Host "  2. 设计或下载 4 个按钮纹理（256x256 PNG）" -ForegroundColor White
    Write-Host "  3. 放入 Textures\UI\ 文件夹" -ForegroundColor White
    Write-Host "  4. 重新运行此脚本验证" -ForegroundColor White
} elseif ($foundRequired -lt $totalRequired) {
    Write-Host "状态：? 部分完成" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "?? 下一步：" -ForegroundColor Yellow
    Write-Host "  1. 完成剩余 $($totalRequired - $foundRequired) 个必需纹理" -ForegroundColor White
    Write-Host "  2. 运行 .\一键部署.ps1 部署到游戏" -ForegroundColor White
    Write-Host "  3. 启动游戏测试效果" -ForegroundColor White
} else {
    Write-Host "状态：? 必需纹理全部就绪！" -ForegroundColor Green
    Write-Host ""
    Write-Host "?? 下一步：" -ForegroundColor Yellow
    Write-Host "  1. 运行 .\一键部署.ps1 部署到游戏" -ForegroundColor White
    Write-Host "  2. 启动 RimWorld" -ForegroundColor White
    Write-Host "  3. 查看右上角按钮显示" -ForegroundColor White
    Write-Host "  4. 测试 Processing 和 Error 状态" -ForegroundColor White
    
    if ($foundOptional -gt 0) {
        Write-Host ""
        Write-Host "?? 额外完成 $foundOptional 个可选纹理！" -ForegroundColor Magenta
    }
}

Write-Host ""
Write-Host "?? 参考文档：" -ForegroundColor Cyan
Write-Host "  - UI纹理文件完整指南.md" -ForegroundColor White
Write-Host "  - 按钮纹理命名规范.md" -ForegroundColor White
Write-Host "  - 动画按钮系统实现总结.md" -ForegroundColor White

Write-Host ""
