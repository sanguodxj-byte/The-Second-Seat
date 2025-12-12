# PortraitController - 立绘控制器使用指南

## ?? 功能概述

`PortraitController` 是一个单例类，负责管理整图切换动画系统，包括：
- ? **纹理缓存** - 避免重复加载
- ? **自动眨眼** - 每3-6秒随机眨眼0.15秒
- ? **语音同步** - 说话时自动显示张嘴纹理
- ? **优先级逻辑** - 眨眼 > 说话 > 默认

---

## ?? 核心方法

### 1?? 获取当前立绘

```csharp
// 在 UI 绘制时调用
Texture2D currentTexture = PortraitController.Instance.GetCurrentPortrait(personaDef);
GUI.DrawTexture(rect, currentTexture);
```

**返回优先级：**
1. **眨眼纹理** - 如果正在眨眼且 `portraitPathBlink` 存在
2. **说话纹理** - 如果 `TTSService.Instance.IsSpeaking` 且 `portraitPathSpeaking` 存在
3. **基础纹理** - 默认状态（`portraitPath`）
4. **占位符** - 如果所有纹理都缺失

---

## ?? XML 配置

### 定义纹理路径

```xml
<NarratorPersonaDef>
    <defName>Sideria_Default</defName>
    <narratorName>希德莉娅</narratorName>
    
    <!-- 基础立绘（必需） -->
    <portraitPath>UI/Narrators/9x16/Sideria/base</portraitPath>
    
    <!-- 眨眼纹理（可选） -->
    <portraitPathBlink>UI/Narrators/9x16/Sideria/blink</portraitPathBlink>
    
    <!-- 说话纹理（可选） -->
    <portraitPathSpeaking>UI/Narrators/9x16/Sideria/speaking</portraitPathSpeaking>
</NarratorPersonaDef>
```

---

## ?? 纹理缓存

### 缓存键格式

```
defName_type
```

**示例：**
- `Sideria_base` - 基础纹理
- `Sideria_blink` - 眨眼纹理
- `Sideria_speaking` - 说话纹理

### 缓存管理

```csharp
// 清空所有缓存
PortraitController.Instance.ClearCache();

// 清除特定人格的缓存
PortraitController.Instance.ClearCacheFor("Sideria_Default");

// 获取缓存信息
string info = PortraitController.Instance.GetCacheInfo();
Log.Message(info);  // "[PortraitController] Cached textures: 15"
```

---

## ??? 眨眼系统

### 眨眼参数

| 参数 | 值 | 说明 |
|------|-----|------|
| **间隔** | 3-6秒（随机） | 两次眨眼之间的时间 |
| **持续** | 0.15秒 | 单次眨眼持续时间 |
| **优先级** | 最高 | 眨眼会覆盖说话动画 |

### 手动触发眨眼（测试用）

```csharp
// 强制立即眨眼
PortraitController.Instance.ForceBlinkNow();

// 获取眨眼状态
string status = PortraitController.Instance.GetBlinkStatus();
// 输出："Blinking (remaining: 0.12s)" 或 "Idle (next blink in: 4.56s)"
```

---

## ??? 语音同步

### 说话状态检测

```csharp
// PortraitController 自动检测 TTSService 的播放状态
if (TTSService.Instance.IsSpeaking)
{
    // 自动显示 speakingTexture
}
```

**优先级：**
- 眨眼 > 说话
- 如果正在眨眼，眨眼完成后才会显示说话动画

---

## ?? UI 集成示例

### 示例 1: 在 Window 中显示

```csharp
public class NarratorWindow : Window
{
    private NarratorPersonaDef currentPersona;
    
    public override void DoWindowContents(Rect inRect)
    {
        // 获取当前应显示的纹理
        Texture2D portrait = PortraitController.Instance.GetCurrentPortrait(currentPersona);
        
        // 绘制立绘
        Rect portraitRect = new Rect(10, 10, 512, 512);
        GUI.DrawTexture(portraitRect, portrait, ScaleMode.ScaleToFit);
    }
}
```

### 示例 2: 在屏幕按钮中使用

```csharp
public class NarratorScreenButton : Window
{
    private NarratorPersonaDef currentPersona;
    
    public override void DoWindowContents(Rect inRect)
    {
        // 每帧获取最新纹理
        Texture2D currentTexture = PortraitController.Instance.GetCurrentPortrait(currentPersona);
        
        // 绘制按钮
        Rect buttonRect = new Rect(0, 0, 128, 128);
        if (Widgets.ButtonImage(buttonRect, currentTexture))
        {
            // 按钮点击逻辑
            OpenNarratorWindow();
        }
    }
}
```

---

## ?? 调试

### 获取完整调试信息

```csharp
string debugInfo = PortraitController.Instance.GetDebugInfo();
Log.Message(debugInfo);
```

**输出示例：**
```
[PortraitController Debug]
Cached textures: 9
Blink status: Idle (next blink in: 2.34s)
TTS Speaking: No
```

### DevMode 日志

启用 DevMode 后，PortraitController 会输出详细日志：

```
[PortraitController] Loaded and cached texture: Sideria_base from UI/Narrators/9x16/Sideria/base
[PortraitController] Next blink scheduled in 4.56s (at 123.45)
[PortraitController] Blink started at 123.45
[PortraitController] Blink ended, next blink at 127.89
```

---

## ?? 优先级逻辑

### 完整流程图

```
┌─────────────────────────────────────────┐
│ GetCurrentPortrait(def)                 │
└─────────────────────────────────────────┘
                ↓
┌─────────────────────────────────────────┐
│ 1. 加载/缓存纹理                         │
│    - baseTexture (必需)                 │
│    - blinkTexture (可选)                │
│    - speakingTexture (可选)             │
└─────────────────────────────────────────┘
                ↓
┌─────────────────────────────────────────┐
│ 2. 优先级判断                            │
│                                          │
│  ┌────────────────────────────┐         │
│  │ 正在眨眼? (isBlinking)     │         │
│  │   ↓ Yes                    │         │
│  │ 返回 blinkTexture          │         │
│  │ (缺失则回退到 baseTexture) │         │
│  └────────────────────────────┘         │
│                ↓ No                      │
│  ┌────────────────────────────┐         │
│  │ 正在说话? (IsSpeaking)     │         │
│  │   ↓ Yes                    │         │
│  │ 返回 speakingTexture       │         │
│  │ (缺失则回退到 baseTexture) │         │
│  └────────────────────────────┘         │
│                ↓ No                      │
│  ┌────────────────────────────┐         │
│  │ 返回 baseTexture           │         │
│  │ (缺失则返回占位符)         │         │
│  └────────────────────────────┘         │
└─────────────────────────────────────────┘
```

---

## ?? 注意事项

### 1. 纹理路径

- ? **正确：** `UI/Narrators/9x16/Sideria/base`
- ? **错误：** `Textures/UI/Narrators/9x16/Sideria/base.png`
- ContentFinder 自动处理 `Textures/` 前缀和 `.png` 后缀

### 2. 纹理文件组织

```
Textures/
└── UI/
    └── Narrators/
        └── 9x16/
            └── Sideria/
                ├── base.png         (必需)
                ├── blink.png        (可选)
                └── speaking.png     (可选)
```

### 3. 性能优化

- ? 纹理会自动缓存，不会重复加载
- ? 眨眼逻辑仅在需要时更新
- ? 使用 `Prefs.DevMode` 控制日志输出

### 4. 回退机制

如果纹理缺失，系统会自动回退：
1. `blinkTexture` 缺失 → 使用 `baseTexture`
2. `speakingTexture` 缺失 → 使用 `baseTexture`
3. `baseTexture` 缺失 → 生成灰色占位符

---

## ?? 最佳实践

### 1. 在 UI 循环中使用

```csharp
public override void DoWindowContents(Rect inRect)
{
    // ? 每帧调用以获取最新状态
    Texture2D texture = PortraitController.Instance.GetCurrentPortrait(personaDef);
    GUI.DrawTexture(rect, texture);
}
```

### 2. 切换人格时清理缓存

```csharp
public void SwitchPersona(NarratorPersonaDef newPersona)
{
    // 清除旧人格的缓存
    if (currentPersona != null)
    {
        PortraitController.Instance.ClearCacheFor(currentPersona.defName);
    }
    
    currentPersona = newPersona;
}
```

### 3. 监听 TTS 事件

```csharp
// PortraitController 自动监听 TTSService.IsSpeaking
// 无需手动处理，只需正常调用 GetCurrentPortrait()
```

---

## ?? 性能指标

| 指标 | 值 |
|------|-----|
| **缓存查找** | O(1) - Dictionary |
| **纹理加载** | 仅首次（之后使用缓存） |
| **眨眼更新** | 每帧 1 次布尔检查 |
| **内存占用** | 每个人格约 3-6 MB（3个纹理） |

---

## ?? 故障排除

### 问题 1: 纹理未显示

**检查：**
1. XML 中 `portraitPath` 路径是否正确
2. 纹理文件是否存在于 `Textures/` 目录
3. 文件扩展名是否为 `.png`

**调试：**
```csharp
if (Prefs.DevMode)
{
    Log.Message(PortraitController.Instance.GetDebugInfo());
}
```

### 问题 2: 眨眼不工作

**检查：**
1. `portraitPathBlink` 是否在 XML 中定义
2. 眨眼纹理文件是否存在
3. 是否在 UI 循环中调用 `GetCurrentPortrait()`

**强制测试：**
```csharp
PortraitController.Instance.ForceBlinkNow();
```

### 问题 3: 说话动画不工作

**检查：**
1. `portraitPathSpeaking` 是否定义
2. `TTSService.Instance.IsSpeaking` 是否为 true
3. TTS 是否正在播放音频

---

**完成！** ?  
**文件：** `Source/TheSecondSeat/Utils/PortraitController.cs`  
**版本：** v1.6.14

_The Second Seat Mod Team_
