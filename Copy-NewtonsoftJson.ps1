Write-Host "?? 复制 Newtonsoft.Json.dll 到 Mod 目录..." -ForegroundColor Cyan

$source = "$env:USERPROFILE\.nuget\packages\newtonsoft.json\13.0.1\lib\net45\Newtonsoft.Json.dll"
$target1 = "D:\steam\steamapps\common\RimWorld\Mods\TheSecondSeat\Assemblies\Newtonsoft.Json.dll"
$target2 = "D:\steam\steamapps\common\RimWorld\Mods\TheSecondSeat\1.6\Assemblies\Newtonsoft.Json.dll"

if (Test-Path $source) {
    Write-Host "`n? 找到源文件" -ForegroundColor Green
    Write-Host "   $source" -ForegroundColor Cyan
    
    # 复制到根目录
    Copy-Item $source $target1 -Force
    Write-Host "`n? 已复制到根目录 Assemblies\" -ForegroundColor Green
    
    # 复制到 1.6 目录
    Copy-Item $source $target2 -Force
    Write-Host "? 已复制到 1.6\Assemblies\" -ForegroundColor Green
    
    # 验证
    Write-Host "`n?? 验证文件:" -ForegroundColor Yellow
    if (Test-Path $target1) {
        $dll1 = Get-Item $target1
        Write-Host "  ? 根目录: $([math]::Round($dll1.Length/1KB, 2)) KB" -ForegroundColor Cyan
    }
    if (Test-Path $target2) {
        $dll2 = Get-Item $target2
        Write-Host "  ? 1.6目录: $([math]::Round($dll2.Length/1KB, 2)) KB" -ForegroundColor Cyan
    }
    
    Write-Host "`n?? 复制完成！" -ForegroundColor Green
    Write-Host "现在请完全关闭并重启 RimWorld" -ForegroundColor Yellow
} else {
    Write-Host "`n? 错误：找不到源文件" -ForegroundColor Red
    Write-Host "   路径: $source" -ForegroundColor Yellow
    Write-Host "`n请检查 NuGet 包是否已安装" -ForegroundColor Yellow
}
