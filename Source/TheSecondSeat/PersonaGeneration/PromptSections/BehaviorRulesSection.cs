using System.Text;
using TheSecondSeat.Storyteller;

namespace TheSecondSeat.PersonaGeneration.PromptSections
{
    /// <summary>
    /// ? v1.6.76: ��Ϊ���򲿷�������
    /// �������� System Prompt ����Ϊ�����������
    /// </summary>
    public static class BehaviorRulesSection
    {
        /// <summary>
        /// Generates behavior rules section using modular prompts
        /// </summary>
        public static string Generate(PersonaAnalysisResult analysis, StorytellerAgent agent, AIDifficultyMode difficultyMode)
        {
            var sb = new StringBuilder();
            
            sb.AppendLine("=== YOUR BEHAVIOR RULES ===");
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
