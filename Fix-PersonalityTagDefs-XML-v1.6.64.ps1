# ? PersonalityTagDefs XML 错误修复 v1.6.64

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  PersonalityTagDefs XML 错误修复 v1.6.64" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# === 问题说明 ===
Write-Host "? 问题：BehaviorInstruction XML格式错误" -ForegroundColor Red
Write-Host ""
Write-Host "错误格式：" -ForegroundColor Yellow
Write-Host '  <li priority="1">?? **YANDERE MODE:**</li>'
Write-Host ""
Write-Host "正确格式：" -ForegroundColor Green
Write-Host '  <li>'
Write-Host '    <priority>1</priority>'
Write-Host '    <text>?? **YANDERE MODE:**</text>'
Write-Host '  </li>'
Write-Host ""

# === 步骤1: 备份原文件 ===
Write-Host "[1/4] 备份原文件..." -ForegroundColor Yellow

$sourceFile = "Defs\PersonalityTagDefs.xml"
$backupFile = "Defs\PersonalityTagDefs.xml.bak.$(Get-Date -Format 'yyyyMMdd-HHmmss')"

if (Test-Path $sourceFile)
{
    Copy-Item $sourceFile $backupFile
    Write-Host "  ? 已备份到: $backupFile" -ForegroundColor Green
}
else
{
    Write-Host "  ? 源文件不存在: $sourceFile" -ForegroundColor Yellow
    exit 1
}

# === 步骤2: 读取并转换XML ===
Write-Host ""
Write-Host "[2/4] 读取并转换XML..." -ForegroundColor Yellow

[xml]$xmlDoc = Get-Content $sourceFile -Encoding UTF8

$fixedCount = 0
$totalInstructions = 0

foreach ($tagDef in $xmlDoc.Defs.ChildNodes)
{
    if ($tagDef.Name -ne "TheSecondSeat.PersonaGeneration.PersonalityTagDef")
    {
        continue
    }
    
    $behaviorNode = $tagDef.behaviorInstructions
    
    if ($null -eq $behaviorNode)
    {
        continue
    }
    
    Write-Host "  处理标签: $($tagDef.defName)" -ForegroundColor Cyan
    
    # 创建新的 behaviorInstructions 节点
    $newBehaviorNode = $xmlDoc.CreateElement("behaviorInstructions")
    
    foreach ($li in $behaviorNode.li)
    {
        $totalInstructions++
        
        # 获取属性和文本
        $priority = $li.priority
        $text = $li.'#text'
        
        if ([string]::IsNullOrWhiteSpace($text))
        {
            $text = $li.InnerText
        }
        
        # 创建新的 <li> 节点
        $newLi = $xmlDoc.CreateElement("li")
        
        # 创建 <priority> 子节点
        $priorityNode = $xmlDoc.CreateElement("priority")
        $priorityNode.InnerText = $priority
        $newLi.AppendChild($priorityNode) | Out-Null
        
        # 创建 <text> 子节点
        $textNode = $xmlDoc.CreateElement("text")
        $textNode.InnerText = $text
        $newLi.AppendChild($textNode) | Out-Null
        
        # 添加到新的 behaviorInstructions
        $newBehaviorNode.AppendChild($newLi) | Out-Null
        
        $fixedCount++
    }
    
    # 替换旧的 behaviorInstructions 节点
    $tagDef.ReplaceChild($newBehaviorNode, $behaviorNode) | Out-Null
    
    Write-Host "    ? 修复了 $($behaviorNode.li.Count) 条指令" -ForegroundColor Green
}

Write-Host ""
Write-Host "  ?? 统计:" -ForegroundColor Cyan
Write-Host "    总指令数: $totalInstructions" -ForegroundColor White
Write-Host "    已修复数: $fixedCount" -ForegroundColor Green

# === 步骤3: 保存修复后的XML ===
Write-Host ""
Write-Host "[3/4] 保存修复后的XML..." -ForegroundColor Yellow

# 使用 UTF-8 BOM 编码保存
$writerSettings = New-Object System.Xml.XmlWriterSettings
$writerSettings.Indent = $true
$writerSettings.IndentChars = "  "
$writerSettings.NewLineChars = "`n"
$writerSettings.Encoding = New-Object System.Text.UTF8Encoding($true)  # UTF-8 with BOM

$writer = [System.Xml.XmlWriter]::Create($sourceFile, $writerSettings)
$xmlDoc.Save($writer)
$writer.Close()

Write-Host "  ? 已保存到: $sourceFile" -ForegroundColor Green

# === 步骤4: 部署到游戏目录 ===
Write-Host ""
Write-Host "[4/4] 部署到游戏目录..." -ForegroundColor Yellow

$gamePath = "D:\steam\steamapps\common\RimWorld\Mods\TheSecondSeat"

if (Test-Path $gamePath)
{
    $targetFile = "$gamePath\Defs\PersonalityTagDefs.xml"
    
    Copy-Item $sourceFile $targetFile -Force
    
    Write-Host "  ? 已部署到: $targetFile" -ForegroundColor Green
}
else
{
    Write-Host "  ? 游戏目录不存在，跳过部署" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  ? 修复完成！" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

Write-Host "?? 修复内容:" -ForegroundColor Cyan
Write-Host "  1. ? 将 $fixedCount 条 BehaviorInstruction 转换为正确格式" -ForegroundColor White
Write-Host "  2. ? 使用 UTF-8 BOM 编码保存" -ForegroundColor White
Write-Host "  3. ? 部署到游戏目录" -ForegroundColor White
Write-Host ""

Write-Host "?? 下一步:" -ForegroundColor Cyan
Write-Host "  1. 启动 RimWorld" -ForegroundColor White
Write-Host "  2. 检查日志，确认没有 'doesn't correspond to any field' 错误" -ForegroundColor White
Write-Host ""

Write-Host "?? 备份文件: $backupFile" -ForegroundColor Cyan
Write-Host ""

Write-Host "? 完成！" -ForegroundColor Green
