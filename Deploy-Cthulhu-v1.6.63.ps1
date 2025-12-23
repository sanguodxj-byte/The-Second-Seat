# 克苏鲁娘叙事者 - 一键部署脚本 v1.6.63

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  克苏鲁娘叙事者部署 v1.6.63" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# 定义路径
$gamePath = "D:\steam\steamapps\common\RimWorld\Mods\TheSecondSeat"
$cthulhuSource = "Cthulhu"
$cthulhuTarget = Join-Path $gamePath "Cthulhu"

Write-Host "[1/5] 检查前置条件..." -ForegroundColor Yellow

# 检查游戏路径
if (-not (Test-Path $gamePath)) {
    Write-Host "  ? 游戏路径不存在: $gamePath" -ForegroundColor Red
    Write-Host "  请修改脚本中的 `$gamePath 变量" -ForegroundColor Yellow
    exit 1
}
Write-Host "  ? 游戏路径存在" -ForegroundColor Green

# 检查主模组
$mainDll = Join-Path $gamePath "Assemblies\TheSecondSeat.dll"
if (-not (Test-Path $mainDll)) {
    Write-Host "  ? 主模组未安装: TheSecondSeat.dll" -ForegroundColor Red
    Write-Host "  请先部署 The Second Seat 主模组" -ForegroundColor Yellow
    exit 1
}
Write-Host "  ? 主模组已安装" -ForegroundColor Green

Write-Host ""
Write-Host "[2/5] 备份旧版本..." -ForegroundColor Yellow

if (Test-Path $cthulhuTarget) {
    $backupPath = "$cthulhuTarget-backup-$(Get-Date -Format 'yyyyMMdd-HHmmss')"
    Copy-Item -Path $cthulhuTarget -Destination $backupPath -Recurse -Force
    Write-Host "  ? 已备份到: $backupPath" -ForegroundColor Green
} else {
    Write-Host "  ? 首次安装，无需备份" -ForegroundColor Gray
}

Write-Host ""
Write-Host "[3/5] 复制 Cthulhu 文件..." -ForegroundColor Yellow

# 删除旧版本
if (Test-Path $cthulhuTarget) {
    Remove-Item -Path $cthulhuTarget -Recurse -Force
}

# 复制新版本
Copy-Item -Path $cthulhuSource -Destination $cthulhuTarget -Recurse -Force
Write-Host "  ? 文件复制完成" -ForegroundColor Green

Write-Host ""
Write-Host "[4/5] 验证部署..." -ForegroundColor Yellow

$requiredFiles = @(
    "About\About.xml",
    "Defs\NarratorPersonaDefs_Cthulhu.xml",
    "Defs\PawnKindDefs_Cthulhu.xml",
    "Defs\ThingDefs_Cthulhu.xml",
    "Defs\HediffDefs_Cthulhu.xml",
    "Languages\ChineseSimplified\Keyed\Cthulhu_Keys.xml",
    "README.md"
)

$allExists = $true
foreach ($file in $requiredFiles) {
    $fullPath = Join-Path $cthulhuTarget $file
    if (Test-Path $fullPath) {
        Write-Host "  ? $file" -ForegroundColor Green
    } else {
        Write-Host "  ? $file 不存在" -ForegroundColor Red
        $allExists = $false
    }
}

Write-Host ""
Write-Host "[5/5] 检查组件代码..." -ForegroundColor Yellow

$componentFiles = @(
    "Source\TheSecondSeat\Components\CompSpawnerFromEdge.cs",
    "Source\TheSecondSeat\Components\CompSanityAura.cs"
)

$needsCompile = $false
foreach ($file in $componentFiles) {
    if (Test-Path $file) {
        Write-Host "  ? $file" -ForegroundColor Green
    } else {
        Write-Host "  ? $file 未找到（需要编译）" -ForegroundColor Yellow
        $needsCompile = $true
    }
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan

if ($allExists -and -not $needsCompile) {
    Write-Host "  ? 部署成功！" -ForegroundColor Green
    Write-Host ""
    Write-Host "?? 下一步操作：" -ForegroundColor Cyan
    Write-Host "  1. 启动 RimWorld" -ForegroundColor White
    Write-Host "  2. 启用 Mod: The Second Seat - Cthulhu Narrator" -ForegroundColor White
    Write-Host "  3. 进入游戏，按 ~ 键打开 Dev 控制台" -ForegroundColor White
    Write-Host "  4. 测试命令：" -ForegroundColor White
    Write-Host "     NarratorDescentSystem.Instance.TriggerDescent(isHostile: false);" -ForegroundColor Gray
} elseif ($needsCompile) {
    Write-Host "  ?? 需要编译组件代码" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "?? 下一步操作：" -ForegroundColor Cyan
    Write-Host "  1. 运行编译脚本：" -ForegroundColor White
    Write-Host "     .\编译并部署到游戏.ps1" -ForegroundColor Gray
    Write-Host "  2. 然后重新运行本脚本" -ForegroundColor White
} else {
    Write-Host "  ? 部署失败，请检查错误" -ForegroundColor Red
}

Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# 显示部署统计
Write-Host "?? 部署统计：" -ForegroundColor Cyan
$totalSize = (Get-ChildItem -Path $cthulhuTarget -Recurse -File | Measure-Object -Property Length -Sum).Sum
Write-Host "  文件总大小: $([math]::Round($totalSize / 1KB, 2)) KB" -ForegroundColor White

$fileCount = (Get-ChildItem -Path $cthulhuTarget -Recurse -File).Count
Write-Host "  文件总数: $fileCount" -ForegroundColor White

Write-Host ""
Write-Host "? 完成！" -ForegroundColor Green
