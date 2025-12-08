# 表情变体系统 - 完整部署脚本（修复版）
# 部署到正确的 RimWorld Mods 目录的 1.6 文件夹

$SourceDir = "C:\Users\Administrator\Desktop\rim mod\The Second Seat"
$TargetDir = "D:\steam\steamapps\common\RimWorld\Mods\TheSecondSeat\1.6"

Write-Host "====================================" -ForegroundColor Cyan
Write-Host "  表情变体系统 - 完整部署工具 v1.6" -ForegroundColor Cyan
Write-Host "  ? 修复版：部署到 1.6 文件夹" -ForegroundColor Yellow
Write-Host "====================================" -ForegroundColor Cyan
Write-Host ""

# 检查源目录
if (!(Test-Path $SourceDir)) {
    Write-Host "? 源目录不存在: $SourceDir" -ForegroundColor Red
    exit 1
}

# 确保目标目录存在
if (!(Test-Path $TargetDir)) {
    Write-Host "?? 创建 1.6 版本文件夹..." -ForegroundColor Cyan
    New-Item -Path $TargetDir -ItemType Directory -Force | Out-Null
    Write-Host "? 1.6 文件夹已创建" -ForegroundColor Green
}

Write-Host "?? 目标目录: $TargetDir" -ForegroundColor Cyan
Write-Host ""

# ==================== 第一部分：源文件部署 ====================
Write-Host "?? 第一部分：部署源代码文件" -ForegroundColor Yellow
Write-Host ""

$sourceFiles = @(
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

foreach ($file in $sourceFiles) {
    $sourcePath = Join-Path $SourceDir $file.Source
    $targetPath = Join-Path $TargetDir $file.Target
    
    if (!(Test-Path $sourcePath)) {
        Write-Host "? 源文件不存在: $($file.Name)" -ForegroundColor Red
        $errorCount++
        continue
    }
    
    try {
        # 确保目标目录存在
        $targetDirPath = Split-Path $targetPath -Parent
        if (!(Test-Path $targetDirPath)) {
            New-Item -Path $targetDirPath -ItemType Directory -Force | Out-Null
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
Write-Host "───────────────────────────────────" -ForegroundColor Gray
Write-Host "源文件部署完成: $successCount 成功, $errorCount 失败" -ForegroundColor $(if ($errorCount -gt 0) { "Yellow" } else { "Green" })
Write-Host ""

# ==================== 第二部分：材质文件部署 ====================
Write-Host "?? 第二部分：部署材质文件夹（Textures）" -ForegroundColor Yellow
Write-Host ""

$textureSourceDir = Join-Path $SourceDir "Textures"
$textureTargetDir = Join-Path $TargetDir "Textures"

if (!(Test-Path $textureSourceDir)) {
    Write-Host "?? 材质源目录不存在，跳过材质部署" -ForegroundColor Yellow
    Write-Host "   路径: $textureSourceDir" -ForegroundColor Gray
}
else {
    try {
        # 确保目标目录存在
        if (!(Test-Path $textureTargetDir)) {
            New-Item -Path $textureTargetDir -ItemType Directory -Force | Out-Null
        }
        
        # 复制整个 Textures 文件夹（递归）
        Write-Host "?? 正在复制材质文件夹到 1.6 目录..." -ForegroundColor Cyan
        
        # 使用 robocopy 进行高效复制（保留目录结构）
        $robocopyResult = robocopy $textureSourceDir $textureTargetDir /E /XO /NFL /NDL /NJH /NJS
        
        if ($LASTEXITCODE -le 7) {
            Write-Host "? 材质文件夹部署成功" -ForegroundColor Green
            
            # 统计文件数量
            $textureCount = (Get-ChildItem $textureTargetDir -Recurse -File).Count
            Write-Host "   共部署 $textureCount 个材质文件到 1.6 文件夹" -ForegroundColor Gray
            
            # 列出关键文件夹
            $keyFolders = @(
                "UI\Narrators\9x16",
                "UI\Narrators\Avatars",
                "UI\StatusIcons"
            )
            
            foreach ($folder in $keyFolders) {
                $folderPath = Join-Path $textureTargetDir $folder
                if (Test-Path $folderPath) {
                    $fileCount = (Get-ChildItem $folderPath -Recurse -File -Filter "*.png").Count
                    Write-Host "   ?? $folder : $fileCount 个文件" -ForegroundColor Cyan
                }
            }
        }
        else {
            Write-Host "?? 材质文件夹复制可能不完整（退出码: $LASTEXITCODE）" -ForegroundColor Yellow
        }
    }
    catch {
        Write-Host "? 材质文件夹部署失败: $_" -ForegroundColor Red
        $errorCount++
    }
}

Write-Host ""
Write-Host "====================================" -ForegroundColor Cyan
Write-Host "部署完成" -ForegroundColor Cyan
Write-Host "  目标路径: $TargetDir" -ForegroundColor Green
Write-Host "  源文件: $successCount 成功, $errorCount 失败" -ForegroundColor $(if ($errorCount -gt 0) { "Yellow" } else { "Green" })
Write-Host "  材质文件: 已部署到 1.6 文件夹" -ForegroundColor Green
Write-Host "====================================" -ForegroundColor Cyan
Write-Host ""

if ($successCount -gt 0) {
    Write-Host "? 文件已部署到 RimWorld 1.6 模组目录" -ForegroundColor Green
    Write-Host "?? 下一步操作：" -ForegroundColor Yellow
    Write-Host "   1. 启动RimWorld" -ForegroundColor White
    Write-Host "   2. 游戏会自动编译源文件" -ForegroundColor White
    Write-Host "   3. 测试表情变体功能" -ForegroundColor White
    Write-Host ""
    Write-Host "?? 部署路径确认：" -ForegroundColor Yellow
    Write-Host "   $TargetDir" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "?? 材质文件说明：" -ForegroundColor Yellow
    Write-Host "   - 9x16 文件夹: 全身立绘（1080x1920）" -ForegroundColor White
    Write-Host "   - Avatars 文件夹: UI按钮头像（512x512）" -ForegroundColor White
    Write-Host "   - 每个表情支持 1-5 个变体" -ForegroundColor White
    Write-Host "   - 文件命名: {Persona}_happy1.png, _happy2.png..." -ForegroundColor White
}

if ($errorCount -gt 0) {
    Write-Host ""
    Write-Host "?? 部分文件部署失败，请检查错误信息" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "按任意键退出..." -ForegroundColor Gray
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
