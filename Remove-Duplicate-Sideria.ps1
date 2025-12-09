Write-Host "?? 删除重复的 Sideria.xml 文件..." -ForegroundColor Cyan

$sideriaXml = "D:\steam\steamapps\common\RimWorld\Mods\TheSecondSeat\Defs\Sideria.xml"

if (Test-Path $sideriaXml) {
    Write-Host "`n?? 找到重复文件: Sideria.xml" -ForegroundColor Yellow
    Write-Host "该文件与 NarratorPersonaDefs.xml 中的 Sideria_Default 定义冲突" -ForegroundColor Yellow
    
    # 删除文件
    Remove-Item $sideriaXml -Force
    Write-Host "? Sideria.xml 已删除" -ForegroundColor Green
} else {
    Write-Host "? Sideria.xml 不存在（已经被删除）" -ForegroundColor Green
}

# 验证最终状态
Write-Host "`n?? Defs 目录最终文件列表:" -ForegroundColor Cyan
$defsDir = "D:\steam\steamapps\common\RimWorld\Mods\TheSecondSeat\Defs"
Get-ChildItem $defsDir -Filter "*.xml" | ForEach-Object {
    Write-Host "  ? $($_.Name)" -ForegroundColor Green
}

Write-Host "`n?? 清理完成！" -ForegroundColor Green
Write-Host "现在 Sideria_Default 只在 NarratorPersonaDefs.xml 中定义一次" -ForegroundColor Cyan
Write-Host "`n?? 请重启 RimWorld 测试" -ForegroundColor Yellow
