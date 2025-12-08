#!/usr/bin/env pwsh
# 自动修复表情文件格式 - 将 JPG 转换为 PNG

Write-Host "?? 自动修复表情文件格式" -ForegroundColor Cyan
Write-Host ""

$expressionPath = "Textures\UI\Narrators\9x16\Expressions\Sideria"

if (-not (Test-Path $expressionPath)) {
    Write-Host "? 表情文件夹不存在: $expressionPath" -ForegroundColor Red
    exit 1
}

# 检查是否安装了 .NET 图像处理库
Add-Type -AssemblyName System.Drawing

# 查找所有 JPG 文件
$jpgFiles = Get-ChildItem $expressionPath -File | Where-Object { $_.Extension -in @('.jpg', '.jpeg') }

if ($jpgFiles.Count -eq 0) {
    Write-Host "? 没有需要转换的 JPG 文件！" -ForegroundColor Green
    exit 0
}

Write-Host "?? 找到 $($jpgFiles.Count) 个 JPG 文件需要转换:" -ForegroundColor Yellow
Write-Host ""

foreach ($file in $jpgFiles) {
    Write-Host "   - $($file.Name)" -ForegroundColor White
}

Write-Host ""
Write-Host "?? 开始转换..." -ForegroundColor Cyan
Write-Host ""

$successCount = 0
$failCount = 0

foreach ($file in $jpgFiles) {
    try {
        $baseName = [System.IO.Path]::GetFileNameWithoutExtension($file.FullName)
        $newPath = Join-Path $expressionPath "$baseName.png"
        
        Write-Host "转换: $($file.Name) → $baseName.png" -ForegroundColor Yellow
        
        # 加载 JPG 图片
        $image = [System.Drawing.Image]::FromFile($file.FullName)
        
        # 保存为 PNG（无损格式）
        $image.Save($newPath, [System.Drawing.Imaging.ImageFormat]::Png)
        $image.Dispose()
        
        Write-Host "   ? 转换成功！" -ForegroundColor Green
        
        # 询问是否删除原文件
        Write-Host "   是否删除原 JPG 文件？(Y/N): " -NoNewline -ForegroundColor Yellow
        $response = Read-Host
        
        if ($response -eq 'Y' -or $response -eq 'y') {
            Remove-Item $file.FullName -Force
            Write-Host "   ??? 已删除原文件" -ForegroundColor Gray
        } else {
            Write-Host "   ?? 保留原文件" -ForegroundColor Gray
        }
        
        $successCount++
        Write-Host ""
        
    } catch {
        Write-Host "   ? 转换失败: $($_.Exception.Message)" -ForegroundColor Red
        $failCount++
        Write-Host ""
    }
}

Write-Host ""
Write-Host "?? 转换结果:" -ForegroundColor Cyan
Write-Host "   成功: $successCount" -ForegroundColor Green
Write-Host "   失败: $failCount" -ForegroundColor Red
Write-Host ""

if ($successCount -gt 0) {
    Write-Host "? 转换完成！请重新启动游戏测试表情切换。" -ForegroundColor Green
} else {
    Write-Host "? 没有成功转换任何文件。" -ForegroundColor Red
    Write-Host ""
    Write-Host "?? 替代方法:" -ForegroundColor Yellow
    Write-Host "   1. 使用 Photoshop/GIMP 手动转换" -ForegroundColor White
    Write-Host "   2. 使用在线工具: https://www.aconvert.com/image/jpg-to-png/" -ForegroundColor White
    Write-Host "   3. 安装 ImageMagick: magick convert smug.jpg smug.png" -ForegroundColor White
}
