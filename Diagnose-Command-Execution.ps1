# 诊断命令执行问题

Write-Host "`n" + "="*60 -ForegroundColor Cyan
Write-Host "  命令执行诊断工具" -ForegroundColor Yellow
Write-Host "="*60 + "`n" -ForegroundColor Cyan

# 查找最近的日志文件
$rimworldPath = "C:\Users\Administrator\AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios"
if (-not (Test-Path $rimworldPath)) {
    $rimworldPath = "$env:USERPROFILE\AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios"
}

$logFile = Join-Path $rimworldPath "Player.log"

if (-not (Test-Path $logFile)) {
    Write-Host "? 无法找到日志文件: $logFile" -ForegroundColor Red
    exit 1
}

Write-Host "?? 日志文件: $logFile" -ForegroundColor Cyan
Write-Host ""

# 检查命令相关的日志
Write-Host "?? 搜索命令执行相关日志..." -ForegroundColor Yellow
Write-Host ""

$commandPatterns = @(
    "ExecuteAdvancedCommand",
    "ParseFromLLMResponse",
    "GameActionExecutor.Execute",
    "command.action",
    "CommandParser",
    "解析命令",
    "执行命令",
    "命令成功",
    "命令失败"
)

$foundLogs = @()

foreach ($pattern in $commandPatterns) {
    $lines = Select-String -Path $logFile -Pattern $pattern -Context 2 | Select-Object -Last 10
    if ($lines) {
        $foundLogs += $lines
    }
}

if ($foundLogs.Count -eq 0) {
    Write-Host "??  未找到任何命令执行相关日志" -ForegroundColor Yellow
    Write-Host "   这可能意味着：" -ForegroundColor Gray
    Write-Host "   1. AI 从未尝试执行命令" -ForegroundColor Gray
    Write-Host "   2. 命令解析在早期阶段失败" -ForegroundColor Gray
    Write-Host "   3. 日志记录被禁用或损坏" -ForegroundColor Gray
} else {
    Write-Host "?? 找到 $($foundLogs.Count) 条相关日志：" -ForegroundColor Green
    Write-Host ""
    
    foreach ($log in $foundLogs) {
        Write-Host "---" -ForegroundColor Gray
        Write-Host $log.Line -ForegroundColor White
        if ($log.Context.PreContext) {
            Write-Host "  前置: $($log.Context.PreContext -join ' | ')" -ForegroundColor DarkGray
        }
        if ($log.Context.PostContext) {
            Write-Host "  后置: $($log.Context.PostContext -join ' | ')" -ForegroundColor DarkGray
        }
    }
}

Write-Host ""
Write-Host "---" -ForegroundColor Gray
Write-Host ""

# 检查 AI 响应中是否包含 command 字段
Write-Host "?? 检查 AI 响应格式..." -ForegroundColor Yellow
Write-Host ""

$aiResponsePatterns = @(
    '"command"',
    'response.command',
    'llmCommand',
    'LLMCommand'
)

$responseFound = $false
foreach ($pattern in $aiResponsePatterns) {
    $lines = Select-String -Path $logFile -Pattern $pattern -Context 1 | Select-Object -Last 5
    if ($lines) {
        $responseFound = $true
        foreach ($log in $lines) {
            Write-Host $log.Line -ForegroundColor Cyan
        }
    }
}

if (-not $responseFound) {
    Write-Host "??  AI 响应中可能没有 command 字段" -ForegroundColor Yellow
    Write-Host "   原因可能是：" -ForegroundColor Gray
    Write-Host "   1. System Prompt 没有要求 AI 返回命令" -ForegroundColor Gray
    Write-Host "   2. AI 选择不执行任何命令" -ForegroundColor Gray
    Write-Host "   3. 难度模式设置导致 AI 不执行命令" -ForegroundColor Gray
}

Write-Host ""
Write-Host "---" -ForegroundColor Gray
Write-Host ""

# 检查 System Prompt 是否包含命令相关指示
Write-Host "?? 检查 System Prompt..." -ForegroundColor Yellow
Write-Host ""

$promptPatterns = @(
    "SystemPrompt",
    "GetDynamicSystemPrompt",
    "command execution",
    "execute commands"
)

$promptFound = $false
foreach ($pattern in $promptPatterns) {
    $lines = Select-String -Path $logFile -Pattern $pattern | Select-Object -Last 3
    if ($lines) {
        $promptFound = $true
        Write-Host "找到 System Prompt 相关日志：" -ForegroundColor Green
        foreach ($log in $lines) {
            Write-Host $log.Line -ForegroundColor White
        }
        break
    }
}

if (-not $promptFound) {
    Write-Host "??  未找到 System Prompt 相关日志" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "="*60 -ForegroundColor Cyan
Write-Host "  诊断建议" -ForegroundColor Yellow
Write-Host "="*60 -ForegroundColor Cyan
Write-Host ""

Write-Host "1. 检查 Mod 设置中的 '难度模式'：" -ForegroundColor White
Write-Host "   - Assistant 模式：AI 始终执行命令" -ForegroundColor Gray
Write-Host "   - Opponent 模式：AI 可能拒绝命令（好感度 < -70）" -ForegroundColor Gray
Write-Host ""

Write-Host "2. 在游戏中测试：" -ForegroundColor White
Write-Host "   - 发送消息：'帮我采收所有成熟作物'" -ForegroundColor Gray
Write-Host "   - 查看 AI 回复是否包含命令执行信息" -ForegroundColor Gray
Write-Host ""

Write-Host "3. 查看完整日志：" -ForegroundColor White
Write-Host "   $logFile" -ForegroundColor Cyan
Write-Host ""

Write-Host "4. 如果仍然无法执行命令：" -ForegroundColor White
Write-Host "   - 尝试重新编译和部署 Mod" -ForegroundColor Gray
Write-Host "   - 检查是否有其他 Mod 冲突" -ForegroundColor Gray
Write-Host "   - 确认 API 设置正确" -ForegroundColor Gray
Write-Host ""
