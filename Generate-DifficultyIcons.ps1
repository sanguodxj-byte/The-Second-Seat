# 生成难度模式占位符图标
# 用于在没有正式图标时显示基本占位符

$ErrorActionPreference = "Stop"

Write-Host "=== 生成难度模式占位符图标 ===" -ForegroundColor Cyan

$targetDir = "Textures\UI\DifficultyMode"

# 确保目录存在
if (-not (Test-Path $targetDir)) {
    New-Item -ItemType Directory -Path $targetDir -Force | Out-Null
    Write-Host "? 创建目录: $targetDir" -ForegroundColor Green
}

# 使用 .NET 生成简单的占位符图片
Add-Type -AssemblyName System.Drawing

function Create-PlaceholderIcon {
    param(
        [string]$FilePath,
        [int]$Width,
        [int]$Height,
        [System.Drawing.Color]$BackgroundColor,
        [string]$Text,
        [System.Drawing.Color]$TextColor
    )
    
    $bitmap = New-Object System.Drawing.Bitmap($Width, $Height)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    
    # 填充背景
    $brush = New-Object System.Drawing.SolidBrush($BackgroundColor)
    $graphics.FillRectangle($brush, 0, 0, $Width, $Height)
    
    # 绘制边框
    $pen = New-Object System.Drawing.Pen([System.Drawing.Color]::White, 2)
    $graphics.DrawRectangle($pen, 1, 1, ($Width - 3), ($Height - 3))
    
    # 计算字体大小
    $fontSize = [Math]::Min($Width, $Height) / 4
    
    # 绘制文字
    $font = New-Object System.Drawing.Font("Arial", $fontSize, [System.Drawing.FontStyle]::Bold)
    $textBrush = New-Object System.Drawing.SolidBrush($TextColor)
    $format = New-Object System.Drawing.StringFormat
    $format.Alignment = [System.Drawing.StringAlignment]::Center
    $format.LineAlignment = [System.Drawing.StringAlignment]::Center
    $rect = New-Object System.Drawing.RectangleF(0, 0, $Width, $Height)
    $graphics.DrawString($Text, $font, $textBrush, $rect, $format)
    
    # 保存
    $bitmap.Save($FilePath, [System.Drawing.Imaging.ImageFormat]::Png)
    
    # 清理
    $graphics.Dispose()
    $bitmap.Dispose()
    $brush.Dispose()
    $pen.Dispose()
    $font.Dispose()
    $textBrush.Dispose()
    
    Write-Host "? 已创建: $FilePath" -ForegroundColor Green
}

# 生成助手模式图标（蓝色）
$assistantColor = [System.Drawing.Color]::FromArgb(255, 70, 130, 180)
$assistantTextColor = [System.Drawing.Color]::White

# 生成对弈者模式图标（红棕色）
$opponentColor = [System.Drawing.Color]::FromArgb(255, 139, 69, 19)
$opponentTextColor = [System.Drawing.Color]::White

Write-Host "`n[1/4] 生成助手模式小图标..." -ForegroundColor Yellow
Create-PlaceholderIcon `
    -FilePath "$targetDir\assistant_icon.png" `
    -Width 64 `
    -Height 64 `
    -BackgroundColor $assistantColor `
    -Text "助" `
    -TextColor $assistantTextColor

Write-Host "[2/4] 生成对弈者模式小图标..." -ForegroundColor Yellow
Create-PlaceholderIcon `
    -FilePath "$targetDir\opponent_icon.png" `
    -Width 64 `
    -Height 64 `
    -BackgroundColor $opponentColor `
    -Text "弈" `
    -TextColor $opponentTextColor

Write-Host "[3/4] 生成助手模式大图..." -ForegroundColor Yellow
Create-PlaceholderIcon `
    -FilePath "$targetDir\assistant_large.png" `
    -Width 256 `
    -Height 256 `
    -BackgroundColor $assistantColor `
    -Text "助手" `
    -TextColor $assistantTextColor

Write-Host "[4/4] 生成对弈者模式大图..." -ForegroundColor Yellow
Create-PlaceholderIcon `
    -FilePath "$targetDir\opponent_large.png" `
    -Width 256 `
    -Height 256 `
    -BackgroundColor $opponentColor `
    -Text "对弈" `
    -TextColor $opponentTextColor

Write-Host "`n=== 占位符图标生成完成 ===" -ForegroundColor Green

Write-Host "`n已生成文件:" -ForegroundColor Cyan
Get-ChildItem "$targetDir\*.png" | ForEach-Object {
    $size = [math]::Round($_.Length / 1KB, 2)
    Write-Host "  $($_.Name) ($size KB)" -ForegroundColor White
}

Write-Host "`n提示:" -ForegroundColor Yellow
Write-Host "  这些是临时占位符图标" -ForegroundColor White
Write-Host "  请替换为正式设计的图标" -ForegroundColor White
Write-Host "  运行 .\Smart-Deploy.ps1 部署到游戏" -ForegroundColor White
