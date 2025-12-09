$sourceDir = "C:\Users\Administrator\Desktop\rim mod\The Second Seat"
$targetDir = "D:\steam\steamapps\common\RimWorld\Mods\TheSecondSeat"

Write-Host "?? 创建并部署 LoadFolders.xml..." -ForegroundColor Cyan

# 创建 LoadFolders.xml 内容
$loadFoldersContent = @"
<?xml version="1.0" encoding="utf-8"?>
<loadFolders>
  <v1.5>
    <li>1.5</li>
    <li>/</li>
  </v1.5>
  <v1.6>
    <li>1.6</li>
    <li>/</li>
  </v1.6>
</loadFolders>
"@

# 写入源目录
$loadFoldersContent | Out-File -FilePath "$sourceDir\LoadFolders.xml" -Encoding UTF8 -Force
Write-Host "? 已创建源目录 LoadFolders.xml" -ForegroundColor Green

# 写入目标目录
$loadFoldersContent | Out-File -FilePath "$targetDir\LoadFolders.xml" -Encoding UTF8 -Force
Write-Host "? 已部署目标目录 LoadFolders.xml" -ForegroundColor Green

Write-Host "`n?? 验证内容:" -ForegroundColor Yellow
Get-Content "$targetDir\LoadFolders.xml"

Write-Host "`n?? LoadFolders.xml 创建并部署完成！" -ForegroundColor Green
Write-Host "现在请重启 RimWorld 测试。" -ForegroundColor Cyan
