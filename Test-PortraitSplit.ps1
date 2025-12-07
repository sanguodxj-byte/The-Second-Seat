# 立绘自动分割测试脚本

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host " 立绘自动分割测试" -ForegroundColor Yellow
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# 检查纹理文件
$baseDir = "Textures\UI\Narrators\9x16\Sideria"
$expressionsDir = "Textures\UI\Narrators\9x16\Expressions\Sideria"
$outfitsDir = "$baseDir\Outfits"

Write-Host "1. 检查基础立绘:" -ForegroundColor Cyan
if (Test-Path "$baseDir\base.png") {
    $baseSize = (Get-Item "$baseDir\base.png").Length
    Write-Host "  ? base.png 存在 ($([math]::Round($baseSize/1MB, 2)) MB)" -ForegroundColor Green
} else {
    Write-Host "  ? base.png 不存在" -ForegroundColor Red
}

Write-Host ""
Write-Host "2. 检查表情差分（完整立绘）:" -ForegroundColor Cyan
$expressions = @("happy", "sad", "angry", "thoughtful")
foreach ($expr in $expressions) {
    $path = "$expressionsDir\$expr.png"
    if (Test-Path $path) {
        $size = (Get-Item $path).Length
        Write-Host "  ? $expr.png ($([math]::Round($size/1MB, 2)) MB)" -ForegroundColor Green
    } else {
        Write-Host "  ? $expr.png 不存在" -ForegroundColor Yellow
    }
}

Write-Host ""
Write-Host "3. 检查服装差分（完整立绘）:" -ForegroundColor Cyan
$outfits = @("neutral_1", "warm_1", "intimate_1", "devoted_1", "devoted_2")
foreach ($outfit in $outfits) {
    $path = "$outfitsDir\$outfit.png"
    if (Test-Path $path) {
        $size = (Get-Item $path).Length
        Write-Host "  ? $outfit.png ($([math]::Round($size/1MB, 2)) MB)" -ForegroundColor Green
    } else {
        Write-Host "  ? $outfit.png 不存在" -ForegroundColor Yellow
    }
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host " 自动分割功能说明" -ForegroundColor Yellow
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

Write-Host "当前系统会自动：" -ForegroundColor White
Write-Host "  1. 从脖子位置（顶部30%）分割立绘" -ForegroundColor Gray
Write-Host "  2. 头部使用表情差分" -ForegroundColor Gray
Write-Host "  3. 身体使用服装差分" -ForegroundColor Gray
Write-Host "  4. 在分割线位置应用羽化（10px）" -ForegroundColor Gray
Write-Host ""

Write-Host "文件要求：" -ForegroundColor White
Write-Host "  ? 所有文件都是 1080x1920 的完整立绘" -ForegroundColor Green
Write-Host "  ? 不需要透明背景" -ForegroundColor Green
Write-Host "  ? 不需要手动抠图" -ForegroundColor Green
Write-Host "  ? 只需修改头部（表情）或身体（服装）" -ForegroundColor Green
Write-Host ""

Write-Host "测试组合示例：" -ForegroundColor White
Write-Host "  1. base.png (身体) + happy.png (头部) = 快乐表情" -ForegroundColor Cyan
Write-Host "  2. base.png + warm_1.png (服装) = 温暖服装" -ForegroundColor Cyan
Write-Host "  3. base.png + happy.png + warm_1.png = 快乐 + 温暖服装" -ForegroundColor Cyan
Write-Host ""

Write-Host "========================================" -ForegroundColor Cyan
Write-Host " 下一步" -ForegroundColor Yellow
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "1. 启动 RimWorld" -ForegroundColor White
Write-Host "2. 开始游戏并打开 AI 对话" -ForegroundColor White
Write-Host "3. 观察立绘是否正确合成" -ForegroundColor White
Write-Host "4. 等待服装更换（12小时游戏时间）" -ForegroundColor White
Write-Host "5. 查看日志确认分割是否成功" -ForegroundColor White
Write-Host ""
Write-Host "日志关键词:" -ForegroundColor Yellow
Write-Host "  '[PortraitLoader] ? 自动分割合成'" -ForegroundColor Gray
Write-Host "  '[PortraitSplitter] ? 合成完成'" -ForegroundColor Gray
Write-Host ""
Write-Host "准备就绪！可以开始测试了 ??" -ForegroundColor Magenta
Write-Host ""
