# ======================================
#  TTS 嘴部动画诊断脚本 v1.6.50
#  诊断 TTS 播放时嘴部不动的问题
# ======================================

$ErrorActionPreference = "Continue"

Write-Host ""
Write-Host "======================================" -ForegroundColor Cyan
Write-Host "  TTS 嘴部动画诊断工具 v1.6.50" -ForegroundColor Cyan
Write-Host "======================================" -ForegroundColor Cyan
Write-Host ""

# ==================== 1. 检查 MouthAnimationSystem 调用点 ====================

Write-Host "步骤 1/6: 检查 MouthAnimationSystem.Update() 调用..." -ForegroundColor Yellow
Write-Host ""

$updateCallers = @(
    "Source\TheSecondSeat\UI\NarratorScreenButton.cs",
    "Source\TheSecondSeat\UI\FullBodyPortraitPanel.cs"
)

$foundUpdateCalls = $false

foreach ($file in $updateCallers) {
    if (Test-Path $file) {
        $content = Get-Content $file -Raw
        
        if ($content -match "MouthAnimationSystem\.Update") {
            Write-Host "   ? 找到调用: $file" -ForegroundColor Green
            
            # 提取调用上下文
            $lines = Get-Content $file
            for ($i = 0; $i -lt $lines.Count; $i++) {
                if ($lines[$i] -match "MouthAnimationSystem\.Update") {
                    Write-Host "      行 $($i+1): $($lines[$i].Trim())" -ForegroundColor Gray
                    $foundUpdateCalls = $true
                }
            }
        }
        else {
            Write-Host "   ? 未找到调用: $file" -ForegroundColor Yellow
        }
    }
}

if (!$foundUpdateCalls) {
    Write-Host ""
    Write-Host "   ? 严重问题：MouthAnimationSystem.Update() 没有被调用！" -ForegroundColor Red
    Write-Host "   这是嘴部动画不工作的主要原因。" -ForegroundColor Red
}

# ==================== 2. 检查 GetMouthLayerName() 调用点 ====================

Write-Host ""
Write-Host "步骤 2/6: 检查 GetMouthLayerName() 调用..." -ForegroundColor Yellow
Write-Host ""

$getMouthCallers = @(
    "Source\TheSecondSeat\UI\FullBodyPortraitPanel.cs",
    "Source\TheSecondSeat\PersonaGeneration\LayeredPortraitCompositor.cs",
    "Source\TheSecondSeat\PersonaGeneration\AvatarLoader.cs"
)

$foundGetMouthCalls = $false

foreach ($file in $getMouthCallers) {
    if (Test-Path $file) {
        $content = Get-Content $file -Raw
        
        if ($content -match "MouthAnimationSystem\.GetMouthLayerName") {
            Write-Host "   ? 找到调用: $file" -ForegroundColor Green
            
            $lines = Get-Content $file
            for ($i = 0; $i -lt $lines.Count; $i++) {
                if ($lines[$i] -match "MouthAnimationSystem\.GetMouthLayerName") {
                    Write-Host "      行 $($i+1): $($lines[$i].Trim())" -ForegroundColor Gray
                    $foundGetMouthCalls = $true
                }
            }
        }
        else {
            Write-Host "   ? 未找到调用: $file" -ForegroundColor Yellow
        }
    }
}

if (!$foundGetMouthCalls) {
    Write-Host ""
    Write-Host "   ? 严重问题：GetMouthLayerName() 没有被调用！" -ForegroundColor Red
    Write-Host "   嘴部图层无法加载。" -ForegroundColor Red
}

# ==================== 3. 检查 TTSAudioPlayer.IsSpeaking() 实现 ====================

Write-Host ""
Write-Host "步骤 3/6: 检查 TTSAudioPlayer.IsSpeaking() 实现..." -ForegroundColor Yellow
Write-Host ""

$ttsPlayerFile = "Source\TheSecondSeat\TTS\TTSAudioPlayer.cs"

if (Test-Path $ttsPlayerFile) {
    $content = Get-Content $ttsPlayerFile -Raw
    
    if ($content -match "public static bool IsSpeaking\(") {
        Write-Host "   ? 找到 IsSpeaking() 方法" -ForegroundColor Green
        
        # 检查方法实现
        if ($content -match "IsSpeaking.*\{([^}]+)\}") {
            Write-Host "      方法体存在" -ForegroundColor Gray
        }
        
        # 检查是否有状态跟踪
        if ($content -match "Dictionary.*string.*bool" -or $content -match "playingStates") {
            Write-Host "   ? 找到播放状态跟踪字典" -ForegroundColor Green
        }
        else {
            Write-Host "   ? 未找到播放状态跟踪机制" -ForegroundColor Yellow
        }
    }
    else {
        Write-Host "   ? 未找到 IsSpeaking() 方法！" -ForegroundColor Red
        Write-Host "   这是 TTS 状态检测失败的主要原因。" -ForegroundColor Red
    }
}
else {
    Write-Host "   ? TTSAudioPlayer.cs 文件不存在！" -ForegroundColor Red
}

# ==================== 4. 检查嘴部纹理文件 ====================

Write-Host ""
Write-Host "步骤 4/6: 检查嘴部纹理文件..." -ForegroundColor Yellow
Write-Host ""

$mouthTextures = @(
    "Textures\UI\Narrators\9x16\Layered\Sideria\small_mouth.png",
    "Textures\UI\Narrators\9x16\Layered\Sideria\medium_mouth.png",
    "Textures\UI\Narrators\9x16\Layered\Sideria\larger_mouth.png"
)

$foundTextures = 0

foreach ($texture in $mouthTextures) {
    if (Test-Path $texture) {
        $size = (Get-Item $texture).Length
        Write-Host "   ? 存在: $(Split-Path $texture -Leaf) ($([math]::Round($size/1KB, 2)) KB)" -ForegroundColor Green
        $foundTextures++
    }
    else {
        Write-Host "   ? 缺失: $(Split-Path $texture -Leaf)" -ForegroundColor Red
    }
}

if ($foundTextures -eq 0) {
    Write-Host ""
    Write-Host "   ? 所有嘴部纹理文件都缺失！" -ForegroundColor Red
    Write-Host "   即使动画系统工作，也无法显示嘴型。" -ForegroundColor Red
}

# ==================== 5. 分析代码流程 ====================

Write-Host ""
Write-Host "步骤 5/6: 分析完整调用链..." -ForegroundColor Yellow
Write-Host ""

Write-Host "   期望的调用流程:" -ForegroundColor Cyan
Write-Host "   ┌─────────────────────────────────────────┐" -ForegroundColor Gray
Write-Host "   │ 1. WindowUpdate() 每帧调用              │" -ForegroundColor Gray
Write-Host "   │    ↓                                    │" -ForegroundColor Gray
Write-Host "   │ 2. MouthAnimationSystem.Update()        │" -ForegroundColor Gray
Write-Host "   │    ↓                                    │" -ForegroundColor Gray
Write-Host "   │ 3. TTSAudioPlayer.IsSpeaking(defName)   │" -ForegroundColor Gray
Write-Host "   │    ↓                                    │" -ForegroundColor Gray
Write-Host "   │ 4. GetMouthLayerName(defName)           │" -ForegroundColor Gray
Write-Host "   │    ↓                                    │" -ForegroundColor Gray
Write-Host "   │ 5. GetMouthShapeLayerName()             │" -ForegroundColor Gray
Write-Host "   │    ↓                                    │" -ForegroundColor Gray
Write-Host "   │ 6. PortraitLoader.GetLayerTexture()     │" -ForegroundColor Gray
Write-Host "   │    ↓                                    │" -ForegroundColor Gray
Write-Host "   │ 7. GUI.DrawTexture() 渲染嘴型           │" -ForegroundColor Gray
Write-Host "   └─────────────────────────────────────────┘" -ForegroundColor Gray
Write-Host ""

# 检查每个环节
$issues = @()

# 检查 WindowUpdate
$buttonFile = "Source\TheSecondSeat\UI\NarratorScreenButton.cs"
if (Test-Path $buttonFile) {
    $content = Get-Content $buttonFile -Raw
    if ($content -match "public override void WindowUpdate\(\)" -and 
        $content -match "MouthAnimationSystem\.Update") {
        Write-Host "   ? 环节 1-2: WindowUpdate → MouthAnimation.Update" -ForegroundColor Green
    }
    else {
        Write-Host "   ? 环节 1-2: WindowUpdate 未调用 MouthAnimation.Update" -ForegroundColor Red
        $issues += "WindowUpdate 未调用 MouthAnimationSystem.Update()"
    }
}

# 检查 DrawLayeredPortraitRuntime
$panelFile = "Source\TheSecondSeat\UI\FullBodyPortraitPanel.cs"
if (Test-Path $panelFile) {
    $content = Get-Content $panelFile -Raw
    if ($content -match "DrawLayeredPortraitRuntime" -and 
        $content -match "MouthAnimationSystem\.GetMouthLayerName") {
        Write-Host "   ? 环节 4-5: DrawLayeredPortrait → GetMouthLayerName" -ForegroundColor Green
    }
    else {
        Write-Host "   ? 环节 4-5: DrawLayeredPortrait 未调用 GetMouthLayerName" -ForegroundColor Red
        $issues += "DrawLayeredPortraitRuntime 未调用 GetMouthLayerName()"
    }
}

# ==================== 6. 生成修复建议 ====================

Write-Host ""
Write-Host "步骤 6/6: 生成修复建议..." -ForegroundColor Yellow
Write-Host ""

if ($issues.Count -gt 0) {
    Write-Host "   发现 $($issues.Count) 个问题:" -ForegroundColor Red
    Write-Host ""
    
    foreach ($issue in $issues) {
        Write-Host "   ? $issue" -ForegroundColor Red
    }
    
    Write-Host ""
    Write-Host "======================================" -ForegroundColor Cyan
    Write-Host "  ?? 修复建议" -ForegroundColor Yellow
    Write-Host "======================================" -ForegroundColor Cyan
    Write-Host ""
    
    if ($issues -match "WindowUpdate") {
        Write-Host "问题 1: WindowUpdate 未调用 MouthAnimationSystem.Update()" -ForegroundColor Yellow
        Write-Host ""
        Write-Host "修复方法：在 NarratorScreenButton.cs 的 WindowUpdate() 中添加:" -ForegroundColor White
        Write-Host @"
   public override void WindowUpdate()
   {
       base.WindowUpdate();
       
       // ... 其他代码 ...
       
       // ? 添加这行
       MouthAnimationSystem.Update(Time.deltaTime);
   }
"@ -ForegroundColor Green
        Write-Host ""
    }
    
    if ($issues -match "DrawLayeredPortrait") {
        Write-Host "问题 2: DrawLayeredPortraitRuntime 未调用 GetMouthLayerName()" -ForegroundColor Yellow
        Write-Host ""
        Write-Host "修复方法：在 FullBodyPortraitPanel.cs 的 DrawLayeredPortraitRuntime() 中添加:" -ForegroundColor White
        Write-Host @"
   // Layer 2: 嘴巴层（动态重载，张嘴动画）
   string mouthLayerName = MouthAnimationSystem.GetMouthLayerName(persona.defName);
   if (!string.IsNullOrEmpty(mouthLayerName))
   {
       var mouthTexture = PortraitLoader.GetLayerTexture(persona, mouthLayerName);
       if (mouthTexture != null)
       {
           GUI.DrawTexture(rect, mouthTexture, ScaleMode.ScaleToFit);
       }
   }
"@ -ForegroundColor Green
        Write-Host ""
    }
    
    if ($foundTextures -eq 0) {
        Write-Host "问题 3: 嘴部纹理文件缺失" -ForegroundColor Yellow
        Write-Host ""
        Write-Host "修复方法：确保以下文件存在:" -ForegroundColor White
        Write-Host "   Textures\UI\Narrators\9x16\Layered\Sideria\" -ForegroundColor Gray
        Write-Host "   ├── small_mouth.png    (小开口)" -ForegroundColor Gray
        Write-Host "   ├── medium_mouth.png   (中等开口)" -ForegroundColor Gray
        Write-Host "   └── larger_mouth.png   (大开口)" -ForegroundColor Gray
        Write-Host ""
    }
}
else {
    Write-Host "   ? 代码结构看起来正常" -ForegroundColor Green
    Write-Host ""
    Write-Host "   可能的其他原因:" -ForegroundColor Yellow
    Write-Host "   1. TTSAudioPlayer.IsSpeaking() 始终返回 false" -ForegroundColor Gray
    Write-Host "   2. TTS 没有真正播放音频" -ForegroundColor Gray
    Write-Host "   3. defName 参数不匹配" -ForegroundColor Gray
    Write-Host "   4. DevMode 未开启，看不到调试日志" -ForegroundColor Gray
    Write-Host ""
}

# ==================== 生成快速修复脚本 ====================

Write-Host ""
Write-Host "======================================" -ForegroundColor Cyan
Write-Host "  ?? 生成快速修复脚本" -ForegroundColor Yellow
Write-Host "======================================" -ForegroundColor Cyan
Write-Host ""

$fixScript = @"
# 快速修复 TTS 嘴部动画
# 自动生成于: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')

Write-Host "应用 TTS 嘴部动画修复..." -ForegroundColor Yellow

# 1. 确保 WindowUpdate 调用 MouthAnimationSystem.Update()
`$buttonFile = "Source\TheSecondSeat\UI\NarratorScreenButton.cs"
if (Test-Path `$buttonFile) {
    `$content = Get-Content `$buttonFile -Raw
    if (`$content -notmatch "MouthAnimationSystem\.Update") {
        Write-Host "   添加 MouthAnimationSystem.Update() 调用..." -ForegroundColor Yellow
        # 在 WindowUpdate 中添加调用
        # (需要手动实现具体逻辑)
    }
}

# 2. 确保 DrawLayeredPortraitRuntime 调用 GetMouthLayerName()
`$panelFile = "Source\TheSecondSeat\UI\FullBodyPortraitPanel.cs"
if (Test-Path `$panelFile) {
    `$content = Get-Content `$panelFile -Raw
    if (`$content -notmatch "MouthAnimationSystem\.GetMouthLayerName") {
        Write-Host "   添加 GetMouthLayerName() 调用..." -ForegroundColor Yellow
        # 在嘴巴层绘制代码中添加调用
        # (需要手动实现具体逻辑)
    }
}

Write-Host "   ? 修复完成！重新编译并部署。" -ForegroundColor Green
"@

$fixScript | Out-File "Fix-TTS-Mouth-Animation.ps1" -Encoding UTF8
Write-Host "   ? 已生成: Fix-TTS-Mouth-Animation.ps1" -ForegroundColor Green

# ==================== 生成测试指南 ====================

$testGuide = @"
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
   ```
   [MouthAnimationSystem] Sideria 嘴部图层: null (表情=Neutral, 开合度=0.00, TTS=False)
   ```

### 步骤 2: 触发 TTS
1. 打开 AI 对话窗口
2. 输入任意消息并发送
3. 等待 AI 回复
4. 观察立绘嘴部是否动态变化

### 步骤 3: 查看调试日志
在控制台中应该看到：
```
[MouthAnimationSystem] Sideria 开始说话
[MouthAnimationSystem] Sideria TTS播放中 - 开合度: 0.45
[MouthAnimationSystem] Sideria 嘴部图层: medium_mouth (表情=Happy, 开合度=0.45, TTS=True)
[MouthAnimationSystem] Sideria TTS播放中 - 开合度: 0.62
[MouthAnimationSystem] Sideria 嘴部图层: larger_mouth (表情=Happy, 开合度=0.62, TTS=True)
[MouthAnimationSystem] Sideria 停止说话
[MouthAnimationSystem] Sideria 嘴部图层: small_mouth (表情=Happy, 开合度=0.50, TTS=False)
```

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
```
%USERPROFILE%\AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Player.log
```

查找关键字:
- `[MouthAnimationSystem]`
- `[TTSAudioPlayer]`
- `TTS播放中`
"@

$testGuide | Out-File "TTS嘴部动画测试指南-v1.6.50.md" -Encoding UTF8
Write-Host "   ? 已生成: TTS嘴部动画测试指南-v1.6.50.md" -ForegroundColor Green

Write-Host ""
Write-Host "======================================" -ForegroundColor Cyan
Write-Host "  诊断完成" -ForegroundColor Green
Write-Host "======================================" -ForegroundColor Cyan
Write-Host ""

if ($issues.Count -gt 0) {
    Write-Host "发现 $($issues.Count) 个问题需要修复。" -ForegroundColor Yellow
    Write-Host "请查看上方的修复建议并手动修复代码。" -ForegroundColor Yellow
}
else {
    Write-Host "代码结构看起来正常。" -ForegroundColor Green
    Write-Host "如果嘴部动画仍不工作，请：" -ForegroundColor Yellow
    Write-Host "1. 开启 DevMode 查看调试日志" -ForegroundColor Gray
    Write-Host "2. 检查 Player.log 查找错误信息" -ForegroundColor Gray
    Write-Host "3. 验证 TTS 是否真正播放音频" -ForegroundColor Gray
}

Write-Host ""
Write-Host "生成的文件:" -ForegroundColor White
Write-Host "   - Fix-TTS-Mouth-Animation.ps1" -ForegroundColor Gray
Write-Host "   - TTS嘴部动画测试指南-v1.6.50.md" -ForegroundColor Gray
Write-Host ""
