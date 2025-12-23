# 性格标签库系统 - 自动部署脚本 v1.6.64

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  性格标签库系统 - 自动部署 v1.6.64" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# 检查文件
Write-Host "[1/4] 检查文件..." -ForegroundColor Yellow

$files = @(
    "Defs\PersonalityTagDefs.xml",
    "Source\TheSecondSeat\PersonaGeneration\PersonalityTagDef.cs",
    "SystemPromptGenerator-重构补丁-v1.6.64.cs",
    "Languages\ChineseSimplified\Keyed\PersonalityTags_Keys.xml",
    "Languages\English\Keyed\PersonalityTags_Keys.xml"
)

foreach ($file in $files) {
    if (Test-Path $file) {
        Write-Host "  ? $file" -ForegroundColor Green
    } else {
        Write-Host "  ? $file 不存在" -ForegroundColor Red
        exit 1
    }
}

Write-Host ""
Write-Host "[2/4] 应用 SystemPromptGenerator 重构补丁..." -ForegroundColor Yellow

# 读取原始文件
$targetFile = "Source\TheSecondSeat\PersonaGeneration\SystemPromptGenerator.cs"
$content = Get-Content $targetFile -Raw -Encoding UTF8

# 查找 GenerateRomanticInstructions 方法
$methodPattern = '(?s)(private static string GenerateRomanticInstructions\(NarratorPersonaDef persona, float affinity\)\s*\{.*?return sb\.ToString\(\);\s*\})'

if ($content -match $methodPattern) {
    # 读取新方法
    $newMethod = Get-Content "SystemPromptGenerator-重构补丁-v1.6.64.cs" -Raw -Encoding UTF8
    
    # 提取实际方法体
    if ($newMethod -match '(?s)(private static string GenerateRomanticInstructions.*?return sb\.ToString\(\);\s*\})') {
        $replacementMethod = $Matches[1]
        
        # 替换方法
        $content = $content -replace [regex]::Escape($Matches[1]), $replacementMethod
        
        # 写回文件
        [System.IO.File]::WriteAllText($targetFile, $content, [System.Text.Encoding]::UTF8)
        Write-Host "  ? GenerateRomanticInstructions 方法已替换" -ForegroundColor Green
    } else {
        Write-Host "  ? 无法提取新方法" -ForegroundColor Red
        exit 1
    }
} else {
    Write-Host "  ? 找不到 GenerateRomanticInstructions 方法" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "[3/4] 为 Sideria 添加性格标签..." -ForegroundColor Yellow

$sideriaFile = "Sideria\Defs\NarratorPersonaDefs_Sideria.xml"

if (Test-Path $sideriaFile) {
    $sideriaContent = Get-Content $sideriaFile -Raw -Encoding UTF8
    
    if ($sideriaContent -match "Kuudere") {
        Write-Host "  ? Sideria 已包含性格标签" -ForegroundColor Gray
    } else {
        # 查找 personalityTags 位置
        if ($sideriaContent -match '(?s)(<personalityTags>.*?</personalityTags>)') {
            $oldTags = $Matches[1]
            
            # 添加标签
            $newTags = @"
  <personalityTags>
    <li>Kuudere</li>
    <li>Arrogant</li>
    <li>Mysterious</li>
  </personalityTags>
"@
            
            $sideriaContent = $sideriaContent -replace [regex]::Escape($oldTags), $newTags
            
            [System.IO.File]::WriteAllText($sideriaFile, $sideriaContent, [System.Text.Encoding]::UTF8)
            Write-Host "  ? Sideria 性格标签已更新" -ForegroundColor Green
        } else {
            # 如果没有 personalityTags，在 </NarratorPersonaDef> 前添加
            $insertPoint = "</NarratorPersonaDef>"
            $tagsBlock = @"
  
  <personalityTags>
    <li>Kuudere</li>
    <li>Arrogant</li>
    <li>Mysterious</li>
  </personalityTags>
  
</NarratorPersonaDef>
"@
            
            $sideriaContent = $sideriaContent -replace [regex]::Escape($insertPoint), $tagsBlock
            
            [System.IO.File]::WriteAllText($sideriaFile, $sideriaContent, [System.Text.Encoding]::UTF8)
            Write-Host "  ? Sideria 性格标签已添加" -ForegroundColor Green
        }
    }
} else {
    Write-Host "  ? Sideria 配置不存在，跳过" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "[4/4] 编译并部署..." -ForegroundColor Yellow

if (Test-Path ".\编译并部署到游戏.ps1") {
    & ".\编译并部署到游戏.ps1"
} else {
    Write-Host "  ? 未找到编译脚本，请手动编译" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  ? 性格标签库系统部署完成！" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

Write-Host "?? 测试步骤：" -ForegroundColor Cyan
Write-Host "  1. 启动 RimWorld" -ForegroundColor White
Write-Host "  2. 开启 Dev 模式" -ForegroundColor White
Write-Host "  3. 输入以下命令检查 Def 加载：" -ForegroundColor White
Write-Host "     DefDatabase<PersonalityTagDef>.AllDefs" -ForegroundColor Gray
Write-Host ""
Write-Host "  4. 预期结果：" -ForegroundColor White
Write-Host "     - Yandere" -ForegroundColor Green
Write-Host "     - Kuudere" -ForegroundColor Green
Write-Host "     - Tsundere" -ForegroundColor Green
Write-Host "     - Gentle" -ForegroundColor Green
Write-Host "     - Arrogant" -ForegroundColor Green
Write-Host "     - Energetic" -ForegroundColor Green
Write-Host "     - Mysterious" -ForegroundColor Green
Write-Host ""
Write-Host "  5. 测试标签激活：" -ForegroundColor White
Write-Host "     - 将好感度调到 90+" -ForegroundColor Gray
Write-Host "     - 与 Sideria 对话" -ForegroundColor Gray
Write-Host "     - 观察是否出现 Kuudere 行为" -ForegroundColor Gray
Write-Host ""

Write-Host "? 完成！" -ForegroundColor Green
