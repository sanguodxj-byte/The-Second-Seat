# 修复嵌套Textures文件夹 v1.6.27
# 解决 Textures\Textures 嵌套问题

Write-Host "========== 修复嵌套Textures文件夹 v1.6.27 ==========" -ForegroundColor Cyan
Write-Host ""

$modRoot = "D:\steam\steamapps\common\RimWorld\Mods\TheSecondSeat"
$texturesRoot = Join-Path $modRoot "Textures"
$nestedTextures = Join-Path $texturesRoot "Textures"

Write-Host "?? 检查文件夹结构..." -ForegroundColor Yellow
Write-Host "Mod根目录: $modRoot"
Write-Host "Textures根目录: $texturesRoot"
Write-Host "嵌套的Textures: $nestedTextures"
Write-Host ""

if (!(Test-Path $nestedTextures)) {
    Write-Host "? 没有嵌套的Textures文件夹，结构正确" -ForegroundColor Green
    exit 0
}

Write-Host "? 发现嵌套的Textures文件夹！" -ForegroundColor Red
Write-Host ""

# 1. 备份当前结构
Write-Host "1?? 创建备份..." -ForegroundColor Yellow
$backupPath = Join-Path $modRoot "Textures_Backup_$(Get-Date -Format 'yyyyMMdd_HHmmss')"
Copy-Item $texturesRoot $backupPath -Recurse -Force
Write-Host "? 备份完成: $backupPath" -ForegroundColor Green
Write-Host ""

# 2. 移动嵌套内容到正确位置
Write-Host "2?? 移动文件到正确位置..." -ForegroundColor Yellow

# 获取嵌套Textures下的所有子文件夹和文件
$nestedItems = Get-ChildItem $nestedTextures -Force

foreach ($item in $nestedItems) {
    $targetPath = Join-Path $texturesRoot $item.Name
    
    Write-Host "  移动: $($item.Name)" -ForegroundColor Gray
    
    # 如果目标已存在，合并内容
    if (Test-Path $targetPath) {
        Write-Host "    目标已存在，合并内容..." -ForegroundColor Yellow
        
        if ($item.PSIsContainer) {
            # 递归复制文件夹内容
            Copy-Item "$($item.FullName)\*" $targetPath -Recurse -Force
        } else {
            # 复制文件（覆盖）
            Copy-Item $item.FullName $targetPath -Force
        }
    } else {
        # 直接移动
        Move-Item $item.FullName $targetPath -Force
    }
}

Write-Host "? 文件移动完成" -ForegroundColor Green
Write-Host ""

# 3. 删除嵌套的Textures文件夹
Write-Host "3?? 删除嵌套文件夹..." -ForegroundColor Yellow
Remove-Item $nestedTextures -Recurse -Force
Write-Host "? 嵌套文件夹已删除" -ForegroundColor Green
Write-Host ""

# 4. 删除可疑的Administrator文件夹
$adminFolder = Join-Path $texturesRoot "Administrator"
if (Test-Path $adminFolder) {
    Write-Host "4?? 删除Administrator文件夹..." -ForegroundColor Yellow
    Remove-Item $adminFolder -Recurse -Force
    Write-Host "? Administrator文件夹已删除" -ForegroundColor Green
    Write-Host ""
}

# 5. 验证修复结果
Write-Host "5?? 验证修复结果..." -ForegroundColor Yellow
Write-Host ""

$uiFolder = Join-Path $texturesRoot "UI"
$narratorsFolder = Join-Path $uiFolder "Narrators"
$layeredFolder = Join-Path $narratorsFolder "9x16\Layered"
$expressionsFolder = Join-Path $narratorsFolder "9x16\Expressions"

Write-Host "检查关键路径:" -ForegroundColor Cyan

if (Test-Path $uiFolder) {
    Write-Host "  ? UI文件夹存在" -ForegroundColor Green
} else {
    Write-Host "  ? UI文件夹不存在" -ForegroundColor Red
}

if (Test-Path $narratorsFolder) {
    Write-Host "  ? Narrators文件夹存在" -ForegroundColor Green
} else {
    Write-Host "  ? Narrators文件夹不存在" -ForegroundColor Red
}

if (Test-Path $layeredFolder) {
    Write-Host "  ? Layered文件夹存在" -ForegroundColor Green
    
    $personaFolders = Get-ChildItem $layeredFolder -Directory
    Write-Host "    找到 $($personaFolders.Count) 个人格文件夹" -ForegroundColor Gray
} else {
    Write-Host "  ? Layered文件夹不存在" -ForegroundColor Red
}

if (Test-Path $expressionsFolder) {
    Write-Host "  ? Expressions文件夹存在" -ForegroundColor Green
    
    $expressionFolders = Get-ChildItem $expressionsFolder -Directory
    Write-Host "    找到 $($expressionFolders.Count) 个表情文件夹" -ForegroundColor Gray
} else {
    Write-Host "  ??  Expressions文件夹不存在（可选）" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# 6. 最终结构展示
Write-Host "?? 修复后的文件夹结构:" -ForegroundColor Cyan
Write-Host ""
Write-Host "Textures/" -ForegroundColor White
Write-Host "  ├── UI/" -ForegroundColor Gray
Write-Host "  │   ├── Narrators/" -ForegroundColor Gray
Write-Host "  │   │   ├── 9x16/" -ForegroundColor Gray
Write-Host "  │   │   │   ├── Layered/" -ForegroundColor Gray
Write-Host "  │   │   │   │   └── {PersonaName}/" -ForegroundColor Gray
Write-Host "  │   │   │   │       ├── body.png" -ForegroundColor Gray
Write-Host "  │   │   │   │       ├── neutral_eyes.png" -ForegroundColor Gray
Write-Host "  │   │   │   │       ├── neutral_mouth.png" -ForegroundColor Gray
Write-Host "  │   │   │   │       └── ..." -ForegroundColor Gray
Write-Host "  │   │   │   └── Expressions/" -ForegroundColor Gray
Write-Host "  │   │   │       └── {PersonaName}/" -ForegroundColor Gray
Write-Host "  │   │   │           └── happy.png" -ForegroundColor Gray
Write-Host "  │   └── StatusIcons/" -ForegroundColor Gray
Write-Host "  └── ..." -ForegroundColor Gray
Write-Host ""

Write-Host "========== 修复完成 ==========" -ForegroundColor Green
Write-Host ""
Write-Host "?? 后续步骤:" -ForegroundColor Cyan
Write-Host "1. 重启RimWorld游戏" -ForegroundColor Yellow
Write-Host "2. 加载存档" -ForegroundColor Yellow
Write-Host "3. 打开AI旁白按钮" -ForegroundColor Yellow
Write-Host "4. 切换到立绘模式，检查立绘是否正常显示" -ForegroundColor Yellow
Write-Host ""
Write-Host "备份位置: $backupPath" -ForegroundColor Gray
