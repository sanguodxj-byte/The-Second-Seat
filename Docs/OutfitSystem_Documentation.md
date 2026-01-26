# The Second Seat - 服装切换系统文档

## 概述

v2.5.0 新增服装切换功能，允许叙事者根据对话情境自主决定更换服装。

**设计理念：LLM 驱动**
- 服装定义仅描述服装本身（纹理、名称、描述）
- 服装更换完全由 LLM 通过 `ChangeOutfit` 命令控制
- LLM 根据提示词中的规则自主判断何时切换服装
- **无硬编码的时间或条件触发**

## 核心组件

### 1. OutfitDef (服装定义)

通过 XML 定义服装的基本信息：

```xml
<TheSecondSeat.PersonaGeneration.OutfitDef>
  <defName>Sideria_Outfit_Pajamas</defName>
  <label>龙族睡袍</label>
  <outfitTag>Pajamas</outfitTag>
  <personaDefName>TSS_Sideria</personaDefName>
  <outfitDescription>希德莉亚在深夜休息时穿着的柔软睡袍。适合在夜晚私密对话时穿着。</outfitDescription>
  <bodyTexture>pajamas_body</bodyTexture>
  <priority>10</priority>
</TheSecondSeat.PersonaGeneration.OutfitDef>
```

### 2. ChangeOutfit 命令

LLM 通过此命令更换服装：

```json
{"action": "ChangeOutfit", "target": "Pajamas"}
```

### 3. 提示词模块

提示词告知 LLM 可用服装和切换规则，LLM 自主决定切换时机。

## 使用指南

### 1. 创建服装纹理

在 `Textures/[PersonaName]/Narrators/Layered/` 目录下创建：

```
pajamas_body.png       - 睡衣主体
casual_body.png        - 休闲装
intimate_body.png      - 私密服装（可选）
```

### 2. 创建 OutfitDef XML

在 `Defs/OutfitDefs/` 目录下创建 XML 文件：

```xml
<?xml version="1.0" encoding="utf-8" ?>
<Defs>
  <TheSecondSeat.PersonaGeneration.OutfitDef>
    <defName>MyPersona_Outfit_Pajamas</defName>
    <label>睡衣</label>
    <outfitTag>Pajamas</outfitTag>
    <personaDefName>MyPersonaDef</personaDefName>
    <outfitDescription>舒适的睡衣，适合深夜对话时穿着</outfitDescription>
    <bodyTexture>pajamas_body</bodyTexture>
    <priority>10</priority>
    <preserveExpressions>true</preserveExpressions>
    <transitionType>Fade</transitionType>
    <transitionDuration>0.5</transitionDuration>
  </TheSecondSeat.PersonaGeneration.OutfitDef>
</Defs>
```

### 3. 提示词规则

提示词模块 (`Module_Outfit.txt`) 告知 LLM：
- 可用的服装列表
- 切换时机建议（时间、好感度、对话氛围）
- 切换时的对话示例

LLM 根据这些规则自主判断何时切换。

## LLM 命令参考

### ChangeOutfit
更换服装。

```json
{"action": "ChangeOutfit", "target": "Pajamas"}
```

**常用标签：**
- `Default` - 默认服装
- `Pajamas` - 睡衣
- `Casual` - 休闲装
- `Intimate` - 私密服装（高好感度）

### GetOutfitList
获取可用服装列表。

```json
{"action": "GetOutfitList"}
```

### GetCurrentOutfit
查看当前穿着的服装。

```json
{"action": "GetCurrentOutfit"}
```

## 提示词示例

提示词会引导 LLM 在适当时机切换服装：

**场景1：深夜对话**
```
玩家：「好晚了，你还没睡吗？」
叙事者：「啊...确实很晚了呢。等等，让我换个舒服点的...」
[执行 ChangeOutfit: Pajamas]
叙事者：「好了，这样舒服多了~你也该注意休息哦。」
```

**场景2：好感度提升**
```
（当好感度达到亲密阶段时）
叙事者：「...既然我们已经这么熟了，我想...穿得随意一点应该没关系吧？」
[执行 ChangeOutfit: Casual]
```

## 文件结构

```
Source/TheSecondSeat/
├── Commands/
│   └── ChangeOutfitCommand.cs  - 服装更换命令
├── PersonaGeneration/
│   ├── OutfitDef.cs            - 服装定义数据结构
│   └── OutfitSystem.cs         - 服装管理系统

The Second Seat/
├── Defs/PromptModuleDefs/
│   └── PromptModuleDefs_Outfit.xml  - 提示词模块定义
├── Languages/ChineseSimplified/Prompts/
│   └── Module_Outfit.txt       - 中文提示词
├── Languages/English/Prompts/
│   └── Module_Outfit.txt       - 英文提示词

[TSS]Sideria - Dragon Guard/Defs/OutfitDefs/
└── OutfitDefs_Sideria.xml      - Sideria 专用服装
```

## 与旧版本的区别

| 功能 | v2.4.x (旧) | v2.5.0 (新) |
|------|-------------|-------------|
| 触发方式 | 硬编码时间条件 | LLM 自主决定 |
| 配置复杂度 | 需要配置条件 | 仅描述服装 |
| 灵活性 | 固定规则 | 根据对话情境动态调整 |
| 自然度 | 机械切换 | 融入对话 |

## 注意事项

1. 纹理尺寸应与 `base_body.png` 一致
2. `preserveExpressions: true` 保留原有表情系统
3. 服装描述会包含在提示词中，要写得清晰
4. LLM 会自然地将服装切换融入对话

## 版本历史

- **v2.5.0**: LLM 驱动的服装切换系统
  - 移除硬编码条件
  - 添加 ChangeOutfit 命令
  - 添加提示词模块
  - LLM 自主决定切换时机
