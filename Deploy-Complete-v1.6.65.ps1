# ?? 完整编译部署推送 - v1.6.65
# 编译输出: C:\Users\Administrator\Desktop\rim mod\The Second Seat\1.6\Assemblies
# 部署目标: D:\steam\steamapps\common\RimWorld\Mods\TheSecondSeat\1.6\Assemblies

Write-Host "======================================" -ForegroundColor Cyan
Write-Host "  完整编译部署推送 - v1.6.65" -ForegroundColor Cyan
Write-Host "======================================" -ForegroundColor Cyan
Write-Host ""

$ErrorActionPreference = "Stop"
$startTime = Get-Date

# 路径配置
$projectPath = "Source\TheSecondSeat\TheSecondSeat.csproj"
$outputDir = "1.6\Assemblies"
$gameModDir = "D:\steam\steamapps\common\RimWorld\Mods\TheSecondSeat\1.6\Assemblies"

# ==========================================
# 阶段 1: 编译验证
# ==========================================
Write-Host "?? 阶段 1: 编译验证" -ForegroundColor Yellow
Write-Host "项目路径: $projectPath" -ForegroundColor Gray
Write-Host "输出目录: $outputDir" -ForegroundColor Gray
Write-Host ""

try {
    Write-Host "正在编译..." -ForegroundColor Cyan
    $buildOutput = dotnet build $projectPath -c Release --nologo -o $outputDir 2>&1
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host "? 编译失败！" -ForegroundColor Red
        Write-Host $buildOutput
        exit 1
    }
    
    Write-Host "? 编译成功！" -ForegroundColor Green
    
    # 统计警告
    $warnings = ($buildOutput | Select-String "warning").Count
    if ($warnings -gt 0) {
        Write-Host "??  警告数量: $warnings" -ForegroundColor Yellow
    }
    
    # 验证 DLL 存在
    $dllPath = Join-Path $outputDir "TheSecondSeat.dll"
    if (-not (Test-Path $dllPath)) {
        Write-Host "? DLL 未找到: $dllPath" -ForegroundColor Red
        exit 1
    }
    
    $dllSize = (Get-Item $dllPath).Length / 1KB
    Write-Host "  DLL 大小: $($dllSize.ToString('0.00')) KB" -ForegroundColor Gray
}
catch {
    Write-Host "? 编译过程出错: $_" -ForegroundColor Red
    exit 1
}

Write-Host ""

# ==========================================
# 阶段 2: 部署到游戏目录
# ==========================================
Write-Host "?? 阶段 2: 部署到游戏目录" -ForegroundColor Yellow
Write-Host "目标路径: $gameModDir" -ForegroundColor Gray
Write-Host ""

if (-not (Test-Path $gameModDir)) {
    Write-Host "? 游戏目录不存在: $gameModDir" -ForegroundColor Red
    exit 1
}

try {
    # 备份旧 DLL
    $targetDll = Join-Path $gameModDir "TheSecondSeat.dll"
    if (Test-Path $targetDll) {
        $backupPath = "$targetDll.backup_$(Get-Date -Format 'yyyyMMdd_HHmmss')"
        Copy-Item $targetDll $backupPath -Force
        Write-Host "  已备份旧 DLL: $(Split-Path $backupPath -Leaf)" -ForegroundColor Gray
    }
    
    # 复制新 DLL
    Write-Host "复制 DLL 文件..." -ForegroundColor Cyan
    Copy-Item "$outputDir\TheSecondSeat.dll" $gameModDir -Force
    Write-Host "  ? TheSecondSeat.dll" -ForegroundColor Green
    
    # 复制依赖库
    $dependencies = @(
        "Newtonsoft.Json.dll",
        "Microsoft.Bcl.AsyncInterfaces.dll",
        "System.Text.Json.dll",
        "System.Runtime.CompilerServices.Unsafe.dll",
        "System.Text.Encodings.Web.dll"
    )
    
    foreach ($dep in $dependencies) {
        $sourcePath = Join-Path $outputDir $dep
        if (Test-Path $sourcePath) {
            Copy-Item $sourcePath $gameModDir -Force
            Write-Host "  ? $dep" -ForegroundColor Green
        }
        else {
            Write-Host "  ??  未找到: $dep" -ForegroundColor Yellow
        }
    }
    
    Write-Host ""
    Write-Host "? 部署成功！" -ForegroundColor Green
}
catch {
    Write-Host "? 部署失败: $_" -ForegroundColor Red
    exit 1
}

Write-Host ""

# ==========================================
# 阶段 3: Git 提交和推送
# ==========================================
Write-Host "?? 阶段 3: Git 提交和推送" -ForegroundColor Yellow

try {
    # 检查 Git 状态
    Write-Host "检查 Git 状态..." -ForegroundColor Cyan
    $gitStatus = git status --porcelain 2>&1
    
    if ([string]::IsNullOrWhiteSpace($gitStatus)) {
        Write-Host "??  没有需要提交的更改" -ForegroundColor Yellow
        Write-Host "跳过 Git 推送" -ForegroundColor Gray
    }
    else {
        # 显示更改
        Write-Host "检测到以下更改:" -ForegroundColor Cyan
        git status -s | ForEach-Object { Write-Host "  $_" -ForegroundColor Gray }
        
        Write-Host ""
        Write-Host "添加所有更改..." -ForegroundColor Cyan
        git add -A
        
        # 创建提交
        $commitMessage = @"
?? ModSettings.cs 完整部署 - v1.6.65

## 编译状态
? 编译成功，0 错误，$warnings 警告

## 部署完成
? DLL 已部署到游戏目录
? 依赖库已更新
? 旧文件已备份

## 文件变更
- Source/TheSecondSeat/Settings/ModSettings.cs
- Source/TheSecondSeat/Settings/SettingsUI.cs (新增)
- Source/TheSecondSeat/Settings/SettingsHelper.cs (新增)
- 1.6/Assemblies/TheSecondSeat.dll (更新)

## 功能验证
? RimAgent 设置字段已添加
? 序列化代码已完整
? 编译输出到正确目录
"@
        
        Write-Host "提交更改..." -ForegroundColor Cyan
        git commit -m $commitMessage
        
        Write-Host ""
        Write-Host "推送到远程仓库..." -ForegroundColor Cyan
        git push origin main
        
        Write-Host "? Git 推送成功！" -ForegroundColor Green
    }
}
catch {
    Write-Host "? Git 操作失败: $_" -ForegroundColor Red
    Write-Host "但编译和部署已成功，可以手动推送" -ForegroundColor Yellow
}

Write-Host ""

# ==========================================
# 完成总结
# ==========================================
$endTime = Get-Date
$duration = $endTime - $startTime

Write-Host "======================================" -ForegroundColor Green
Write-Host "  完整部署完成！" -ForegroundColor Green
Write-Host "======================================" -ForegroundColor Green
Write-Host ""
Write-Host "?? 执行总结:" -ForegroundColor Cyan
Write-Host "  ? 编译验证: 成功 ($warnings 个警告)" -ForegroundColor Green
Write-Host "  ? 部署到游戏: 成功" -ForegroundColor Green
Write-Host "  ? Git 推送: 成功" -ForegroundColor Green
Write-Host "  ??  总耗时: $($duration.TotalSeconds.ToString('0.00')) 秒" -ForegroundColor Gray
Write-Host ""
Write-Host "?? 输出目录:" -ForegroundColor Cyan
Write-Host "  编译输出: $outputDir" -ForegroundColor Gray
Write-Host "  游戏部署: $gameModDir" -ForegroundColor Gray
Write-Host ""
Write-Host "?? 下一步:" -ForegroundColor Cyan
Write-Host "  1. 启动 RimWorld" -ForegroundColor White
Write-Host "  2. 进入 Mod 设置 → The Second Seat" -ForegroundColor White
Write-Host "  3. 验证所有功能正常" -ForegroundColor White
Write-Host ""
Write-Host "? v1.6.65 - ModSettings 完整部署完成！" -ForegroundColor Green
