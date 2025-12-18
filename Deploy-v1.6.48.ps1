# ========================================
# The Second Seat Mod - 部署脚本 v1.6.48
# ========================================
# 功能：编译并部署 Mod 到 RimWorld
# 修改：移除 Harmony.dll 部署（RimWorld 已内置）
# ========================================

Write-Host "======================================" -ForegroundColor Cyan
Write-Host "  The Second Seat Mod - 自动部署" -ForegroundColor Cyan
Write-Host "  版本: v1.6.48" -ForegroundColor Cyan
Write-Host "======================================" -ForegroundColor Cyan
Write-Host ""

# 配置路径
$projectPath = "Source\TheSecondSeat\TheSecondSeat.csproj"
$sourceDll = "Source\TheSecondSeat\bin\Release\net472\TheSecondSeat.dll"
$targetPath = "D:\steam\steamapps\common\RimWorld\Mods\TheSecondSeat\Assemblies"
$targetDll = Join-Path $targetPath "TheSecondSeat.dll"

# ========================================
# 步骤 1: 清理旧文件
# ========================================
Write-Host "步骤 1/4: 清理旧文件..." -ForegroundColor Yellow

if (Test-Path $sourceDll) {
    Remove-Item $sourceDll -Force
    Write-Host "   ? 已删除旧的编译文件" -ForegroundColor Green
}

# ========================================
# 步骤 2: 编译项目
# ========================================
Write-Host ""
Write-Host "步骤 2/4: 编译项目..." -ForegroundColor Yellow

$buildResult = dotnet build $projectPath --configuration Release 2>&1

if ($LASTEXITCODE -eq 0) {
    Write-Host "   ? 编译成功" -ForegroundColor Green
    
    # 统计警告数量
    $warnings = ($buildResult | Select-String "warning CS").Count
    if ($warnings -gt 0) {
        Write-Host "   ??  $warnings 个警告（非致命）" -ForegroundColor Yellow
    }
} else {
    Write-Host "   ? 编译失败！" -ForegroundColor Red
    Write-Host ""
    Write-Host "错误详情:" -ForegroundColor Red
    $buildResult | Where-Object { $_ -match "error" } | ForEach-Object {
        Write-Host "   $_" -ForegroundColor Red
    }
    exit 1
}

# ========================================
# 步骤 3: 部署 DLL
# ========================================
Write-Host ""
Write-Host "步骤 3/4: 部署 DLL 文件..." -ForegroundColor Yellow

# 确保目标目录存在
if (!(Test-Path $targetPath)) {
    New-Item -ItemType Directory -Path $targetPath -Force | Out-Null
    Write-Host "   ?? 已创建目标目录" -ForegroundColor Gray
}

# 复制主 DLL
if (Test-Path $sourceDll) {
    Copy-Item $sourceDll -Destination $targetDll -Force
    
    $fileInfo = Get-Item $targetDll
    $fileSize = [math]::Round($fileInfo.Length / 1KB, 2)
    
    Write-Host "   ? TheSecondSeat.dll 已部署" -ForegroundColor Green
    Write-Host "      - 大小: $fileSize KB" -ForegroundColor Gray
    Write-Host "      - 时间: $($fileInfo.LastWriteTime)" -ForegroundColor Gray
} else {
    Write-Host "   ? 找不到编译后的 DLL 文件！" -ForegroundColor Red
    exit 1
}

# ? 移除 Harmony.dll 部署（RimWorld 已内置）
Write-Host "   ??  跳过 Harmony.dll（RimWorld 已内置）" -ForegroundColor Cyan

# ========================================
# 步骤 4: 验证部署
# ========================================
Write-Host ""
Write-Host "步骤 4/4: 验证部署..." -ForegroundColor Yellow

$deployedDll = Get-Item $targetDll -ErrorAction SilentlyContinue
$compiledDll = Get-Item $sourceDll -ErrorAction SilentlyContinue

if ($deployedDll -and $compiledDll) {
    $timeDiff = ($deployedDll.LastWriteTime - $compiledDll.LastWriteTime).TotalSeconds
    
    if ([math]::Abs($timeDiff) -lt 5) {
        Write-Host "   ? 部署验证通过" -ForegroundColor Green
        Write-Host "      - 编译时间与部署时间一致" -ForegroundColor Gray
    } else {
        Write-Host "   ??  时间戳不一致（可能需要重新部署）" -ForegroundColor Yellow
    }
} else {
    Write-Host "   ??  无法验证文件时间戳" -ForegroundColor Yellow
}

# 检查其他必需文件
Write-Host ""
Write-Host "   检查 Mod 配置文件..." -ForegroundColor Gray

$aboutXml = "D:\steam\steamapps\common\RimWorld\Mods\TheSecondSeat\About\About.xml"
$defsFolder = "D:\steam\steamapps\common\RimWorld\Mods\TheSecondSeat\Defs"

if (Test-Path $aboutXml) {
    Write-Host "   ? About.xml 存在" -ForegroundColor Green
} else {
    Write-Host "   ??  About.xml 不存在" -ForegroundColor Yellow
}

if (Test-Path $defsFolder) {
    $defCount = (Get-ChildItem $defsFolder -Filter "*.xml" -Recurse).Count
    Write-Host "   ? Defs 文件夹存在 ($defCount 个文件)" -ForegroundColor Green
} else {
    Write-Host "   ??  Defs 文件夹不存在" -ForegroundColor Yellow
}

# ========================================
# 完成总结
# ========================================
Write-Host ""
Write-Host "======================================" -ForegroundColor Green
Write-Host "  ?? 部署完成！" -ForegroundColor Green
Write-Host "======================================" -ForegroundColor Green
Write-Host ""
Write-Host "?? 部署文件:" -ForegroundColor White
Write-Host "   ? TheSecondSeat.dll ($fileSize KB)" -ForegroundColor Gray
Write-Host ""
Write-Host "?? 部署位置:" -ForegroundColor White
Write-Host "   $targetPath" -ForegroundColor Gray
Write-Host ""
Write-Host "?? 下一步操作:" -ForegroundColor White
Write-Host "   1. 启动 RimWorld" -ForegroundColor Gray
Write-Host "   2. 在 Mod 管理器中启用 'The Second Seat'" -ForegroundColor Gray
Write-Host "   3. 进入游戏测试功能" -ForegroundColor Gray
Write-Host ""
Write-Host "?? 提示:" -ForegroundColor Yellow
Write-Host "   - Harmony 由 RimWorld 提供，无需单独部署" -ForegroundColor Gray
Write-Host "   - 如遇问题，检查 Player.log 查看错误信息" -ForegroundColor Gray
Write-Host ""
