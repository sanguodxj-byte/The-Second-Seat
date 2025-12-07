# ========================================
# 自动添加难度模式UI到设置
# ========================================

Write-Host "=== 双难度模式UI自动集成 ===" -ForegroundColor Cyan
Write-Host ""

# 读取ModSettings.cs
$file = "Source\TheSecondSeat\Settings\ModSettings.cs"
$content = Get-Content $file -Encoding UTF8 -Raw

# 查找插入点（listingStandard.GapLine(); 之后，LLM设置之前）
$insertMarker = "listingStandard.GapLine();`n`n            // === LLM"

# 要插入的代码
$uiCode = @'

            // === AI难度模式选择 ===
            listingStandard.Gap(12f);
            listingStandard.Label("AI难度模式");
            listingStandard.Gap(8f);

            // 助手模式
            if (listingStandard.RadioButton(
                "? 助手模式 - 无条件支持，主动建议",
                settings.difficultyMode == PersonaGeneration.AIDifficultyMode.Assistant))
            {
                settings.difficultyMode = PersonaGeneration.AIDifficultyMode.Assistant;
            }

            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.7f, 0.7f, 0.7f);
            listingStandard.Label("  AI将忠实执行所有指令，主动提供建议帮助殖民地发展。");
            listingStandard.Label("  适合新手或希望轻松体验的玩家。");
            GUI.color = Color.white;
            Text.Font = GameFont.Small;

            listingStandard.Gap(12f);

            // 对弈者模式
            if (listingStandard.RadioButton(
                "? 对弈者模式 - 挑战平衡，事件控制",
                settings.difficultyMode == PersonaGeneration.AIDifficultyMode.Opponent))
            {
                settings.difficultyMode = PersonaGeneration.AIDifficultyMode.Opponent;
            }

            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.7f, 0.7f, 0.7f);
            listingStandard.Label("  AI通过事件生成器考验你的策略，好感度影响事件难度。");
            listingStandard.Label("  适合寻求挑战的玩家。极低好感度时可能拒绝指令。");
            GUI.color = Color.white;
            Text.Font = GameFont.Small;

            listingStandard.GapLine();
'@

# 检查是否已经包含UI代码
if ($content -match "AI难度模式") {
    Write-Host "? UI代码已存在，无需添加" -ForegroundColor Green
} else {
    # 替换内容
    $newContent = $content -replace [regex]::Escape($insertMarker), "$insertMarker$uiCode"
    
    # 保存文件
    $newContent | Set-Content $file -Encoding UTF8 -NoNewline
    
    Write-Host "? UI代码已成功添加到 ModSettings.cs" -ForegroundColor Green
}

Write-Host ""
Write-Host "=== 下一步：编译部署 ===" -ForegroundColor Yellow
Write-Host ""
Write-Host "运行: .\Smart-Deploy.ps1" -ForegroundColor Gray
Write-Host ""
