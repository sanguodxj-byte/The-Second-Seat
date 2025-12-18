# TTS 嘴部动画测试指南 v1.6.50

## 测试前准备

1. **开启 DevMode**
   - 游戏中按 Esc
   - Options → Developer mode → 勾选

2. **开启立绘模式**
   - Mod Settings → The Second Seat
   - 勾选 "使用立绘模式"

3. **启用 TTS**
   - Mod Settings → The Second Seat → TTS
   - 勾选 "启用语音合成（TTS）"
   - 选择提供商（Edge/Azure/Local）

## 测试步骤

### 步骤 1: 验证调用链
1. 启动游戏，进入任意存档
2. 按 F12 打开开发者控制台
3. 查看日志，应该看到：
   `
   [MouthAnimationSystem] Sideria 嘴部图层: null (表情=Neutral, 开合度=0.00, TTS=False)
   `

### 步骤 2: 触发 TTS
1. 打开 AI 对话窗口
2. 输入任意消息并发送
3. 等待 AI 回复
4. 观察立绘嘴部是否动态变化

### 步骤 3: 查看调试日志
在控制台中应该看到：
`
[MouthAnimationSystem] Sideria 开始说话
[MouthAnimationSystem] Sideria TTS播放中 - 开合度: 0.45
[MouthAnimationSystem] Sideria 嘴部图层: medium_mouth (表情=Happy, 开合度=0.45, TTS=True)
[MouthAnimationSystem] Sideria TTS播放中 - 开合度: 0.62
[MouthAnimationSystem] Sideria 嘴部图层: larger_mouth (表情=Happy, 开合度=0.62, TTS=True)
[MouthAnimationSystem] Sideria 停止说话
[MouthAnimationSystem] Sideria 嘴部图层: small_mouth (表情=Happy, 开合度=0.50, TTS=False)
`

## 常见问题

### 问题 1: 没有任何日志输出
**原因**: MouthAnimationSystem.Update() 没有被调用
**解决**: 检查 WindowUpdate() 是否调用了 Update()

### 问题 2: 日志显示 TTS=False
**原因**: TTSAudioPlayer.IsSpeaking() 始终返回 false
**解决**: 检查 TTS 播放状态跟踪逻辑

### 问题 3: 嘴型图层始终为 null
**原因**: GetMouthLayerName() 返回 null 或纹理文件缺失
**解决**: 检查嘴部纹理文件是否存在

### 问题 4: 立绘不显示嘴型
**原因**: DrawLayeredPortraitRuntime() 没有绘制嘴部图层
**解决**: 检查嘴部图层绘制代码

## Player.log 位置

Windows:
`
%USERPROFILE%\AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Player.log
`

查找关键字:
- [MouthAnimationSystem]
- [TTSAudioPlayer]
- TTS播放中
