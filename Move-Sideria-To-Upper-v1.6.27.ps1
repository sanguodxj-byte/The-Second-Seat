# 移动Sideria到上层正确位置 v1.6.27
# 将 Textures\UI\Narrators\9x16\Layered\Sideria 移动到上层

Write-Host "========== 移动Sideria文件夹到正确位置 v1.6.27 ==========" -ForegroundColor Cyan
Write-Host ""

$modRoot = "D:\steam\steamapps\common\RimWorld\Mods\TheSecondSeat"
$currentPath = Join-Path $modRoot "Textures\UI\Narrators\9x16\Layered\Sideria"
$targetPath = Join-Path $modRoot "Textures\UI\Narrators\9x16\Sideria"

Write-Host "当前路径: $currentPath" -ForegroundColor Yellow
Write-Host "目标路径: $targetPath" -ForegroundColor Green
Write-Host ""

# 检查当前路径是否存在
if (!(Test-Path $currentPath)) {
    Write-Host "? 源文件夹不存在: $currentPath" -ForegroundColor Red
    exit 1
}

# 1. 备份
Write-Host "1?? 创建备份..." -ForegroundColor Yellow
$backupPath = Join-Path $modRoot "Textures_Backup_Sideria_$(Get-Date -Format 'yyyyMMdd_HHmmss')"
Copy-Item (Join-Path $modRoot "Textures") $backupPath -Recurse -Force
Write-Host "? 备份完成: $backupPath" -ForegroundColor Green
Write-Host ""

# 2. 移动文件夹
Write-Host "2?? 移动Sideria文件夹..." -ForegroundColor Yellow

# 如果目标已存在，先删除
if (Test-Path $targetPath) {
    Write-Host "  目标已存在，先删除..." -ForegroundColor Yellow
    Remove-Item $targetPath -Recurse -Force
}

# 移动文件夹
Move-Item $currentPath $targetPath -Force
Write-Host "? 文件夹已移动" -ForegroundColor Green
Write-Host ""

# 3. 验证结果
Write-Host "3?? 验证结果..." -ForegroundColor Yellow

if (Test-Path $targetPath) {
    Write-Host "? Sideria文件夹现在位于: $targetPath" -ForegroundColor Green
    
    # 列出内容
    $items = Get-ChildItem $targetPath
    Write-Host "  包含 $($items.Count) 个项目:" -ForegroundColor Gray
    
    foreach ($item in $items) {
        if ($item.PSIsContainer) {
            Write-Host "    ?? $($item.Name)" -ForegroundColor Cyan
        } else {
            $sizeKB = [math]::Round($item.Length / 1KB, 2)
            Write-Host "    ?? $($item.Name) ($sizeKB KB)" -ForegroundColor Gray
        }
    }
} else {
    Write-Host "? 移动失败" -ForegroundColor Red
}

Write-Host ""

# 4. 检查base.png或完整立绘
Write-Host "4?? 检查立绘文件..." -ForegroundColor Yellow

$basePng = Join-Path $targetPath "base.png"
$neutralPng = Join-Path $targetPath "neutral.png"

if (Test-Path $basePng) {
    $fileInfo = Get-Item $basePng
    Write-Host "? 找到 base.png ($([math]::Round($fileInfo.Length / 1KB, 2)) KB)" -ForegroundColor Green
} elseif (Test-Path $neutralPng) {
    $fileInfo = Get-Item $neutralPng
    Write-Host "? 找到 neutral.png ($([math]::Round($fileInfo.Length / 1KB, 2)) KB)" -ForegroundColor Green
} else {
    Write-Host "??  未找到 base.png 或 neutral.png" -ForegroundColor Yellow
    Write-Host "   需要放置完整立绘到: $targetPath" -ForegroundColor Gray
}

Write-Host ""

# 5. 检查表情文件夹
$expressionsPath = Join-Path $modRoot "Textures\UI\Narrators\9x16\Expressions\Sideria"

Write-Host "5?? 检查表情文件夹..." -ForegroundColor Yellow

if (Test-Path $expressionsPath) {
    $expressionFiles = Get-ChildItem $expressionsPath -Filter "*.png"
    Write-Host "? 表情文件夹存在，包含 $($expressionFiles.Count) 个PNG文件" -ForegroundColor Green
    
    if ($expressionFiles.Count -gt 0) {
        Write-Host "   表情文件:" -ForegroundColor Gray
        foreach ($file in $expressionFiles | Select-Object -First 5) {
            Write-Host "     - $($file.Name)" -ForegroundColor Gray
        }
        if ($expressionFiles.Count -gt 5) {
            Write-Host "     ... 还有 $($expressionFiles.Count - 5) 个文件" -ForegroundColor Gray
        }
    }
} else {
    Write-Host "??  表情文件夹不存在: $expressionsPath" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# 6. 最终结构说明
Write-Host "?? 正确的文件夹结构:" -ForegroundColor Cyan
Write-Host ""
Write-Host "Textures/" -ForegroundColor White
Write-Host "  └── UI/" -ForegroundColor Gray
Write-Host "      └── Narrators/" -ForegroundColor Gray
Write-Host "          └── 9x16/" -ForegroundColor Gray
Write-Host "              ├── Sideria/" -ForegroundColor Green
Write-Host "              │   ├── base.png (必需)" -ForegroundColor Yellow
Write-Host "              │   └── 其他资源..." -ForegroundColor Gray
Write-Host "              ├── Expressions/" -ForegroundColor Gray
Write-Host "              │   └── Sideria/" -ForegroundColor Gray
Write-Host "              │       ├── happy.png" -ForegroundColor Gray
Write-Host "              │       ├── sad.png" -ForegroundColor Gray
Write-Host "              │       └── ..." -ForegroundColor Gray
Write-Host "              └── Layered/" -ForegroundColor Gray
Write-Host "                  └── {其他人格}/" -ForegroundColor Gray
Write-Host ""

Write-Host "========== 移动完成 ==========" -ForegroundColor Green
Write-Host ""
Write-Host "?? 后续步骤:" -ForegroundColor Cyan
Write-Host "1. 确认 base.png 文件存在于:" -ForegroundColor Yellow
Write-Host "   $targetPath\base.png" -ForegroundColor Gray
Write-Host ""
Write-Host "2. 如果使用分层立绘，需要重新创建 Layered\Sideria\ 文件夹" -ForegroundColor Yellow
Write-Host ""
Write-Host "3. 重启RimWorld游戏并测试立绘显示" -ForegroundColor Yellow
Write-Host ""
Write-Host "备份位置: $backupPath" -ForegroundColor Gray
