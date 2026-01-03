# 综合部署脚本：The Second Seat & Sideria

$steamModsPath = "D:\steam\steamapps\common\RimWorld\Mods"

# 1. 部署主 Mod (The Second Seat)
$mainSource = "D:\rim mod\The Second Seat"
$mainDest = "$steamModsPath\The Second Seat"

Write-Host "正在部署主 Mod: The Second Seat..." -ForegroundColor Cyan
if (-not (Test-Path $mainDest)) { New-Item -ItemType Directory -Path $mainDest | Out-Null }

# 排除列表
$exclude = @(
    ".git", ".vs", ".vscode", "Source", "obj", "bin",
    "*.sln", "*.csproj", "*.user", "*.suo",
    "*.md", "*.ps1", "*.txt", "global.json"
)

# 复制主 Mod 文件
Copy-Item "$mainSource\*" "$mainDest\" -Recurse -Force -Exclude $exclude

# 2. 部署子 Mod (The Second Seat - Sideria)
$subSource = "D:\rim mod\The Second Seat - Sideria"
$subDest = "$steamModsPath\The Second Seat - Sideria"

Write-Host "正在部署子 Mod: Sideria..." -ForegroundColor Cyan
if (-not (Test-Path $subDest)) { New-Item -ItemType Directory -Path $subDest | Out-Null }

# 复制子 Mod 文件
Copy-Item "$subSource\*" "$subDest\" -Recurse -Force -Exclude $exclude

# 3. 部署子 Mod (The Second Seat - Cthulhu)
$cthulhuSource = "D:\rim mod\The Second Seat - Cthulhu"
$cthulhuDest = "$steamModsPath\The Second Seat - Cthulhu"

if (Test-Path $cthulhuSource) {
    Write-Host "正在部署子 Mod: Cthulhu..." -ForegroundColor Cyan
    if (-not (Test-Path $cthulhuDest)) { New-Item -ItemType Directory -Path $cthulhuDest | Out-Null }
    Copy-Item "$cthulhuSource\*" "$cthulhuDest\" -Recurse -Force -Exclude $exclude
}

Write-Host "✓ 所有部署完成！" -ForegroundColor Green
Write-Host "目标目录: $steamModsPath" -ForegroundColor White