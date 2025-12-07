# API 问题诊断脚本

Write-Host "`n=== API 问题诊断 ===" -ForegroundColor Cyan -BackgroundColor Black

$log = "$env:LOCALAPPDATA\..\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Player.log"

# 1. 当前 API 配置
Write-Host "`n1??  当前 API 配置：" -ForegroundColor Yellow
$config = Get-Content $log | Select-String "LLM configured" | Select-Object -Last 1
if ($config) {
    Write-Host $config -ForegroundColor Cyan
    
    # 解析配置
    if ($config -match "endpoint=([^,]+)") {
        $endpoint = $matches[1].Trim()
        Write-Host "`n  Endpoint: $endpoint" -ForegroundColor White
        
        # 判断 API 类型
        if ($endpoint -like "*generativelanguage.googleapis.com*") {
            Write-Host "  类型: Gemini API" -ForegroundColor Magenta
            Write-Host "  ??  警告：Gemini 使用特殊格式！" -ForegroundColor Yellow
        } elseif ($endpoint -like "*api.openai.com*") {
            Write-Host "  类型: OpenAI API" -ForegroundColor Green
        } elseif ($endpoint -like "*api.deepseek.com*") {
            Write-Host "  类型: DeepSeek API" -ForegroundColor Green
        } elseif ($endpoint -like "*localhost*") {
            Write-Host "  类型: 本地 LLM" -ForegroundColor Green
        }
    }
    
    if ($config -match "model=(.+)") {
        $model = $matches[1].Trim()
        Write-Host "  Model: $model" -ForegroundColor White
    }
} else {
    Write-Host "  ? 未找到配置" -ForegroundColor Red
}

# 2. 最近的错误
Write-Host "`n2??  最近的 API 错误：" -ForegroundColor Yellow
$errors = Get-Content $log | Select-String "The Second Seat.*LLM|error|Error|failed|Failed" | Select-Object -Last 10
if ($errors) {
    foreach ($err in $errors) {
        if ($err -like "*error*" -or $err -like "*Error*" -or $err -like "*failed*") {
            Write-Host "  " $err -ForegroundColor Red
        } else {
            Write-Host "  " $err -ForegroundColor Gray
        }
    }
} else {
    Write-Host "  ? 无错误" -ForegroundColor Green
}

# 3. 网络连接测试
Write-Host "`n3??  网络连接测试：" -ForegroundColor Yellow
if ($endpoint -and $endpoint -like "*generativelanguage.googleapis.com*") {
    Write-Host "  测试 Gemini API 连接..." -ForegroundColor Cyan
    $result = Test-NetConnection generativelanguage.googleapis.com -Port 443 -WarningAction SilentlyContinue
    if ($result.TcpTestSucceeded) {
        Write-Host "  ? 网络连接正常" -ForegroundColor Green
    } else {
        Write-Host "  ? 无法连接到 Gemini API" -ForegroundColor Red
    }
} elseif ($endpoint -and $endpoint -like "*api.openai.com*") {
    Write-Host "  测试 OpenAI API 连接..." -ForegroundColor Cyan
    $result = Test-NetConnection api.openai.com -Port 443 -WarningAction SilentlyContinue
    if ($result.TcpTestSucceeded) {
        Write-Host "  ? 网络连接正常" -ForegroundColor Green
    } else {
        Write-Host "  ? 无法连接到 OpenAI API" -ForegroundColor Red
    }
}

# 4. 指示灯位置调试
Write-Host "`n4??  指示灯位置调试：" -ForegroundColor Yellow
$debug = Get-Content $log | Select-String "\[TSS Debug\]" | Select-Object -Last 5
if ($debug) {
    foreach ($d in $debug) {
        Write-Host "  " $d -ForegroundColor Cyan
    }
} else {
    Write-Host "  ??  未找到调试日志（游戏未运行或未触发）" -ForegroundColor Yellow
}

# 5. 建议
Write-Host "`n5??  建议操作：" -ForegroundColor Yellow

if ($endpoint -like "*generativelanguage.googleapis.com*") {
    Write-Host @"
  
  ??  检测到 Gemini API！
  
  Gemini 使用不同的 API 格式，当前代码使用 OpenAI 格式。
  
  临时解决方案：
  1. 切换到 OpenAI 或 DeepSeek
  2. 或使用本地 LLM（推荐）
  
  本地 LLM 配置：
  - 下载 LM Studio: https://lmstudio.ai/
  - 启动本地服务器（端口 1234）
  - 在模组设置中选择"本地模型"
  
"@ -ForegroundColor Yellow
} else {
    Write-Host @"
  
  ? 使用的是兼容格式的 API
  
  如果仍然失败，请检查：
  1. API Key 是否正确
  2. API 配额是否用完
  3. 网络连接是否稳定
  
"@ -ForegroundColor Green
}

Write-Host "`n=== 诊断完成 ===" -ForegroundColor Cyan
Write-Host ""
