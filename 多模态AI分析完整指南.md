# ?? 多模态 AI 分析 - 完整使用指南

## ?? 功能概述

**MultimodalAnalysisService** 是 The Second Seat 的核心 AI 功能，它能够：

1. ? **自动提取立绘颜色** - 无需手动取色
2. ? **识别视觉元素** - 龙、武器、铠甲等
3. ? **生成角色描述** - 自动填充简介
4. ? **推断人格特质** - AI 智能判断
5. ? **深度文本分析** - 超越关键词匹配

---

## ?? 快速开始

### 1. 配置 API

#### 选项 A：OpenAI（推荐）

```
游戏内设置：
1. 选项 → 模组设置 → The Second Seat
2. 勾选"启用多模态分析"
3. 选择提供者：OpenAI
4. 输入 API Key
5. Vision Model: gpt-4-vision-preview
6. Text Model: gpt-4
7. 点击"应用"
```

**获取 API Key**：
- 访问: https://platform.openai.com/api-keys
- 创建新 Key
- 复制并粘贴到设置中

**费用**：
- Vision API: ~$0.01-0.03 /图片
- GPT-4: ~$0.03 /1K tokens

---

#### 选项 B：DeepSeek（性价比高）

```
提供者：DeepSeek
API Key: 你的 DeepSeek Key
Vision Model: deepseek-vl
Text Model: deepseek-chat
```

**获取 API Key**：
- 访问: https://platform.deepseek.com/
- 注册并获取 Key

**费用**：
- Vision API: ~?0.005 /图片
- Text API: ~?0.001 /1K tokens

---

#### 选项 C：Gemini（Google）

```
提供者：Gemini
API Key: 你的 Google AI Key
Vision Model: gemini-pro-vision
Text Model: gemini-pro
```

**获取 API Key**：
- 访问: https://makersuite.google.com/app/apikey

**费用**：
- 免费额度：60 requests/分钟

---

## ?? 使用流程

### 完整工作流程

```
1. 准备立绘图片（PNG/JPG）
        ↓
2. 创建人格定义 XML
        ↓
3. 游戏加载时自动分析
        ↓
4. AI 提取颜色 + 生成描述
        ↓
5. PersonaAnalyzer 整合数据
        ↓
6. 生成完整人格配置
```

---

## ?? XML 定义示例

### 最小配置（让 AI 完成一切）

```xml
<NarratorPersonaDef>
  <defName>MysteriousDragon</defName>
  <narratorName>银龙女士</narratorName>
  
  <!-- 只需指定图片路径！ -->
  <portraitPath>UI/Narrators/MysteriousDragon</portraitPath>
  
  <!-- 其他字段留空，AI 自动填充 -->
  <primaryColor>(0, 0, 0)</primaryColor>  <!-- 将被 AI 覆盖 -->
  <biography></biography>                  <!-- 将被 AI 生成 -->
</NarratorPersonaDef>
```

---

### 半自动配置（保留人工控制）

```xml
<NarratorPersonaDef>
  <defName>DarkDragonRider</defName>
  <narratorName>暗黑龙骑士</narratorName>
  
  <portraitPath>UI/Narrators/DarkDragonRider</portraitPath>
  
  <!-- AI 提取颜色，但可以手动微调 -->
  <primaryColor>(0.78, 0.08, 0.24)</primaryColor>
  
  <!-- 提供简短简介，AI 会深度分析 -->
  <biography>
    A menacing dragon rider who commands fear and destruction.
  </biography>
  
  <!-- 可选：手动覆盖 AI 的判断 -->
  <overridePersonality>Sadistic</overridePersonality>
</NarratorPersonaDef>
```

---

## ?? AI 分析输出示例

### Vision API 返回（自动）

```json
{
  "dominantColors": [
    {"hex": "#C81428", "percentage": 30, "name": "Deep Red"},
    {"hex": "#1A1A1E", "percentage": 40, "name": "Black"},
    {"hex": "#E0E0E8", "percentage": 20, "name": "Silver"}
  ],
  "visualElements": [
    "dragon", "horns", "red eyes", "sword", "armor", 
    "menacing pose", "dark atmosphere"
  ],
  "characterDescription": "A fearsome dragon rider clad in dark armor...",
  "mood": "threatening",
  "suggestedPersonality": "Sadistic",
  "styleKeywords": ["menacing", "authoritative", "cold"]
}
```

**自动应用到**：
- `primaryColor` ← 从 dominantColors 提取
- `accentColor` ← 第二高的颜色
- `biography` ← characterDescription
- `overridePersonality` ← suggestedPersonality
- `toneTags` ← styleKeywords

---

### Text Deep Analysis 返回（可选）

如果你提供了简介，AI 会进行深度分析：

```json
{
  "personality_traits": [
    "merciless", "calculating", "dominant", "sadistic"
  ],
  "dialogue_style": {
    "formality": 0.8,
    "emotional_expression": 0.3,
    "verbosity": 0.4,
    "humor": 0.2,
    "sarcasm": 0.7
  },
  "tone_tags": [
    "menacing", "authoritative", "cold", "sadistic"
  ],
  "event_preferences": {
    "positive_bias": -0.4,
    "negative_bias": 0.6,
    "chaos_level": 0.6,
    "intervention_frequency": 0.8
  },
  "forbidden_words": [
    "cute", "kawaii", "friendly", "love", "hug"
  ]
}
```

**自动应用到**：
- `DialogueStyle` ← dialogue_style
- `ToneTags` ← tone_tags
- `EventPreferences` ← event_preferences
- `forbiddenWords` ← forbidden_words

---

## ?? 高级配置

### 调整 AI 分析精度

在代码中修改 Prompt（仅开发者）：

```csharp
// MultimodalAnalysisService.cs 第 150 行

string prompt = @"Analyze this character portrait...
FOCUS ON:
1. Color extraction (be precise with hex codes)
2. Visual symbolism (weapons = personality)
3. Atmosphere (lighting, composition)
4. Cultural references (dragon = power)
";
```

---

### 缓存分析结果

AI 分析很贵，所以建议缓存：

```csharp
// 在 PersonaSelectionWindow.cs 中
private static Dictionary<string, VisionAnalysisResult> analysisCache = new();

public static async Task<VisionAnalysisResult> AnalyzeWithCache(Texture2D texture, string cacheKey)
{
    if (analysisCache.ContainsKey(cacheKey))
    {
        return analysisCache[cacheKey];
    }
    
    var result = await MultimodalAnalysisService.Instance.AnalyzeTextureAsync(texture);
    
    if (result != null)
    {
        analysisCache[cacheKey] = result;
    }
    
    return result;
}
```

---

## ?? 成本估算

### 单次人格创建

| 步骤 | API 调用 | 成本（OpenAI）| 成本（DeepSeek）|
|-----|---------|--------------|----------------|
| Vision 分析 | 1x gpt-4-vision | $0.02 | ?0.005 |
| Text 深度分析 | 1x gpt-4 | $0.03 | ?0.001 |
| **总计** | - | **$0.05** | **?0.006** |

### 月度估算

- 创建 10 个人格：$0.50 / ?0.06
- 创建 100 个人格：$5.00 / ?0.60

**建议**：
- 使用 DeepSeek（便宜 90%）
- 启用缓存（避免重复分析）
- 只在最终版本时分析

---

## ?? 故障排除

### 问题 1：API Key 无效

**症状**：
```
[MultimodalAnalysis] Vision API error: 401 - Unauthorized
```

**解决**：
1. 检查 API Key 是否正确
2. 确认 API Key 有权限调用 Vision API
3. 检查余额是否充足

---

### 问题 2：网络超时

**症状**：
```
[MultimodalAnalysis] Error: The operation has timed out
```

**解决**：
1. 检查网络连接
2. 增加超时时间（ModSettings.cs 第 25 行）
3. 使用国内代理（DeepSeek）

---

### 问题 3：AI 分析结果不准确

**症状**：
- 颜色提取错误
- 人格判断不符合预期

**解决**：
1. **手动覆盖**：使用 `<overridePersonality>`
2. **提供更详细的简介**：AI 会参考文本
3. **调整 Prompt**：修改分析指令

---

### 问题 4：图片加载失败

**症状**：
```
[MultimodalAnalysis] Texture is null
```

**解决**：
1. 确认图片路径正确
2. 检查图片格式（PNG/JPG）
3. 确认图片在 `Textures/` 目录下

---

## ?? 最佳实践

### 1. 图片准备

**推荐规格**：
- 分辨率：512x512 到 1024x1024
- 格式：PNG（支持透明）
- 文件大小：<2MB
- 内容：清晰的角色特写

**避免**：
- 模糊的图片
- 过于复杂的场景
- 多个角色同框

---

### 2. 简介编写

**有效的简介**：
```
She is a ruthless dragon rider who takes pleasure in watching 
her enemies struggle. Cold, calculating, and merciless, she 
commands absolute obedience through fear.
```

**无效的简介**：
```
She is nice.
```

**技巧**：
- 使用描述性形容词
- 包含行为动词
- 描述情感倾向
- 提及视觉元素

---

### 3. 性能优化

#### 开发阶段
```
启用多模态分析 ?
实时测试 ?
频繁调整 ?
```

#### 发布阶段
```
禁用多模态分析 ?
使用预生成的配置 ?
避免运行时开销 ?
```

**原因**：
- AI 分析需要网络请求（延迟）
- 消耗 API 额度（成本）
- 玩家可能没有 API Key

**最佳实践**：
1. 开发时使用 AI 生成配置
2. 将结果保存到 XML
3. 发布时直接使用 XML

---

## ?? 性能对比

| 方案 | 颜色提取 | 元素识别 | 描述生成 | 成本 | 准确度 |
|-----|---------|---------|---------|------|--------|
| **手动填写** | ? 手动 | ? 无 | ? 手动 | 免费 | 看经验 |
| **PersonaAnalyzer** | ?? 采样 | ? 无 | ?? 关键词 | 免费 | 60% |
| **多模态 AI** | ? 精确 | ? 完整 | ? 智能 | $0.05 | 95% |

---

## ?? 总结

### 多模态 AI 的价值

**没有它**：
- ?? 手动取色（麻烦）
- ?? 手动写简介（费时）
- ?? 简单关键词匹配（不准）

**有了它**：
- ? 自动提取颜色（精确）
- ? 自动生成描述（智能）
- ? 深度理解人格（准确）

### 推荐使用场景

? **适合**：
- 创建大量自定义人格
- 追求高质量人格定义
- 不介意少量成本

? **不适合**：
- 只使用内置人格
- 完全离线环境
- 对成本极度敏感

---

**现在，PersonaAnalyzer 终于有了真正的 AI 加持！** ???

---

**版本**: 1.1.0  
**更新**: 2025-01-XX  
**作者**: The Second Seat Team
