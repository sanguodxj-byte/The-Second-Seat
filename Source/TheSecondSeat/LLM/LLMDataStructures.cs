using System;

namespace TheSecondSeat.LLM
{
    /// <summary>
    /// Response structure from the LLM
    /// </summary>
    [Serializable]
    public class LLMResponse
    {
        public string thought { get; set; } = "";
        public string dialogue { get; set; } = "";
        public LLMCommand? command { get; set; }
        
        // ? 新增：表情包ID（可选）
        public string emoticon { get; set; } = "";
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
