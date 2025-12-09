# TTS 高质量音频 - 快速参考

## ?? 核心修改

```csharp
// 文件: Source/TheSecondSeat/TTS/TTSService.cs
// 位置: GenerateAzureTTSAsync() 方法

// 旧版本 (24kHz)
"riff-24khz-16bit-mono-pcm"

// 新版本 (48kHz) ?
"riff-48khz-16bit-mono-pcm"
```

## ?? 质量对比

| 项目 | 旧版本 | 新版本 | 提升 |
|-----|--------|--------|------|
| 采样率 | 24 kHz | **48 kHz** | **2x** |
| 比特率 | 384 kbps | **768 kbps** | **2x** |
| 质量 | 标准 | **高质量** | ??? |

## ? 使用步骤

1. **重启 RimWorld**
2. **Mod 设置 → The Second Seat → TTS**
3. **启用 Azure TTS**
4. **输入 API 密钥**
5. **选择语音 (推荐: zh-CN-XiaoxiaoNeural)**
6. **测试 TTS**
7. **享受高质量语音！**

## ?? 推荐语音

- ? **zh-CN-XiaoxiaoNeural** - 女声，温暖，通用
- **zh-CN-YunxiNeural** - 男声，自然，通用
- **zh-CN-XiaoyiNeural** - 女声，可爱，萝莉
- **zh-CN-YunjianNeural** - 男声，专业，新闻

## ?? 验证方法

### 代码验证
```powershell
Select-String -Path "Source\TheSecondSeat\TTS\TTSService.cs" -Pattern "riff-48khz"
```

### 音频验证
1. 测试 TTS
2. 打开音频文件 (`%USERPROFILE%\AppData\LocalLow\...\TheSecondSeat\TTS\`)
3. 右键 → 属性 → 详细信息
4. 确认: **采样率 = 48000 Hz** ?

## ?? 好处

- ? 更平滑的语音过渡
- ? 更自然的人声效果
- ? 更好的情感表达
- ? 符合专业音频标准

## ?? 部署状态

- ? 代码修改完成
- ? 编译成功
- ? DLL 已部署

---

**版本:** v1.6.9 | **状态:** ? 已完成
