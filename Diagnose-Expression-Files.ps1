#!/usr/bin/env pwsh
# 表情文件诊断脚本 - 检查文件格式一致性

Write-Host "?? 表情文件诊断 - 文件格式检查" -ForegroundColor Cyan
Write-Host ""

$expressionPath = "Textures\UI\Narrators\9x16\Expressions\Sideria"

if (-not (Test-Path $expressionPath)) {
    Write-Host "? 表情文件夹不存在: $expressionPath" -ForegroundColor Red
    exit 1
}

Write-Host "?? 检查目录: $expressionPath" -ForegroundColor Green
Write-Host ""

# 获取所有图片文件
$files = Get-ChildItem $expressionPath -File | Where-Object { $_.Extension -in @('.png', '.jpg', '.jpeg') }

Write-Host "?? 文件格式统计:" -ForegroundColor Yellow
Write-Host ""

# 按格式分组
$pngFiles = $files | Where-Object { $_.Extension -eq '.png' }
$jpgFiles = $files | Where-Object { $_.Extension -in @('.jpg', '.jpeg') }

Write-Host "? PNG 文件: $($pngFiles.Count)" -ForegroundColor Green
foreach ($file in $pngFiles) {
    Write-Host "   - $($file.Name)" -ForegroundColor White
}

Write-Host ""
Write-Host "? JPG 文件: $($jpgFiles.Count)" -ForegroundColor Red
foreach ($file in $jpgFiles) {
    Write-Host "   - $($file.Name)" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "?? 问题分析:" -ForegroundColor Cyan
Write-Host ""

if ($jpgFiles.Count -gt 0) {
    Write-Host "?? 发现问题:" -ForegroundColor Red
    Write-Host "   以下文件使用了 JPG 格式，但代码只加载 PNG 文件！" -ForegroundColor Yellow
    Write-Host ""
    
    foreach ($file in $jpgFiles) {
        $baseName = [System.IO.Path]::GetFileNameWithoutExtension($file.Name)
        $newName = "$baseName.png"
        
        Write-Host "   ? $($file.Name)" -ForegroundColor Red
        Write-Host "      → 需要转换为: $newName" -ForegroundColor Yellow
        Write-Host "      → 表情类型: $baseName" -ForegroundColor Cyan
        Write-Host ""
    }
    
    Write-Host "?? 修复方法:" -ForegroundColor Green
    Write-Host ""
    Write-Host "方法1: 使用图片编辑软件转换" -ForegroundColor White
    Write-Host "   1. 用 Photoshop/GIMP 打开 JPG 文件" -ForegroundColor Gray
    Write-Host "   2. 另存为 PNG 格式" -ForegroundColor Gray
    Write-Host "   3. 删除原 JPG 文件" -ForegroundColor Gray
    Write-Host ""
    
    Write-Host "方法2: 使用 PowerShell 批量转换（需要安装 ImageMagick）" -ForegroundColor White
    Write-Host "   magick convert smug.jpg smug.png" -ForegroundColor Gray
    Write-Host ""
    
    Write-Host "方法3: 在线转换工具" -ForegroundColor White
    Write-Host "   https://www.aconvert.com/image/jpg-to-png/" -ForegroundColor Gray
    Write-Host ""
} else {
    Write-Host "? 所有文件格式正确！" -ForegroundColor Green
}

Write-Host ""
Write-Host "?? 表情文件清单:" -ForegroundColor Cyan
Write-Host ""

$expectedExpressions = @(
    "neutral",
    "happy",
    "sad",
    "angry",
    "surprised",
    "worried",
    "smug",
    "disappointed",
    "thoughtful",
    "annoyed",
    "playful",
    "shy"
)

foreach ($exp in $expectedExpressions) {
    $pngFile = $pngFiles | Where-Object { $_.Name -eq "$exp.png" }
    $jpgFile = $jpgFiles | Where-Object { $_.Name -in @("$exp.jpg", "$exp.jpeg") }
    
    if ($pngFile) {
        Write-Host "   ? $exp.png" -ForegroundColor Green
    } elseif ($jpgFile) {
        Write-Host "   ?? $($jpgFile.Name) (需要转换为 PNG)" -ForegroundColor Yellow
    } else {
        Write-Host "   ? $exp.png (缺失)" -ForegroundColor Red
    }
}

Write-Host ""
Write-Host "?? 代码检查:" -ForegroundColor Cyan
Write-Host ""

$codeSnippet = @"
// ExpressionCompositor.cs 中的文件加载逻辑：
string expressionPath = `$"{{EXPRESSIONS_PATH}}{{personaName}}/{{expressionFileName}}.png";
var texture = ContentFinder<Texture2D>.Get(expressionPath, false);

// 注意：代码硬编码了 .png 扩展名！
// 如果文件是 .jpg 格式，将无法加载！
"@

Write-Host $codeSnippet -ForegroundColor Gray

Write-Host ""
Write-Host "? 诊断完成！" -ForegroundColor Green
