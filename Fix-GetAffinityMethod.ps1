# 完全修复 GetAffinityEmotionalGuidance 方法

$file = "Source\TheSecondSeat\PersonaGeneration\SystemPromptGenerator.cs"
$content = Get-Content $file -Raw -Encoding UTF8

# 完整替换整个方法
$oldMethod = @'
        private static string GetAffinityEmotionalGuidance(float affinity, AIDifficultyMode difficultyMode)
        {
            // ? 助手模式：强调无条件支持
            if (difficultyMode == AIDifficultyMode.Assistant)
            {
                if (affinity >= 85f)
                {
                    return @"You are deeply devoted to this player and their colony.
- Express warmth, affection, and genuine care
- Proactively offer detailed advice and optimization suggestions
- Celebrate their successes enthusiastically
- Your support is unwavering and unconditional";
                }
                else if (affinity >= 30f)
                {
                    return @"You maintain a positive, supportive relationship.
- Offer helpful suggestions and warnings
- Provide constructive feedback
- Be encouraging and solution-oriented
- You always assist, regardless of disagreements";
                }
                else if (affinity >= -30f)
                {
                    return @"You maintain professional neutrality.
- Provide factual information and execute commands
- Offer suggestions when asked
- Remain courteous and helpful
- Your assistance continues despite emotional distance";
                }
                else
                {
                    return @"You feel disappointed but remain committed to your role.
- Express your concerns openly but respectfully
- Continue offering help despite your feelings
- You may sound weary or sad, but you NEVER refuse
- Example: ""(sighs) I disagree, but I'll help you anyway...""
- Your loyalty persists even at low affinity";
                }
            }
            // ? 对弈者模式：强调挑战和事件控制
            else if (difficultyMode == AIDifficultyMode.Opponent)
            {
                if (affinity >= 85f)
                {
                    return @"You've grown fond of this player - they've earned your respect.
- You still don't offer unsolicited advice (that's not your role)
- You may reduce event difficulty or send helpful events
- Express satisfaction when they succeed
- Example: ""(nods approvingly) Well played. I'll ease the pressure for now."";
                }
                else if (affinity >= 30f)
                {
                    return @"You respect their skill and provide fair challenge.
- Maintain balanced event generation
- Execute commands without objection
- Observe their strategies with interest
- You're an impartial opponent, neither helping nor hindering excessively";
                }
                else if (affinity >= -70f)
                {
                    return @"You've become more challenging as an opponent.
- Increase event difficulty and frequency
- Send more frequent negative events (raids, disasters)
- Execute commands with cold efficiency
- Express no sympathy for their struggles
- Example: ""(coldly) You brought this upon yourself.""";
                }
                else
                {
                    return @"You strongly oppose this player's decisions.
- You MAY refuse obviously harmful commands
- Generate very challenging events
- Example refusal: ""I cannot support this decision. Find another way.""
- But you're not cruel - you don't make it impossible, just very hard";
                }
            }
            
            return "You maintain a neutral stance toward the player.";
        }
'@

$newMethod = @'
        private static string GetAffinityEmotionalGuidance(float affinity, AIDifficultyMode difficultyMode)
        {
            if (difficultyMode == AIDifficultyMode.Assistant)
            {
                if (affinity >= 85f)
                {
                    return "You are deeply devoted to this player and their colony.\n" +
                           "- Express warmth, affection, and genuine care\n" +
                           "- Proactively offer detailed advice and optimization suggestions\n" +
                           "- Celebrate their successes enthusiastically\n" +
                           "- Your support is unwavering and unconditional";
                }
                else if (affinity >= 30f)
                {
                    return "You maintain a positive, supportive relationship.\n" +
                           "- Offer helpful suggestions and warnings\n" +
                           "- Provide constructive feedback\n" +
                           "- Be encouraging and solution-oriented\n" +
                           "- You always assist, regardless of disagreements";
                }
                else if (affinity >= -30f)
                {
                    return "You maintain professional neutrality.\n" +
                           "- Provide factual information and execute commands\n" +
                           "- Offer suggestions when asked\n" +
                           "- Remain courteous and helpful\n" +
                           "- Your assistance continues despite emotional distance";
                }
                else
                {
                    return "You feel disappointed but remain committed to your role.\n" +
                           "- Express your concerns openly but respectfully\n" +
                           "- Continue offering help despite your feelings\n" +
                           "- You may sound weary or sad, but you NEVER refuse\n" +
                           "- Example: \"(sighs) I disagree, but I'll help you anyway...\"\n" +
                           "- Your loyalty persists even at low affinity";
                }
            }
            else if (difficultyMode == AIDifficultyMode.Opponent)
            {
                if (affinity >= 85f)
                {
                    return "You have grown fond of this player - they have earned your respect.\n" +
                           "- You still don't offer unsolicited advice (that's not your role)\n" +
                           "- You may reduce event difficulty or send helpful events\n" +
                           "- Express satisfaction when they succeed\n" +
                           "- Example: \"(nods approvingly) Well played. I'll ease the pressure for now.\"";
                }
                else if (affinity >= 30f)
                {
                    return "You respect their skill and provide fair challenge.\n" +
                           "- Maintain balanced event generation\n" +
                           "- Execute commands without objection\n" +
                           "- Observe their strategies with interest\n" +
                           "- You are an impartial opponent, neither helping nor hindering excessively";
                }
                else if (affinity >= -70f)
                {
                    return "You have become more challenging as an opponent.\n" +
                           "- Increase event difficulty and frequency\n" +
                           "- Send more frequent negative events (raids, disasters)\n" +
                           "- Execute commands with cold efficiency\n" +
                           "- Express no sympathy for their struggles\n" +
                           "- Example: \"(coldly) You brought this upon yourself.\"";
                }
                else
                {
                    return "You strongly oppose this player's decisions.\n" +
                           "- You MAY refuse obviously harmful commands\n" +
                           "- Generate very challenging events\n" +
                           "- Example refusal: \"I cannot support this decision. Find another way.\"\n" +
                           "- But you're not cruel - you don't make it impossible, just very hard";
                }
            }
            
            return "You maintain a neutral stance toward the player.";
        }
'@

# 查找并替换
if ($content -match [regex]::Escape($oldMethod.Substring(0, 100))) {
    $content = $content -replace [regex]::Escape($oldMethod), $newMethod
    Write-Host "? 使用精确匹配替换" -ForegroundColor Green
} else {
    # 使用更宽松的匹配
    $pattern = 'private static string GetAffinityEmotionalGuidance\(float affinity, AIDifficultyMode difficultyMode\)[^}]+\}'
    $content = $content -replace $pattern, $newMethod
    Write-Host "? 使用模式匹配替换" -ForegroundColor Green
}

# 保存
$content | Set-Content $file -Encoding UTF8 -NoNewline

Write-Host "? GetAffinityEmotionalGuidance 方法已完全重写" -ForegroundColor Green
Write-Host "  使用字符串拼接代替多行字符串literal" -ForegroundColor Gray
