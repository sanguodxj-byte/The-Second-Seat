#!/usr/bin/env pwsh
# 强制部署脚本 - v1.7.6
# 用于在游戏运行时强制复制DLL

Write-Host "?? The Second Seat - 强制部署 v1.7.6" -ForegroundColor Cyan
Write-Host ""

# 源DLL路径
$sourceDll = "Source\TheSecondSeat\bin\Release\net472\TheSecondSeat.dll"
# 目标路径
$targetDir = "D:\steam\steamapps\common\RimWorld\Mods\TheSecondSeat\1.5\Assemblies"
$targetDll = "$targetDir\TheSecondSeat.dll"

# 检查源文件
if (-not (Test-Path $sourceDll)) {
    Write-Host "? 源DLL不存在，请先编译！" -ForegroundColor Red
    Write-Host "   运行: dotnet build -c Release" -ForegroundColor Yellow
    exit 1
}

Write-Host "?? 源文件: $sourceDll" -ForegroundColor Green
Write-Host "?? 目标目录: $targetDir" -ForegroundColor Green
Write-Host ""

# 创建目标目录（如果不存在）
if (-not (Test-Path $targetDir)) {
    Write-Host "?? 创建目标目录..." -ForegroundColor Yellow
    New-Item -ItemType Directory -Path $targetDir -Force | Out-Null
}

# 尝试多种方法复制
$copied = $false
$attempts = @(
    @{
        Name = "方法1: 直接复制"
        Action = { Copy-Item $sourceDll $targetDll -Force -ErrorAction Stop }
    },
    @{
        Name = "方法2: 删除后复制"
        Action = { 
            if (Test-Path $targetDll) { Remove-Item $targetDll -Force -ErrorAction Stop }
            Copy-Item $sourceDll $targetDll -Force -ErrorAction Stop
        }
    },
    @{
        Name = "方法3: Robocopy"
        Action = { 
            $sourceDir = Split-Path $sourceDll -Parent
            robocopy $sourceDir $targetDir "TheSecondSeat.dll" /R:3 /W:1 /NFL /NDL /NJH /NJS | Out-Null
            if ($LASTEXITCODE -le 7) { return $true } else { throw "Robocopy failed" }
        }
    },
    @{
        Name = "方法4: 临时重命名"
        Action = {
            if (Test-Path $targetDll) {
                $backup = "$targetDll.old"
                Move-Item $targetDll $backup -Force -ErrorAction Stop
            }
            Copy-Item $sourceDll $targetDll -Force -ErrorAction Stop
            if (Test-Path "$targetDll.old") { Remove-Item "$targetDll.old" -Force -ErrorAction SilentlyContinue }
        }
    }
)

foreach ($attempt in $attempts) {
    Write-Host "?? 尝试: $($attempt.Name)..." -ForegroundColor Cyan
    try {
        & $attempt.Action
        $copied = $true
        Write-Host "   ? 成功！" -ForegroundColor Green
        break
    } catch {
        Write-Host "   ?? 失败: $($_.Exception.Message)" -ForegroundColor Yellow
    }
}

if (-not $copied) {
    Write-Host ""
    Write-Host "? 所有方法都失败了！" -ForegroundColor Red
    Write-Host ""
    Write-Host "?? 手动部署步骤：" -ForegroundColor Yellow
    Write-Host "1. 关闭 RimWorld 游戏" -ForegroundColor White
    Write-Host "2. 再次运行此脚本" -ForegroundColor White
    Write-Host "   或者手动复制文件：" -ForegroundColor White
    Write-Host "   从: $sourceDll" -ForegroundColor Cyan
    Write-Host "   到: $targetDll" -ForegroundColor Cyan
    exit 1
}

# 验证部署
Write-Host ""
Write-Host "?? 验证部署..." -ForegroundColor Cyan

$sourceInfo = Get-Item $sourceDll
$targetInfo = Get-Item $targetDll

if ($sourceInfo.Length -eq $targetInfo.Length) {
    Write-Host "? 文件大小匹配: $($sourceInfo.Length) bytes" -ForegroundColor Green
} else {
    Write-Host "?? 文件大小不匹配！" -ForegroundColor Yellow
    Write-Host "   源: $($sourceInfo.Length) bytes" -ForegroundColor Yellow
    Write-Host "   目标: $($targetInfo.Length) bytes" -ForegroundColor Yellow
}

Write-Host "? 最后修改: $($targetInfo.LastWriteTime)" -ForegroundColor Green

Write-Host ""
Write-Host "?? 部署完成！" -ForegroundColor Green
Write-Host ""
Write-Host "?? 版本信息: v1.7.6" -ForegroundColor Cyan
Write-Host "   - System Prompt矛盾修复" -ForegroundColor White
Write-Host "   - 0警告编译优化" -ForegroundColor White
Write-Host "   - SDK 8.0强制使用" -ForegroundColor White
Write-Host ""
Write-Host "?? 下一步:" -ForegroundColor Yellow
Write-Host "   1. 启动 RimWorld" -ForegroundColor White
Write-Host "   2. 测试命令功能" -ForegroundColor White
Write-Host "   3. 验证好感度影响" -ForegroundColor White
