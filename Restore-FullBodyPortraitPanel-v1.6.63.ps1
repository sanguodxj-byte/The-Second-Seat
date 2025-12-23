# 完整重写 FullBodyPortraitPanel.cs v1.6.63
# 用途：创建完整的新版本文件（包含通用姿态系统）

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  创建完整的 FullBodyPortraitPanel.cs" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

$filePath = "Source\TheSecondSeat\UI\FullBodyPortraitPanel.cs"

# 确保文件存在
if (!(Test-Path $filePath)) {
    New-Item -Path $filePath -ItemType File -Force | Out-Null
}

# 从备份恢复
$backupPath = "Source\TheSecondSeat\UI\FullBodyPortraitPanel.cs.backup_v1.6.63"

if (Test-Path $backupPath) {
    Write-Host "[1/2] 从备份恢复..." -ForegroundColor Yellow
    Copy-Item $backupPath $filePath -Force
    Write-Host "  ? 已从备份恢复" -ForegroundColor Green
} else {
    Write-Host "[ERROR] 备份文件不存在：$backupPath" -ForegroundColor Red
    Write-Host "请手动参考重构指南修改文件" -ForegroundColor Yellow
    exit 1
}

Write-Host ""
Write-Host "[2/2] 验证文件..." -ForegroundColor Yellow

$content = Get-Content $filePath -Raw -Encoding UTF8

if ($content -match "通用姿态系统字段") {
    Write-Host "  ? 文件包含姿态系统代码" -ForegroundColor Green
} else {
    Write-Host "  ?? 文件未包含姿态系统（可能是旧版本备份）" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "请按照重构指南手动添加：" -ForegroundColor Cyan
    Write-Host "  1. 通用姿态系统字段（7个）" -ForegroundColor White
    Write-Host "  2. 公共接口方法（2个）" -ForegroundColor White
    Write-Host "  3. 修改 DrawPortraitContents()" -ForegroundColor White
    Write-Host "  4. 修改 DrawLayeredPortraitRuntime()" -ForegroundColor White
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "  ? 完成！" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "下一步：" -ForegroundColor Cyan
Write-Host "  1. 查看文件：code $filePath" -ForegroundColor White
Write-Host "  2. 编译验证：.\编译并部署到游戏.ps1" -ForegroundColor White
Write-Host "  3. 查看重构指南：通用姿态系统-重构指南-v1.6.63.md" -ForegroundColor White
