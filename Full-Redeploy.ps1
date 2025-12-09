cd "C:\Users\Administrator\Desktop\rim mod\The Second Seat"
$targetDir = "D:\steam\steamapps\common\RimWorld\Mods\TheSecondSeat"

Write-Host "?? 完整清理并重新部署..." -ForegroundColor Cyan

# 1. 删除旧的 DLL
Write-Host "`n1?? 删除旧 DLL..." -ForegroundColor Yellow
Remove-Item "$targetDir\1.6\Assemblies\TheSecondSeat.dll" -Force -ErrorAction SilentlyContinue
Write-Host "? 旧 DLL 已删除" -ForegroundColor Green

# 2. 清理编译缓存
Write-Host "`n2?? 清理编译缓存..." -ForegroundColor Yellow
Remove-Item "Source\TheSecondSeat\bin" -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item "Source\TheSecondSeat\obj" -Recurse -Force -ErrorAction SilentlyContinue
Write-Host "? 缓存已清理" -ForegroundColor Green

# 3. 重新编译
Write-Host "`n3?? 编译新版本..." -ForegroundColor Yellow
dotnet build "Source\TheSecondSeat\TheSecondSeat.csproj" -c Release --nologo
if ($LASTEXITCODE -ne 0) {
    Write-Host "? 编译失败" -ForegroundColor Red
    exit 1
}
Write-Host "? 编译成功" -ForegroundColor Green

# 4. 部署新 DLL
Write-Host "`n4?? 部署新 DLL..." -ForegroundColor Yellow
Copy-Item "Source\TheSecondSeat\bin\Release\net472\TheSecondSeat.dll" "$targetDir\1.6\Assemblies\" -Force
$dll = Get-Item "$targetDir\1.6\Assemblies\TheSecondSeat.dll"
Write-Host "? 新 DLL 已部署: $($dll.Length/1KB) KB - $($dll.LastWriteTime)" -ForegroundColor Green

Write-Host "`n?? 部署完成！" -ForegroundColor Green
Write-Host "?? 关键更新：dialogueStyle 和 eventPreferences 现在是嵌套对象" -ForegroundColor Cyan
Write-Host "?? 请完全关闭并重启 RimWorld" -ForegroundColor Yellow
