# ========================================
# 删除 PortraitLoader.cs 中的成功日志
# ========================================

$filePath = "Source\TheSecondSeat\PersonaGeneration\PortraitLoader.cs"

Write-Host "=== 删除成功日志 ===" -ForegroundColor Cyan

# 读取文件内容
$content = Get-Content $filePath -Raw -Encoding UTF8

# 统计删除前的日志数量
$beforeMessageCount = ([regex]::Matches($content, 'Log\.Message\(')).Count
$beforeWarningCount = ([regex]::Matches($content, 'Log\.Warning\(')).Count
$beforeErrorCount = ([regex]::Matches($content, 'Log\.Error\(')).Count

Write-Host "删除前日志统计:" -ForegroundColor Yellow
Write-Host "  - Log.Message: $beforeMessageCount"
Write-Host "  - Log.Warning: $beforeWarningCount"
Write-Host "  - Log.Error: $beforeErrorCount"

# 定义要删除的成功日志模式（保留缓存清空的日志）
$patternsToRemove = @(
    'Log\.Message\(\$"\[PortraitLoader\] \? 使用缓存:.*?\);',
    'Log\.Message\(\$"\[PortraitLoader\] \?\? 缓存未命中.*?\);',
    'Log\.Message\(\$"\[PortraitLoader\] \? 自动分割模式成功"\);',
    'Log\.Message\(\$"\[PortraitLoader\] \? 整图表情模式成功"\);',
    'Log\.Message\(\$"\[PortraitLoader\] \? 面部覆盖模式成功"\);',
    'Log\.Message\(\$"\[PortraitLoader\] \? 使用基础立绘"\);',
    'Log\.Message\(\$"\[PortraitLoader\] \? 已缓存:.*?\);',
    'Log\.Message\(\$"\[PortraitLoader\] 开始加载:.*?\);',
    'Log\.Message\(\$"\[PortraitLoader\] \? 从 Expressions 加载整图表情:.*?\);',
    'Log\.Message\(\$"\[PortraitLoader\] 从自定义路径加载表情:.*?\);',
    'Log\.Message\(\$"\[PortraitLoader\] \? 使用面部覆盖模式:.*?\);',
    'Log\.Message\(\$"\[PortraitLoader\] \? 加载基础立绘:.*?\);',
    'Log\.Message\(\$"\[PortraitLoader\] \? 从 portraitPath 加载:.*?\);',
    'Log\.Message\(\$"\[PortraitLoader\] \? 从自定义路径加载:.*?\);',
    'Log\.Message\(\$"\[PortraitLoader\] 清除缓存:.*?\);',
    'Log\.Message\(\$"\[PortraitLoader\] 创建 Mod 立绘目录:.*?\);',
    'Log\.Message\(\$"\[PortraitLoader\] 创建用户立绘目录:.*?\);',
    'Log\.Message\(\$"\[PortraitLoader\] 找到原版立绘:.*?\);',
    'Log\.Message\(\$"\[PortraitLoader\] 找到Mod立绘:.*?\);'
)

# 删除匹配的日志行
$removedCount = 0
foreach ($pattern in $patternsToRemove) {
    $matches = [regex]::Matches($content, $pattern)
    if ($matches.Count -gt 0) {
        $content = [regex]::Replace($content, $pattern, '')
        $removedCount += $matches.Count
        Write-Host "  移除 $($matches.Count) 条: $pattern" -ForegroundColor DarkGray
    }
}

# 移除空行（如果日志删除后留下了空行）
$content = [regex]::Replace($content, '(\r?\n){3,}', "`r`n`r`n")

# 统计删除后的日志数量
$afterMessageCount = ([regex]::Matches($content, 'Log\.Message\(')).Count
$afterWarningCount = ([regex]::Matches($content, 'Log\.Warning\(')).Count
$afterErrorCount = ([regex]::Matches($content, 'Log\.Error\(')).Count

Write-Host "`n删除后日志统计:" -ForegroundColor Green
Write-Host "  - Log.Message: $afterMessageCount (-$($beforeMessageCount - $afterMessageCount))"
Write-Host "  - Log.Warning: $afterWarningCount"
Write-Host "  - Log.Error: $afterErrorCount"

# 保存文件
$content | Set-Content $filePath -Encoding UTF8 -NoNewline

Write-Host "`n? 成功删除 $removedCount 条成功日志！" -ForegroundColor Green
Write-Host "   文件已更新: $filePath" -ForegroundColor Cyan

# 询问是否编译部署
Write-Host "`n是否立即编译并部署？(Y/N)" -ForegroundColor Yellow -NoNewline
$response = Read-Host " "

if ($response -eq 'Y' -or $response -eq 'y') {
    Write-Host "`n开始编译部署..." -ForegroundColor Cyan
    & .\Smart-Deploy.ps1
} else {
    Write-Host "已跳过编译部署。" -ForegroundColor Gray
}
