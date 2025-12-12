# PortraitController - 快速参考

## ?? 核心用法

```csharp
// 获取当前应显示的立绘
Texture2D texture = PortraitController.Instance.GetCurrentPortrait(personaDef);
GUI.DrawTexture(rect, texture);
```

---

## ?? XML 配置

```xml
<NarratorPersonaDef>
    <defName>Sideria_Default</defName>
    
    <!-- 基础立绘（必需） -->
    <portraitPath>UI/Narrators/9x16/Sideria/base</portraitPath>
    
    <!-- 眨眼纹理（可选） -->
    <portraitPathBlink>UI/Narrators/9x16/Sideria/blink</portraitPathBlink>
    
    <!-- 说话纹理（可选） -->
    <portraitPathSpeaking>UI/Narrators/9x16/Sideria/speaking</portraitPathSpeaking>
</NarratorPersonaDef>
```

---

## ?? 优先级

```
眨眼 (最高) > 说话 > 默认
```

---

## ??? 眨眼参数

| 参数 | 值 |
|------|-----|
| **间隔** | 3-6秒（随机） |
| **持续** | 0.15秒 |

---

## ?? 常用方法

```csharp
// 清空所有缓存
PortraitController.Instance.ClearCache();

// 清除特定人格
PortraitController.Instance.ClearCacheFor("Sideria_Default");

// 强制眨眼（测试）
PortraitController.Instance.ForceBlinkNow();

// 获取状态
string status = PortraitController.Instance.GetBlinkStatus();
string debug = PortraitController.Instance.GetDebugInfo();
```

---

## ?? 纹理组织

```
Textures/UI/Narrators/9x16/Sideria/
├── base.png      (必需)
├── blink.png     (可选)
└── speaking.png  (可选)
```

---

## ?? 回退机制

```
blinkTexture 缺失    → baseTexture
speakingTexture 缺失 → baseTexture
baseTexture 缺失     → 灰色占位符
```

---

**版本：** v1.6.14  
**状态：** ? 可用

_The Second Seat Mod Team_
