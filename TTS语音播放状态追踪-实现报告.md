# TTS 语音播放状态追踪 - 实现报告

## ? 实现完成

**实现时间：** 2025-12-09  
**版本：** v1.6.13  
**目的：** 为唇形同步动画系统提供语音播放状态追踪

---

## ?? 修改内容

### 1?? TTSService.cs - 添加播放状态属性

**文件：** `Source/TheSecondSeat/TTS/TTSService.cs`

#### ? 新增属性

```csharp
// ? 语音播放状态（用于唇形同步）
public bool IsSpeaking { get; private set; } = false;
```

#### ? 修改 AutoPlayAudioFile 方法

在音频播放前后设置 `IsSpeaking` 状态：

```csharp
private void AutoPlayAudioFile(string filePath)
{
    try
    {
        // ...existing validation code...
        
        byte[] audioData = File.ReadAllBytes(filePath);
        
        // ? 播放前：设置正在说话状态
        IsSpeaking = true;
        
        // 使用 Unity AudioSource 播放
        TTSAudioPlayer.Instance.PlayFromBytes(audioData, () => {
            // ? 播放结束：清除正在说话状态
            IsSpeaking = false;
            Log.Message("[TTSService] Audio playback finished");
        });
        
        Log.Message($"[TTSService] Playing audio via Unity AudioSource: {filePath}");
    }
    catch (Exception ex)
    {
        Log.Warning($"[TTSService] Failed to auto-play audio: {ex.Message}");
        // ? 异常时也要重置状态
        IsSpeaking = false;
    }
}
```

---

### 2?? TTSAudioPlayer.cs - 支持播放完成回调

**文件：** `Source/TheSecondSeat/TTS/TTSAudioPlayer.cs`

#### ? 修改 PlayFromBytes 方法

添加 `onComplete` 回调参数：

```csharp
/// <summary>
/// 从字节数组播放音频
/// </summary>
/// <param name="audioData">音频数据</param>
/// <param name="onComplete">播放完成回调（可选）</param>
public void PlayFromBytes(byte[] audioData, Action? onComplete = null)
{
    if (audioData == null || audioData.Length == 0)
    {
        Log.Warning("[TTSAudioPlayer] Audio data is empty");
        onComplete?.Invoke();  // ? 失败时也要调用回调
        return;
    }

    try
    {
        string tempFilePath = SaveToTempFile(audioData);
        
        if (string.IsNullOrEmpty(tempFilePath))
        {
            Log.Error("[TTSAudioPlayer] Failed to save temp file");
            onComplete?.Invoke();  // ? 失败时也要调用回调
            return;
        }

        // 使用协程加载和播放
        StartCoroutine(LoadAndPlayCoroutine(tempFilePath, onComplete));
    }
    catch (Exception ex)
    {
        Log.Error($"[TTSAudioPlayer] Error: {ex.Message}");
        onComplete?.Invoke();  // ? 异常时也要调用回调
    }
}
```

#### ? 修改 LoadAndPlayCoroutine 方法

在播放完成时调用回调：

```csharp
private IEnumerator LoadAndPlayCoroutine(string filePath, Action? onComplete = null)
{
    string fileUri = "file://" + filePath;
    
    using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip(fileUri, AudioType.WAV))
    {
        yield return www.SendWebRequest();

        if (www.result != UnityWebRequest.Result.Success)
        {
            Log.Error($"[TTSAudioPlayer] Failed to load audio: {www.error}");
            onComplete?.Invoke();  // ? 失败时调用回调
            yield break;
        }

        AudioClip clip = DownloadHandlerAudioClip.GetContent(www);
        
        if (clip == null)
        {
            Log.Error("[TTSAudioPlayer] AudioClip is null");
            onComplete?.Invoke();  // ? 失败时调用回调
            yield break;
        }

        // ...create AudioSource and play...
        
        currentAudioSource.Play();
        Log.Message("[TTSAudioPlayer] Playing audio...");

        // 等待播放完成
        yield return new WaitForSeconds(clip.length);

        // 清理
        Destroy(tempGO);
        currentAudioSource = null;
        
        Log.Message("[TTSAudioPlayer] Playback finished");

        // ? 播放完成：调用回调
        onComplete?.Invoke();

        // 删除临时文件
        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
        catch (Exception ex)
        {
            Log.Warning($"[TTSAudioPlayer] Failed to delete temp file: {ex.Message}");
        }
    }
}
```

---

### 3?? NarratorPersonaDef.cs - 添加动画纹理路径

**文件：** `Source/TheSecondSeat/PersonaGeneration/NarratorPersonaDef.cs`

#### ? 新增字段

```csharp
// === 立绘相关 ===
public string portraitPath = "";
public bool useCustomPortrait = false;
public string customPortraitPath = "";

// ? 动画系统纹理路径
public string portraitPathBlink = "";      // 闭眼纹理路径
public string portraitPathSpeaking = "";   // 张嘴纹理路径
```

---

## ?? 功能说明

### 播放状态追踪流程

```
┌──────────────────────────────────────────────────┐
│ 1. TTSService.SpeakAsync()                       │
│    └─> 生成音频 → AutoPlayAudioFile()           │
└──────────────────────────────────────────────────┘
                    ↓
┌──────────────────────────────────────────────────┐
│ 2. AutoPlayAudioFile()                           │
│    ├─> IsSpeaking = true  ← 开始播放            │
│    └─> TTSAudioPlayer.PlayFromBytes(data, cb)   │
└──────────────────────────────────────────────────┘
                    ↓
┌──────────────────────────────────────────────────┐
│ 3. TTSAudioPlayer.LoadAndPlayCoroutine()        │
│    ├─> 加载音频                                  │
│    ├─> 播放音频                                  │
│    ├─> yield WaitForSeconds(clip.length)        │
│    └─> onComplete?.Invoke()  ← 播放完成回调     │
└──────────────────────────────────────────────────┘
                    ↓
┌──────────────────────────────────────────────────┐
│ 4. 回调执行                                       │
│    └─> IsSpeaking = false  ← 结束播放           │
└──────────────────────────────────────────────────┘
```

---

## ?? 使用示例

### 示例 1: 检查是否正在说话

```csharp
// 在动画系统中检查播放状态
public void UpdateAnimation()
{
    if (TTSService.Instance.IsSpeaking)
    {
        // 显示张嘴纹理
        ShowSpeakingAnimation();
    }
    else
    {
        // 显示默认纹理
        ShowDefaultAnimation();
    }
}
```

### 示例 2: 唇形同步动画

```csharp
// 在立绘动画系统中
public class AvatarAnimator
{
    private Texture2D baseTexture;
    private Texture2D blinkTexture;
    private Texture2D speakingTexture;
    
    public void Update()
    {
        Texture2D currentTexture = baseTexture;
        
        // 优先检查是否正在说话
        if (TTSService.Instance.IsSpeaking)
        {
            currentTexture = speakingTexture;  // 张嘴
        }
        else if (IsBlinking())
        {
            currentTexture = blinkTexture;  // 闭眼
        }
        
        DisplayTexture(currentTexture);
    }
    
    private bool IsBlinking()
    {
        // 眨眼逻辑（每3-5秒随机眨眼）
        return UnityEngine.Random.value < 0.1f;
    }
}
```

---

## ?? XML 配置示例

### 配置动画纹理路径

```xml
<NarratorPersonaDef>
    <defName>Sideria_Default</defName>
    <narratorName>希德莉娅</narratorName>
    
    <!-- 基础立绘 -->
    <portraitPath>UI/Narrators/9x16/Sideria/base</portraitPath>
    
    <!-- ? 动画纹理路径 -->
    <portraitPathBlink>UI/Narrators/9x16/Sideria/blink</portraitPathBlink>
    <portraitPathSpeaking>UI/Narrators/9x16/Sideria/speaking</portraitPathSpeaking>
</NarratorPersonaDef>
```

---

## ?? 注意事项

### 1. 状态重置保障

在所有可能的退出路径都设置了 `IsSpeaking = false`：
- ? 播放完成后
- ? 播放失败时
- ? 异常发生时

### 2. 回调机制

`onComplete` 回调确保：
- ? 播放成功完成时调用
- ? 播放失败时也调用
- ? 异常时调用
- ? 所有路径都会触发回调

### 3. 线程安全

- `IsSpeaking` 在主线程中读写
- 协程在 Unity 主线程执行
- 无需额外的线程同步

---

## ?? 下一步

### 建议实现的功能

1. **唇形同步动画系统**
   - 在 `AvatarLoader` 中检查 `TTSService.Instance.IsSpeaking`
   - 根据状态切换纹理（base → speaking → base）

2. **眨眼动画**
   - 随机触发眨眼（每3-5秒）
   - 眨眼优先级低于说话动画

3. **动画状态机**
   ```csharp
   enum AvatarState
   {
       Idle,       // 默认状态
       Blinking,   // 眨眼
       Speaking    // 说话（最高优先级）
   }
   ```

---

## ?? 优点

| 优点 | 说明 |
|------|------|
| ? **简单可靠** | 单一状态变量，易于检查 |
| ? **状态安全** | 所有退出路径都重置状态 |
| ? **回调机制** | 播放完成自动通知 |
| ? **易于扩展** | 可以添加更多动画状态 |
| ? **向后兼容** | `onComplete` 参数可选 |

---

## ?? 验证清单

### 编译验证

```bash
# 编译项目
dotnet build Source/TheSecondSeat/TheSecondSeat.csproj
```

### 运行时验证

1. ? 启动游戏
2. ? 触发 TTS 播放
3. ? 观察日志输出：
   ```
   [TTSService] Playing audio via Unity AudioSource: ...
   [TTSAudioPlayer] Playing audio...
   [TTSAudioPlayer] Playback finished
   [TTSService] Audio playback finished
   ```
4. ? 在外部系统中检查 `TTSService.Instance.IsSpeaking`

---

## ?? 总结

### ? 已完成

1. ? 添加 `IsSpeaking` 属性到 `TTSService`
2. ? 在播放前设置 `IsSpeaking = true`
3. ? 在播放后设置 `IsSpeaking = false`
4. ? 添加播放完成回调机制
5. ? 在所有退出路径重置状态
6. ? 添加动画纹理路径字段

### ?? 效果

- 外部系统可以通过 `TTSService.Instance.IsSpeaking` 检查播放状态
- 播放完成自动重置状态
- 支持唇形同步动画实现
- 向后兼容（回调参数可选）

---

**实现完成！** ?  
**版本：** v1.6.13  
**状态：** 可以开始实现唇形同步动画系统

_The Second Seat Mod Team_
