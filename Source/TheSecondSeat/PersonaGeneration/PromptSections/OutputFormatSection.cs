using System.Text;
using TheSecondSeat.Storyteller;
using Verse;

namespace TheSecondSeat.PersonaGeneration.PromptSections
{
    /// <summary>
    /// ⭐ v1.6.76: 输出格式部分生成器
    /// ⭐ v1.6.84: 修复双语问题 - 移除所有硬编码英文，使用语言文件
    /// ⭐ v3.1.1: 支持按需加载工具列表
    /// 负责生成 System Prompt 的输出格式部分
    /// </summary>
    public static class OutputFormatSection
    {
        // 判断是否使用中文
        private static bool IsChinese => LanguageDatabase.activeLanguage?.folderName?.Contains("Chinese") == true;
        
        /// <summary>
        /// 生成输出格式部分
        /// </summary>
        /// <param name="difficultyMode">难度模式</param>
        /// <param name="includeToolBox">是否包含工具列表（命令列表和示例）</param>
        public static string Generate(AIDifficultyMode difficultyMode = AIDifficultyMode.Assistant, bool includeToolBox = true)
        {
            var sb = new StringBuilder();
            
            // ⭐ v1.6.84: 所有标题本地化
            sb.AppendLine(IsChinese ? "=== 输出格式 ===" : "=== OUTPUT FORMAT ===");
            sb.AppendLine();
            
            // 1. Structure (从语言文件加载) - 总是加载，定义基本响应格式
            sb.AppendLine(PromptLoader.Load("OutputFormat_Structure"));
            sb.AppendLine();
            
            // 2. DM Tool Usage Guidelines (从语言文件加载)
            string usageFileName = difficultyMode switch
            {
                AIDifficultyMode.Assistant => "OutputFormat_Usage_Assistant",
                AIDifficultyMode.Engineer => "OutputFormat_Usage_Engineer",
                _ => "OutputFormat_Usage_Opponent"
            };
            sb.AppendLine(PromptLoader.Load(usageFileName));
            sb.AppendLine();
            
            // 3. Field Descriptions (标题本地化) - 总是加载
            sb.AppendLine(IsChinese ? "**字段说明：**" : "**FIELD DESCRIPTIONS:**");
            sb.AppendLine();
            sb.AppendLine(PromptLoader.Load("OutputFormat_Fields"));
            sb.AppendLine();
            
            // ⭐ v3.1.1: 仅在需要工具时加载命令列表和示例
            if (includeToolBox)
            {
                // 4. Available Commands (标题本地化)
                sb.AppendLine(IsChinese ? "**可用命令：**" : "**AVAILABLE COMMANDS:**");
                sb.AppendLine(PromptLoader.Load("OutputFormat_Commands_List"));
                
                // ⭐ Add Fate Dice Tool
                if (IsChinese)
                {
                    sb.AppendLine("- RollDice(difficulty): 投掷 D20 骰子进行检定（包含好感度修正）。用于结果不确定的高风险行动。默认难度 10。");
                }
                else
                {
                    sb.AppendLine("- RollDice(difficulty): Roll a D20 with affinity modifier. Use for uncertain or high-stakes actions. Default difficulty 10.");
                }
                sb.AppendLine();
                
                // 5. Examples (标题本地化)
                sb.AppendLine(IsChinese ? "**响应示例：**" : "**EXAMPLE RESPONSES:**");
                sb.AppendLine();
                sb.AppendLine(PromptLoader.Load("OutputFormat_Examples"));
            }
            else
            {
                // ⭐ v3.1.1: 聊天模式下的简化提示
                sb.AppendLine(IsChinese
                    ? "**当前为对话模式**：专注于交流，无需执行游戏命令。如需执行操作，请明确说明。"
                    : "**Conversation Mode**: Focus on dialogue. No game commands needed. Specify if action is required.");
            }

            return sb.ToString();
        }
    }
}
