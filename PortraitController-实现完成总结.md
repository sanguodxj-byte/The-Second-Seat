# PortraitController - 实现完成总结

## ? 实现完成

**实现时间：** 2025-12-09  
**版本：** v1.6.14  
**目的：** 创建整图切换动画系统核心控制器

---

## ?? 核心功能

### 1?? 单例模式

```csharp
public class PortraitController
{
    private static PortraitController? instance;
    
    public static PortraitController Instance
    {
        get
        {
            if (instance == null)
            {
                instance = new PortraitController();
            }
            return instance;
        }
    }
}
```

**优点：**
- ? 全局唯一实例
- ? 懒加载（首次访问时创建）
- ? 线程安全（Unity 主线程单线程）

---

### 2?? 纹理缓存系统

```csharp
private Dictionary<string, Texture2D> textureCache = new Dictionary<string, Texture2D>();
```

**缓存键格式：** `defName_type`

**示例：**
- `Sideria_base`
- `Sideria_blink`
- `Sideria_speaking`

**优点：**
- ? 避免重复加载
- ? O(1) 查找速度
- ? 支持多人格缓存

---

### 3?? 自动眨眼系统

#### 眨眼参数

| 参数 | 值 | 说明 |
|------|-----|------|
| `BLINK_DURATION` | 0.15秒 | 单次眨眼持续时间 |
| `BLINK_INTERVAL_MIN` | 3秒 | 最小间隔 |
| `BLINK_INTERVAL_MAX` | 6秒 | 最大间隔 |

#### 眨眼状态机

```
[ 静止 ] → (到达 nextBlinkTime) → [ 眨眼 ] → (持续 0.15s) → [ 静止 ]
```

#### 核心方法

```csharp
private void UpdateBlinkState()
{
    float currentTime = Time.realtimeSinceStartup;
    
    if (isBlinking)
    {
        // 检查眨眼是否结束
        if (currentTime - blinkStartTime >= BLINK_DURATION)
        {
            isBlinking = false;
            ScheduleNextBlink();  // 计划下次眨眼
        }
    }
    else
    {
        // 检查是否应该开始眨眼
        if (currentTime >= nextBlinkTime)
        {
            StartBlink();
        }
    }
}
```

---

### 4?? 语音同步

#### 状态检测

```csharp
if (TTSService.Instance != null && TTSService.Instance.IsSpeaking)
{
    // 显示 speakingTexture
}
```

**优点：**
- ? 实时检测播放状态
- ? 自动同步（无需手动触发）
- ? 支持回退机制

---

### 5?? 优先级逻辑

#### 完整优先级流程

```csharp
public Texture2D GetCurrentPortrait(NarratorPersonaDef def)
{
    // 1. 加载/缓存纹理
    Texture2D baseTexture = GetOrLoadTexture(def.defName, "base", def.portraitPath);
    Texture2D blinkTexture = GetOrLoadTexture(def.defName, "blink", def.portraitPathBlink);
    Texture2D speakingTexture = GetOrLoadTexture(def.defName, "speaking", def.portraitPathSpeaking);
    
    // 2. 优先级判断
    
    // 优先级 1: 眨眼（最高）
    if (isBlinking && blinkTexture != null)
    {
        return blinkTexture;
    }
    
    // 优先级 2: 说话
    if (TTSService.Instance.IsSpeaking && speakingTexture != null)
    {
        return speakingTexture;
    }
    
    // 优先级 3: 默认
    if (baseTexture != null)
    {
        return baseTexture;
    }
    
    // 优先级 4: 占位符
    return GenerateFallbackTexture();
}
```

**优先级表：**

| 状态 | 优先级 | 返回纹理 | 回退方案 |
|------|--------|----------|----------|
| 眨眼中 | 1（最高） | `blinkTexture` | → `baseTexture` |
| 说话中 | 2 | `speakingTexture` | → `baseTexture` |
| 静止 | 3 | `baseTexture` | → 占位符 |
| 所有缺失 | 4（最低） | 灰色占位符 | - |

---

## ?? 使用场景

### 场景 1: 在 UI Window 中显示

```csharp
public class NarratorWindow : Window
{
    private NarratorPersonaDef currentPersona;
    
    public override void DoWindowContents(Rect inRect)
    {
        // 每帧获取最新纹理
        Texture2D texture = PortraitController.Instance.GetCurrentPortrait(currentPersona);
        
        // 绘制
        Rect portraitRect = new Rect(10, 10, 512, 512);
        GUI.DrawTexture(portraitRect, texture, ScaleMode.ScaleToFit);
    }
}
```

### 场景 2: 在屏幕按钮中使用

```csharp
public class NarratorScreenButton : Window
{
    public override void DoWindowContents(Rect inRect)
    {
        Texture2D texture = PortraitController.Instance.GetCurrentPortrait(currentPersona);
        
        if (Widgets.ButtonImage(buttonRect, texture))
        {
            OpenNarratorWindow();
        }
    }
}
```

---

## ?? 技术特性

### 1. 纹理加载

```csharp
private Texture2D? GetOrLoadTexture(string defName, string type, string texturePath)
{
    // 1. 检查路径
    if (string.IsNullOrEmpty(texturePath))
        return null;
    
    // 2. 检查缓存
    string cacheKey = $"{defName}_{type}";
    if (textureCache.TryGetValue(cacheKey, out Texture2D cached))
        return cached;
    
    // 3. 从磁盘加载
    Texture2D texture = ContentFinder<Texture2D>.Get(texturePath, false);
    
    // 4. 设置质量并缓存
    if (texture != null)
    {
        SetTextureQuality(texture);
        textureCache[cacheKey] = texture;
    }
    
    return texture;
}
```

### 2. 纹理质量设置

```csharp
private void SetTextureQuality(Texture2D texture)
{
    if (texture == null) return;
    
    try
    {
        texture.filterMode = FilterMode.Bilinear;  // 双线性过滤
        texture.anisoLevel = 4;                    // 各向异性过滤
    }
    catch
    {
        // 静默忽略
    }
}
```

### 3. 占位符生成

```csharp
private Texture2D GenerateFallbackTexture()
{
    Texture2D fallback = new Texture2D(512, 512, TextureFormat.RGBA32, false);
    Color gray = new Color(0.3f, 0.3f, 0.3f, 1f);
    
    for (int y = 0; y < 512; y++)
    {
        for (int x = 0; x < 512; x++)
        {
            fallback.SetPixel(x, y, gray);
        }
    }
    
    fallback.Apply();
    return fallback;
}
```

---

## ?? 缓存管理

### 方法列表

| 方法 | 功能 |
|------|------|
| `ClearCache()` | 清空所有缓存 |
| `ClearCacheFor(defName)` | 清除特定人格的缓存 |
| `GetCacheInfo()` | 获取缓存统计信息 |

### 使用示例

```csharp
// 切换人格时清理旧缓存
public void SwitchPersona(NarratorPersonaDef newPersona)
{
    if (currentPersona != null)
    {
        PortraitController.Instance.ClearCacheFor(currentPersona.defName);
    }
    
    currentPersona = newPersona;
}

// 查看缓存状态
Log.Message(PortraitController.Instance.GetCacheInfo());
// 输出: "[PortraitController] Cached textures: 15"
```

---

## ?? 调试功能

### 1. 强制眨眼

```csharp
// 立即触发眨眼（用于测试）
PortraitController.Instance.ForceBlinkNow();
```

### 2. 获取眨眼状态

```csharp
string status = PortraitController.Instance.GetBlinkStatus();
// 输出: "Blinking (remaining: 0.12s)" 或 "Idle (next blink in: 4.56s)"
```

### 3. 完整调试信息

```csharp
string debug = PortraitController.Instance.GetDebugInfo();
Log.Message(debug);
```

**输出示例：**
```
[PortraitController Debug]
Cached textures: 9
Blink status: Idle (next blink in: 2.34s)
TTS Speaking: No
```

---

## ?? 性能分析

### 时间复杂度

| 操作 | 复杂度 | 说明 |
|------|--------|------|
| 缓存查找 | O(1) | Dictionary 哈希查找 |
| 纹理加载 | O(n) | 仅首次，n = 文件大小 |
| 眨眼更新 | O(1) | 简单时间比较 |
| 优先级判断 | O(1) | 几个布尔检查 |

### 空间复杂度

| 项目 | 大小 | 说明 |
|------|------|------|
| 单个纹理 | 约 1-2 MB | 512x512 PNG |
| 3个纹理/人格 | 约 3-6 MB | base + blink + speaking |
| 缓存开销 | Dictionary + 键 | 可忽略不计 |

### 内存占用估算

```
假设：
- 5个人格
- 每个人格3个纹理
- 每个纹理1.5 MB

总内存 = 5 × 3 × 1.5 MB = 22.5 MB
```

**优化建议：**
- ? 缓存避免重复加载（节省 CPU 和 I/O）
- ? 仅缓存当前使用的人格（可选优化）
- ? 支持手动清理缓存

---

## ?? 注意事项

### 1. 纹理路径格式

```csharp
// ? 正确
portraitPath = "UI/Narrators/9x16/Sideria/base"

// ? 错误（不需要前缀和后缀）
portraitPath = "Textures/UI/Narrators/9x16/Sideria/base.png"
```

### 2. 线程安全

- ? Unity 是单线程渲染
- ? 所有调用在主线程
- ? 无需额外同步机制

### 3. 回退机制

**重要：** 所有纹理缺失都有回退方案
- `blinkTexture` 缺失 → 使用 `baseTexture`
- `speakingTexture` 缺失 → 使用 `baseTexture`
- `baseTexture` 缺失 → 生成灰色占位符

### 4. DevMode 日志

启用 DevMode 后输出详细日志：

```
[PortraitController] Loaded and cached texture: Sideria_base from ...
[PortraitController] Next blink scheduled in 4.56s (at 123.45)
[PortraitController] Blink started at 123.45
[PortraitController] Blink ended, next blink at 127.89
```

---

## ?? 未来扩展

### 可能的功能扩展

1. **更多动画状态**
   - 微笑、惊讶、愤怒等表情
   - 转头、点头等动作

2. **动画过渡**
   - 淡入淡出效果
   - 平滑过渡

3. **情绪系统集成**
   - 根据好感度改变表情频率
   - 根据心情调整眨眼速度

4. **缓存策略优化**
   - LRU（最近最少使用）缓存
   - 自动清理长时间未使用的纹理

---

## ?? 文件清单

### 已创建文件

1. ? `Source/TheSecondSeat/Utils/PortraitController.cs` - 核心实现
2. ? `PortraitController-使用指南.md` - 详细使用说明
3. ? `PortraitController-快速参考.md` - 快速查阅
4. ? `PortraitController-实现完成总结.md` - 本文档

---

## ?? 下一步

### 推荐实现顺序

1. **集成到 NarratorScreenButton**
   - 替换现有纹理显示逻辑
   - 测试眨眼和语音同步

2. **集成到 NarratorWindow**
   - 在对话窗口显示动画
   - 测试多人格切换

3. **添加调试面板**
   - 实时显示缓存状态
   - 手动触发眨眼测试
   - 显示当前优先级

4. **性能测试**
   - 监控内存占用
   - 测试多人格场景
   - 验证缓存有效性

---

## ? 验证清单

### 功能验证

- [ ] 单例模式正常工作
- [ ] 纹理缓存有效
- [ ] 眨眼自动触发（3-6秒间隔）
- [ ] 眨眼持续时间正确（0.15秒）
- [ ] 语音同步工作（TTS 播放时显示说话纹理）
- [ ] 优先级正确（眨眼 > 说话 > 默认）
- [ ] 回退机制有效（缺失纹理时使用 base）
- [ ] 缓存清理功能正常
- [ ] DevMode 日志输出正确

### 性能验证

- [ ] 缓存命中率高（重复调用不重新加载）
- [ ] 内存占用合理（约 3-6 MB/人格）
- [ ] CPU 占用低（眨眼更新 O(1)）
- [ ] 无内存泄漏

---

**实现完成！** ?  
**版本：** v1.6.14  
**状态：** 可以开始集成到 UI 系统

_The Second Seat Mod Team_
