# Materials 部署 - 快速参考

## ?? 新增功能

**版本：** v1.6.15  
**新增：** Materials 文件夹自动部署

---

## ?? 快速部署

```powershell
.\Deploy-Animation-System.ps1
```

**输出：**
```
=== 步骤 6/6: 复制 Materials 文件 ===
? Materials 已复制: 3 个文件
```

---

## ?? 目录结构

### 源目录
```
Materials/
└── UI/Narrator/
    └── NarratorButton.xml
```

### 目标目录
```
RimWorld/Mods/TheSecondSeat/Materials/
└── UI/Narrator/
    └── NarratorButton.xml
```

---

## ?? Materials 示例

```xml
<?xml version="1.0" encoding="utf-8"?>
<Defs>
  <MaterialDef>
    <defName>UI_NarratorButton</defName>
    <texturePath>UI/Narrator/NarratorButton</texturePath>
    <shader>UI/Default</shader>
  </MaterialDef>
</Defs>
```

---

## ?? 完整步骤

```
1/6: 检查环境
2/6: 编译项目
3/6: 复制 DLL
4/6: 复制 Defs
5/6: 复制 Textures
6/6: 复制 Materials     ? 新增
```

---

## ?? 验证

```powershell
Get-ChildItem "...\Materials" -Recurse -Filter "*.xml"
```

---

## ?? 使用场景

| 场景 | 需要 Materials |
|------|---------------|
| 简单纹理 | ? |
| 发光效果 | ? |
| 透明叠加 | ? |

---

**快速参考完成！** ?

_The Second Seat Mod Team_
