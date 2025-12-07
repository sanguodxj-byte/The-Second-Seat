Write-Host "=== 最终部署 ===" -ForegroundColor Cyan

$source = "C:\Users\Administrator\Desktop\rim mod\The Second Seat\Source\TheSecondSeat\bin\Release\net472\TheSecondSeat.dll"
$dest = "D:\steam\steamapps\common\RimWorld\Mods\TheSecondSeat\Assemblies\TheSecondSeat.dll"

if (Test-Path $source) {
    Copy-Item $source $dest -Force
    Write-Host "? DLL 已复制" -ForegroundColor Green
    
    $dll = Get-Item $dest
    Write-Host "`n部署信息：" -ForegroundColor Yellow
    Write-Host "  大小: $([math]::Round($dll.Length/1KB, 2)) KB" -ForegroundColor Cyan
    Write-Host "  时间: $($dll.LastWriteTime)" -ForegroundColor Cyan
    
    Write-Host "`n? 所有修复已部署！" -ForegroundColor Green -BackgroundColor Black
    Write-Host "`n关键修复：" -ForegroundColor Yellow
    Write-Host "1. ? ESC 键不再阻塞游戏菜单" -ForegroundColor White
    Write-Host "2. ? 指示灯缩小到 6x6 像素" -ForegroundColor White
    Write-Host "3. ? 指示灯位置在右上角" -ForegroundColor White
    Write-Host "4. ? StaticConstructorOnStartup 属性" -ForegroundColor White
    Write-Host "5. ? API 模型名正确传递" -ForegroundColor White
    Write-Host "6. ? 超时时间 60 秒" -ForegroundColor White
    
    Write-Host "`n现在重启游戏测试！" -ForegroundColor Yellow
} else {
    Write-Host "? 源文件不存在" -ForegroundColor Red
}
