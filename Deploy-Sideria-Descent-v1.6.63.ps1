# Sideria 降临系统自动部署脚本 v1.6.63
# 功能：自动创建必要的 Def 文件和目录结构

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Sideria 降临系统自动部署 v1.6.63" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# 定义路径
$sideriaDefsPath = "Sideria\Defs"
$texturesPath = "Textures\UI\Narrators\Descent"

Write-Host "[1/5] 创建目录结构..." -ForegroundColor Yellow

# 创建 Defs 目录
if (-not (Test-Path $sideriaDefsPath)) {
    New-Item -ItemType Directory -Path $sideriaDefsPath -Force | Out-Null
    Write-Host "  ? 创建 Sideria\Defs\" -ForegroundColor Green
} else {
    Write-Host "  ? Sideria\Defs\ 已存在" -ForegroundColor Gray
}

# 创建纹理目录
$posturesPath = Join-Path $texturesPath "Postures"
$effectsPath = Join-Path $texturesPath "Effects"

foreach ($path in @($posturesPath, $effectsPath)) {
    if (-not (Test-Path $path)) {
        New-Item -ItemType Directory -Path $path -Force | Out-Null
        Write-Host "  ? 创建 $path" -ForegroundColor Green
    } else {
        Write-Host "  ? $path 已存在" -ForegroundColor Gray
    }
}

Write-Host ""
Write-Host "[2/5] 创建 PawnKindDef 文件..." -ForegroundColor Yellow

$pawnKindDefContent = @'
<?xml version="1.0" encoding="utf-8"?>
<Defs>
  <!-- Sideria 降临实体 -->
  <PawnKindDef>
    <defName>TSS_Sideria_Avatar</defName>
    <label>Sideria</label>
    <race>Human</race>
    <defaultFactionType>PlayerColony</defaultFactionType>
    
    <backstoryCategories>
      <li>Outlander</li>
    </backstoryCategories>
    
    <baseRecruitDifficulty>0.0</baseRecruitDifficulty>
    <combatEnhancingDrugsChance>0</combatEnhancingDrugsChance>
    
    <!-- 外观配置 -->
    <apparelTags>
      <li>IndustrialBasic</li>
      <li>IndustrialAdvanced</li>
    </apparelTags>
    
    <apparelMoney>
      <min>1000</min>
      <max>2000</max>
    </apparelMoney>
    
    <apparelAllowHeadgearChance>0.5</apparelAllowHeadgearChance>
    
    <!-- 武器配置 -->
    <weaponMoney>
      <min>500</min>
      <max>1500</max>
    </weaponMoney>
    
    <weaponTags>
      <li>Gun</li>
    </weaponTags>
    
    <!-- 能力配置 -->
    <initialWillRange>
      <min>3</min>
      <max>5</max>
    </initialWillRange>
    
    <initialResistanceRange>
      <min>15</min>
      <max>25</max>
    </initialResistanceRange>
  </PawnKindDef>

  <!-- Sideria 伴随巨龙 -->
  <PawnKindDef>
    <defName>TSS_TrueDragon</defName>
    <label>真龙</label>
    <race>Megaspider</race> <!-- 临时使用，后续可替换 -->
    <defaultFactionType>PlayerColony</defaultFactionType>
    
    <combatPower>500</combatPower>
    <canArriveManhunter>false</canArriveManhunter>
    <ecoSystemWeight>1.0</ecoSystemWeight>
    
    <lifeStages>
      <li>
        <bodyGraphicData>
          <texPath>Things/Pawn/Animal/Megaspider</texPath>
          <drawSize>3.5</drawSize>
        </bodyGraphicData>
        <dessicatedBodyGraphicData>
          <texPath>Things/Pawn/Animal/Megaspider_Dessicated</texPath>
          <drawSize>3.5</drawSize>
        </dessicatedBodyGraphicData>
      </li>
    </lifeStages>
  </PawnKindDef>
</Defs>
'@

$pawnKindDefPath = Join-Path $sideriaDefsPath "PawnKindDefs_Sideria.xml"
[System.IO.File]::WriteAllText($pawnKindDefPath, $pawnKindDefContent, [System.Text.Encoding]::UTF8)
Write-Host "  ? 创建 PawnKindDefs_Sideria.xml" -ForegroundColor Green

Write-Host ""
Write-Host "[3/5] 创建 ThingDef 文件..." -ForegroundColor Yellow

$thingDefContent = @'
<?xml version="1.0" encoding="utf-8"?>
<Defs>
  <!-- Sideria 龙形降临特效 -->
  <ThingDef ParentName="DropPodIncoming">
    <defName>TSS_Sideria_DragonDescent</defName>
    <label>龙形降临</label>
    <description>Sideria 的降临特效，伴随着巨龙的咆哮。</description>
    
    <graphicData>
      <texPath>Things/Special/DropPodIncoming</texPath>
      <graphicClass>Graphic_Single</graphicClass>
      <drawSize>(2,2)</drawSize>
    </graphicData>
    
    <soundImpactDefault>Explosion_GiantBomb</soundImpactDefault>
  </ThingDef>
</Defs>
'@

$thingDefPath = Join-Path $sideriaDefsPath "ThingDefs_Sideria_Descent.xml"
[System.IO.File]::WriteAllText($thingDefPath, $thingDefContent, [System.Text.Encoding]::UTF8)
Write-Host "  ? 创建 ThingDefs_Sideria_Descent.xml" -ForegroundColor Green

Write-Host ""
Write-Host "[4/5] 创建纹理占位符 README..." -ForegroundColor Yellow

# Postures README
$posturesReadme = @'
# 降临姿态纹理

## 文件名
body_arrival.png

## 规格
- 尺寸: 1024x1572
- 格式: PNG（透明背景）
- 内容: Sideria 降临时的特殊姿态

## 设计建议
- 双臂张开，如同拥抱天地
- 身体微微后仰，展现自信
- 衣袍飘扬，营造动感
- 眼神犀利，俯视众生

## 当前状态
?? 占位符 - 请替换为实际纹理文件
'@

$posturesReadmePath = Join-Path $posturesPath "README.md"
[System.IO.File]::WriteAllText($posturesReadmePath, $posturesReadme, [System.Text.Encoding]::UTF8)
Write-Host "  ? 创建 Postures\README.md" -ForegroundColor Green

# Effects README
$effectsReadme = @'
# 降临特效纹理

## 文件名
glitch_circle.png

## 规格
- 尺寸: 512x512 或 1024x1024
- 格式: PNG（透明背景）
- 内容: 环形特效，带有故障艺术风格

## 设计建议
- 圆形或六边形魔法阵
- 带有故障效果的线条
- 半透明，叠加时更明显
- 颜色：紫色/蓝色/白色混合

## 当前状态
?? 占位符 - 请替换为实际纹理文件
'@

$effectsReadmePath = Join-Path $effectsPath "README.md"
[System.IO.File]::WriteAllText($effectsReadmePath, $effectsReadme, [System.Text.Encoding]::UTF8)
Write-Host "  ? 创建 Effects\README.md" -ForegroundColor Green

Write-Host ""
Write-Host "[5/5] 验证部署..." -ForegroundColor Yellow

$requiredFiles = @(
    "Sideria\Defs\PawnKindDefs_Sideria.xml",
    "Sideria\Defs\ThingDefs_Sideria_Descent.xml",
    "Textures\UI\Narrators\Descent\Postures\README.md",
    "Textures\UI\Narrators\Descent\Effects\README.md"
)

$allExists = $true
foreach ($file in $requiredFiles) {
    if (Test-Path $file) {
        Write-Host "  ? $file" -ForegroundColor Green
    } else {
        Write-Host "  ? $file 不存在" -ForegroundColor Red
        $allExists = $false
    }
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
if ($allExists) {
    Write-Host "  ? 部署成功！" -ForegroundColor Green
} else {
    Write-Host "  ?? 部署不完整，请检查错误" -ForegroundColor Yellow
}
Write-Host "========================================" -ForegroundColor Cyan

Write-Host ""
Write-Host "?? 下一步操作：" -ForegroundColor Cyan
Write-Host "  1. 准备纹理资源：" -ForegroundColor White
Write-Host "     - body_arrival.png (1024x1572)" -ForegroundColor Gray
Write-Host "     - glitch_circle.png (512x512)" -ForegroundColor Gray
Write-Host ""
Write-Host "  2. 放置纹理文件到：" -ForegroundColor White
Write-Host "     - Textures\UI\Narrators\Descent\Postures\" -ForegroundColor Gray
Write-Host "     - Textures\UI\Narrators\Descent\Effects\" -ForegroundColor Gray
Write-Host ""
Write-Host "  3. 编译并部署：" -ForegroundColor White
Write-Host "     .\编译并部署到游戏.ps1" -ForegroundColor Gray
Write-Host ""
Write-Host "  4. 游戏内测试：" -ForegroundColor White
Write-Host "     NarratorDescentSystem.Instance.TriggerDescent(isHostile: false);" -ForegroundColor Gray
Write-Host ""

Write-Host "? 完成！" -ForegroundColor Green
