# 快速部署翻译修复和 TTS 功能
$ModDir = "D:\steam\steamapps\common\RimWorld\Mods\TheSecondSeat"
$SourceDir = "."

Write-Host "=====================================" -ForegroundColor Cyan
Write-Host "  翻译修复和 TTS 功能部署脚本" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host ""

# 1. 复制 DLL
Write-Host "[1/3] 复制 DLL 文件..." -ForegroundColor Yellow
try {
    Copy-Item "$SourceDir\Source\TheSecondSeat\bin\Release\net472\TheSecondSeat.dll" "$ModDir\Assemblies\TheSecondSeat.dll" -Force
    Write-Host "? DLL 复制成功" -ForegroundColor Green
}
catch {
    Write-Host "? DLL 复制失败: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "请确保 RimWorld 已关闭" -ForegroundColor Yellow
    exit 1
}

# 2. 复制中文翻译
Write-Host "[2/3] 复制中文翻译文件..." -ForegroundColor Yellow
$translationDir = "$ModDir\Languages\ChineseSimplified\Keyed"
if (!(Test-Path $translationDir)) {
    New-Item -ItemType Directory -Path $translationDir -Force | Out-Null
}
Copy-Item "$SourceDir\Languages\ChineseSimplified\Keyed\TheSecondSeat_Keys.xml" "$translationDir\TheSecondSeat_Keys.xml" -Force
Write-Host "? 翻译文件复制成功" -ForegroundColor Green

# 3. 复制英文翻译（备用）
Write-Host "[3/3] 复制英文翻译文件..." -ForegroundColor Yellow
$englishDir = "$ModDir\Languages\English\Keyed"
if (!(Test-Path $englishDir)) {
    New-Item -ItemType Directory -Path $englishDir -Force | Out-Null
}
Copy-Item "$SourceDir\Languages\English\Keyed\TheSecondSeat_Keys.xml" "$englishDir\TheSecondSeat_Keys.xml" -Force
Write-Host "? 英文翻译文件复制成功" -ForegroundColor Green

Write-Host ""
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host "  部署完成！" -ForegroundColor Green
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "新增功能:" -ForegroundColor Yellow
Write-Host "  ? TTS 语音合成功能" -ForegroundColor White
Write-Host "  ? 设置界面折叠优化" -ForegroundColor White
Write-Host "  ? 中文翻译修复" -ForegroundColor White
Write-Host ""
Write-Host "请重启 RimWorld 以应用更改" -ForegroundColor Cyan
