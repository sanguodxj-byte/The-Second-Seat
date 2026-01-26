using System.Text;
using TheSecondSeat.Storyteller;
using Verse;

namespace TheSecondSeat.PersonaGeneration.PromptSections
{
    /// <summary>
    /// ⭐ v1.6.76: 行为规则部分生成器
    /// 负责生成 System Prompt 的行为规则部分
    /// </summary>
    public static class BehaviorRulesSection
    {
        // 判断是否使用中文
        private static bool IsChinese => LanguageDatabase.activeLanguage?.folderName?.Contains("Chinese") == true;

        /// <summary>
        /// Generates behavior rules section using modular prompts
        /// </summary>
        public static string Generate(PersonaAnalysisResult analysis, StorytellerAgent agent, AIDifficultyMode difficultyMode)
        {
            var sb = new StringBuilder();
            
            sb.AppendLine(IsChinese ? "=== 行为规则 ===" : "=== YOUR BEHAVIOR RULES ===");
            sb.AppendLine();
            
            if (difficultyMode == AIDifficultyMode.Assistant)
            {
                sb.AppendLine(PromptLoader.Load("BehaviorRules_Assistant"));
            }
            else if (difficultyMode == AIDifficultyMode.Opponent)
            {
                sb.AppendLine(PromptLoader.Load("BehaviorRules_Opponent"));
            }
            else if (difficultyMode == AIDifficultyMode.Engineer)
            {
                sb.AppendLine(PromptLoader.Load("BehaviorRules_Engineer"));
            }
            
            sb.AppendLine();
            sb.AppendLine(PromptLoader.Load("BehaviorRules_Universal"));
            
            return sb.ToString();
        }
    }
}
