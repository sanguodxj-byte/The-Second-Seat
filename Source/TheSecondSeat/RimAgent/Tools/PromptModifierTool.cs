using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Verse;
using RimWorld;
using UnityEngine;
using TheSecondSeat.PersonaGeneration;

namespace TheSecondSeat.RimAgent.Tools
{
    /// <summary>
    /// 提示词修改工具
    /// 允许 Agent 读取和修改 Config/TheSecondSeat/Prompts 下的文件
    /// 修改操作需要玩家确认
    /// </summary>
    public class PromptModifierTool : ITool
    {
        public string Name => "prompt_modifier";
        public string Description => "Manage and modify system prompts in 'Config/TheSecondSeat/Prompts'. " +
                                     "Actions: 'list', 'read', 'modify'. " +
                                     "Args: 'action', 'filename' (for read/modify), 'content' (for modify). " +
                                     "Modification requires player approval.";

        private string PromptsDirectory => Path.Combine(GenFilePaths.ConfigFolderPath, "TheSecondSeat", "Prompts");

        public async Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
        {
            if (!parameters.TryGetValue("action", out object actionObj) || !(actionObj is string action))
            {
                return ToolResult.Failure("Missing 'action' argument (list, read, modify).");
            }

            try
            {
                EnsureDirectoryExists();

                switch (action.ToLower())
                {
                    case "list":
                        return ListPrompts();
                    case "read":
                        return ReadPrompt(parameters);
                    case "modify":
                        return await ModifyPromptAsync(parameters);
                    default:
                        return ToolResult.Failure($"Unknown action: {action}");
                }
            }
            catch (Exception ex)
            {
                return ToolResult.Failure($"Error: {ex.Message}");
            }
        }

        private void EnsureDirectoryExists()
        {
            if (!Directory.Exists(PromptsDirectory))
            {
                Directory.CreateDirectory(PromptsDirectory);
            }
        }

        private ToolResult ListPrompts()
        {
            var files = Directory.GetFiles(PromptsDirectory, "*.txt")
                                 .Select(Path.GetFileName)
                                 .ToList();
            
            if (files.Count == 0)
            {
                return ToolResult.Successful("No prompt files found in directory.");
            }

            return ToolResult.Successful($"Found {files.Count} files:\n" + string.Join("\n", files));
        }

        private ToolResult ReadPrompt(Dictionary<string, object> parameters)
        {
            if (!parameters.TryGetValue("filename", out object fileObj) || !(fileObj is string filename))
                return ToolResult.Failure("Missing 'filename' argument.");

            string filePath = Path.Combine(PromptsDirectory, filename);
            
            if (!IsPathSafe(filePath))
                return ToolResult.Failure("Access denied: Cannot access files outside Prompts directory.");

            if (!File.Exists(filePath))
                return ToolResult.Failure($"File not found: {filename}");

            string content = File.ReadAllText(filePath);
            return ToolResult.Successful(content);
        }

        private Task<ToolResult> ModifyPromptAsync(Dictionary<string, object> parameters)
        {
            if (!parameters.TryGetValue("filename", out object fileObj) || !(fileObj is string filename))
                return Task.FromResult(ToolResult.Failure("Missing 'filename' argument."));
            
            if (!parameters.TryGetValue("content", out object contentObj) || !(contentObj is string newContent))
                return Task.FromResult(ToolResult.Failure("Missing 'content' argument."));

            string filePath = Path.Combine(PromptsDirectory, filename);
            
            if (!IsPathSafe(filePath))
                return Task.FromResult(ToolResult.Failure("Access denied: Cannot access files outside Prompts directory."));

            // 创建备份
            if (File.Exists(filePath))
            {
                try
                {
                    File.Copy(filePath, filePath + ".bak", true);
                }
                catch (Exception ex)
                {
                    return Task.FromResult(ToolResult.Failure($"Failed to create backup: {ex.Message}"));
                }
            }

            File.WriteAllText(filePath, newContent);
            
            // 清除 PromptLoader 缓存，确保下次读取时加载新内容
            PromptLoader.ClearCache();
            
            return Task.FromResult(ToolResult.Successful($"File '{filename}' updated successfully. Backup created. Cache cleared."));
        }

        private bool IsPathSafe(string filePath)
        {
            string fullPath = Path.GetFullPath(filePath);
            string rootPath = Path.GetFullPath(PromptsDirectory);
            return fullPath.StartsWith(rootPath, StringComparison.OrdinalIgnoreCase);
        }
    }
}
