# ? ModSettings 快速简化脚本 - v1.6.65
# 目标：减少代码重复，删除未使用方法

Write-Host "?? 开始简化 ModSettings.cs..." -ForegroundColor Cyan

$modSettingsPath = "Source\TheSecondSeat\Settings\ModSettings.cs"

if (-not (Test-Path $modSettingsPath)) {
    Write-Host "? 文件不存在: $modSettingsPath" -ForegroundColor Red
    exit 1
}

# 读取文件
$content = Get-Content $modSettingsPath -Raw -Encoding UTF8

Write-Host "?? 原始文件大小: $([math]::Round((Get-Item $modSettingsPath).Length / 1KB, 2)) KB" -ForegroundColor Yellow

# 1. 简化 GetExampleGlobalPrompt 方法
Write-Host "`n?? 简化 GetExampleGlobalPrompt..." -ForegroundColor Cyan

$oldPromptPattern = @'
        private string GetExampleGlobalPrompt\(\)
        \{
            return @"[\s\S]*?";
        \}
'@

$newPromptMethod = @'
        private string GetExampleGlobalPrompt()
        {
            return "# 全局提示词示例\n\n" +
                   "你可以在这里添加全局指令来影响AI的行为。\n\n" +
                   "例如：\n" +
                   "- 使用友好轻松的语气\n" +
                   "- 优先考虑玩家的安全\n" +
                   "- 在危险情况下提供警告";
        }
'@

$content = $content -replace $oldPromptPattern, $newPromptMethod

# 保存修改
$content | Set-Content $modSettingsPath -Encoding UTF8 -NoNewline

Write-Host "? 简化完成" -ForegroundColor Green

Write-Host "`n?? 新文件大小: $([math]::Round((Get-Item $modSettingsPath).Length / 1KB, 2)) KB" -ForegroundColor Yellow

Write-Host "`n?? 完成！" -ForegroundColor Green
Write-Host "请编译项目验证修改是否正确。" -ForegroundColor Cyan
