# ?? The Second Seat - 立绘文件说明

## ?? 目录结构

```
Textures/
└── UI/
    └── Narrators/
        ├── Cassandra.png     # 256x256, 蓝色系战略型女性
        ├── Phoebe.png        # 256x256, 橙黄色系友善型女性
        ├── Randy.png         # 256x256, 红色系混乱型男性
        ├── Igor.png          # 256x256, 深红色系施虐型男性
        ├── Luna.png          # 256x256, 浅蓝色系保护型女性
        └── README.md         # 本文件
```

---

## ?? 立绘规格

### 技术要求

| 属性 | 要求 |
|-----|------|
| **尺寸** | 256x256 pixels |
| **格式** | PNG (推荐) 或 JPG |
| **透明** | 支持（PNG only） |
| **颜色** | RGB, 24-bit |
| **文件大小** | < 500KB |

### 内容要求

- **构图**：头部和肩膀特写
- **风格**：Anime / Semi-realistic
- **表情**：符合人格特质
- **背景**：透明或纯色

---

## ?? 人格对应立绘

### 1. Cassandra Classic（蓝色系）
- **主色调**：Blue (#4D7FB3)
- **性格**：严肃、专业、战略型
- **建议表情**：沉着、自信
- **服饰**：职业装或军装

### 2. Phoebe Chillax（橙黄色系）
- **主色调**：Orange/Yellow (#E6B84D)
- **性格**：友善、温暖、轻松
- **建议表情**：微笑、温柔
- **服饰**：休闲装或花纹服饰

### 3. Randy Random（红色系）
- **主色调**：Red (#CC3333)
- **性格**：混乱、狂野、不可预测
- **建议表情**：狂笑或诡异微笑
- **服饰**：不规则、夸张

### 4. Igor Invader（深红色系）
- **主色调**：Dark Red (#661111)
- **性格**：施虐、残酷、挑战型
- **建议表情**：冷笑、轻蔑
- **服饰**：黑色系、尖刺装饰

### 5. Luna Protector（浅蓝色系）
- **主色调**：Light Blue (#B3CDE6)
- **性格**：保护、温柔、母性
- **建议表情**：关切、温和
- **服饰**：飘逸、柔和

---

## ??? 当前状态

| 人格 | 立绘文件 | 状态 |
|-----|---------|------|
| Cassandra | `Cassandra.png` | ? **需要创建** |
| Phoebe | `Phoebe.png` | ? **需要创建** |
| Randy | `Randy.png` | ? **需要创建** |
| Igor | `Igor.png` | ? **需要创建** |
| Luna | `Luna.png` | ? **需要创建** |

---

## ?? 创建立绘的方法

### 方案 A：手绘/Photoshop
1. 创建 256x256 画布
2. 绘制角色头像
3. 导出为 PNG（透明背景）

### 方案 B：AI 生成（推荐）
使用以下 Prompt 生成：

```
portrait of [character description], [personality traits],
anime style, head and shoulders, 
professional lighting, vibrant colors,
[主色调] color scheme,
256x256 resolution
```

**示例（Cassandra）**：
```
portrait of a professional female strategist, 
serious and calculating expression,
blue military uniform, short brown hair,
anime style, head and shoulders,
blue color scheme (hex #4D7FB3),
256x256 resolution
```

### 方案 C：使用占位符（临时）
- 系统会自动生成颜色方块作为占位符
- 颜色基于 `NarratorPersonaDef.xml` 中的 `primaryColor`

---

## ?? 添加立绘到 Mod

### 步骤 1：准备文件
```
1. 创建 256x256 PNG 图片
2. 命名为对应的人格名称（例如：Cassandra.png）
3. 确保文件大小 < 500KB
```

### 步骤 2：放置文件
```
复制到: Textures/UI/Narrators/
```

### 步骤 3：验证路径
```xml
<!-- 在 NarratorPersonaDefs.xml 中 -->
<portraitPath>UI/Narrators/Cassandra</portraitPath>
<!-- 注意：不需要 .png 扩展名，不需要 Textures/ 前缀 -->
```

### 步骤 4：测试
```
1. 重新编译 Mod
2. 部署到 RimWorld
3. 启动游戏
4. 打开人格选择窗口
5. 确认立绘显示正常
```

---

## ?? 故障排除

### 问题：立绘不显示
**检查清单**：
- [ ] 文件是否存在于正确路径
- [ ] 文件名是否正确（区分大小写）
- [ ] 文件格式是否为 PNG/JPG
- [ ] `portraitPath` 是否正确配置
- [ ] 是否重新编译和部署了 Mod

### 问题：立绘显示模糊
**解决方案**：
- 确保原图分辨率为 256x256
- 使用无损压缩（PNG）
- 避免过度压缩

### 问题：透明背景不工作
**解决方案**：
- 使用 PNG 格式（JPG 不支持透明）
- 确保保存时选择了"透明背景"选项

---

## ?? 后续计划

### v1.0（当前）
- ? 创建 5 个内置人格立绘
- ? 实现立绘加载系统
- ? 支持用户自定义立绘

### v1.1（计划）
- [ ] 多分辨率支持（128x128, 512x512）
- [ ] 立绘动画（眨眼、表情变化）
- [ ] 在线立绘库
- [ ] 社区立绘分享

---

## ?? 贡献立绘

如果你想为社区贡献自己绘制的立绘：

1. **准备文件**：符合上述规格
2. **提交到 GitHub**：创建 Pull Request
3. **说明信息**：包含人格描述和作者信息
4. **授权说明**：MIT License 或 CC BY 4.0

---

## ?? 相关文档

- [人格生成系统指南.md](../../人格生成系统指南.md)
- [立绘加载系统实现总结.md](../../立绘加载系统实现总结.md)
- [NarratorPersonaDefs.xml](../../Defs/NarratorPersonaDefs.xml)

---

**版本**: 1.0  
**更新**: 2025-01-XX  
**作者**: The Second Seat Team

**注意**：立绘文件目前为空，请根据上述规格创建或从社区获取。
