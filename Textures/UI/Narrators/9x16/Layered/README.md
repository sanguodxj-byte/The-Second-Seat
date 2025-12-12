# 分层立绘系统 - 纹理资源文件夹

## ? **v1.6.18 新特性：表情部件拆分**

本文件夹用于存放**分层立绘**的纹理资源。立绘由多个透明PNG图层叠加合成，支持动态切换表情、服装、配饰等。

**新特性：** 表情不再是完整图片，而是拆分为 **eyes（眼睛）**、**mouth（嘴巴）**、**flush（脸红）** 三个独立部件进行拼接！

---

## ?? **文件夹结构**

```
Layered/
└── {PersonaName}/         ← 人格名称文件夹（如 Sideria）
    ├── base_body.png      ← ? 底图（身体+默认表情）
    ├── background.png     ← 背景（可选）
    ├── outfit_default.png ← 默认服装
    ├── hair.png           ← 头发层
    ├── accessories.png    ← 配饰层
    └── 表情部件（拆分）：
        ├── neutral_eyes.png    ← 默认表情：眼睛
        ├── neutral_mouth.png   ← 默认表情：嘴巴
        ├── neutral_flush.png   ← 默认表情：脸红（可选）
        ├── happy_eyes.png      ← 开心：眼睛
        ├── happy_mouth.png     ← 开心：嘴巴
        ├── happy_flush.png     ← 开心：脸红（可选）
        ├── sad_eyes.png
        ├── sad_mouth.png
        ├── angry_eyes.png
        ├── angry_mouth.png
        └── ...
```

---

## ?? **核心概念：base_body 作为底图**

### **旧系统（v1.6.17）：**
```
完整立绘 = 身体层 + 完整表情层
```

### **新系统（v1.6.18）：**
```
完整立绘 = base_body（身体+默认表情） + 表情部件（eyes + mouth + flush）
```

**优势：**
- 只需要绘制1个底图（`base_body.png`）
- 每个表情只需要2-3个小部件（眼睛、嘴巴、脸红）
- 文件大小减少 ~50%
- 支持独立的眨眼/张嘴动画

---

## ?? **图层叠加顺序（Priority）**

```
Priority  图层ID        文件名                      说明
--------  ----------   -------------------------  ------------------
0         base_body    base_body.png              ? 底图（身体+默认表情）
5         background   background.png             背景（可选）
10        outfit       outfit_{outfit}.png        服装层
20        flush        {expression}_flush.png     脸红层（可选）
30        eyes         {expression}_eyes.png      眼睛层（必需）
40        mouth        {expression}_mouth.png     嘴巴层（必需）
50        hair         hair.png                   头发层
60        accessories  accessories.png            配饰层
70        fx           fx.png                     特效层（可选）
```

**说明：**
- Priority 数字越小，图层越靠下（底层）
- `base_body` Priority=0，是最底层
- 表情部件（flush/eyes/mouth）覆盖在 `base_body` 之上

---

## ?? **表情部件命名规则**

### **格式：`{expression}_{part}.png`**

| 表情类型 | 眼睛部件 | 嘴巴部件 | 脸红部件（可选） |
|---------|---------|---------|----------------|
| Neutral | `neutral_eyes.png` | `neutral_mouth.png` | `neutral_flush.png` |
| Happy   | `happy_eyes.png` | `happy_mouth.png` | `happy_flush.png` |
| Sad     | `sad_eyes.png` | `sad_mouth.png` | `sad_flush.png` |
| Angry   | `angry_eyes.png` | `angry_mouth.png` | `angry_flush.png` |
| Surprised | `surprised_eyes.png` | `surprised_mouth.png` | `surprised_flush.png` |
| Confused | `confused_eyes.png` | `confused_mouth.png` | - |
| Smug    | `smug_eyes.png` | `smug_mouth.png` | - |
| Shy     | `shy_eyes.png` | `shy_mouth.png` | `shy_flush.png` |

---

## ?? **图片尺寸规范**

### **标准分辨率：1024x1572（9:16比例）**

| 图层类型 | 尺寸 | 格式 | 透明度 | 文件大小（参考） |
|---------|------|------|-------|----------------|
| base_body | 1024x1572 | PNG | 背景透明 | ~300KB |
| background | 1024x1572 | PNG | 可不透明 | ~200KB |
| outfit | 1024x1572 | PNG | 背景透明 | ~150KB |
| eyes | 1024x1572 | PNG | **只有眼睛部分，其余透明** | ~50KB |
| mouth | 1024x1572 | PNG | **只有嘴巴部分，其余透明** | ~30KB |
| flush | 1024x1572 | PNG | **只有脸颊部分，其余透明** | ~20KB |
| hair | 1024x1572 | PNG | 背景透明 | ~200KB |
| accessories | 1024x1572 | PNG | 背景透明 | ~150KB |

**重要：** 所有图层必须使用相同的画布尺寸（1024x1572），否则对齐会出错！

---

## ?? **制作指南**

### **Step 1: 准备底图**
1. 创建 **`base_body.png`**（1024x1572）
   - 包含：完整身体 + 默认表情（眼睛+嘴巴）
   - 背景：透明
   - 用途：作为所有表情的基础

### **Step 2: 制作表情部件**
对于每个表情（如 `happy`）：

#### **2.1 眼睛层 `happy_eyes.png`**
- **只绘制眼睛部分**
- 其余区域保持透明
- 大小：1024x1572（与底图一致）
- 位置：与 `base_body` 中的眼睛位置对齐

#### **2.2 嘴巴层 `happy_mouth.png`**
- **只绘制嘴巴部分**
- 其余区域保持透明
- 大小：1024x1572
- 位置：与 `base_body` 中的嘴巴位置对齐

#### **2.3 脸红层 `happy_flush.png`**（可选）
- 绘制脸颊红晕
- 使用柔和的粉色/红色
- 建议使用 50-70% 不透明度
- 大小：1024x1572

---

## ??? **Photoshop/GIMP 工作流程**

### **推荐图层结构：**
```
Photoshop 项目：
├── 底图组 (base_body)
│   ├── 身体
│   ├── 默认眼睛
│   └── 默认嘴巴
├── Happy 表情组
│   ├── happy_flush (脸红)
│   ├── happy_eyes (眼睛)
│   └── happy_mouth (嘴巴)
├── Sad 表情组
│   ├── sad_eyes
│   └── sad_mouth
└── ...
```

### **导出步骤：**
1. **导出底图：**
   - 只显示"底图组"
   - 文件 → 导出 → 存储为PNG → `base_body.png`

2. **导出表情部件：**
   - 对于每个表情（如 Happy）：
     - 只显示 `happy_flush` 图层 → 导出 `happy_flush.png`
     - 只显示 `happy_eyes` 图层 → 导出 `happy_eyes.png`
     - 只显示 `happy_mouth` 图层 → 导出 `happy_mouth.png`

3. **检查透明度：**
   - 在导出设置中确保"透明度"已勾选
   - 背景不应该是白色或其他颜色

---

## ?? **完整示例（Sideria）**

```
Layered/Sideria/
├── base_body.png           (1024x1572, 300KB) ← 底图
├── hair.png                (1024x1572, 200KB)
├── accessories.png         (1024x1572, 150KB)
└── 表情部件：
    ├── neutral_eyes.png    (1024x1572, 50KB)
    ├── neutral_mouth.png   (1024x1572, 30KB)
    ├── happy_eyes.png      (1024x1572, 50KB)
    ├── happy_mouth.png     (1024x1572, 30KB)
    ├── happy_flush.png     (1024x1572, 20KB)
    ├── sad_eyes.png        (1024x1572, 50KB)
    ├── sad_mouth.png       (1024x1572, 30KB)
    ├── angry_eyes.png      (1024x1572, 50KB)
    ├── angry_mouth.png     (1024x1572, 30KB)
    └── shy_eyes.png
    └── shy_mouth.png
    └── shy_flush.png
```

**总大小：** ~1.5MB（8个表情）  
**对比完整立绘：** ~3MB（8个表情）

---

## ?? **系统如何工作**

### **1. 加载底图**
```csharp
Texture2D baseBody = LoadTexture("UI/Narrators/9x16/Layered/Sideria/base_body.png");
```

### **2. 加载表情部件**
```csharp
// 当前表情：Happy
Texture2D happyFlush = LoadTexture("UI/Narrators/9x16/Layered/Sideria/happy_flush.png");
Texture2D happyEyes = LoadTexture("UI/Narrators/9x16/Layered/Sideria/happy_eyes.png");
Texture2D happyMouth = LoadTexture("UI/Narrators/9x16/Layered/Sideria/happy_mouth.png");
```

### **3. 合成最终立绘**
```
最终立绘 = baseBody (底图)
          + happyFlush (脸红，如果有)
          + happyEyes (眼睛)
          + happyMouth (嘴巴)
          + hair (头发)
          + accessories (配饰)
```

**结果：** 完整的 Sideria Happy 表情立绘！

---

## ?? **动画支持**

### **眨眼动画（BlinkAnimationSystem）**
- 独立控制 `eyes` 图层
- 不影响 `mouth` 和 `flush`
- 眨眼时替换为 `{expression}_eyes_closed.png`

### **张嘴动画（MouthAnimationSystem）**
- 独立控制 `mouth` 图层
- 不影响 `eyes` 和 `flush`
- 说话时替换为 `{expression}_mouth_open.png`

### **表情切换**
- 同时更新 `flush`、`eyes`、`mouth` 三个图层
- 平滑过渡，无闪烁

---

## ?? **常见问题**

### **Q1: 部件图层对不齐怎么办？**
**A:** 确保所有PNG文件尺寸一致（1024x1572），并在Photoshop中使用参考线标记眼睛和嘴巴位置。

### **Q2: 脸红层不显示？**
**A:** 检查文件名是否正确（`{expression}_flush.png`），并确保不透明度足够（建议50-70%）。

### **Q3: 表情切换时闪烁？**
**A:** 这是因为纹理加载延迟。系统会自动缓存纹理，第二次切换时不会闪烁。

### **Q4: 可以不使用脸红层吗？**
**A:** 可以！脸红层是可选的。如果文件不存在，系统会跳过该图层。

### **Q5: 可以混搭不同表情的部件吗？**
**A:** 理论上可以！但需要修改代码逻辑。目前系统会加载同一表情的所有部件。

---

## ?? **相关文档**

- [分层立绘表情拆分系统-v1.6.18.md](../../../分层立绘表情拆分系统-v1.6.18.md) - 详细实现文档
- [分层立绘系统-快速参考.md](../../../分层立绘系统-快速参考.md)
- [眨眼和张嘴动画-快速参考-v1.6.18.md](../../../眨眼和张嘴动画-快速参考-v1.6.18.md)

---

**版本：** v1.6.18  
**最后更新：** 2024  
**路径：** `Textures/UI/Narrators/9x16/Layered/README.md`
