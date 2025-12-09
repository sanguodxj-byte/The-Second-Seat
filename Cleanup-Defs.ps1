$defsDir = "D:\steam\steamapps\common\RimWorld\Mods\TheSecondSeat\Defs"
Write-Host "?? 检查并清理重复的 XML 文件..." -ForegroundColor Cyan

if (Test-Path $defsDir) {
    # 列出所有 XML 文件
    $xmlFiles = Get-ChildItem -Path $defsDir -Filter "*.xml" -Recurse
    Write-Host "`n找到 $($xmlFiles.Count) 个 XML 文件:" -ForegroundColor Yellow
    foreach ($file in $xmlFiles) {
        Write-Host "  - $($file.Name)"
    }
    
    Write-Host "`n?? 清理测试和备份文件..." -ForegroundColor Cyan
    
    # 删除所有测试文件
    Get-ChildItem -Path $defsDir -Filter "*TEST*.xml" -Recurse | ForEach-Object {
        Write-Host "  删除: $($_.Name)" -ForegroundColor Yellow
        Remove-Item $_.FullName -Force
    }
    
    # 删除所有备份文件
    Get-ChildItem -Path $defsDir -Filter "*FIXED*.xml" -Recurse | ForEach-Object {
        Write-Host "  删除: $($_.Name)" -ForegroundColor Yellow
        Remove-Item $_.FullName -Force
    }
    
    # 删除所有 .bak 文件
    Get-ChildItem -Path $defsDir -Filter "*.bak" -Recurse | ForEach-Object {
        Write-Host "  删除: $($_.Name)" -ForegroundColor Yellow
        Remove-Item $_.FullName -Force
    }
    
    Write-Host "`n? 清理完成！" -ForegroundColor Green
    
    # 验证剩余文件
    Write-Host "`n?? 剩余的 XML 文件:" -ForegroundColor Cyan
    Get-ChildItem -Path $defsDir -Filter "*.xml" -Recurse | ForEach-Object {
        Write-Host "  ? $($_.Name)" -ForegroundColor Green
    }
} else {
    Write-Host "? Defs 目录不存在: $defsDir" -ForegroundColor Red
}

Write-Host "`n?? 完成！请重启 RimWorld 测试" -ForegroundColor Cyan
