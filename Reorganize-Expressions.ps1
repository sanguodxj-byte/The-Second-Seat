# 表情文件重组脚本
# 将表情文件从立绘文件夹移动到独立的 Expressions 文件夹

param(
    [string]$NarratorsPath = "Textures\UI\Narrators\9x16",
    [switch]$DryRun = $false  # 模拟运行，不实际移动文件
)

$ErrorActionPreference = "Stop"

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host " 表情文件重组脚本 v1.0" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

if ($DryRun) {
    Write-Host "??  DRY RUN 模式 - 不会实际移动文件" -ForegroundColor Yellow
    Write-Host ""
}

# 检查路径是否存在
if (!(Test-Path $NarratorsPath)) {
    Write-Host "? 错误: 找不到路径 $NarratorsPath" -ForegroundColor Red
    exit 1
}

$expressionsPath = Join-Path $NarratorsPath "Expressions"

# 创建 Expressions 主文件夹
if (!(Test-Path $expressionsPath)) {
    if (!$DryRun) {
        New-Item -Path $expressionsPath -ItemType Directory | Out-Null
    }
    Write-Host "? 创建 Expressions 文件夹: $expressionsPath" -ForegroundColor Green
} else {
    Write-Host "??  Expressions 文件夹已存在" -ForegroundColor Gray
}

Write-Host ""

# 获取所有人格文件夹（排除 Expressions 文件夹）
$personaFolders = Get-ChildItem -Path $NarratorsPath -Directory | Where-Object { 
    $_.Name -ne "Expressions" 
}

if ($personaFolders.Count -eq 0) {
    Write-Host "??  没有找到人格文件夹" -ForegroundColor Yellow
    exit 0
}

Write-Host "?? 找到 $($personaFolders.Count) 个人格文件夹" -ForegroundColor Cyan
Write-Host ""

$totalMoved = 0
$totalFolders = 0

foreach ($persona in $personaFolders) {
    $personaName = $persona.Name
    $sourcePath = $persona.FullName
    $targetPath = Join-Path $expressionsPath $personaName
    
    Write-Host "处理人格: $personaName" -ForegroundColor Cyan
    Write-Host ("─" * 50) -ForegroundColor DarkGray
    
    # 检查是否有表情文件需要移动
    $expressionFiles = Get-ChildItem -Path $sourcePath -File | Where-Object { 
        $_.Name -notmatch "^base\." -and $_.Extension -eq ".png"
    }
    
    $layersPath = Join-Path $sourcePath "layers"
    $hasLayers = Test-Path $layersPath
    
    if ($expressionFiles.Count -eq 0 -and !$hasLayers) {
        Write-Host "  ??  没有表情文件，跳过" -ForegroundColor Gray
        Write-Host ""
        continue
    }
    
    # 创建表情文件夹
    if (!(Test-Path $targetPath)) {
        if (!$DryRun) {
            New-Item -Path $targetPath -ItemType Directory | Out-Null
        }
        Write-Host "  ?? 创建文件夹: Expressions\$personaName" -ForegroundColor Green
    } else {
        Write-Host "  ??  文件夹已存在: Expressions\$personaName" -ForegroundColor Gray
    }
    
    $movedCount = 0
    
    # 移动整图表情文件
    if ($expressionFiles.Count > 0) {
        Write-Host "  ?? 移动整图表情:" -ForegroundColor Yellow
        
        foreach ($file in $expressionFiles) {
            $targetFile = Join-Path $targetPath $file.Name
            
            if ($DryRun) {
                Write-Host "     [DRY RUN] $($file.Name)" -ForegroundColor DarkYellow
            } else {
                try {
                    Move-Item -Path $file.FullName -Destination $targetFile -Force
                    Write-Host "     ? $($file.Name)" -ForegroundColor Green
                    $movedCount++
                } catch {
                    Write-Host "     ? $($file.Name) - 失败: $_" -ForegroundColor Red
                }
            }
        }
    }
    
    # 移动 layers 文件夹
    if ($hasLayers) {
        $targetLayersPath = Join-Path $targetPath "layers"
        
        if ($DryRun) {
            Write-Host "  ?? [DRY RUN] 移动 layers 文件夹" -ForegroundColor DarkYellow
        } else {
            try {
                Move-Item -Path $layersPath -Destination $targetLayersPath -Force
                Write-Host "  ?? ? 移动 layers 文件夹" -ForegroundColor Green
                $movedCount++
            } catch {
                Write-Host "  ?? ? layers 文件夹移动失败: $_" -ForegroundColor Red
            }
        }
    }
    
    $totalMoved += $movedCount
    $totalFolders++
    
    if (!$DryRun -and $movedCount > 0) {
        Write-Host "  ? 完成: 移动了 $movedCount 个项目" -ForegroundColor Green
    } elseif ($DryRun) {
        Write-Host "  ??  [DRY RUN] 将移动 $($expressionFiles.Count + ($hasLayers ? 1 : 0)) 个项目" -ForegroundColor Gray
    }
    
    Write-Host ""
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host " 重组完成！" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

if ($DryRun) {
    Write-Host "??  这是 DRY RUN 模式，没有实际移动文件" -ForegroundColor Yellow
    Write-Host "?? 运行 .\Reorganize-Expressions.ps1 (不加 -DryRun) 来实际执行" -ForegroundColor Cyan
} else {
    Write-Host "?? 统计:" -ForegroundColor Cyan
    Write-Host "   - 处理人格: $totalFolders 个" -ForegroundColor White
    Write-Host "   - 移动项目: $totalMoved 个" -ForegroundColor White
    Write-Host ""
    Write-Host "?? 表情文件现在位于: $expressionsPath" -ForegroundColor Cyan
}

Write-Host ""
Write-Host "?? 完成！请验证文件结构是否正确。" -ForegroundColor Green
Write-Host ""

# 显示新结构预览
Write-Host "?? 新文件结构预览:" -ForegroundColor Cyan
Write-Host ""
Get-ChildItem -Path $expressionsPath -Directory -ErrorAction SilentlyContinue | ForEach-Object {
    Write-Host "   Expressions\$($_.Name)\" -ForegroundColor Yellow
    
    $files = Get-ChildItem -Path $_.FullName -File -ErrorAction SilentlyContinue
    $subDirs = Get-ChildItem -Path $_.FullName -Directory -ErrorAction SilentlyContinue
    
    foreach ($file in $files) {
        Write-Host "   ├─ $($file.Name)" -ForegroundColor Gray
    }
    
    foreach ($dir in $subDirs) {
        Write-Host "   └─ $($dir.Name)\" -ForegroundColor DarkYellow
        $layerFiles = Get-ChildItem -Path $dir.FullName -File -ErrorAction SilentlyContinue
        foreach ($lf in $layerFiles) {
            Write-Host "      └─ $($lf.Name)" -ForegroundColor DarkGray
        }
    }
    
    Write-Host ""
}

Write-Host ""
Write-Host "?? 提示: 记得更新 PortraitLoader.cs 代码以使用新路径！" -ForegroundColor Yellow
Write-Host ""
