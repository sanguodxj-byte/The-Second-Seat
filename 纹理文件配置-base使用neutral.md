# 纹理文件配置 - base 使用 neutral

## ?? 配置说明

### 纹理文件映射

| XML 配置 | 实际使用的纹理文件 | 说明 |
|---------|------------------|------|
| `portraitPath` | `neutral.png` | 基础立绘 = 中性表情 |
| `portraitPathBlink` | `blink.png` | 闭眼纹理 |
| `portraitPathSpeaking` | `speaking.png` | 张嘴纹理 |

---

## ?? 文件组织

### 推荐目录结构

```
Textures/UI/Narrators/9x16/Sideria/
├── neutral.png     (必需) - 中性表情（也是基础立绘）
├── blink.png       (可选) - 闭眼纹理
├── speaking.png    (可选) - 张嘴纹理
├── happy.png       (可选) - 开心表情
├── sad.png         (可选) - 悲伤表情
├── angry.png       (可选) - 生气表情
└── ...             (其他表情)
```

### 为什么这样组织？

1. ? **neutral.png 是基础** - 默认状态就是中性表情
2. ? **复用性高** - 其他系统也可以直接使用 neutral.png
3. ? **语义清晰** - 文件名直接表明用途

---

## ?? XML 配置

### Defs/NarratorPersonaDefs.xml

```xml
<TheSecondSeat.PersonaGeneration.NarratorPersonaDef>
    <defName>Sideria_Default</defName>
    <narratorName>Sideria</narratorName>
    
    <!-- ? 基础立绘：使用 neutral.png -->
    <portraitPath>UI/Narrators/9x16/Sideria/neutral</portraitPath>
    
    <!-- ? 眨眼纹理 -->
    <portraitPathBlink>UI/Narrators/9x16/Sideria/blink</portraitPathBlink>
    
    <!-- ? 说话纹理 -->
    <portraitPathSpeaking>UI/Narrators/9x16/Sideria/speaking</portraitPathSpeaking>
</TheSecondSeat.PersonaGeneration.NarratorPersonaDef>
```

---

## ?? 动画系统行为

### 状态切换流程

```
[ 中性表情 neutral.png ]
        ↓
    (眨眼触发)
        ↓
[ 闭眼 blink.png ] (0.15秒)
        ↓
[ 中性表情 neutral.png ]
        ↓
    (TTS播放)
        ↓
[ 说话 speaking.png ]
        ↓
    (TTS结束)
        ↓
[ 中性表情 neutral.png ]
```

### 优先级

```
眨眼 (blink.png) > 说话 (speaking.png) > 中性 (neutral.png)
```

---

## ?? 纹理制作建议

### 1. neutral.png（中性表情）

**特征：**
- ? 面部放松，无明显情绪
- ? 眼睛正常睁开
- ? 嘴巴闭合或微张
- ? 适合长时间显示

**参考：**
```
?? 中性表情
眼睛：正常睁开
嘴巴：闭合
表情：平静
```

### 2. blink.png（闭眼）

**特征：**
- ? 基于 neutral.png
- ? 只修改眼睛部分为闭合
- ? 其他部分保持一致

**制作方法：**
1. 复制 neutral.png
2. 修改眼睛区域为闭眼
3. 保存为 blink.png

### 3. speaking.png（说话）

**特征：**
- ? 基于 neutral.png
- ? 只修改嘴巴部分为张开
- ? 其他部分保持一致

**制作方法：**
1. 复制 neutral.png
2. 修改嘴巴区域为张开
3. 保存为 speaking.png

---

## ?? 迁移现有文件

### 如果你已有 base.png

**方案 1: 重命名**
```powershell
# 将 base.png 重命名为 neutral.png
Rename-Item "Textures/UI/Narrators/9x16/Sideria/base.png" "neutral.png"
```

**方案 2: 复制**
```powershell
# 保留 base.png，复制为 neutral.png
Copy-Item "Textures/UI/Narrators/9x16/Sideria/base.png" "neutral.png"
```

---

## ?? 更新 XML 配置

### 修改步骤

1. 打开 `Defs/NarratorPersonaDefs.xml`
2. 找到 Sideria 的配置
3. 修改 `portraitPath` 为 `neutral`

```xml
<!-- ? 旧配置 -->
<portraitPath>UI/Narrators/9x16/Sideria/base</portraitPath>

<!-- ? 新配置 -->
<portraitPath>UI/Narrators/9x16/Sideria/neutral</portraitPath>
```

---

## ? 验证清单

### 文件验证

- [ ] `neutral.png` 文件存在
- [ ] `blink.png` 文件存在（可选）
- [ ] `speaking.png` 文件存在（可选）
- [ ] 所有文件尺寸一致（512x512 或 1024x1024）
- [ ] 所有文件格式为 PNG

### 配置验证

- [ ] XML 中 `portraitPath` 指向 `neutral`
- [ ] XML 中 `portraitPathBlink` 指向 `blink`
- [ ] XML 中 `portraitPathSpeaking` 指向 `speaking`

### 功能验证

- [ ] 游戏启动后显示中性表情
- [ ] 每 3-6 秒触发眨眼
- [ ] TTS 播放时显示说话动画
- [ ] 所有动画切换流畅

---

## ?? 优点总结

### 使用 neutral.png 作为基础的优点

| 优点 | 说明 |
|------|------|
| ? **语义清晰** | 文件名直接表明是中性表情 |
| ? **复用性高** | 可以被表情系统复用 |
| ? **易于理解** | 新人一看就知道是默认状态 |
| ? **符合惯例** | 大多数表情系统都这样命名 |

---

## ?? 快速部署

### 一键配置脚本

我会为你创建一个 PowerShell 脚本来自动完成配置。

---

**配置指南完成！** ?  
**版本：** v1.6.14  
**状态：** 可以开始部署

_The Second Seat Mod Team_
