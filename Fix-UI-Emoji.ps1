# UI Emoji和乱码自动修复脚本
# 功能：扫描并修复所有.cs文件中的emoji和编码问题

param(
    [string]$ProjectRoot = "C:\Users\Administrator\Desktop\rim mod\The Second Seat",
    [switch]$Preview = $false  # 预览模式：只显示问题，不修复
)

# 颜色输出函数
function Write-ColorOutput($ForegroundColor, $Message) {
    Write-Host $Message -ForegroundColor $ForegroundColor
}

# 备份函数
function Backup-Source {
    param([string]$SourceDir, [string]$BackupDir)
    
    if (Test-Path $BackupDir) {
        Write-ColorOutput Yellow "[警告] 备份目录已存在，跳过备份"
        return $false
    }
    
    Write-ColorOutput Cyan "[备份] 正在备份源代码..."
    Copy-Item -Path $SourceDir -Destination $BackupDir -Recurse -Force
    Write-ColorOutput Green "[备份] 完成: $BackupDir"
    return $true
}

# 主逻辑
$sourceDir = Join-Path $ProjectRoot "Source\TheSecondSeat"

if (-not (Test-Path $sourceDir)) {
    Write-ColorOutput Red "[错误] 源代码目录不存在: $sourceDir"
    exit 1
}

Write-ColorOutput Cyan "========================================="
Write-ColorOutput Cyan "  UI文本Emoji和乱码修复工具"
Write-ColorOutput Cyan "========================================="
Write-ColorOutput White ""

if ($Preview) {
    Write-ColorOutput Yellow "[模式] 预览模式 - 只显示问题，不进行修复"
} else {
    Write-ColorOutput Green "[模式] 修复模式 - 将自动修复所有问题"
    
    # 创建备份
    $timestamp = Get-Date -Format 'yyyyMMdd_HHmmss'
    $backupDir = Join-Path $ProjectRoot "Backup_UIFix_$timestamp"
    Backup-Source -SourceDir $sourceDir -BackupDir $backupDir
}

Write-ColorOutput White ""

# 定义替换规则
$replaceRules = @(
    # Emoji替换
    @{ Pattern = "?"; Replacement = "[OK]"; Description = "成功标记" }
    @{ Pattern = "?"; Replacement = "[OK]"; Description = "成功勾选" }
    @{ Pattern = "?"; Replacement = "[X]"; Description = "失败叉号" }
    @{ Pattern = "?"; Replacement = "[FAIL]"; Description = "失败标记" }
    @{ Pattern = "??"; Replacement = "[!]"; Description = "警告emoji" }
    @{ Pattern = "?"; Replacement = "[!]"; Description = "警告符号" }
    @{ Pattern = "●"; Replacement = "*"; Description = "实心圆点" }
    @{ Pattern = "?"; Replacement = "*"; Description = "中圆点" }
    @{ Pattern = "○"; Replacement = "o"; Description = "空心圆点" }
    @{ Pattern = "★"; Replacement = "*"; Description = "实心星号" }
    @{ Pattern = "☆"; Replacement = "*"; Description = "空心星号" }
    @{ Pattern = "→"; Replacement = "->"; Description = "右箭头" }
    @{ Pattern = "←"; Replacement = "<-"; Description = "左箭头" }
    @{ Pattern = "↓"; Replacement = "v"; Description = "下箭头" }
    @{ Pattern = "↑"; Replacement = "^"; Description = "上箭头" }
    
    # 中文特殊符号
    @{ Pattern = "？"; Replacement = "?"; Description = "全角问号" }
    @{ Pattern = "！"; Replacement = "!"; Description = "全角感叹号" }
    @{ Pattern = "，"; Replacement = ","; Description = "全角逗号（代码中）" }
    @{ Pattern = "。"; Replacement = "."; Description = "全角句号（代码中）" }
    
    # 常见乱码箭头（折叠UI）
    @{ Pattern = "\?"; Replacement = ">"; Description = "乱码问号" }
    @{ Pattern = "??"; Replacement = "v"; Description = "乱码箭头" }
    
    # 移除装饰性emoji
    @{ Pattern = "??"; Replacement = ""; Description = "庆祝emoji" }
    @{ Pattern = "??"; Replacement = ""; Description = "剪贴板emoji" }
    @{ Pattern = "??"; Replacement = ""; Description = "扳手emoji" }
)

# 扫描所有.cs文件
$csFiles = Get-ChildItem -Path $sourceDir -Filter "*.cs" -Recurse
$totalFiles = $csFiles.Count
$filesWithIssues = 0
$totalReplacements = 0

Write-ColorOutput Cyan "[扫描] 正在检查 $totalFiles 个文件..."
Write-ColorOutput White ""

foreach ($file in $csFiles) {
    try {
        # 尝试以UTF-8读取
        $content = Get-Content -Path $file.FullName -Raw -Encoding UTF8 -ErrorAction Stop
        $originalContent = $content
        $fileReplacements = 0
        $fileIssues = @()
        
        # 应用所有替换规则
        foreach ($rule in $replaceRules) {
            $pattern = [regex]::Escape($rule.Pattern)
            $matches = [regex]::Matches($content, $pattern)
            
            if ($matches.Count -gt 0) {
                $fileIssues += "$($matches.Count)x $($rule.Description)"
                $content = $content -replace $pattern, $rule.Replacement
                $fileReplacements += $matches.Count
            }
        }
        
        # 检测编码问题（乱码）
        if ($content -match "????") {
            $fileIssues += "检测到编码乱码"
            $fileReplacements++
        }
        
        # 如果有变化
        if ($content -ne $originalContent) {
            $filesWithIssues++
            $totalReplacements += $fileReplacements
            
            $relativePath = $file.FullName.Substring($sourceDir.Length + 1)
            Write-ColorOutput Yellow "[问题] $relativePath"
            
            foreach ($issue in $fileIssues) {
                Write-ColorOutput Gray "  -> $issue"
            }
            
            # 如果不是预览模式，执行修复
            if (-not $Preview) {
                # 保存为UTF-8 BOM
                $utf8Bom = New-Object System.Text.UTF8Encoding $true
                [System.IO.File]::WriteAllText($file.FullName, $content, $utf8Bom)
                Write-ColorOutput Green "  [修复] 已保存"
            }
            
            Write-ColorOutput White ""
        }
    }
    catch {
        Write-ColorOutput Red "[错误] 无法处理文件: $($file.Name)"
        Write-ColorOutput Red "  原因: $($_.Exception.Message)"
        Write-ColorOutput White ""
    }
}

# 输出总结
Write-ColorOutput Cyan "========================================="
Write-ColorOutput Cyan "  修复完成"
Write-ColorOutput Cyan "========================================="
Write-ColorOutput White "扫描文件数: $totalFiles"
Write-ColorOutput $(if ($filesWithIssues -eq 0) { "Green" } else { "Yellow" }) "问题文件数: $filesWithIssues"
Write-ColorOutput $(if ($totalReplacements -eq 0) { "Green" } else { "Yellow" }) "替换次数: $totalReplacements"

if ($Preview) {
    Write-ColorOutput Yellow ""
    Write-ColorOutput Yellow "[提示] 这是预览模式，未进行任何修改"
    Write-ColorOutput Yellow "[提示] 要执行修复，请运行："
    Write-ColorOutput Cyan "  .\Fix-UI-Emoji.ps1"
} else {
    if ($filesWithIssues -gt 0) {
        Write-ColorOutput Green ""
        Write-ColorOutput Green "[成功] 所有问题已修复"
        Write-ColorOutput Cyan "[备份] 原始文件已备份到: $backupDir"
    } else {
        Write-ColorOutput Green ""
        Write-ColorOutput Green "[完美] 未发现任何问题！"
    }
}

Write-ColorOutput White ""
Write-ColorOutput Cyan "========================================="

# 返回状态码
if ($filesWithIssues -eq 0) {
    exit 0
} else {
    exit $filesWithIssues
}
