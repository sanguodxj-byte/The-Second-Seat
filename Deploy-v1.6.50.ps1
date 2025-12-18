# ======================================
#  The Second Seat Mod - 自动部署脚本
#  版本: v1.6.50
#  修复: 立绘位置上移 + 半透明透明度 + 张嘴动画调试
# ======================================

$ErrorActionPreference = "Stop"

Write-Host ""
Write-Host "======================================" -ForegroundColor Cyan
Write-Host "  The Second Seat Mod - 自动部署" -ForegroundColor Cyan
Write-Host "  版本: v1.6.50" -ForegroundColor Cyan
Write-Host "======================================" -ForegroundColor Cyan
Write-Host ""

# ==================== 步骤 1/5: 检查 RimWorld 进程 ====================

Write-Host "步骤 1/5: 检查 RimWorld 进程..." -ForegroundColor Yellow
$rimworldProcess = Get-Process | Where-Object {$_.Name -like "*RimWorld*"}

if ($rimworldProcess) {
    Write-Host "   ? 发现 RimWorld 正在运行" -ForegroundColor Yellow
    Write-Host "   正在尝试关闭..." -ForegroundColor Yellow
    
    try {
        $rimworldProcess | Stop-Process -Force -ErrorAction Stop
        Start-Sleep -Seconds 2
        Write-Host "   ? RimWorld 已关闭" -ForegroundColor Green
    }
    catch {
        Write-Host "   ? 无法自动关闭 RimWorld，请手动关闭后继续" -ForegroundColor Yellow
        Read-Host "按 Enter 继续..."
    }
}
else {
    Write-Host "   ? RimWorld 未运行" -ForegroundColor Green
}

# ==================== 步骤 2/5: 清理旧文件 ====================

Write-Host ""
Write-Host "步骤 2/5: 清理旧文件..." -ForegroundColor Yellow

$binPath = "Source\TheSecondSeat\bin"
$objPath = "Source\TheSecondSeat\obj"

if (Test-Path $binPath) {
    Remove-Item $binPath -Recurse -Force -ErrorAction SilentlyContinue
    Write-Host "   ? 已删除 bin 文件夹" -ForegroundColor Green
}

if (Test-Path $objPath) {
    Remove-Item $objPath -Recurse -Force -ErrorAction SilentlyContinue
    Write-Host "   ? 已删除 obj 文件夹" -ForegroundColor Green
}

# ==================== 步骤 3/5: 编译项目 ====================

Write-Host ""
Write-Host "步骤 3/5: 编译项目..." -ForegroundColor Yellow

$buildOutput = dotnet build "Source\TheSecondSeat\TheSecondSeat.csproj" --configuration Release --verbosity minimal 2>&1 | Out-String

if ($LASTEXITCODE -eq 0) {
    Write-Host "   ? 编译成功" -ForegroundColor Green
    
    # 检查警告数量
    $warningCount = ([regex]::Matches($buildOutput, "warning")).Count
    if ($warningCount -gt 0) {
        Write-Host "   ?  $warningCount 个警告（非致命）" -ForegroundColor Yellow
    }
}
else {
    Write-Host "   ? 编译失败！" -ForegroundColor Red
    Write-Host ""
    Write-Host "错误详情:" -ForegroundColor Red
    Write-Host $buildOutput -ForegroundColor Red
    exit 1
}

# ==================== 步骤 4/5: 部署 DLL 文件 ====================

Write-Host ""
Write-Host "步骤 4/5: 部署 DLL 文件..." -ForegroundColor Yellow

$sourceDll = "Source\TheSecondSeat\bin\Release\net472\TheSecondSeat.dll"
$targetDir = "D:\steam\steamapps\common\RimWorld\Mods\TheSecondSeat\Assemblies"
$targetDll = Join-Path $targetDir "TheSecondSeat.dll"

# 确保目标目录存在
if (!(Test-Path $targetDir)) {
    New-Item -ItemType Directory -Path $targetDir -Force | Out-Null
    Write-Host "   ? 创建 Assemblies 文件夹" -ForegroundColor Green
}

# 检查源文件
if (!(Test-Path $sourceDll)) {
    Write-Host "   ? 源 DLL 不存在: $sourceDll" -ForegroundColor Red
    exit 1
}

# 复制 DLL
try {
    Copy-Item $sourceDll $targetDll -Force
    $dllInfo = Get-Item $targetDll
    
    Write-Host "   ? TheSecondSeat.dll 已部署" -ForegroundColor Green
    Write-Host "      - 大小: $([math]::Round($dllInfo.Length/1KB, 2)) KB" -ForegroundColor Gray
    Write-Host "      - 时间: $($dllInfo.LastWriteTime)" -ForegroundColor Gray
}
catch {
    Write-Host "   ? 复制 DLL 失败: $_" -ForegroundColor Red
    exit 1
}

# Harmony.dll 检查（RimWorld 已内置，无需部署）
Write-Host "   ?  跳过 Harmony.dll（RimWorld 已内置）" -ForegroundColor Yellow

# ==================== 步骤 5/5: 验证部署 ====================

Write-Host ""
Write-Host "步骤 5/5: 验证部署..." -ForegroundColor Yellow

$sourceInfo = Get-Item $sourceDll
$targetInfo = Get-Item $targetDll

# 验证文件大小
if ($sourceInfo.Length -eq $targetInfo.Length) {
    Write-Host "   ? 文件大小一致" -ForegroundColor Green
}
else {
    Write-Host "   ? 文件大小不一致（可能文件系统延迟）" -ForegroundColor Yellow
}

# 验证时间戳
$timeDiff = [Math]::Abs(($sourceInfo.LastWriteTime - $targetInfo.LastWriteTime).TotalSeconds)
if ($timeDiff -lt 5) {
    Write-Host "   ? 部署验证通过" -ForegroundColor Green
    Write-Host "      - 编译时间与部署时间一致" -ForegroundColor Gray
}
else {
    Write-Host "   ? 时间戳差异: $timeDiff 秒" -ForegroundColor Yellow
}

# 检查 Mod 配置文件
Write-Host ""
Write-Host "   检查 Mod 配置文件..." -ForegroundColor Gray

$aboutXml = "D:\steam\steamapps\common\RimWorld\Mods\TheSecondSeat\About\About.xml"
if (Test-Path $aboutXml) {
    Write-Host "   ? About.xml 存在" -ForegroundColor Green
}
else {
    Write-Host "   ? About.xml 缺失" -ForegroundColor Yellow
}

$defsDir = "D:\steam\steamapps\common\RimWorld\Mods\TheSecondSeat\Defs"
if (Test-Path $defsDir) {
    $defFiles = (Get-ChildItem $defsDir -Recurse -File).Count
    Write-Host "   ? Defs 文件夹存在 ($defFiles 个文件)" -ForegroundColor Green
}
else {
    Write-Host "   ? Defs 文件夹缺失" -ForegroundColor Yellow
}

# ==================== 部署完成总结 ====================

Write-Host ""
Write-Host "======================================" -ForegroundColor Cyan
Write-Host "  ?? 部署完成！" -ForegroundColor Green
Write-Host "======================================" -ForegroundColor Cyan
Write-Host ""

Write-Host "?? 部署文件:" -ForegroundColor White
Write-Host "   ? TheSecondSeat.dll ($([math]::Round($dllInfo.Length/1KB, 2)) KB)" -ForegroundColor Green
Write-Host ""

Write-Host "?? 部署位置:" -ForegroundColor White
Write-Host "   $targetDir" -ForegroundColor Gray
Write-Host ""

Write-Host "? v1.6.50 修复内容:" -ForegroundColor White
Write-Host "   ? 立绘位置上移 40px" -ForegroundColor Green
Write-Host "   ? 半透明模式透明度修复" -ForegroundColor Green
Write-Host "   ? 张嘴动画调试增强" -ForegroundColor Green
Write-Host ""

Write-Host "?? 下一步操作:" -ForegroundColor White
Write-Host "   1. 启动 RimWorld" -ForegroundColor Gray
Write-Host "   2. 在 Mod 管理器中启用 'The Second Seat'" -ForegroundColor Gray
Write-Host "   3. 进入游戏测试功能" -ForegroundColor Gray
Write-Host ""

Write-Host "?? 测试清单:" -ForegroundColor White
Write-Host "   □ 开启立绘模式（设置中）" -ForegroundColor Gray
Write-Host "   □ 观察立绘位置（应上移40px）" -ForegroundColor Gray
Write-Host "   □ 鼠标移到立绘上测试半透明效果" -ForegroundColor Gray
Write-Host "   □ 开启 DevMode 查看张嘴动画日志" -ForegroundColor Gray
Write-Host "   □ 触发 TTS 播放测试张嘴动画" -ForegroundColor Gray
Write-Host ""

Write-Host "?? 提示:" -ForegroundColor White
Write-Host "   - Harmony 由 RimWorld 提供，无需单独部署" -ForegroundColor Gray
Write-Host "   - 如遇问题，检查 Player.log 查看错误信息" -ForegroundColor Gray
Write-Host "   - DevMode 可查看详细的张嘴动画调试日志" -ForegroundColor Gray
Write-Host ""
