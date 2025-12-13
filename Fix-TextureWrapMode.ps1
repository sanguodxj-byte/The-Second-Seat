# Fix-TextureWrapMode.ps1 - 添加 texture.wrapMode = TextureWrapMode.Clamp

$filePath = "Source\TheSecondSeat\PersonaGeneration\PortraitLoader.cs"

# 读取文件
$content = Get-Content $filePath -Raw -Encoding UTF8

# 在 texture.anisoLevel = 4; 后面添加 wrapMode 设置
$newContent = $content -replace '(texture\.anisoLevel = 4;)', @'
$1
                
                // ? [核心修复] 2. 循环模式设为 Clamp (钳制)
                // 这行代码是消除边缘黑线/杂色的关键！
                // 它告诉 GPU：不要去采样对面的像素，边缘是什么就是什么。
                texture.wrapMode = TextureWrapMode.Clamp;
'@

# 写回文件
$newContent | Set-Content $filePath -Encoding UTF8 -NoNewline

Write-Host "? 已添加 texture.wrapMode = TextureWrapMode.Clamp" -ForegroundColor Green
