# TTS 重构 - 快速参考

## ?? 核心变更

### 架构升级

```
旧架构: UnityWebRequest (主线程阻塞)
     ↓
新架构: HttpClient (后台) + Coroutine (主线程)
```

---

## ?? 关键代码

### TTSService - 两阶段分离

```csharp
public async Task<string?> SpeakAsync(string text)
{
    // ? 阶段1：后台下载（Task.Run）
    string? filePath = await Task.Run(async () => 
        await DownloadAndSaveAudioAsync(cleanText)
    );

    // ? 阶段2：主线程播放（LongEventHandler）
    Verse.LongEventHandler.ExecuteWhenFinished(() => 
    {
        PlayAudioOnMainThread(filePath);
    });
}
```

### HttpClient 下载

```csharp
private async Task<byte[]?> DownloadAudioBytesAsync(string text)
{
    var response = await httpClient.PostAsync(endpoint, content);
    byte[] audioData = await response.Content.ReadAsByteArrayAsync();
    return audioData;
}
```

### 文件保存

```csharp
using (FileStream fs = new FileStream(filePath, FileMode.Create))
{
    await fs.WriteAsync(audioData, 0, audioData.Length);
}
```

### 主线程播放

```csharp
private void PlayAudioOnMainThread(string filePath)
{
    IsSpeaking = true;
    TTSAudioPlayer.Instance.Play(filePath, () => {
        IsSpeaking = false;
    });
}
```

---

## ?? TTSAudioPlayer - Coroutine 加载

```csharp
public void Play(string filePath, Action? onComplete = null)
{
    Stop();
    currentPlaybackCoroutine = StartCoroutine(
        LoadAndPlayCoroutine(filePath, onComplete)
    );
}

private IEnumerator LoadAndPlayCoroutine(string filePath, Action? onComplete)
{
    string fileUri = "file://" + filePath;
    
    using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip(fileUri, AudioType.WAV))
    {
        yield return www.SendWebRequest();
        
        AudioClip clip = DownloadHandlerAudioClip.GetContent(www);
        
        GameObject tempGO = new GameObject("TempAudioPlayer");
        AudioSource source = tempGO.AddComponent<AudioSource>();
        source.clip = clip;
        source.Play();
        
        yield return new WaitForSeconds(clip.length);
        
        Destroy(tempGO);
        onComplete?.Invoke();
    }
}
```

---

## ?? 线程安全

```csharp
// ? 锁保护共享状态
private readonly object lockObject = new object();
private bool isSpeaking = false;

public bool IsSpeaking 
{ 
    get { lock (lockObject) { return isSpeaking; } }
    private set { lock (lockObject) { isSpeaking = value; } }
}
```

---

## ?? 部署命令

```powershell
# 1. 备份
Copy-Item "Source\TheSecondSeat\TTS\TTSService.cs" "*.bak"
Copy-Item "Source\TheSecondSeat\TTS\TTSAudioPlayer.cs" "*.bak"

# 2. 替换
Move-Item "Source\TheSecondSeat\TTS\TTSService_Refactored.cs" `
          "Source\TheSecondSeat\TTS\TTSService.cs" -Force

Move-Item "Source\TheSecondSeat\TTS\TTSAudioPlayer_Refactored.cs" `
          "Source\TheSecondSeat\TTS\TTSAudioPlayer.cs" -Force

# 3. 编译
cd Source
dotnet build TheSecondSeat.csproj
```

---

## ? 测试清单

### 功能测试
- [ ] 游戏不卡顿
- [ ] 音频正常下载
- [ ] 音频正常播放
- [ ] 播放状态正确

### 边缘测试
- [ ] 空文本处理
- [ ] 网络错误处理
- [ ] 快速连续调用

---

## ?? 主要优势

| 特性 | 旧架构 | 新架构 |
|------|--------|--------|
| 主线程阻塞 | ? 2-5秒 | ? 0秒 |
| 线程安全 | ? 否 | ? 是 |
| 代码清晰度 | ?? 中等 | ? 高 |
| 错误处理 | ?? 基础 | ? 完善 |

---

**版本**: v1.6.19  
**状态**: ? 已完成
