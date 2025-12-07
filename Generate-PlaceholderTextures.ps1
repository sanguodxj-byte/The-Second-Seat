Write-Host ""
Write-Host "=== 生成占位符纹理 ===" -ForegroundColor Cyan
Write-Host ""

Add-Type -AssemblyName System.Drawing

$outputDir = "Textures\UI"
if (-not (Test-Path $outputDir)) {
    New-Item -Path $outputDir -ItemType Directory -Force | Out-Null
    Write-Host "? 已创建纹理目录" -ForegroundColor Green
}

Write-Host "?? 生成配置：" -ForegroundColor Yellow
Write-Host "  尺寸：532x532 像素（高质量）" -ForegroundColor White
Write-Host "  格式：PNG" -ForegroundColor White
Write-Host "  类型：纯色占位符" -ForegroundColor White
Write-Host "  说明：532x532 自动缩放到 48x48 显示" -ForegroundColor White
Write-Host ""

$textures = @(
    @{Name="NarratorButton_Ready.png"; Color="204,204,204"; Desc="就绪（灰白色）"},
    @{Name="NarratorButton_Processing.png"; Color="255,184,77"; Desc="处理中（琥珀色）"},
    @{Name="NarratorButton_Error.png"; Color="255,51,51"; Desc="错误（红色）"},
    @{Name="NarratorButton_Disabled.png"; Color="128,128,128"; Desc="禁用（灰色）"}
)

Write-Host "?? 正在生成纹理..." -ForegroundColor Yellow
Write-Host ""

foreach ($texture in $textures) {
    try {
        # 创建 532x532 位图
        $bitmap = New-Object System.Drawing.Bitmap(532, 532)
        $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
        
        # 解析颜色
        $rgb = $texture.Color -split ','
        $color = [System.Drawing.Color]::FromArgb(255, [int]$rgb[0], [int]$rgb[1], [int]$rgb[2])
        
        # 填充背景
        $brush = New-Object System.Drawing.SolidBrush($color)
        $graphics.FillRectangle($brush, 0, 0, 532, 532)
        
        # 添加边框（可选）
        $pen = New-Object System.Drawing.Pen([System.Drawing.Color]::White, 8)
        $graphics.DrawRectangle($pen, 4, 4, 524, 524)
        
        # 保存文件
        $path = Join-Path $outputDir $texture.Name
        $bitmap.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
        
        $fileInfo = Get-Item $path
        $sizeKB = [math]::Round($fileInfo.Length / 1KB, 2)
        
        Write-Host "  ? $($texture.Desc)" -ForegroundColor Green
        Write-Host "     文件：$($texture.Name)" -ForegroundColor DarkGray
        Write-Host "     大小：$sizeKB KB" -ForegroundColor DarkGray
        Write-Host "     尺寸：532x532 像素" -ForegroundColor DarkGray
        Write-Host "     颜色：RGB($($texture.Color))" -ForegroundColor DarkGray
        
        # 清理资源
        $pen.Dispose()
        $brush.Dispose()
        $graphics.Dispose()
        $bitmap.Dispose()
    }
    catch {
        Write-Host "  ? 生成失败：$($texture.Name)" -ForegroundColor Red
        Write-Host "     错误：$($_.Exception.Message)" -ForegroundColor Red
    }
}

Write-Host ""
Write-Host "=" * 60 -ForegroundColor Cyan
Write-Host "?? 占位符生成完成！" -ForegroundColor Magenta
Write-Host "=" * 60 -ForegroundColor Cyan

Write-Host ""
Write-Host "?? 生成统计：" -ForegroundColor Yellow
$generatedFiles = Get-ChildItem -Path $outputDir -Filter "NarratorButton_*.png"
Write-Host "  生成文件：$($generatedFiles.Count) / 4" -ForegroundColor White
$totalSize = ($generatedFiles | Measure-Object -Property Length -Sum).Sum / 1KB
Write-Host "  总大小：$([math]::Round($totalSize, 2)) KB" -ForegroundColor White

Write-Host ""
Write-Host "?? 注意事项：" -ForegroundColor Yellow
Write-Host "  - 这些是纯色占位符，仅用于测试" -ForegroundColor White
Write-Host "  - 建议后续替换为真实设计" -ForegroundColor White
Write-Host "  - 动画效果完全正常" -ForegroundColor White

Write-Host ""
Write-Host "?? 下一步：" -ForegroundColor Cyan
Write-Host "  1. 运行验证脚本" -ForegroundColor White
Write-Host "     .\Verify-Textures.ps1" -ForegroundColor Gray
Write-Host ""
Write-Host "  2. 部署到游戏" -ForegroundColor White
Write-Host "     .\一键部署.ps1" -ForegroundColor Gray
Write-Host ""
Write-Host "  3. 启动 RimWorld 测试" -ForegroundColor White
Write-Host "     查看右上角按钮（应显示彩色方块）" -ForegroundColor Gray
Write-Host ""
Write-Host "  4. 用真实设计替换" -ForegroundColor White
Write-Host "     参考：UI纹理文件完整指南.md" -ForegroundColor Gray

Write-Host ""
