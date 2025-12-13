# 表情缓存诊断脚本 - v1.6.28
# 用于检查表情切换时缓存是否正常清理

Write-Host "=" -ForegroundColor Cyan
Write-Host "表情缓存诊断" -ForegroundColor Cyan
Write-Host "=" -ForegroundColor Cyan
Write-Host ""

# 检查关键代码
$targetFile = "Source\TheSecondSeat\UI\NarratorScreenButton.cs"

Write-Host "1. 检查 TriggerExpression() 方法..." -ForegroundColor Yellow

$content = Get-Content $targetFile -Raw

# 查找 TriggerExpression 方法
if ($content -match '(?s)private void TriggerExpression\(.*?\{(.*?)\}(?=\s+///|\s+private\s+)') {
    $method = $matches[1]
    
    Write-Host "   ? 找到 TriggerExpression 方法" -ForegroundColor Green
    
    # 检查是否正确清理缓存
    if ($method -match 'ClearAvatarCache\(currentPersona\.defName, lastExpression\)') {
        Write-Host "   ? 调用了 ClearAvatarCache" -ForegroundColor Green
    } else {
        Write-Host "   ? 没有调用 ClearAvatarCache！" -ForegroundColor Red
    }
    
    if ($method -match 'ClearPortraitCache\(currentPersona\.defName, lastExpression\)') {
        Write-Host "   ? 调用了 ClearPortraitCache" -ForegroundColor Green
    } else {
        Write-Host "   ? 没有调用 ClearPortraitCache！" -ForegroundColor Red
    }
    
    # 检查条件判断
    if ($method -match 'if \(lastExpression != expression\)') {
        Write-Host "   ? 清理缓存有条件判断（可能导致缓存未清理）" -ForegroundColor Yellow
        Write-Host "     当前逻辑: 只有表情不同时才清理" -ForegroundColor Gray
        Write-Host "     问题: 如果表情相同，缓存不会更新" -ForegroundColor Gray
    }
    
    # 显示关键代码
    Write-Host ""
    Write-Host "2. TriggerExpression 当前逻辑:" -ForegroundColor Yellow
    Write-Host $method.Trim() -ForegroundColor Gray
    
} else {
    Write-Host "   ? 未找到 TriggerExpression 方法！" -ForegroundColor Red
}

Write-Host ""
Write-Host "=" -ForegroundColor Cyan
Write-Host "诊断建议" -ForegroundColor Cyan
Write-Host "=" -ForegroundColor Cyan
Write-Host ""

Write-Host "问题1: 缓存清理条件错误" -ForegroundColor Yellow
Write-Host "  当前: if (lastExpression != expression) { 清理缓存 }" -ForegroundColor Gray
Write-Host "  问题: 如果连续设置相同表情，缓存不会清理" -ForegroundColor Red
Write-Host ""
Write-Host "  建议修复:" -ForegroundColor Green
Write-Host "    // ? 无条件清理旧表情缓存" -ForegroundColor Green
Write-Host "    AvatarLoader.ClearAvatarCache(currentPersona.defName, lastExpression);" -ForegroundColor Green
Write-Host "    PortraitLoader.ClearPortraitCache(currentPersona.defName, lastExpression);" -ForegroundColor Green
Write-Host "    " -ForegroundColor Green
Write-Host "    // ? 设置新表情" -ForegroundColor Green
Write-Host "    ExpressionSystem.SetExpression(currentPersona.defName, expression, duration, reason);" -ForegroundColor Green
Write-Host ""

Write-Host "问题2: 日志缺失" -ForegroundColor Yellow
Write-Host "  建议添加调试日志：" -ForegroundColor Green
Write-Host "    if (Prefs.DevMode)" -ForegroundColor Green
Write-Host "    {" -ForegroundColor Green
Write-Host "        Log.Message($\"[TriggerExpression] {currentPersona.defName}: {lastExpression} → {expression}\");" -ForegroundColor Green
Write-Host "    }" -ForegroundColor Green
Write-Host ""

Write-Host "=" -ForegroundColor Cyan
Write-Host "执行修复？" -ForegroundColor Cyan
Write-Host "=" -ForegroundColor Cyan
Write-Host ""
Write-Host "按任意键退出（不执行修复）" -ForegroundColor Gray
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
