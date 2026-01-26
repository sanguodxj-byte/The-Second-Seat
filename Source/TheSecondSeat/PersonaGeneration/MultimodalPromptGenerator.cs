using System.Collections.Generic;
using System.Text;
using Verse; // For potential logging or other utils if needed

namespace TheSecondSeat.PersonaGeneration
{
    /// <summary>
    /// Multimodal Prompt Generator
    /// 负责生成 Vision API 和 Text API 的提示词
    /// ⭐ v2.3.0: 重构为使用 Scriban 模板，支持多语言和热更新
    /// </summary>
    public static class MultimodalPromptGenerator
    {
        // ========== Vision Analysis Prompts (Detailed) ==========

        public static string GetVisionPrompt()
        {
            // 基础调用，无特质和补充
            return GetVisionPromptWithTraits(null, null);
        }

        public static string GetVisionPromptWithTraits(List<string> selectedTraits, string userSupplement)
        {
            var context = new Scriban.PromptContext
            {
                Analysis = new Scriban.AnalysisInfo
                {
                    SelectedTraits = selectedTraits ?? new List<string>(),
                    UserSupplement = userSupplement
                }
            };

            return Scriban.PromptRenderer.Render("Vision_Analysis_Detailed", context);
        }

        // ========== Brief Prompts (for Base64 API) ==========

        public static string GetBriefVisionPrompt()
        {
            // 简略版不需要额外上下文
            var context = new Scriban.PromptContext();
            return Scriban.PromptRenderer.Render("Vision_Analysis_Brief", context);
        }

        // ========== Text Analysis Prompts ==========

        public static string GetTextAnalysisPrompt(string text)
        {
            var context = new Scriban.PromptContext
            {
                Analysis = new Scriban.AnalysisInfo
                {
                    BiographyText = text
                }
            };

            return Scriban.PromptRenderer.Render("Text_Analysis_Biography", context);
        }
    }
}