# Fix-GetLayerTexture-WrapMode.ps1 - 修复 GetLayerTexture 的 wrapMode

$filePath = "Source\TheSecondSeat\PersonaGeneration\PortraitLoader.cs"

# 读取文件
$content = Get-Content $filePath -Raw -Encoding UTF8

# 修复 GetLayerTexture 中的 wrapMode（移除错误的注释，添加正确的代码）
$pattern = '(texture\.anisoLevel = 4;)\s*\r?\n\s*\r?\n\s*// \? \[核心修复\].*?\r?\n.*?\r?\n.*?\r?\n\s*texture\.wrapMode = TextureWrapMode\.Clamp; // 各向异性过滤（提升斜角质量）'

$replacement = @'
$1 // 各向异性过滤（提升斜角质量）
                texture.wrapMode = TextureWrapMode.Clamp; // ? 消除边缘杂色
'@

$newContent = $content -replace $pattern, $replacement

# 写回文件
$newContent | Set-Content $filePath -Encoding UTF8 -NoNewline

Write-Host "? 已修复 GetLayerTexture() 的 wrapMode 设置" -ForegroundColor Green
