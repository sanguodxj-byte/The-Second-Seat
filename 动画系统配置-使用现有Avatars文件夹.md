# 动画系统配置 - 使用现有 Avatars 文件夹

## ?? 配置说明

### 纹理文件映射

| 动画状态 | XML 配置 | 实际文件路径 | 说明 |
|---------|---------|-------------|------|
| **基础/中性** | `portraitPath` | `Avatars/Sideria/base.png` | 512x512 头像裁剪 |
| **眨眼** | `portraitPathBlink` | `Avatars/Sideria/blink.png` | 闭眼纹理（可选） |
| **说话** | `portraitPathSpeaking` | `Avatars/Sideria/speaking.png` | 张嘴纹理（可选） |

---

## ?? 现有文件结构

你已经有了完整的头像文件夹！

```
Textures/UI/Narrators/Avatars/Sideria/
├── base.png          ? 已存在 - 中性表情 (512x512)
├── happy.png         ? 已存在 - 开心表情
├── happy1.png        ? 已存在 - 开心变体1
├── happy2.png        ? 已存在 - 开心变体2
├── sad.png           ? 已存在 - 悲伤表情
├── angry.png         ? 已存在 - 愤怒表情
├── shy.png           ? 已存在 - 害羞表情
├── shy1.png          ? 已存在 - 害羞变体1
└── ...               其他表情
```

**需要添加的文件（可选）：**
```
├── blink.png         ? 需要添加 - 闭眼纹理
└── speaking.png      ? 需要添加 - 张嘴纹理
```

---

## ?? XML 配置（已完成）

**文件：** `Defs/NarratorPersonaDefs.xml`

```xml
<TheSecondSeat.PersonaGeneration.NarratorPersonaDef>
    <defName>Sideria_Default</defName>
    
    <!-- ? 使用现有的 Avatars 文件夹 -->
    <portraitPath>UI/Narrators/Avatars/Sideria/base</portraitPath>
    <portraitPathBlink>UI/Narrators/Avatars/Sideria/blink</portraitPathBlink>
    <portraitPathSpeaking>UI/Narrators/Avatars/Sideria/speaking</portraitPathSpeaking>
</TheSecondSeat.PersonaGeneration.NarratorPersonaDef>
```

---

## ?? 制作 blink.png 和 speaking.png

### 方案 1: 基于 base.png 制作（推荐）

#### 制作 blink.png（闭眼）

1. 复制 `base.png`
2. 修改眼睛区域为闭眼
3. 其他部分保持不变
4. 保存为 `blink.png`

#### 制作 speaking.png（说话）

1. 复制 `base.png`
2. 修改嘴巴区域为张开
3. 其他部分保持不变
4. 保存为 `speaking.png`

### 方案 2: 暂时不制作（使用回退）

如果暂时不制作这些纹理，系统会自动回退到 `base.png`：

```
眨眼触发 → blink.png 不存在 → 使用 base.png
说话时   → speaking.png 不存在 → 使用 base.png
```

**效果：**
- ? 不会崩溃
- ? 能正常运行
- ? 但没有动画效果

---

## ?? 动画系统工作流程

### 完整流程

```
[ base.png - 中性表情 ]
        ↓
    (眨眼触发 - 每3-6秒)
        ↓
[ blink.png - 闭眼 ] (0.15秒)
        ↓
[ base.png - 恢复中性 ]
        ↓
    (TTS 播放)
        ↓
[ speaking.png - 说话 ]
        ↓
    (TTS 结束)
        ↓
[ base.png - 恢复中性 ]
```

### 优先级

```
眨眼 (blink.png) > 说话 (speaking.png) > 中性 (base.png)
```

---

## ?? 验证步骤

### 1. 检查现有文件

```powershell
# 检查 base.png 是否存在
Test-Path "Textures/UI/Narrators/Avatars/Sideria/base.png"
# 预期：True

# 检查 blink.png 是否存在
Test-Path "Textures/UI/Narrators/Avatars/Sideria/blink.png"
# 如果返回 False，需要制作
```

### 2. 测试动画系统

**步骤：**
1. 编译项目
2. 启动游戏
3. 观察 AI 按钮

**预期效果：**
- ? 显示 `base.png`（中性表情）
- ? 如果有 `blink.png`：每 3-6 秒眨眼
- ? 如果有 `speaking.png`：TTS 播放时显示说话动画
- ? 如果没有：使用 `base.png` 作为回退

---

## ?? 制作纹理的建议

### blink.png（闭眼纹理）

**制作要点：**
- ? 基于 `base.png`
- ? 只修改眼睛区域
- ? 眼睛完全闭合
- ? 其他部分一致

**参考：**
```
??? 睁眼 (base.png)    → ??? 闭眼 (blink.png)
```

### speaking.png（说话纹理）

**制作要点：**
- ? 基于 `base.png`
- ? 只修改嘴巴区域
- ? 嘴巴微微张开
- ? 其他部分一致

**参考：**
```
?? 闭嘴 (base.png)     → ?? 张嘴 (speaking.png)
```

---

## ? 快速开始

### 立即可用（无需额外工作）

1. ? XML 配置已完成
2. ? `base.png` 已存在
3. ? 动画系统已实现
4. ? 编译即可使用

### 可选优化（提升体验）

1. ? 制作 `blink.png` - 添加眨眼动画
2. ? 制作 `speaking.png` - 添加说话动画

---

## ?? 文件清单

### 必需文件

| 文件 | 状态 | 说明 |
|------|------|------|
| `base.png` | ? 已存在 | 中性表情，512x512 |

### 可选文件

| 文件 | 状态 | 效果 |
|------|------|------|
| `blink.png` | ? 需要制作 | 眨眼动画（每3-6秒） |
| `speaking.png` | ? 需要制作 | 说话动画（TTS播放时） |

---

## ?? 部署步骤

### 1. 编译项目

```bash
dotnet build Source/TheSecondSeat/TheSecondSeat.csproj --configuration Release
```

### 2. 复制文件

```bash
# 复制 DLL
Copy-Item Source/TheSecondSeat/bin/Release/net472/TheSecondSeat.dll `
    D:/steam/steamapps/common/RimWorld/Mods/TheSecondSeat/Assemblies/

# 复制 Defs
Copy-Item Defs/NarratorPersonaDefs.xml `
    D:/steam/steamapps/common/RimWorld/Mods/TheSecondSeat/Defs/
```

### 3. 启动游戏测试

**预期效果：**
- ? AI 按钮显示 `base.png`
- ? 如果有 `blink.png`：自动眨眼
- ? 如果有 `speaking.png`：说话时动画
- ? 如果没有：使用 `base.png` 回退

---

## ?? 优点总结

### 使用现有 Avatars 文件夹的优点

| 优点 | 说明 |
|------|------|
| ? **零迁移成本** | 无需移动或重命名文件 |
| ? **立即可用** | `base.png` 已存在 |
| ? **结构清晰** | 文件组织合理 |
| ? **易于管理** | 所有头像在同一文件夹 |
| ? **向后兼容** | 不影响现有表情系统 |

---

## ?? 与表情系统的关系

### 两套系统并存

| 系统 | 路径 | 用途 |
|------|------|------|
| **动画系统** | `Avatars/Sideria/` | AI 按钮、对话窗口 |
| **表情系统** | `9x16/Expressions/Sideria/` | 全身立绘显示 |

**特点：**
- ? 互不冲突
- ? 可以同时使用
- ? 各自独立

---

**配置完成！** ?  
**版本：** v1.6.14  
**状态：** 可以直接编译和测试

现在只需要：
1. 编译项目
2. （可选）制作 `blink.png` 和 `speaking.png`
3. 启动游戏测试

_The Second Seat Mod Team_
