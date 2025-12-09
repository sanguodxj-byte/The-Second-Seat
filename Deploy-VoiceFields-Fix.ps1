#!/usr/bin/env pwsh
# Deploy-VoiceFields-Fix.ps1
# 部署语音参数字段修复

param(
    [switch]$Force,
    [switch]$SkipBackup
)

$ErrorActionPreference = "Stop"

Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan
Write-Host "  ?? 语音参数字段修复部署脚本" -ForegroundColor Yellow
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan
Write-Host ""

# 1. 检查 DLL 是否存在
$dllPath = "Source\TheSecondSeat\bin\Release\net472\TheSecondSeat.dll"
if (-not (Test-Path $dllPath)) {
    Write-Host "? 错误：DLL 不存在，请先编译" -ForegroundColor Red
    Write-Host "   运行：dotnet build Source\TheSecondSeat\TheSecondSeat.csproj -c Release"
    exit 1
}

Write-Host "? 找到 DLL：$dllPath" -ForegroundColor Green
$dllSize = [math]::Round((Get-Item $dllPath).Length / 1MB, 2)
Write-Host "   大小：$dllSize MB" -ForegroundColor Gray
Write-Host ""

# 2. 备份现有 DLL（如果存在）
$gameDir = "D:\steam\steamapps\common\RimWorld\Mods\TheSecondSeat"
$targets = @(
    "$gameDir\Assemblies\TheSecondSeat.dll",
    "$gameDir\1.6\Assemblies\TheSecondSeat.dll"
)

if (-not $SkipBackup) {
    Write-Host "?? 备份现有 DLL..." -ForegroundColor Cyan
    foreach ($target in $targets) {
        if (Test-Path $target) {
            $backupPath = "$target.backup_$(Get-Date -Format 'yyyyMMdd_HHmmss')"
            Copy-Item $target $backupPath -Force
            Write-Host "   ? 备份到：$backupPath" -ForegroundColor Green
        }
    }
    Write-Host ""
}

# 3. 部署 DLL
Write-Host "?? 部署 DLL..." -ForegroundColor Cyan
foreach ($target in $targets) {
    $targetDir = Split-Path $target -Parent
    if (-not (Test-Path $targetDir)) {
        New-Item -ItemType Directory -Path $targetDir -Force | Out-Null
        Write-Host "   ? 创建目录：$targetDir" -ForegroundColor Green
    }
    
    Copy-Item $dllPath $target -Force
    Write-Host "   ? 部署到：$target" -ForegroundColor Green
}
Write-Host ""

# 4. 验证部署
Write-Host "?? 验证部署..." -ForegroundColor Cyan
$allOk = $true
foreach ($target in $targets) {
    if (Test-Path $target) {
        $targetSize = [math]::Round((Get-Item $target).Length / 1MB, 2)
        if ($targetSize -eq $dllSize) {
            Write-Host "   ? $target ($targetSize MB)" -ForegroundColor Green
        } else {
            Write-Host "   ?? $target 大小不匹配！" -ForegroundColor Yellow
            $allOk = $false
        }
    } else {
        Write-Host "   ? $target 不存在！" -ForegroundColor Red
        $allOk = $false
    }
}
Write-Host ""

# 5. 检查代码修改
Write-Host "?? 验证代码修改..." -ForegroundColor Cyan
$defFile = "Source\TheSecondSeat\PersonaGeneration\NarratorPersonaDef.cs"
$content = Get-Content $defFile -Raw

if ($content -match 'public string voicePitch = "\+0Hz";' -and 
    $content -match 'public string voiceRate = "\+0%";') {
    Write-Host "   ? voicePitch 和 voiceRate 字段已添加" -ForegroundColor Green
} else {
    Write-Host "   ? 字段未找到！" -ForegroundColor Red
    $allOk = $false
}
Write-Host ""

# 6. 生成部署报告
if ($allOk) {
    Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Green
    Write-Host "  ? 部署成功！" -ForegroundColor Green
    Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Green
    Write-Host ""
    Write-Host "?? 修改内容：" -ForegroundColor Yellow
    Write-Host "   - 文件：NarratorPersonaDef.cs"
    Write-Host "   - 新增字段：voicePitch, voiceRate"
    Write-Host "   - 默认值：+0Hz, +0%"
    Write-Host ""
    Write-Host "?? XML 使用示例：" -ForegroundColor Yellow
    Write-Host '   <defaultVoice>zh-CN-XiaoxiaoNeural</defaultVoice>'
    Write-Host '   <voicePitch>+50Hz</voicePitch>  <!-- 提高音调 -->'
    Write-Host '   <voiceRate>+10%</voiceRate>    <!-- 加快语速 -->'
    Write-Host ""
    Write-Host "?? 参数范围：" -ForegroundColor Yellow
    Write-Host "   - voicePitch: -100Hz ~ +100Hz (推荐 ±50Hz)"
    Write-Host "   - voiceRate:  -50%  ~ +100%  (推荐 ±20%)"
    Write-Host ""
    Write-Host "?? 下一步：" -ForegroundColor Cyan
    Write-Host "   1. 重启 RimWorld"
    Write-Host "   2. 在人格 XML 中添加 voicePitch 和 voiceRate"
    Write-Host "   3. 测试 TTS 效果"
    Write-Host ""
    Write-Host "?? 文档：" -ForegroundColor Cyan
    Write-Host "   - 语音音调与速度字段-快速参考.md"
    Write-Host "   - 语音参数字段实现总结.md"
    Write-Host ""
} else {
    Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Red
    Write-Host "  ? 部署失败！" -ForegroundColor Red
    Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Red
    Write-Host ""
    Write-Host "请检查上述错误并重试。" -ForegroundColor Yellow
    Write-Host ""
    exit 1
}

Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan
