cd "C:\Users\Administrator\Desktop\rim mod\The Second Seat"
$targetDir = "D:\steam\steamapps\common\RimWorld\Mods\TheSecondSeat"

Write-Host "?? 修复部署位置..." -ForegroundColor Cyan

# 1. 清理所有旧 DLL
Write-Host "`n1?? 清理所有位置的旧 DLL..." -ForegroundColor Yellow
Remove-Item "$targetDir\Assemblies\TheSecondSeat.dll" -Force -ErrorAction SilentlyContinue
Remove-Item "$targetDir\1.6\Assemblies\TheSecondSeat.dll" -Force -ErrorAction SilentlyContinue
Write-Host "? 旧 DLL 已清理" -ForegroundColor Green

# 2. 编译
Write-Host "`n2?? 编译..." -ForegroundColor Yellow
Remove-Item "Source\TheSecondSeat\bin" -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item "Source\TheSecondSeat\obj" -Recurse -Force -ErrorAction SilentlyContinue
dotnet build "Source\TheSecondSeat\TheSecondSeat.csproj" -c Release --nologo
if ($LASTEXITCODE -ne 0) { Write-Host "? 编译失败" -ForegroundColor Red ; exit 1 }
Write-Host "? 编译成功" -ForegroundColor Green

# 3. 部署到两个位置（确保覆盖）
Write-Host "`n3?? 部署 DLL 到所有位置..." -ForegroundColor Yellow
New-Item -ItemType Directory -Path "$targetDir\Assemblies" -Force | Out-Null
New-Item -ItemType Directory -Path "$targetDir\1.6\Assemblies" -Force | Out-Null
Copy-Item "Source\TheSecondSeat\bin\Release\net472\TheSecondSeat.dll" "$targetDir\Assemblies\" -Force
Copy-Item "Source\TheSecondSeat\bin\Release\net472\TheSecondSeat.dll" "$targetDir\1.6\Assemblies\" -Force
Write-Host "? DLL 已部署到根目录和 1.6 目录" -ForegroundColor Green

# 4. 验证
Write-Host "`n4?? 验证部署..." -ForegroundColor Yellow
$dll1 = Get-Item "$targetDir\Assemblies\TheSecondSeat.dll" -ErrorAction SilentlyContinue
$dll2 = Get-Item "$targetDir\1.6\Assemblies\TheSecondSeat.dll" -ErrorAction SilentlyContinue
if ($dll1) { Write-Host "? 根目录: $($dll1.Length/1KB) KB - $($dll1.LastWriteTime)" -ForegroundColor Green }
if ($dll2) { Write-Host "? 1.6目录: $($dll2.Length/1KB) KB - $($dll2.LastWriteTime)" -ForegroundColor Green }

Write-Host "`n?? 部署完成！请重启 RimWorld" -ForegroundColor Cyan
