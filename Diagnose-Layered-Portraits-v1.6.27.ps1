# 分层立绘诊断脚本 v1.6.27
# 检查立绘文件夹结构和分层立绘配置

Write-Host "========== 分层立绘诊断 v1.6.27 ==========" -ForegroundColor Cyan
Write-Host ""

# 定义路径
$modRoot = "D:\steam\steamapps\common\RimWorld\Mods\TheSecondSeat"
$texturesRoot = Join-Path $modRoot "Textures\UI\Narrators"
$layeredRoot = Join-Path $texturesRoot "9x16\Layered"
$expressionsRoot = Join-Path $texturesRoot "9x16\Expressions"

# 检查基础路径
Write-Host "?? 检查基础路径..." -ForegroundColor Yellow
Write-Host "Mod根目录: $modRoot"
Write-Host "纹理根目录: $texturesRoot"
Write-Host "分层立绘目录: $layeredRoot"
Write-Host "表情目录: $expressionsRoot"
Write-Host ""

if (!(Test-Path $modRoot)) {
    Write-Host "? Mod根目录不存在！" -ForegroundColor Red
    exit 1
}

if (!(Test-Path $texturesRoot)) {
    Write-Host "? 纹理根目录不存在！" -ForegroundColor Red
    exit 1
}

# 检查分层立绘文件夹
Write-Host "?? 检查分层立绘文件夹结构..." -ForegroundColor Yellow
Write-Host ""

if (!(Test-Path $layeredRoot)) {
    Write-Host "? 分层立绘目录不存在: $layeredRoot" -ForegroundColor Red
    Write-Host "  需要创建以下结构:" -ForegroundColor Yellow
    Write-Host "  Textures/UI/Narrators/9x16/Layered/{PersonaName}/" -ForegroundColor Gray
    Write-Host "    ├── background.png (可选)" -ForegroundColor Gray
    Write-Host "    ├── body.png" -ForegroundColor Gray
    Write-Host "    ├── hair_back.png (可选)" -ForegroundColor Gray
    Write-Host "    ├── neutral_eyes.png" -ForegroundColor Gray
    Write-Host "    ├── neutral_mouth.png" -ForegroundColor Gray
    Write-Host "    ├── happy_eyes.png" -ForegroundColor Gray
    Write-Host "    ├── happy_mouth.png" -ForegroundColor Gray
    Write-Host "    └── hair.png" -ForegroundColor Gray
    Write-Host ""
} else {
    Write-Host "? 分层立绘目录存在" -ForegroundColor Green
    
    # 列出所有人格文件夹
    $personaFolders = Get-ChildItem -Path $layeredRoot -Directory
    
    if ($personaFolders.Count -eq 0) {
        Write-Host "??  警告: 分层立绘目录为空，没有任何人格文件夹" -ForegroundColor Yellow
    } else {
        Write-Host "?? 找到 $($personaFolders.Count) 个人格文件夹:" -ForegroundColor Cyan
        Write-Host ""
        
        foreach ($persona in $personaFolders) {
            Write-Host "  ├── $($persona.Name)" -ForegroundColor White
            
            # 检查必需的图层文件
            $layerFiles = @(
                @{Name="body.png"; Required=$true},
                @{Name="neutral_eyes.png"; Required=$true},
                @{Name="neutral_mouth.png"; Required=$true},
                @{Name="hair.png"; Required=$false},
                @{Name="background.png"; Required=$false},
                @{Name="hair_back.png"; Required=$false},
                @{Name="happy_eyes.png"; Required=$false},
                @{Name="happy_mouth.png"; Required=$false},
                @{Name="sad_eyes.png"; Required=$false},
                @{Name="sad_mouth.png"; Required=$false},
                @{Name="angry_eyes.png"; Required=$false},
                @{Name="angry_mouth.png"; Required=$false}
            )
            
            $missingRequired = @()
            $foundOptional = @()
            
            foreach ($layer in $layerFiles) {
                $layerPath = Join-Path $persona.FullName $layer.Name
                
                if (Test-Path $layerPath) {
                    $fileInfo = Get-Item $layerPath
                    $sizeKB = [math]::Round($fileInfo.Length / 1KB, 2)
                    
                    if ($layer.Required) {
                        Write-Host "    │  ? $($layer.Name) ($sizeKB KB)" -ForegroundColor Green
                    } else {
                        Write-Host "    │  ? $($layer.Name) ($sizeKB KB)" -ForegroundColor Gray
                        $foundOptional += $layer.Name
                    }
                } else {
                    if ($layer.Required) {
                        Write-Host "    │  ? $($layer.Name) (必需)" -ForegroundColor Red
                        $missingRequired += $layer.Name
                    }
                }
            }
            
            Write-Host "    │" -ForegroundColor Gray
            
            if ($missingRequired.Count -gt 0) {
                Write-Host "    └── ? 缺少必需图层: $($missingRequired -join ', ')" -ForegroundColor Red
            } elseif ($foundOptional.Count -eq 0) {
                Write-Host "    └── ??  只有必需图层，建议添加表情变体" -ForegroundColor Yellow
            } else {
                Write-Host "    └── ? 配置完整，包含 $($foundOptional.Count) 个可选图层" -ForegroundColor Green
            }
            
            Write-Host ""
        }
    }
}

# 检查表情文件夹（兼容旧模式）
Write-Host "?? 检查表情文件夹结构..." -ForegroundColor Yellow
Write-Host ""

if (!(Test-Path $expressionsRoot)) {
    Write-Host "??  表情目录不存在（仅分层立绘模式需要）" -ForegroundColor Yellow
    Write-Host "  路径: $expressionsRoot" -ForegroundColor Gray
} else {
    Write-Host "? 表情目录存在" -ForegroundColor Green
    
    $expressionFolders = Get-ChildItem -Path $expressionsRoot -Directory
    
    if ($expressionFolders.Count -eq 0) {
        Write-Host "  (空)" -ForegroundColor Gray
    } else {
        Write-Host "  找到 $($expressionFolders.Count) 个人格表情文件夹" -ForegroundColor Gray
        
        foreach ($folder in $expressionFolders) {
            $fileCount = (Get-ChildItem -Path $folder.FullName -Filter "*.png").Count
            Write-Host "    - $($folder.Name): $fileCount 个表情文件" -ForegroundColor Gray
        }
    }
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan

# 检查 NarratorPersonaDef 配置
Write-Host ""
Write-Host "?? 检查人格定义配置..." -ForegroundColor Yellow
Write-Host ""

$defsPath = Join-Path $modRoot "Defs\NarratorPersonaDefs.xml"

if (!(Test-Path $defsPath)) {
    Write-Host "? 人格定义文件不存在: $defsPath" -ForegroundColor Red
} else {
    Write-Host "? 人格定义文件存在" -ForegroundColor Green
    
    # 读取XML并检查配置
    try {
        [xml]$xml = Get-Content $defsPath -Encoding UTF8
        $personas = $xml.Defs.TheSecondSeat.PersonaGeneration.NarratorPersonaDef
        
        if ($personas) {
            Write-Host "  找到 $($personas.Count) 个人格定义" -ForegroundColor Cyan
            Write-Host ""
            
            foreach ($persona in $personas) {
                $defName = $persona.defName
                $useLayered = $persona.useLayeredPortrait
                
                Write-Host "  ?? $defName" -ForegroundColor White
                Write-Host "    useLayeredPortrait: $useLayered" -ForegroundColor Gray
                
                if ($useLayered -eq "true") {
                    # 检查对应的文件夹是否存在
                    $layeredFolder = Join-Path $layeredRoot $defName
                    
                    if (Test-Path $layeredFolder) {
                        Write-Host "    ? 分层立绘文件夹存在" -ForegroundColor Green
                    } else {
                        Write-Host "    ? 分层立绘文件夹不存在: $layeredFolder" -ForegroundColor Red
                    }
                } else {
                    Write-Host "    ??  未启用分层立绘模式" -ForegroundColor Yellow
                }
                
                Write-Host ""
            }
        } else {
            Write-Host "  ??  未找到任何人格定义" -ForegroundColor Yellow
        }
    } catch {
        Write-Host "  ? 解析XML失败: $($_.Exception.Message)" -ForegroundColor Red
    }
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# 提供修复建议
Write-Host "?? 修复建议:" -ForegroundColor Cyan
Write-Host ""
Write-Host "1. 如果想使用分层立绘模式:" -ForegroundColor Yellow
Write-Host "   - 创建 Textures/UI/Narrators/9x16/Layered/{PersonaName}/ 文件夹" -ForegroundColor Gray
Write-Host "   - 放置必需图层: body.png, neutral_eyes.png, neutral_mouth.png" -ForegroundColor Gray
Write-Host "   - 在 NarratorPersonaDefs.xml 中设置 <useLayeredPortrait>true</useLayeredPortrait>" -ForegroundColor Gray
Write-Host ""

Write-Host "2. 如果想使用传统整图模式:" -ForegroundColor Yellow
Write-Host "   - 放置完整立绘到 Textures/UI/Narrators/9x16/{PersonaName}/base.png" -ForegroundColor Gray
Write-Host "   - 表情文件放到 Textures/UI/Narrators/9x16/Expressions/{PersonaName}/" -ForegroundColor Gray
Write-Host "   - 在 NarratorPersonaDefs.xml 中设置 <useLayeredPortrait>false</useLayeredPortrait>" -ForegroundColor Gray
Write-Host ""

Write-Host "3. 检查完成后，重启游戏以加载新纹理" -ForegroundColor Yellow
Write-Host ""

Write-Host "========== 诊断完成 ==========" -ForegroundColor Cyan
