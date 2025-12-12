# 简化版UI Emoji修复脚本
# 直接替换，不使用复杂正则

param(
    [string]$ProjectRoot = "C:\Users\Administrator\Desktop\rim mod\The Second Seat",
    [switch]$Preview = $false
)

$sourceDir = Join-Path $ProjectRoot "Source\TheSecondSeat"

if (-not (Test-Path $sourceDir)) {
    Write-Host "[错误] 源代码目录不存在: $sourceDir" -ForegroundColor Red
    exit 1
}

Write-Host "=========================================" -ForegroundColor Cyan
Write-Host "  简化版UI文本修复工具" -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor White
Write-Host ""

if ($Preview) {
    Write-Host "[模式] 预览模式" -ForegroundColor Yellow
} else {
    Write-Host "[模式] 修复模式" -ForegroundColor Green
    
    # 创建备份
    $timestamp = Get-Date -Format 'yyyyMMdd_HHmmss'
    $backupDir = Join-Path $ProjectRoot "Backup_UIFix_$timestamp"
    Copy-Item -Path $sourceDir -Destination $backupDir -Recurse -Force
    Write-Host "[备份] 完成: $backupDir" -ForegroundColor Green
}

Write-Host ""

# 简单的字符串替换列表
$replacements = @{
    # Emoji
    "?" = "[OK]"
    "?" = "[OK]"
    "?" = "[X]"
    "?" = "[FAIL]"
    "??" = "[!]"
    "?" = "[!]"
    "●" = "*"
    "?" = "*"
    "○" = "o"
    "★" = "*"
    "☆" = "*"
    "→" = "->"
    "←" = "<-"
    "↓" = "v"
    "↑" = "^"
    
    # 装饰性emoji（移除）
    "??" = ""
    "??" = ""
    "??" = ""
    
    # 中文特殊符号（在代码注释中保留，只在字符串中替换）
    # 暂时不处理，避免误伤注释
}

$csFiles = Get-ChildItem -Path $sourceDir -Filter "*.cs" -Recurse
$filesFixed = 0
$totalReplacements = 0

foreach ($file in $csFiles) {
    try {
        $content = Get-Content -Path $file.FullName -Raw -Encoding UTF8
        $originalContent = $content
        $fileReplacements = 0
        
        # 应用所有替换
        foreach ($key in $replacements.Keys) {
            $value = $replacements[$key]
            if ($content.Contains($key)) {
                $count = ($content.ToCharArray() | Where-Object { $_ -eq $key }).Count
                $content = $content.Replace($key, $value)
                $fileReplacements += $count
            }
        }
        
        # 如果有变化
        if ($content -ne $originalContent) {
            $filesFixed++
            $totalReplacements += $fileReplacements
            
            $relativePath = $file.FullName.Substring($sourceDir.Length + 1)
            Write-Host "[修复] $relativePath ($fileReplacements 处)" -ForegroundColor Yellow
            
            if (-not $Preview) {
                # 保存为UTF-8 BOM
                $utf8Bom = New-Object System.Text.UTF8Encoding $true
                [System.IO.File]::WriteAllText($file.FullName, $content, $utf8Bom)
                Write-Host "  -> 已保存" -ForegroundColor Green
            }
        }
    }
    catch {
        Write-Host "[警告] 跳过文件: $($file.Name) - $($_.Exception.Message)" -ForegroundColor Yellow
    }
}

Write-Host ""
Write-Host "=========================================" -ForegroundColor Cyan
Write-Host "  修复完成" -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor White
Write-Host "扫描文件数: $($csFiles.Count)" -ForegroundColor White
Write-Host "修复文件数: $filesFixed" -ForegroundColor $(if ($filesFixed -eq 0) { "Green" } else { "Yellow" })
Write-Host "替换次数: $totalReplacements" -ForegroundColor $(if ($totalReplacements -eq 0) { "Green" } else { "Yellow" })

if ($Preview) {
    Write-Host ""
    Write-Host "[提示] 预览模式，未进行任何修改" -ForegroundColor Yellow
    Write-Host "[提示] 要执行修复，请运行: .\Fix-UI-Emoji-Simple.ps1" -ForegroundColor Cyan
} else {
    if ($filesFixed -gt 0) {
        Write-Host ""
        Write-Host "[成功] 所有问题已修复" -ForegroundColor Green
        Write-Host "[备份] 原始文件已备份到: $backupDir" -ForegroundColor Cyan
    }
}

Write-Host ""
Write-Host "=========================================" -ForegroundColor Cyan

exit $(if ($filesFixed -eq 0) { 0 } else { $filesFixed })
