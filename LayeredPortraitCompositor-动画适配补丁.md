# 分层立绘合成器 - 眨眼和张嘴动画适配补丁

## ?? 适配说明

此补丁为 `LayeredPortraitCompositor.cs` 添加眨眼和张嘴动画支持。

## ?? 修改内容

### 1. 修改 `CompositeLayers` 方法

在加载图层前，添加动画层处理：

```csharp
// 在方法开始处添加（第28行左右）
public static Texture2D CompositeLayers(
    LayeredPortraitConfig config, 
    ExpressionType expression = ExpressionType.Neutral, 
    string outfit = "default")
{
    if (config == null)
    {
        Log.Error("[LayeredPortraitCompositor] Config is null");
        return GenerateFallbackTexture(config?.OutputSize ?? new Vector2Int(1024, 1572));
    }

    // ? 新增：动态替换眼睛和嘴巴层路径
    string personaDefName = config.PersonaDefName;
    
    // 获取动画状态
    string blinkLayerName = BlinkAnimationSystem.GetBlinkLayerName(personaDefName);
    string mouthLayerName = MouthAnimationSystem.GetMouthLayerName(personaDefName);
    
    // 临时修改配置中的眼睛和嘴巴层路径
    var originalConfig = config;
    config = ApplyAnimationLayers(config, blinkLayerName, mouthLayerName);
    
    // ... 继续原有逻辑
}
```

### 2. 新增 `ApplyAnimationLayers` 方法

在文件末尾添加（约470行）：

```csharp
/// <summary>
/// ? 应用眨眼和张嘴动画层
/// </summary>
private static LayeredPortraitConfig ApplyAnimationLayers(
    LayeredPortraitConfig original, 
    string blinkLayerName, 
    string mouthLayerName)
{
    // 克隆配置（避免修改原配置）
    var modified = new LayeredPortraitConfig
    {
        PersonaDefName = original.PersonaDefName,
        OutputSize = original.OutputSize,
        EnableCache = original.EnableCache,
        Layers = new List<LayerDefinition>()
    };
    
    foreach (var layer in original.Layers)
    {
        var clonedLayer = layer.Clone();
        
        // 替换眼睛层路径
        if (layer.Type == LayerType.Eyes)
        {
            string personaFolder = GetPersonaFolderName(original.PersonaDefName);
            clonedLayer.TexturePath = $"UI/Narrators/9x16/Layers/{personaFolder}/{blinkLayerName}";
            
            if (Prefs.DevMode)
            {
                Log.Message($"[LayeredPortraitCompositor] Eyes layer → {blinkLayerName}");
            }
        }
        
        // 替换嘴巴层路径
        if (layer.Type == LayerType.Mouth)
        {
            string personaFolder = GetPersonaFolderName(original.PersonaDefName);
            clonedLayer.TexturePath = $"UI/Narrators/9x16/Layers/{personaFolder}/{mouthLayerName}";
            
            if (Prefs.DevMode)
            {
                Log.Message($"[LayeredPortraitCompositor] Mouth layer → {mouthLayerName}");
            }
        }
        
        modified.Layers.Add(clonedLayer);
    }
    
    return modified;
}
```

## ?? 完整文件位置

`Source/TheSecondSeat/PersonaGeneration/LayeredPortraitCompositor.cs`

## ?? 集成步骤

1. 备份原文件
2. 打开 `LayeredPortraitCompositor.cs`
3. 在 `CompositeLayers` 方法开头添加动画层处理代码
4. 在文件末尾添加 `ApplyAnimationLayers` 方法
5. 编译测试

## ? 预期效果

- **眨眼动画：** 眼睛层自动在 `eyes_open`, `eyes_half`, `eyes_closed` 之间切换
- **张嘴动画：** 嘴巴层根据 TTS 播放状态动态更新
- **分层合成：** 动画层与其他层正常混合
- **缓存失效：** 动画状态变化时，缓存键自动更新

## ?? 常见问题

### Q: 动画不流畅？
A: 检查 `PORTRAIT_UPDATE_INTERVAL` 是否设置得太大（建议 15-30 ticks）

### Q: 眼睛/嘴巴层缺失？
A: 确保纹理路径正确：
```
Textures/UI/Narrators/9x16/Layers/Sideria/
├── eyes_open.png
├── eyes_half.png
├── eyes_closed.png
├── mouth_closed.png
├── mouth_smile.png
├── mouth_open_small.png
└── mouth_open_wide.png
```

### Q: 性能问题？
A: 禁用缓存或增大更新间隔
