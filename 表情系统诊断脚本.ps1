# 表情系统诊断脚本
# 用于快速测试表情切换功能

Write-Host "=== 表情系统诊断 ===" -ForegroundColor Cyan
Write-Host ""

# 1. 检查 DLL 部署状态
Write-Host "1. 检查 DLL 部署..." -ForegroundColor Yellow
$dllPath = "D:\steam\steamapps\common\RimWorld\Mods\TheSecondSeat\Assemblies\TheSecondSeat.dll"

if (Test-Path $dllPath) {
    $dll = Get-Item $dllPath
    Write-Host "   ? DLL 存在" -ForegroundColor Green
    Write-Host "   文件大小: $($dll.Length) bytes" -ForegroundColor Gray
    Write-Host "   修改时间: $($dll.LastWriteTime)" -ForegroundColor Gray
} else {
    Write-Host "   ? DLL 不存在！" -ForegroundColor Red
    exit
}

Write-Host ""

# 2. 检查表情文件是否存在
Write-Host "2. 检查表情文件..." -ForegroundColor Yellow
$expressionsPath = "D:\steam\steamapps\common\RimWorld\Mods\TheSecondSeat\Textures\UI\Narrators\9x16\Expressions"

if (Test-Path $expressionsPath) {
    $expressionFolders = Get-ChildItem $expressionsPath -Directory
    Write-Host "   ? Expressions 文件夹存在" -ForegroundColor Green
    Write-Host "   人格数量: $($expressionFolders.Count)" -ForegroundColor Gray
    
    foreach ($folder in $expressionFolders) {
        $files = Get-ChildItem $folder.FullName -Filter "*.png"
        if ($files.Count -gt 0) {
            Write-Host "   ?? $($folder.Name): $($files.Count) 个表情文件" -ForegroundColor Cyan
            foreach ($file in $files) {
                Write-Host "      - $($file.Name)" -ForegroundColor Gray
            }
        }
    }
} else {
    Write-Host "   ??  Expressions 文件夹不存在" -ForegroundColor Yellow
    Write-Host "   创建文件夹..." -ForegroundColor Gray
    New-Item -ItemType Directory -Path $expressionsPath -Force | Out-Null
}

Write-Host ""

# 3. 游戏内测试命令
Write-Host "3. 游戏内测试命令" -ForegroundColor Yellow
Write-Host ""
Write-Host "   请在 RimWorld 开发者控制台执行以下命令：" -ForegroundColor White
Write-Host ""
Write-Host "   # 打开表情调试器" -ForegroundColor Cyan
Write-Host '   var persona = Verse.DefDatabase<TheSecondSeat.PersonaGeneration.NarratorPersonaDef>.GetNamed("Cassandra_Classic");' -ForegroundColor Gray
Write-Host '   Verse.Find.WindowStack.Add(new TheSecondSeat.UI.Dialog_ExpressionDebug(persona));' -ForegroundColor Gray
Write-Host ""
Write-Host "   # 手动切换表情（测试）" -ForegroundColor Cyan
Write-Host '   TheSecondSeat.PersonaGeneration.ExpressionSystem.SetExpression("Cassandra_Classic", TheSecondSeat.PersonaGeneration.ExpressionType.Happy);' -ForegroundColor Gray
Write-Host ""
Write-Host "   # 查看表情状态" -ForegroundColor Cyan
Write-Host '   Verse.Log.Message(TheSecondSeat.PersonaGeneration.ExpressionSystem.GetDebugInfo());' -ForegroundColor Gray
Write-Host ""

# 4. 检查日志关键词
Write-Host "4. 需要观察的日志关键词" -ForegroundColor Yellow
Write-Host "   在游戏日志中搜索以下关键词：" -ForegroundColor Gray
Write-Host "   - [ExpressionSystem] ?" -ForegroundColor Green
Write-Host "   - [NarratorScreenButton] ? 更新头像" -ForegroundColor Green
Write-Host "   - [PortraitLoader]" -ForegroundColor Green
Write-Host "   - [ExpressionDebug]" -ForegroundColor Green
Write-Host ""

# 5. 常见问题诊断
Write-Host "5. 常见问题诊断" -ForegroundColor Yellow
Write-Host ""
Write-Host "   ? 问题：表情切换日志显示成功，但头像没变化" -ForegroundColor Cyan
Write-Host "   ?? 原因：" -ForegroundColor Yellow
Write-Host "      1. NarratorScreenButton 的 UpdatePortrait 更新间隔太长（30 ticks）" -ForegroundColor Gray
Write-Host "      2. 缓存没有正确清除" -ForegroundColor Gray
Write-Host "      3. PortraitLoader.LoadPortrait 返回了旧的缓存" -ForegroundColor Gray
Write-Host ""
Write-Host "   ?? 解决方案：" -ForegroundColor Yellow
Write-Host "      1. 强制清除所有缓存：" -ForegroundColor Gray
Write-Host '         TheSecondSeat.PersonaGeneration.PortraitLoader.ClearCache();' -ForegroundColor White
Write-Host ""
Write-Host "      2. 手动触发头像更新（如果 NarratorScreenButton 已打开）：" -ForegroundColor Gray
Write-Host '         // 先切换表情' -ForegroundColor White
Write-Host '         TheSecondSeat.PersonaGeneration.ExpressionSystem.SetExpression("Cassandra_Classic", TheSecondSeat.PersonaGeneration.ExpressionType.Happy);' -ForegroundColor White
Write-Host '         // 等待 0.5-1 秒后检查按钮' -ForegroundColor White
Write-Host ""
Write-Host "      3. 使用表情调试器（推荐）：" -ForegroundColor Gray
Write-Host "         - 表情调试器会实时重新加载头像" -ForegroundColor White
Write-Host "         - 左侧立绘预览区域会立即显示新表情" -ForegroundColor White
Write-Host ""

Write-Host ""
Write-Host "=== 诊断完成 ===" -ForegroundColor Cyan
Write-Host ""
Write-Host "下一步：" -ForegroundColor Yellow
Write-Host "1. 启动 RimWorld" -ForegroundColor White
Write-Host "2. 加载存档" -ForegroundColor White
Write-Host "3. 打开开发者模式（按 ~）" -ForegroundColor White
Write-Host "4. 执行上述测试命令" -ForegroundColor White
Write-Host "5. 观察日志输出" -ForegroundColor White
Write-Host ""
