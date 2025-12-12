# 分层立绘资源验证脚本 - v1.6.18

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  分层立绘资源验证" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

$baseDir = "Textures\UI\Narrators\9x16\Layered"

# 检查 Layered 文件夹是否存在
if (!(Test-Path $baseDir)) {
    Write-Host "[?] 错误: Layered 文件夹不存在!" -ForegroundColor Red
    Write-Host "    路径: $baseDir" -ForegroundColor Yellow
    exit 1
}

Write-Host "[?] Layered 文件夹存在" -ForegroundColor Green
Write-Host ""

# 获取所有人格文件夹
$personaFolders = Get-ChildItem -Path $baseDir -Directory

if ($personaFolders.Count -eq 0) {
    Write-Host "[?] 错误: 没有找到任何人格文件夹!" -ForegroundColor Red
    exit 1
}

Write-Host "[?] 找到 $($personaFolders.Count) 个人格文件夹" -ForegroundColor Green
Write-Host ""

# 定义分层立绘规范
$requiredFiles = @{
    "base_body.png" = "底图（身体+默认表情）"
}

$expressionParts = @{
    "eyes" = "眼睛部件"
    "mouth" = "嘴巴部件"
    "flush" = "脸红部件（可选）"
}

$optionalFiles = @{
    "hair.png" = "头发层"
    "accessories.png" = "配饰层"
    "background.png" = "背景层"
    "outfit_default.png" = "默认服装"
}

# 表情列表
$expressions = @("neutral", "happy", "sad", "angry", "surprised", "confused", "smug", "shy")

# 验证每个人格文件夹
foreach ($persona in $personaFolders) {
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "  验证人格: $($persona.Name)" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host ""
    
    $personaPath = $persona.FullName
    $allFiles = Get-ChildItem -Path $personaPath -File -Filter "*.png"
    
    Write-Host "文件总数: $($allFiles.Count)" -ForegroundColor Yellow
    Write-Host ""
    
    # 1. 检查必需文件
    Write-Host "【1. 必需文件检查】" -ForegroundColor Magenta
    $hasBaseBody = $false
    
    foreach ($file in $requiredFiles.Keys) {
        $filePath = Join-Path $personaPath $file
        if (Test-Path $filePath) {
            $fileSize = (Get-Item $filePath).Length
            $sizeMB = [math]::Round($fileSize / 1MB, 2)
            Write-Host "  [?] $file - $sizeMB MB" -ForegroundColor Green
            Write-Host "      说明: $($requiredFiles[$file])" -ForegroundColor Gray
            
            if ($file -eq "base_body.png") {
                $hasBaseBody = $true
            }
        } else {
            Write-Host "  [?] $file - 缺失!" -ForegroundColor Red
            Write-Host "      说明: $($requiredFiles[$file])" -ForegroundColor Gray
        }
    }
    Write-Host ""
    
    # 2. 检查表情部件
    Write-Host "【2. 表情部件检查】" -ForegroundColor Magenta
    $expressionStats = @{}
    
    foreach ($expr in $expressions) {
        $expressionStats[$expr] = @{
            "eyes" = $false
            "mouth" = $false
            "flush" = $false
        }
    }
    
    foreach ($file in $allFiles) {
        $fileName = $file.Name.ToLower()
        
        # 检查是否是表情部件
        foreach ($expr in $expressions) {
            foreach ($part in $expressionParts.Keys) {
                if ($fileName -match "^${expr}_${part}\.png$") {
                    $expressionStats[$expr][$part] = $true
                }
            }
        }
    }
    
    # 显示表情部件统计
    foreach ($expr in $expressions) {
        $stats = $expressionStats[$expr]
        $hasEyes = $stats["eyes"]
        $hasMouth = $stats["mouth"]
        $hasFlush = $stats["flush"]
        
        $status = ""
        if ($hasEyes -and $hasMouth) {
            $status = "[?] 完整"
            $color = "Green"
        } elseif ($hasEyes -or $hasMouth) {
            $status = "[?] 部分"
            $color = "Yellow"
        } else {
            $status = "[?] 缺失"
            $color = "DarkGray"
        }
        
        Write-Host "  $status $expr 表情" -ForegroundColor $color
        if ($hasEyes -or $hasMouth -or $hasFlush) {
            Write-Host "      - eyes: $(if ($hasEyes) { '?' } else { '?' })" -ForegroundColor Gray
            Write-Host "      - mouth: $(if ($hasMouth) { '?' } else { '?' })" -ForegroundColor Gray
            Write-Host "      - flush: $(if ($hasFlush) { '?' } else { '? (可选)' })" -ForegroundColor Gray
        }
    }
    Write-Host ""
    
    # 3. 检查可选文件
    Write-Host "【3. 可选文件检查】" -ForegroundColor Magenta
    foreach ($file in $optionalFiles.Keys) {
        $filePath = Join-Path $personaPath $file
        if (Test-Path $filePath) {
            $fileSize = (Get-Item $filePath).Length
            $sizeMB = [math]::Round($fileSize / 1MB, 2)
            Write-Host "  [?] $file - $sizeMB MB" -ForegroundColor Green
            Write-Host "      说明: $($optionalFiles[$file])" -ForegroundColor Gray
        } else {
            Write-Host "  [○] $file - 未配置（可选）" -ForegroundColor DarkGray
            Write-Host "      说明: $($optionalFiles[$file])" -ForegroundColor Gray
        }
    }
    Write-Host ""
    
    # 4. 检查未识别文件
    Write-Host "【4. 其他文件检查】" -ForegroundColor Magenta
    $recognizedPatterns = @(
        "^base_body\.png$",
        "^hair\.png$",
        "^accessories\.png$",
        "^background\.png$",
        "^outfit_.*\.png$",
        "^(neutral|happy|sad|angry|surprised|confused|smug|shy)_(eyes|mouth|flush)\.png$",
        "^closed_eyes\.png$",
        "^small_mouth\.png$",
        "^lager_mouth\.png$",
        "^flush_.*\.png$"
    )
    
    $unknownFiles = @()
    foreach ($file in $allFiles) {
        $fileName = $file.Name.ToLower()
        $isRecognized = $false
        
        foreach ($pattern in $recognizedPatterns) {
            if ($fileName -match $pattern) {
                $isRecognized = $true
                break
            }
        }
        
        if (!$isRecognized) {
            $unknownFiles += $file.Name
        }
    }
    
    if ($unknownFiles.Count -gt 0) {
        Write-Host "  [?] 发现 $($unknownFiles.Count) 个未识别的文件:" -ForegroundColor Yellow
        foreach ($file in $unknownFiles) {
            Write-Host "      - $file" -ForegroundColor Yellow
        }
    } else {
        Write-Host "  [?] 所有文件都符合命名规范" -ForegroundColor Green
    }
    Write-Host ""
    
    # 5. 生成建议
    Write-Host "【5. 配置建议】" -ForegroundColor Magenta
    
    if (!$hasBaseBody) {
        Write-Host "  [!] 缺少 base_body.png，系统无法使用分层立绘！" -ForegroundColor Red
        Write-Host "      建议: 创建 base_body.png（身体+默认表情）" -ForegroundColor Yellow
    } else {
        Write-Host "  [?] 已配置 base_body.png" -ForegroundColor Green
    }
    
    # 统计可用表情
    $completeExpressions = @()
    $partialExpressions = @()
    
    foreach ($expr in $expressions) {
        $stats = $expressionStats[$expr]
        if ($stats["eyes"] -and $stats["mouth"]) {
            $completeExpressions += $expr
        } elseif ($stats["eyes"] -or $stats["mouth"]) {
            $partialExpressions += $expr
        }
    }
    
    if ($completeExpressions.Count -gt 0) {
        Write-Host "  [?] 完整表情: $($completeExpressions -join ', ')" -ForegroundColor Green
    }
    
    if ($partialExpressions.Count -gt 0) {
        Write-Host "  [?] 部分表情: $($partialExpressions -join ', ')" -ForegroundColor Yellow
        Write-Host "      建议: 补充缺失的 eyes 或 mouth 部件" -ForegroundColor Yellow
    }
    
    $missingExpressions = $expressions | Where-Object { 
        !$expressionStats[$_]["eyes"] -and !$expressionStats[$_]["mouth"] 
    }
    
    if ($missingExpressions.Count -gt 0) {
        Write-Host "  [○] 未配置表情: $($missingExpressions -join ', ')" -ForegroundColor DarkGray
        Write-Host "      建议: 可选配置，根据需要添加" -ForegroundColor DarkGray
    }
    
    Write-Host ""
}

# 总结
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  验证完成" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "下一步:" -ForegroundColor Yellow
Write-Host "  1. 如果缺少 base_body.png，请先创建" -ForegroundColor White
Write-Host "  2. 补充缺失的表情部件（eyes/mouth）" -ForegroundColor White
Write-Host "  3. 在游戏中启用分层立绘系统（useLayeredPortrait = true）" -ForegroundColor White
Write-Host ""
