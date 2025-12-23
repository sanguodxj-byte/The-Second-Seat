# 修复 ModSettings.cs - 删除重复的方法定义
# v1.6.65

Write-Host "?? 修复 ModSettings.cs..." -ForegroundColor Cyan

$file = "Source\TheSecondSeat\Settings\ModSettings.cs"

# 读取文件
$content = Get-Content $file -Raw -Encoding UTF8

# 删除完整的 DrawDifficultyOption 方法定义（保留委托调用）
$content = $content -replace '(?s)        \/\/\/ <summary>\s+\/\/\/ ? 绘制难度模式选项.*?Text\.Font = GameFont\.Small;\s+\}', ''

# 删除完整的 DrawCollapsibleSection 方法定义（在第一个委托调用之后）
$content = $content -replace '(?s)        \/\/ 折叠区域辅助方法.*?listing\.GapLine\(\);\s+\}', ''

# 删除完整的 LoadDifficultyIcons 方法定义（在第一个委托调用之后）
$content = $content -replace '(?s)        \/\/ ? v1\.6\.45: 加载难度图标.*?opponentModeIcon = ContentFinder.*?\}\s+\}', ''

# 保存修改
$content | Set-Content $file -Encoding UTF8 -NoNewline

Write-Host "? 修复完成！" -ForegroundColor Green
Write-Host "正在编译..." -ForegroundColor Cyan

# 编译验证
dotnet build "Source\TheSecondSeat\TheSecondSeat.csproj" -c Release --nologo
