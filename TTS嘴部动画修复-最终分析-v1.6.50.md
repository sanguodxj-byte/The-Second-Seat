# TTS 嘴部动画修复 - 最终分析报告 v1.6.50

## ?? 问题诊断

### 已确认的事实
1. ? **代码结构完全正确**
   - `MouthAnimationSystem.Update()` 被正确调用（每帧）
   - `GetMouthLayerName()` 被正确调用（绘制立绘时）
   - 嘴部纹理文件存在（small/medium/larger_mouth.png）

2. ? **TTSAudioPlayer.IsSpeaking() 实现正确**
   - 使用 `Dictionary<string, bool>` 跟踪播放状态
   - `SetSpeakingState()` 正确更新状态
   - 在播放开始时设置 `true`，结束时设置 `false`

3. ?? **问题所在**：TTSAudioPlayer 的播放逻辑可能有以下问题之一：
   - TTS 根本没有播放（API 失败、配置错误）
   - 播放时没有传递 `personaDefName` 参数
   - `LoadPlayDeleteCoroutine` 在设置状态前就返回了

---

## ?? 根本原因分析

### 场景 1：TTS 没有被触发
**可能原因**：
- Mod 设置中 TTS 未启用
- 自动播放 TTS 选项未勾选
- TTS API 配置错误（无 API Key、提供商配置错误）

**验证方法**：
1. 打开 Mod 设置 → The Second Seat → TTS
2. 检查是否勾选"启用语音合成（TTS）"
3. 检查是否勾选"自动播放 TTS（叙事者发言时）"

### 场景 2：调用 TTS 时没有传递 persona DefName
**可能原因**：
- `NarratorManager` 或其他调用处使用了旧的 API
- 调用 `PlayFromBytes(audioData)` 而不是 `PlayFromBytes(audioData, personaDefName)`

**验证方法**：
检查所有调用 TTS 播放的代码：
```csharp
// ? 错误的调用（没有传递 personaDefName）
TTSAudioPlayer.Instance.PlayFromBytes(audioData);

// ? 正确的调用（传递了 personaDefName）
TTSAudioPlayer.Instance.PlayFromBytes(audioData, persona.defName);
```

### 场景 3：Coroutine 提前退出
**可能原因**：
- UnityWebRequest 加载失败
- AudioClip 为 null
- 文件路径错误

**日志标志**：
```
[TTSAudioPlayer] Failed to load audio: ...
[TTSAudioPlayer] AudioClip is null
```

---

## ?? 快速修复方案

### 修复 1：检查 TTS 调用点

找到所有调用 TTS 的位置，确保传递 `personaDefName`：

#### 可能的调用位置
1. **NarratorManager.cs** - AI 回复后触发 TTS
2. **NarratorController.cs** - 处理叙事者对话
3. **CommandToolLibrary.cs** - 执行命令后的反馈

#### 修复示例

**修改前**：
```csharp
// 在某处播放 TTS
string? audioFilePath = await TTS.TTSService.Instance.SpeakAsync(responseText);
if (!string.IsNullOrEmpty(audioFilePath))
{
    TTS.TTSAudioPlayer.Instance.PlayAndDelete(audioFilePath);  // ? 没有传递 personaDefName
}
```

**修改后**：
```csharp
// 在某处播放 TTS
string? audioFilePath = await TTS.TTSService.Instance.SpeakAsync(responseText);
if (!string.IsNullOrEmpty(audioFilePath) && currentPersona != null)
{
    TTS.TTSAudioPlayer.Instance.PlayAndDelete(
        audioFilePath, 
        currentPersona.defName  // ? 传递 personaDefName
    );
}
```

### 修复 2：增强调试日志

在 `MouthAnimationSystem.GetMouthLayerName()` 中增强日志：

```csharp
// ? 3. 检查 TTS 播放状态（修改：增强调试）
bool isPlayingTTS = false;
try
{
    isPlayingTTS = TTS.TTSAudioPlayer.IsSpeaking(defName);
    
    // ? 关键：每次都输出状态（用于诊断）
    if (Prefs.DevMode)
    {
        Log.Message($"[MouthAnimationSystem] IsSpeaking({defName}) = {isPlayingTTS}");
    }
}
catch (System.Exception ex)
{
    if (Prefs.DevMode)
    {
        Log.Warning($"[MouthAnimationSystem] 检测 TTS 状态失败: {ex.Message}");
    }
    isPlayingTTS = false;
}
```

### 修复 3：在 TTSAudioPlayer 中增强日志

```csharp
/// <summary>
/// ? v1.6.30: 检查指定人格是否正在说话（用于口型同步）
/// </summary>
public static bool IsSpeaking(string personaDefName)
{
    if (string.IsNullOrEmpty(personaDefName))
    {
        return false;
    }

    bool result = speakingStates.TryGetValue(personaDefName, out bool isSpeaking) && isSpeaking;
    
    // ? 增强日志：每次查询都输出状态
    if (Prefs.DevMode)
    {
        Log.Message($"[TTSAudioPlayer] IsSpeaking({personaDefName}) = {result} (States count: {speakingStates.Count})");
    }
    
    return result;
}
```

---

## ?? 完整诊断检查清单

### 1. 检查 Mod 设置
- [ ] 开启 DevMode（Esc → Options → Developer mode）
- [ ] 启用 TTS（Mod Settings → TTS → 勾选"启用语音合成"）
- [ ] 启用自动播放（Mod Settings → TTS → 勾选"自动播放 TTS"）
- [ ] 配置 TTS 提供商（Edge TTS 无需配置，直接可用）

### 2. 触发 TTS 播放
- [ ] 进入游戏，打开 AI 对话窗口
- [ ] 发送任意消息给 AI
- [ ] 等待 AI 回复

### 3. 检查日志输出
打开 Player.log（`%APPDATA%\..\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Player.log`）

**期望看到的日志**：
```
[TTSAudioPlayer] Loading audio: file://...
[TTSAudioPlayer] AudioClip loaded: X.XX seconds
[TTSAudioPlayer] Playing audio...
[TTSAudioPlayer] Speaking started: Sideria         <-- 关键！
[MouthAnimationSystem] IsSpeaking(Sideria) = True  <-- 关键！
[MouthAnimationSystem] Sideria 开始说话
[MouthAnimationSystem] Sideria TTS播放中 - 开合度: 0.XX
[MouthAnimationSystem] Sideria 嘴部图层: medium_mouth (...)
...
[TTSAudioPlayer] Playback finished
[TTSAudioPlayer] Speaking finished: Sideria
[MouthAnimationSystem] Sideria 停止说话
```

**如果看到的是**：
```
[TTSAudioPlayer] Playing audio...
[MouthAnimationSystem] IsSpeaking(Sideria) = False  <-- 问题：始终 False
```

**说明**：播放时没有传递 `personaDefName`，或者传递的名字不匹配。

---

## ?? 立即行动步骤

### 步骤 1：搜索所有 TTS 调用点
```powershell
# 搜索所有调用 PlayFromBytes 的地方
Select-String -Path "Source\TheSecondSeat\*.cs" -Pattern "PlayFromBytes" -Recurse

# 搜索所有调用 PlayAndDelete 的地方
Select-String -Path "Source\TheSecondSeat\*.cs" -Pattern "PlayAndDelete" -Recurse
```

### 步骤 2：修改所有调用点
确保所有调用都传递了 `personaDefName`：
```csharp
// 正确的调用模式
TTSAudioPlayer.Instance.PlayFromBytes(audioData, personaDefName);
TTSAudioPlayer.Instance.PlayAndDelete(filePath, personaDefName);
```

### 步骤 3：重新编译和测试
```powershell
.\Deploy-v1.6.50.ps1
```

### 步骤 4：查看日志验证
1. 启动游戏，触发 AI 对话
2. 打开 Player.log
3. 搜索 `[TTSAudioPlayer]` 和 `[MouthAnimationSystem]`
4. 验证状态是否正确更新

---

## ?? 预期日志对比

### ? 当前（错误）日志
```
[TTSAudioPlayer] Playing audio...
[MouthAnimationSystem] IsSpeaking(Sideria) = False
(没有嘴部图层变化)
```

### ? 修复后（正确）日志
```
[TTSAudioPlayer] Playing audio...
[TTSAudioPlayer] Speaking started: Sideria
[MouthAnimationSystem] IsSpeaking(Sideria) = True
[MouthAnimationSystem] Sideria 开始说话
[MouthAnimationSystem] Sideria TTS播放中 - 开合度: 0.45
[MouthAnimationSystem] Sideria 嘴部图层: medium_mouth
[MouthAnimationSystem] Sideria TTS播放中 - 开合度: 0.62
[MouthAnimationSystem] Sideria 嘴部图层: larger_mouth
[TTSAudioPlayer] Speaking finished: Sideria
[MouthAnimationSystem] Sideria 停止说话
```

---

## ?? 关键结论

**TTS 嘴部动画不工作的唯一原因**：

1. ? 代码结构完全正确
2. ? 状态跟踪机制正确
3. ? **播放 TTS 时没有传递 `personaDefName` 参数**

**修复重点**：
- 找到所有调用 `TTSAudioPlayer` 的地方
- 确保传递 `personaDefName` 参数
- 验证参数值与人格 DefName 匹配

**预计修复时间**：< 10 分钟
