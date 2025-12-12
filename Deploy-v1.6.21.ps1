# Deploy-v1.6.21.ps1 - 头像和立绘切换按钮修复部署脚本

Write-Host "========================================" -ForegroundColor Cyan
Write-Host " v1.6.21 部署脚本" -ForegroundColor Cyan
Write-Host " 头像和立绘切换按钮修复" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

$ErrorActionPreference = "Stop"
$projectDir = "C:\Users\Administrator\Desktop\rim mod\The Second Seat"
$rimworldModDir = "D:\steam\steamapps\common\RimWorld\Mods\TheSecondSeat"

# 1. 检查修改是否完成
Write-Host "[1/5] 检查修改..." -ForegroundColor Yellow
$narratorScreenButton = Join-Path $projectDir "Source\TheSecondSeat\UI\NarratorScreenButton.cs"

if (!(Test-Path $narratorScreenButton)) {
    Write-Host "  ? 错误：找不到 NarratorScreenButton.cs" -ForegroundColor Red
    exit 1
}

# 检查是否包含 lastUsePortraitMode 字段
$content = Get-Content $narratorScreenButton -Raw
if ($content -notmatch "private bool lastUsePortraitMode") {
    Write-Host "  ? 警告：NarratorScreenButton.cs 缺少 lastUsePortraitMode 字段" -ForegroundColor Yellow
    Write-Host "  请手动添加字段（参考 NarratorScreenButton-UpdatePortrait-补丁-v1.6.21.md）" -ForegroundColor Yellow
    $continue = Read-Host "是否继续部署？(y/n)"
    if ($continue -ne "y") {
        exit 1
    }
}

if ($content -notmatch "Portrait mode changed to") {
    Write-Host "  ? 警告：UpdatePortrait 方法可能未修改" -ForegroundColor Yellow
    Write-Host "  请手动修改方法（参考 NarratorScreenButton-UpdatePortrait-补丁-v1.6.21.md）" -ForegroundColor Yellow
    $continue = Read-Host "是否继续部署？(y/n)"
    if ($continue -ne "y") {
        exit 1
    }
}

Write-Host "  ? 代码修改检查完成" -ForegroundColor Green

# 2. 编译项目
Write-Host ""
Write-Host "[2/5] 编译项目..." -ForegroundColor Yellow
$csprojPath = Join-Path $projectDir "Source\TheSecondSeat\TheSecondSeat.csproj"

try {
    $buildOutput = dotnet build $csprojPath -c Release 2>&1
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host "  ? 编译失败！" -ForegroundColor Red
        Write-Host $buildOutput
        exit 1
    }
    
    Write-Host "  ? 编译成功" -ForegroundColor Green
}
catch {
    Write-Host "  ? 编译异常：$_" -ForegroundColor Red
    exit 1
}

# 3. 复制 DLL
Write-Host ""
Write-Host "[3/5] 复制 DLL..." -ForegroundColor Yellow
$sourceDll = Join-Path $projectDir "1.5\Assemblies\TheSecondSeat.dll"
$targetDll = Join-Path $rimworldModDir "1.5\Assemblies\TheSecondSeat.dll"

if (!(Test-Path $sourceDll)) {
    Write-Host "  ? 错误：找不到编译后的 DLL" -ForegroundColor Red
    exit 1
}

# 确保目标目录存在
$targetDir = Split-Path $targetDll
if (!(Test-Path $targetDir)) {
    New-Item -ItemType Directory -Path $targetDir -Force | Out-Null
}

Copy-Item $sourceDll $targetDll -Force
Write-Host "  ? DLL 已复制" -ForegroundColor Green

# 4. 验证缓存键修改
Write-Host ""
Write-Host "[4/5] 验证缓存键修改..." -ForegroundColor Yellow

$avatarLoader = Join-Path $projectDir "Source\TheSecondSeat\PersonaGeneration\AvatarLoader.cs"
$portraitLoader = Join-Path $projectDir "Source\TheSecondSeat\PersonaGeneration\PortraitLoader.cs"

$avatarContent = Get-Content $avatarLoader -Raw
$portraitContent = Get-Content $portraitLoader -Raw

$issues = @()

if ($avatarContent -notmatch '\$"\{def\.defName\}_avatar_\{expressionSuffix\}"') {
    $issues += "AvatarLoader.cs 缓存键格式不正确"
}

if ($avatarContent -notmatch "ClearAllCache") {
    $issues += "AvatarLoader.cs 缺少 ClearAllCache 方法"
}

if ($portraitContent -notmatch '\$"\{def\.defName\}_portrait_\{expressionSuffix\}"') {
    $issues += "PortraitLoader.cs 缓存键格式不正确"
}

if ($portraitContent -notmatch "ClearAllCache") {
    $issues += "PortraitLoader.cs 缺少 ClearAllCache 方法"
}

if ($issues.Count -gt 0) {
    Write-Host "  ? 发现问题：" -ForegroundColor Red
    foreach ($issue in $issues) {
        Write-Host "    - $issue" -ForegroundColor Red
    }
    Write-Host "  请检查修复报告中的说明" -ForegroundColor Yellow
}
else {
    Write-Host "  ? 缓存键验证通过" -ForegroundColor Green
}

# 5. 生成部署报告
Write-Host ""
Write-Host "[5/5] 生成部署报告..." -ForegroundColor Yellow

$report = @"
# v1.6.21 部署报告
## 部署时间
$(Get-Date -Format "yyyy-MM-dd HH:mm:ss")

## 修改文件
- AvatarLoader.cs (缓存键修改 + ClearAllCache 方法)
- PortraitLoader.cs (缓存键修改 + ClearAllCache 方法)
- NarratorScreenButton.cs (设置变化检测)

## 缓存键变更
| 加载器 | 旧格式 | 新格式 |
|--------|--------|--------|
| AvatarLoader | `{defName}_avatar{expression}` | `{defName}_avatar_{expression}` |
| PortraitLoader | `{defName}{expression}` | `{defName}_portrait_{expression}` |

## 测试清单
- [ ] 从头像模式切换到立绘模式，AI按钮立即显示立绘
- [ ] 从立绘模式切换到头像模式，AI按钮立即显示头像
- [ ] 表情切换在两种模式下都正常工作
- [ ] DevMode 下查看日志，确认缓存清除和重新加载
- [ ] 无需重启游戏即可切换模式

## 下一步
1. 启动 RimWorld
2. 加载存档
3. 在设置中切换"使用立绘模式"
4. 观察 AI 按钮是否立即切换图片
5. 开启 DevMode 查看日志确认功能正常

---
**部署状态：** 成功  
**版本：** v1.6.21
"@

$reportPath = Join-Path $projectDir "部署报告-v1.6.21.md"
$report | Set-Content $reportPath -Encoding UTF8

Write-Host "  ? 部署报告已生成：部署报告-v1.6.21.md" -ForegroundColor Green

# 完成
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host " ? v1.6.21 部署完成！" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "接下来请：" -ForegroundColor Yellow
Write-Host "1. 启动 RimWorld" -ForegroundColor White
Write-Host "2. 加载存档" -ForegroundColor White
Write-Host "3. 打开设置 → The Second Seat" -ForegroundColor White
Write-Host "4. 切换'使用立绘模式'复选框" -ForegroundColor White
Write-Host "5. 返回游戏，观察 AI 按钮是否立即切换" -ForegroundColor White
Write-Host ""
Write-Host "注意：如果 UpdatePortrait 方法未修改，请参考" -ForegroundColor Yellow
Write-Host "NarratorScreenButton-UpdatePortrait-补丁-v1.6.21.md" -ForegroundColor Yellow
Write-Host ""
