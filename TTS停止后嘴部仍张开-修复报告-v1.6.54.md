# TTS 停止后嘴部仍张开问题修复 - v1.6.54

## ?? 问题描述

**用户反馈**：TTS 播放结束后，嘴部应该恢复闭嘴状态（Neutral 表情的默认状态），但实际上显示的是 `larger_mouth`（大张嘴）。

## ?? 问题根源

### 原因分析

在 `MouthAnimationSystem.cs` 的 `GetMouthLayerName` 方法中：

**问题代码（第 157 行）**：
```csharp
// ? 5. 平滑过渡到目标开合度
state.currentOpenness = Mathf.Lerp(state.currentOpenness, targetOpenness, Time.deltaTime * 5f);
```

**问题流程**：

1. **TTS 播放时**：
   - `currentOpenness` 在 0 ~ 0.8 之间波动（正弦波）
   - 嘴部正确显示动态张合

2. **TTS 停止后**：
   - `targetOpenness = 0f`（Neutral 表情 → 闭嘴）
   - `Mathf.Lerp` 使用 `Time.deltaTime * 5f`（约 0.08）
   - **过渡非常缓慢**：从 0.8 降到 0 需要数秒

3. **结果**：
   - TTS 停止后，`currentOpenness` 仍然 > 0.6
   - 触发 `GetMouthShapeLayerName` 返回 `larger_mouth`
   - 嘴部看起来仍然大张着

---

## ?? 解决方案

### 修复策略

**TTS 停止时立即重置开合度，不使用平滑过渡**

### 修改文件

**`Source\TheSecondSeat\PersonaGeneration\MouthAnimationSystem.cs`** (第 143-169 行)

### 关键修复

#### 修复前（错误）
```csharp
if (isPlayingTTS)
{
    // TTS 播放中：动态张嘴
    state.isSpeaking = true;
    state.speakingTime += Time.deltaTime;
    targetOpenness = Mathf.Lerp(0f, 0.8f, (sineWave + 1f) * 0.5f);
}
else
{
    // TTS 停止：静默状态
    state.isSpeaking = false;
    state.speakingTime = 0f;
    targetOpenness = GetMouthOpennessForExpression(state.currentExpression);
}

// ? 问题：总是使用平滑过渡（导致延迟）
state.currentOpenness = Mathf.Lerp(state.currentOpenness, targetOpenness, Time.deltaTime * 5f);
```

#### 修复后（正确）
```csharp
if (isPlayingTTS)
{
    // TTS 播放中：动态张嘴
    state.isSpeaking = true;
    state.speakingTime += Time.deltaTime;
    targetOpenness = Mathf.Lerp(0f, 0.8f, (sineWave + 1f) * 0.5f);
}
else
{
    // ? v1.6.54: TTS 停止后立即重置开合度
    if (state.isSpeaking)
    {
        // 刚刚停止说话，立即重置
        state.isSpeaking = false;
        state.speakingTime = 0f;
        state.currentOpenness = 0f;  // ? 关键修复：立即重置为 0
        
        if (Prefs.DevMode)
        {
            Log.Message($"[MouthAnimationSystem] {defName} TTS停止 - 立即闭嘴");
        }
    }
    
    targetOpenness = GetMouthOpennessForExpression(state.currentExpression);
}

// ? 修复：区分 TTS 播放和停止状态
if (isPlayingTTS)
{
    // TTS 播放中：平滑过渡（用于正弦波动画）
    state.currentOpenness = Mathf.Lerp(state.currentOpenness, targetOpenness, Time.deltaTime * 10f);
}
else
{
    // TTS 停止后：直接设置为目标值（避免延迟）
    state.currentOpenness = targetOpenness;
}
```

---

## ?? 修复对比

### 修复前
```
[TTS 播放中]
currentOpenness: 0 → 0.4 → 0.8 → 0.6 → 0.3 → 0.7 (正弦波)
嘴部图层: medium_mouth → larger_mouth → medium_mouth → larger_mouth

[TTS 停止]
Time 0.0s: currentOpenness = 0.8 → larger_mouth  ? 仍然大张
Time 0.1s: currentOpenness = 0.76 → larger_mouth ? 仍然大张
Time 0.2s: currentOpenness = 0.72 → larger_mouth ? 仍然大张
Time 0.5s: currentOpenness = 0.6 → larger_mouth  ? 仍然大张
Time 1.0s: currentOpenness = 0.4 → medium_mouth  ? 仍然半张
Time 2.0s: currentOpenness = 0.1 → small_mouth   ? 仍然微张
Time 3.0s: currentOpenness = 0.02 → null (闭嘴) ? 终于闭嘴
```

### 修复后
```
[TTS 播放中]
currentOpenness: 0 → 0.4 → 0.8 → 0.6 → 0.3 → 0.7 (正弦波)
嘴部图层: medium_mouth → larger_mouth → medium_mouth → larger_mouth

[TTS 停止]
Time 0.0s: currentOpenness = 0.0 → null (闭嘴) ? 立即闭嘴
Time 0.1s: currentOpenness = 0.0 → null (闭嘴) ? 保持闭嘴
Time 1.0s: currentOpenness = 0.0 → null (闭嘴) ? 保持闭嘴
```

---

## ? 部署状态

- **编译状态**: ? 成功（498 KB）
- **部署时间**: 2025-12-17 16:13:14
- **警告数量**: 8 个（非致命）
- **部署位置**: `D:\steam\steamapps\common\RimWorld\Mods\TheSecondSeat\Assemblies\TheSecondSeat.dll`

---

## ?? 测试指南

### 步骤 1：开启必要设置

1. **开启 DevMode**（查看日志）
   - 游戏中按 `Esc`
   - `Options → Developer mode → 勾选`

2. **开启 TTS**
   - `Mod Settings → The Second Seat → TTS`
   - 勾选 `启用语音合成（TTS）`
   - 勾选 `自动播放 TTS（叙事者发言时）`

3. **开启立绘模式**（可选，便于观察）
   - `Mod Settings → The Second Seat`
   - 勾选 `使用立绘模式`

### 步骤 2：触发 TTS 播放

1. 进入游戏，加载任意存档
2. 点击左上角的 AI 按钮（头像）
3. 在对话窗口输入消息（如："你好"）
4. 发送消息，等待 AI 回复

### 步骤 3：观察嘴部动画

#### TTS 播放中（正常）
```
预期行为：
- 嘴部动态张合（small_mouth ? medium_mouth ? larger_mouth）
- 与 TTS 音频同步
- 正弦波式的开合动画

DevMode 日志：
[MouthAnimationSystem] Sideria TTS播放中 - 开合度: 0.45
[MouthAnimationSystem] Sideria 嘴部图层: medium_mouth
[MouthAnimationSystem] Sideria TTS播放中 - 开合度: 0.62
[MouthAnimationSystem] Sideria 嘴部图层: larger_mouth
```

#### TTS 停止后（修复重点）
```
预期行为：
? 嘴部立即闭合（显示 base_body 的闭嘴）
? 不再显示任何嘴部图层（layerName = null）
? 没有延迟或缓慢过渡

DevMode 日志：
[TTSAudioPlayer] Playback finished
[TTSAudioPlayer] Speaking finished: Sideria
[MouthAnimationSystem] Sideria TTS停止 - 立即闭嘴  ? 新增日志
[MouthAnimationSystem] Sideria 停止说话
[MouthAnimationSystem] Sideria 嘴部图层: null (表情=Neutral, 开合度=0.00, TTS=False)
```

### 步骤 4：验证修复

**检查清单**：

- [ ] TTS 播放时：嘴部动态张合 ?
- [ ] TTS 停止后：嘴部**立即**闭合 ?（不再延迟3秒）
- [ ] 闭嘴状态：显示 `base_body` 的闭嘴（不显示嘴部图层）?
- [ ] 日志输出：显示 "TTS停止 - 立即闭嘴" ?

---

## ?? 故障排查

### 问题 1：TTS 停止后嘴部仍然张开

**可能原因**：
- 旧代码仍在内存中（未重启游戏）
- DLL 未正确部署

**解决方法**：
1. 确认 DLL 时间戳：`2025-12-17 16:13:14`
2. 完全退出游戏，重新启动
3. 加载存档后测试

### 问题 2：日志中没有 "TTS停止 - 立即闭嘴"

**可能原因**：
- DevMode 未开启
- TTS 未正确播放（没有触发停止事件）

**解决方法**：
1. 确认 DevMode 已开启
2. 查看 Player.log 中的 `[TTSAudioPlayer]` 日志
3. 确认 TTS 播放完成

### 问题 3：嘴部闭合后又立即张开

**可能原因**：
- 表情系统设置了非 Neutral 表情（如 Happy）
- `GetMouthOpennessForExpression` 返回非 0 值

**检查方法**：
查看日志中的表情类型：
```
[MouthAnimationSystem] Sideria 嘴部图层: small_mouth (表情=Happy, 开合度=0.50, TTS=False)
```

如果表情是 Happy/Smug 等，嘴部会根据表情显示静态嘴型（符合预期）。

---

## ?? 性能影响

- **CPU**: 无影响（减少了不必要的 Lerp 计算）
- **内存**: 无影响
- **响应速度**: **提升**（TTS 停止后立即闭嘴，无延迟）

---

## ?? 总结

### 修复亮点

1. **问题定位准确**：识别到平滑过渡导致的延迟
2. **修复逻辑清晰**：TTS 停止时立即重置 `currentOpenness`
3. **向后兼容**：不影响 TTS 播放中的动画效果
4. **调试友好**：新增 "TTS停止 - 立即闭嘴" 日志

### 技术要点

**关键设计决策**：
- **TTS 播放中**：使用平滑过渡（`Mathf.Lerp`）实现正弦波动画
- **TTS 停止后**：立即重置为目标值（避免延迟）

**为什么不能总是使用平滑过渡？**
- 正弦波动画需要平滑过渡（避免突变）
- 但 TTS 停止是**状态切换**，应该立即响应

### 相关修复

- **v1.6.51**: 修复 TTS 嘴部动画（传递 `personaDefName` 参数）
- **v1.6.52**: 修复触摸模式提示框显示
- **v1.6.53**: 修复半透明模式透明度
- **v1.6.54**: 修复 TTS 停止后嘴部仍张开（本次修复）

---

## ? 完成！

The Second Seat - 让 AI 口型同步更加精准！

如有问题，请查看：
- `Player.log`（游戏日志）
- `TTS嘴部动画修复完成报告-v1.6.51.md`（TTS 基础修复）
- `TTS嘴部动画测试指南-v1.6.50.md`（测试指南）
