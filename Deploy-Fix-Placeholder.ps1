Copy-Item "C:\Users\Administrator\Desktop\rim mod\The Second Seat\Source\TheSecondSeat\bin\Release\net472\TheSecondSeat.dll" "D:\steam\steamapps\common\RimWorld\Mods\TheSecondSeat\Assemblies\" -Force
Write-Host "`n? 占位符问题修复完成！" -ForegroundColor Green

Write-Host "`n关键修复：" -ForegroundColor Yellow
Write-Host "1. PreOpen() 完全禁用默认背景" -ForegroundColor Cyan
Write-Host "2. 直接使用 GUI.DrawTexture() 绘制" -ForegroundColor Cyan
Write-Host "3. 不调用 DrawAnimatedIcon（避免占位符）" -ForegroundColor Cyan
Write-Host "4. 正确的绘制顺序" -ForegroundColor Cyan

Write-Host "`n现在应该显示：" -ForegroundColor Yellow
Write-Host "? 128x128 自定义图标（PNG）" -ForegroundColor Green
Write-Host "? 右上角 12x12 绿色指示灯" -ForegroundColor Green
Write-Host "? 没有红/绿/黄色占位符" -ForegroundColor Green

Write-Host "`n重启游戏验证！" -ForegroundColor Cyan
