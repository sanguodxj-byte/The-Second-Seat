# 完整部署脚本 - 包括DLL和材质文件

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host " The Second Seat - 完整部署" -ForegroundColor Yellow
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

$ErrorActionPreference = "Stop"

# 目标Mod目录
$targetModDir = "D:\steam\steamapps\common\RimWorld\Mods\TheSecondSeat"

if (-not (Test-Path $targetModDir)) {
    Write-Host "? 错误: 找不到Mod目录: $targetModDir" -ForegroundColor Red
    Write-Host "请确认RimWorld安装路径是否正确" -ForegroundColor Yellow
    exit 1
}

Write-Host "目标Mod目录: $targetModDir" -ForegroundColor Gray
Write-Host ""

# 1. 部署DLL
Write-Host "1. 部署DLL..." -ForegroundColor Cyan
$dllSource = "Source\TheSecondSeat\bin\Release\net472\TheSecondSeat.dll"
$dllTarget = Join-Path $targetModDir "Assemblies"

if (Test-Path $dllSource) {
    if (-not (Test-Path $dllTarget)) {
        New-Item -Path $dllTarget -ItemType Directory -Force | Out-Null
    }
    
    Copy-Item $dllSource -Destination $dllTarget -Force
    $dllSize = (Get-Item $dllSource).Length / 1MB
    Write-Host "  ? TheSecondSeat.dll ($([math]::Round($dllSize, 2)) MB)" -ForegroundColor Green
} else {
    Write-Host "  ? 找不到DLL: $dllSource" -ForegroundColor Red
    Write-Host "  请先编译项目: dotnet build -c Release" -ForegroundColor Yellow
    exit 1
}

Write-Host ""

# 2. 部署材质文件
Write-Host "2. 部署材质文件..." -ForegroundColor Cyan

$textureSource = "Textures"
$textureTarget = Join-Path $targetModDir "Textures"

# 创建目标目录结构
$dirs = @(
    "UI\Narrators\9x16\Sideria",
    "UI\Narrators\9x16\Sideria\Outfits",
    "UI\Narrators\9x16\Expressions\Sideria"
)

foreach ($dir in $dirs) {
    $fullPath = Join-Path $textureTarget $dir
    if (-not (Test-Path $fullPath)) {
        New-Item -Path $fullPath -ItemType Directory -Force | Out-Null
        Write-Host "  ?? 创建目录: $dir" -ForegroundColor Gray
    }
}

# 复制基础立绘
Write-Host ""
Write-Host "  ?? 基础立绘:" -ForegroundColor White
$basePortrait = "Textures\UI\Narrators\9x16\Sideria\base.png"
if (Test-Path $basePortrait) {
    $targetPath = Join-Path $textureTarget "UI\Narrators\9x16\Sideria\base.png"
    Copy-Item $basePortrait -Destination $targetPath -Force
    $size = (Get-Item $basePortrait).Length / 1MB
    Write-Host "    ? base.png ($([math]::Round($size, 2)) MB)" -ForegroundColor Green
} else {
    Write-Host "    ?? base.png 不存在" -ForegroundColor Yellow
}

# 复制表情差分
Write-Host ""
Write-Host "  ?? 表情差分:" -ForegroundColor White
$expressionsSource = "Textures\UI\Narrators\9x16\Expressions\Sideria"
$expressionsTarget = Join-Path $textureTarget "UI\Narrators\9x16\Expressions\Sideria"

$expressionFiles = @("happy", "sad", "angry", "surprised", "thoughtful", "annoyed", "smug")
$copiedCount = 0

foreach ($expr in $expressionFiles) {
    $pngFile = Join-Path $expressionsSource "$expr.png"
    $jpgFile = Join-Path $expressionsSource "$expr.jpg"
    
    if (Test-Path $pngFile) {
        Copy-Item $pngFile -Destination $expressionsTarget -Force
        $size = (Get-Item $pngFile).Length / 1MB
        Write-Host "    ? $expr.png ($([math]::Round($size, 2)) MB)" -ForegroundColor Green
        $copiedCount++
    } elseif (Test-Path $jpgFile) {
        Copy-Item $jpgFile -Destination $expressionsTarget -Force
        $size = (Get-Item $jpgFile).Length / 1MB
        Write-Host "    ? $expr.jpg ($([math]::Round($size, 2)) MB)" -ForegroundColor Green
        $copiedCount++
    }
}

if ($copiedCount -eq 0) {
    Write-Host "    ?? 没有找到表情文件" -ForegroundColor Yellow
}

# 复制服装差分
Write-Host ""
Write-Host "  ?? 服装差分:" -ForegroundColor White
$outfitsSource = "Textures\UI\Narrators\9x16\Sideria\Outfits"
$outfitsTarget = Join-Path $textureTarget "UI\Narrators\9x16\Sideria\Outfits"

$outfitFiles = @("neutral_1", "warm_1", "intimate_1", "devoted_1", "devoted_2")
$copiedCount = 0

foreach ($outfit in $outfitFiles) {
    $pngFile = Join-Path $outfitsSource "$outfit.png"
    
    if (Test-Path $pngFile) {
        Copy-Item $pngFile -Destination $outfitsTarget -Force
        $size = (Get-Item $pngFile).Length / 1MB
        Write-Host "    ? $outfit.png ($([math]::Round($size, 2)) MB)" -ForegroundColor Green
        $copiedCount++
    }
}

if ($copiedCount -eq 0) {
    Write-Host "    ?? 没有找到服装文件" -ForegroundColor Yellow
}

# 复制README文件
Write-Host ""
Write-Host "  ?? 文档文件:" -ForegroundColor White
$readmeFiles = @(
    "Textures\UI\Narrators\9x16\README.md",
    "Textures\UI\Narrators\9x16\Sideria\README.md",
    "Textures\UI\Narrators\9x16\Sideria\Outfits\README.md",
    "Textures\UI\Narrators\9x16\Expressions\Sideria\README.md"
)

foreach ($readme in $readmeFiles) {
    if (Test-Path $readme) {
        $relativePath = $readme.Replace("Textures\", "")
        $targetPath = Join-Path $textureTarget $relativePath
        Copy-Item $readme -Destination $targetPath -Force
        Write-Host "    ? $(Split-Path $readme -Leaf)" -ForegroundColor Green
    }
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host " 部署完成" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

Write-Host "?? 部署统计:" -ForegroundColor White
Write-Host "  ? DLL: 1 个文件" -ForegroundColor Gray
Write-Host "  ? 立绘: $(if (Test-Path $basePortrait) { '1' } else { '0' }) 个文件" -ForegroundColor Gray
Write-Host "  ? 表情: $copiedCount 个文件" -ForegroundColor Gray
Write-Host "  ? 服装: $copiedCount 个文件" -ForegroundColor Gray
Write-Host ""

Write-Host "?? 下一步:" -ForegroundColor Yellow
Write-Host "  1. 启动 RimWorld" -ForegroundColor White
Write-Host "  2. 开始游戏或加载存档" -ForegroundColor White
Write-Host "  3. 打开 AI 对话窗口" -ForegroundColor White
Write-Host "  4. 测试表情切换和立绘自动分割" -ForegroundColor White
Write-Host ""

Write-Host "?? 监控日志关键词:" -ForegroundColor Yellow
Write-Host "  ? [PortraitLoader] ? 自动分割合成" -ForegroundColor Gray
Write-Host "  ? [PortraitSplitter] ? 合成完成" -ForegroundColor Gray
Write-Host "  ? [ExpressionSystem] 表情切换" -ForegroundColor Gray
Write-Host "  ? [OutfitSystem] 服装更换" -ForegroundColor Gray
Write-Host ""

Write-Host "? 准备就绪！" -ForegroundColor Magenta
Write-Host ""
