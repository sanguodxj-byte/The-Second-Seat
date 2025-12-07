# 智能部署脚本 - 保护立绘文件
# 只复制 DLL 和必要文件，不覆盖 Textures/UI/Narrators/ 中的真实立绘

$ErrorActionPreference = "Stop"

Write-Host "`n=== 智能部署（保护立绘）===" -ForegroundColor Cyan

$projectRoot = "C:\Users\Administrator\Desktop\rim mod\The Second Seat"
$gameMod = "D:\steam\steamapps\common\RimWorld\Mods\TheSecondSeat"

# 1. 编译 DLL
Write-Host "`n?? 步骤 1: 编译 DLL..." -ForegroundColor Yellow

Push-Location "$projectRoot\Source\TheSecondSeat"
try {
    $result = dotnet build -c Release --nologo 2>&1 | Select-String -Pattern "error|成功|成功" | Select-Object -First 3
    Write-Host $result
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host "? 编译失败" -ForegroundColor Red
        exit 1
    }
} finally {
    Pop-Location
}

# 2. 复制 DLL
Write-Host "`n?? 步骤 2: 复制 DLL..." -ForegroundColor Yellow

$sourceDll = "$projectRoot\Source\TheSecondSeat\bin\Release\net472\TheSecondSeat.dll"
$targetDll = "$gameMod\Assemblies\TheSecondSeat.dll"

if (Test-Path $sourceDll) {
    Copy-Item $sourceDll -Destination $targetDll -Force
    $size = [math]::Round((Get-Item $targetDll).Length / 1KB, 2)
    Write-Host "? DLL 已复制 ($size KB)" -ForegroundColor Green
} else {
    Write-Host "? 源 DLL 不存在" -ForegroundColor Red
    exit 1
}

# 3. 复制翻译文件
Write-Host "`n?? 步骤 3: 复制翻译文件..." -ForegroundColor Yellow

Copy-Item "$projectRoot\Languages" -Destination "$gameMod\Languages" -Recurse -Force
Write-Host "? 翻译文件已更新" -ForegroundColor Green

# 4. 智能复制 Textures（跳过立绘）
Write-Host "`n?? 步骤 4: 智能复制纹理（保护立绘）..." -ForegroundColor Yellow

# 只复制 UI 文件夹（不含 Narrators）
$uiSourcePath = "$projectRoot\Textures\UI"
$uiTargetPath = "$gameMod\Textures\UI"

if (Test-Path $uiSourcePath) {
    # 获取所有 UI 子文件夹（排除 Narrators）
    $uiSubFolders = Get-ChildItem -Path $uiSourcePath -Directory | Where-Object { $_.Name -ne "Narrators" }
    
    foreach ($folder in $uiSubFolders) {
        $dest = Join-Path $uiTargetPath $folder.Name
        Copy-Item $folder.FullName -Destination $dest -Recurse -Force
        Write-Host "  ? UI/$($folder.Name)" -ForegroundColor Green
    }
    
    # 复制 UI 根目录的文件（如果有）
    $uiFiles = Get-ChildItem -Path $uiSourcePath -File
    foreach ($file in $uiFiles) {
        Copy-Item $file.FullName -Destination $uiTargetPath -Force
        Write-Host "  ? UI/$($file.Name)" -ForegroundColor Green
    }
}

# 检查立绘文件夹
$narratorsPath = "$gameMod\Textures\UI\Narrators"
if (Test-Path $narratorsPath) {
    $realPortraits = Get-ChildItem -Path $narratorsPath -Filter "*.png" | Where-Object { 
        $_.Length -gt 10KB  # 真实立绘通常大于 10KB
    }
    
    if ($realPortraits.Count -gt 0) {
        Write-Host "`n? 检测到 $($realPortraits.Count) 个真实立绘，已保护：" -ForegroundColor Green
        foreach ($portrait in $realPortraits) {
            $sizeMB = [math]::Round($portrait.Length / 1MB, 2)
            Write-Host "  ?? $($portrait.Name) ($sizeMB MB)" -ForegroundColor Cyan
        }
    } else {
        Write-Host "`n?? 未检测到真实立绘（只有占位符）" -ForegroundColor Yellow
        Write-Host "  ?? 请将真实立绘放入：$narratorsPath" -ForegroundColor White
    }
} else {
    Write-Host "`n?? 立绘文件夹不存在：$narratorsPath" -ForegroundColor Yellow
}

# 5. 总结
Write-Host "`n" + "="*60 -ForegroundColor Cyan
Write-Host "? 智能部署完成！" -ForegroundColor Green -BackgroundColor Black
Write-Host "="*60 -ForegroundColor Cyan

Write-Host "`n?? 已更新内容：" -ForegroundColor Yellow
Write-Host "  ? DLL 程序集" -ForegroundColor Green
Write-Host "  ? 翻译文件（中英文）" -ForegroundColor Green
Write-Host "  ? UI 纹理（按钮、图标等）" -ForegroundColor Green
Write-Host "  ? 占位符纹理" -ForegroundColor Green

Write-Host "`n??? 已保护内容：" -ForegroundColor Yellow
Write-Host "  ? Textures/UI/Narrators/ 中的真实立绘" -ForegroundColor Green

Write-Host "`n?? 下一步：" -ForegroundColor Cyan
Write-Host "  1. 重启 RimWorld" -ForegroundColor White
Write-Host "  2. 检查立绘是否显示正常" -ForegroundColor White
Write-Host "  3. 测试新功能（难度调整、表情包等）" -ForegroundColor White

Write-Host "`n"
