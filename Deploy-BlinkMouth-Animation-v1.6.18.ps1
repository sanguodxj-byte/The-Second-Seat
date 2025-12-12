# ================================================================
# 眨眼和张嘴动画系统部署脚本 v1.6.18
# ================================================================
# 功能：部署眨眼动画和张嘴动画系统到分层立绘
# 日期：2025-01-XX
# ================================================================

Write-Host "=====================================" -ForegroundColor Cyan
Write-Host "眨眼和张嘴动画系统部署 v1.6.18" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host ""

$ErrorActionPreference = "Stop"
$projectRoot = "C:\Users\Administrator\Desktop\rim mod\The Second Seat"
$modPath = "D:\steam\steamapps\common\RimWorld\Mods\TheSecondSeat"

# 1. 验证源文件
Write-Host "[1/6] 验证源文件..." -ForegroundColor Yellow
$sourceFiles = @(
    "$projectRoot\Source\TheSecondSeat\PersonaGeneration\BlinkAnimationSystem.cs",
    "$projectRoot\Source\TheSecondSeat\PersonaGeneration\MouthAnimationSystem.cs",
    "$projectRoot\Source\TheSecondSeat\PersonaGeneration\LayerDefinition.cs",
    "$projectRoot\Source\TheSecondSeat\PersonaGeneration\LayeredPortraitCompositor.cs",
    "$projectRoot\Source\TheSecondSeat\UI\NarratorScreenButton.cs",
    "$projectRoot\Source\TheSecondSeat\UI\NarratorWindow.cs"
)

foreach ($file in $sourceFiles) {
    if (Test-Path $file) {
        Write-Host "  ? $([System.IO.Path]::GetFileName($file))" -ForegroundColor Green
    } else {
        Write-Host "  ? 缺失: $file" -ForegroundColor Red
        throw "源文件缺失"
    }
}

# 2. 验证动画系统代码
Write-Host "`n[2/6] 验证动画系统代码..." -ForegroundColor Yellow

$blinkCode = Get-Content "$projectRoot\Source\TheSecondSeat\PersonaGeneration\BlinkAnimationSystem.cs" -Raw
if ($blinkCode -match "GetBlinkLayerName") {
    Write-Host "  ? BlinkAnimationSystem 包含核心方法" -ForegroundColor Green
} else {
    Write-Host "  ? BlinkAnimationSystem 代码不完整" -ForegroundColor Red
    throw "眨眼系统代码不完整"
}

$mouthCode = Get-Content "$projectRoot\Source\TheSecondSeat\PersonaGeneration\MouthAnimationSystem.cs" -Raw
if ($mouthCode -match "GetMouthLayerName") {
    Write-Host "  ? MouthAnimationSystem 包含核心方法" -ForegroundColor Green
} else {
    Write-Host "  ? MouthAnimationSystem 代码不完整" -ForegroundColor Red
    throw "张嘴系统代码不完整"
}

$layerDefCode = Get-Content "$projectRoot\Source\TheSecondSeat\PersonaGeneration\LayerDefinition.cs" -Raw
if ($layerDefCode -match "Eyes" -and $layerDefCode -match "Mouth") {
    Write-Host "  ? LayerDefinition 包含 Eyes 和 Mouth 层类型" -ForegroundColor Green
} else {
    Write-Host "  ? LayerDefinition 未添加新层类型" -ForegroundColor Red
    throw "层定义不完整"
}

$compositorCode = Get-Content "$projectRoot\Source\TheSecondSeat\PersonaGeneration\LayeredPortraitCompositor.cs" -Raw
if ($compositorCode -match "ApplyAnimationLayers") {
    Write-Host "  ? LayeredPortraitCompositor 包含动画适配" -ForegroundColor Green
} else {
    Write-Host "  ? LayeredPortraitCompositor 未适配动画" -ForegroundColor Red
    throw "合成器未适配动画"
}

# 3. 编译项目
Write-Host "`n[3/6] 编译项目..." -ForegroundColor Yellow
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
Write-Host "`n[4/6] 部署 DLL..." -ForegroundColor Yellow
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

# 5. 创建纹理模板文件夹
Write-Host "`n[5/6] 创建纹理模板文件夹..." -ForegroundColor Yellow
$textureBasePath = "$modPath\Textures\UI\Narrators\9x16\Layered"

# 为 Sideria 创建示例文件夹
$sideriaPath = "$textureBasePath\Sideria"
if (-not (Test-Path $sideriaPath)) {
    New-Item -ItemType Directory -Path $sideriaPath -Force | Out-Null
    Write-Host "  ? 创建文件夹: Sideria" -ForegroundColor Green
} else {
    Write-Host "  ? 文件夹已存在: Sideria" -ForegroundColor Gray
}

# 创建 README 文件
$readmeContent = @"
# 分层立绘纹理资源说明 v1.6.18

## ?? 文件结构

每个人格需要以下纹理文件（尺寸：1024x1574）：

### 必需文件
- base_body.png          ← 身体层
- face_neutral.png       ← 默认表情
- eyes_open.png          ← 睁眼层
- eyes_half.png          ← 半闭眼层
- eyes_closed.png        ← 闭眼层
- mouth_closed.png       ← 闭嘴层
- mouth_smile.png        ← 微笑层
- mouth_open_small.png   ← 小张嘴层
- mouth_open_wide.png    ← 大张嘴层

### 可选文件
- face_happy.png         ← 开心表情
- face_sad.png           ← 悲伤表情
- face_angry.png         ← 生气表情
- outfit_default.png     ← 默认服装
- mouth_frown.png        ← 皱眉嘴型

## ?? 制作要求

1. **尺寸：** 1024×1574 像素
2. **格式：** PNG（透明背景）
3. **对齐：** 眼睛/嘴巴层必须与 face_neutral.png 像素级对齐
4. **文件名：** 全小写，无空格

## ?? 详细文档

参见：眨眼和张嘴动画-纹理资源模板-v1.6.18.md
"@

$readmePath = "$textureBasePath\README.txt"
Set-Content -Path $readmePath -Value $readmeContent -Encoding UTF8
Write-Host "  ? 创建 README.txt" -ForegroundColor Green

# 6. 验证功能完整性
Write-Host "`n[6/6] 验证功能完整性..." -ForegroundColor Yellow

$checksums = @{
    "BlinkAnimationSystem" = "GetBlinkLayerName"
    "MouthAnimationSystem" = "GetMouthLayerName"
    "LayerDefinition" = "Eyes.*Mouth"
    "LayeredPortraitCompositor" = "ApplyAnimationLayers"
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
Write-Host "  ? BlinkAnimationSystem.cs - 眨眼动画系统" -ForegroundColor White
Write-Host "  ? MouthAnimationSystem.cs - 张嘴动画系统" -ForegroundColor White
Write-Host "  ? LayerDefinition.cs - 添加 Eyes/Mouth 层类型" -ForegroundColor White
Write-Host "  ? LayeredPortraitCompositor.cs - 动画适配" -ForegroundColor White
Write-Host ""
Write-Host "?? 下一步：" -ForegroundColor Yellow
Write-Host "  1. 准备纹理文件（eyes_open/half/closed, mouth_*）" -ForegroundColor White
Write-Host "  2. 放入 Textures/UI/Narrators/9x16/Layered/{Persona}/" -ForegroundColor White
Write-Host "  3. 启动 RimWorld" -ForegroundColor White
Write-Host "  4. 观察眨眼和张嘴动画效果" -ForegroundColor White
Write-Host ""
Write-Host "?? 参考文档：" -ForegroundColor Yellow
Write-Host "  ? 眨眼和张嘴动画-纹理资源模板-v1.6.18.md" -ForegroundColor White
Write-Host "  ? 眨眼和张嘴动画系统-完整实现报告-v1.6.18.md" -ForegroundColor White
Write-Host ""
