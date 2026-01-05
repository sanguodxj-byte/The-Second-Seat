using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Verse;

namespace TheSecondSeat.RimAgent
{
    /// <summary>
    /// ? v1.6.65: RimAgentTools - 工具库管理器
    /// 功能：工具注册与管理、工具执行接口、参数验证、结果封装
    /// </summary>
    public static class RimAgentTools
    {
        private static readonly Dictionary<string, ITool> registeredTools = new Dictionary<string, ITool>();
        private static readonly object lockObj = new object();
        
        public static void RegisterTool(string name, ITool tool)
        {
            lock (lockObj)
            {
                registeredTools[name] = tool;
                Log.Message($"[RimAgentTools] Tool '{name}' registered");
            }
        }
        
        public static async Task<ToolResult> ExecuteAsync(string toolName, Dictionary<string, object> parameters)
        {
            try
            {
                if (!registeredTools.TryGetValue(toolName, out var tool))
                {
                    return new ToolResult { Success = false, Error = $"Tool '{toolName}' not found" };
                }
                
                return await tool.ExecuteAsync(parameters);
            }
            catch (Exception ex)
            {
                Log.Error($"[RimAgentTools] Error: {ex.Message}");
                return new ToolResult { Success = false, Error = ex.Message };
            }
        }
        
        public static List<string> GetRegisteredToolNames()
        {
            lock (lockObj) { return new List<string>(registeredTools.Keys); }
        }
        
        /// <summary>
        /// ⭐ v1.6.65: 获取指定工具实例
        /// </summary>
        public static ITool GetTool(string toolName)
        {
            lock (lockObj)
            {
                return registeredTools.TryGetValue(toolName, out var tool) ? tool : null;
            }
        }
        
        public static bool IsToolRegistered(string toolName)
        {
            lock (lockObj) { return registeredTools.ContainsKey(toolName); }
        }
        
        public static void ClearAllTools()
        {
            lock (lockObj) { registeredTools.Clear(); }
        }
    }
    
    public interface ITool
    {
        string Name { get; }
        string Description { get; }
        Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters);
    }
    
    public class ToolResult
    {
        public bool Success { get; set; }
        public object Data { get; set; }
        public string Error { get; set; }
        public DateTime ExecutedAt { get; set; }
        public ToolResult() { ExecutedAt = DateTime.Now; }

        public static ToolResult Failure(string error)
        {
            return new ToolResult { Success = false, Error = error };
        }

        public static ToolResult Successful(object data)
        {
            return new ToolResult { Success = true, Data = data };
        }
    }
}