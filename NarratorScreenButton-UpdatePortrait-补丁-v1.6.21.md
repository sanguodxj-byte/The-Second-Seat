# 手动修改 NarratorScreenButton.cs 的代码补丁
# v1.6.21 - 头像和立绘切换按钮修复

## 需要修改的方法：UpdatePortrait()

找到 `UpdatePortrait()` 方法（大约在第 616 行），将整个方法替换为以下代码：

```csharp
/// <summary>
/// ? 更新动态头像（支持表情系统和立绘模式）
/// ? v1.6.21: 检测设置变化，自动切换头像/立绘模式
/// </summary>
private void UpdatePortrait()
{
    if (Find.TickManager.TicksGame - portraitUpdateTick < PORTRAIT_UPDATE_INTERVAL)
    {
        return;
    }
    
    portraitUpdateTick = Find.TickManager.TicksGame;
    
    // ? v1.6.21: 检测设置变化
    var modSettings = LoadedModManager.GetMod<TheSecondSeatMod>()?.GetSettings<TheSecondSeatSettings>();
    bool currentPortraitMode = modSettings?.usePortraitMode ?? false;
    
    if (currentPortraitMode != lastUsePortraitMode)
    {
        // 设置变化，清除所有缓存
        AvatarLoader.ClearAllCache();
        PortraitLoader.ClearAllCache();
        
        // ? 同时清除分层立绘缓存
        try
        {
            LayeredPortraitCompositor.ClearAllCache();
        }
        catch
        {
            // 如果方法不存在，静默忽略
        }
        
        lastUsePortraitMode = currentPortraitMode;
        currentPortrait = null;  // 强制重新加载
        currentPersona = null;
        
        if (Prefs.DevMode)
        {
            Log.Message($"[NarratorScreenButton] Portrait mode changed to: {(currentPortraitMode ? "立绘模式" : "头像模式")}");
        }
    }
    
    try
    {
        var manager = Current.Game?.GetComponent<NarratorManager>();
        if (manager == null)
        {
            currentPortrait = null;
            return;
        }
        
        var persona = manager.GetCurrentPersona();
        if (persona == null)
        {
            currentPortrait = null;
            return;
        }
        
        var expressionState = ExpressionSystem.GetExpressionState(persona.defName);
        ExpressionType currentExpression = expressionState.CurrentExpression;
        
        if (persona != currentPersona || currentExpression != lastExpression)
        {
            if (currentPersona != null && lastExpression != currentExpression)
            {
                AvatarLoader.ClearAvatarCache(currentPersona.defName, lastExpression);
                PortraitLoader.ClearPortraitCache(currentPersona.defName, lastExpression);
            }
            
            currentPersona = persona;
            lastExpression = currentExpression;
            
            // ? 根据设置选择加载头像或立绘
            if (modSettings != null && modSettings.usePortraitMode)
            {
                // 立绘模式：使用 1024x1572 全身立绘
                currentPortrait = PortraitLoader.LoadPortrait(persona, currentExpression);
            }
            else
            {
                // 头像模式：使用 512x512 头像
                currentPortrait = AvatarLoader.LoadAvatar(persona, currentExpression);
            }
        }
    }
    catch (System.Exception ex)
    {
        Log.Warning($"[NarratorScreenButton] 更新头像失败: {ex.Message}");
        currentPortrait = null;
    }
}
```

## 修改步骤

1. 打开 `Source/TheSecondSeat/UI/NarratorScreenButton.cs`
2. 找到 `UpdatePortrait()` 方法（使用 Ctrl+F 搜索）
3. 完整替换整个方法（从 `private void UpdatePortrait()` 到方法结束的 `}`）
4. 保存文件

## 验证

修改完成后，检查：
- 文件中已有 `private bool lastUsePortraitMode = false;` 字段（第 47 行）
- `UpdatePortrait()` 方法包含设置变化检测逻辑
- 没有编译错误

## 预期效果

修改完成并编译后：
- 在游戏设置中切换"使用立绘模式"
- 返回游戏，AI按钮上的图片立即切换
- 无需重启游戏
