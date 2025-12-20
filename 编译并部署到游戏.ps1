#!/usr/bin/env pwsh
# TSS 编译后自动部署到游戏目录
# 版本: v2.0.3-AUTO
# 功能: 编译 → 检测更改 → 智能部署

param(
    [string]$GameModPath = "D:\steam\steamapps\common\RimWorld\Mods\TheSecondSeat",
    [switch]$FullDeploy = $false,
    [switch]$SkipBackup = $false,
    [switch]$LaunchGame = $false
)

$ErrorActionPreference = "Stop"

Write-Host "╔════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║  TSS 自动编译+部署 v2.0.3                                  ║" -ForegroundColor Cyan
Write-Host "║  Build → Deploy → Verify                                  ║" -ForegroundColor Cyan
Write-Host "╚════════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
Write-Host ""

# ============================================================
# 0. 配置路径
# ============================================================
$workspaceRoot = "C:\Users\Administrator\Desktop\rim mod\The Second Seat"
$sourceRoot = Join-Path $workspaceRoot "Source\TheSecondSeat"
$projectFile = Join-Path $sourceRoot "TheSecondSeat.csproj"
$dllSource = Join-Path $sourceRoot "bin\Release\net472\TheSecondSeat.dll"

Write-Host "?? 路径配置：" -ForegroundColor Yellow
Write-Host "   工作区: $workspaceRoot" -ForegroundColor Gray
Write-Host "   源代码: $sourceRoot" -ForegroundColor Gray
Write-Host "   游戏目录: $GameModPath" -ForegroundColor Gray
Write-Host ""

# 切换到工作区
Set-Location $workspaceRoot

# ============================================================
# 1. 检查环境
# ============================================================
Write-Host "?? [1/6] 检查环境..." -ForegroundColor Yellow
Write-Host ""

if (!(Test-Path $projectFile)) {
    Write-Host "? 项目文件不存在: $projectFile" -ForegroundColor Red
    exit 1
}
Write-Host "? 项目文件存在" -ForegroundColor Green

if (!(Test-Path $GameModPath)) {
    Write-Host "? 游戏目录不存在: $GameModPath" -ForegroundColor Red
    exit 1
}
Write-Host "? 游戏目录存在" -ForegroundColor Green

Write-Host ""

# ============================================================
# 2. 备份（可选）
# ============================================================
if (!$SkipBackup) {
    Write-Host "?? [2/6] 备份现有文件..." -ForegroundColor Yellow
    Write-Host ""

    $backupDir = Join-Path $GameModPath "Backup_$(Get-Date -Format 'yyyyMMdd_HHmmss')"
    
    if (Test-Path "$GameModPath\Assemblies\TheSecondSeat.dll") {
        New-Item -ItemType Directory -Path $backupDir -Force | Out-Null
        Copy-Item "$GameModPath\Assemblies\TheSecondSeat.dll" "$backupDir\" -Force
        Write-Host "? DLL 已备份" -ForegroundColor Green
    }

    Write-Host ""
} else {
    Write-Host "??  [2/6] 跳过备份步骤" -ForegroundColor Gray
    Write-Host ""
}

# ============================================================
# 3. 编译项目
# ============================================================
Write-Host "?? [3/6] 编译项目..." -ForegroundColor Yellow
Write-Host ""

# 记录编译开始时间
$buildStartTime = Get-Date

# 清理旧编译
Write-Host "   清理旧编译..." -ForegroundColor Gray
dotnet clean $projectFile --configuration Release --verbosity quiet | Out-Null

# 编译
Write-Host "   编译中..." -ForegroundColor Gray
$buildOutput = dotnet build $projectFile `
    --configuration Release `
    --verbosity minimal `
    2>&1

$buildEndTime = Get-Date
$buildDuration = ($buildEndTime - $buildStartTime).TotalSeconds

if ($LASTEXITCODE -eq 0) {
    Write-Host "? 编译成功（耗时 $([math]::Round($buildDuration, 2))秒）" -ForegroundColor Green
    
    # 显示警告（如果有）
    $warnings = $buildOutput | Select-String "warning"
    if ($warnings) {
        Write-Host "??  编译警告:" -ForegroundColor Yellow
        $warnings | ForEach-Object { Write-Host "   $_" -ForegroundColor Yellow }
    }
} else {
    Write-Host "? 编译失败！" -ForegroundColor Red
    Write-Host ""
    Write-Host "错误详情：" -ForegroundColor Red
    $buildOutput | Where-Object { $_ -match "error" } | ForEach-Object {
        Write-Host "   $_" -ForegroundColor Red
    }
    exit 1
}

Write-Host ""

# ============================================================
# 4. 智能检测更改
# ============================================================
Write-Host "?? [4/6] 检测更改..." -ForegroundColor Yellow
Write-Host ""

$needsFullDeploy = $FullDeploy

# 检查 DLL 是否更改
if (Test-Path "$GameModPath\Assemblies\TheSecondSeat.dll") {
    $oldDllHash = (Get-FileHash "$GameModPath\Assemblies\TheSecondSeat.dll" -Algorithm MD5).Hash
    $newDllHash = (Get-FileHash $dllSource -Algorithm MD5).Hash
    
    if ($oldDllHash -ne $newDllHash) {
        Write-Host "? DLL 已更改，需要部署" -ForegroundColor Green
    } else {
        Write-Host "??  DLL 未更改" -ForegroundColor Gray
    }
} else {
    Write-Host "? 首次部署 DLL" -ForegroundColor Green
    $needsFullDeploy = $true
}

# 检查其他文件
$defsChanged = $false
$texturesChanged = $false

# 检查 Defs
if (Test-Path "Defs") {
    $defsFiles = Get-ChildItem "Defs" -Recurse -File
    foreach ($file in $defsFiles) {
        $targetPath = Join-Path $GameModPath "Defs\$($file.Name)"
        if (!(Test-Path $targetPath)) {
            $defsChanged = $true
            break
        }
        
        $sourceHash = (Get-FileHash $file.FullName -Algorithm MD5).Hash
        $targetHash = (Get-FileHash $targetPath -Algorithm MD5).Hash
        
        if ($sourceHash -ne $targetHash) {
            $defsChanged = $true
            break
        }
    }
}

if ($defsChanged) {
    Write-Host "? Defs 文件已更改" -ForegroundColor Green
} else {
    Write-Host "??  Defs 文件未更改" -ForegroundColor Gray
}

Write-Host ""

# ============================================================
# 5. 部署文件
# ============================================================
Write-Host "?? [5/6] 部署文件..." -ForegroundColor Yellow
Write-Host ""

# 5.1 部署 DLL
Write-Host "   [5.1] 部署 DLL..." -ForegroundColor Cyan
$assemblyDir = Join-Path $GameModPath "Assemblies"
if (!(Test-Path $assemblyDir)) {
    New-Item -ItemType Directory -Path $assemblyDir -Force | Out-Null
}

Copy-Item $dllSource (Join-Path $assemblyDir "TheSecondSeat.dll") -Force
$dllSize = (Get-Item $dllSource).Length
Write-Host "   ? TheSecondSeat.dll ($([math]::Round($dllSize/1KB, 2)) KB)" -ForegroundColor Green

# 5.2 部署 Defs（如果更改）
if ($defsChanged -or $needsFullDeploy) {
    Write-Host "   [5.2] 部署 Defs..." -ForegroundColor Cyan
    $defsTarget = Join-Path $GameModPath "Defs"
    
    if (Test-Path "Defs") {
        if (!(Test-Path $defsTarget)) {
            New-Item -ItemType Directory -Path $defsTarget -Force | Out-Null
        }
        
        Copy-Item "Defs\*" $defsTarget -Recurse -Force
        $xmlFiles = Get-ChildItem $defsTarget -Filter "*.xml" -Recurse
        Write-Host "   ? 已部署 $($xmlFiles.Count) 个 XML 文件" -ForegroundColor Green
    }
} else {
    Write-Host "   [5.2] 跳过 Defs（未更改）" -ForegroundColor Gray
}

# 5.3 部署其他资源（仅完整部署）
if ($needsFullDeploy) {
    Write-Host "   [5.3] 部署其他资源..." -ForegroundColor Cyan
    
    # About
    if (Test-Path "About") {
        Copy-Item "About" (Join-Path $GameModPath "About") -Recurse -Force
        Write-Host "   ? About" -ForegroundColor Green
    }
    
    # Languages
    if (Test-Path "Languages") {
        Copy-Item "Languages" (Join-Path $GameModPath "Languages") -Recurse -Force
        Write-Host "   ? Languages" -ForegroundColor Green
    }
    
    # Textures
    if (Test-Path "Textures") {
        Copy-Item "Textures" (Join-Path $GameModPath "Textures") -Recurse -Force
        Write-Host "   ? Textures" -ForegroundColor Green
    }
    
    # Materials
    if (Test-Path "Materials") {
        Copy-Item "Materials" (Join-Path $GameModPath "Materials") -Recurse -Force
        Write-Host "   ? Materials" -ForegroundColor Green
    }
    
    # LoadFolders.xml
    if (Test-Path "LoadFolders.xml") {
        Copy-Item "LoadFolders.xml" (Join-Path $GameModPath "LoadFolders.xml") -Force
        Write-Host "   ? LoadFolders.xml" -ForegroundColor Green
    }
} else {
    Write-Host "   [5.3] 跳过其他资源（增量部署）" -ForegroundColor Gray
}

Write-Host ""

# ============================================================
# 6. 验证部署
# ============================================================
Write-Host "? [6/6] 验证部署..." -ForegroundColor Yellow
Write-Host ""

$verificationPassed = $true

# 检查关键文件
$criticalFiles = @(
    "Assemblies\TheSecondSeat.dll",
    "About\About.xml"
)

foreach ($file in $criticalFiles) {
    $fullPath = Join-Path $GameModPath $file
    if (Test-Path $fullPath) {
        Write-Host "? $file" -ForegroundColor Green
    } else {
        Write-Host "? $file 缺失" -ForegroundColor Red
        $verificationPassed = $false
    }
}

# 检查 EventTester.cs 是否编译进 DLL
Write-Host ""
Write-Host "?? 检查 EventTester 类..." -ForegroundColor Cyan
try {
    $dllPath = Join-Path $GameModPath "Assemblies\TheSecondSeat.dll"
    $dll = [System.Reflection.Assembly]::LoadFile($dllPath)
    $eventTesterType = $dll.GetType("TheSecondSeat.Testing.EventTester")
    
    if ($eventTesterType) {
        Write-Host "? EventTester 类已编译" -ForegroundColor Green
        
        # 检查调试动作
        $debugActions = $eventTesterType.GetMethods() | Where-Object { 
            $_.GetCustomAttributes($false) | Where-Object { $_.GetType().Name -eq "DebugActionAttribute" }
        }
        
        if ($debugActions) {
            Write-Host "? 找到 $($debugActions.Count) 个测试按钮" -ForegroundColor Green
        } else {
            Write-Host "??  未找到 DebugAction 标记" -ForegroundColor Yellow
        }
    } else {
        Write-Host "? EventTester 类未找到" -ForegroundColor Red
        $verificationPassed = $false
    }
} catch {
    Write-Host "??  无法验证 EventTester: $_" -ForegroundColor Yellow
}

Write-Host ""

if (!$verificationPassed) {
    Write-Host "? 部署验证失败！" -ForegroundColor Red
    exit 1
}

# ============================================================
# 7. 部署总结
# ============================================================
Write-Host "?? 部署总结" -ForegroundColor Yellow
Write-Host ""

Write-Host "╔════════════════════════════════════════════════════════════╗" -ForegroundColor Green
Write-Host "║              ?? 部署成功！                                 ║" -ForegroundColor Green
Write-Host "╚════════════════════════════════════════════════════════════╝" -ForegroundColor Green
Write-Host ""

Write-Host "?? 部署信息：" -ForegroundColor Cyan
Write-Host "   类型: $(if ($needsFullDeploy) { '完整部署' } else { '增量部署' })" -ForegroundColor White
Write-Host "   位置: $GameModPath" -ForegroundColor White
Write-Host "   编译时间: $([math]::Round($buildDuration, 2))秒" -ForegroundColor White
Write-Host ""

Write-Host "?? 下一步操作：" -ForegroundColor Cyan
Write-Host "   1. 启动 RimWorld" -ForegroundColor White
Write-Host "   2. 加载存档（需要有地图）" -ForegroundColor White
Write-Host "   3. 按 F12 开启开发者模式" -ForegroundColor White
Write-Host "   4. 点击 ?? 齿轮 → 搜索 'TSS Events'" -ForegroundColor White
Write-Host "   5. 点击测试按钮" -ForegroundColor White
Write-Host ""

Write-Host "?? 测试工具位置：" -ForegroundColor Cyan
Write-Host "   开发者工具栏（F12）→ ?? 齿轮 → 搜索 'TSS Events'" -ForegroundColor White
Write-Host ""

# ============================================================
# 可选：启动游戏
# ============================================================
if ($LaunchGame) {
    Write-Host "?? 启动游戏..." -ForegroundColor Yellow
    
    $rimworldExe = "D:\steam\steamapps\common\RimWorld\RimWorldWin64.exe"
    if (Test-Path $rimworldExe) {
        Start-Process $rimworldExe
        Write-Host "? RimWorld 已启动" -ForegroundColor Green
    } else {
        Write-Host "??  未找到 RimWorld 可执行文件" -ForegroundColor Yellow
    }
    
    Write-Host ""
}

# ============================================================
# 生成部署报告
# ============================================================
$reportPath = "TSS-编译部署报告-$(Get-Date -Format 'yyyyMMdd-HHmmss').txt"
$report = @"
TSS 编译+部署报告
================================

部署时间: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')
部署类型: $(if ($needsFullDeploy) { '完整部署' } else { '增量部署' })
部署目标: $GameModPath

编译信息:
- 耗时: $([math]::Round($buildDuration, 2))秒
- 状态: 成功
- DLL 大小: $([math]::Round($dllSize/1KB, 2)) KB

已部署文件:
- Assemblies\TheSecondSeat.dll
$(if ($defsChanged -or $needsFullDeploy) { "- Defs\*.xml" } else { "- Defs (跳过)" })
$(if ($needsFullDeploy) { @"
- About\About.xml
- Languages\*
- Textures\*
- Materials\*
"@ } else { "- 其他资源 (跳过)" })

验证状态: $(if ($verificationPassed) { "通过" } else { "失败" })

EventTester 状态: $(if ($eventTesterType) { "已编译" } else { "未找到" })

下一步:
1. 启动 RimWorld
2. 按 F12 开启开发者模式
3. 点击 ?? → 搜索 'TSS Events'
4. 测试事件

"@

$report | Out-File -FilePath $reportPath -Encoding UTF8
Write-Host "?? 部署报告已保存: $reportPath" -ForegroundColor Cyan
Write-Host ""

Write-Host "? 部署完成！可以启动游戏测试了。??" -ForegroundColor Green
Write-Host ""
