# ?? ModSettings.cs 代码简化重构方案 - v1.6.65

## ?? 当前状态分析

### 文件大小
- **当前**: ~1100 行代码，约 8MB
- **主要类**: `TheSecondSeatSettings`（数据类）、`TheSecondSeatMod`（UI类）

### 代码膨胀原因
1. **UI 绘制代码过多**（~600 行）
   - `DrawDifficultyOption()` - 100 行
   - `DrawDifficultyCard()` - 80 行（未使用）
   - `DrawCollapsibleSection()` - 40 行
   - `DoSettingsWindowContents()` - 400+ 行

2. **配置方法重复**（~100 行）
   - `ConfigureWebSearch()`
   - `ConfigureMultimodalAnalysis()`
   - `ConfigureTTS()`

3. **测试方法过多**（~80 行）
   - `TestConnection()`
   - `TestTTS()`
   - `ShowVoiceSelectionMenu()`

---

## ? 重构方案

### 方案 1: 拆分 UI 辅助类（推荐）
**收益**: 减少 ~200 行代码

```
Source\TheSecondSeat\Settings\
├── ModSettings.cs (设置数据 + 主入口)
├── SettingsUI.cs (UI 辅助类)
└── SettingsHelper.cs (配置/测试方法)
```

#### 1.1 创建 `SettingsUI.cs`
提取所有 UI 绘制方法：
- `DrawDifficultyOption()`
- `DrawDifficultyCard()` (删除，未使用)
- `DrawCollapsibleSection()`

#### 1.2 创建 `SettingsHelper.cs`
提取所有配置和测试方法：
- `ConfigureWebSearch()`
- `ConfigureMultimodalAnalysis()`
- `ConfigureTTS()`
- `TestConnection()`
- `TestTTS()`
- `ShowVoiceSelectionMenu()`

#### 1.3 保留 `ModSettings.cs`
仅保留：
- `TheSecondSeatSettings` 数据类
- `TheSecondSeatMod` 主类（调用UI/Helper）
- `DoSettingsWindowContents()` 主逻辑

---

### 方案 2: 删除未使用代码（最简单）
**收益**: 减少 ~100 行代码

#### 删除清单
1. **`DrawDifficultyCard()` 方法** - 80 行
   - 未被调用，与 `DrawDifficultyOption()` 功能重复

2. **简化 `GetExampleGlobalPrompt()`** - 10 行
   - 当前返回占位符，可改为返回空字符串

---

### 方案 3: 折叠区域内联化（激进）
**收益**: 减少 ~100 行代码

将所有 `DrawCollapsibleSection()` 调用改为直接内联绘制：

```csharp
// 当前（40 行）
DrawCollapsibleSection(listingStandard, "LLM Settings", ref settings.collapseLLMSettings, () =>
{
    // 绘制代码
});

// 简化后（15 行）
if (listingStandard.ButtonText("▼ LLM Settings"))
{
    settings.collapseLLMSettings = !settings.collapseLLMSettings;
}
if (!settings.collapseLLMSettings)
{
    // 绘制代码
}
```

---

## ?? 推荐执行计划

### 阶段 1: 快速瘦身（立即执行）
**目标**: 减少 150 行

1. ? 删除 `DrawDifficultyCard()` 方法（80 行）
2. ? 简化 `GetExampleGlobalPrompt()`（10 行）
3. ? 移除重复的注释（~60 行）

### 阶段 2: 结构重构（可选）
**目标**: 减少 300 行

1. 创建 `SettingsUI.cs`
2. 创建 `SettingsHelper.cs`
3. 重构 `DoSettingsWindowContents()`

---

## ?? 详细重构步骤

### 步骤 1: 删除未使用代码

#### 1.1 删除 `DrawDifficultyCard()`
位置：ModSettings.cs 第 270-350 行

```csharp
// ? 删除整个方法（未被调用）
private void DrawDifficultyCard(Rect rect, Texture2D? icon, string title, string description, bool isSelected, Color accentColor)
{
    // 80 行代码...
}
```

#### 1.2 简化 `GetExampleGlobalPrompt()`
位置：ModSettings.cs 第 1050-1070 行

```csharp
// 当前
private string GetExampleGlobalPrompt()
{
    return @"# ?????????..."; // 大段文本
}

// 简化后
private string GetExampleGlobalPrompt()
{
    return "# 全局提示词示例\n请根据需要自定义AI的行为准则...";
}
```

---

### 步骤 2: 创建 SettingsUI.cs（可选）

```csharp
using UnityEngine;
using Verse;
using RimWorld;
using System;

namespace TheSecondSeat.Settings
{
    /// <summary>
    /// 设置界面 UI 辅助类
    /// 提取自 ModSettings.cs，减少主文件代码量
    /// </summary>
    public static class SettingsUI
    {
        /// <summary>
        /// 绘制难度模式选项（带图标）
        /// </summary>
        public static void DrawDifficultyOption(
            Rect rect, 
            Texture2D? icon, 
            string title, 
            string subtitle, 
            string description, 
            bool isSelected, 
            Color accentColor)
        {
            // 背景
            if (isSelected)
            {
                Widgets.DrawBoxSolid(rect, new Color(accentColor.r * 0.3f, accentColor.g * 0.3f, accentColor.b * 0.3f, 0.5f));
            }
            else if (Mouse.IsOver(rect))
            {
                Widgets.DrawBoxSolid(rect, new Color(0.25f, 0.25f, 0.25f, 0.5f));
            }
            
            // ... 其余代码
        }
        
        /// <summary>
        /// 绘制折叠区域
        /// </summary>
        public static void DrawCollapsibleSection(
            Listing_Standard listing, 
            string title, 
            ref bool collapsed, 
            Action drawContent)
        {
            var headerRect = listing.GetRect(30f);
            
            // ... 其余代码
        }
    }
}
```

---

### 步骤 3: 创建 SettingsHelper.cs（可选）

```csharp
using Verse;
using RimWorld;
using System;
using TheSecondSeat.WebSearch;

namespace TheSecondSeat.Settings
{
    /// <summary>
    /// 设置辅助类 - 配置和测试方法
    /// 提取自 ModSettings.cs
    /// </summary>
    public static class SettingsHelper
    {
        public static void ConfigureWebSearch(TheSecondSeatSettings settings)
        {
            string? apiKey = settings.searchEngine.ToLower() switch
            {
                "bing" => settings.bingApiKey,
                "google" => settings.googleApiKey,
                _ => null
            };

            WebSearchService.Instance.Configure(
                settings.searchEngine,
                apiKey,
                settings.googleSearchEngineId
            );

            Log.Message($"[The Second Seat] Web search configured: {settings.searchEngine}");
        }
        
        public static void ConfigureMultimodalAnalysis(TheSecondSeatSettings settings)
        {
            try
            {
                PersonaGeneration.MultimodalAnalysisService.Instance.Configure(
                    settings.multimodalProvider,
                    settings.multimodalApiKey,
                    settings.visionModel,
                    settings.textAnalysisModel
                );
                
                Log.Message($"[The Second Seat] Multimodal analysis configured: {settings.multimodalProvider}");
            }
            catch (Exception ex)
            {
                Log.Error($"[The Second Seat] Multimodal analysis configuration failed: {ex.Message}");
            }
        }
        
        public static void ConfigureTTS(TheSecondSeatSettings settings)
        {
            try
            {
                TTS.TTSService.Instance.Configure(
                    settings.ttsProvider,
                    settings.ttsApiKey,
                    settings.ttsRegion,
                    settings.ttsVoice,
                    settings.ttsSpeechRate,
                    settings.ttsVolume
                );
                
                Log.Message($"[The Second Seat] TTS configured: {settings.ttsProvider}");
            }
            catch (Exception ex)
            {
                Log.Error($"[The Second Seat] TTS configuration failed: {ex.Message}");
            }
        }
        
        public static async void TestConnection(TheSecondSeatSettings settings)
        {
            try
            {
                Messages.Message("TSS_Settings_Testing".Translate(), MessageTypeDefOf.NeutralEvent);
                
                var success = await LLM.LLMService.Instance.TestConnectionAsync();
                
                if (success)
                {
                    Messages.Message("TSS_Settings_TestSuccess".Translate(), MessageTypeDefOf.PositiveEvent);
                }
                else
                {
                    Messages.Message("TSS_Settings_TestFailed".Translate(), MessageTypeDefOf.NegativeEvent);
                }
            }
            catch (Exception ex)
            {
                Messages.Message($"Connection test failed: {ex.Message}", MessageTypeDefOf.NegativeEvent);
            }
        }
        
        public static async void TestTTS(TheSecondSeatSettings settings)
        {
            try
            {
                Messages.Message("Testing TTS...", MessageTypeDefOf.NeutralEvent);
                
                string testText = "Hello, this is a voice test.";
                string? filePath = await TTS.TTSService.Instance.SpeakAsync(testText);
                
                if (!string.IsNullOrEmpty(filePath))
                {
                    Messages.Message("TTS test successful!", MessageTypeDefOf.PositiveEvent);
                }
                else
                {
                    Messages.Message("TTS test failed", MessageTypeDefOf.NegativeEvent);
                }
            }
            catch (Exception ex)
            {
                Messages.Message($"TTS test failed: {ex.Message}", MessageTypeDefOf.NegativeEvent);
            }
        }
        
        public static void ShowVoiceSelectionMenu(TheSecondSeatSettings settings)
        {
            var voices = TTS.TTSService.GetAvailableVoices();
            var options = new System.Collections.Generic.List<FloatMenuOption>();

            foreach (var voice in voices)
            {
                string voiceCopy = voice;
                options.Add(new FloatMenuOption(voice, () => {
                    settings.ttsVoice = voiceCopy;
                }));
            }

            Find.WindowStack.Add(new FloatMenu(options));
        }
    }
}
```

---

## ?? 预期收益

### 方案 1（快速瘦身）
- **代码量**: 1100 行 → 950 行（减少 13.6%）
- **文件大小**: 8MB → 6.9MB（减少 13.75%）
- **工作量**: 10 分钟

### 方案 2（结构重构）
- **代码量**: 1100 行 → 600 行（减少 45.5%）
- **文件大小**: 8MB → 4.4MB（减少 45%）
- **工作量**: 30 分钟
- **文件数**: 1 个 → 3 个

---

## ?? 推荐行动

### 立即执行（5 分钟）
1. 删除 `DrawDifficultyCard()` 方法
2. 简化 `GetExampleGlobalPrompt()`
3. 编译验证

### 后续优化（可选）
1. 创建 `SettingsUI.cs` 和 `SettingsHelper.cs`
2. 移动相关代码
3. 更新调用点
4. 编译测试

---

## ?? 注意事项

1. **保持向后兼容**: 不要修改 `TheSecondSeatSettings` 数据类
2. **避免破坏现有功能**: 重构后务必测试所有设置页面
3. **逐步重构**: 先删除未使用代码，再考虑结构重构

---

## ?? 执行命令

创建清理脚本：

```powershell
# Clean-ModSettings-v1.6.65.ps1
```

---

? **建议**: 先执行"快速瘦身"方案，如果效果满意，再考虑"结构重构"。
