# API 连接诊断脚本
# 用于快速排查 The Second Seat 模组的 API 连接问题

$ErrorActionPreference = "SilentlyContinue"

Write-Host "`n" + "="*70 -ForegroundColor Cyan
Write-Host "  The Second Seat - API 连接诊断工具" -ForegroundColor Yellow
Write-Host "="*70 + "`n" -ForegroundColor Cyan

# 1. 检查本地 LLM (LM Studio)
Write-Host "?? 检查 1/5: 本地 LLM (localhost:1234)..." -ForegroundColor Cyan
try {
    $response = Invoke-WebRequest -Uri "http://localhost:1234/v1/models" -TimeoutSec 5 -UseBasicParsing
    if ($response.StatusCode -eq 200) {
        Write-Host "  ? 本地 LLM 运行正常 (端口 1234)" -ForegroundColor Green
        $hasLocalLLM = $true
    }
} catch {
    Write-Host "  ? 本地 LLM 未运行" -ForegroundColor Red
    Write-Host "     → 请启动 LM Studio 并加载模型" -ForegroundColor Gray
    $hasLocalLLM = $false
}

Write-Host ""

# 2. 检查网络连接
Write-Host "?? 检查 2/5: 远程 API 可达性..." -ForegroundColor Cyan

$endpoints = @(
    @{ Name = "OpenAI"; Host = "api.openai.com" },
    @{ Name = "DeepSeek"; Host = "api.deepseek.com" },
    @{ Name = "Google (Gemini)"; Host = "generativelanguage.googleapis.com" }
)

$reachableAPIs = @()
foreach ($endpoint in $endpoints) {
    try {
        $test = Test-NetConnection -ComputerName $endpoint.Host -Port 443 -WarningAction SilentlyContinue
        if ($test.TcpTestSucceeded) {
            Write-Host "  ? $($endpoint.Name) 可访问" -ForegroundColor Green
            $reachableAPIs += $endpoint.Name
        } else {
            Write-Host "  ? $($endpoint.Name) 不可访问" -ForegroundColor Red
        }
    } catch {
        Write-Host "  ? $($endpoint.Name) 连接测试失败" -ForegroundColor Red
    }
}

Write-Host ""

# 3. 检查配置文件
Write-Host "?? 检查 3/5: 模组配置文件..." -ForegroundColor Cyan
$configPath = "$env:LOCALAPPDATA\..\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Config\ModSettings_TheSecondSeat.xml"

if (Test-Path $configPath) {
    Write-Host "  ? 配置文件存在" -ForegroundColor Green
    $config = Get-Content $configPath -Raw
    
    # 解析关键配置
    if ($config -match "<apiEndpoint>(.*?)</apiEndpoint>") {
        $apiEndpoint = $matches[1]
        Write-Host "     API Endpoint: $apiEndpoint" -ForegroundColor Gray
    }
    if ($config -match "<modelName>(.*?)</modelName>") {
        $modelName = $matches[1]
        Write-Host "     Model Name: $modelName" -ForegroundColor Gray
    }
    if ($config -match "<apiKey>(.*?)</apiKey>") {
        $apiKey = $matches[1]
        if ($apiKey -ne "") {
            $maskedKey = $apiKey.Substring(0, 7) + "..." + $apiKey.Substring($apiKey.Length - 4)
            Write-Host "     API Key: $maskedKey" -ForegroundColor Gray
        } else {
            Write-Host "     API Key: (未设置)" -ForegroundColor Yellow
        }
    }
    if ($config -match "<enableMultimodalAnalysis>(.*?)</enableMultimodalAnalysis>") {
        $multimodal = $matches[1]
        if ($multimodal -eq "True") {
            Write-Host "     ? 多模态分析: 已启用" -ForegroundColor Green
        } else {
            Write-Host "     ?? 多模态分析: 未启用" -ForegroundColor Yellow
        }
    }
} else {
    Write-Host "  ?? 配置文件不存在（首次运行正常）" -ForegroundColor Yellow
    Write-Host "     路径: $configPath" -ForegroundColor Gray
}

Write-Host ""

# 4. 检查 DLL 文件
Write-Host "?? 检查 4/5: 模组文件..." -ForegroundColor Cyan
$rimworldPath = "D:\steam\steamapps\common\RimWorld\Mods\TheSecondSeat"
$dllPath = "$rimworldPath\Assemblies\TheSecondSeat.dll"

if (Test-Path $dllPath) {
    $dll = Get-Item $dllPath
    $sizeKB = [math]::Round($dll.Length / 1KB, 2)
    Write-Host "  ? DLL 文件存在 ($sizeKB KB)" -ForegroundColor Green
    Write-Host "     编译时间: $($dll.LastWriteTime)" -ForegroundColor Gray
} else {
    Write-Host "  ? DLL 文件不存在！" -ForegroundColor Red
    Write-Host "     路径: $dllPath" -ForegroundColor Gray
}

# 检查 About.xml
$aboutPath = "$rimworldPath\About\About.xml"
if (Test-Path $aboutPath) {
    Write-Host "  ? About.xml 存在" -ForegroundColor Green
} else {
    Write-Host "  ? About.xml 缺失！" -ForegroundColor Red
}

Write-Host ""

# 5. 检查日志文件
Write-Host "?? 检查 5/5: 游戏日志..." -ForegroundColor Cyan
$logPath = "D:\steam\steamapps\common\RimWorld\Player.log"

if (Test-Path $logPath) {
    Write-Host "  ? 日志文件存在" -ForegroundColor Green
    
    # 读取最后 100 行
    $logLines = Get-Content $logPath -Tail 100
    
    # 检查错误
    $errors = $logLines | Select-String -Pattern "(\[The Second Seat\].*error|\[MultimodalAnalysis\].*error|Exception.*TheSecondSeat)" -CaseSensitive:$false
    
    if ($errors.Count -gt 0) {
        Write-Host "  ?? 发现错误日志 ($($errors.Count) 条):" -ForegroundColor Yellow
        foreach ($error in $errors | Select-Object -First 3) {
            Write-Host "     $($error.Line)" -ForegroundColor Red
        }
        if ($errors.Count > 3) {
            Write-Host "     ... 还有 $($errors.Count - 3) 条错误" -ForegroundColor Gray
        }
    } else {
        Write-Host "  ? 未发现严重错误" -ForegroundColor Green
    }
} else {
    Write-Host "  ?? 日志文件不存在（游戏未启动过）" -ForegroundColor Yellow
}

Write-Host ""

# === 诊断总结 ===
Write-Host "="*70 -ForegroundColor Cyan
Write-Host "  诊断总结" -ForegroundColor Yellow
Write-Host "="*70 -ForegroundColor Cyan

if ($hasLocalLLM) {
    Write-Host "`n? 推荐使用：本地 LLM (LM Studio)" -ForegroundColor Green
    Write-Host "   配置：" -ForegroundColor Gray
    Write-Host "   - API Endpoint: http://localhost:1234/v1/chat/completions" -ForegroundColor Gray
    Write-Host "   - API Key: (留空)" -ForegroundColor Gray
    Write-Host "   - Model Name: local-model" -ForegroundColor Gray
} elseif ($reachableAPIs.Count -gt 0) {
    Write-Host "`n?? 本地 LLM 未运行，可使用远程 API：" -ForegroundColor Yellow
    foreach ($api in $reachableAPIs) {
        Write-Host "   - $api" -ForegroundColor Gray
    }
} else {
    Write-Host "`n? 所有 API 端点都不可用！" -ForegroundColor Red
    Write-Host "   请检查：" -ForegroundColor Gray
    Write-Host "   1. LM Studio 是否启动并加载模型" -ForegroundColor Gray
    Write-Host "   2. 网络连接是否正常" -ForegroundColor Gray
    Write-Host "   3. 防火墙设置" -ForegroundColor Gray
}

Write-Host "`n?? 详细文档：API链接失败诊断指南.md" -ForegroundColor Cyan
Write-Host "?? 游戏日志：$logPath" -ForegroundColor Cyan

Write-Host "`n" + "="*70 -ForegroundColor Cyan
Write-Host "  诊断完成" -ForegroundColor Yellow
Write-Host "="*70 + "`n" -ForegroundColor Cyan

# 可选：自动打开日志
$openLog = Read-Host "是否打开游戏日志？(Y/N)"
if ($openLog -eq "Y" -or $openLog -eq "y") {
    if (Test-Path $logPath) {
        notepad $logPath
    }
}
