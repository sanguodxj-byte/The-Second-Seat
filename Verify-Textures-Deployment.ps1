# 材质文件验证脚本（修复版）
# 检查 1.6 文件夹中的材质文件是否正确部署

$TargetDir = "D:\steam\steamapps\common\RimWorld\Mods\TheSecondSeat\1.6"
$TextureDir = Join-Path $TargetDir "Textures"

Write-Host "====================================" -ForegroundColor Cyan
Write-Host "  材质文件验证工具（1.6版本）" -ForegroundColor Cyan
Write-Host "====================================" -ForegroundColor Cyan
Write-Host ""

# 检查 1.6 目录
if (!(Test-Path $TargetDir)) {
    Write-Host "? 1.6 版本目录不存在: $TargetDir" -ForegroundColor Red
    exit 1
}

Write-Host "? 1.6 版本目录存在" -ForegroundColor Green
Write-Host "   路径: $TargetDir" -ForegroundColor Cyan
Write-Host ""

# 检查材质根目录
if (!(Test-Path $TextureDir)) {
    Write-Host "? 材质目录不存在: $TextureDir" -ForegroundColor Red
    exit 1
}

Write-Host "? 材质根目录存在" -ForegroundColor Green
Write-Host ""

# 检查关键文件夹
$keyFolders = @(
    @{ Path = "UI\Narrators\9x16"; Name = "立绘文件夹（9:16全身像）" },
    @{ Path = "UI\Narrators\Avatars"; Name = "头像文件夹（1:1按钮）" },
    @{ Path = "UI\StatusIcons"; Name = "状态图标文件夹" }
)

Write-Host "?? 关键文件夹检查：" -ForegroundColor Yellow
Write-Host ""

foreach ($folder in $keyFolders) {
    $folderPath = Join-Path $TextureDir $folder.Path
    
    if (Test-Path $folderPath) {
        $pngCount = (Get-ChildItem $folderPath -Recurse -File -Filter "*.png" -ErrorAction SilentlyContinue).Count
        $subFolders = (Get-ChildItem $folderPath -Directory -ErrorAction SilentlyContinue).Count
        
        Write-Host "? $($folder.Name)" -ForegroundColor Green
        Write-Host "   路径: $($folder.Path)" -ForegroundColor Gray
        Write-Host "   PNG文件: $pngCount 个" -ForegroundColor Cyan
        Write-Host "   子文件夹: $subFolders 个" -ForegroundColor Cyan
        Write-Host ""
    }
    else {
        Write-Host "? $($folder.Name) 不存在" -ForegroundColor Red
        Write-Host "   路径: $($folder.Path)" -ForegroundColor Gray
        Write-Host ""
    }
}

# 检查源文件
Write-Host "?? 源代码文件检查：" -ForegroundColor Yellow
Write-Host ""

$sourceDir = Join-Path $TargetDir "Source\TheSecondSeat"
if (Test-Path $sourceDir) {
    $csFiles = Get-ChildItem $sourceDir -Recurse -File -Filter "*.cs" -ErrorAction SilentlyContinue
    Write-Host "? 源代码文件夹存在" -ForegroundColor Green
    Write-Host "   C# 文件: $($csFiles.Count) 个" -ForegroundColor Cyan
    Write-Host ""
    
    $importantFiles = @(
        "PersonaGeneration\ExpressionSystem.cs",
        "UI\NarratorScreenButton.cs",
        "PersonaGeneration\PortraitLoader.cs",
        "PersonaGeneration\AvatarLoader.cs"
    )
    
    foreach ($file in $importantFiles) {
        $filePath = Join-Path $sourceDir $file
        if (Test-Path $filePath) {
            Write-Host "   ? $file" -ForegroundColor Green
        }
        else {
            Write-Host "   ? $file 缺失" -ForegroundColor Red
        }
    }
    Write-Host ""
}
else {
    Write-Host "? 源代码文件夹不存在" -ForegroundColor Red
    Write-Host ""
}

# 检查人格立绘文件夹
$portraitsDir = Join-Path $TextureDir "UI\Narrators\9x16"
if (Test-Path $portraitsDir) {
    $personaFolders = Get-ChildItem $portraitsDir -Directory -ErrorAction SilentlyContinue
    
    Write-Host "?? 已部署的人格立绘：" -ForegroundColor Yellow
    Write-Host ""
    
    if ($personaFolders.Count -eq 0) {
        Write-Host "?? 未找到任何人格文件夹" -ForegroundColor Yellow
        Write-Host "   请在以下路径创建人格文件夹：" -ForegroundColor Gray
        Write-Host "   $portraitsDir\{PersonaName}\" -ForegroundColor Cyan
    }
    else {
        foreach ($personaFolder in $personaFolders) {
            $basePng = Join-Path $personaFolder.FullName "base.png"
            $expressionFolder = Join-Path $personaFolder.FullName "Expressions"
            
            $hasBase = Test-Path $basePng
            $expressionCount = 0
            
            if (Test-Path $expressionFolder) {
                $expressionCount = (Get-ChildItem $expressionFolder -File -Filter "*.png" -ErrorAction SilentlyContinue).Count
            }
            
            $status = if ($hasBase) { "?" } else { "??" }
            Write-Host "$status $($personaFolder.Name)" -ForegroundColor $(if ($hasBase) { "Green" } else { "Yellow" })
            Write-Host "   基础立绘: $(if ($hasBase) { '存在' } else { '缺失' })" -ForegroundColor $(if ($hasBase) { "Green" } else { "Red" })
            Write-Host "   表情文件: $expressionCount 个" -ForegroundColor Cyan
            Write-Host ""
        }
    }
}

# 检查头像文件夹
$avatarsDir = Join-Path $TextureDir "UI\Narrators\Avatars"
if (Test-Path $avatarsDir) {
    $avatarFolders = Get-ChildItem $avatarsDir -Directory -ErrorAction SilentlyContinue
    
    Write-Host "?? 已部署的头像：" -ForegroundColor Yellow
    Write-Host ""
    
    if ($avatarFolders.Count -eq 0) {
        Write-Host "?? 未找到任何头像文件夹" -ForegroundColor Yellow
    }
    else {
        foreach ($avatarFolder in $avatarFolders) {
            $basePng = Join-Path $avatarFolder.FullName "base.png"
            $avatarCount = (Get-ChildItem $avatarFolder.FullName -File -Filter "*.png" -ErrorAction SilentlyContinue).Count
            
            $status = if (Test-Path $basePng) { "?" } else { "??" }
            Write-Host "$status $($avatarFolder.Name): $avatarCount 个文件" -ForegroundColor $(if (Test-Path $basePng) { "Green" } else { "Yellow" })
        }
        Write-Host ""
    }
}

# 检查表情变体文件
Write-Host "?? 表情变体文件检查：" -ForegroundColor Yellow
Write-Host ""

$expressionTypes = @("happy", "sad", "angry", "surprised", "worried", "smug", "disappointed", "thoughtful", "annoyed", "playful", "shy", "confused")

if (Test-Path $portraitsDir) {
    $personaFolders = Get-ChildItem $portraitsDir -Directory -ErrorAction SilentlyContinue
    
    foreach ($personaFolder in $personaFolders) {
        $expressionFolder = Join-Path $personaFolder.FullName "Expressions"
        
        if (Test-Path $expressionFolder) {
            Write-Host "?? $($personaFolder.Name):" -ForegroundColor Cyan
            
            foreach ($expression in $expressionTypes) {
                $baseFile = Join-Path $expressionFolder "${expression}.png"
                $variants = @()
                
                for ($i = 1; $i -le 5; $i++) {
                    $variantFile = Join-Path $expressionFolder "${expression}${i}.png"
                    if (Test-Path $variantFile) {
                        $variants += $i
                    }
                }
                
                if ((Test-Path $baseFile) -or $variants.Count -gt 0) {
                    $status = if (Test-Path $baseFile) { "?" } else { "??" }
                    $variantText = if ($variants.Count -gt 0) { " + 变体 " + ($variants -join ",") } else { "" }
                    Write-Host "  $status $expression : 基础$(if (Test-Path $baseFile) { '?' } else { '?' })$variantText" -ForegroundColor $(if (Test-Path $baseFile) { "Green" } else { "Yellow" })
                }
            }
            Write-Host ""
        }
    }
}

# 统计总览
Write-Host "====================================" -ForegroundColor Cyan
Write-Host "统计总览" -ForegroundColor Cyan
Write-Host "====================================" -ForegroundColor Cyan
Write-Host ""

$totalPng = (Get-ChildItem $TextureDir -Recurse -File -Filter "*.png" -ErrorAction SilentlyContinue).Count
$totalFolders = (Get-ChildItem $TextureDir -Recurse -Directory -ErrorAction SilentlyContinue).Count
$totalCsFiles = (Get-ChildItem $sourceDir -Recurse -File -Filter "*.cs" -ErrorAction SilentlyContinue).Count

Write-Host "?? 部署统计（1.6 文件夹）：" -ForegroundColor Yellow
Write-Host "   部署路径: $TargetDir" -ForegroundColor Cyan
Write-Host "   PNG文件: $totalPng 个" -ForegroundColor Cyan
Write-Host "   材质文件夹: $totalFolders 个" -ForegroundColor Cyan
Write-Host "   C#源文件: $totalCsFiles 个" -ForegroundColor Cyan
Write-Host ""

Write-Host "? 验证完成" -ForegroundColor Green
Write-Host ""
Write-Host "按任意键退出..." -ForegroundColor Gray
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
