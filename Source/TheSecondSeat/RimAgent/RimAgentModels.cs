using System.Collections.Generic;

namespace TheSecondSeat.RimAgent
{
    /// <summary>
    /// ? v1.6.65: RimAgentModels - 数据模型定义
    /// </summary>
    public class ToolDefinition
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public Dictionary<string, ParameterDefinition> Parameters { get; set; }
        
        public ToolDefinition()
        {
            Parameters = new Dictionary<string, ParameterDefinition>();
        }
    }
    
    public class ParameterDefinition
    {
        public string Type { get; set; }
        public string Description { get; set; }
        public bool Required { get; set; }
        public object DefaultValue { get; set; }
    }
    
    public class AgentConfig
    {
        public string AgentId { get; set; }
        public string SystemPrompt { get; set; }
        public string ProviderName { get; set; }
        public float Temperature { get; set; } = 0.7f;
        public int MaxTokens { get; set; } = 500;
        public List<string> Tools { get; set; }
        
        public AgentConfig()
        {
            Tools = new List<string>();
        }
    }
}