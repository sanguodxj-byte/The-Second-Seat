# ? 修复测试事件异常触发 - v1.6.64

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  修复测试事件异常触发 - v1.6.64" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# === 步骤1: 创建NeverTrigger触发器 ===
Write-Host "[1/4] 创建NeverTrigger触发器..." -ForegroundColor Yellow

$neverTriggerCode = @'
using System.Collections.Generic;
using Verse;

namespace TheSecondSeat.Framework.Triggers
{
    /// <summary>
    /// ? 永不触发触发器 - 用于测试事件
    /// 
    /// 此触发器的Check()永远返回false，
    /// 确保事件只能通过ForceTriggerEvent()手动触发
    /// 
    /// XML使用示例：
    /// <code>
    /// <triggers>
    ///   <li Class="TheSecondSeat.Framework.Triggers.NeverTrigger" />
    /// </triggers>
    /// </code>
    /// </summary>
    public class NeverTrigger : TSSTrigger
    {
        public override bool Check(Map map, Dictionary<string, object> context)
        {
            return false;  // 永远返回false，阻止自动触发
        }
        
        public override bool Validate(out string error)
        {
            error = "";
            return true;
        }
    }
}
'@

$neverTriggerPath = "Source\TheSecondSeat\Framework\Triggers\NeverTrigger.cs"
Set-Content -Path $neverTriggerPath -Value $neverTriggerCode -Encoding UTF8
Write-Host "  ? NeverTrigger.cs 创建完成" -ForegroundColor Green

# === 步骤2: 修改NarratorEventManager ===
Write-Host ""
Write-Host "[2/4] 增强NarratorEventManager安全检查..." -ForegroundColor Yellow

$managerPath = "Source\TheSecondSeat\Framework\NarratorEventManager.cs"
$managerContent = Get-Content -Path $managerPath -Raw -Encoding UTF8

# 查找CheckAllEvents方法并增强
$checkAllEventsPattern = '(?s)(private void CheckAllEvents\(\).*?\{.*?foreach \(var eventDef in allEvents\).*?\{.*?try.*?\{)'
$checkAllEventsReplacement = @'
$1
                // ? 跳过高优先级事件（已在高优先级检查中处理）
                if (eventDef.priority >= 100)
                {
                    continue;
                }
                
                // ? 新增：跳过测试/调试事件
                if (eventDef.category == "Test" || 
                    eventDef.category == "Debug")
                {
                    continue;
                }
                
                // ? 新增：跳过没有triggers的事件（防御性编程）
                if (eventDef.triggers == null || eventDef.triggers.Count == 0)
                {
                    if (Prefs.DevMode)
                    {
                        Log.Warning($"[NarratorEventManager] Event '{eventDef.defName}' has no triggers, skipping auto-check");
                    }
                    continue;
                }
                
'@

if ($managerContent -match $checkAllEventsPattern)
{
    $managerContent = $managerContent -replace $checkAllEventsPattern, $checkAllEventsReplacement
    Set-Content -Path $managerPath -Value $managerContent -Encoding UTF8 -NoNewline
    Write-Host "  ? CheckAllEvents() 增强完成" -ForegroundColor Green
}
else
{
    Write-Host "  ? CheckAllEvents() 模式未找到，跳过修改" -ForegroundColor Yellow
}

# === 步骤3: 更新测试事件XML ===
Write-Host ""
Write-Host "[3/4] 更新测试事件XML定义..." -ForegroundColor Yellow

$customEventsPath = "Defs\TSS_Custom_Events.xml"
if (Test-Path $customEventsPath)
{
    [xml]$eventsXml = Get-Content -Path $customEventsPath -Encoding UTF8
    
    $modified = $false
    
    foreach ($eventDef in $eventsXml.Defs.ChildNodes)
    {
        if ($eventDef.Name -eq "TheSecondSeat.Framework.NarratorEventDef")
        {
            $defName = $eventDef.defName
            
            # 检查是否是测试事件
            if ($defName -match "^Test" -or $defName -match "Debug")
            {
                # 检查是否已有triggers节点
                if (-not $eventDef.triggers)
                {
                    # 创建triggers节点
                    $triggersNode = $eventsXml.CreateElement("triggers")
                    
                    # 创建NeverTrigger
                    $neverTriggerNode = $eventsXml.CreateElement("li")
                    $neverTriggerNode.SetAttribute("Class", "TheSecondSeat.Framework.Triggers.NeverTrigger")
                    
                    $triggersNode.AppendChild($neverTriggerNode) | Out-Null
                    $eventDef.AppendChild($triggersNode) | Out-Null
                    
                    Write-Host "  ? 为事件 $defName 添加NeverTrigger" -ForegroundColor Green
                    $modified = $true
                }
            }
        }
    }
    
    if ($modified)
    {
        # 使用XmlWriterSettings保持格式
        $writerSettings = New-Object System.Xml.XmlWriterSettings
        $writerSettings.Indent = $true
        $writerSettings.IndentChars = "  "
        $writerSettings.NewLineChars = "`n"
        $writerSettings.Encoding = [System.Text.Encoding]::UTF8
        
        $writer = [System.Xml.XmlWriter]::Create($customEventsPath, $writerSettings)
        $eventsXml.Save($writer)
        $writer.Close()
        
        Write-Host "  ? 测试事件XML已更新" -ForegroundColor Green
    }
    else
    {
        Write-Host "  ? 所有测试事件已配置NeverTrigger" -ForegroundColor Cyan
    }
}
else
{
    Write-Host "  ? TSS_Custom_Events.xml 未找到" -ForegroundColor Yellow
}

# === 步骤4: 编译并部署 ===
Write-Host ""
Write-Host "[4/4] 编译并部署..." -ForegroundColor Yellow

if (Test-Path ".\编译并部署到游戏.ps1")
{
    & ".\编译并部署到游戏.ps1"
}
else
{
    Write-Host "  ? 未找到编译脚本，请手动编译" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  ? 修复完成！" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

Write-Host "?? 修复内容:" -ForegroundColor Cyan
Write-Host "  1. ? 创建 NeverTrigger 触发器" -ForegroundColor White
Write-Host "  2. ? NarratorEventManager 跳过测试事件" -ForegroundColor White
Write-Host "  3. ? 所有测试事件添加 NeverTrigger" -ForegroundColor White
Write-Host ""

Write-Host "?? 游戏内验证:" -ForegroundColor Cyan
Write-Host "  1. 启动游戏" -ForegroundColor White
Write-Host "  2. 观察测试事件是否还会自动触发" -ForegroundColor White
Write-Host "  3. 使用 DebugAction 手动触发测试事件" -ForegroundColor White
Write-Host "  4. 确认手动触发仍然正常工作" -ForegroundColor White
Write-Host ""

Write-Host "? 完成！" -ForegroundColor Green
