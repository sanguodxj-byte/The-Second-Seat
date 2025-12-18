# TSS实体功能与TTS - 快速参考 v2.0.2

## ?? 快速索引

### 已完成任务
- ? **任务A**: PlaySoundAction 修复
- ? **任务B**: AdvancedActions.cs（已存在）
- ? **任务C**: TTSService OpenAI 支持

---

## ?? OpenAI TTS 配置（?核心功能）

### 1. GPT-SoVITS 本地配置
```csharp
TTSService.Instance.Configure(
    provider: "openai",
    apiUrl: "http://127.0.0.1:9880/v1/audio/speech",
    modelName: "gpt-sovits",
    voice: "zh-CN-XiaoxiaoNeural"
);
```

### 2. OpenAI 官方 API
```csharp
TTSService.Instance.Configure(
    provider: "openai",
    key: "sk-...",
    apiUrl: "https://api.openai.com/v1/audio/speech",
    modelName: "tts-1",
    voice: "alloy"
);
```

### 3. 自定义兼容服务
```csharp
TTSService.Instance.Configure(
    provider: "openai",
    apiUrl: "https://your-service.com/v1/audio/speech",
    modelName: "your-model",
    voice: "your-voice"
);
```

---

## ?? 音效播放（BasicActions）

### PlaySoundAction
```xml
<li Class="TheSecondSeat.Framework.Actions.PlaySoundAction">
  <sound>ThunderOnMap</sound>
  <volume>1.0</volume>
</li>
```

**常用音效**:
- `ThunderOnMap` - 雷鸣
- `TradeShip_Ambience` - 商船
- `Click` - 点击
- `Tick_High` - 提示音

---

## ? 高阶动作（AdvancedActions）

### 1. 雷击天罚
```xml
<li Class="TheSecondSeat.Framework.Actions.StrikeLightningAction">
  <strikeMode>Random</strikeMode>
  <strikeCount>3</strikeCount>
  <damageAmount>100</damageAmount>
  <radius>5</radius>
</li>
```

### 2. 健康状态操控
```xml
<li Class="TheSecondSeat.Framework.Actions.GiveHediffAction">
  <hediffDef>Flu</hediffDef>
  <targetMode>Random</targetMode>
  <severity>0.5</severity>
  <targetCount>3</targetCount>
</li>
```

### 3. 强制触发事件
```xml
<li Class="TheSecondSeat.Framework.Actions.StartIncidentAction">
  <incidentDef>TraderCaravanArrival</incidentDef>
  <points>500</points>
  <forced>true</forced>
</li>
```

### 4. 叙事者语音
```xml
<li Class="TheSecondSeat.Framework.Actions.NarratorSpeakAction">
  <text>你做得很好！</text>
  <showDialogue>true</showDialogue>
</li>
```

---

## ?? OpenAI Speech API 格式

### 请求
```json
POST /v1/audio/speech
Content-Type: application/json

{
  "model": "gpt-sovits",
  "input": "你好，世界！",
  "voice": "zh-CN-XiaoxiaoNeural",
  "response_format": "wav",
  "speed": 1.0
}
```

### 响应
- **成功**: 返回 WAV 音频数据
- **失败**: 返回 JSON 错误信息

---

## ?? 使用示例

### 完整事件：语音 + 雷击
```xml
<TheSecondSeat.Framework.NarratorEventDef>
  <defName>DivinePunishment</defName>
  <category>Punishment</category>
  
  <actions>
    <!-- 1. 语音警告 -->
    <li Class="TheSecondSeat.Framework.Actions.NarratorSpeakAction">
      <text>你的所作所为...让我不得不采取措施了。</text>
      <showDialogue>true</showDialogue>
    </li>
    
    <!-- 2. 延迟3秒后降雷 -->
    <li Class="TheSecondSeat.Framework.Actions.StrikeLightningAction">
      <delayTicks>180</delayTicks>
      <strikeMode>Random</strikeMode>
      <strikeCount>5</strikeCount>
    </li>
  </actions>
</TheSecondSeat.Framework.NarratorEventDef>
```

---

## ?? TTS 提供商对比

| 提供商 | 延迟 | 质量 | 成本 | 离线 |
|--------|------|------|------|------|
| **Azure TTS** | ~300ms | ????? | 付费 | ? |
| **OpenAI TTS** | ~500ms | ????? | 付费 | ? |
| **GPT-SoVITS** | ~200ms | ???? | 免费 | ? |
| **Local TTS** | ~100ms | ?? | 免费 | ? |
| **Edge TTS** | - | - | 免费 | ? |

---

## ?? 常见问题

### OpenAI TTS 无响应
1. 检查服务是否启动（GPT-SoVITS端口9880）
2. 检查 API URL 是否正确
3. 开启 DevMode 查看日志

### 音效不播放
1. 检查 SoundDef 是否存在
2. 检查游戏音量设置
3. 使用 Prefs.DevMode 查看日志

### 高阶动作不生效
1. 检查XML语法是否正确
2. 检查 Class 命名空间是否完整
3. 查看游戏日志（F12调试窗口）

---

## ?? 调试技巧

### 启用详细日志
```csharp
Prefs.DevMode = true;  // 游戏内按F12
```

### 测试TTS
```csharp
// 在游戏控制台执行
await TTSService.Instance.SpeakAsync("测试文本");
```

### 检查音频文件
```
SaveData/TheSecondSeat/TTS/tts_*.wav
```

---

## ?? 相关文档

- **完整报告**: `TSS实体功能与TTS升级-完成报告-v2.0.2.md`
- **高阶动作**: `TSS高阶动作系统-快速参考-v2.0.1.md`
- **事件系统**: `Docs/TSS事件系统-快速参考.md`

---

## ?? 核心功能

### ? 已完成
1. PlaySoundAction 音效播放
2. 4个高阶动作（雷击/疾病/事件/语音）
3. **OpenAI兼容TTS**（GPT-SoVITS）
4. 完整的事件系统框架
5. 数据驱动配置（XML）

### ? 亮点
- 支持多TTS提供商（Azure/OpenAI/Local/GPT-SoVITS）
- OpenAI标准接口（易于扩展）
- 类型安全（继承体系）
- 异常安全（完整错误处理）

---

**版本**: v2.0.2  
**更新时间**: 2025-01-XX  
**状态**: ? 开发完成，编译通过
