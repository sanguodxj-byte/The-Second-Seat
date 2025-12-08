# 表情变体系统 - 源文件部署脚本
# 直接复制源文件到RimWorld，让游戏自动编译

$SourceDir = "C:\Users\Administrator\Desktop\rim mod\The Second Seat"
$TargetDir = "D:\steam\steamapps\common\RimWorld\Mods\TheSecondSeat"

Write-Host "====================================" -ForegroundColor Cyan
Write-Host "  表情变体系统 - 源文件部署工具" -ForegroundColor Cyan
Write-Host "====================================" -ForegroundColor Cyan
Write-Host ""

# 检查目录
if (!(Test-Path $SourceDir)) {
    Write-Host "? 源目录不存在: $SourceDir" -ForegroundColor Red
    exit 1
}

if (!(Test-Path $TargetDir)) {
    Write-Host "? 目标目录不存在: $TargetDir" -ForegroundColor Red
    exit 1
}

# 复制关键文件
$files = @(
    @{
        Source = "Source\TheSecondSeat\PersonaGeneration\ExpressionSystem.cs"
        Target = "Source\TheSecondSeat\PersonaGeneration\ExpressionSystem.cs"
        Name = "表情系统核心"
    },
    @{
        Source = "Source\TheSecondSeat\UI\NarratorScreenButton.cs"
        Target = "Source\TheSecondSeat\UI\NarratorScreenButton.cs"
        Name = "触摸互动界面"
    },
    @{
        Source = "Source\TheSecondSeat\PersonaGeneration\PortraitLoader.cs"
        Target = "Source\TheSecondSeat\PersonaGeneration\PortraitLoader.cs"
        Name = "立绘加载器"
    },
    @{
        Source = "Source\TheSecondSeat\PersonaGeneration\AvatarLoader.cs"
        Target = "Source\TheSecondSeat\PersonaGeneration\AvatarLoader.cs"
        Name = "头像加载器"
    }
)

$successCount = 0
$errorCount = 0

foreach ($file in $files) {
    $sourcePath = Join-Path $SourceDir $file.Source
    $targetPath = Join-Path $TargetDir $file.Target
    
    if (!(Test-Path $sourcePath)) {
        Write-Host "? 源文件不存在: $($file.Name)" -ForegroundColor Red
        $errorCount++
        continue
    }
    
    try {
        # 确保目标目录存在
        $targetDir = Split-Path $targetPath -Parent
        if (!(Test-Path $targetDir)) {
            New-Item -Path $targetDir -ItemType Directory -Force | Out-Null
        }
        
        Copy-Item $sourcePath -Destination $targetPath -Force
        Write-Host "? $($file.Name) 已部署" -ForegroundColor Green
        $successCount++
    }
    catch {
        Write-Host "? $($file.Name) 部署失败: $_" -ForegroundColor Red
        $errorCount++
    }
}

Write-Host ""
Write-Host "====================================" -ForegroundColor Cyan
Write-Host "部署完成" -ForegroundColor Cyan
Write-Host "  成功: $successCount 个文件" -ForegroundColor Green
Write-Host "  失败: $errorCount 个文件" -ForegroundColor $(if ($errorCount -gt 0) { "Red" } else { "Green" })
Write-Host "====================================" -ForegroundColor Cyan
Write-Host ""

if ($successCount -gt 0) {
    Write-Host "? 源文件已部署到RimWorld模组目录" -ForegroundColor Green
    Write-Host "?? 下一步操作：" -ForegroundColor Yellow
    Write-Host "   1. 启动RimWorld" -ForegroundColor White
    Write-Host "   2. 游戏会自动编译源文件" -ForegroundColor White
    Write-Host "   3. 测试表情变体功能" -ForegroundColor White
    Write-Host ""
    Write-Host "?? 表情变体说明：" -ForegroundColor Yellow
    Write-Host "   - 每个表情支持 1-5 个变体" -ForegroundColor White
    Write-Host "   - 文件命名: {Persona}_happy1.png, _happy2.png..." -ForegroundColor White
    Write-Host "   - 变体不存在时自动回退到基础版本" -ForegroundColor White
}

if ($errorCount -gt 0) {
    Write-Host ""
    Write-Host "?? 部分文件部署失败，请检查错误信息" -ForegroundColor Yellow
}
