# The Second Seat - 一键部署脚本
# 直接部署到 RimWorld Mods 目录

$ErrorActionPreference = "Stop"

# 路径配置
$projectRoot = "C:\Users\Administrator\Desktop\rim mod\The Second Seat"
$rimworldMods = "D:\steam\steamapps\common\RimWorld\Mods"
$targetPath = "$rimworldMods\TheSecondSeat"

Write-Host "`n" + "="*70 -ForegroundColor Cyan
Write-Host "  The Second Seat - 一键部署到 RimWorld" -ForegroundColor Yellow
Write-Host "="*70 + "`n" -ForegroundColor Cyan

# 步骤 1: 编译 DLL
Write-Host "?? 步骤 1/4: 编译 DLL..." -ForegroundColor Cyan
Write-Host "  源目录: $projectRoot" -ForegroundColor Gray

$sourceDll = "$projectRoot\Source\TheSecondSeat\bin\Release\net472\TheSecondSeat.dll"

Push-Location "$projectRoot\Source\TheSecondSeat"
try {
    Write-Host "  正在编译..." -ForegroundColor Yellow
    $output = dotnet build -c Release --nologo 2>&1
    
    if ($LASTEXITCODE -eq 0) {
        $dllSize = (Get-Item $sourceDll).Length / 1KB
        Write-Host "  ? 编译成功 ($([math]::Round($dllSize, 2)) KB)" -ForegroundColor Green
    } else {
        Write-Host "  ? 编译失败！" -ForegroundColor Red
        Write-Host $output
        exit 1
    }
} finally {
    Pop-Location
}

# 步骤 2: 复制 DLL 到项目 Assemblies
Write-Host "`n?? 步骤 2/4: 更新项目 DLL..." -ForegroundColor Cyan

$projectAssemblies = "$projectRoot\Assemblies"
if (-not (Test-Path $projectAssemblies)) {
    New-Item -ItemType Directory -Path $projectAssemblies -Force | Out-Null
}

Copy-Item $sourceDll -Destination "$projectAssemblies\TheSecondSeat.dll" -Force
Write-Host "  ? 已更新 Assemblies\TheSecondSeat.dll" -ForegroundColor Green

# 步骤 3: 清理目标目录
Write-Host "`n??? 步骤 3/4: 清理旧版本..." -ForegroundColor Cyan

if (Test-Path $targetPath) {
    Write-Host "  删除旧版本..." -ForegroundColor Yellow
    Remove-Item $targetPath -Recurse -Force
    Write-Host "  ? 旧版本已清理" -ForegroundColor Green
} else {
    Write-Host "  ?? 无需清理（首次部署）" -ForegroundColor Yellow
}

# 步骤 4: 部署文件
Write-Host "`n?? 步骤 4/4: 部署到 RimWorld..." -ForegroundColor Cyan
Write-Host "  目标: $targetPath" -ForegroundColor Gray

# 创建目标目录
New-Item -ItemType Directory -Path $targetPath -Force | Out-Null

# 要复制的项目
$items = @(
    @{Name="About"; Required=$true},
    @{Name="Assemblies"; Required=$true},
    @{Name="Defs"; Required=$true},
    @{Name="Languages"; Required=$true},
    @{Name="LoadFolders.xml"; Required=$true},
    @{Name="Textures"; Required=$false},
    @{Name="Patches"; Required=$false},
    @{Name="Sounds"; Required=$false}
)

$copiedCount = 0
$totalSize = 0

foreach ($item in $items) {
    $sourcePath = Join-Path $projectRoot $item.Name
    $destPath = Join-Path $targetPath $item.Name
    
    if (Test-Path $sourcePath) {
        Copy-Item $sourcePath -Destination $destPath -Recurse -Force
        
        # 计算大小
        if (Test-Path $destPath) {
            $itemSize = (Get-ChildItem $destPath -Recurse | Measure-Object -Property Length -Sum).Sum / 1KB
            $totalSize += $itemSize
            Write-Host "  ? $($item.Name) ($([math]::Round($itemSize, 2)) KB)" -ForegroundColor Green
            $copiedCount++
        }
    } else {
        if ($item.Required) {
            Write-Host "  ? $($item.Name) (缺失，必需文件！)" -ForegroundColor Red
        } else {
            Write-Host "  ?? $($item.Name) (可选，跳过)" -ForegroundColor Gray
        }
    }
}

# 验证部署
Write-Host "`n?? 步骤 5/4: 验证部署..." -ForegroundColor Cyan

$criticalFiles = @(
    "About\About.xml",
    "Assemblies\TheSecondSeat.dll",
    "LoadFolders.xml",
    "Defs\GameComponentDefs.xml"
)

$allValid = $true
foreach ($file in $criticalFiles) {
    $filePath = Join-Path $targetPath $file
    if (Test-Path $filePath) {
        Write-Host "  ? $file" -ForegroundColor Green
    } else {
        Write-Host "  ? $file (缺失！)" -ForegroundColor Red
        $allValid = $false
    }
}

# 总结
Write-Host "`n" + "="*70 -ForegroundColor Cyan
if ($allValid) {
    Write-Host "  ? 部署成功！" -ForegroundColor Green
} else {
    Write-Host "  ?? 部署完成，但存在问题" -ForegroundColor Yellow
}
Write-Host "="*70 -ForegroundColor Cyan

Write-Host "`n?? 部署统计:" -ForegroundColor Cyan
Write-Host "  复制文件: $copiedCount 个项目" -ForegroundColor Gray
Write-Host "  总大小: $([math]::Round($totalSize, 2)) KB" -ForegroundColor Gray
Write-Host "  目标路径: $targetPath" -ForegroundColor Gray

# 检查 DLL 版本信息
$deployedDll = "$targetPath\Assemblies\TheSecondSeat.dll"
if (Test-Path $deployedDll) {
    $dllInfo = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($deployedDll)
    Write-Host "`n?? DLL 信息:" -ForegroundColor Cyan
    Write-Host "  文件版本: $($dllInfo.FileVersion)" -ForegroundColor Gray
    Write-Host "  编译时间: $((Get-Item $deployedDll).LastWriteTime)" -ForegroundColor Gray
}

# 提示下一步
Write-Host "`n?? 下一步操作:" -ForegroundColor Yellow
Write-Host "  1. 启动 RimWorld" -ForegroundColor Gray
Write-Host "  2. Mods → 启用 'The Second Seat'" -ForegroundColor Gray
Write-Host "  3. 重启游戏" -ForegroundColor Gray
Write-Host "  4. 开始游戏 → 测试功能" -ForegroundColor Gray

Write-Host "`n?? 配置提醒:" -ForegroundColor Yellow
Write-Host "  选项 → Mod Settings → The Second Seat" -ForegroundColor Gray
Write-Host "  - API Endpoint: http://localhost:1234/v1/chat/completions" -ForegroundColor Gray
Write-Host "  - Model Name: local-model" -ForegroundColor Gray

Write-Host "`n? 祝游戏愉快！" -ForegroundColor Green
Write-Host ""

# 可选：自动打开 RimWorld Mods 目录
$openFolder = Read-Host "是否打开 RimWorld Mods 目录？(Y/N)"
if ($openFolder -eq "Y" -or $openFolder -eq "y") {
    explorer $rimworldMods
}
