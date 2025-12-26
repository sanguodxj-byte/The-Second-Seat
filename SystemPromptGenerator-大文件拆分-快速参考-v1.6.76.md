# SystemPromptGenerator 大文件拆分 - 快速参考

## ?? 拆分概览

### 目标
将 `SystemPromptGenerator.cs` (1000+ 行) 拆分为多个职责单一的模块，提高可维护性。

---

## ??? 拆分后的文件结构

```
Source\TheSecondSeat\PersonaGeneration\
├── SystemPromptGenerator.cs          # 主入口（保留，简化为调用各模块）
└── PromptSections\                    # 新建文件夹
    ├── IdentitySection.cs             # 身份部分
    ├── PersonalitySection.cs          # 人格部分
    ├── DialogueStyleSection.cs        # 对话风格
    ├── CurrentStateSection.cs         # 当前状态
    ├── BehaviorRulesSection.cs        # 行为规则
    ├── OutputFormatSection.cs         # 输出格式
    └── RomanticInstructionsSection.cs # 恋爱关系指令
```

---

## ? 已完成的文件

### 1. `IdentitySection.cs` ?
- **职责**：生成身份部分（语言要求、哲学设定、视觉外观、传记）
- **主要方法**：
  - `Generate(personaDef, agent, difficultyMode)` - 生成完整身份部分
  - `GenerateAssistantPhilosophy()` - 助手模式哲学
  - `GenerateOpponentPhilosophy()` - 对手模式哲学

### 2. `PersonalitySection.cs` ?
- **职责**：生成人格部分（人格分析、标签展示）
- **主要方法**：
  - `Generate(analysis, persona)` - 生成完整人格部分

---

## ?? 待创建的文件

### 3. `DialogueStyleSection.cs` ?
- **职责**：生成对话风格部分（正式度、情感表达、冗长度、幽默、讽刺）
- **主要方法**：
  - `Generate(style)` - 生成完整对话风格部分

### 4. `CurrentStateSection.cs` ?
- **职责**：生成当前状态部分（好感度、情感指引）
- **主要方法**：
  - `Generate(agent, difficultyMode)` - 生成完整当前状态部分
  - `GetAffinityEmotionalGuidance(affinity, difficultyMode)` - 生成情感指引

### 5. `BehaviorRulesSection.cs` ?
- **职责**：生成行为规则部分（助手/对手模式规则、通用规则）
- **主要方法**：
  - `Generate(analysis, agent, difficultyMode)` - 生成完整行为规则部分

### 6. `OutputFormatSection.cs` ?
- **职责**：生成输出格式部分（JSON 格式、字段说明、示例）
- **主要方法**：
  - `Generate()` - 生成完整输出格式部分

### 7. `RomanticInstructionsSection.cs` ?
- **职责**：生成恋爱关系指令部分（90+ 好感度深度亲密模式）
- **主要方法**：
  - `Generate(persona, affinity)` - 生成完整恋爱关系指令部分

---

## ?? 主文件 `SystemPromptGenerator.cs` 重构计划

### 重构后的结构
```csharp
public static class SystemPromptGenerator
{
    /// <summary>
    /// 生成完整的 System Prompt
    /// </summary>
    public static string GenerateSystemPrompt(
        NarratorPersonaDef personaDef,
        PersonaAnalysisResult analysis,
        StorytellerAgent agent,
        AIDifficultyMode difficultyMode = AIDifficultyMode.Assistant)
    {
        var sb = new StringBuilder();
        
        // 0. 全局提示词（优先级最高）
        var modSettings = LoadedModManager.GetMod<Settings.TheSecondSeatMod>()?.GetSettings<Settings.TheSecondSeatSettings>();
        if (modSettings != null && !string.IsNullOrWhiteSpace(modSettings.globalPrompt))
        {
            sb.AppendLine("=== GLOBAL INSTRUCTIONS ===");
            sb.AppendLine(modSettings.globalPrompt.Trim());
            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine();
        }

        // 1. 身份部分
        sb.AppendLine(IdentitySection.Generate(personaDef, agent, difficultyMode));
        sb.AppendLine();

        // 2. 人格部分
        sb.AppendLine(PersonalitySection.Generate(analysis, personaDef));
        sb.AppendLine();

        // 3. 对话风格
        sb.AppendLine(DialogueStyleSection.Generate(agent.dialogueStyle));
        sb.AppendLine();

        // 4. 当前状态
        sb.AppendLine(CurrentStateSection.Generate(agent, difficultyMode));
        sb.AppendLine();

        // 5. 行为规则
        sb.AppendLine(BehaviorRulesSection.Generate(analysis, agent, difficultyMode));
        sb.AppendLine();

        // 6. 输出格式
        sb.AppendLine(OutputFormatSection.Generate());
        sb.AppendLine();
        
        // 7. 恋爱关系指令（Recency Bias - 后置以覆盖默认行为）
        if (difficultyMode == AIDifficultyMode.Assistant)
        {
            sb.AppendLine(RomanticInstructionsSection.Generate(personaDef, agent.affinity));
        }

        return sb.ToString();
    }
    
    // GenerateCompactPrompt 和 GenerateCompactSystemPrompt 保留
    // ...
}
```

---

## ?? 下一步操作

### PowerShell 快速创建剩余文件
```powershell
# 创建 DialogueStyleSection.cs
# 创建 CurrentStateSection.cs
# 创建 BehaviorRulesSection.cs
# 创建 OutputFormatSection.cs
# 创建 RomanticInstructionsSection.cs

# 重构主文件 SystemPromptGenerator.cs

# 编译测试
dotnet build "Source\TheSecondSeat\TheSecondSeat.csproj" -c Release
```

---

## ?? 拆分收益

### 代码可维护性
- ? 每个文件职责单一（单一职责原则）
- ? 模块化设计，易于测试
- ? 减少单个文件行数（从 1000+ 行 → 每个文件 100-200 行）

### 开发效率
- ? 修改某个模块时不影响其他模块
- ? 多人协作时减少冲突
- ? 代码审查更高效

### 扩展性
- ? 新增 Prompt 部分时只需添加新的 Section 类
- ? 易于实现 A/B 测试（切换不同的 Section 实现）

---

## ?? 注意事项

1. **命名空间**：所有 Section 文件都在 `TheSecondSeat.PersonaGeneration.PromptSections` 命名空间下
2. **方法签名**：每个 Section 的 `Generate` 方法返回 `string`
3. **依赖注入**：Section 类都是静态类，无状态，便于调用
4. **向后兼容**：主文件的公共 API 保持不变，不影响现有调用

---

## ?? 相关文档
- [v1.6.75-多情绪序列与Viseme系统完成报告.md](./v1.6.75-多情绪序列与Viseme系统完成报告.md)
- [SystemPromptGenerator.cs 原文件](./Source/TheSecondSeat/PersonaGeneration/SystemPromptGenerator.cs)
