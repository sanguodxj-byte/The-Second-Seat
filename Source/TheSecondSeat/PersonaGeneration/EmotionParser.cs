using System;
using System.Text.RegularExpressions;
using UnityEngine;
using Verse;

namespace TheSecondSeat.PersonaGeneration
{
    /// <summary>
    /// 情绪标签解析器
    /// 负责解析 LLM 返回的情绪字符串，支持强度标记
    /// </summary>
    public static class EmotionParser
    {
        /// <summary>
        /// 解析情绪字符串，返回类型和强度
        /// 支持格式: "Happy", "Happy:2", "Happy(2)", "Happy_2"
        /// </summary>
        public static (ExpressionType type, int intensity) Parse(string emotionStr)
        {
            if (string.IsNullOrEmpty(emotionStr))
                return (ExpressionType.Neutral, 0);

            string normalized = emotionStr.Trim();
            int intensity = 0;
            string typeStr = normalized;

            // 尝试提取强度
            // 匹配 :2, (2), _2, [2]
            var match = Regex.Match(normalized, @"[:_\(\[](\d+)[\)\]]?$");
            if (match.Success)
            {
                if (int.TryParse(match.Groups[1].Value, out int val))
                {
                    intensity = val;
                }
                // 移除强度部分，只保留类型字符串
                typeStr = normalized.Substring(0, match.Index).Trim();
            }

            // 标准化类型字符串
            typeStr = NormalizeEmotionType(typeStr);

            if (Enum.TryParse<ExpressionType>(typeStr, true, out var type))
            {
                return (type, intensity);
            }

            // 如果解析失败，尝试作为 Neutral 处理
            return (ExpressionType.Neutral, 0);
        }

        /// <summary>
        /// 标准化表情字符串 (处理大小写和别名)
        /// </summary>
        public static string NormalizeEmotionType(string emotion)
        {
            if (string.IsNullOrEmpty(emotion)) return "Neutral";
            
            // 首字母大写，其余小写
            string normalized = emotion.Trim().ToLower();
            
            // 处理常见别名映射 - 返回值必须匹配 ExpressionType 枚举
            switch (normalized)
            {
                // 开心相关
                case "happy":
                case "joy":
                case "smile":
                case "cheerful":
                case "delighted":
                case "laugh":
                case "laughing":
                    return "Happy";
                    
                // 悲伤相关
                case "sad":
                case "crying":
                case "sorrowful":
                case "grief":
                    return "Sad";
                    
                // 愤怒相关
                case "angry":
                case "mad":
                case "furious":
                case "rage":
                    return "Angry";
                    
                // 惊讶相关
                case "surprised":
                case "shocked":
                case "amazed":
                case "wow":
                    return "Surprised";
                    
                // 担忧相关
                case "worried":
                case "anxious":
                case "concerned":
                case "fear":
                case "afraid":
                    return "Worried";
                    
                // 失望相关
                case "disappointed":
                case "let down":
                    return "Disappointed";
                    
                // 烦躁相关
                case "annoyed":
                case "irritated":
                case "frustrated":
                    return "Annoyed";
                    
                // 得意相关
                case "smug":
                case "proud":
                case "satisfied":
                case "confident":
                    return "Smug";
                    
                // 沉思相关
                case "thoughtful":
                case "thinking":
                case "pondering":
                case "contemplative":
                case "vigilant":
                case "alert":
                case "focused":
                    return "Thoughtful";
                    
                // 调皮相关
                case "playful":
                case "mischievous":
                case "teasing":
                case "joking":
                    return "Playful";
                    
                // 害羞相关
                case "shy":
                case "bashful":
                case "embarrassed":
                case "blushing":
                case "flustered":
                    return "Shy";
                    
                // 疑惑相关
                case "confused":
                case "puzzled":
                case "bewildered":
                case "questioning":
                    return "Confused";

                // 中性
                case "neutral":
                case "calm":
                case "normal":
                    return "Neutral";
                    
                default:
                    // 尝试首字母大写返回（可能已经是有效的枚举名）
                    if (normalized.Length > 0)
                    {
                        string titleCase = char.ToUpper(normalized[0]) + normalized.Substring(1);
                        // 验证是否为有效的枚举值
                        if (Enum.TryParse<ExpressionType>(titleCase, true, out _))
                        {
                            return titleCase;
                        }
                    }
                    return "Neutral";
            }
        }
    }
}
