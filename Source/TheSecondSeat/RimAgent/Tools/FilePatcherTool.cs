using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Verse;
using TheSecondSeat.Core;

namespace TheSecondSeat.RimAgent.Tools
{
    /// <summary>
    /// 文件修补工具 (手术刀)
    /// 用于查找并替换配置文件中的文本，修复拼写错误或格式问题
    /// ? v1.7.0: 增强安全性，支持备份，支持 Mod 目录下的文件修复
    /// </summary>
    public class FilePatcherTool : ITool
    {
        public string Name => "patch_file";
        public string Description => "Safely patches a text file by replacing a string. Creates a .bak backup before modifying. " +
                                     "Args: 'path' (absolute or relative path), 'original_text', 'new_text'. " +
                                     "Restricted to Mod Configs and The Second Seat mod directories.";

        public async Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
        {
            await Task.CompletedTask;

            try
            {
                // 1. 参数获取
                string path = "";
                if (parameters.TryGetValue("path", out object pathObj)) path = pathObj as string;
                else if (parameters.TryGetValue("file", out object fileObj)) path = fileObj as string; // 兼容旧参数

                if (string.IsNullOrEmpty(path))
                    return ToolResult.Failure("Missing 'path' argument.");

                if (!parameters.TryGetValue("original_text", out object origObj) || !(origObj is string oldText))
                    return ToolResult.Failure("Missing 'original_text' argument.");
                
                if (!parameters.TryGetValue("new_text", out object newObj) || !(newObj is string newText))
                    return ToolResult.Failure("Missing 'new_text' argument.");

                // 2. 路径解析与安全检查
                string fullPath = Path.GetFullPath(path);
                bool isAllowed = false;

                // 允许路径 A: 用户配置目录 (Config)
                string configDir = Path.GetFullPath(GenFilePaths.ConfigFolderPath);
                if (fullPath.StartsWith(configDir, StringComparison.OrdinalIgnoreCase))
                {
                    isAllowed = true;
                }

                // 允许路径 B: 本 Mod 及其子 Mod 的安装目录, 但仅限于 .txt 文件
                if (!isAllowed)
                {
                    foreach (var mod in LoadedModManager.RunningModsListForReading)
                    {
                        if (mod.PackageId.ToLower().Contains("thesecondseat"))
                        {
                            string modDir = Path.GetFullPath(mod.RootDir);
                            if (fullPath.StartsWith(modDir, StringComparison.OrdinalIgnoreCase))
                            {
                                // ⭐ 安全增强：只允许修改 .txt 文件
                                if (Path.GetExtension(fullPath).Equals(".txt", StringComparison.OrdinalIgnoreCase))
                                {
                                    isAllowed = true;
                                    break;
                                }
                            }
                        }
                    }
                }

                if (!isAllowed)
                {
                    return ToolResult.Failure($"Security Violation: Access denied to '{path}'. " +
                                              "You can only modify files in the Mod Config folder or '.txt' files within 'The Second Seat' mod directories.");
                }

                if (!File.Exists(fullPath))
                {
                    return ToolResult.Failure($"File not found: {fullPath}");
                }

                // 3. 读取内容
                string content = File.ReadAllText(fullPath);
                
                // 4. 检查原始内容是否存在
                if (!content.Contains(oldText)) 
                {
                    return ToolResult.Failure($"Original text not found in file. Please check spelling and whitespace exactly.\nTarget: {path}");
                }

                // 5. 创建备份 (必须!)
                string backupPath = fullPath + ".bak";
                
                // ⭐ 修复: 防止覆盖已有备份，避免回滚失效
                if (File.Exists(backupPath))
                {
                    // 如果已存在备份，使用时间戳创建新备份
                    string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                    backupPath = fullPath + $".{timestamp}.bak";
                }

                try
                {
                    File.Copy(fullPath, backupPath, overwrite: false); // 不允许覆盖
                }
                catch (Exception ex)
                {
                    return ToolResult.Failure($"Failed to create backup at {backupPath}: {ex.Message}. Operation aborted for safety.");
                }

                // 6. 执行替换并写入
                string newContent = content.Replace(oldText, newText);
                File.WriteAllText(fullPath, newContent);

                return new ToolResult 
                { 
                    Success = true, 
                    Data = $"Success: Patch applied to '{Path.GetFileName(path)}'.\n" +
                           $"Backup created at: {Path.GetFileName(backupPath)}\n" +
                           $"Original: '{oldText}'\n" +
                           $"Replaced with: '{newText}'\n\n" +
                           "Note: XML/Def changes require a game restart to take effect." 
                };
            }
            catch (Exception ex)
            {
                return ToolResult.Failure($"System Error: {ex.Message}");
            }
        }
    }
}