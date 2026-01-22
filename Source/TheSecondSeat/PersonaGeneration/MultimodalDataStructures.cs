using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TheSecondSeat.LLM;

namespace TheSecondSeat.PersonaGeneration
{
    /// <summary>
    /// Vision 分析结果
    /// 📌 v1.6.62: 添加 personalityTags 字段
    /// </summary>
    public class VisionAnalysisResult
    {
        public List<ColorInfo> dominantColors { get; set; } = new List<ColorInfo>();
        public List<string> visualElements { get; set; } = new List<string>();
        public string characterDescription { get; set; } = "";
        public string mood { get; set; } = "";
        public string suggestedPersonality { get; set; } = "";
        public List<string> styleKeywords { get; set; } = new List<string>();
        
        /// <summary>
        /// 📌 v1.6.62: 个性标签（如：善良、坚强、爱撒娇、病娇等）
        /// </summary>
        public List<string> personalityTags { get; set; } = new List<string>();

        /// <summary>
        /// 📌 互动短语库
        /// </summary>
        public List<PhraseSet> phraseLibrary { get; set; } = new List<PhraseSet>();

        /// <summary>
        /// 📌 交互区域坐标（由多模态分析引擎提供）
        /// 使用归一化坐标 (0.0-1.0)，原点在左上角
        /// </summary>
        public VisionInteractionZones interactionZones { get; set; } = null;

        /// <summary>
        /// 获取主色调（占比最高的颜色）
        /// </summary>
        public Color GetPrimaryColor()
        {
            if (dominantColors == null || dominantColors.Count == 0)
                return Color.white;

            var primary = dominantColors.OrderByDescending(c => c.percentage).First();
            return HexToColor(primary.hex);
        }

        /// <summary>
        /// 获取重音色（占比第二的颜色）
        /// </summary>
        public Color GetAccentColor()
        {
            if (dominantColors == null || dominantColors.Count < 2)
                return Color.gray;

            var accent = dominantColors.OrderByDescending(c => c.percentage).Skip(1).First();
            return HexToColor(accent.hex);
        }

        private Color HexToColor(string hex)
        {
            hex = hex.Replace("#", "");

            if (hex.Length != 6)
                return Color.white;

            try
            {
                byte r = Convert.ToByte(hex.Substring(0, 2), 16);
                byte g = Convert.ToByte(hex.Substring(2, 2), 16);
                byte b = Convert.ToByte(hex.Substring(4, 2), 16);

                return new Color(r / 255f, g / 255f, b / 255f);
            }
            catch
            {
                return Color.white;
            }
        }
    }

    public class ColorInfo
    {
        public string hex { get; set; } = "";
        public int percentage { get; set; } = 0;
        public string name { get; set; } = "";
    }

    /// <summary>
    /// 文本深度分析结果
    /// </summary>
    public class TextAnalysisResult
    {
        public List<string> personality_traits { get; set; } = new List<string>();
        public DialogueStyleAnalysis dialogue_style { get; set; } = new DialogueStyleAnalysis();
        public List<string> tone_tags { get; set; } = new List<string>();
        public EventPreferencesAnalysis event_preferences { get; set; } = new EventPreferencesAnalysis();
        public List<string> forbidden_words { get; set; } = new List<string>();
    }

    public class DialogueStyleAnalysis
    {
        public float formality { get; set; } = 0.5f;
        public float emotional_expression { get; set; } = 0.5f;
        public float verbosity { get; set; } = 0.5f;
        public float humor { get; set; } = 0.3f;
        public float sarcasm { get; set; } = 0.2f;
    }

    public class EventPreferencesAnalysis
    {
        public float positive_bias { get; set; } = 0f;
        public float negative_bias { get; set; } = 0f;
        public float chaos_level { get; set; } = 0f;
        public float intervention_frequency { get; set; } = 0.5f;
    }

    /// <summary>
    /// 📌 交互区域（由多模态分析返回）
    /// 坐标系：左上角为原点 (0,0)，右下角为 (1,1)
    /// </summary>
    public class VisionInteractionZones
    {
        public VisionZoneRect head { get; set; } = null;
        public VisionZoneRect body { get; set; } = null;
    }

    /// <summary>
    /// 📌 区域矩形（归一化坐标 0.0-1.0）
    /// </summary>
    public class VisionZoneRect
    {
        public float xMin { get; set; } = 0f;
        public float yMin { get; set; } = 0f;
        public float xMax { get; set; } = 1f;
        public float yMax { get; set; } = 1f;
    }
}