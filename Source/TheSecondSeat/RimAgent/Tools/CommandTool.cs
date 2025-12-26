using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Verse;
using TheSecondSeat.Commands;
using TheSecondSeat.LLM;

namespace TheSecondSeat.RimAgent.Tools
{
    /// <summary>
    /// 命令工具 - 执行游戏命令 (修复版)
    /// </summary>
    public class CommandTool : ITool
    {
        public string Name => "command";
        public string Description => "执行游戏命令（如批量收获、批量装备等）";
        
        public async Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
        {
            Log.Message(string.Format("[CommandTool] ExecuteAsync called with parameters: {0}", string.Join(", ", parameters.Keys)));
            try
            {
                if (!parameters.TryGetValue("action", out var actionObj))
                {
                    return new ToolResult { Success = false, Error = "Missing parameter: action" };
                }
                
                string action = actionObj.ToString();
                
                // 创建 LLMCommand 对象
                var llmCommand = new LLMCommand
                {
                    action = action,
                    target = parameters.ContainsKey("target") ? parameters["target"]?.ToString() : null,
                    parameters = parameters.ContainsKey("params") ? parameters["params"] : null
                };
                
                // ✅ 修复：CommandParser.ParseAndExecute 返回 CommandResult
                var result = CommandParser.ParseAndExecute(llmCommand);
                
                if (result.Success)
                {
                    return new ToolResult { Success = true, Data = result.Message };
                }
                else
                {
                    return new ToolResult { Success = false, Error = result.Message };
                }
            }
            catch (Exception ex)
            {
                return new ToolResult { Success = false, Error = ex.Message };
            }
        }
    }
}