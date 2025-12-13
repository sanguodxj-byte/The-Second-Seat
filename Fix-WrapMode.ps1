# Fix-WrapMode.ps1 - 添加 texture.wrapMode = TextureWrapMode.Clamp

$filePath = "Source\TheSecondSeat\PersonaGeneration\PortraitLoader.cs"

$content = @'
        /// <summary>
        /// ? 设置纹理高质量参数（安全版本）
        /// ? v1.6.37: 添加 wrapMode = Clamp 消除边缘黑线/杂色
        /// </summary>
        private static void SetTextureQuality(Texture2D texture)
        {
            if (texture == null) return;
            
            try
            {
                // 1. 过滤模式 (保持 Bilinear 以获得平滑缩放)
                texture.filterMode = FilterMode.Bilinear;
                texture.anisoLevel = 4;
                
                // ? [核心修复] 2. 循环模式设为 Clamp (钳制)
                // 这行代码是消除边缘黑线/杂色的关键！
                // 它告诉 GPU：不要去采样对面的像素，边缘是什么就是什么。
                texture.wrapMode = TextureWrapMode.Clamp;
                
                // 注意：不调用 texture.Apply()，因为 ContentFinder 加载的纹理是只读的
                // Apply 会触发 "Texture not readable" 错误
            }
            catch
            {
                // 静默忽略，纹理设置不是关键功能
            }
        }
'@

# 读取文件
$fileContent = Get-Content $filePath -Raw -Encoding UTF8

# 找到并替换方法
$pattern = '(\s+/// <summary>[\s\S]*?/// ? 设置纹理高质量参数[\s\S]*?)\s+private static void SetTextureQuality[\s\S]*?\n\s+\}'

$fileContent = $fileContent -replace $pattern, $content

# 写回文件
$fileContent | Set-Content $filePath -Encoding UTF8 -NoNewline

Write-Host "? 已修复 SetTextureQuality 方法，添加了 texture.wrapMode = TextureWrapMode.Clamp" -ForegroundColor Green
