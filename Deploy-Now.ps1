# Deploy-Now.ps1
# 快速部署脚本 - 头像表情系统修复

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  The Second Seat - 快速部署" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

# 路径配置
$SourceDir = $PSScriptRoot
$TargetDir = "D:\steam\steamapps\common\RimWorld\Mods\TheSecondSeat"

Write-Host "`n[1/4] 检查源目录..." -ForegroundColor Yellow
if (-not (Test-Path $SourceDir)) {
    Write-Host "错误: 源目录不存在!" -ForegroundColor Red
    exit 1
}
Write-Host "源目录: $SourceDir" -ForegroundColor Green

Write-Host "`n[2/4] 创建目标目录..." -ForegroundColor Yellow
if (-not (Test-Path $TargetDir)) {
    New-Item -ItemType Directory -Path $TargetDir -Force | Out-Null
    Write-Host "已创建: $TargetDir" -ForegroundColor Green
} else {
    Write-Host "目标目录已存在" -ForegroundColor Green
}

Write-Host "`n[3/4] 复制文件..." -ForegroundColor Yellow

# 要复制的文件夹
$Folders = @(
    "About",
    "Assemblies", 
    "Defs",
    "Languages",
    "Textures",
    "Emoticons"
)

foreach ($folder in $Folders) {
    $src = Join-Path $SourceDir $folder
    $dst = Join-Path $TargetDir $folder
    
    if (Test-Path $src) {
        if (Test-Path $dst) {
            Remove-Item $dst -Recurse -Force
        }
        Copy-Item $src $dst -Recurse -Force
        Write-Host "  ? $folder" -ForegroundColor Green
    }
}

# 复制根目录文件
$RootFiles = @("LoadFolders.xml")
foreach ($file in $RootFiles) {
    $src = Join-Path $SourceDir $file
    if (Test-Path $src) {
        Copy-Item $src $TargetDir -Force
        Write-Host "  ? $file" -ForegroundColor Green
    }
}

Write-Host "`n[4/4] 验证部署..." -ForegroundColor Yellow

$RequiredFiles = @(
    "About\About.xml",
    "Assemblies\TheSecondSeat.dll",
    "Defs\NarratorPersonaDefs.xml"
)

$AllGood = $true
foreach ($file in $RequiredFiles) {
    $path = Join-Path $TargetDir $file
    if (Test-Path $path) {
        Write-Host "  ? $file" -ForegroundColor Green
    } else {
        Write-Host "  ? $file 缺失!" -ForegroundColor Red
        $AllGood = $false
    }
}

Write-Host "`n========================================" -ForegroundColor Cyan
if ($AllGood) {
    Write-Host "  部署成功!" -ForegroundColor Green
    Write-Host "  请重启 RimWorld 测试" -ForegroundColor Green
} else {
    Write-Host "  部署可能不完整" -ForegroundColor Yellow
    Write-Host "  请检查 Assemblies 文件夹" -ForegroundColor Yellow
}
Write-Host "========================================" -ForegroundColor Cyan
