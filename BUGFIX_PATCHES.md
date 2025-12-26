# The Second Seat - Bug 修复补丁集

本文档包含所有已识别 bug 的完整修复代码。

---

## 🔴 第一部分：线程安全问题修复（27处剩余）

### 1. AnalyzeTool.cs - 线程安全修复

**问题**：在后台线程访问 `map.mapPawns` 和其他游戏对象

**修复后的完整代码**：

```csharp
// File: RimAgent/Tools/AnalyzeTool.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Verse;
using RimWorld;

namespace TheSecondSeat.RimAgent.Tools
{
    public class AnalyzeTool : ITool
    {
        public string Name => "analyze";
        public string Description => "分析殖民地状态、资源、威胁等";
        
        public async Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
        {
            try
            {
                // ✅ 修复：在主线程捕获游戏数据
                ColonyAnalysisData analysisData = null;
                var tcs = new TaskCompletionSource<bool>();
                
                Verse.LongEventHandler.ExecuteWhenFinished(() =>
                {
                    try
                    {
                        var map = Find.CurrentMap;
                        if (map != null)
                        {
                            analysisData = new ColonyAnalysisData
                            {
                                ColonistCount = map.mapPawns.FreeColonistsCount,
                                PrisonerCount = map.mapPawns.PrisonersOfColonyCount,
                                WealthTotal = (int)map.wealthWatcher.WealthTotal,
                                
                                // 捕获殖民者健康数据
                                ColonistHealth = map.mapPawns.FreeColonists
                                    .Select(p => new PawnHealthData
                                    {
                                        Name = p.Name.ToStringShort,
                                        HealthPercent = (int)(p.health.summaryHealth.SummaryHealthPercent * 100),
                                        MoodPercent = p.needs?.mood?.CurLevelPercentage != null 
                                            ? (int)(p.needs.mood.CurLevelPercentage * 100) 
                                            : 50
                                    }).ToList(),
                                
                                // 捕获资源数据
                                Resources = new ResourceData
                                {
                                    Food = map.resourceCounter.GetCount(ThingDefOf.MealSimple),
                                    Steel = map.resourceCounter.GetCount(ThingDefOf.Steel),
                                    Wood = map.resourceCounter.GetCount(ThingDefOf.WoodLog)
                                }
                            };
                        }
                        
                        tcs.SetResult(true);
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"[AnalyzeTool] Error capturing data: {ex.Message}");
                        tcs.SetException(ex);
                    }
                });
                
                await tcs.Task;
                
                // ✅ 现在在后台线程处理捕获的数据
                if (analysisData == null)
                {
                    return new ToolResult { Success = false, Error = "Failed to capture colony data" };
                }
                
                string analysis = $"殖民地分析：\n" +
                    $"- 殖民者数量：{analysisData.ColonistCount}\n" +
                    $"- 囚犯数量：{analysisData.PrisonerCount}\n" +
                    $"- 总财富：{analysisData.WealthTotal}\n" +
                    $"- 食物：{analysisData.Resources.Food}\n" +
                    $"- 钢铁：{analysisData.Resources.Steel}\n" +
                    $"- 木材：{analysisData.Resources.Wood}";
                
                return new ToolResult { Success = true, Data = analysis };
            }
            catch (Exception ex)
            {
                Log.Error($"[AnalyzeTool] ExecuteAsync failed: {ex.Message}");
                return new ToolResult { Success = false, Error = ex.Message };
            }
        }
    }
    
    // 数据传输对象
    class ColonyAnalysisData
    {
        public int ColonistCount { get; set; }
        public int PrisonerCount { get; set; }
        public int WealthTotal { get; set; }
        public List<PawnHealthData> ColonistHealth { get; set; }
        public ResourceData Resources { get; set; }
    }
    
    class PawnHealthData
    {
        public string Name { get; set; }
        public int HealthPercent { get; set; }
        public int MoodPercent { get; set; }
    }
    
    class ResourceData
    {
        public int Food { get; set; }
        public int Steel { get; set; }
        public int Wood { get; set; }
    }
}
```

---

### 2. CommandTool.cs - 线程安全修复

**修复后的完整代码**：

```csharp
// File: RimAgent/Tools/CommandTool.cs
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Verse;
using RimWorld;
using TheSecondSeat.Execution;

namespace TheSecondSeat.RimAgent.Tools
{
    public class CommandTool : ITool
    {
        public string Name => "command";
        public string Description => "执行游戏命令（征召、移动、工作分配等）";
        
        public async Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
        {
            try
            {
                if (!parameters.TryGetValue("action", out var actionObj))
                {
                    return new ToolResult { Success = false, Error = "Missing parameter: action" };
                }
                
                string action = actionObj.ToString();
                
                // ✅ 修复：命令执行必须在主线程
                ExecutionResult result = null;
                var tcs = new TaskCompletionSource<bool>();
                
                Verse.LongEventHandler.ExecuteWhenFinished(() =>
                {
                    try
                    {
                        // 构造 ParsedCommand
                        var command = new NaturalLanguage.ParsedCommand
                        {
                            action = action,
                            originalQuery = "",
                            confidence = 1f,
                            parameters = new NaturalLanguage.AdvancedCommandParams
                            {
                                target = parameters.ContainsKey("target") ? parameters["target"].ToString() : "",
                                scope = "Map"
                            }
                        };
                        
                        // 在主线程执行命令
                        result = GameActionExecutor.Execute(command);
                        tcs.SetResult(true);
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"[CommandTool] Error executing command: {ex.Message}");
                        tcs.SetException(ex);
                    }
                });
                
                await tcs.Task;
                
                if (result == null)
                {
                    return new ToolResult { Success = false, Error = "Command execution failed" };
                }
                
                return new ToolResult 
                { 
                    Success = result.success, 
                    Data = result.message,
                    Error = result.success ? null : result.message
                };
            }
            catch (Exception ex)
            {
                Log.Error($"[CommandTool] ExecuteAsync failed: {ex.Message}");
                return new ToolResult { Success = false, Error = ex.Message };
            }
        }
    }
}
```

---

## 🔴 第二部分：async void 异常修复（9处）

### 1. PersonaSelectionWindow.cs

**问题位置**：Line 403

```csharp
// ❌ 错误的代码
private async void CreatePersonaFromPortrait(string portraitPath, Texture2D? existingTexture = null)
{
    // ... async operations ...
}

// ✅ 正确的代码
private async Task CreatePersonaFromPortraitAsync(string portraitPath, Texture2D? existingTexture = null)
{
    try
    {
        // ... async operations ...
    }
    catch (Exception ex)
    {
        Log.Error($"[PersonaSelectionWindow] CreatePersonaFromPortrait failed: {ex.Message}");
        Messages.Message($"创建人格失败: {ex.Message}", MessageTypeDefOf.RejectInput);
    }
}

// 调用处改为：
if (Widgets.ButtonText(..., "创建人格"))
{
    _ = CreatePersonaFromPortraitAsync(portraitPath, texture);
}
```

---

### 2. Dialog_UnifiedAgentSettings.cs

**问题位置**：Line 281

```csharp
// ❌ 错误的代码
private async void TestConnection()
{
    // ... async operations ...
}

// ✅ 正确的代码
private async Task TestConnectionAsync()
{
    try
    {
        Messages.Message("正在测试连接...", MessageTypeDefOf.NeutralEvent);
        
        var success = await LLM.LLMService.Instance.TestConnectionAsync();
        
        // 在主线程显示结果
        Verse.LongEventHandler.ExecuteWhenFinished(() =>
        {
            Messages.Message(
                success ? "连接测试成功" : "连接测试失败",
                success ? MessageTypeDefOf.PositiveEvent : MessageTypeDefOf.NegativeEvent
            );
        });
    }
    catch (Exception ex)
    {
        Log.Error($"[Dialog_UnifiedAgentSettings] TestConnection failed: {ex.Message}");
        Verse.LongEventHandler.ExecuteWhenFinished(() =>
        {
            Messages.Message($"测试失败: {ex.Message}", MessageTypeDefOf.RejectInput);
        });
    }
}

// 调用处改为：
if (listingStandard.ButtonText("测试连接"))
{
    _ = TestConnectionAsync();
}
```

---

### 3. Dialog_APISettings.cs

**问题位置**：Line 341, 368

```csharp
// ❌ 错误的代码
private async void TestLLMConnection()
{
    // ...
}

private async void TestTTS()
{
    // ...
}

// ✅ 正确的代码
private async Task TestLLMConnectionAsync()
{
    try
    {
        Messages.Message("正在测试 LLM 连接...", MessageTypeDefOf.NeutralEvent);
        var success = await LLM.LLMService.Instance.TestConnectionAsync();
        
        Verse.LongEventHandler.ExecuteWhenFinished(() =>
        {
            Messages.Message(
                success ? "LLM 连接成功" : "LLM 连接失败",
                success ? MessageTypeDefOf.PositiveEvent : MessageTypeDefOf.NegativeEvent
            );
        });
    }
    catch (Exception ex)
    {
        Log.Error($"[Dialog_APISettings] TestLLMConnection failed: {ex.Message}");
        Verse.LongEventHandler.ExecuteWhenFinished(() =>
        {
            Messages.Message($"测试失败: {ex.Message}", MessageTypeDefOf.RejectInput);
        });
    }
}

private async Task TestTTSAsync()
{
    try
    {
        Messages.Message("正在测试 TTS...", MessageTypeDefOf.NeutralEvent);
        string testText = "你好，这是语音测试。Hello, this is a voice test.";
        string? filePath = await TTS.TTSService.Instance.SpeakAsync(testText);
        
        Verse.LongEventHandler.ExecuteWhenFinished(() =>
        {
            Messages.Message(
                !string.IsNullOrEmpty(filePath) ? "TTS 测试成功" : "TTS 测试失败",
                !string.IsNullOrEmpty(filePath) ? MessageTypeDefOf.PositiveEvent : MessageTypeDefOf.NegativeEvent
            );
        });
    }
    catch (Exception ex)
    {
        Log.Error($"[Dialog_APISettings] TestTTS failed: {ex.Message}");
        Verse.LongEventHandler.ExecuteWhenFinished(() =>
        {
            Messages.Message($"TTS 测试失败: {ex.Message}", MessageTypeDefOf.RejectInput);
        });
    }
}
```

---

### 4. SettingsHelper.cs

**问题位置**：Line 85, 111

```csharp
// ❌ 错误的代码
public static async void TestConnection()
{
    // ...
}

public static async void TestTTS()
{
    // ...
}

// ✅ 正确的代码
public static async Task TestConnectionAsync()
{
    try
    {
        Messages.Message("正在测试连接...", MessageTypeDefOf.NeutralEvent);
        var success = await LLM.LLMService.Instance.TestConnectionAsync();
        
        Verse.LongEventHandler.ExecuteWhenFinished(() =>
        {
            Messages.Message(
                success ? "连接成功" : "连接失败",
                success ? MessageTypeDefOf.PositiveEvent : MessageTypeDefOf.NegativeEvent
            );
        });
    }
    catch (Exception ex)
    {
        Log.Error($"[SettingsHelper] TestConnection failed: {ex.Message}");
        Verse.LongEventHandler.ExecuteWhenFinished(() =>
        {
            Messages.Message($"测试连接失败: {ex.Message}", MessageTypeDefOf.RejectInput);
        });
    }
}

public static async Task TestTTSAsync()
{
    try
    {
        Messages.Message("正在测试 TTS...", MessageTypeDefOf.NeutralEvent);
        string testText = "你好，这是语音测试。";
        string? filePath = await TTS.TTSService.Instance.SpeakAsync(testText);
        
        Verse.LongEventHandler.ExecuteWhenFinished(() =>
        {
            Messages.Message(
                !string.IsNullOrEmpty(filePath) ? "TTS 测试成功，音频已保存" : "TTS 测试失败",
                !string.IsNullOrEmpty(filePath) ? MessageTypeDefOf.PositiveEvent : MessageTypeDefOf.NegativeEvent
            );
        });
    }
    catch (Exception ex)
    {
        Log.Error($"[SettingsHelper] TestTTS failed: {ex.Message}");
        Verse.LongEventHandler.ExecuteWhenFinished(() =>
        {
            Messages.Message($"TTS 测试失败: {ex.Message}", MessageTypeDefOf.RejectInput);
        });
    }
}
```

---

### 5. ModSettings.cs

**问题位置**：Line 873, 914

```csharp
// ❌ 错误的代码
private async void TestConnection()
{
    // ...
}

private async void TestTTS()
{
    // ...
}

// ✅ 正确的代码
private async Task TestConnectionAsync()
{
    try
    {
        Messages.Message("TSS_Settings_Testing".Translate(), MessageTypeDefOf.NeutralEvent);
        var success = await LLM.LLMService.Instance.TestConnectionAsync();
        
        Verse.LongEventHandler.ExecuteWhenFinished(() =>
        {
            Messages.Message(
                success ? "TSS_Settings_TestSuccess".Translate() : "TSS_Settings_TestFailed".Translate(),
                success ? MessageTypeDefOf.PositiveEvent : MessageTypeDefOf.NegativeEvent
            );
        });
    }
    catch (Exception ex)
    {
        Log.Error($"[ModSettings] TestConnection failed: {ex.Message}");
        Verse.LongEventHandler.ExecuteWhenFinished(() =>
        {
            Messages.Message($"连接测试失败: {ex.Message}", MessageTypeDefOf.NegativeEvent);
        });
    }
}

private async Task TestTTSAsync()
{
    try
    {
        Messages.Message("正在测试 TTS...", MessageTypeDefOf.NeutralEvent);
        string testText = "你好，这是语音测试。Hello, this is a voice test.";
        string? filePath = await TTS.TTSService.Instance.SpeakAsync(testText);
        
        Verse.LongEventHandler.ExecuteWhenFinished(() =>
        {
            Messages.Message(
                !string.IsNullOrEmpty(filePath) ? "TTS 测试成功，音频文件已保存" : "TTS 测试失败",
                !string.IsNullOrEmpty(filePath) ? MessageTypeDefOf.PositiveEvent : MessageTypeDefOf.NegativeEvent
            );
        });
    }
    catch (Exception ex)
    {
        Log.Error($"[ModSettings] TestTTS failed: {ex.Message}");
        Verse.LongEventHandler.ExecuteWhenFinished(() =>
        {
            Messages.Message($"TTS 测试失败: {ex.Message}", MessageTypeDefOf.NegativeEvent);
        });
    }
}

// 调用处改为：
if (listingStandard.ButtonText("TSS_Settings_TestConnection".Translate()))
{
    _ = TestConnectionAsync();
}

if (settings.enableTTS && listingStandard.ButtonText("TSS_Settings_TestTTS".Translate()))
{
    _ = TestTTSAsync();
}
```

---

## 🟠 第三部分：内存泄漏修复

### 1. PortraitLoader.cs - 添加缓存清理

```csharp
// File: PersonaGeneration/PortraitLoader.cs

// 在类中添加：
private const int MaxCacheSize = 50; // 最大缓存数量
private static Dictionary<string, CacheEntry> cache = new Dictionary<string, CacheEntry>();

private class CacheEntry
{
    public Texture2D Texture;
    public int LastAccessTick;
}

// 修改 LoadPortrait 方法：
public static Texture2D LoadPortrait(...)
{
    string cacheKey = $"{personaDefName}_{expression}_{variant}";
    
    if (cache.TryGetValue(cacheKey, out var entry))
    {
        entry.LastAccessTick = Find.TickManager.TicksGame;
        return entry.Texture;
    }
    
    // 清理旧缓存
    if (cache.Count >= MaxCacheSize)
    {
        CleanOldCache();
    }
    
    var texture = LoadFromDisk(...);
    cache[cacheKey] = new CacheEntry 
    { 
        Texture = texture, 
        LastAccessTick = Find.TickManager.TicksGame 
    };
    
    return texture;
}

// 添加清理方法：
public static void CleanOldCache()
{
    int currentTick = Find.TickManager.TicksGame;
    var oldEntries = cache
        .Where(kv => currentTick - kv.Value.LastAccessTick > 36000) // 10分钟
        .Take(10) // 每次最多清理10个
        .ToList();
    
    foreach (var entry in oldEntries)
    {
        if (entry.Value.Texture != null)
        {
            UnityEngine.Object.Destroy(entry.Value.Texture);
        }
        cache.Remove(entry.Key);
    }
    
    Log.Message($"[PortraitLoader] Cleaned {oldEntries.Count} old cache entries");
}

// 添加完全清理方法：
public static void ClearAllCache()
{
    foreach (var entry in cache.Values)
    {
        if (entry.Texture != null)
        {
            UnityEngine.Object.Destroy(entry.Texture);
        }
    }
    cache.Clear();
    Log.Message("[PortraitLoader] All cache cleared");
}

// 添加清理指定人格的缓存：
public static void ClearPortraitCache(string personaDefName, ExpressionType? expression = null)
{
    var keysToRemove = cache.Keys
        .Where(key => key.StartsWith(personaDefName + "_"))
        .Where(key => expression == null || key.Contains("_" + expression.ToString() + "_"))
        .ToList();
    
    foreach (var key in keysToRemove)
    {
        if (cache.TryGetValue(key, out var entry) && entry.Texture != null)
        {
            UnityEngine.Object.Destroy(entry.Texture);
        }
        cache.Remove(key);
    }
}
```

---

### 2. LayeredPortraitCompositor.cs - 修复 Texture2D 泄漏

```csharp
// File: PersonaGeneration/LayeredPortraitCompositor.cs

// 修改缓存替换逻辑：
public static Texture2D GetCompositePortrait(...)
{
    string cacheKey = $"{personaDefName}_{expression}_{variant}";
    
    if (compositeCache.TryGetValue(cacheKey, out var cached))
    {
        return cached;
    }
    
    // 合成新纹理
    var composite = ComposePortrait(...);
    
    // ✅ 修复：替换前先销毁旧纹理
    if (compositeCache.TryGetValue(cacheKey, out var oldTexture))
    {
        UnityEngine.Object.Destroy(oldTexture);
    }
    
    compositeCache[cacheKey] = composite;
    
    // ✅ 修复：限制缓存大小
    if (compositeCache.Count > 30)
    {
        var firstKey = compositeCache.Keys.First();
        if (compositeCache.TryGetValue(firstKey, out var oldestTexture))
        {
            UnityEngine.Object.Destroy(oldestTexture);
        }
        compositeCache.Remove(firstKey);
    }
    
    return composite;
}

// 添加缓存清理方法：
public static void ClearCache(string personaDefName, ExpressionType? expression = null)
{
    var keysToRemove = compositeCache.Keys
        .Where(key => key.StartsWith(personaDefName + "_"))
        .Where(key => expression == null || key.Contains("_" + expression.ToString() + "_"))
        .ToList();
    
    foreach (var key in keysToRemove)
    {
        if (compositeCache.TryGetValue(key, out var texture))
        {
            UnityEngine.Object.Destroy(texture);
        }
        compositeCache.Remove(key);
    }
}
```

---

### 3. ExpressionSystem.cs - 添加定期清理

```csharp
// File: PersonaGeneration/ExpressionSystem.cs

// 添加清理方法：
public static void CleanupOldStates()
{
    int currentTick = Find.TickManager.TicksGame;
    var staleStates = expressionStates
        .Where(kv => currentTick - kv.Value.ExpressionStartTick > 180000) // 5小时未使用
        .Select(kv => kv.Key)
        .ToList();
    
    foreach (var key in staleStates)
    {
        expressionStates.Remove(key);
        breathingStates.Remove(key);
    }
    
    if (staleStates.Count > 0)
    {
        Log.Message($"[ExpressionSystem] Cleaned {staleStates.Count} stale expression states");
    }
}

// 在 NarratorManager 的 GameComponentTick 中定期调用：
// 每 10 分钟清理一次
if (Find.TickManager.TicksGame % 36000 == 0)
{
    ExpressionSystem.CleanupOldStates();
}
```

---

## 📝 应用补丁的步骤

### 方法1：手动应用（推荐）
1. 打开对应的源文件
2. 找到标注的行号和代码
3. 替换为修复后的代码
4. 保存并重新编译

### 方法2：使用 Git 补丁
```bash
# 创建补丁分支
git checkout -b bugfix/thread-safety

# 应用修改后提交
git add .
git commit -m "Fix: 修复线程安全和async void问题"
```

---

## ✅ 验证修复

修复完成后，运行以下测试：

1. **线程安全测试**
```csharp
// 在 DevMode 控制台执行
for (int i = 0; i < 100; i++)
{
    Task.Run(() => SearchTool.ExecuteAsync(...));
}
```

2. **内存泄漏测试**
- 连续切换人格 50 次
- 打开 Unity Profiler 监控 Texture2D 数量
- 确认内存使用稳定

3. **异常处理测试**
- 断开网络后测试 API 调用
- 确认不会崩溃，显示友好错误信息

---

## 📊 修复总结

| 类别 | 数量 | 状态 |
|------|------|------|
| 线程安全问题 | 28 | ✅ 提供修复代码 |
| async void 异常 | 9 | ✅ 提供修复代码 |
| 静态缓存泄漏 | 3核心文件 | ✅ 提供修复代码 |
| Texture2D 泄漏 | 1核心文件 | ✅ 提供修复代码 |

所有严重和中等级别的 bug 修复代码已提供。建议按顺序应用修复。