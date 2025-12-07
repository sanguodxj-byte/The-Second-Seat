# ?? 难度模式配图说明

## ?? 文件结构

```
Textures/UI/DifficultyMode/
├── assistant_icon.png        # 助手模式图标（左侧）
├── opponent_icon.png         # 对弈者模式图标（右侧）
├── assistant_large.png       # 助手模式大图（选择界面）
├── opponent_large.png        # 对弈者模式大图（选择界面）
├── mode_selector_bg.png      # 选择器背景（可选）
└── README.md                 # 本文件
```

---

## ??? 图片规格

### 1. 模式图标（设置UI按钮上方）
**用途**：显示在设置界面的难度选择按钮上方

| 文件名 | 尺寸 | 格式 | 说明 |
|--------|------|------|------|
| `assistant_icon.png` | 64x64 | PNG | 助手模式小图标 |
| `opponent_icon.png` | 64x64 | PNG | 对弈者模式小图标 |

### 2. 模式大图（点开选择界面）
**用途**：点击难度选择后，左右对称展示

| 文件名 | 尺寸 | 格式 | 说明 |
|--------|------|------|------|
| `assistant_large.png` | 256x256 | PNG | 助手模式大图（左侧） |
| `opponent_large.png` | 256x256 | PNG | 对弈者模式大图（右侧） |

### 3. 背景（可选）
| 文件名 | 尺寸 | 格式 | 说明 |
|--------|------|------|------|
| `mode_selector_bg.png` | 600x300 | PNG | 选择界面背景 |

---

## ?? 设计建议

### 助手模式图标 (assistant)
**风格**：温暖、支持、友好
- 颜色：蓝色/绿色/金色
- 元素建议：
  - ?? 握手/援助之手
  - ?? 灯泡（建议）
  - ??? 盾牌（保护）
  - ? 星星/光环（支持）
  - ?? 清单/指南

### 对弈者模式图标 (opponent)
**风格**：挑战、神秘、战略
- 颜色：红色/紫色/深蓝
- 元素建议：
  - ?? 交叉剑
  - ?? 骰子（随机事件）
  - ??? 眼睛（观察）
  - ?? 棋子
  - ? 闪电（挑战）

---

## ?? 布局示意图

### 设置界面（RadioButton上方）
```
┌─────────────────────────────────────────────────────┐
│  AI难度模式                                          │
│                                                     │
│    [assistant_icon]        [opponent_icon]          │
│         64x64                  64x64                │
│                                                     │
│    ○ 助手模式              ○ 对弈者模式              │
│    无条件支持              挑战平衡                  │
└─────────────────────────────────────────────────────┘
```

### 点开选择界面（左右对称）
```
┌─────────────────────────────────────────────────────────────┐
│                     选择AI难度模式                           │
│                                                             │
│  ┌───────────────────┐      ┌───────────────────┐          │
│  │                   │      │                   │          │
│  │  assistant_large  │      │  opponent_large   │          │
│  │     256x256       │      │     256x256       │          │
│  │                   │      │                   │          │
│  └───────────────────┘      └───────────────────┘          │
│                                                             │
│       助手模式                    对弈者模式                 │
│    无条件支持你                  考验你的策略                │
│    主动提供建议                  控制事件难度                │
│                                                             │
│     [选择此模式]                 [选择此模式]                │
└─────────────────────────────────────────────────────────────┘
```

---

## ?? 命名规范

### ? 正确命名
```
assistant_icon.png      ? 全小写，下划线分隔
opponent_large.png      ? 全小写，下划线分隔
```

### ? 错误命名
```
Assistant_Icon.png      ? 大写字母
assistant-icon.png      ? 连字符
assistantIcon.png       ? 驼峰命名
```

---

## ?? 代码集成位置

### 设置UI（ModSettings.cs）
```csharp
// 在 DoSettingsWindowContents 方法中
// 位置：RadioButton 上方

// 加载图标
Texture2D assistantIcon = ContentFinder<Texture2D>.Get("UI/DifficultyMode/assistant_icon");
Texture2D opponentIcon = ContentFinder<Texture2D>.Get("UI/DifficultyMode/opponent_icon");

// 绘制图标（左右对称）
Rect iconRect = listing.GetRect(70f);
float iconSize = 64f;
float spacing = 100f;
float centerX = iconRect.center.x;

// 助手模式图标（左侧）
Rect assistantRect = new Rect(centerX - spacing - iconSize/2, iconRect.y, iconSize, iconSize);
GUI.DrawTexture(assistantRect, assistantIcon);

// 对弈者模式图标（右侧）
Rect opponentRect = new Rect(centerX + spacing - iconSize/2, iconRect.y, iconSize, iconSize);
GUI.DrawTexture(opponentRect, opponentIcon);
```

---

## ?? 文件放置

### 源码位置
```
C:\Users\Administrator\Desktop\rim mod\The Second Seat\Textures\UI\DifficultyMode\
├── assistant_icon.png
├── opponent_icon.png
├── assistant_large.png
├── opponent_large.png
└── README.md
```

### 游戏Mod位置（部署后）
```
D:\steam\steamapps\common\RimWorld\Mods\TheSecondSeat\Textures\UI\DifficultyMode\
├── assistant_icon.png
├── opponent_icon.png
├── assistant_large.png
└── opponent_large.png
```

---

## ? 检查清单

- [ ] 创建 `Textures/UI/DifficultyMode/` 文件夹
- [ ] 添加 `assistant_icon.png` (64x64)
- [ ] 添加 `opponent_icon.png` (64x64)
- [ ] 添加 `assistant_large.png` (256x256)
- [ ] 添加 `opponent_large.png` (256x256)
- [ ] 修改 `ModSettings.cs` 添加图标显示代码
- [ ] 运行 `.\Smart-Deploy.ps1` 部署
- [ ] 游戏内验证图标显示

---

## ?? 快速开始

### 1. 创建占位符（可选）
如果暂时没有设计好的图标，可以使用纯色占位符：

```powershell
# 创建简单的占位符脚本
# 运行后会生成基本的占位图片
.\Generate-DifficultyIcons.ps1
```

### 2. 使用现有素材
可以从以下来源获取图标：
- RimWorld 原版素材（`RimWorld\Data\Core\Textures\UI\`）
- 免费图标网站（flaticon.com, icons8.com）
- AI 生成（DALL-E, Midjourney）

---

**创建时间**: 2025-01-15  
**版本**: v1.0  
**状态**: ? 目录结构已创建
