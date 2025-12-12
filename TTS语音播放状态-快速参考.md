# TTS 语音播放状态 - 快速参考

## ?? 核心功能

**检查是否正在说话：**

```csharp
bool isSpeaking = TTSService.Instance.IsSpeaking;
```

---

## ?? 使用场景

### 1?? 唇形同步动画

```csharp
public void UpdateAvatar()
{
    Texture2D texture;
    
    if (TTSService.Instance.IsSpeaking)
    {
        texture = speakingTexture;  // 张嘴
    }
    else
    {
        texture = neutralTexture;   // 默认
    }
    
    GUI.DrawTexture(rect, texture);
}
```

### 2?? 动画状态机

```csharp
enum AvatarState { Idle, Blinking, Speaking }

AvatarState GetCurrentState()
{
    if (TTSService.Instance.IsSpeaking)
        return AvatarState.Speaking;  // 最高优先级
    
    if (IsBlinkingNow())
        return AvatarState.Blinking;
    
    return AvatarState.Idle;
}
```

### 3?? UI 显示

```csharp
// 在聊天窗口显示"正在说话"指示器
if (TTSService.Instance.IsSpeaking)
{
    DrawSpeakingIndicator();  // 显示动画波形
}
```

---

## ?? XML 配置

### 定义动画纹理路径

```xml
<NarratorPersonaDef>
    <defName>Sideria_Default</defName>
    
    <!-- 基础立绘 -->
    <portraitPath>UI/Narrators/9x16/Sideria/base</portraitPath>
    
    <!-- 动画纹理 -->
    <portraitPathBlink>UI/Narrators/9x16/Sideria/blink</portraitPathBlink>
    <portraitPathSpeaking>UI/Narrators/9x16/Sideria/speaking</portraitPathSpeaking>
</NarratorPersonaDef>
```

---

## ?? 状态流转

```
[ 静默 IsSpeaking = false ]
           ↓ (播放开始)
[ 说话 IsSpeaking = true  ]
           ↓ (播放完成)
[ 静默 IsSpeaking = false ]
```

---

## ?? 注意事项

| 项目 | 说明 |
|------|------|
| **线程安全** | ? 在主线程读写 |
| **自动重置** | ? 播放完成自动设为 false |
| **异常处理** | ? 异常时也会重置状态 |
| **向后兼容** | ? 不影响现有代码 |

---

## ?? 实现优先级

### 推荐实现顺序

1. **基础唇形同步** - 说话时显示张嘴纹理
2. **随机眨眼动画** - 每3-5秒随机眨眼
3. **动画状态机** - 优先级：说话 > 眨眼 > 静止
4. **UI 指示器** - 显示"正在说话"波形动画

---

## ?? 示例代码

### 完整的动画系统

```csharp
public class AvatarAnimator
{
    private Texture2D baseTexture;
    private Texture2D blinkTexture;
    private Texture2D speakingTexture;
    
    private float lastBlinkTime = 0f;
    private float blinkDuration = 0.2f;
    private bool isBlinking = false;
    
    public void Update()
    {
        Texture2D currentTexture = GetCurrentTexture();
        DrawAvatar(currentTexture);
    }
    
    private Texture2D GetCurrentTexture()
    {
        // 优先级 1: 正在说话
        if (TTSService.Instance.IsSpeaking)
        {
            return speakingTexture;
        }
        
        // 优先级 2: 眨眼动画
        UpdateBlinkState();
        if (isBlinking)
        {
            return blinkTexture;
        }
        
        // 优先级 3: 默认状态
        return baseTexture;
    }
    
    private void UpdateBlinkState()
    {
        float currentTime = Time.realtimeSinceStartup;
        
        if (isBlinking)
        {
            // 眨眼持续 0.2 秒
            if (currentTime - lastBlinkTime > blinkDuration)
            {
                isBlinking = false;
            }
        }
        else
        {
            // 每 3-5 秒随机眨眼
            float timeSinceLastBlink = currentTime - lastBlinkTime;
            float nextBlinkTime = UnityEngine.Random.Range(3f, 5f);
            
            if (timeSinceLastBlink > nextBlinkTime)
            {
                isBlinking = true;
                lastBlinkTime = currentTime;
            }
        }
    }
    
    private void DrawAvatar(Texture2D texture)
    {
        Rect avatarRect = new Rect(10, 10, 128, 128);
        GUI.DrawTexture(avatarRect, texture, ScaleMode.ScaleToFit);
    }
}
```

---

## ?? 调试

### 检查状态

```csharp
if (Prefs.DevMode)
{
    string status = TTSService.Instance.IsSpeaking ? "说话中" : "静默";
    Log.Message($"[AvatarAnimator] 当前状态: {status}");
}
```

### 日志输出

```
[TTSService] Playing audio via Unity AudioSource: ...
[TTSAudioPlayer] Playing audio...
[TTSAudioPlayer] Playback finished
[TTSService] Audio playback finished
```

---

## ?? 性能

| 指标 | 值 |
|------|-----|
| CPU 占用 | 极低（仅布尔值检查） |
| 内存占用 | 1 字节（bool） |
| 响应延迟 | 即时（主线程） |

---

**快速参考完成！** ?  
**版本：** v1.6.13

_The Second Seat Mod Team_
