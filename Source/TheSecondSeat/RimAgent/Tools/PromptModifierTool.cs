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
    ///
    /// ⭐ 写入路径优先级说明：
    /// PromptLoader 的读取优先级：
    /// 1. Config/TheSecondSeat/Prompts/{Language}/{file} (用户覆盖 - 语言特定) ← 我们写入到这里
    /// 2. Config/TheSecondSeat/Prompts/{file} (用户覆盖 - 全局)
    /// 3. Mod/Languages/{Language}/Prompts/{file} (Mod 默认)
    /// 4. Mod/Languages/English/Prompts/{file} (Mod 回退)
    /// </summary>
    public class PromptModifierTool : ITool
    {
        public string Name => "prompt_modifier";
        public string Description => "Manage and modify system prompts in 'Config/TheSecondSeat/Prompts'. " +
                                     "Actions: 'list', 'read', 'modify'. " +
                                     "Args: 'action', 'filename' (for read/modify), 'content' (for modify). " +
                                     "Modification requires player approval. " +
                                     "Files are written to language-specific folder for highest priority.";

        private string PromptsDirectory => Path.Combine(GenFilePaths.ConfigFolderPath, "TheSecondSeat", "Prompts");
        
        /// <summary>
        /// 语言特定的提示词目录 - 写入到这里确保最高优先级
        /// </summary>
        private string LanguageSpecificPromptsDirectory =>
            Path.Combine(GenFilePaths.ConfigFolderPath, "TheSecondSeat", "Prompts", LanguageDatabase.activeLanguage.folderName);

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
            
            // 同时确保语言特定目录存在
            if (!Directory.Exists(LanguageSpecificPromptsDirectory))
            {
                Directory.CreateDirectory(LanguageSpecificPromptsDirectory);
            }
        }

        private ToolResult ListPrompts()
        {
            var allFiles = new HashSet<string>();
            
            // 收集全局目录的文件
            if (Directory.Exists(PromptsDirectory))
            {
                foreach (var file in Directory.GetFiles(PromptsDirectory, "*.txt"))
                {
                    allFiles.Add(Path.GetFileName(file));
                }
            }
            
            // 收集语言特定目录的文件（这些具有更高优先级）
            if (Directory.Exists(LanguageSpecificPromptsDirectory))
            {
                foreach (var file in Directory.GetFiles(LanguageSpecificPromptsDirectory, "*.txt"))
                {
                    allFiles.Add(Path.GetFileName(file) + " [" + LanguageDatabase.activeLanguage.folderName + "]");
                }
            }
            
            if (allFiles.Count == 0)
            {
                return ToolResult.Successful("No prompt files found in directory.");
            }

            return ToolResult.Successful($"Found {allFiles.Count} files (language-specific files have highest priority):\n" + string.Join("\n", allFiles.OrderBy(f => f)));
        }

        private ToolResult ReadPrompt(Dictionary<string, object> parameters)
        {
            if (!parameters.TryGetValue("filename", out object fileObj) || !(fileObj is string filename))
                return ToolResult.Failure("Missing 'filename' argument.");

            // 使用 PromptLoader 读取，它会自动处理优先级
            // 这样读取和写入使用相同的优先级逻辑
            string promptName = filename.EndsWith(".txt") ? filename.Substring(0, filename.Length - 4) : filename;
            string content = PromptLoader.Load(promptName);
            
            if (content.StartsWith("[Error:"))
            {
                return ToolResult.Failure($"File not found: {filename}");
            }
            
            return ToolResult.Successful(content);
        }

        private Task<ToolResult> ModifyPromptAsync(Dictionary<string, object> parameters)
        {
            if (!parameters.TryGetValue("filename", out object fileObj) || !(fileObj is string filename))
                return Task.FromResult(ToolResult.Failure("Missing 'filename' argument."));
            
            if (!parameters.TryGetValue("content", out object contentObj) || !(contentObj is string newContent))
                return Task.FromResult(ToolResult.Failure("Missing 'content' argument."));

            // ⭐ 写入到语言特定目录，确保最高优先级
            string filePath = Path.Combine(LanguageSpecificPromptsDirectory, filename);
            
            if (!IsPathSafe(filePath))
                return Task.FromResult(ToolResult.Failure("Access denied: Cannot access files outside Prompts directory."));

            // 确保语言特定目录存在
            if (!Directory.Exists(LanguageSpecificPromptsDirectory))
            {
                Directory.CreateDirectory(LanguageSpecificPromptsDirectory);
            }

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
            
            string langFolder = LanguageDatabase.activeLanguage.folderName;
            return Task.FromResult(ToolResult.Successful(
                $"File '{filename}' written to '{langFolder}' folder (highest priority). Backup created. Cache cleared."));
        }

        private bool IsPathSafe(string filePath)
        {
            string fullPath = Path.GetFullPath(filePath);
            string rootPath = Path.GetFullPath(PromptsDirectory);
            // 允许访问 PromptsDirectory 及其子目录（包括语言特定目录）
            return fullPath.StartsWith(rootPath, StringComparison.OrdinalIgnoreCase);
        }
    }
}
