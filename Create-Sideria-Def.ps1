# 安全创建 Sideria 人格定义

$xmlContent = @'
<?xml version="1.0" encoding="utf-8"?>
<Defs>
  <!-- Sideria 人格定义 -->
  <TheSecondSeat.PersonaGeneration.NarratorPersonaDef>
    <defName>Sideria_Default</defName>
    <label>Sideria</label>
    <narratorName>Sideria</narratorName>
    
    <!-- 立绘路径 - 使用 9x16 目录 -->
    <portraitPath>UI/Narrators/9x16/Sideria/base</portraitPath>
    
    <!-- 主色调和强调色 -->
    <primaryColor>(0.60, 0.70, 0.90, 1.00)</primaryColor>
    <accentColor>(0.40, 0.60, 0.80, 1.00)</accentColor>
    
    <!-- 简介 -->
    <biography>
Sideria 是一位神秘而强大的AI叙事者。她以冷静和理智著称，但在深入了解玩家后，会展现出温暖和关怀的一面。

她擅长战略规划，能够预见危机并提供明智的建议。她的存在如同星空般深邃，既遥远又亲近。
    </biography>
    
    <!-- 人格特质 -->
    <overridePersonality>Strategic</overridePersonality>
    
    <!-- 对话风格 -->
    <dialogueStyle>
      <formalityLevel>0.50</formalityLevel>
      <emotionalExpression>0.60</emotionalExpression>
      <humorLevel>0.30</humorLevel>
      <sarcasmLevel>0.20</sarcasmLevel>
      <verbosity>0.50</verbosity>
    </dialogueStyle>
    
    <!-- 语气标签 -->
    <toneTags>
      <li>calm</li>
      <li>strategic</li>
      <li>protective</li>
      <li>mysterious</li>
    </toneTags>
  </TheSecondSeat.PersonaGeneration.NarratorPersonaDef>
</Defs>
'@

# 保存文件（UTF-8 无 BOM）
$targetPath = "Defs\NarratorPersonaDefs\Sideria.xml"
$targetDir = Split-Path $targetPath -Parent

# 创建目录
if (-not (Test-Path $targetDir)) {
    New-Item -Path $targetDir -ItemType Directory -Force | Out-Null
    Write-Host "创建目录: $targetDir" -ForegroundColor Gray
}

# 保存文件
$utf8NoBom = New-Object System.Text.UTF8Encoding $false
[System.IO.File]::WriteAllText($targetPath, $xmlContent, $utf8NoBom)

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host " Sideria 人格定义创建完成" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "文件位置: $targetPath" -ForegroundColor White
Write-Host "编码: UTF-8 (无 BOM)" -ForegroundColor Gray
Write-Host ""
Write-Host "下一步:" -ForegroundColor Yellow
Write-Host "  1. 部署到游戏 Mod 目录" -ForegroundColor White
Write-Host "  2. 重启游戏测试" -ForegroundColor White
Write-Host ""
