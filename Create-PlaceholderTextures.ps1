# 占位符纹理创建脚本
# 创建缺失的占位符PNG图像

# 256x256 透明PNG占位符 (Base64编码的最小PNG)
$placeholderPng = [Convert]::FromBase64String('iVBORw0KGgoAAAANSUhEUgAAAQAAAAEACAYAAABccqhmAAAACXBIWXMAAAsTAAALEwEAmpwYAAABHklEQVR4nO3BMQEAAADCoPVP7WsIoAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAeA0LAD6KAAEAAQBJRU5ErkJggg==')

# 定义需要创建的占位符路径
$texturePaths = @(
    # Cassandra
    "Textures\UI\Narrators\Descent\Postures\Cassandra\standing.png",
    "Textures\UI\Narrators\Descent\Effects\Cassandra\glow.png",
    
    # Phoebe
    "Textures\UI\Narrators\Descent\Postures\Phoebe\standing.png",
    "Textures\UI\Narrators\Descent\Effects\Phoebe\glow.png",
    
    # Randy
    "Textures\UI\Narrators\Descent\Postures\Randy\standing.png",
    "Textures\UI\Narrators\Descent\Effects\Randy\glow.png",
    
    # Igor
    "Textures\UI\Narrators\Descent\Postures\Igor\standing.png",
    "Textures\UI\Narrators\Descent\Effects\Igor\glow.png",
    
    # Luna
    "Textures\UI\Narrators\Descent\Postures\Luna\standing.png",
    "Textures\UI\Narrators\Descent\Effects\Luna\glow.png",
    
    # Sideria - Descent Postures
    "Textures\UI\Narrators\Descent\Postures\Sideria\standing.png",
    "Textures\UI\Narrators\Descent\Postures\Sideria\floating.png",
    "Textures\UI\Narrators\Descent\Postures\Sideria\combat.png",
    
    # Sideria - Layered Portrait
    "Textures\UI\Narrators\9x16\Layered\Sideria\base_body.png",
    "Textures\UI\Narrators\9x16\Layered\Sideria\eyes_normal.png",
    "Textures\UI\Narrators\9x16\Layered\Sideria\mouth_normal.png",
    
    # Cthulhu (克苏鲁)
    "Textures\UI\Narrators\Descent\Postures\Cthulhu\standing.png",
    "Textures\UI\Narrators\Descent\Postures\Cthulhu\floating.png",
    "Textures\UI\Narrators\Descent\Postures\Cthulhu\combat.png"
)

Write-Host "Creating placeholder textures..." -ForegroundColor Cyan

foreach ($path in $texturePaths) {
    $fullPath = Join-Path $PSScriptRoot $path
    $directory = Split-Path $fullPath -Parent
    
    # 确保目录存在
    if (-not (Test-Path $directory)) {
        New-Item -ItemType Directory -Path $directory -Force | Out-Null
        Write-Host "  Created directory: $directory" -ForegroundColor DarkGray
    }
    
    # 如果文件不存在则创建占位符
    if (-not (Test-Path $fullPath)) {
        [System.IO.File]::WriteAllBytes($fullPath, $placeholderPng)
        Write-Host "  Created: $path" -ForegroundColor Green
    } else {
        Write-Host "  Exists: $path" -ForegroundColor Yellow
    }
}

Write-Host "`n✓ Placeholder textures created successfully!" -ForegroundColor Green