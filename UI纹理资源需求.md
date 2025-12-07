# ?? AI 叙事者 UI 纹理资源需求

## ?? 需要的纹理文件

### 1. 按钮图标（左侧）
**文件路径**：`Textures/UI/NarratorButton.png`

**设计要求**：
- **尺寸**：256x256 像素（高分辨率）
- **风格**：科技感、赛博朋克
- **元素**：
  - 六边形边框
  - 中心圆形区域
  - 电路纹路
  - AI 核心图案
- **颜色**：青色/绿色发光效果
- **格式**：PNG with Alpha

**参考设计**：
```
┌─────────────────────┐
│  ╱─────────────╲    │
│ ╱  ┌───────┐   ╲   │
│╱   │ ◎  AI │    ╲  │
││    └───────┘    ││  │
│╲               ╱   │
│ ╲─────────────╱    │
└─────────────────────┘
```

---

### 2. 状态面板背景
**文件路径**：`Textures/UI/NarratorPanel.png`

**设计要求**：
- **尺寸**：512x512 像素
- **风格**：科技面板、全息投影感
- **元素**：
  - 圆角矩形外框
  - 内部网格纹理
  - 发光边缘
  - 扫描线效果
- **颜色**：深色背景 + 青色/绿色边框
- **透明度**：半透明背景（Alpha 0.8）

---

### 3. 状态指示器
**文件路径**：`Textures/UI/StatusIndicators.png`

**包含元素**：
- **ONLINE 指示灯**：绿色圆点
- **SYNC 环形进度条**：青色圆环
- **LINK 齿轮图标**：绿色齿轮
- **ERROR 警告图标**：红色三角

**尺寸**：256x256 像素（包含所有图标）

---

## ?? 颜色规范

### 主题色
```css
/* 背景色 */
--bg-dark: rgba(20, 30, 40, 0.95)
--bg-panel: rgba(40, 50, 60, 0.9)

/* 高亮色 */
--glow-cyan: #00E5FF (青色发光)
--glow-green: #00FF88 (绿色发光)
--glow-red: #FF3366 (红色警告)

/* 文字色 */
--text-primary: #00E5FF
--text-secondary: #88DDFF
--text-warning: #FF6666
```

---

## ?? UI 布局规范

### 按钮悬浮面板
```
┌─────────────────────────────────────┐
│  NARRATOR.OS                        │ ← 标题
├─────────────────────────────────────┤
│  ● ONLINE                           │ ← 状态指示
│                                     │
│  ┌─────────┐                        │
│  │ SYNC:   │  98%                   │ ← 同步状态
│  └─────────┘                        │
│                                     │
│  ┌─────────┐   ERROR:               │
│  │  ?? LINK │   TIMEOUT             │ ← 连接状态
│  └─────────┘                        │
│                                     │
├─────────────────────────────────────┤
│  ████████████??????? 70%           │ ← 好感度条
└─────────────────────────────────────┘
```

---

## ??? 快速生成方案

### 方案 1：使用现有 RimWorld 纹理
如果暂时没有自定义纹理，可以使用：
```csharp
// 按钮图标
ContentFinder<Texture2D>.Get("UI/Widgets/ButtonBGAtlas", false)
ContentFinder<Texture2D>.Get("UI/Commands/DesirePower", false)

// 状态图标
ContentFinder<Texture2D>.Get("UI/Icons/Medical/Health", false)
TexButton.Info // 信息图标
```

### 方案 2：代码绘制（无需纹理）
```csharp
// 使用 GUI 原语绘制
Widgets.DrawBoxSolid(rect, color); // 背景
Widgets.DrawBox(rect, 2); // 边框
Widgets.DrawCircle(center, radius, color); // 圆形
Widgets.DrawLine(from, to, color, width); // 线条
```

---

## ?? 翻译键需求

### 状态面板翻译
```xml
<!-- 中文 -->
<TSS_StatusPanel_Title>NARRATOR.OS</TSS_StatusPanel_Title>
<TSS_StatusPanel_Online>在线</TSS_StatusPanel_Online>
<TSS_StatusPanel_Offline>离线</TSS_StatusPanel_Offline>
<TSS_StatusPanel_Sync>同步</TSS_StatusPanel_Sync>
<TSS_StatusPanel_Link>连接</TSS_StatusPanel_Link>
<TSS_StatusPanel_Error>错误</TSS_StatusPanel_Error>
<TSS_StatusPanel_Timeout>超时</TSS_StatusPanel_Timeout>
<TSS_StatusPanel_Favorability>好感度</TSS_StatusPanel_Favorability>

<!-- 英文 -->
<TSS_StatusPanel_Title>NARRATOR.OS</TSS_StatusPanel_Title>
<TSS_StatusPanel_Online>ONLINE</TSS_StatusPanel_Online>
<TSS_StatusPanel_Offline>OFFLINE</TSS_StatusPanel_Offline>
<TSS_StatusPanel_Sync>SYNC</TSS_StatusPanel_Sync>
<TSS_StatusPanel_Link>LINK</TSS_StatusPanel_Link>
<TSS_StatusPanel_Error>ERROR</TSS_StatusPanel_Error>
<TSS_StatusPanel_Timeout>TIMEOUT</TSS_StatusPanel_Timeout>
<TSS_StatusPanel_Favorability>FAVORABILITY</TSS_StatusPanel_Favorability>
```

---

## ?? 实现优先级

### 阶段 1：基础功能（无纹理）?
- 使用 GUI 原语绘制
- 纯代码实现科技感效果
- 基础颜色和布局

### 阶段 2：简单纹理
- 按钮图标（可用占位符）
- 基础背景纹理
- 状态指示器

### 阶段 3：高级视觉效果
- 动画效果
- 粒子效果
- 全息投影感

---

## ?? 临时占位符方案

如果暂时没有美术资源，使用：

1. **按钮图标**：使用 RimWorld 内置的 `TexButton.Info`
2. **面板背景**：代码绘制半透明矩形
3. **状态指示**：使用颜色块 + 文字

**代码示例**：
```csharp
// 绘制科技感面板（纯代码）
void DrawTechPanel(Rect rect)
{
    // 背景
    GUI.color = new Color(0.1f, 0.2f, 0.3f, 0.95f);
    Widgets.DrawBoxSolid(rect, GUI.color);
    GUI.color = Color.white;
    
    // 发光边框
    GUI.color = new Color(0f, 0.9f, 1f, 0.8f); // 青色
    Widgets.DrawBox(rect, 2);
    GUI.color = Color.white;
    
    // 标题
    Text.Font = GameFont.Medium;
    Text.Anchor = TextAnchor.UpperCenter;
    GUI.color = new Color(0f, 0.9f, 1f);
    Widgets.Label(new Rect(rect.x, rect.y + 10, rect.width, 30), "NARRATOR.OS");
    GUI.color = Color.white;
    Text.Anchor = TextAnchor.UpperLeft;
    Text.Font = GameFont.Small;
}
```

---

**当前优先级**：先实现代码绘制版本（无需纹理），功能完整后再添加美术资源。
