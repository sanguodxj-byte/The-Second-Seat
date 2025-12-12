# Materials 文件夹部署 v1.6.15

## ?? 概述

部署脚本现在支持自动部署 **Materials（材质）文件夹**。

---

## ?? Materials 文件夹

### 作用

Materials 文件夹用于存放 RimWorld 的材质定义文件（`.xml`），用于：

- ? 自定义纹理渲染效果
- ? 控制纹理的着色器（Shader）
- ? 设置纹理的混合模式
- ? 配置纹理的透明度和遮罩

### 典型用途

```
Materials/
├── UI/
│   ├── Narrator/
│   │   └── NarratorButton.xml       # AI按钮材质
│   └── StatusIcons/
│       └── StatusIcon.xml           # 状态图标材质
└── Effects/
    └── Glow.xml                     # 发光效果材质
```

---

## ?? 部署流程

### 步骤 6: 复制 Materials 文件

```
[ 源目录 ]
Materials/
└── UI/Narrator/NarratorButton.xml
    ↓
[ 复制到 ]
RimWorld/Mods/TheSecondSeat/Materials/
└── UI/Narrator/NarratorButton.xml
```

**特点：**
- ? 自动创建目录结构
- ? 递归复制所有 `.xml` 文件
- ? 保持相对路径
- ? 统计复制数量

---

## ?? 部署输出

### 成功示例

```powershell
=== 步骤 6/6: 复制 Materials 文件 ===
? Materials 已复制: 3 个文件
```

### 空目录

```powershell
=== 步骤 6/6: 复制 Materials 文件 ===
? Materials 目录为空
```

### 目录不存在

```powershell
=== 步骤 6/6: 复制 Materials 文件 ===
? Materials 目录不存在
```

---

## ?? Materials 文件示例

### AI 按钮材质

**文件：** `Materials/UI/Narrator/NarratorButton.xml`

```xml
<?xml version="1.0" encoding="utf-8"?>
<Defs>
  <MaterialDef>
    <defName>UI_NarratorButton</defName>
    <texturePath>UI/Narrator/NarratorButton</texturePath>
    <shader>UI/Default</shader>
    <renderQueue>3000</renderQueue>
  </MaterialDef>
</Defs>
```

**说明：**
- `texturePath`: 对应的纹理路径
- `shader`: 使用的着色器
- `renderQueue`: 渲染队列（控制绘制顺序）

---

### 发光效果材质

**文件：** `Materials/Effects/Glow.xml`

```xml
<?xml version="1.0" encoding="utf-8"?>
<Defs>
  <MaterialDef>
    <defName>Effect_Glow</defName>
    <texturePath>Effects/Glow</texturePath>
    <shader>Transparent/Glow</shader>
    <renderQueue>3100</renderQueue>
    <color>(1.0, 1.0, 1.0, 0.8)</color>
  </MaterialDef>
</Defs>
```

**特点：**
- 透明着色器
- 自定义颜色和透明度
- 较高的渲染队列（绘制在最上层）

---

## ?? 完整目录结构

### 源目录（项目）

```
The Second Seat/
├── Materials/
│   ├── UI/
│   │   ├── Narrator/
│   │   │   └── NarratorButton.xml
│   │   └── StatusIcons/
│   │       └── StatusIcon.xml
│   └── Effects/
│       └── Glow.xml
├── Textures/
│   └── ...
└── Defs/
    └── ...
```

### 目标目录（Mod）

```
RimWorld/Mods/TheSecondSeat/
├── Materials/                          ? 新增
│   ├── UI/
│   │   ├── Narrator/
│   │   │   └── NarratorButton.xml    ? 已复制
│   │   └── StatusIcons/
│   │       └── StatusIcon.xml        ? 已复制
│   └── Effects/
│       └── Glow.xml                  ? 已复制
├── Textures/
│   └── ...
└── Defs/
    └── ...
```

---

## ?? 使用 Materials

### 在代码中引用

```csharp
// 加载材质
Material buttonMaterial = MaterialPool.MatFrom("UI/Narrator/NarratorButton");

// 使用材质绘制
Graphics.DrawTexture(rect, texture, buttonMaterial);
```

### 在 XML 中引用

```xml
<graphicData>
  <texPath>UI/Narrator/NarratorButton</texPath>
  <graphicClass>Graphic_Single</graphicClass>
  <shaderType>CutoutComplex</shaderType>
  <materialPath>UI/Narrator/NarratorButton</materialPath>
</graphicData>
```

---

## ?? 验证部署

### 检查命令

```powershell
# 检查 Materials 文件
Get-ChildItem "D:\steam\steamapps\common\RimWorld\Mods\TheSecondSeat\Materials" -Recurse -Filter "*.xml"
```

### 预期输出

```
Directory: ...\Materials\UI\Narrator

Mode                 LastWriteTime         Length Name
----                 -------------         ------ ----
-a----         12/9/2023   3:00 PM           1234 NarratorButton.xml

Directory: ...\Materials\Effects

Mode                 LastWriteTime         Length Name
----                 -------------         ------ ----
-a----         12/9/2023   3:00 PM            567 Glow.xml
```

---

## ?? 完整部署流程

### 步骤总览

```
1/6: 检查环境
2/6: 编译项目
3/6: 复制 DLL
4/6: 复制 Defs（保护用户数据）
5/6: 复制 Textures（保护立绘）
6/6: 复制 Materials                    ? 新增
```

---

## ?? 快速部署

### 完整部署

```powershell
.\Deploy-Animation-System.ps1
```

**结果：**
```
? DLL 已复制
? Defs 已复制: 2 个文件
? Textures 已复制: 46 个文件
? Materials 已复制: 3 个文件     ? 新增
```

---

## ?? 注意事项

### 1. Materials 文件格式

**正确的格式：**
```xml
<?xml version="1.0" encoding="utf-8"?>
<Defs>
  <MaterialDef>
    <defName>...</defName>
    <texturePath>...</texturePath>
    <shader>...</shader>
  </MaterialDef>
</Defs>
```

**常见错误：**
- ? 缺少 XML 声明
- ? 缺少 `<Defs>` 根节点
- ? `texturePath` 路径错误

---

### 2. 材质与纹理的关系

```
纹理文件（Textures）
    ↓
材质定义（Materials）
    ↓
代码引用（MaterialPool）
    ↓
UI 渲染
```

**说明：**
- 纹理是图片文件（`.png`、`.jpg`）
- 材质是定义文件（`.xml`），指定如何使用纹理
- 代码通过 `MaterialPool.MatFrom()` 加载材质

---

### 3. 可选性

**Materials 是可选的：**
- ? 没有 Materials 文件夹：使用 RimWorld 默认材质
- ? 有 Materials 文件夹：使用自定义材质

**建议：**
- 简单纹理：不需要 Materials
- 特殊效果：创建 Materials（发光、透明等）

---

## ? 总结

### 新增功能

```
? Materials 文件夹自动部署
? 递归复制所有 .xml 文件
? 保持目录结构
? 统计部署数量
```

### 使用建议

| 场景 | 是否需要 Materials |
|------|-------------------|
| 简单 UI 纹理 | ? 不需要 |
| 发光效果 | ? 需要 |
| 透明叠加 | ? 需要 |
| 自定义着色器 | ? 需要 |

---

**Materials 部署完成！** ?  
**版本：** v1.6.15  
**状态：** 可选功能，按需使用

_The Second Seat Mod Team_
