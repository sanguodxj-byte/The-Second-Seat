# 多模态 API 人格分析 - 完整指南

## ?? 功能概述

多模态 API 分析系统通过调用先进的 AI 模型（GPT-4 Vision、DeepSeek VL、Gemini Pro Vision）来自动分析叙事者的立绘图像和简介文本，生成精确的人格配置。

**核心优势**：
- ? **深度理解**：AI 能理解图像的情感氛围和艺术风格
- ? **准确推断**：比简单的颜色映射更智能
- ? **零配置**：自动从游戏文件夹读取立绘
- ? **多模态融合**：整合图像和文本分析结果
- ? **三大平台支持**：OpenAI、DeepSeek、Gemini

---

## ?? 快速开始

### 步骤 1：配置 API

1. 打开游戏 → **选项** → **模组设置** → **The Second Seat**
2. 找到"多模态人格分析设置"部分
3. 勾选 ? **"启用多模态分析"**
4. 选择 AI 提供商：
   - **OpenAI** - 最成熟，图像理解最强
   - **DeepSeek** - 性价比高，中文友好
   - **Gemini** - 免费额度大，速度快
5. 输入 **API Key**
6. 点击 **"应用"**

### 步骤 2：使用分析

1. 打开 AI 窗口 → 点击 **"切换人格"**
2. 选择任意人格
3. 点击 **"使用 API 分析"** 按钮
4. 等待 5-10 秒，AI 会返回分析结果
5. 查看分析报告并应用

---

## ?? 分析流程详解

### 阶段 1：图像分析

AI 会分析立绘的以下特征：

#### 1. **视觉特征**
```
- 整体氛围（严肃、俏皮、神秘、温暖）
- 主色调和情感关联
- 面部表情（如果可见）
- 艺术风格（写实、动漫、抽象等）
- 显著符号或元素
```

#### 2. **推断人格**
```
基于视觉线索推断 6 种人格之一：
- Benevolent: 温暖色调，柔和表情
- Sadistic: 深色调，锐利元素
- Chaotic: 鲜艳色彩，不规则构图
- Strategic: 冷色调，对称布局
- Protective: 温和蓝色，坚定姿态
- Manipulative: 紫色系，神秘感
```

#### 3. **对话风格建议**
```
- formalityLevel: 根据服装和姿态
- emotionalExpression: 根据面部表情
- confidence: 根据整体气场
```

**示例输出（Cassandra Classic）**：
```json
{
  "visualDescription": "A professional woman in formal attire with a calm, authoritative demeanor",
  "dominantColors": ["blue", "gray", "white"],
  "mood": "serious, calculated, professional",
  "suggestedPersonality": "Strategic",
  "confidence": 0.85,
  "dialogueStyle": {
    "formalityLevel": 0.8,
    "emotionalExpression": 0.3,
    "confidence": 0.9
  },
  "toneTags": ["authoritative", "measured"],
  "reasoning": "The blue color scheme and formal presentation suggest a strategic, rational personality"
}
```

---

### 阶段 2：文本分析

AI 深度解析简介文本：

#### 1. **人格识别**
```
分析关键短语和语气：
- "strategic and balanced" → Strategic
- "kind-hearted and protective" → Benevolent
- "pure chaos incarnate" → Chaotic
- "enjoys watching struggles" → Sadistic
```

#### 2. **对话风格提取**
```
从文本中推断：
- "speaks formally" → formalityLevel = 0.8
- "casual and warmly" → emotionalExpression = 0.7
- "brief responses" → verbosity = 0.3
- "dark humor" → humorLevel = 0.6, sarcasmLevel = 0.7
```

#### 3. **事件偏好分析**
```
识别倾向性描述：
- "prefers rewarding events" → positiveEventBias = 0.4
- "testing your limits" → negativeEventBias = 0.5
- "wild abandon" → chaosLevel = 0.9
- "proactively intervenes" → interventionFrequency = 0.8
```

**示例输出（Phoebe Chillax）**：
```json
{
  "suggestedPersonality": "Benevolent",
  "confidence": 0.92,
  "dialogueStyle": {
    "formalityLevel": 0.3,
    "emotionalExpression": 0.8,
    "verbosity": 0.5,
    "humorLevel": 0.6,
    "sarcasmLevel": 0.1
  },
  "toneTags": ["gentle", "nurturing", "cheerful"],
  "eventPreferences": {
    "positiveEventBias": 0.5,
    "negativeEventBias": -0.3,
    "chaosLevel": 0.2,
    "interventionFrequency": 0.7
  },
  "keyPhrases": ["kind-hearted", "protect", "breathing room", "warm friend"],
  "reasoning": "The biography emphasizes care, protection, and giving players space, indicating a benevolent personality"
}
```

---

### 阶段 3：多模态融合

系统智能整合两种分析结果：

#### 融合规则
```
1. 人格选择：
   - 文本置信度 > 0.7 → 优先文本结果
   - 图像置信度 > 0.6 → 参考图像结果
   - 否则 → 选择置信度更高的

2. 对话风格：
   - 文本权重 70%（更准确）
   - 图像权重 30%（辅助验证）

3. 事件偏好：
   - 完全采用文本分析（图像不包含此信息）
```

**融合示例**：
```
图像分析：Strategic (0.85)
文本分析：Strategic (0.92)
→ 最终：Strategic

对话风格融合：
formalityLevel = 0.8 × 0.7 + 0.8 × 0.3 = 0.80
emotionalExpression = 0.4 × 0.7 + 0.3 × 0.3 = 0.37
```

---

## ?? 配置详解

### OpenAI 配置

**推荐模型**：
- Vision: `gpt-4-vision-preview`（最准确）
- Text: `gpt-4-turbo`（深度分析）

**获取 API Key**：
1. 访问 https://platform.openai.com/api-keys
2. 创建新的 API Key
3. 复制并粘贴到模组设置

**定价**（2024年）：
- Vision: $0.01 / 图像
- Text: $0.03 / 1000 tokens
- **单次人格分析成本**: ~$0.05

**注意**：
- 需要信用卡绑定
- 图像大小限制：20MB
- 支持 PNG, JPG, WebP

---

### DeepSeek 配置

**推荐模型**：
- Vision: `deepseek-vl`
- Text: `deepseek-chat`

**获取 API Key**：
1. 访问 https://platform.deepseek.com/
2. 注册账号（支持国内手机号）
3. 创建 API Key

**定价**（2024年）：
- Vision: ?0.005 / 图像
- Text: ?0.001 / 1000 tokens
- **单次人格分析成本**: ~?0.01（约 $0.0014）

**优势**：
- 性价比极高（OpenAI 的 1/35）
- 中文理解优秀
- 国内访问快

---

### Gemini 配置

**推荐模型**：
- Vision: `gemini-pro-vision`
- Text: `gemini-pro`

**获取 API Key**：
1. 访问 https://makersuite.google.com/app/apikey
2. 创建新项目
3. 启用 Generative AI API
4. 创建 API Key

**定价**（2024年）：
- **免费层**：60 次/分钟
- Vision: 免费（有限额）
- Text: 免费（有限额）
- **单次人格分析成本**: $0（在免费额度内）

**优势**：
- 完全免费（有限额）
- 速度快
- 无需信用卡

**限制**：
- 每天免费额度：1500 次请求
- 超出后：$0.002 / 1000 tokens

---

## ?? 实际案例

### 案例 1：分析原版 Cassandra

**输入**：
- 立绘：`UI/Storyteller/Cassandra`
- 简介：
  ```
  Cassandra Classic follows the tried-and-true method of increasing 
  difficulty over time. She's strategic and calculated, carefully 
  balancing challenge and reward.
  ```

**图像分析结果**（GPT-4 Vision）：
```json
{
  "visualDescription": "A professional woman in blue formal attire with a calm expression",
  "dominantColors": ["blue", "gray", "white"],
  "mood": "serious, professional, authoritative",
  "suggestedPersonality": "Strategic",
  "confidence": 0.87,
  "dialogueStyle": {
    "formalityLevel": 0.8,
    "emotionalExpression": 0.3,
    "confidence": 0.9
  },
  "toneTags": ["authoritative", "measured"],
  "reasoning": "The professional attire and calm demeanor suggest a strategic personality"
}
```

**文本分析结果**：
```json
{
  "suggestedPersonality": "Strategic",
  "confidence": 0.93,
  "dialogueStyle": {
    "formalityLevel": 0.7,
    "emotionalExpression": 0.4,
    "verbosity": 0.6,
    "humorLevel": 0.2,
    "sarcasmLevel": 0.1
  },
  "toneTags": ["authoritative", "strategic", "measured"],
  "eventPreferences": {
    "positiveEventBias": 0.0,
    "negativeEventBias": 0.0,
    "chaosLevel": 0.1,
    "interventionFrequency": 0.5
  },
  "keyPhrases": ["strategic", "calculated", "balancing"]
}
```

**融合结果**：
```
人格：Strategic（置信度 0.93）
对话风格：
  - formalityLevel: 0.73（正式）
  - emotionalExpression: 0.37（理性）
  - verbosity: 0.60（适中）
事件偏好：完全平衡（0.0, 0.0）
```

**对比本地分析**：
- 本地：Strategic（基于蓝色）
- API：Strategic（置信度 0.93）
- ? **一致！API 提供了更详细的参数**

---

### 案例 2：分析原版 Randy Random

**输入**：
- 立绘：`UI/Storyteller/Randy`
- 简介：
  ```
  Randy Random doesn't follow any rules. He's pure chaos incarnate, 
  throwing events at you with wild abandon. One moment you're 
  drowning in gifts, the next you're fighting for survival.
  ```

**图像分析结果**（DeepSeek VL）：
```json
{
  "visualDescription": "A chaotic figure with vibrant red and orange colors, wild expression",
  "dominantColors": ["red", "orange", "yellow"],
  "mood": "wild, unpredictable, energetic",
  "suggestedPersonality": "Chaotic",
  "confidence": 0.91,
  "dialogueStyle": {
    "formalityLevel": 0.2,
    "emotionalExpression": 0.9,
    "confidence": 0.7
  },
  "toneTags": ["playful", "wild"],
  "reasoning": "The vibrant colors and energetic pose indicate a chaotic personality"
}
```

**文本分析结果**：
```json
{
  "suggestedPersonality": "Chaotic",
  "confidence": 0.95,
  "dialogueStyle": {
    "formalityLevel": 0.2,
    "emotionalExpression": 0.8,
    "verbosity": 0.4,
    "humorLevel": 0.8,
    "sarcasmLevel": 0.5
  },
  "toneTags": ["playful", "mischievous", "wild"],
  "eventPreferences": {
    "positiveEventBias": 0.0,
    "negativeEventBias": 0.0,
    "chaosLevel": 0.95,
    "interventionFrequency": 0.7
  },
  "keyPhrases": ["chaos", "wild abandon", "unpredictable"]
}
```

**融合结果**：
```
人格：Chaotic（置信度 0.95）
对话风格：
  - formalityLevel: 0.20（极度随意）
  - emotionalExpression: 0.83（非常情绪化）
  - humorLevel: 0.80（极度幽默）
事件偏好：
  - chaosLevel: 0.95（极度混乱）
  - interventionFrequency: 0.70（频繁干预）
```

**对比本地分析**：
- 本地：Chaotic（基于红色 + 关键词"chaos"）
- API：Chaotic（置信度 0.95，更详细）
- ? **一致，且 API 提供了更精确的 chaosLevel**

---

### 案例 3：分析模糊人格

**输入**：
- 立绘：紫色系，神秘感
- 简介：
  ```
  She is both kind and cunning, protecting those she cares about 
  while subtly guiding their decisions. Her methods are gentle 
  yet manipulative.
  ```

**图像分析结果**（Gemini Pro Vision）：
```json
{
  "visualDescription": "A mysterious figure in purple robes with an enigmatic smile",
  "dominantColors": ["purple", "violet", "dark blue"],
  "mood": "mysterious, enigmatic, alluring",
  "suggestedPersonality": "Manipulative",
  "confidence": 0.75,
  "toneTags": ["mysterious", "enigmatic"]
}
```

**文本分析结果**：
```json
{
  "suggestedPersonality": "Protective",
  "confidence": 0.68,
  "dialogueStyle": {
    "formalityLevel": 0.6,
    "emotionalExpression": 0.6,
    "verbosity": 0.5,
    "humorLevel": 0.3,
    "sarcasmLevel": 0.4
  },
  "toneTags": ["nurturing", "mysterious"],
  "eventPreferences": {
    "positiveEventBias": 0.3,
    "negativeEventBias": 0.0,
    "chaosLevel": 0.3,
    "interventionFrequency": 0.7
  },
  "keyPhrases": ["kind", "cunning", "protecting", "guiding", "manipulative"],
  "reasoning": "Mixed signals: protective actions but manipulative methods"
}
```

**融合结果**：
```
人格：Manipulative（图像 0.75 > 文本 0.68）
融合评论：
"This persona shows Protective tendencies (caring, intervening) 
but uses Manipulative methods (subtle guidance, cunning). 
The visual mysterious aura aligns more with Manipulative."
```

**本地分析的困难**：
- 关键词混合："kind" (Benevolent) + "manipulative" (Manipulative)
- 本地可能误判为 Protective
- ? **API 能理解复杂的人格组合**

---

## ?? 使用场景

### 场景 1：快速创建自定义人格

```
1. 准备立绘图像（放入 Textures 文件夹）
2. 编写简短的简介（100-200 词）
3. 创建 XML 定义（只需名称、路径、简介）
4. 使用 API 分析 → 自动生成完整配置
5. 无需手动调整参数！
```

**时间对比**：
- 手动配置：30-60 分钟
- API 分析：5-10 秒
- **节省 99% 时间**

---

### 场景 2：分析其他 Mod 的叙事者

```
1. 找到其他 Mod 的叙事者立绘路径
2. 创建新的 NarratorPersonaDef
3. 设置 portraitPath 指向该立绘
4. 编写简介（可参考原 Mod 描述）
5. API 分析 → 获得人格配置
6. 可以让不同 Mod 的叙事者共存！
```

---

### 场景 3：优化现有人格

```
如果现有人格的对话风格不符合预期：
1. 使用 API 重新分析
2. 对比 API 建议和当前配置
3. 微调参数
4. 测试对话效果
```

---

## ?? 技术细节

### 图像搜索路径

系统按以下优先级搜索立绘：

```
1. Mods/TheSecondSeat/Textures/{portraitPath}
2. Mods/TheSecondSeat/{portraitPath}
3. RimWorld/Data/Core/Textures/{portraitPath}
4. RimWorld/Data/Core/Textures/UI/Storyteller/{portraitPath}
5. 所有其他 Mod 的 Textures 文件夹
```

**支持的格式**：`.png`, `.jpg`, `.jpeg`

---

### API 请求格式

#### OpenAI Vision API
```http
POST https://api.openai.com/v1/chat/completions
Headers:
  Authorization: Bearer YOUR_API_KEY
  Content-Type: application/json

Body:
{
  "model": "gpt-4-vision-preview",
  "messages": [
    {
      "role": "user",
      "content": [
        {"type": "text", "text": "分析提示词..."},
        {"type": "image_url", "image_url": {"url": "data:image/png;base64,..."}}
      ]
    }
  ],
  "max_tokens": 1000
}
```

#### DeepSeek Vision API
```http
POST https://api.deepseek.com/v1/chat/completions
# 格式与 OpenAI 相同
```

#### Gemini Vision API
```http
POST https://generativelanguage.googleapis.com/v1/models/gemini-pro-vision:generateContent?key=YOUR_API_KEY

Body:
{
  "contents": [
    {
      "parts": [
        {"text": "分析提示词..."},
        {"inline_data": {"mime_type": "image/png", "data": "base64..."}}
      ]
    }
  ]
}
```

---

### 错误处理

系统内置完善的错误处理：

```
1. 立绘未找到 → 返回错误，不调用 API
2. API 调用失败 → 自动回退到本地分析
3. JSON 解析错误 → 提取有效部分
4. 网络超时（60秒） → 显示错误提示
5. 任何异常 → 日志记录 + 用户提示
```

---

## ?? 性能和成本

### 分析速度

| 阶段 | OpenAI | DeepSeek | Gemini |
|-----|--------|---------|--------|
| 图像分析 | 3-5秒 | 2-4秒 | 1-3秒 |
| 文本分析 | 2-3秒 | 1-2秒 | 1-2秒 |
| **总计** | **5-8秒** | **3-6秒** | **2-5秒** |

### 成本对比

| 提供商 | 单次成本 | 100次成本 | 1000次成本 |
|--------|---------|----------|-----------|
| **OpenAI** | $0.05 | $5.00 | $50.00 |
| **DeepSeek** | ?0.01 (~$0.0014) | ?1.00 (~$0.14) | ?10.00 (~$1.40) |
| **Gemini** | $0（免费） | $0（免费） | ~$2.00（超额）|

**推荐**：
- **个人使用**：Gemini（免费）
- **频繁使用**：DeepSeek（性价比）
- **追求质量**：OpenAI（最准确）

---

## ??? 故障排除

### 问题 1：立绘找不到

**症状**：`Image not found: {path}`

**解决**：
```
1. 检查路径是否正确（相对于 Textures 文件夹）
2. 确认文件扩展名（.png, .jpg）
3. 尝试绝对路径
4. 查看日志中的搜索路径
```

### 问题 2：API 调用失败

**症状**：`API call failed: {error}`

**解决**：
```
1. 检查 API Key 是否正确
2. 确认网络连接
3. 查看 API 额度是否用尽
4. 尝试切换提供商
5. 查看详细错误日志
```

### 问题 3：分析结果不准确

**症状**：推断的人格与预期不符

**解决**：
```
1. 优化简介文本（添加更明确的关键词）
2. 尝试不同的 AI 提供商
3. 使用 overridePersonality 手动指定
4. 调整 dialogueStyle 参数微调
```

### 问题 4：分析速度慢

**症状**：超过 30 秒仍未返回

**解决**：
```
1. 检查网络连接速度
2. 尝试 Gemini（最快）
3. 压缩图像文件大小
4. 简化简介文本
```

---

## ?? 最佳实践

### ? 推荐做法

1. **简介文本**：
   - 150-300 词（太短信息不足，太长浪费 Token）
   - 包含明确的人格描述词
   - 描述说话方式和行为倾向

2. **立绘图像**：
   - 分辨率：512×512 或更高
   - 格式：PNG（透明背景更好）
   - 主体清晰，色彩鲜明

3. **提供商选择**：
   - 测试阶段：Gemini（免费）
   - 正式使用：DeepSeek（便宜）
   - 追求极致：OpenAI（最准）

### ? 避免做法

1. **简介过于模糊**："She is complex" → 无法推断
2. **立绘过小**：<256×256 → 细节丢失
3. **频繁重复分析**：浪费 API 额度
4. **忽略本地回退**：API 失败后仍可用本地分析

---

## ?? 未来扩展

### 计划中的功能

- [ ] **批量分析**：一次分析多个人格
- [ ] **分析历史**：保存和对比历史分析结果
- [ ] **半自动优化**：API 建议 + 手动微调
- [ ] **社区分享**：分享分析结果和配置
- [ ] **自定义 Prompt**：用户自定义分析提示词

---

## ?? 相关文档

- [人格生成系统指南.md](人格生成系统指南.md) - 本地分析原理
- [人格快速参考.md](人格快速参考.md) - 速查卡
- [功能总览.md](功能总览.md) - 完整功能列表

---

**版本**: 1.0.0  
**最后更新**: 2024  
**作者**: TheSecondSeat 开发团队

?? **让 AI 帮您设计 AI 人格！** ?
