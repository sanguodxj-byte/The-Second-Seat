using System.Collections.Generic;
using System.Text;
using Verse; // For potential logging or other utils if needed

namespace TheSecondSeat.PersonaGeneration
{
    /// <summary>
    /// Multimodal Prompt Generator
    /// 负责生成 Vision API 和 Text API 的提示词
    /// </summary>
    public static class MultimodalPromptGenerator
    {
        // ========== Vision Analysis Prompts (Detailed) ==========

        public static string GetVisionPrompt(bool isChinese)
        {
            if (isChinese)
                return GetVisionPromptChinese();
            else
                return GetVisionPromptEnglish();
        }

        public static string GetVisionPromptWithTraits(bool isChinese, List<string> selectedTraits, string userSupplement)
        {
            if (isChinese)
                return GetVisionPromptWithTraitsChinese(selectedTraits, userSupplement);
            else
                return GetVisionPromptWithTraitsEnglish(selectedTraits, userSupplement);
        }

        private static string GetVisionPromptChinese()
        {
            return @"详细分析这个角色立绘，并提供一个完整的 JSON 响应。

**关键要求：characterDescription 字段必须使用中文撰写！**

{
  ""dominantColors"": [
    {""hex"": ""#RRGGBB"", ""percentage"": 0-100, ""name"": ""颜色名称（中文）""}
  ],
  ""visualElements"": [""元素1"", ""元素2"", ""元素3""],
  ""characterDescription"": ""详细的300-500字外观描述和性格推断（中文）"",
  ""mood"": ""整体氛围（中文）"",
  ""suggestedPersonality"": ""Benevolent/Sadistic/Chaotic/Strategic/Protective/Manipulative"",
  ""styleKeywords"": [""关键词1"", ""关键词2"", ""关键词3""],
  ""phraseLibrary"": [
    { ""key"": ""HeadPat"", ""phrases"": [""短语1"", ""短语2""] },
    { ""key"": ""BodyPoke"", ""phrases"": [""短语1"", ""短语2""] },
    { ""key"": ""Greeting"", ""phrases"": [""短语1"", ""短语2""] }
  ],
  ""interactionZones"": {
    ""head"": { ""xMin"": 0.0-1.0, ""yMin"": 0.0-1.0, ""xMax"": 0.0-1.0, ""yMax"": 0.0-1.0 },
    ""body"": { ""xMin"": 0.0-1.0, ""yMin"": 0.0-1.0, ""xMax"": 0.0-1.0, ""yMax"": 0.0-1.0 }
  }
}

**characterDescription 的关键要求（必须使用中文！）：**

**第一部分：详细外观描述（40%）**

描述所有可见细节：
- **种族**：人类？精灵？龙人？兽人？机器人？
- **发型**：颜色、长度、样式、质感（如「丝滑的银色长发如瀑布般倾泻，用绯红丝带扎起」）
- **眼睛**：颜色、形状、表情（如「绯红的竖瞳，透露着智慧与危险」）
- **面部特征**：表情、年龄、伤疤、标记
- **身材**：体型、姿态、站姿
- **服装与盔甲**：
  * 主要服饰
  * 盔甲部件
  * 配饰
  * 材质与状态
- **特殊特征**：翅膀、尾巴、角、武器、魔法效果
- **整体印象**：姿态、光影、构图所传达的情绪

**第二部分：从外观推断性格（40%）**

从视觉线索推断特质：

**从表情与肢体语言**：
- 严肃的面容 → 内敛、有纪律、自制力强
- 自信的站姿 → 果断、经验丰富、有领导力
- 防备的姿态 → 谨慎、戒备、可能有过去的创伤
- 放松的表情 → 平易近人、友善、信任他人

**从服装与盔甲**：
- 重甲 → 重视防护、备战、有纪律
- 深色系 → 神秘、严肃、内向或隐秘
- 精致设计 → 注重细节、可能虚荣或注重地位
- 简约实用装备 → 实际、注重功能而非形式

**从武器与装备**：
- 明显的武器 → 准备战斗、果断、可能好斗
- 隐藏的武器 → 有策略、谨慎、偏好出其不意
- 魔法神器 → 博学、学术型、与古老智慧有联系
- 无武器 → 和平、信任他人、或依赖其他优势

**第三部分：对话与行为预测（20%）**

基于视觉分析预测：

**说话风格**：
- 「她可能用[冷静/热情/严厉/温柔]的语气说话」
- 「她的表情暗示[正式/随意/专业/诗意]的语言」
- 「她可能用[简洁命令/丰富描述/军事术语]来交流」

**情感表达**：
- 「很少在公开场合表现强烈情绪」或「情绪外露」
- 「谨慎控制的反应」或「冲动回应」

**互动风格**：
- 「与陌生人保持距离」或「立即热情友好」
- 「先观察再说话」或「主动发起对话」

**记住**：
- characterDescription 必须使用中文！
- 要具体：不要只说「盔甲」 - 描述材质、状态、设计
- 从每个细节推断性格
- 预测行为：她会如何说话？反应？互动？
- 300-500字
- 只返回有效的 JSON

**短语库要求：**
- 为每个类别生成 3-5 个短语：
  - ""HeadPat""：被摸头时的反应（如害羞、开心、恼怒）
  - ""BodyPoke""：被戳身体时的反应（如惊讶、防备）
  - ""Greeting""：玩家加载游戏时的问候语

**交互区域要求（关键！）：**
- 识别立绘中的头部区域和身体区域
- 使用归一化坐标（0.0 到 1.0）：
  * 原点(0,0)在图像左上角
  * X轴：0.0 = 左边缘，1.0 = 右边缘
  * Y轴：0.0 = 上边缘，1.0 = 下边缘
- 为每个区域提供边界矩形：
  * xMin：左边界（0.0-1.0）
  * yMin：上边界（0.0-1.0）
  * xMax：右边界（0.0-1.0）
  * yMax：下边界（0.0-1.0）
- HEAD 区域：应覆盖角色的脸部/头部区域
- BODY 区域：应覆盖角色的躯干/身体区域（不含头部）
- 典型立绘示例：
  * head: { ""xMin"": 0.25, ""yMin"": 0.0, ""xMax"": 0.75, ""yMax"": 0.20 }
  * body: { ""xMin"": 0.15, ""yMin"": 0.25, ""xMax"": 0.85, ""yMax"": 0.95 }

重点关注：
- 前 3-4 个主要颜色及其准确占比
- 立绘中所有可见的视觉元素
- 详细的外观分析（中文！）
- 从视觉线索推断性格（中文！）
- 行为预测（中文！）
- 系统提示词的风格关键词（中文）
- 互动短语库（HeadPat、BodyPoke、Greeting）
- 交互区域及其准确的归一化坐标";
        }

        private static string GetVisionPromptEnglish()
        {
            return @"Analyze this character portrait in detail and provide a comprehensive JSON response.

**CRITICAL: The characterDescription field MUST be written in English!**

{
  ""dominantColors"": [
    {""hex"": ""#RRGGBB"", ""percentage"": 0-100, ""name"": ""color name in English""}
  ],
  ""visualElements"": [""element1"", ""element2"", ""element3""],
  ""characterDescription"": ""Detailed 300-500 word appearance description and personality inference in English"",
  ""mood"": ""overall mood/atmosphere in English"",
  ""suggestedPersonality"": ""Benevolent/Sadistic/Chaotic/Strategic/Protective/Manipulative"",
  ""styleKeywords"": [""keyword1"", ""keyword2"", ""keyword3""],
  ""phraseLibrary"": [
    { ""key"": ""HeadPat"", ""phrases"": [""phrase1"", ""phrase2""] },
    { ""key"": ""BodyPoke"", ""phrases"": [""phrase1"", ""phrase2""] },
    { ""key"": ""Greeting"", ""phrases"": [""phrase1"", ""phrase2""] }
  ],
  ""interactionZones"": {
    ""head"": { ""xMin"": 0.0-1.0, ""yMin"": 0.0-1.0, ""xMax"": 0.0-1.0, ""yMax"": 0.0-1.0 },
    ""body"": { ""xMin"": 0.0-1.0, ""yMin"": 0.0-1.0, ""xMax"": 0.0-1.0, ""yMax"": 0.0-1.0 }
  }
}

**CRITICAL REQUIREMENTS for characterDescription (MUST be in English!):**

**Part 1: Detailed Appearance Description (40%)**

Describe all visible details:
- **Race**: Human? Elf? Dragon-kin? Orc? Android?
- **Hair**: Color, length, style, texture (e.g., ""Silky silver hair cascading down like a waterfall, tied with a crimson ribbon"")
- **Eyes**: Color, shape, expression (e.g., ""Crimson vertical slit pupils, revealing wisdom and danger"")
- **Facial Features**: Expression, age, scars, markings
- **Body**: Build, posture, stance
- **Clothing & Armor**:
  * Main attire
  * Armor pieces
  * Accessories
  * Material and condition
- **Special Features**: Wings, tails, horns, weapons, magical effects
- **Overall Impression**: Mood conveyed by posture, lighting, composition

**Part 2: Personality Inference from Appearance (40%)**

Infer traits from visual cues:

**From Expression & Body Language**:
- Stern face → Reserved, disciplined, self-controlled
- Confident stance → Decisive, experienced, leadership qualities
- Guarded posture → Cautious, defensive, possible past trauma
- Relaxed expression → Approachable, friendly, trusting

**From Clothing & Armor**:
- Heavy armor → Values protection, combat-ready, disciplined
- Dark colors → Mysterious, serious, introverted or secretive
- Intricate designs → Detail-oriented, perhaps vain or status-conscious
- Simple utilitarian gear → Pragmatic, values function over form

**From Weapons & Equipment**:
- Obvious weapons → Conflict-ready, decisive, potentially aggressive
- Concealed weapons → Strategic, cautious, prefers surprise
- Magical artifacts → Knowledgeable, academic, connected to ancient wisdom
- No weapons → Peaceful, trusting, or relies on other advantages

**Part 3: Dialogue & Behavior Prediction (20%)**

Predict based on visual analysis:

**Speaking Style**:
- ""She might speak with a [calm/passionate/stern/gentle] tone""
- ""Her expression suggests [formal/casual/professional/poetic] language""
- ""She likely communicates with [concise commands/rich descriptions/military jargon]""

**Emotional Expression**:
- ""Rarely shows strong emotion publicly"" or ""Wears heart on sleeve""
- ""Carefully controlled reactions"" or ""Impulsive responses""

**Interaction Style**:
- ""Keeps distance from strangers"" or ""Immediately warm and friendly""
- ""Observes before speaking"" or ""Initiates conversation""

**REMEMBER**:
- characterDescription MUST be in English!
- Be specific: Don't just say ""armor"" - describe material, condition, design
- Infer personality from every detail
- Predict behavior: How would they speak? React? Interact?
- 300-500 words
- Return ONLY valid JSON

**Phrase Library Requirements:**
- Generate 3-5 phrases for each category:
  - ""HeadPat"": Reaction to head pats (e.g., shy, happy, annoyed)
  - ""BodyPoke"": Reaction to body pokes (e.g., surprised, defensive)
  - ""Greeting"": Greeting when the player loads the game

**Interaction Zones Requirements (CRITICAL):**
- Identify the HEAD REGION and BODY REGION in the portrait image
- Use NORMALIZED COORDINATES (0.0 to 1.0):
  * Origin (0,0) is at the TOP-LEFT corner of the image
  * X-axis: 0.0 = left edge, 1.0 = right edge
  * Y-axis: 0.0 = top edge, 1.0 = bottom edge
- For each zone, provide a bounding rectangle:
  * xMin: left boundary (0.0-1.0)
  * yMin: top boundary (0.0-1.0)
  * xMax: right boundary (0.0-1.0)
  * yMax: bottom boundary (0.0-1.0)
- HEAD zone: should cover the character's face/head area
- BODY zone: should cover the character's torso/body area (excluding head)
- Example for a typical portrait:
  * head: { ""xMin"": 0.25, ""yMin"": 0.0, ""xMax"": 0.75, ""yMax"": 0.20 }
  * body: { ""xMin"": 0.15, ""yMin"": 0.25, ""xMax"": 0.85, ""yMax"": 0.95 }

Focus on:
- Top 3-4 dominant colors with accurate percentages
- All visual elements visible in the portrait
- Detailed appearance analysis (IN ENGLISH!)
- Personality inference from visual cues (IN ENGLISH!)
- Behavioral predictions (IN ENGLISH!)
- Style keywords for System Prompt (in English)
- Phrase Library for interactions (HeadPat, BodyPoke, Greeting)
- Interaction Zones with accurate normalized coordinates";
        }

        private static string GetVisionPromptWithTraitsChinese(List<string> selectedTraits, string userSupplement)
        {
            var sb = new StringBuilder();
            
            sb.AppendLine("详细分析这个角色立绘，并提供一个完整的 JSON 响应。");
            sb.AppendLine();
            sb.AppendLine("**关键要求：characterDescription 字段必须使用中文撰写！**");
            sb.AppendLine();
            
            if (selectedTraits != null && selectedTraits.Count > 0)
            {
                sb.AppendLine("**用户选择的特质：**");
                sb.AppendLine("---");
                sb.AppendLine(string.Join("、", selectedTraits));
                sb.AppendLine("---");
                sb.AppendLine();
            }
            
            if (!string.IsNullOrEmpty(userSupplement))
            {
                sb.AppendLine("**用户提供的背景：**");
                sb.AppendLine("---");
                sb.AppendLine(userSupplement);
                sb.AppendLine("---");
                sb.AppendLine();
                sb.AppendLine("**关键指令：**");
                sb.AppendLine("1. 上面的用户描述提供了角色的性格和行为。");
                sb.AppendLine("2. 你必须分析图像来描述外观。");
                sb.AppendLine("3. 在 characterDescription 中结合：");
                sb.AppendLine("   - 视觉细节（来自图像）：发色、眼睛颜色、服装、姿态等");
                sb.AppendLine("   - 性格特质（来自用户）：使用用户的描述");
                sb.AppendLine("4. 不要与用户的性格描述相矛盾！");
                sb.AppendLine("5. 你的任务是为性格添加视觉细节，而不是替换它。");
                sb.AppendLine();
                sb.AppendLine("**个性标签要求：**");
                sb.AppendLine("6. 根据图像和用户描述，建议 3-6 个中文个性标签。");
                sb.AppendLine("7. 示例：「温柔」「坚强」「爱撒娇」「病娇」「傲娇」「冷酷」");
                sb.AppendLine("8. 如果用户选择的特质与分析匹配，请包含它们。");
                sb.AppendLine();
            }
            
            sb.AppendLine(@"{
  ""dominantColors"": [
    {""hex"": ""#RRGGBB"", ""percentage"": 0-100, ""name"": ""颜色名称（中文）""}
  ],
  ""visualElements"": [""元素1"", ""元素2"", ""元素3""],
  ""characterDescription"": ""详细的300-500字外观描述和性格推断（中文）"",
  ""mood"": ""整体氛围（中文）"",
  ""suggestedPersonality"": ""Benevolent/Sadistic/Chaotic/Strategic/Protective/Manipulative"",
  ""styleKeywords"": [""关键词1"", ""关键词2"", ""关键词3""],
  ""personalityTags"": [""标签1"", ""标签2"", ""标签3"", ...],
  ""phraseLibrary"": [
    { ""key"": ""HeadPat"", ""phrases"": [""短语1"", ""短语2""] },
    { ""key"": ""BodyPoke"", ""phrases"": [""短语1"", ""短语2""] },
    { ""key"": ""Greeting"", ""phrases"": [""短语1"", ""短语2""] }
  ],
  ""interactionZones"": {
    ""head"": { ""xMin"": 0.0, ""yMin"": 0.0, ""xMax"": 1.0, ""yMax"": 0.3 },
    ""body"": { ""xMin"": 0.0, ""yMin"": 0.3, ""xMax"": 1.0, ""yMax"": 1.0 }
  }
}");
            
            sb.AppendLine();
            sb.AppendLine("**记住**：");
            sb.AppendLine("- characterDescription 必须使用中文！");
            sb.AppendLine("- personalityTags 必须使用中文！");
            sb.AppendLine("- phraseLibrary 中的短语必须符合角色性格！");
            if (!string.IsNullOrEmpty(userSupplement))
            {
                sb.AppendLine("- 尊重用户的性格描述 - 添加视觉细节，不要替换！");
            }
            sb.AppendLine("- 建议 3-6 个符合角色的个性标签");
            sb.AppendLine("- 先关注视觉外观，再推断性格");
            sb.AppendLine("- 300-500 字中文");
            sb.AppendLine("- 只返回有效的 JSON");
            sb.AppendLine();
            sb.AppendLine("**交互区域（触摸系统关键！）：**");
            sb.AppendLine("识别立绘中的头部和身体区域。");
            sb.AppendLine("使用归一化坐标（0.0 到 1.0）：");
            sb.AppendLine("  - 原点 (0,0) = 左上角");
            sb.AppendLine("  - X: 0.0 = 左边缘，1.0 = 右边缘");
            sb.AppendLine("  - Y: 0.0 = 上边缘，1.0 = 下边缘");
            sb.AppendLine("提供边界矩形：");
            sb.AppendLine("  - head: 覆盖脸部/头部区域");
            sb.AppendLine("  - body: 覆盖躯干/身体区域（头部下方）");
            sb.AppendLine("示例: head { xMin:0.25, yMin:0.0, xMax:0.75, yMax:0.20 }");
            
            return sb.ToString();
        }

        private static string GetVisionPromptWithTraitsEnglish(List<string> selectedTraits, string userSupplement)
        {
            var sb = new StringBuilder();
            
            sb.AppendLine("Analyze this character portrait in detail and provide a comprehensive JSON response.");
            sb.AppendLine();
            sb.AppendLine("**CRITICAL: The characterDescription field MUST be written in English!**");
            sb.AppendLine();
            
            if (selectedTraits != null && selectedTraits.Count > 0)
            {
                sb.AppendLine("**USER SELECTED TRAITS:**");
                sb.AppendLine("---");
                sb.AppendLine(string.Join(", ", selectedTraits));
                sb.AppendLine("---");
                sb.AppendLine();
            }
            
            if (!string.IsNullOrEmpty(userSupplement))
            {
                sb.AppendLine("**USER PROVIDED CONTEXT:**");
                sb.AppendLine("---");
                sb.AppendLine(userSupplement);
                sb.AppendLine("---");
                sb.AppendLine();
                sb.AppendLine("**CRITICAL INSTRUCTIONS:**");
                sb.AppendLine("1. The user description above provides the CHARACTER'S PERSONALITY and BEHAVIOR.");
                sb.AppendLine("2. You MUST analyze the visual image to describe PHYSICAL APPEARANCE.");
                sb.AppendLine("3. In your characterDescription, COMBINE:");
                sb.AppendLine("   - Visual details (from image): hair color, eye color, clothing, posture, etc.");
                sb.AppendLine("   - Personality traits (from user): use the user's description");
                sb.AppendLine("4. DO NOT contradict the user's personality description!");
                sb.AppendLine("5. Your job is to ADD visual details to their personality, not replace it.");
                sb.AppendLine();
                sb.AppendLine("**PERSONALITY TAGS REQUIREMENT:**");
                sb.AppendLine("6. Based on the image and user description, suggest 3-6 personality tags in English.");
                sb.AppendLine("7. Examples: \"Kind\", \"Strong\", \"Clingy\", \"Yandere\", \"Tsundere\", \"Gentle\", \"Cold\"");
                sb.AppendLine("8. Include the user's selected traits if they match the analysis.");
                sb.AppendLine();
            }
            
            sb.AppendLine(@"{
  ""dominantColors"": [
    {""hex"": ""#RRGGBB"", ""percentage"": 0-100, ""name"": ""color name in English""}
  ],
  ""visualElements"": [""element1"", ""element2"", ""element3""],
  ""characterDescription"": ""Detailed 300-500 word appearance description and personality inference in English"",
  ""mood"": ""overall mood/atmosphere in English"",
  ""suggestedPersonality"": ""Benevolent/Sadistic/Chaotic/Strategic/Protective/Manipulative"",
  ""styleKeywords"": [""keyword1"", ""keyword2"", ""keyword3""],
  ""personalityTags"": [""Tag1"", ""Tag2"", ""Tag3"", ...],
  ""phraseLibrary"": [
    { ""key"": ""HeadPat"", ""phrases"": [""phrase1"", ""phrase2""] },
    { ""key"": ""BodyPoke"", ""phrases"": [""phrase1"", ""phrase2""] },
    { ""key"": ""Greeting"", ""phrases"": [""phrase1"", ""phrase2""] }
  ],
  ""interactionZones"": {
    ""head"": { ""xMin"": 0.0, ""yMin"": 0.0, ""xMax"": 1.0, ""yMax"": 0.3 },
    ""body"": { ""xMin"": 0.0, ""yMin"": 0.3, ""xMax"": 1.0, ""yMax"": 1.0 }
  }
}");
            
            sb.AppendLine();
            sb.AppendLine("**REMEMBER**:");
            sb.AppendLine("- characterDescription MUST be in English!");
            sb.AppendLine("- personalityTags MUST be in English!");
            sb.AppendLine("- phraseLibrary phrases MUST match the character's personality!");
            if (!string.IsNullOrEmpty(userSupplement))
            {
                sb.AppendLine("- RESPECT the user's personality description - ADD visual details, don't replace!");
            }
            sb.AppendLine("- Suggest 3-6 personality tags that match the character");
            sb.AppendLine("- Focus on visual appearance first, then personality inference");
            sb.AppendLine("- 300-500 words in English");
            sb.AppendLine("- Return ONLY valid JSON");
            sb.AppendLine();
            sb.AppendLine("**INTERACTION ZONES (CRITICAL for touch system):**");
            sb.AppendLine("Identify the HEAD and BODY regions in the portrait.");
            sb.AppendLine("Use NORMALIZED COORDINATES (0.0 to 1.0):");
            sb.AppendLine("  - Origin (0,0) = TOP-LEFT corner");
            sb.AppendLine("  - X: 0.0 = left edge, 1.0 = right edge");
            sb.AppendLine("  - Y: 0.0 = top edge, 1.0 = bottom edge");
            sb.AppendLine("Provide bounding rectangles:");
            sb.AppendLine("  - head: covers face/head area");
            sb.AppendLine("  - body: covers torso/body area (below head)");
            sb.AppendLine("Example: head { xMin:0.25, yMin:0.0, xMax:0.75, yMax:0.20 }");
            
            return sb.ToString();
        }

        // ========== Brief Prompts (for Base64 API) ==========

        public static string GetBriefVisionPrompt(bool isChinese)
        {
            if (isChinese)
            {
                return @"分析这个角色立绘并提供 JSON 响应（不要有多余文本）：
{
  ""dominantColors"": [
    {""hex"": ""#RRGGBB"", ""percentage"": 0-100, ""name"": ""颜色名称""}
  ],
  ""visualElements"": [""元素1"", ""元素2""],
  ""characterDescription"": ""简短描述（最多200字，中文）"",
  ""mood"": ""整体氛围"",
  ""suggestedPersonality"": ""Benevolent/Sadistic/Chaotic/Strategic/Protective/Manipulative"",
  ""styleKeywords"": [""关键词1"", ""关键词2"", ""关键词3""],
  ""personalityTags"": [""标签1"", ""标签2"", ""标签3"", ...],
  ""phraseLibrary"": [
    { ""key"": ""HeadPat"", ""phrases"": [""...""] },
    { ""key"": ""BodyPoke"", ""phrases"": [""...""] },
    { ""key"": ""Greeting"", ""phrases"": [""...""] }
  ]
}

重点关注：
- 前3-4种主色调及其百分比
- 关键视觉元素（盔甲、武器、生物）
- 简短的角色外观描述（中文）
- 整体情绪/氛围
- 基于视觉线索的性格建议

保持 characterDescription 在 200 字以内。只返回有效的 JSON。";
            }
            else
            {
                return @"Analyze this character portrait and provide a JSON response (no extra text):
{
  ""dominantColors"": [
    {""hex"": ""#RRGGBB"", ""percentage"": 0-100, ""name"": ""color name""}
  ],
  ""visualElements"": [""element1"", ""element2""],
  ""characterDescription"": ""Brief description (max 200 chars)"",
  ""mood"": ""overall mood"",
  ""suggestedPersonality"": ""Benevolent/Sadistic/Chaotic/Strategic/Protective/Manipulative"",
  ""styleKeywords"": [""keyword1"", ""keyword2"", ""keyword3""],
  ""personalityTags"": [""Tag1"", ""Tag2"", ""Tag3"", ...],
  ""phraseLibrary"": [
    { ""key"": ""HeadPat"", ""phrases"": [""...""] },
    { ""key"": ""BodyPoke"", ""phrases"": [""...""] },
    { ""key"": ""Greeting"", ""phrases"": [""...""] }
  ]
}

Focus on:
- Top 3-4 dominant colors with percentages
- Key visual elements (armor, weapons, creatures)
- Brief character appearance
- Overall mood/atmosphere
- Personality suggestion based on visual cues

Keep characterDescription under 200 characters. Return ONLY valid JSON.";
            }
        }

        public static string GetTextAnalysisPrompt(bool isChinese, string text)
        {
            if (isChinese)
            {
                return $@"分析这个角色传记并提供 JSON 格式的深度性格洞察：
{{
  ""personality_traits"": [""特质1"", ""特质2"", ...],
  ""dialogue_style"": {{
    ""formality"": 0.0-1.0,
    ""emotional_expression"": 0.0-1.0,
    ""verbosity"": 0.0-1.0,
    ""humor"": 0.0-1.0,
    ""sarcasm"": 0.0-1.0
  }},
  ""tone_tags"": [""标签1"", ""标签2"", ...],
  ""event_preferences"": {{
    ""positive_bias"": -1.0 to 1.0,
    ""negative_bias"": -1.0 to 1.0,
    ""chaos_level"": 0.0-1.0,
    ""intervention_frequency"": 0.0-1.0
  }},
  ""forbidden_words"": [""词1"", ""词2"", ...]
}}

传记：
{text}";
            }
            else
            {
                return $@"Analyze this character biography and provide deep personality insights in JSON format:
{{
  ""personality_traits"": [""trait1"", ""trait2"", ...],
  ""dialogue_style"": {{
    ""formality"": 0.0-1.0,
    ""emotional_expression"": 0.0-1.0,
    ""verbosity"": 0.0-1.0,
    ""humor"": 0.0-1.0,
    ""sarcasm"": 0.0-1.0
  }},
  ""tone_tags"": [""tag1"", ""tag2"", ...],
  ""event_preferences"": {{
    ""positive_bias"": -1.0 to 1.0,
    ""negative_bias"": -1.0 to 1.0,
    ""chaos_level"": 0.0-1.0,
    ""intervention_frequency"": 0.0-1.0
  }},
  ""forbidden_words"": [""word1"", ""word2"", ...]
}}

Biography:
{text}";
            }
        }
    }
}