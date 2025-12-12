# 动画系统部署脚本 v1.6.15
# 一键部署整图切换动画系统
# ? 增强：保护用户自定义人格数据和立绘
# ? 新增：Materials 文件夹部署

param(
    [Parameter(Mandatory=$false)]
    [string]$RimWorldPath = "D:\steam\steamapps\common\RimWorld",
    
    [Parameter(Mandatory=$false)]
    [string]$Configuration = "Release",
    
    [Parameter(Mandatory=$false)]
    [switch]$SkipBuild,
    
    [Parameter(Mandatory=$false)]
    [switch]$OpenGameAfter,
    
    # ? 新增：保护模式（默认开启）
    [Parameter(Mandatory=$false)]
    [switch]$ProtectUserData = $true
)

# 颜色输出函数
function Write-ColorOutput {
    param(
        [string]$Message,
        [string]$Color = "White"
    )
    Write-Host $Message -ForegroundColor $Color
}

function Write-Success { param([string]$Message) Write-ColorOutput "? $Message" "Green" }
function Write-Error { param([string]$Message) Write-ColorOutput "? $Message" "Red" }
function Write-Warning { param([string]$Message) Write-ColorOutput "? $Message" "Yellow" }
function Write-Info { param([string]$Message) Write-ColorOutput "? $Message" "Cyan" }

Write-ColorOutput "`n=== 动画系统部署脚本 v1.6.15 ===`n" "Cyan"

# 定义路径
$ProjectRoot = Get-Location
$ModPath = Join-Path $RimWorldPath "Mods\TheSecondSeat"
$SourceDir = "Source\TheSecondSeat"
$ProjectFile = Join-Path $SourceDir "TheSecondSeat.csproj"

Write-Info "项目根目录: $ProjectRoot"
Write-Info "RimWorld 路径: $RimWorldPath"
Write-Info "Mod 路径: $ModPath"
Write-Info "配置: $Configuration"
Write-Host ""

# ===========================
# 步骤 1/6: 检查环境
# ===========================

Write-ColorOutput "=== 步骤 1/6: 检查环境 ===" "Cyan"

# 检查 RimWorld 路径
if (-not (Test-Path $RimWorldPath)) {
    Write-Error "RimWorld 路径不存在: $RimWorldPath"
    Write-Warning "请使用 -RimWorldPath 参数指定正确路径"
    exit 1
}
Write-Success "RimWorld 路径存在"

# 检查项目文件
if (-not (Test-Path $ProjectFile)) {
    Write-Error "项目文件不存在: $ProjectFile"
    exit 1
}
Write-Success "项目文件存在"

# 检查 Mod 目录
if (-not (Test-Path $ModPath)) {
    Write-Warning "Mod 目录不存在，将创建: $ModPath"
    New-Item -ItemType Directory -Path $ModPath -Force | Out-Null
    Write-Success "Mod 目录已创建"
} else {
    Write-Success "Mod 目录存在"
}

Write-Host ""

# ===========================
# 步骤 2/6: 编译项目
# ===========================

Write-ColorOutput "=== 步骤 2/6: 编译项目 ===" "Cyan"

if ($SkipBuild) {
    Write-Warning "跳过编译（-SkipBuild）"
} else {
    Write-Info "开始编译..."
    
    $buildOutput = dotnet build $ProjectFile --configuration $Configuration 2>&1
    
    if ($LASTEXITCODE -ne 0) {
        Write-Error "编译失败！"
        Write-Host $buildOutput
        exit 1
    }
    
    Write-Success "编译成功"
}

Write-Host ""

# ===========================
# 步骤 3/6: 复制 DLL
# ===========================

Write-ColorOutput "=== 步骤 3/6: 复制 DLL ===" "Cyan"

$dllSource = Join-Path $SourceDir "bin\$Configuration\net472\TheSecondSeat.dll"
$dllDest = Join-Path $ModPath "Assemblies"

if (-not (Test-Path $dllSource)) {
    Write-Error "DLL 文件不存在: $dllSource"
    exit 1
}

# 创建 Assemblies 目录
if (-not (Test-Path $dllDest)) {
    New-Item -ItemType Directory -Path $dllDest -Force | Out-Null
}

# 复制 DLL
Copy-Item $dllSource $dllDest -Force
Write-Success "DLL 已复制到: $dllDest"

# 检查文件大小
$dllSize = (Get-Item (Join-Path $dllDest "TheSecondSeat.dll")).Length / 1KB
Write-Info "DLL 大小: $($dllSize.ToString('F2')) KB"

Write-Host ""

# ===========================
# 步骤 4/6: 复制 Defs（保护模式）
# ===========================

Write-ColorOutput "=== 步骤 4/6: 复制 Defs ===" "Cyan"

$defsSource = "Defs"
$defsDest = Join-Path $ModPath "Defs"

if (Test-Path $defsSource) {
    # ? 保护用户自定义人格数据
    $protectedFiles = @(
        "NarratorPersonaDefs\CustomPersona_*.xml"
    )
    
    # ? 备份保护的文件
    $backupDir = Join-Path $env:TEMP "TheSecondSeat_Backup_$(Get-Date -Format 'yyyyMMdd_HHmmss')"
    if ($ProtectUserData -and (Test-Path $defsDest)) {
        Write-Info "保护用户自定义数据..."
        
        foreach ($pattern in $protectedFiles) {
            $files = Get-ChildItem (Join-Path $defsDest $pattern) -ErrorAction SilentlyContinue
            
            if ($files) {
                New-Item -ItemType Directory -Path $backupDir -Force | Out-Null
                
                foreach ($file in $files) {
                    $relativePath = $file.FullName.Substring($defsDest.Length + 1)
                    $backupFile = Join-Path $backupDir $relativePath
                    $backupFileDir = Split-Path $backupFile -Parent
                    
                    New-Item -ItemType Directory -Path $backupFileDir -Force | Out-Null
                    Copy-Item $file.FullName $backupFile -Force
                    
                    Write-Success "已备份: $relativePath"
                }
            }
        }
    }
    
    # ? 清理旧的 Defs 文件（排除受保护的）
    if (Test-Path $defsDest) {
        Write-Info "清理旧的 Defs 文件（保留用户自定义数据）..."
        
        # 获取所有受保护的文件
        $protectedPaths = @()
        foreach ($pattern in $protectedFiles) {
            $files = Get-ChildItem (Join-Path $defsDest $pattern) -ErrorAction SilentlyContinue
            if ($files) {
                $protectedPaths += $files | ForEach-Object { $_.FullName }
            }
        }
        
        # 删除非受保护的文件
        Get-ChildItem $defsDest -Recurse -File | ForEach-Object {
            if ($protectedPaths -notcontains $_.FullName) {
                Remove-Item $_.FullName -Force
            }
        }
        
        # 清理空目录
        Get-ChildItem $defsDest -Recurse -Directory | 
            Where-Object { (Get-ChildItem $_.FullName).Count -eq 0 } | 
            Remove-Item -Force -ErrorAction SilentlyContinue
    }
    
    # 创建 Defs 目录
    if (-not (Test-Path $defsDest)) {
        New-Item -ItemType Directory -Path $defsDest -Force | Out-Null
    }
    
    # ? 复制新文件（跳过受保护的）
    $xmlFiles = Get-ChildItem $defsSource -Filter "*.xml" -Recurse
    $copiedCount = 0
    $skippedCount = 0
    
    foreach ($file in $xmlFiles) {
        # 计算相对路径
        $relativePath = $file.FullName.Substring((Resolve-Path $defsSource).Path.Length + 1)
        $destFile = Join-Path $defsDest $relativePath
        
        # ? 检查是否是受保护的文件
        $isProtected = $false
        foreach ($pattern in $protectedFiles) {
            if ($relativePath -like $pattern) {
                $isProtected = $true
                break
            }
        }
        
        if ($isProtected -and (Test-Path $destFile)) {
            Write-Warning "跳过受保护的文件: $relativePath"
            $skippedCount++
            continue
        }
        
        # 创建子目录（如果需要）
        $destDir = Split-Path $destFile -Parent
        if (-not (Test-Path $destDir)) {
            New-Item -ItemType Directory -Path $destDir -Force | Out-Null
        }
        
        # ? 验证路径合法性（避免异常路径）        
        if ($destFile -match "\\ers\\|\\Administrator\\|\\Desktop\\") {
            Write-Warning "检测到异常路径，跳过: $destFile"
            continue
        }
        
        # 复制文件
        Copy-Item $file.FullName $destFile -Force
        $copiedCount++
    }
    
    Write-Success "Defs 已复制: $copiedCount 个文件"
    if ($skippedCount -gt 0) {
        Write-Info "跳过受保护的文件: $skippedCount 个"
    }
    
    # ? 恢复备份的文件
    if ($ProtectUserData -and (Test-Path $backupDir)) {
        Write-Info "恢复用户自定义数据..."
        
        Get-ChildItem $backupDir -Recurse -File | ForEach-Object {
            $relativePath = $_.FullName.Substring($backupDir.Length + 1)
            $destFile = Join-Path $defsDest $relativePath
            
            Copy-Item $_.FullName $destFile -Force
            Write-Success "已恢复: $relativePath"
        }
        
        # 清理临时备份
        Remove-Item $backupDir -Recurse -Force
    }
} else {
    Write-Warning "Defs 目录不存在"
}

Write-Host ""

# ===========================
# 步骤 5/6: 复制纹理文件（保护立绘）
# ===========================

Write-ColorOutput "=== 步骤 5/6: 复制纹理文件 ===" "Cyan"

$texturesSource = "Textures"
$texturesDest = Join-Path $ModPath "Textures"

if (Test-Path $texturesSource) {
    # ? 保护用户自定义立绘
    $protectedTexturePatterns = @(
        "UI\Narrators\Avatars\*\custom_*.*",
        "UI\Narrators\Avatars\CustomPersona_*\*.*"
    )
    
    # ? 备份保护的立绘
    $textureBackupDir = Join-Path $env:TEMP "TheSecondSeat_Textures_Backup_$(Get-Date -Format 'yyyyMMdd_HHmmss')"
    if ($ProtectUserData -and (Test-Path $texturesDest)) {
        Write-Info "保护用户自定义立绘..."
        
        foreach ($pattern in $protectedTexturePatterns) {
            $files = Get-ChildItem (Join-Path $texturesDest $pattern) -ErrorAction SilentlyContinue
            
            if ($files) {
                New-Item -ItemType Directory -Path $textureBackupDir -Force | Out-Null
                
                foreach ($file in $files) {
                    $relativePath = $file.FullName.Substring($texturesDest.Length + 1)
                    $backupFile = Join-Path $textureBackupDir $relativePath
                    $backupFileDir = Split-Path $backupFile -Parent
                    
                    New-Item -ItemType Directory -Path $backupFileDir -Force | Out-Null
                    Copy-Item $file.FullName $backupFile -Force
                    
                    Write-Success "已备份立绘: $relativePath"
                }
            }
        }
    }
    
    # 创建 Textures 目录
    if (-not (Test-Path $texturesDest)) {
        New-Item -ItemType Directory -Path $texturesDest -Force | Out-Null
    }
    
    # 复制所有纹理文件
    $textureFiles = Get-ChildItem $texturesSource -Include "*.png","*.jpg" -Recurse
    $copiedTextureCount = 0
    $skippedTextureCount = 0
    
    foreach ($file in $textureFiles) {
        $relativePath = $file.FullName.Substring($texturesSource.Length + 1)
        $destFile = Join-Path $texturesDest $relativePath
        
        # ? 检查是否是受保护的立绘
        $isProtected = $false
        foreach ($pattern in $protectedTexturePatterns) {
            if ($relativePath -like $pattern) {
                $isProtected = $true
                break
            }
        }
        
        if ($isProtected -and (Test-Path $destFile)) {
            Write-Warning "跳过受保护的立绘: $relativePath"
            $skippedTextureCount++
            continue
        }
        
        # 创建子目录
        $destDir = Split-Path $destFile -Parent
        if (-not (Test-Path $destDir)) {
            New-Item -ItemType Directory -Path $destDir -Force | Out-Null
        }
        
        # 复制文件
        Copy-Item $file.FullName $destFile -Force
        $copiedTextureCount++
    }
    
    Write-Success "纹理已复制: $copiedTextureCount 个文件"
    if ($skippedTextureCount -gt 0) {
        Write-Info "跳过受保护的立绘: $skippedTextureCount 个"
    }
    
    # ? 恢复备份的立绘
    if ($ProtectUserData -and (Test-Path $textureBackupDir)) {
        Write-Info "恢复用户自定义立绘..."
        
        Get-ChildItem $textureBackupDir -Recurse -File | ForEach-Object {
            $relativePath = $_.FullName.Substring($textureBackupDir.Length + 1)
            $destFile = Join-Path $texturesDest $relativePath
            
            Copy-Item $_.FullName $destFile -Force
            Write-Success "已恢复立绘: $relativePath"
        }
        
        # 清理临时备份
        Remove-Item $textureBackupDir -Recurse -Force
    }
} else {
    Write-Warning "Textures 目录不存在"
}

Write-Host ""

# ===========================
# 步骤 6: 复制 Materials 文件
# ===========================

Write-ColorOutput "=== 步骤 6/6: 复制 Materials 文件 ===" "Cyan"

$materialsSource = "Materials"
$materialsDest = Join-Path $ModPath "Materials"

if (Test-Path $materialsSource) {
    # 创建 Materials 目录
    if (-not (Test-Path $materialsDest)) {
        New-Item -ItemType Directory -Path $materialsDest -Force | Out-Null
    }
    
    # 复制所有材质文件
    $materialFiles = Get-ChildItem $materialsSource -Include "*.xml" -Recurse
    $copiedMaterialCount = 0
    
    foreach ($file in $materialFiles) {
        $relativePath = $file.FullName.Substring($materialsSource.Length + 1)
        $destFile = Join-Path $materialsDest $relativePath
        
        # 创建子目录
        $destDir = Split-Path $destFile -Parent
        if (-not (Test-Path $destDir)) {
            New-Item -ItemType Directory -Path $destDir -Force | Out-Null
        }
        
        # 复制文件
        Copy-Item $file.FullName $destFile -Force
        $copiedMaterialCount++
    }
    
    if ($copiedMaterialCount -gt 0) {
        Write-Success "Materials 已复制: $copiedMaterialCount 个文件"
    } else {
        Write-Warning "Materials 目录为空"
    }
} else {
    Write-Warning "Materials 目录不存在"
}

Write-Host ""

# ===========================
# 验证部署
# ===========================

Write-ColorOutput "=== 验证部署 ===" "Cyan"

# 检查关键文件
$checkFiles = @(
    @{Path = "Assemblies\TheSecondSeat.dll"; Required = $true},
    @{Path = "Defs\NarratorPersonaDefs.xml"; Required = $true},
    @{Path = "Textures\UI\Narrators\Avatars\Sideria\base.png"; Required = $true},
    @{Path = "Textures\UI\Narrators\Avatars\Sideria\blink.png"; Required = $false},
    @{Path = "Textures\UI\Narrators\Avatars\Sideria\speaking.png"; Required = $false}
)

$missingRequired = @()
$missingOptional = @()

foreach ($file in $checkFiles) {
    $fullPath = Join-Path $ModPath $file.Path
    
    if (Test-Path $fullPath) {
        Write-Success "存在: $($file.Path)"
    } else {
        if ($file.Required) {
            Write-Error "缺失（必需）: $($file.Path)"
            $missingRequired += $file.Path
        } else {
            Write-Warning "缺失（可选）: $($file.Path)"
            $missingOptional += $file.Path
        }
    }
}

Write-Host ""

# ===========================
# 部署总结
# ===========================

Write-ColorOutput "=== 部署总结 ===" "Cyan"

if ($missingRequired.Count -eq 0) {
    Write-Success "部署完成！所有必需文件已就位"
    
    if ($missingOptional.Count -gt 0) {
        Write-Host ""
        Write-Warning "可选文件缺失："
        foreach ($file in $missingOptional) {
            Write-Host "  - $file" -ForegroundColor Yellow
        }
        Write-Host ""
        Write-Info "提示: 添加这些文件可以启用完整的动画效果"
    }
    
    Write-Host ""
    Write-ColorOutput "=== 下一步 ===" "Cyan"
    Write-Host ""
    Write-Info "1. 启动 RimWorld"
    Write-Info "2. 确认 Mod 已加载"
    Write-Info "3. 进入游戏测试"
    Write-Host ""
    Write-Info "测试要点："
    Write-Host "  - AI 按钮是否显示 base.png" -ForegroundColor White
    Write-Host "  - 是否有眨眼动画（3-6秒间隔）" -ForegroundColor White
    Write-Host "  - TTS 播放时是否显示说话动画" -ForegroundColor White
    Write-Host ""
    
    if ($OpenGameAfter) {
        Write-Info "正在启动 RimWorld..."
        $rimworldExe = Join-Path $RimWorldPath "RimWorldWin64.exe"
        if (Test-Path $rimworldExe) {
            Start-Process $rimworldExe
            Write-Success "RimWorld 已启动"
        } else {
            Write-Warning "找不到 RimWorld 可执行文件"
        }
    } else {
        Write-Info "使用 -OpenGameAfter 参数可自动启动游戏"
    }
    
} else {
    Write-Error "部署失败！缺少必需文件："
    foreach ($file in $missingRequired) {
        Write-Host "  - $file" -ForegroundColor Red
    }
    exit 1
}

Write-Host ""
Write-ColorOutput "=== 部署完成 ===" "Green"
Write-Host ""
