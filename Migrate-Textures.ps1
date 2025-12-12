# 纹理文件迁移脚本 - 将 base.png 改为 neutral.png

param(
    [Parameter(Mandatory=$false)]
    [string]$PersonaName = "Sideria",
    
    [Parameter(Mandatory=$false)]
    [switch]$Rename,  # 重命名模式（删除 base.png）
    
    [Parameter(Mandatory=$false)]
    [switch]$Copy     # 复制模式（保留 base.png）
)

# 颜色输出函数
function Write-ColorOutput {
    param(
        [string]$Message,
        [string]$Color = "White"
    )
    Write-Host $Message -ForegroundColor $Color
}

# 检查文件是否存在
function Test-FileExists {
    param([string]$Path)
    return Test-Path $Path
}

Write-ColorOutput "=== 纹理文件迁移脚本 v1.6.14 ===" "Cyan"
Write-ColorOutput ""

# 定义路径
$texturesDir = "Textures\UI\Narrators\9x16\$PersonaName"
$basePath = Join-Path $texturesDir "base.png"
$neutralPath = Join-Path $texturesDir "neutral.png"
$blinkPath = Join-Path $texturesDir "blink.png"
$speakingPath = Join-Path $texturesDir "speaking.png"

Write-ColorOutput "人格名称: $PersonaName" "Yellow"
Write-ColorOutput "纹理目录: $texturesDir" "Yellow"
Write-ColorOutput ""

# 检查目录是否存在
if (-not (Test-Path $texturesDir)) {
    Write-ColorOutput "? 错误: 纹理目录不存在: $texturesDir" "Red"
    Write-ColorOutput ""
    Write-ColorOutput "请确保以下目录存在:" "Yellow"
    Write-ColorOutput "  $texturesDir" "White"
    exit 1
}

Write-ColorOutput "? 纹理目录存在" "Green"
Write-ColorOutput ""

# 检查当前文件状态
Write-ColorOutput "=== 当前文件状态 ===" "Cyan"

$baseExists = Test-FileExists $basePath
$neutralExists = Test-FileExists $neutralPath
$blinkExists = Test-FileExists $blinkPath
$speakingExists = Test-FileExists $speakingPath

Write-ColorOutput "base.png:     $(if ($baseExists) { '? 存在' } else { '? 不存在' })" $(if ($baseExists) { "Green" } else { "Red" })
Write-ColorOutput "neutral.png:  $(if ($neutralExists) { '? 存在' } else { '? 不存在' })" $(if ($neutralExists) { "Green" } else { "Red" })
Write-ColorOutput "blink.png:    $(if ($blinkExists) { '? 存在' } else { '? 不存在（可选）' })" $(if ($blinkExists) { "Green" } else { "Yellow" })
Write-ColorOutput "speaking.png: $(if ($speakingExists) { '? 存在' } else { '? 不存在（可选）' })" $(if ($speakingExists) { "Green" } else { "Yellow" })
Write-ColorOutput ""

# 如果 neutral.png 已存在
if ($neutralExists) {
    Write-ColorOutput "? neutral.png 已存在，无需迁移" "Green"
    
    if ($baseExists) {
        Write-ColorOutput ""
        Write-ColorOutput "? 检测到 base.png 也存在" "Yellow"
        Write-ColorOutput ""
        Write-ColorOutput "建议操作:" "Cyan"
        Write-ColorOutput "1. 如果 neutral.png 是最新的，可以删除 base.png" "White"
        Write-ColorOutput "2. 如果 base.png 是最新的，可以重新运行脚本使用 -Rename 参数" "White"
        Write-ColorOutput ""
        Write-ColorOutput "删除 base.png 命令:" "Yellow"
        Write-ColorOutput "  Remove-Item '$basePath'" "White"
    }
    
    exit 0
}

# 如果 base.png 不存在
if (-not $baseExists) {
    Write-ColorOutput "? 错误: base.png 不存在，无法迁移" "Red"
    Write-ColorOutput ""
    Write-ColorOutput "请确保以下文件存在:" "Yellow"
    Write-ColorOutput "  $basePath" "White"
    Write-ColorOutput ""
    Write-ColorOutput "或者直接将你的纹理文件命名为 neutral.png" "Cyan"
    exit 1
}

# 决定操作模式
if (-not $Rename -and -not $Copy) {
    Write-ColorOutput "请选择操作模式:" "Cyan"
    Write-ColorOutput ""
    Write-ColorOutput "1. 重命名 (推荐) - 将 base.png 重命名为 neutral.png" "Green"
    Write-ColorOutput "   命令: .\Migrate-Textures.ps1 -Rename" "White"
    Write-ColorOutput ""
    Write-ColorOutput "2. 复制 - 保留 base.png，复制为 neutral.png" "Yellow"
    Write-ColorOutput "   命令: .\Migrate-Textures.ps1 -Copy" "White"
    Write-ColorOutput ""
    
    $choice = Read-Host "请输入选择 (1/2)"
    
    if ($choice -eq "1") {
        $Rename = $true
    } elseif ($choice -eq "2") {
        $Copy = $true
    } else {
        Write-ColorOutput "? 无效选择" "Red"
        exit 1
    }
}

Write-ColorOutput ""
Write-ColorOutput "=== 开始迁移 ===" "Cyan"

try {
    if ($Rename) {
        # 重命名模式
        Write-ColorOutput "模式: 重命名" "Yellow"
        Write-ColorOutput "操作: $basePath → $neutralPath" "White"
        
        Rename-Item -Path $basePath -NewName "neutral.png" -ErrorAction Stop
        
        Write-ColorOutput "? 重命名成功" "Green"
    }
    elseif ($Copy) {
        # 复制模式
        Write-ColorOutput "模式: 复制" "Yellow"
        Write-ColorOutput "操作: $basePath → $neutralPath (保留原文件)" "White"
        
        Copy-Item -Path $basePath -Destination $neutralPath -ErrorAction Stop
        
        Write-ColorOutput "? 复制成功" "Green"
        Write-ColorOutput ""
        Write-ColorOutput "? base.png 仍然存在，如不需要可手动删除:" "Yellow"
        Write-ColorOutput "  Remove-Item '$basePath'" "White"
    }
    
    Write-ColorOutput ""
    Write-ColorOutput "=== 迁移完成 ===" "Cyan"
    Write-ColorOutput ""
    
    # 验证结果
    Write-ColorOutput "=== 验证文件 ===" "Cyan"
    
    $neutralExistsNow = Test-FileExists $neutralPath
    $baseExistsNow = Test-FileExists $basePath
    
    Write-ColorOutput "neutral.png: $(if ($neutralExistsNow) { '? 存在' } else { '? 不存在' })" $(if ($neutralExistsNow) { "Green" } else { "Red" })
    Write-ColorOutput "base.png:    $(if ($baseExistsNow) { '? 存在' } else { '? 已删除' })" $(if ($baseExistsNow) { "Yellow" } else { "Green" })
    
    Write-ColorOutput ""
    Write-ColorOutput "=== 下一步 ===" "Cyan"
    Write-ColorOutput ""
    Write-ColorOutput "1. 确认 Defs/NarratorPersonaDefs.xml 中的配置:" "White"
    Write-ColorOutput "   <portraitPath>UI/Narrators/9x16/$PersonaName/neutral</portraitPath>" "Yellow"
    Write-ColorOutput ""
    Write-ColorOutput "2. 准备其他动画纹理（可选）:" "White"
    Write-ColorOutput "   - blink.png (闭眼)" "Yellow"
    Write-ColorOutput "   - speaking.png (说话)" "Yellow"
    Write-ColorOutput ""
    Write-ColorOutput "3. 编译并测试游戏" "White"
    Write-ColorOutput ""
    
}
catch {
    Write-ColorOutput ""
    Write-ColorOutput "? 错误: $_" "Red"
    exit 1
}

Write-ColorOutput "? 所有操作完成！" "Green"
