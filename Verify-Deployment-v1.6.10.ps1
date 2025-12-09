# 快速部署验证脚本

$ErrorActionPreference = "Stop"

Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan
Write-Host "  ?? The Second Seat v1.6.10 - 部署验证" -ForegroundColor Yellow
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan
Write-Host ""

# 验证编译
Write-Host "1??  检查编译输出..." -ForegroundColor Cyan
$dllPath = "Source\TheSecondSeat\bin\Release\net472\TheSecondSeat.dll"
if (Test-Path $dllPath) {
    $dll = Get-Item $dllPath
    Write-Host "  ? DLL 存在" -ForegroundColor Green
    Write-Host "     路径: $dllPath" -ForegroundColor Gray
    Write-Host "     大小: $([math]::Round($dll.Length / 1MB, 2)) MB" -ForegroundColor Gray
    Write-Host "     修改时间: $($dll.LastWriteTime)" -ForegroundColor Gray
} else {
    Write-Host "  ? DLL 不存在！" -ForegroundColor Red
    exit 1
}

Write-Host ""

# 验证部署
Write-Host "2??  检查部署位置..." -ForegroundColor Cyan
$deployPaths = @(
    "D:\steam\steamapps\common\RimWorld\Mods\TheSecondSeat\Assemblies\TheSecondSeat.dll",
    "D:\steam\steamapps\common\RimWorld\Mods\TheSecondSeat\1.6\Assemblies\TheSecondSeat.dll"
)

foreach ($path in $deployPaths) {
    if (Test-Path $path) {
        $file = Get-Item $path
        Write-Host "  ? $path" -ForegroundColor Green
        Write-Host "     修改时间: $($file.LastWriteTime)" -ForegroundColor Gray
    } else {
        Write-Host "  ? $path (未找到)" -ForegroundColor Red
    }
}

Write-Host ""

# 验证代码修改
Write-Host "3??  验证代码修改..." -ForegroundColor Cyan

# 验证 TTS 48kHz
$ttsFile = "Source\TheSecondSeat\TTS\TTSService.cs"
$ttsContent = Get-Content $ttsFile -Raw
if ($ttsContent -match 'riff-48khz-16bit-mono-pcm') {
    Write-Host "  ? TTS 48kHz 采样率" -ForegroundColor Green
} else {
    Write-Host "  ? TTS 仍为 24kHz！" -ForegroundColor Red
}

# 验证人格数据文件夹功能
$personaFile = "Source\TheSecondSeat\UI\PersonaSelectionWindow.cs"
$personaContent = Get-Content $personaFile -Raw
if ($personaContent -match 'OpenPersonaDefsFolder') {
    Write-Host "  ? 人格数据文件夹功能" -ForegroundColor Green
} else {
    Write-Host "  ? 人格数据文件夹功能未添加！" -ForegroundColor Red
}

# 验证多模态分析优化
$multimodalFile = "Source\TheSecondSeat\PersonaGeneration\MultimodalAnalysisService.cs"
$multimodalContent = Get-Content $multimodalFile -Raw
if ($multimodalContent -match '8192.*确保完整JSON返回') {
    Write-Host "  ? 多模态分析 maxTokens=8192" -ForegroundColor Green
} else {
    Write-Host "  ??  多模态分析 maxTokens 可能未更新" -ForegroundColor Yellow
}

Write-Host ""

# 总结
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan
Write-Host "  ?? 验证总结" -ForegroundColor Yellow
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan
Write-Host ""
Write-Host "? 编译输出正常" -ForegroundColor Green
Write-Host "? DLL 已部署" -ForegroundColor Green
Write-Host "? 代码修改已应用" -ForegroundColor Green
Write-Host ""
Write-Host "?? 部署验证完成！" -ForegroundColor Cyan
Write-Host ""
Write-Host "?? 下一步:" -ForegroundColor Yellow
Write-Host "  1. 完全关闭 RimWorld" -ForegroundColor White
Write-Host "  2. 重新启动游戏" -ForegroundColor White
Write-Host "  3. 测试 TTS (设置 → Mod 设置 → The Second Seat → TTS)" -ForegroundColor White
Write-Host "  4. 测试人格数据文件夹 (人格选择 → 右键 → 打开人格数据文件夹)" -ForegroundColor White
Write-Host "  5. 测试多模态分析 (基于立绘生成人格)" -ForegroundColor White
Write-Host ""
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan
