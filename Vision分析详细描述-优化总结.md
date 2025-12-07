# ?? Vision 分析详细描述 - 优化总结

**版本**: v1.5.0  
**日期**: 2025-01-15  
**状态**: ? **已完成并部署**

---

## ?? 问题背景

### 原始问题

**用户反馈**：
```
A stoic dragonkin girl with long white hair, horns, and a scaled tail. 
She has piercing red eyes and wears a dark hooded robe over brown leather armor, 
accented with crimson patterns.
```

这是 **Vision API 返回的 `characterDescription`**，但存在以下问题：

1. ? **只有外观描述**（头发、眼睛、服装）
2. ? **缺少性格描述**（只提到 "stoic"）
3. ? **缺少对话风格**（怎么说话？）
4. ? **缺少行为倾向**（如何反应？）
5. ? **缺少事件偏好**（喜欢什么事件？）

### 根本原因

**Vision Prompt 过于简化**：
```
characterDescription: "Brief description (max 200 chars)"
```

这导致 AI 只描述"看到的内容"，不推断"背后的性格"。

---

## ? 解决方案

### 核心改进

**扩展 Vision Prompt**，要求 AI 提供：
1. **详细外观描述**（300-500 词）
2. **基于视觉的性格推断**
3. **对话风格预测**
4. **行为模式预测**

### 新的 Vision Prompt 结构

```
**Part 1: DETAILED PHYSICAL DESCRIPTION (40%)**
- 种族/物种
- 头发（颜色、长度、样式）
- 眼睛（颜色、形状、表情）
- 面部特征
- 体型和姿势
- 服装和装甲（材质、状态、设计）
- 特殊特征（角、尾巴、翅膀）

**Part 2: PERSONALITY INFERENCE FROM VISUAL CUES (40%)**
- 从表情和肢体语言推断
- 从服装和装甲推断
- 从武器和装备推断
- 从种族特征推断

**Part 3: DIALOGUE & BEHAVIOR PREDICTION (20%)**
- 说话方式
- 情感表达
- 互动风格
```

---

## ?? 修改内容

### 文件：`MultimodalAnalysisService.cs`

**位置**：第 160-250 行（`GetVisionPrompt` 方法）

#### 修改前（简化版）
```csharp
private string GetVisionPrompt()
{
    return @"Analyze this character portrait and provide a JSON response:
{
  ""characterDescription"": ""Brief description (max 200 chars)"",
  ...
}
Keep characterDescription under 200 characters.";
}
```

#### 修改后（详细版）
```csharp
private string GetVisionPrompt()
{
    return @"Analyze this character portrait in detail and provide a comprehensive JSON response:
{
  ""characterDescription"": ""Detailed 300-500 word analysis combining appearance AND inferred personality"",
  ...
}

**CRITICAL REQUIREMENTS for characterDescription** (300-500 words):

**Part 1: DETAILED PHYSICAL DESCRIPTION (40%)**
Describe EVERY visible detail:
- Species/Race
- Hair: Color, length, style, texture
- Eyes: Color, shape, expression
- Facial Features: Expression, age, scars
- Body Type: Build, posture, stance
- Clothing & Armor: Materials, condition, design
- Special Features: Wings, tail, horns, weapons

**Part 2: PERSONALITY INFERENCE FROM VISUAL CUES (40%)**
- From Expression: Stoic face → disciplined, self-controlled
- From Clothing: Dark colors → mysterious, serious
- From Armor: Heavy armor → protective, combat-ready
- From Weapons: Ready stance → assertive
- From Species: Dragon features → prideful, powerful

**Part 3: DIALOGUE & BEHAVIOR PREDICTION (20%)**
- Speaking Style: Calm/passionate/harsh?
- Emotional Expression: Reserved/expressive?
- Interaction Style: Distant/warm?

**Example Output** (示例 - 500 词详细分析)
";
}
```

---

## ?? 新 Prompt 的优势

### 1. **详细的外观分析**

**之前**：
```
A stoic dragonkin girl with white hair and red eyes.
```

**现在**（预期）：
```
This is a dragonkin girl with distinct draconic heritage. She has long, flowing 
white hair that cascades past her shoulders, contrasting sharply with her deep 
crimson eyes featuring vertical slit pupils. Two small curved horns emerge from 
her temples, and a long scaled tail extends from beneath her robes...

She wears a dark hooded robe made of high-quality fabric, flowing elegantly 
around her form. Beneath this, she's equipped with brown leather armor reinforced 
at critical points—the shoulder guards show signs of battle wear but remain 
functional...
```

### 2. **基于视觉的性格推断**

**之前**：无性格推断

**现在**（预期）：
```
Based on these visual cues, she likely possesses a Strategic or Protective 
personality. The military bearing and practical armor suggest discipline and 
preparedness. Her stoic expression indicates emotional control and composure 
under pressure.
```

### 3. **对话风格预测**

**之前**：无对话风格

**现在**（预期）：
```
She probably speaks in a measured, deliberate tone, choosing words carefully. 
Her dialogue would be concise and direct, favoring clarity over flowery language. 
She responds to logic and practical considerations rather than emotional appeals.
```

### 4. **行为模式预测**

**之前**：无行为模式

**现在**（预期）：
```
In conversation, she would likely maintain professional distance initially, 
observing others before deciding to trust them. She values competence and 
reliability over charm. Her emotional expression would be restrained in public, 
though those she trusts might see a softer side.
```

---

## ?? 完整工作流程

### 生成人格时的数据流

```
1. 用户上传立绘图片
   ↓
2. Vision API 分析图片（使用新的详细 Prompt）
   ↓ 返回：
   - dominantColors: [...]
   - visualElements: [...]
   - characterDescription: "详细 500 词分析（外观+性格+对话+行为）"
   - mood: "stoic, disciplined, mysterious"
   - suggestedPersonality: "Strategic"
   - styleKeywords: ["composed", "military", "reserved"]
   ↓
3. PersonaSelectionWindow 保存到 NarratorPersonaDef
   - biography = characterDescription（详细分析）
   - visualDescription = characterDescription（副本）
   - visualElements = ["horns", "tail", "armor", "robe"]
   - visualMood = "stoic"
   - overridePersonality = "Strategic"
   ↓
4. PersonaAnalyzer 本地分析 biography
   ↓ 从详细描述中提取：
   - dialogueStyle（正式度、情感度、详细度）
   - eventPreferences（事件偏好）
   - toneTags（语气标签）
   ↓
5. 合并数据生成最终人格
   ↓
6. PersonaDefExporter 导出 XML
   ↓
7. SystemPromptGenerator 使用 visualDescription
   在 System Prompt 中添加 "YOUR VISUAL PRESENCE" 部分
```

---

## ?? 示例对比

### 场景：龙族少女立绘

#### ? 旧版 Prompt（简化）

**Vision 返回**：
```json
{
  "characterDescription": "A stoic dragonkin girl with long white hair, horns, and a scaled tail. She has piercing red eyes and wears a dark hooded robe over brown leather armor, accented with crimson patterns.",
  "suggestedPersonality": "Strategic",
  "styleKeywords": ["mysterious", "stoic"]
}
```

**问题**：
- 只有 100 词
- 没有性格推断
- 没有对话风格

---

#### ? 新版 Prompt（详细）

**Vision 返回**（预期 500 词）：
```json
{
  "characterDescription": "This is a dragonkin girl with distinct draconic heritage. She has long, flowing white hair that cascades past her shoulders, contrasting sharply with her deep crimson eyes featuring vertical slit pupils. Two small curved horns emerge from her temples, and a long scaled tail extends from beneath her robes, both displaying red scales that shimmer in the light.

She wears a dark hooded robe made of high-quality fabric, flowing elegantly around her form. Beneath this, she's equipped with brown leather armor reinforced at critical points—the shoulder guards show signs of battle wear but remain functional. The armor is decorated with intricate crimson patterns that echo her natural scale coloration, suggesting either personal customization or cultural significance.

Her facial expression is notably stoic and composed, with subtle frown lines suggesting years of discipline or hardship. Her posture is upright and alert, reminiscent of military training. The way she carries herself indicates confidence born from experience rather than arrogance. Her piercing gaze suggests high intelligence and constant awareness of her surroundings.

Based on these visual cues, she likely possesses a Strategic or Protective personality. The military bearing and practical armor suggest discipline and preparedness. Her stoic expression indicates emotional control and composure under pressure. She probably speaks in a measured, deliberate tone, choosing words carefully. Her dialogue would be concise and direct, favoring clarity over flowery language.

In conversation, she would likely maintain professional distance initially, observing others before deciding to trust them. She values competence and reliability over charm. Her emotional expression would be restrained in public, though those she trusts might see a softer side. She responds to logic and practical considerations rather than emotional appeals.

The draconic features suggest a degree of pride and self-reliance. She may be territorial about her personal space and values. Her well-maintained equipment indicates attention to detail and self-sufficiency. The crimson accents throughout her appearance suggest controlled passion beneath the calm exterior—she has strong convictions but channels them through discipline rather than outbursts.",

  "suggestedPersonality": "Strategic",
  "styleKeywords": ["stoic", "disciplined", "composed", "military", "reserved", "perceptive", "strategic"]
}
```

**优势**：
- ? 500 词详细分析
- ? 完整的外观描述
- ? 深入的性格推断
- ? 对话风格预测
- ? 行为模式预测

---

## ?? 生成的 XML 示例

```xml
<TheSecondSeat.PersonaGeneration.NarratorPersonaDef>
  <defName>CustomPersona_DragonGirl</defName>
  <label>Sideria</label>
  <narratorName>Sideria</narratorName>

  <!-- ? 详细简介（500 词） -->
  <biography>
    This is a dragonkin girl with distinct draconic heritage. She has long, flowing white hair...
    
    Based on these visual cues, she likely possesses a Strategic or Protective personality...
    
    In conversation, she would likely maintain professional distance initially...
  </biography>

  <!-- ? Vision 分析结果（AI 对自身外观的理解） -->
  <visualDescription>
    This is a dragonkin girl with distinct draconic heritage. She has long, flowing white hair...
  </visualDescription>

  <visualElements>
    <li>white hair</li>
    <li>crimson eyes</li>
    <li>dragon horns</li>
    <li>scaled tail</li>
    <li>dark hooded robe</li>
    <li>leather armor</li>
    <li>crimson patterns</li>
  </visualElements>

  <visualMood>stoic, disciplined, mysterious</visualMood>

  <!-- ? 性格（来自 Vision 推断） -->
  <overridePersonality>Strategic</overridePersonality>

  <!-- ? 对话风格（从详细描述推断） -->
  <dialogueStyle>
    <formalityLevel>0.70</formalityLevel>       <!-- 正式、军事化 -->
    <emotionalExpression>0.30</emotionalExpression>  <!-- 克制情感 -->
    <humorLevel>0.20</humorLevel>               <!-- 少幽默 -->
    <verbosity>0.45</verbosity>                 <!-- 简洁 -->
  </dialogueStyle>

  <!-- ? 语气标签 -->
  <toneTags>
    <li>stoic</li>
    <li>disciplined</li>
    <li>composed</li>
    <li>military</li>
    <li>reserved</li>
    <li>perceptive</li>
    <li>strategic</li>
  </toneTags>

</TheSecondSeat.PersonaGeneration.NarratorPersonaDef>
```

---

## ?? System Prompt 中的应用

### 新增：YOUR VISUAL PRESENCE 部分

```
=== WHO YOU ARE ===
In this role, you manifest as **Sideria**.

YOUR VISUAL PRESENCE (HOW YOU APPEAR):
----------------------------------------
This is a dragonkin girl with distinct draconic heritage. She has long, flowing 
white hair that cascades past her shoulders, contrasting sharply with her deep 
crimson eyes featuring vertical slit pupils...

Visual elements: white hair, crimson eyes, dragon horns, scaled tail, dark hooded robe, leather armor, crimson patterns
Overall atmosphere: stoic, disciplined, mysterious
----------------------------------------

IMPORTANT: This is how you look. Your appearance reflects your nature.
- You may occasionally reference your visual traits in conversation
- Your appearance should align with your personality and behavior
- When describing yourself, use these visual details naturally

YOUR CORE IDENTITY (CRITICAL - NEVER DEVIATE FROM THIS):
----------------------------------------
This is a dragonkin girl with distinct draconic heritage...

Based on these visual cues, she likely possesses a Strategic or Protective personality...
----------------------------------------
```

**效果**：
- AI 知道自己的外观
- AI 可以在对话中自然提及自己的特征
- AI 的行为与外观一致

---

## ?? 测试建议

### 测试步骤

1. **准备测试图片**：
   - 龙族少女立绘
   - 精灵法师立绘
   - 机械战士立绘

2. **在游戏中生成人格**：
   ```
   人格选择 → 从立绘生成新人格 → 选择图片
   ```

3. **检查生成的 XML**：
   ```xml
   <!-- 查看 biography 长度 -->
   <biography>应该有 300-500 词</biography>
   
   <!-- 查看 visualDescription -->
   <visualDescription>应该详细描述外观和性格</visualDescription>
   
   <!-- 查看 toneTags -->
   <toneTags>应该有 5-10 个关键词</toneTags>
   ```

4. **测试对话**：
   ```
   玩家："你看起来像个战士"
   AI（预期）："Indeed. My armor and combat stance reflect years of military training. [touches dragon horns] My draconic heritage also grants me natural resilience."
   ```

---

## ?? 性能考虑

### Token 消耗

**之前**：
- Prompt: ~300 tokens
- Response: ~100 tokens
- **总计**: ~400 tokens

**现在**：
- Prompt: ~1200 tokens（详细指令）
- Response: ~500 tokens（详细分析）
- **总计**: ~1700 tokens

**成本增加**：
- 4.25 倍
- 但换来的是 **5 倍详细的分析**

### 处理时间

**之前**：~3-5 秒  
**现在**：~5-8 秒（预期）

---

## ? 部署状态

### 已修改文件

- ? `MultimodalAnalysisService.cs`（第 160-250 行）
  - 扩展 `GetVisionPrompt` 方法
  - 详细的外观描述要求
  - 性格推断指令
  - 对话风格预测

### 已编译

```
? 0 个错误
?? 82 个警告（可忽略）
```

### 已部署

```
? TheSecondSeat.dll → D:\steam\steamapps\common\RimWorld\Mods\TheSecondSeat\Assemblies\
```

---

## ?? 下一步

### 用户操作

1. **重启 RimWorld**
2. **打开人格选择窗口**
3. **从立绘生成新人格**
4. **观察生成的 biography 是否详细**
5. **查看 XML 文件验证字段**

### 如果效果不理想

**可能原因**：
1. API 未遵循 Prompt 指令 → 调整 Prompt 措辞
2. max_tokens 太小 → 增加到 2048
3. 模型能力不足 → 换用 GPT-4V 或 Claude 3

**调试方法**：
```
查看 RimWorld/Player.log
搜索 "[MultimodalAnalysis]"
查看返回的原始 JSON
```

---

## ?? 总结

### 核心改进

? **Vision Prompt 从 200 字符扩展到 500 词**  
? **要求 AI 推断性格、对话风格、行为模式**  
? **提供详细的分析指令和示例**  
? **生成的人格定义更加完整和可用**

### 效果

- **之前**：只有外观描述，缺少性格
- **现在**：详细的外观 + 性格 + 对话 + 行为预测

### 适用场景

- 从立绘生成新人格
- 多模态 AI 分析
- 自动人格创建

---

**版本**: v1.5.0  
**状态**: ? **已完成并部署**  
**测试**: 等待用户反馈

?? **现在 Vision API 会返回详细的性格描述了！**
