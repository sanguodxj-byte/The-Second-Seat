# 验证立绘保护脚本

Write-Host "`n=== 立绘文件验证 ===" -ForegroundColor Cyan

$narratorsPath = "D:\steam\steamapps\common\RimWorld\Mods\TheSecondSeat\Textures\UI\Narrators"

if (-not (Test-Path $narratorsPath)) {
    Write-Host "? 立绘文件夹不存在：$narratorsPath" -ForegroundColor Red
    exit 1
}

Write-Host "`n?? 立绘文件夹：$narratorsPath" -ForegroundColor Yellow
Write-Host "`n检测到的立绘文件：" -ForegroundColor Cyan

$portraits = Get-ChildItem -Path $narratorsPath -Filter "*.png" | Sort-Object Length -Descending

if ($portraits.Count -eq 0) {
    Write-Host "  ?? 没有找到任何 PNG 文件" -ForegroundColor Yellow
} else {
    $realCount = 0
    $placeholderCount = 0
    
    foreach ($file in $portraits) {
        $sizeMB = [math]::Round($file.Length / 1MB, 3)
        $sizeKB = [math]::Round($file.Length / 1KB, 1)
        
        # 判断是真实立绘还是占位符
        if ($file.Length -gt 10KB) {
            $realCount++
            Write-Host "  ? $($file.Name)" -ForegroundColor Green -NoNewline
            Write-Host " - $sizeMB MB" -ForegroundColor Cyan -NoNewline
            Write-Host " (真实立绘)" -ForegroundColor White
        } else {
            $placeholderCount++
            Write-Host "  ?? $($file.Name)" -ForegroundColor Yellow -NoNewline
            Write-Host " - $sizeKB KB" -ForegroundColor Gray -NoNewline
            Write-Host " (占位符)" -ForegroundColor DarkGray
        }
    }
    
    Write-Host "`n" + "-"*60 -ForegroundColor Gray
    Write-Host "?? 统计：" -ForegroundColor Yellow
    Write-Host "  真实立绘：$realCount 个" -ForegroundColor Green
    Write-Host "  占位符：$placeholderCount 个" -ForegroundColor Yellow
    Write-Host "  总计：$($portraits.Count) 个" -ForegroundColor Cyan
    
    if ($realCount -gt 0) {
        Write-Host "`n? 立绘文件已保护！" -ForegroundColor Green -BackgroundColor Black
        Write-Host "  真实立绘没有被占位符覆盖" -ForegroundColor White
    } else {
        Write-Host "`n?? 只有占位符，没有真实立绘" -ForegroundColor Yellow
        Write-Host "  ?? 请将真实立绘 PNG 文件放入此文件夹" -ForegroundColor White
    }
}

Write-Host "`n"
