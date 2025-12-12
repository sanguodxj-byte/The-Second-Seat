# ========================================
# The Second Seat - 完整部署脚本 v1.6.17
# ========================================

$ErrorActionPreference = "Stop"
$sourceDir = "C:\Users\Administrator\Desktop\rim mod\The Second Seat"
$targetDir = "D:\steam\steamapps\common\RimWorld\Mods\TheSecondSeat"

Write-Host "`n=====================================" -ForegroundColor Cyan
Write-Host "  The Second Seat - Mod 部署工具" -ForegroundColor Cyan
Write-Host "  版本: v1.6.17" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan

# 步骤1: 检查源目录
Write-Host "`n[1/8] 检查源目录..." -ForegroundColor Yellow
if (-not (Test-Path $sourceDir)) {
    Write-Host "? 源目录不存在: $sourceDir" -ForegroundColor Red
    exit 1
}
Write-Host "? 源目录存在" -ForegroundColor Green

# 步骤2: 检查/创建目标目录
Write-Host "`n[2/8] 检查目标目录..." -ForegroundColor Yellow
if (-not (Test-Path $targetDir)) {
    Write-Host "? 目标目录不存在，正在创建..." -ForegroundColor Yellow
    New-Item -ItemType Directory -Path $targetDir -Force | Out-Null
    New-Item -ItemType Directory -Path "$targetDir\1.6\Assemblies" -Force | Out-Null
    New-Item -ItemType Directory -Path "$targetDir\1.6\Defs" -Force | Out-Null
    New-Item -ItemType Directory -Path "$targetDir\1.6\Languages" -Force | Out-Null
    New-Item -ItemType Directory -Path "$targetDir\Textures" -Force | Out-Null
}
Write-Host "? 目标目录准备完成" -ForegroundColor Green

# 步骤3: 清理旧文件
Write-Host "`n[3/8] 清理旧文件..." -ForegroundColor Yellow
Remove-Item "$targetDir\1.6\Assemblies\TheSecondSeat.dll" -Force -ErrorAction SilentlyContinue
Remove-Item "$targetDir\1.6\Assemblies\Newtonsoft.Json.dll" -Force -ErrorAction SilentlyContinue
Write-Host "? 旧DLL已清理" -ForegroundColor Green

# 步骤4: 清理编译缓存
Write-Host "`n[4/8] 清理编译缓存..." -ForegroundColor Yellow
Set-Location $sourceDir
Remove-Item "Source\TheSecondSeat\bin" -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item "Source\TheSecondSeat\obj" -Recurse -Force -ErrorAction SilentlyContinue
Write-Host "? 编译缓存已清理" -ForegroundColor Green

# 步骤5: 编译项目
Write-Host "`n[5/8] 编译项目..." -ForegroundColor Yellow
$buildOutput = dotnet build "Source\TheSecondSeat\TheSecondSeat.csproj" -c Release --nologo 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host "? 编译失败！" -ForegroundColor Red
    Write-Host $buildOutput -ForegroundColor Red
    exit 1
}
Write-Host "? 编译成功" -ForegroundColor Green

# 步骤6: 复制DLL文件
Write-Host "`n[6/8] 部署DLL文件..." -ForegroundColor Yellow

# 复制主DLL
Copy-Item "Source\TheSecondSeat\bin\Release\net472\TheSecondSeat.dll" "$targetDir\1.6\Assemblies\" -Force
$dll = Get-Item "$targetDir\1.6\Assemblies\TheSecondSeat.dll"
Write-Host "? 主DLL: $($dll.Length/1KB) KB - $($dll.LastWriteTime)" -ForegroundColor Green

# 复制Newtonsoft.Json.dll（如果存在）
if (Test-Path "Source\TheSecondSeat\bin\Release\net472\Newtonsoft.Json.dll") {
    Copy-Item "Source\TheSecondSeat\bin\Release\net472\Newtonsoft.Json.dll" "$targetDir\1.6\Assemblies\" -Force
    Write-Host "? Newtonsoft.Json.dll 已复制" -ForegroundColor Green
}

# 步骤7: 复制其他资源
Write-Host "`n[7/8] 部署资源文件..." -ForegroundColor Yellow

# 复制About.xml
if (Test-Path "About\About.xml") {
    Copy-Item "About\About.xml" "$targetDir\" -Force
    Write-Host "? About.xml" -ForegroundColor Green
}

# 复制LoadFolders.xml
if (Test-Path "LoadFolders.xml") {
    Copy-Item "LoadFolders.xml" "$targetDir\" -Force
    Write-Host "? LoadFolders.xml" -ForegroundColor Green
}

# 复制Defs文件夹
if (Test-Path "Defs") {
    Copy-Item "Defs\*" "$targetDir\1.6\Defs\" -Recurse -Force
    $defCount = (Get-ChildItem "$targetDir\1.6\Defs\*.xml" -Recurse).Count
    Write-Host "? Defs: $defCount 个文件" -ForegroundColor Green
}

# 复制Languages文件夹
if (Test-Path "Languages") {
    # 先删除旧的Languages文件夹
    Remove-Item "$targetDir\1.6\Languages" -Recurse -Force -ErrorAction SilentlyContinue
    # 复制新的Languages文件夹
    Copy-Item "Languages" "$targetDir\1.6\Languages" -Recurse -Force
    Write-Host "? Languages" -ForegroundColor Green
}

# 复制Textures文件夹
if (Test-Path "Textures") {
    Copy-Item "Textures\*" "$targetDir\Textures\" -Recurse -Force
    $textureCount = (Get-ChildItem "$targetDir\Textures\*" -Recurse -File).Count
    Write-Host "? Textures: $textureCount 个文件" -ForegroundColor Green
}

# 复制Materials文件夹（如果存在）
if (Test-Path "Materials") {
    Copy-Item "Materials\*" "$targetDir\Materials\" -Recurse -Force
    Write-Host "? Materials" -ForegroundColor Green
}

# 复制Sounds文件夹（如果存在）
if (Test-Path "Sounds") {
    Copy-Item "Sounds\*" "$targetDir\Sounds\" -Recurse -Force
    Write-Host "? Sounds" -ForegroundColor Green
}

# 步骤8: 验证部署
Write-Host "`n[8/8] 验证部署..." -ForegroundColor Yellow

$success = $true

# 检查主DLL
if (-not (Test-Path "$targetDir\1.6\Assemblies\TheSecondSeat.dll")) {
    Write-Host "? 主DLL未找到" -ForegroundColor Red
    $success = $false
} else {
    Write-Host "? 主DLL存在" -ForegroundColor Green
}

# 检查About.xml
if (-not (Test-Path "$targetDir\About.xml")) {
    Write-Host "? About.xml未找到" -ForegroundColor Red
    $success = $false
} else {
    Write-Host "? About.xml存在" -ForegroundColor Green
}

# 检查Defs
$defFiles = Get-ChildItem "$targetDir\1.6\Defs\*.xml" -Recurse -ErrorAction SilentlyContinue
if ($defFiles.Count -eq 0) {
    Write-Host "? 没有找到Defs文件" -ForegroundColor Yellow
} else {
    Write-Host "? Defs: $($defFiles.Count) 个文件" -ForegroundColor Green
}

# 最终报告
Write-Host "`n=====================================" -ForegroundColor Cyan
if ($success) {
    Write-Host "? 部署完成！" -ForegroundColor Green
    Write-Host "`n?? 目标位置:" -ForegroundColor Cyan
    Write-Host "   $targetDir" -ForegroundColor White
    Write-Host "`n?? 部署内容:" -ForegroundColor Cyan
    Write-Host "   - 主DLL: $(($dll.Length/1KB).ToString('F2')) KB" -ForegroundColor White
    Write-Host "   - Defs: $defCount 个文件" -ForegroundColor White
    Write-Host "   - 纹理: $textureCount 个文件" -ForegroundColor White
    Write-Host "`n??  下一步操作:" -ForegroundColor Yellow
    Write-Host "   1. 完全关闭RimWorld游戏" -ForegroundColor White
    Write-Host "   2. 重新启动RimWorld" -ForegroundColor White
    Write-Host "   3. 在Mod列表中启用 'The Second Seat'" -ForegroundColor White
    Write-Host "   4. 重启游戏以加载Mod" -ForegroundColor White
} else {
    Write-Host "? 部署失败，请检查错误信息" -ForegroundColor Red
}
Write-Host "=====================================" -ForegroundColor Cyan

# 询问是否打开目标文件夹
Write-Host "`n是否打开目标文件夹? (Y/N): " -NoNewline -ForegroundColor Cyan
$openFolder = Read-Host
if ($openFolder -eq "Y" -or $openFolder -eq "y") {
    Start-Process "explorer.exe" -ArgumentList $targetDir
}

Write-Host "`n部署脚本执行完毕！" -ForegroundColor Green
