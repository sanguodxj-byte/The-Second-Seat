# 表情包使用指南

欢迎使用 AI 叙事者表情包功能！叙事者可以根据对话内容自动选择合适的表情包。

## 文件结构

所有的表情包图片都存储在这个文件夹中：

```
Emoticons/
├── README.md              # 本文件
├── emoticons.txt          # 元数据文件，配置表情包标签
├── smile.png              # 示例：微笑表情
├── cry.png                # 示例：哭泣表情
├── think.png              # 示例：思考表情
└── ... 更多表情 ...
```

---

## 表情包格式要求

### 支持的图片格式
- PNG（推荐，支持透明背景）
- JPG / JPEG

### 尺寸建议
- **推荐尺寸**: 128x128 到 512x512 像素
- **最小尺寸**: 64x64 像素
- **最大尺寸**: 1024x1024 像素（超过会影响性能）

### 命名规范
- 文件名即为表情包ID（不含扩展名）
- 使用英文字母、数字和下划线
- 示例：`happy_smile.png`, `sad_cry.png`, `think_01.png`

---

## 元数据配置（emoticons.txt）

### 为什么需要元数据？
元数据文件用于给表情包**添加标签**，帮助 AI 在对话时自动选择合适的表情包。

### 配置格式

```ini
# 表情包元数据文件
# 格式说明：
# [表情包ID]  - 必须与文件名一致（不含扩展名）
# name = 显示名称
# tags = 标签1, 标签2, 标签3  - 用逗号分隔
# description = 简短描述

[smile]
name = 微笑
tags = happy, joy
description = 开心的微笑

[cry]
name = 哭泣
tags = sad, crying
description = 伤心的哭泣

[think]
name = 思考
tags = thinking, confused
description = 认真思考
```

---

## 常用的标签列表

AI 会根据对话内容和好感度自动选择标签，以下是推荐的标签列表：

### 开心系列
- `happy` - 开心
- `joy` - 喜悦
- `excited` - 兴奋
- `smug` - 得意
- `proud` - 自豪

### 悲伤系列
- `sad` - 悲伤
- `disappointed` - 失望
- `crying` - 哭泣
- `melancholic` - 忧郁

### 愤怒系列
- `angry` - 愤怒
- `frustrated` - 沮丧

### 惊讶系列
- `surprised` - 惊讶
- `shocked` - 震惊

### 思考系列
- `thinking` - 思考中
- `confused` - 困惑

### 爱意系列
- `love` - 爱
- `affection` - 亲切

### 中性系列
- `neutral` - 中性
- `calm` - 平静

### 其他
- `embarrassed` - 尴尬
- `shy` - 害羞
- `tired` - 疲惫
- `sleepy` - 困倦

---

## 快速开始

### 步骤 1：准备表情包图片
1. 准备一些表情包图片（PNG/JPG格式）
2. 调整尺寸：256x256 像素
3. 命名示例：`smile.png`, `cry.png`, `angry.png`

### 步骤 2：将图片放入文件夹
- 复制所有图片到 `Emoticons/` 文件夹

### 步骤 3：配置元数据（可选）
- 编辑 `emoticons.txt` 文件
- 为每个表情包添加标签

**示例**:
```ini
[smile]
name = 微笑
tags = happy, joy
```

### 步骤 4：重启游戏
- 关闭 RimWorld
- 重新启动，游戏会自动加载

### 步骤 5：测试
- 与 AI 叙事者对话
- 观察 AI 回复，查看 AI 是否使用表情包

---

## 使用技巧

### 1. 合理使用标签
- 一个表情包可以有多个标签
- 标签越准确，AI 选择越精准

### 2. 控制表情包数量
- 建议 10-30 个表情包为宜
- 过多会影响 AI 选择速度

### 3. 风格统一
- 尽量使用风格统一的表情包
- 例如：都是方形，或都是Q版风

### 4. 透明背景
- PNG 格式支持透明背景
- 增强聊天框显示效果

---

## AI 如何选择表情包

AI 会根据以下规则自动选择表情包：

### 1. 好感度
- **好感度 > 60**: 倾向选择 `happy`, `joy`, `love`
- **好感度 30-59**: `happy`, `neutral`, `calm`
- **好感度 -10-29**: `neutral`, `calm`, `thinking`
- **好感度 < -50**: `angry`, `frustrated`, `smug`

### 2. 当前情绪
- 如果当前情绪"喜悦"：选择 `happy`, `joy`
- 如果当前情绪"愤怒"：选择 `angry`, `frustrated`
- 如果当前情绪"悲伤"：选择 `sad`, `disappointed`

### 3. 对话内容关键词
- 包含"开心"、"太好了"：选择 `happy`, `excited`
- 包含"什么"、"为什么"：选择 `surprised`
- 包含"..."、"嗯"：选择 `thinking`
- 包含"难过"、"遗憾"：选择 `sad`

---

## 故障排查

### 表情包没有加载？
1. 检查文件格式是否为 PNG/JPG
2. 检查文件名是否包含特殊字符
3. 查看游戏日志：`RimWorld/Logs/Player.log`
4. 确认文件路径正确

### 表情包显示异常？
1. 检查图片尺寸是否过大/过小
2. 建议使用 256x256 标准尺寸
3. 确保图片未损坏

### AI 不使用表情包？
1. 检查 `emoticons.txt` 配置是否正确
2. 确认表情包有标签
3. AI 会随机选择是否使用表情包（不是每次都用）

---

## 示例配置

### 示例 1：基础配置
```ini
[happy_smile]
name = 开心微笑
tags = happy, joy, excited
description = 非常开心的表情

[sad_cry]
name = 悲伤哭泣
tags = sad, crying, disappointed
description = 非常悲伤的表情

[think_hmm]
name = 思考中
tags = thinking, confused
description = 认真思考中

[angry_mad]
name = 愤怒
tags = angry, frustrated
description = 愤怒的表情

[love_heart]
name = 爱心
tags = love, affection, happy
description = 充满爱意
```

---

## 推荐资源

### 免费表情包网站
- **Flaticon**: https://flaticon.com (搜索"emoticon"或"emoji")
- **Icon8**: https://icons8.com/icons/set/emoji
- **OpenMoji**: https://openmoji.org/

### 在线编辑工具
- **Pixlr**: https://pixlr.com (在线图片编辑)
- **Photopea**: https://photopea.com (在线PS替代)

---

## 常见问题

### Q: 表情包会影响游戏性能吗？
A: 不会。表情包只在对话时加载和显示，数量在合理范围内（<50）不会影响性能。

### Q: 能否使用动图表情包？
A: 目前只支持静态图片（PNG/JPG），不支持 GIF 动图。

### Q: AI 一定会使用表情包吗？
A: 不一定。AI 会根据对话内容和情绪判断是否需要表情包，也可以在对话中明确要求"用表情包回复我"。

### Q: 如何禁用表情包功能？
A: 可以删除 `Emoticons/` 文件夹内的所有图片即可禁用。

---

**版本**: v1.5.0  
**作者**: TheSecondSeat 开发团队  
**更新日期**: 2024

需要更多帮助请查看 [完整使用手册.md](../完整使用手册.md)
