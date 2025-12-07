# ========================================
# 双难度模式 - 快速集成脚本
# ========================================

Write-Host "=== 双难度模式系统集成 ===" -ForegroundColor Cyan
Write-Host ""

# 1. 检查核心文件是否存在
Write-Host "检查核心文件..." -ForegroundColor Yellow

$files = @(
    "Source\TheSecondSeat\PersonaGeneration\AIDifficultyMode.cs",
    "Source\TheSecondSeat\PersonaGeneration\SystemPromptGenerator.cs",
    "Source\TheSecondSeat\Settings\ModSettings.cs"
)

foreach ($file in $files) {
    if (Test-Path $file) {
        Write-Host "  ? $file" -ForegroundColor Green
    } else {
        Write-Host "  ? $file 不存在！" -ForegroundColor Red
    }
}

Write-Host ""

# 2. 查找需要修改的调用点
Write-Host "查找需要修改的调用点..." -ForegroundColor Yellow

$results = Select-String -Path "Source\TheSecondSeat\**\*.cs" -Pattern "GenerateSystemPrompt\(" -Recursive

Write-Host "  找到 $($results.Count) 处调用" -ForegroundColor Cyan

foreach ($result in $results) {
    $file = $result.Filename
    $line = $result.LineNumber
    Write-Host "  - $file : Line $line" -ForegroundColor Gray
}

Write-Host ""

# 3. 提示需要手动修改的内容
Write-Host "=== 需要手动完成的任务 ===" -ForegroundColor Yellow
Write-Host ""

Write-Host "1. 添加UI到 ModSettings.cs" -ForegroundColor Cyan
Write-Host "   位置: DoSettingsWindowContents 方法，第 278 行之后"
Write-Host "   内容: 见 '双难度模式系统实现总结.md'"
Write-Host ""

Write-Host "2. 修改调用点传入难度模式参数" -ForegroundColor Cyan
Write-Host "   需要修改的文件:"
foreach ($result in $results) {
    Write-Host "   - $($result.Path)" -ForegroundColor Gray
}
Write-Host ""

Write-Host "   修改示例:" -ForegroundColor White
Write-Host @"
   // 获取难度模式设置
   var modSettings = LoadedModManager.GetMod<Settings.TheSecondSeatMod>()?
       .GetSettings<Settings.TheSecondSeatSettings>();
   
   // 传入难度模式
   var systemPrompt = SystemPromptGenerator.GenerateSystemPrompt(
       personaDef,
       analysis,
       agent,
       modSettings?.difficultyMode ?? AIDifficultyMode.Assistant
   );
"@ -ForegroundColor Gray

Write-Host ""

# 4. 测试建议
Write-Host "=== 测试建议 ===" -ForegroundColor Yellow
Write-Host ""
Write-Host "1. 编译并部署:" -ForegroundColor Cyan
Write-Host "   .\Smart-Deploy.ps1" -ForegroundColor Gray
Write-Host ""

Write-Host "2. 游戏内测试:" -ForegroundColor Cyan
Write-Host "   a) 打开设置，选择助手模式"
Write-Host "   b) 发送指令，AI应该始终执行"
Write-Host "   c) 修改好感度到-80"
Write-Host "   d) 切换到对弈者模式"
Write-Host "   e) 发送指令，AI可能拒绝"
Write-Host ""

Write-Host "3. 查看系统提示词:" -ForegroundColor Cyan
Write-Host "   .\Export-SystemPrompt.ps1" -ForegroundColor Gray
Write-Host ""

# 5. 显示文档链接
Write-Host "=== 相关文档 ===" -ForegroundColor Yellow
Write-Host ""
Write-Host "- 双难度模式系统实现总结.md" -ForegroundColor Gray
Write-Host "- Export-SystemPrompt.ps1 (查看提示词)"
Write-Host ""

Write-Host "=== 完成 ===" -ForegroundColor Green
Write-Host ""
Write-Host "核心逻辑已实现，请按照上述说明完成UI和调用点的集成。"
