# ================================================================
# Verify-And-Build-RimAgent.ps1
# ? v1.6.65: 验证 RimAgent 文件并编译项目
# ================================================================

Write-Host "================================================================" -ForegroundColor Cyan
Write-Host "  RimAgent v1.6.65 验证与编译脚本" -ForegroundColor Cyan
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host ""

# ================================================================
# 1. 验证文件是否存在
# ================================================================
Write-Host "?? 1. 验证文件..." -ForegroundColor Yellow

$baseDir = "Source\TheSecondSeat\RimAgent"
$requiredFiles = @(
    "RimAgent.cs",
    "RimAgentTools.cs",
    "RimAgentModels.cs",
    "ConcurrentRequestManager.cs",
    "ILLMProvider.cs",
    "LLMProviderFactory.cs"
)

$allFilesExist = $true
foreach ($file in $requiredFiles) {
    $path = Join-Path $baseDir $file
    if (Test-Path $path) {
        $size = (Get-Item $path).Length
        Write-Host "  ? $file ($size bytes)" -ForegroundColor Green
    } else {
        Write-Host "  ? $file (缺失)" -ForegroundColor Red
        $allFilesExist = $false
    }
}

if (-not $allFilesExist) {
    Write-Host ""
    Write-Host "? 部分文件缺失，请先运行 Create-RimAgent-Files.ps1" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "? 所有文件验证通过！" -ForegroundColor Green
Write-Host ""

# ================================================================
# 2. 检查项目文件
# ================================================================
Write-Host "?? 2. 检查项目文件..." -ForegroundColor Yellow

$csprojPath = "Source\TheSecondSeat\TheSecondSeat.csproj"
if (Test-Path $csprojPath) {
    Write-Host "  ? 项目文件存在: $csprojPath" -ForegroundColor Green
} else {
    Write-Host "  ? 项目文件不存在: $csprojPath" -ForegroundColor Red
    exit 1
}

Write-Host ""

# ================================================================
# 3. 编译项目
# ================================================================
Write-Host "?? 3. 编译项目..." -ForegroundColor Yellow
Write-Host ""

$msbuildPath = & "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe" -latest -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe | Select-Object -First 1

if ($msbuildPath) {
    Write-Host "  使用 MSBuild: $msbuildPath" -ForegroundColor Cyan
    & $msbuildPath $csprojPath /p:Configuration=Release /p:Platform=AnyCPU /v:minimal /nologo
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host ""
        Write-Host "? 编译成功！" -ForegroundColor Green
    } else {
        Write-Host ""
        Write-Host "? 编译失败，请检查错误信息" -ForegroundColor Red
        exit 1
    }
} else {
    Write-Host "  ?? 未找到 MSBuild，尝试使用 dotnet build..." -ForegroundColor Yellow
    dotnet build $csprojPath -c Release --nologo
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host ""
        Write-Host "? 编译成功！" -ForegroundColor Green
    } else {
        Write-Host ""
        Write-Host "? 编译失败，请检查错误信息" -ForegroundColor Red
        exit 1
    }
}

Write-Host ""

# ================================================================
# 4. 检查输出文件
# ================================================================
Write-Host "?? 4. 检查输出文件..." -ForegroundColor Yellow

$dllPath = "Source\TheSecondSeat\bin\Release\net472\TheSecondSeat.dll"
if (Test-Path $dllPath) {
    $dllSize = (Get-Item $dllPath).Length
    $dllTime = (Get-Item $dllPath).LastWriteTime
    Write-Host "  ? DLL 文件: $dllPath" -ForegroundColor Green
    Write-Host "     大小: $dllSize bytes" -ForegroundColor White
    Write-Host "     时间: $dllTime" -ForegroundColor White
} else {
    Write-Host "  ? DLL 文件不存在: $dllPath" -ForegroundColor Red
    exit 1
}

Write-Host ""

# ================================================================
# 5. 生成使用示例
# ================================================================
Write-Host "?? 5. RimAgent 使用示例" -ForegroundColor Yellow
Write-Host ""

$exampleCode = @'
// ================================================================
// RimAgent v1.6.65 使用示例
// ================================================================

using TheSecondSeat.RimAgent;

// 1. 创建 Agent
var provider = LLMProviderFactory.GetProvider("openai");
var agent = new RimAgent("narrator-agent", "You are a RimWorld narrator", provider);

// 2. 注册工具
agent.RegisterTool("search");
agent.RegisterTool("analyze_colony");

// 3. 执行任务
var response = await agent.ExecuteAsync(
    "分析当前殖民地状态",
    temperature: 0.7f,
    maxTokens: 500
);

if (response.Success)
{
    Log.Message($"[RimAgent] 响应: {response.Content}");
}
else
{
    Log.Error($"[RimAgent] 错误: {response.Error}");
}

// 4. 查看统计
Log.Message(agent.GetDebugInfo());

// 5. 使用并发管理器
var result = await ConcurrentRequestManager.Instance.EnqueueAsync(
    async () => await agent.ExecuteAsync("你好"),
    maxRetries: 3
);

// 6. 清理
agent.ClearHistory();
agent.Reset();
'@

Write-Host $exampleCode -ForegroundColor Cyan
Write-Host ""

# ================================================================
# 完成总结
# ================================================================
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host "  ? RimAgent v1.6.65 验证与编译完成！" -ForegroundColor Green
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "?? 已创建的组件:" -ForegroundColor Yellow
Write-Host "  ? RimAgent           - 核心 Agent 类" -ForegroundColor Green
Write-Host "  ? RimAgentTools      - 工具库管理器" -ForegroundColor Green
Write-Host "  ? RimAgentModels     - 数据模型定义" -ForegroundColor Green
Write-Host "  ? ConcurrentRequestManager - 并发控制" -ForegroundColor Green
Write-Host ""
Write-Host "?? 下一步建议:" -ForegroundColor Yellow
Write-Host "  1. 在 NarratorManager 中集成 RimAgent" -ForegroundColor White
Write-Host "  2. 实现工具注册（search, analyze_colony 等）" -ForegroundColor White
Write-Host "  3. 在 LLMService 中使用 ConcurrentRequestManager" -ForegroundColor White
Write-Host "  4. 添加单元测试验证功能" -ForegroundColor White
Write-Host ""
Write-Host "?? 文档位置: Source\TheSecondSeat\RimAgent\README.md" -ForegroundColor Cyan
Write-Host ""
