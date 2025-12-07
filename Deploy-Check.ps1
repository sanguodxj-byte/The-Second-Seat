# 部署检查脚本
param(
    [string]$ModPath = ".",
    [switch]$Verbose,
    [switch]$Deploy
)

Write-Host "`n" + "="*60 -ForegroundColor Cyan
Write-Host "  The Second Seat - 部署检查工具" -ForegroundColor Yellow
Write-Host "="*60 + "`n" -ForegroundColor Cyan

# 1. 检查必要文件
Write-Host "?? 检查文件完整性..." -ForegroundColor Cyan

$required = @{
    "About\About.xml" = "模组元数据"
    "Assemblies\TheSecondSeat.dll" = "核心 DLL"
    "LoadFolders.xml" = "加载配置"
    "Defs\GameComponentDefs.xml" = "游戏组件定义"
    "Defs\NarratorPersonaDefs.xml" = "人格定义"
    "Languages\ChineseSimplified\Keyed\TheSecondSeat_Keys.xml" = "中文翻译"
    "Languages\English\Keyed\TheSecondSeat_Keys.xml" = "英文翻译"
}

$missing = @()
$found = 0

foreach ($file in $required.Keys) {
    $path = Join-Path $ModPath $file
    $desc = $required[$file]
    
    if (Test-Path $path) {
        Write-Host "  ? $desc" -ForegroundColor Green -NoNewline
        Write-Host " ($file)" -ForegroundColor Gray
        $found++
    } else {
        Write-Host "  ? $desc" -ForegroundColor Red -NoNewline
        Write-Host " ($file) 缺失!" -ForegroundColor Red
        $missing += $file
    }
}

Write-Host "`n  总计: $found/$($required.Count) 个文件" -ForegroundColor Yellow

# 2. 检查 DLL 详细信息
Write-Host "`n?? 检查 DLL 信息..." -ForegroundColor Cyan

$dllPath = Join-Path $ModPath "Assemblies\TheSecondSeat.dll"
if (Test-Path $dllPath) {
    try {
        $assembly = [System.Reflection.Assembly]::LoadFile((Resolve-Path $dllPath).Path)
        $version = $assembly.GetName().Version
        $runtime = $assembly.ImageRuntimeVersion
        $fileSize = (Get-Item $dllPath).Length / 1KB
        
        Write-Host "  ?? 版本号: $version" -ForegroundColor Green
        Write-Host "  ?? .NET 运行时: $runtime" -ForegroundColor Green
        Write-Host "  ?? 文件大小: $([math]::Round($fileSize, 2)) KB" -ForegroundColor Green
        
        # 检查依赖
        $references = $assembly.GetReferencedAssemblies()
        Write-Host "`n  ?? 依赖项:" -ForegroundColor Yellow
        foreach ($ref in $references) {
            Write-Host "    - $($ref.Name) ($($ref.Version))" -ForegroundColor Gray
        }
    } catch {
        Write-Host "  ?? 无法加载 DLL: $($_.Exception.Message)" -ForegroundColor Red
    }
} else {
    Write-Host "  ? DLL 文件不存在！" -ForegroundColor Red
}

# 3. 检查语言文件
Write-Host "`n?? 检查语言文件..." -ForegroundColor Cyan

$languages = @{
    "ChineseSimplified" = "简体中文"
    "English" = "英语"
}

foreach ($lang in $languages.Keys) {
    $langName = $languages[$lang]
    $langPath = Join-Path $ModPath "Languages\$lang\Keyed"
    
    if (Test-Path $langPath) {
        $xmlFiles = Get-ChildItem $langPath -Filter *.xml
        $keyCount = 0
        
        foreach ($xmlFile in $xmlFiles) {
            $xml = [xml](Get-Content $xmlFile.FullName)
            $keyCount += $xml.LanguageData.ChildNodes.Count
        }
        
        Write-Host "  ? $langName : $keyCount 个翻译键" -ForegroundColor Green
    } else {
        Write-Host "  ? $langName : 目录不存在" -ForegroundColor Red
    }
}

# 4. 检查 About.xml 内容
Write-Host "`n?? 检查模组元数据..." -ForegroundColor Cyan

$aboutPath = Join-Path $ModPath "About\About.xml"
if (Test-Path $aboutPath) {
    $aboutXml = [xml](Get-Content $aboutPath)
    $modMeta = $aboutXml.ModMetaData
    
    Write-Host "  ?? 名称: $($modMeta.name)" -ForegroundColor Green
    Write-Host "  ?? Package ID: $($modMeta.packageId)" -ForegroundColor Green
    Write-Host "  ?? 支持版本: $($modMeta.supportedVersions.li -join ', ')" -ForegroundColor Green
    Write-Host "  ?? 作者: $($modMeta.author)" -ForegroundColor Green
    
    if ($modMeta.modVersion) {
        Write-Host "  ?? 模组版本: $($modMeta.modVersion)" -ForegroundColor Green
    } else {
        Write-Host "  ?? 未设置模组版本号" -ForegroundColor Yellow
    }
}

# 5. 检查可选文件
Write-Host "`n?? 检查可选内容..." -ForegroundColor Cyan

$optional = @{
    "About\Preview.png" = "Workshop 预览图"
    "Textures\UI\Buttons\AIButton.png" = "AI 按钮图标"
    "Textures\Narrators\Cassandra.png" = "Cassandra 立绘"
}

foreach ($file in $optional.Keys) {
    $path = Join-Path $ModPath $file
    $desc = $optional[$file]
    
    if (Test-Path $path) {
        $size = (Get-Item $path).Length / 1KB
        Write-Host "  ? $desc ($([math]::Round($size, 2)) KB)" -ForegroundColor Green
    } else {
        Write-Host "  ?? $desc (建议添加)" -ForegroundColor Yellow
    }
}

# 6. 检查文档
Write-Host "`n?? 检查文档完整性..." -ForegroundColor Cyan

$docs = @(
    "README.md",
    "快速入门.md",
    "完整使用手册.md",
    "ARCHITECTURE.md",
    "DEVELOPMENT.md"
)

$docCount = 0
foreach ($doc in $docs) {
    $docPath = Join-Path $ModPath $doc
    if (Test-Path $docPath) {
        $docCount++
        Write-Host "  ? $doc" -ForegroundColor Green
    } else {
        Write-Host "  ?? $doc (缺失)" -ForegroundColor Yellow
    }
}

Write-Host "`n  总计: $docCount/$($docs.Count) 个文档" -ForegroundColor Yellow

# 7. 检查编译配置
Write-Host "`n?? 检查编译配置..." -ForegroundColor Cyan

$csprojPath = Join-Path $ModPath "Source\TheSecondSeat\TheSecondSeat.csproj"
if (Test-Path $csprojPath) {
    $csproj = [xml](Get-Content $csprojPath)
    $targetFramework = $csproj.Project.PropertyGroup.TargetFramework
    
    Write-Host "  ?? 目标框架: $targetFramework" -ForegroundColor Green
    
    # 检查依赖包
    $packages = $csproj.Project.ItemGroup.PackageReference
    if ($packages) {
        Write-Host "`n  ?? NuGet 包:" -ForegroundColor Yellow
        foreach ($pkg in $packages) {
            Write-Host "    - $($pkg.Include) v$($pkg.Version)" -ForegroundColor Gray
        }
    }
}

# 8. 生成报告
Write-Host "`n" + "="*60 -ForegroundColor Cyan
Write-Host "  部署检查报告" -ForegroundColor Yellow
Write-Host "="*60 -ForegroundColor Cyan

$totalScore = 0
$maxScore = 0

# 文件完整性 (40分)
$maxScore += 40
if ($missing.Count -eq 0) {
    Write-Host "`n? 文件完整性: 40/40" -ForegroundColor Green
    $totalScore += 40
} else {
    $score = [math]::Max(0, 40 - $missing.Count * 5)
    Write-Host "`n?? 文件完整性: $score/40 (缺失 $($missing.Count) 个文件)" -ForegroundColor Yellow
    $totalScore += $score
}

# DLL 有效性 (30分)
$maxScore += 30
if (Test-Path $dllPath) {
    Write-Host "? DLL 有效性: 30/30" -ForegroundColor Green
    $totalScore += 30
} else {
    Write-Host "? DLL 有效性: 0/30" -ForegroundColor Red
}

# 语言支持 (15分)
$maxScore += 15
if ((Test-Path (Join-Path $ModPath "Languages\ChineseSimplified")) -and 
    (Test-Path (Join-Path $ModPath "Languages\English"))) {
    Write-Host "? 语言支持: 15/15" -ForegroundColor Green
    $totalScore += 15
} else {
    Write-Host "?? 语言支持: 7/15 (部分语言缺失)" -ForegroundColor Yellow
    $totalScore += 7
}

# 文档完整性 (15分)
$maxScore += 15
$docScore = [math]::Round(($docCount / $docs.Count) * 15)
if ($docScore -eq 15) {
    Write-Host "? 文档完整性: $docScore/15" -ForegroundColor Green
} else {
    Write-Host "?? 文档完整性: $docScore/15" -ForegroundColor Yellow
}
$totalScore += $docScore

# 最终评分
$percentage = [math]::Round(($totalScore / $maxScore) * 100)

Write-Host "`n" + "="*60 -ForegroundColor Cyan
Write-Host "  最终评分: $totalScore / $maxScore ($percentage%)" -ForegroundColor $(
    if ($percentage -ge 90) { "Green" }
    elseif ($percentage -ge 70) { "Yellow" }
    else { "Red" }
)
Write-Host "="*60 -ForegroundColor Cyan

# 9. 给出建议
Write-Host "`n?? 建议:" -ForegroundColor Cyan

if ($missing.Count -gt 0) {
    Write-Host "  ?? 补全缺失的文件:" -ForegroundColor Yellow
    foreach ($file in $missing) {
        Write-Host "    - $file" -ForegroundColor Red
    }
}

if (-not (Test-Path (Join-Path $ModPath "About\Preview.png"))) {
    Write-Host "  ?? 创建 Preview.png (640x360) 用于 Workshop" -ForegroundColor Yellow
}

if (-not (Test-Path (Join-Path $ModPath "Textures"))) {
    Write-Host "  ?? 添加 Textures 目录提升视觉效果" -ForegroundColor Yellow
}

if ($percentage -ge 90) {
    Write-Host "`n?? 恭喜！项目已准备好发布！" -ForegroundColor Green
    Write-Host "   可以执行以下操作:" -ForegroundColor Cyan
    Write-Host "   1. 上传到 Steam Workshop" -ForegroundColor Gray
    Write-Host "   2. 创建 GitHub Release" -ForegroundColor Gray
    Write-Host "   3. 分享到社区" -ForegroundColor Gray
} elseif ($percentage -ge 70) {
    Write-Host "`n?? 项目基本完成，但建议完善后再发布" -ForegroundColor Yellow
} else {
    Write-Host "`n? 项目尚未完成，需要进一步开发" -ForegroundColor Red
}

# 10. 自动部署选项
if ($Deploy -and $percentage -ge 90) {
    Write-Host "`n?? 开始自动部署..." -ForegroundColor Cyan
    
    $rimworldPath = "D:\steam\steamapps\common\RimWorld\Mods"
    $targetPath = Join-Path $rimworldPath "TheSecondSeat"
    
    if (Test-Path $targetPath) {
        Write-Host "  ??? 清理旧版本..." -ForegroundColor Yellow
        Remove-Item $targetPath -Recurse -Force
    }
    
    Write-Host "  ?? 复制文件..." -ForegroundColor Yellow
    Copy-Item $ModPath -Destination $targetPath -Recurse -Force
    
    Write-Host "`n  ? 部署完成！" -ForegroundColor Green
    Write-Host "  ?? 目标路径: $targetPath" -ForegroundColor Cyan
    Write-Host "  ?? 请重启 RimWorld 测试" -ForegroundColor Yellow
}

Write-Host "`n"
