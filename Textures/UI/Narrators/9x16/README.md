# 9:16 立绘文件夹结构说明

## ?? 文件夹结构

```
Textures/UI/Narrators/9x16/
├─ Cassandra/                    ← 人格文件夹
│  └─ base.png                   ← 基础立绘 (1080x1920 推荐)
│
├─ Phoebe/
│  └─ base.png
│
├─ Randy/
│  └─ base.png
│
└─ Expressions/                  ← 表情文件夹（新增）
   ├─ Cassandra/
   │  ├─ happy.png               ← 整图表情 (1080x1920)
   │  ├─ sad.png
   │  ├─ angry.png
   │  └─ layers/                 ← 分层表情（可选）
   │     ├─ eyes_happy.png
   │     ├─ eyes_sad.png
   │     ├─ mouth_smile.png
   │     └─ mouth_frown.png
   │
   ├─ Phoebe/
   │  └─ ...
   │
   └─ Randy/
      └─ ...
```

---

## ? 新特性：表情文件独立存放

### 为什么要分离？
- ? **选择立绘时清爽** - 只看到 `base.png`
- ? **文件组织清晰** - 立绘和表情分离
- ? **易于管理** - 添加新表情不影响立绘文件夹

### 旧结构（不推荐）
```
Cassandra/
├─ base.png
├─ happy.png         ← 干扰项
├─ sad.png           ← 干扰项
└─ angry.png         ← 干扰项
```

### 新结构（推荐）?
```
Cassandra/
└─ base.png          ← 只有基础立绘，清爽！

Expressions/Cassandra/
├─ happy.png
├─ sad.png
└─ angry.png
```

---

## ?? 文件规格

### 基础立绘 (base.png)
- **尺寸**: 1080x1920 (9:16 比例)
- **格式**: PNG (支持透明)
- **位置**: `9x16/{人格名}/base.png`

### 整图表情
- **尺寸**: 1080x1920 (与基础立绘相同)
- **格式**: PNG (支持透明)
- **位置**: `9x16/Expressions/{人格名}/{表情}.png`
- **命名**: `happy.png`, `sad.png`, `angry.png` 等

### 分层表情（可选）
- **尺寸**: 根据面部区域调整
- **格式**: PNG (支持透明)
- **位置**: `9x16/Expressions/{人格名}/layers/{部位}_{变体}.png`
- **命名**: 
  - `eyes_happy.png` - 快乐的眼睛
  - `eyes_sad.png` - 悲伤的眼睛
  - `mouth_smile.png` - 微笑的嘴巴
  - `mouth_frown.png` - 皱眉的嘴巴

---

## ?? 快速开始

### 步骤 1: 创建文件夹
手动创建或使用脚本创建上述文件夹结构。

### 步骤 2: 放置基础立绘
将 9:16 比例的基础立绘命名为 `base.png`，放入对应人格文件夹：
```
9x16/Cassandra/base.png
9x16/Phoebe/base.png
```

### 步骤 3: 添加表情（可选）
将表情文件放入 `Expressions` 文件夹：
```
9x16/Expressions/Cassandra/happy.png
9x16/Expressions/Cassandra/sad.png
```

### 步骤 4: 测试
启动游戏，表情系统会自动检测并加载文件。

---

## ?? 加载优先级

系统按以下顺序尝试加载：

1. ? **新路径：整图表情** - `Expressions/Cassandra/happy.png`
2. ?? **旧路径：整图表情** - `Cassandra/happy.png` (兼容)
3. ? **新路径：分层表情** - `Expressions/Cassandra/layers/...`
4. ?? **旧路径：分层表情** - `Cassandra/layers/...` (兼容)
5. ?? **降级：基础立绘** - `Cassandra/base.png`
6. ?? **最终降级：占位符** - 程序生成

---

## ?? 支持的表情

### 标准表情列表
- `happy` - 快乐 ??
- `sad` - 悲伤 ??
- `angry` - 愤怒 ??
- `surprised` - 惊讶 ??
- `worried` - 担忧 ??
- `smug` - 得意 ??
- `disappointed` - 失望 ??
- `thoughtful` - 沉思 ??
- `annoyed` - 烦躁 ??

### 特殊表情（面部覆盖）
文件名需添加 `_face` 后缀：
- `happy_face.png` - 只包含面部表情
- `sad_face.png`

---

## ??? 工具和脚本

### 迁移脚本
如果你已有旧结构的表情文件，使用迁移脚本：
```powershell
.\Reorganize-Expressions.ps1
```

### 验证脚本
检查文件结构是否正确：
```powershell
.\Verify-Portraits.ps1
```

---

## ?? 提示

### 推荐工作流程
1. **先创建基础立绘** - 确保每个人格都有 `base.png`
2. **再添加表情** - 放入 `Expressions` 文件夹
3. **测试游戏** - 查看表情切换是否正常
4. **查看日志** - 确认加载路径

### 常见问题
- **Q: 表情不显示？**
  - A: 检查文件名是否正确（如 `happy.png` 而不是 `Happy.png`）
  
- **Q: 还能用旧路径吗？**
  - A: 可以，系统会降级到旧路径，但会显示警告

- **Q: 分层表情如何制作？**
  - A: 参考 `面部区域覆盖表情制作指南.md`

---

**创建时间**: 2025-01-XX  
**状态**: ? 文件夹结构说明  
**相关文档**: 
- 表情文件独立存放方案-完整指南.md
- 立绘放置完整指南.md
