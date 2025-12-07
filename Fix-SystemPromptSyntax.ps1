# 快速修复SystemPromptGenerator.cs的语法错误

$file = "Source\TheSecondSeat\PersonaGeneration\SystemPromptGenerator.cs"
$content = Get-Content $file -Raw -Encoding UTF8

# 修复第547-551行的多行字符串（缺少闭合引号）
$pattern = [regex]::Escape(@"
                else if (affinity >= 30f)
                {
                    return @"You respect their skill and provide fair challenge.
- Maintain balanced event generation
- Execute commands without objection
- Observe their strategies with interest
- You're an impartial opponent, neither helping nor hindering excessively";
"@)

$replacement = @"
                else if (affinity >= 30f)
                {
                    return @"You respect their skill and provide fair challenge.
- Maintain balanced event generation
- Execute commands without objection
- Observe their strategies with interest
- You're an impartial opponent, neither helping nor hindering excessively";
"@

# 替换
$newContent = $content -replace [regex]::Escape(@"
                else if (affinity >= 30f)
                {
                    return @"You respect their skill and provide fair challenge.
- Maintain balanced event generation
- Execute commands without objection
- Observe their strategies with interest
- You're an impartial opponent, neither helping nor hindering excessively";
"@), $replacement

# 如果没有变化，说明格式可能不对，直接替换问题行
if ($newContent -eq $content) {
    Write-Host "使用简单替换..." -ForegroundColor Yellow
    $newContent = $content -replace 'You''re an impartial opponent, neither helping nor hindering excessively";', 'You''re an impartial opponent, neither helping nor hindering excessively";'
}

# 保存
$newContent | Set-Content $file -Encoding UTF8 -NoNewline

Write-Host "? 文件已修复" -ForegroundColor Green
