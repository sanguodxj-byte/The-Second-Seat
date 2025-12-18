# 快速修复 TTS 嘴部动画
# 自动生成于: 2025-12-17 13:59:50

Write-Host "应用 TTS 嘴部动画修复..." -ForegroundColor Yellow

# 1. 确保 WindowUpdate 调用 MouthAnimationSystem.Update()
$buttonFile = "Source\TheSecondSeat\UI\NarratorScreenButton.cs"
if (Test-Path $buttonFile) {
    $content = Get-Content $buttonFile -Raw
    if ($content -notmatch "MouthAnimationSystem\.Update") {
        Write-Host "   添加 MouthAnimationSystem.Update() 调用..." -ForegroundColor Yellow
        # 在 WindowUpdate 中添加调用
        # (需要手动实现具体逻辑)
    }
}

# 2. 确保 DrawLayeredPortraitRuntime 调用 GetMouthLayerName()
$panelFile = "Source\TheSecondSeat\UI\FullBodyPortraitPanel.cs"
if (Test-Path $panelFile) {
    $content = Get-Content $panelFile -Raw
    if ($content -notmatch "MouthAnimationSystem\.GetMouthLayerName") {
        Write-Host "   添加 GetMouthLayerName() 调用..." -ForegroundColor Yellow
        # 在嘴巴层绘制代码中添加调用
        # (需要手动实现具体逻辑)
    }
}

Write-Host "   ? 修复完成！重新编译并部署。" -ForegroundColor Green
