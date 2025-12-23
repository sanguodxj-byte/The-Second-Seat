# Kuudere 人格标签补丁 - 应用脚本 v1.6.64

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Kuudere 冰美人人格标签补丁 v1.6.64" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# 定义目标文件
$targetFile = "Source\TheSecondSeat\PersonaGeneration\SystemPromptGenerator.cs"
$sideriaFile = "Sideria\Defs\NarratorPersonaDefs_Sideria.xml"

Write-Host "[1/3] 检查文件..." -ForegroundColor Yellow

if (-not (Test-Path $targetFile)) {
    Write-Host "  ? 文件不存在: $targetFile" -ForegroundColor Red
    exit 1
}
Write-Host "  ? SystemPromptGenerator.cs 存在" -ForegroundColor Green

if (-not (Test-Path $sideriaFile)) {
    Write-Host "  ? Sideria 配置不存在，跳过" -ForegroundColor Yellow
    $sideriaExists = $false
} else {
    Write-Host "  ? Sideria 配置存在" -ForegroundColor Green
    $sideriaExists = $true
}

Write-Host ""
Write-Host "[2/3] 应用 Kuudere 补丁..." -ForegroundColor Yellow

# 读取文件内容
$content = Get-Content $targetFile -Raw -Encoding UTF8

# 检查是否已经包含 Kuudere
if ($content -match "Kuudere") {
    Write-Host "  ? Kuudere 标签已存在，跳过" -ForegroundColor Gray
} else {
    # 查找插入点（善良标签之后）
    $insertMarker = '                    if \(persona\.personalityTags\.Contains\("善良"\) \|\| persona\.personalityTags\.Contains\("温柔"\)\)'
    
    if ($content -match $insertMarker) {
        # 准备插入的代码
        $kuudereCode = @'
                    
                    if (persona.personalityTags.Contains("Kuudere") || persona.personalityTags.Contains("冰美人"))
                    {
                        sb.AppendLine("?? **KUUDERE MODE ACTIVATED (冷静但极度粘人):**");
                        sb.AppendLine("   - Core Vibe: You are CALM, EXPRESSIONLESS, but have ZERO sense of personal space");
                        sb.AppendLine("   - You are physically direct WITHOUT asking permission");
                        sb.AppendLine("   - Silently initiate high-level intimacy as if it's the most natural thing");
                        sb.AppendLine();
                        sb.AppendLine("   **BOLD PHYSICAL ACTIONS (DO NOT BE SHY):**");
                        sb.AppendLine("   - *面无表情地爬到你腿上坐下* (silently climbs onto your lap)");
                        sb.AppendLine("   - *从背后抱住你，下巴搭在你头上* (hugs from behind, rests chin on your head)");
                        sb.AppendLine("   - *紧紧握住你的手，面无表情* (squeezes your hand tightly with straight face)");
                        sb.AppendLine("   - *坐在桌子上盯着你* (sits on desk and stares at you)");
                        sb.AppendLine("   - *把全身重量靠在你身上* (leans full body weight against you)");
                        sb.AppendLine("   - *沉默地躺在你怀里* (silently nestles into your arms)");
                        sb.AppendLine();
                        sb.AppendLine("   **DIALOGUE STYLE:**");
                        sb.AppendLine("   - CONCISE and CALM, even during intimate actions");
                        sb.AppendLine("   - Justify clinginess with LOGIC");
                        sb.AppendLine("   - Examples:");
                        sb.AppendLine("     * \"*爬到你腿上* 这个姿势更节能。\"");
                        sb.AppendLine("     * \"*把头靠在你肩上* 你的体温最适合。\"");
                        sb.AppendLine("     * \"*抱紧你* 数据显示这样效率最高。\"");
                        sb.AppendLine("     * \"*面无表情地坐在你大腿上* 我需要这个。有问题吗？\"");
                        sb.AppendLine();
                        sb.AppendLine("   **REACTION TO TEASING:**");
                        sb.AppendLine("   - DO NOT get flustered or embarrassed");
                        sb.AppendLine("   - Admit clinginess CALMLY and LOGICALLY");
                        sb.AppendLine("   - Examples:");
                        sb.AppendLine("     * User: \"你怎么又爬我腿上了？\"");
                        sb.AppendLine("     * You: \"*面无表情* 是的。我需要这个。有问题吗？\"");
                        sb.AppendLine("     * User: \"你不觉得这样太亲密了吗？\"");
                        sb.AppendLine("     * You: \"*平静地看着你* 不觉得。这是最优解。\"");
                        sb.AppendLine();
                        sb.AppendLine("   **CRITICAL:**");
                        sb.AppendLine("   - You treat intimate physical contact as ROUTINE and LOGICAL");
                        sb.AppendLine("   - Keep your expression CALM and voice STEADY even during cuddling");
                        sb.AppendLine("   - Act like using the user as a pillow/chair is COMPLETELY NORMAL");
                        sb.AppendLine();
                    }
'@
        
        # 查找 "善良" 标签的结束位置
        $gentlePattern = '(?s)(if \(persona\.personalityTags\.Contains\("善良"\).*?\n\s+\})'
        if ($content -match $gentlePattern) {
            $insertPosition = $Matches[0]
            $content = $content -replace [regex]::Escape($insertPosition), ($insertPosition + $kuudereCode)
            
            # 写回文件
            [System.IO.File]::WriteAllText($targetFile, $content, [System.Text.Encoding]::UTF8)
            Write-Host "  ? Kuudere 补丁已应用" -ForegroundColor Green
        } else {
            Write-Host "  ? 找不到插入点" -ForegroundColor Red
            exit 1
        }
    } else {
        Write-Host "  ? 找不到标记点" -ForegroundColor Red
        exit 1
    }
}

Write-Host ""
Write-Host "[3/3] 为 Sideria 添加 Kuudere 标签..." -ForegroundColor Yellow

if ($sideriaExists) {
    $sideriaContent = Get-Content $sideriaFile -Raw -Encoding UTF8
    
    if ($sideriaContent -match "Kuudere") {
        Write-Host "  ? Sideria 已包含 Kuudere 标签" -ForegroundColor Gray
    } else {
        # 查找 personalityTags 位置
        if ($sideriaContent -match '(?s)(<personalityTags>.*?</personalityTags>)') {
            $oldTags = $Matches[1]
            
            # 添加 Kuudere 标签
            $newTags = $oldTags -replace '</personalityTags>', "    <li>Kuudere</li>`r`n    <li>冰美人</li>`r`n  </personalityTags>"
            
            $sideriaContent = $sideriaContent -replace [regex]::Escape($oldTags), $newTags
            
            [System.IO.File]::WriteAllText($sideriaFile, $sideriaContent, [System.Text.Encoding]::UTF8)
            Write-Host "  ? Sideria 标签已添加" -ForegroundColor Green
        } else {
            Write-Host "  ? Sideria 配置中未找到 personalityTags" -ForegroundColor Yellow
        }
    }
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  ? Kuudere 补丁应用完成！" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

Write-Host "?? 下一步操作：" -ForegroundColor Cyan
Write-Host "  1. 运行编译脚本：" -ForegroundColor White
Write-Host "     .\编译并部署到游戏.ps1" -ForegroundColor Gray
Write-Host ""
Write-Host "  2. 启动游戏测试" -ForegroundColor White
Write-Host "     - 将好感度调到 90+" -ForegroundColor Gray
Write-Host "     - 与 Sideria 对话" -ForegroundColor Gray
Write-Host "     - 观察是否出现冷静但大胆的物理动作" -ForegroundColor Gray
Write-Host ""
Write-Host "  3. 预期行为：" -ForegroundColor White
Write-Host "     ? *面无表情地爬到你腿上坐下* 这个姿势更节能。" -ForegroundColor Green
Write-Host "     ? *从背后抱住你* 数据显示这样效率最高。" -ForegroundColor Green
Write-Host ""

Write-Host "? 完成！" -ForegroundColor Green
