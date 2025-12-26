using System;
using System.Collections.Generic;
using TheSecondSeat.PersonaGeneration;

namespace TheSecondSeat.TTS
{
    /// <summary>
    /// 表情/情绪到 Azure TTS 情感风格的映射器
    /// 根据 ExpressionType 或 emotion 字符串自动推断 SSML 情感参数
    /// ⭐ v1.6.75: 新增 emotion 字符串映射，支持 LLM 输出的情绪标签
    /// </summary>
    public static class EmotionMapper
    {
        /// <summary>
        /// 表情到情感风格的映射表
        /// </summary>
        private static readonly Dictionary<ExpressionType, EmotionStyle> ExpressionToEmotion = new Dictionary<ExpressionType, EmotionStyle>
        {
            // ? 积极情绪
            { ExpressionType.Happy,        new EmotionStyle("cheerful", 1.5f) },      // 开心
            { ExpressionType.Smug,         new EmotionStyle("cheerful", 1.2f) },      // 得意
            { ExpressionType.Playful,      new EmotionStyle("cheerful", 1.3f) },      // 调皮
            
            // ? 消极情绪
            { ExpressionType.Sad,          new EmotionStyle("sad", 1.3f) },           // 悲伤
            { ExpressionType.Disappointed, new EmotionStyle("sad", 1.0f) },           // 失望
            { ExpressionType.Worried,      new EmotionStyle("sad", 0.8f) },           // 担忧
            
            // ? 愤怒情绪
            { ExpressionType.Angry,        new EmotionStyle("angry", 1.5f) },         // 愤怒
            { ExpressionType.Annoyed,      new EmotionStyle("angry", 1.0f) },         // 烦躁
            
            // ? 惊讶情绪
            { ExpressionType.Surprised,    new EmotionStyle("excited", 1.4f) },       // 惊讶
            
            // ? 沉思/中性
            { ExpressionType.Thoughtful,   new EmotionStyle("chat", 0.9f) },          // 沉思
            { ExpressionType.Neutral,      new EmotionStyle("chat", 1.0f) },          // 中性
            
            // ? 新增：害羞（温柔风格，低强度）
            { ExpressionType.Shy,          new EmotionStyle("gentle", 0.8f) },        // 害羞
        };

        /// <summary>
        /// ⭐ v1.6.75: 情绪字符串到 Azure TTS 风格的映射表
        /// 支持 LLM 输出的 emotion 标签（happy, sad, angry, neutral, surprised, confused 等）
        /// </summary>
        private static readonly Dictionary<string, EmotionStyle> EmotionStringToStyle = new Dictionary<string, EmotionStyle>(StringComparer.OrdinalIgnoreCase)
        {
            // ? LLM 标准情绪标签
            { "happy",       new EmotionStyle("cheerful", 1.5f) },      // 开心
            { "joy",         new EmotionStyle("cheerful", 1.6f) },      // 喜悦
            { "excited",     new EmotionStyle("excited", 1.5f) },       // 兴奋
            
            { "sad",         new EmotionStyle("sad", 1.3f) },           // 悲伤
            { "disappointed", new EmotionStyle("sad", 1.0f) },          // 失望
            { "crying",      new EmotionStyle("sad", 1.5f) },           // 哭泣
            
            { "angry",       new EmotionStyle("angry", 1.5f) },         // 愤怒
            { "frustrated",  new EmotionStyle("angry", 1.2f) },         // 沮丧
            { "annoyed",     new EmotionStyle("angry", 1.0f) },         // 烦躁
            
            { "surprised",   new EmotionStyle("excited", 1.4f) },       // 惊讶
            { "shocked",     new EmotionStyle("excited", 1.6f) },       // 震惊
            
            { "confused",    new EmotionStyle("chat", 0.9f) },          // 困惑
            { "thinking",    new EmotionStyle("chat", 0.9f) },          // 思考
            { "neutral",     new EmotionStyle("chat", 1.0f) },          // 中性
            { "calm",        new EmotionStyle("calm", 1.0f) },          // 平静
            
            { "love",        new EmotionStyle("gentle", 1.3f) },        // 爱
            { "affection",   new EmotionStyle("gentle", 1.2f) },        // 亲昵
            { "embarrassed", new EmotionStyle("gentle", 0.8f) },        // 尴尬
            { "shy",         new EmotionStyle("gentle", 0.8f) },        // 害羞
            
            { "tired",       new EmotionStyle("chat", 0.7f) },          // 疲惫
            { "sleepy",      new EmotionStyle("chat", 0.6f) },          // 困倦
            { "bored",       new EmotionStyle("chat", 0.8f) },          // 无聊
            
            { "smug",        new EmotionStyle("cheerful", 1.2f) },      // 得意
            { "proud",       new EmotionStyle("cheerful", 1.3f) },      // 自豪
        };

        /// <summary>
        /// 根据表情类型获取情感风格
        /// </summary>
        public static EmotionStyle GetEmotionStyle(ExpressionType expression)
        {
            if (ExpressionToEmotion.TryGetValue(expression, out var style))
            {
                return style;
            }

            // 默认：闲聊风格
            return new EmotionStyle("chat", 1.0f);
        }

        /// <summary>
        /// ⭐ v1.6.75: 根据情绪字符串获取情感风格
        /// 支持 LLM 输出的 emotion 标签
        /// </summary>
        /// <param name="emotionString">情绪标签（如 "happy", "sad", "angry" 等）</param>
        /// <returns>对应的 Azure TTS 情感风格</returns>
        public static EmotionStyle GetEmotionStyle(string emotionString)
        {
            if (string.IsNullOrEmpty(emotionString))
            {
                return new EmotionStyle("chat", 1.0f);
            }

            if (EmotionStringToStyle.TryGetValue(emotionString.Trim(), out var style))
            {
                return style;
            }

            // 默认：闲聊风格
            return new EmotionStyle("chat", 1.0f);
        }

        /// <summary>
        /// 根据当前表情获取情感风格（从表情系统）
        /// </summary>
        public static EmotionStyle GetCurrentEmotionStyle(string personaDefName)
        {
            try
            {
                var expressionState = ExpressionSystem.GetExpressionState(personaDefName);
                return GetEmotionStyle(expressionState.CurrentExpression);
            }
            catch
            {
                // 获取失败，返回默认
                return new EmotionStyle("chat", 1.0f);
            }
        }
    }

    /// <summary>
    /// 情感风格数据结构
    /// </summary>
    public class EmotionStyle
    {
        /// <summary>
        /// Azure TTS 情感风格名称
        /// </summary>
        public string StyleName { get; }

        /// <summary>
        /// 情感强度（0.01 - 2.0）
        /// </summary>
        public float StyleDegree { get; }

        public EmotionStyle(string styleName, float styleDegree)
        {
            StyleName = styleName;
            StyleDegree = UnityEngine.Mathf.Clamp(styleDegree, 0.01f, 2.0f);
        }
    }
}
