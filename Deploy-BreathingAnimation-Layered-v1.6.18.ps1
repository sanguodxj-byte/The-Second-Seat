# ================================================================
# 呼吸动画与分层立绘适配部署脚本 v1.6.18
# ================================================================
# 功能：确保呼吸动画和表情系统完全适配分层立绘
# 日期：2025-01-XX
# ================================================================

Write-Host "=====================================" -ForegroundColor Cyan
Write-Host "呼吸动画与分层立绘适配部署 v1.6.18" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host ""

$ErrorActionPreference = "Stop"
$projectRoot = "C:\Users\Administrator\Desktop\rim mod\The Second Seat"
$modPath = "D:\steam\steamapps\common\RimWorld\Mods\TheSecondSeat"

# 1. 验证源文件
Write-Host "[1/5] 验证源文件..." -ForegroundColor Yellow
$sourceFiles = @(
    "$projectRoot\Source\TheSecondSeat\UI\NarratorScreenButton.cs",
    "$projectRoot\Source\TheSecondSeat\UI\NarratorWindow.cs",
    "$projectRoot\Source\TheSecondSeat\PersonaGeneration\ExpressionSystem.cs",
    "$projectRoot\Source\TheSecondSeat\PersonaGeneration\ExpressionSystem_WithBreathing.cs",
    "$projectRoot\Source\TheSecondSeat\PersonaGeneration\PortraitLoader.cs",
    "$projectRoot\Source\TheSecondSeat\PersonaGeneration\LayeredPortraitCompositor.cs"
)

foreach ($file in $sourceFiles) {
    if (Test-Path $file) {
        Write-Host "  ? $([System.IO.Path]::GetFileName($file))" -ForegroundColor Green
    } else {
        Write-Host "  ? 缺失: $file" -ForegroundColor Red
        throw "源文件缺失"
    }
}

# 2. 验证呼吸动画适配代码
Write-Host "`n[2/5] 验证呼吸动画适配..." -ForegroundColor Yellow
$buttonCode = Get-Content "$projectRoot\Source\TheSecondSeat\UI\NarratorScreenButton.cs" -Raw

if ($buttonCode -match "float breathingOffset = ExpressionSystem\.GetBreathingOffset") {
    Write-Host "  ? NarratorScreenButton 已应用呼吸偏移" -ForegroundColor Green
} else {
    Write-Host "  ? NarratorScreenButton 未应用呼吸偏移" -ForegroundColor Red
    throw "呼吸动画适配不完整"
}

$windowCode = Get-Content "$projectRoot\Source\TheSecondSeat\UI\NarratorWindow.cs" -Raw

if ($windowCode -match "float breathingOffset = ExpressionSystem\.GetBreathingOffset") {
    Write-Host "  ? NarratorWindow 已应用呼吸偏移" -ForegroundColor Green
} else {
    Write-Host "  ? NarratorWindow 未应用呼吸偏移" -ForegroundColor Red
    throw "呼吸动画适配不完整"
}

# 3. 编译项目
Write-Host "`n[3/5] 编译项目..." -ForegroundColor Yellow
try {
    $msbuildPath = & "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe" `
        -latest -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe `
        -prerelease | Select-Object -First 1
    
    if (-not $msbuildPath) {
        throw "未找到 MSBuild"
    }
    
    & $msbuildPath "$projectRoot\Source\TheSecondSeat\TheSecondSeat.csproj" `
        /p:Configuration=Release /p:Platform=AnyCPU /v:minimal /nologo
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "  ? 编译成功" -ForegroundColor Green
    } else {
        throw "编译失败，退出码: $LASTEXITCODE"
    }
} catch {
    Write-Host "  ? 编译失败: $_" -ForegroundColor Red
    throw
}

# 4. 复制 DLL 到 Mod 目录
Write-Host "`n[4/5] 部署 DLL..." -ForegroundColor Yellow
$dllSource = "$projectRoot\Source\TheSecondSeat\bin\Release\net472\TheSecondSeat.dll"
$dllTarget = "$modPath\Assemblies\TheSecondSeat.dll"

if (Test-Path $dllSource) {
    Copy-Item $dllSource $dllTarget -Force
    Write-Host "  ? DLL 已复制到 Mod 目录" -ForegroundColor Green
    
    $dllInfo = Get-Item $dllTarget
    Write-Host "    文件大小: $([Math]::Round($dllInfo.Length / 1KB, 2)) KB" -ForegroundColor Gray
    Write-Host "    修改时间: $($dllInfo.LastWriteTime)" -ForegroundColor Gray
} else {
    Write-Host "  ? DLL 未找到: $dllSource" -ForegroundColor Red
    throw "DLL 不存在"
}

# 5. 验证功能完整性
Write-Host "`n[5/5] 验证功能完整性..." -ForegroundColor Yellow

$checksums = @{
    "ExpressionSystem" = "GetBreathingOffset"
    "NarratorScreenButton" = "breathingOffset"
    "NarratorWindow" = "UpdateBreathingTransition"
    "PortraitLoader" = "LoadLayeredPortrait"
}

$allPassed = $true
foreach ($key in $checksums.Keys) {
    $pattern = $checksums[$key]
    $file = Get-ChildItem "$projectRoot\Source\TheSecondSeat" -Recurse -Filter "*$key*.cs" | Select-Object -First 1
    
    if ($file -and (Get-Content $file.FullName -Raw) -match $pattern) {
        Write-Host "  ? $key 包含 $pattern" -ForegroundColor Green
    } else {
        Write-Host "  ? $key 缺少 $pattern" -ForegroundColor Red
        $allPassed = $false
    }
}

if (-not $allPassed) {
    throw "功能验证失败"
}

# 完成
Write-Host "`n=====================================" -ForegroundColor Cyan
Write-Host "? 部署成功！" -ForegroundColor Green
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "?? 部署内容：" -ForegroundColor Yellow
Write-Host "  ? NarratorScreenButton.cs - 呼吸动画适配" -ForegroundColor White
Write-Host "  ? NarratorWindow.cs - 呼吸动画适配" -ForegroundColor White
Write-Host "  ? ExpressionSystem.cs - 呼吸偏移计算" -ForegroundColor White
Write-Host "  ? PortraitLoader.cs - 分层立绘加载" -ForegroundColor White
Write-Host ""
Write-Host "?? 下一步：" -ForegroundColor Yellow
Write-Host "  1. 启动 RimWorld" -ForegroundColor White
Write-Host "  2. 启用 The Second Seat Mod" -ForegroundColor White
Write-Host "  3. 观察立绘呼吸动画效果" -ForegroundColor White
Write-Host ""
