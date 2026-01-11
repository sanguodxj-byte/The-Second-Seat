using System;
using System.Collections.Generic;

namespace TheSecondSeat.LLM
{
    /// <summary>
    /// Response structure from the LLM
    /// ⭐ v1.6.75: 优化情绪序列格式，减少 token 消耗
    /// </summary>
    [Serializable]
    public class LLMResponse
    {
        // ⭐ v1.6.85: 存储原始响应内容（用于 ReAct 循环解析）
        public string rawContent { get; set; } = "";

        public string thought { get; set; } = "";
        public string dialogue { get; set; } = "";
        
        // 表情字段（推荐 AI 提供）
        public string expression { get; set; } = "";
        
        // ✅ v1.6.66: 情绪标签 (单情绪模式，向后兼容)
        public string emotion { get; set; } = "neutral";
        
        // ⭐ v1.6.75: 紧凑情绪序列（推荐，节省 token）
        // 格式：使用 | 分隔多个情绪标签
        // 示例："happy|worried|angry"
        public string? emotions { get; set; }
        
        // ⭐ v1.6.75: 详细情绪序列（可选，完整模式）
        public List<EmotionSegment>? emotionSequence { get; set; }
        
        // ✅ v1.6.66: 口型编码 (Closed, Small, Medium, Large, Smile, OShape)
        public string viseme { get; set; } = "Closed";
        
        // 表情符号ID（可选）
        public string emoticon { get; set; } = "";
        
        public LLMCommand? command { get; set; }
    }

    /// <summary>
    /// ⭐ v1.6.75: 情绪片段（用于详细情绪序列）
    /// </summary>
    [Serializable]
    public class EmotionSegment
    {
        /// <summary>
        /// 对应的文本片段
        /// </summary>
        public string text { get; set; } = "";
        
        /// <summary>
        /// 情绪标签（happy, sad, angry, surprised, worried, confused, neutral）
        /// 或缩写：h, s, a, su, w, c, n
        /// </summary>
        public string emotion { get; set; } = "neutral";
        
        /// <summary>
        /// 估算播放时长（秒，可选，如果为 0 则自动估算）
        /// </summary>
        public float estimatedDuration { get; set; } = 0f;
    }

    [Serializable]
    public class LLMCommand
    {
        public string action { get; set; } = "";
        public string? target { get; set; }
        public object? parameters { get; set; }
    }

    /// <summary>
    /// Request structure for OpenAI API
    /// </summary>
    [Serializable]
    public class OpenAIRequest
    {
        public string model { get; set; } = "gpt-4";
        public Message[] messages { get; set; } = Array.Empty<Message>();
        public float temperature { get; set; } = 0.7f;
        public int max_tokens { get; set; } = 500;
    }

    [Serializable]
    public class Message
    {
        public string role { get; set; } = "";
        public string content { get; set; } = "";
    }

    [Serializable]
    public class OpenAIResponse
    {
        public Choice[] choices { get; set; } = Array.Empty<Choice>();
    }

    [Serializable]
    public class Choice
    {
        public Message? message { get; set; }
    }
}
