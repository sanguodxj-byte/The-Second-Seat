using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Verse;

namespace TheSecondSeat.Emoticons
{
    /// <summary>
    /// 表情包数据
    /// </summary>
    public class EmoticonData
    {
        public string id;              // 表情包ID（文件名不含扩展名）
        public string displayName;     // 显示名称
        public string filePath;        // 完整文件路径
        public Texture2D texture;      // 加载的纹理
        public List<string> tags;      // 情感标签（happy, sad, angry, surprised等）
        public string description;     // 描述（可选）

        public EmoticonData(string id, string filePath)
        {
            this.id = id;
            this.filePath = filePath;
            this.displayName = id;
            this.tags = new List<string>();
            this.description = "";
        }
    }

    /// <summary>
    /// 表情包加载器 - 从指定文件夹加载用户自定义表情包
    /// </summary>
    public static class EmoticonLoader
    {
        private const string EMOTICON_FOLDER = "Emoticons";  // 表情包文件夹名
        private const string METADATA_FILE = "emoticons.txt"; // 元数据文件

        /// <summary>
        /// 从模组目录加载所有表情包
        /// </summary>
        public static List<EmoticonData> LoadAllEmoticons()
        {
            var emoticons = new List<EmoticonData>();

            try
            {
                // 获取模组根目录
                string modRootPath = GetModRootPath();
                if (string.IsNullOrEmpty(modRootPath))
                {
                    Log.Warning("[EmoticonLoader] 无法找到模组根目录");
                    return emoticons;
                }

                // 表情包文件夹路径
                string emoticonPath = Path.Combine(modRootPath, EMOTICON_FOLDER);

                // 如果文件夹不存在，创建它
                if (!Directory.Exists(emoticonPath))
                {
                    Directory.CreateDirectory(emoticonPath);
                    Log.Message($"[EmoticonLoader] 已创建表情包文件夹: {emoticonPath}");
                    CreateSampleMetadataFile(emoticonPath);
                    return emoticons;
                }

                // 加载元数据（如果存在）
                var metadata = LoadMetadata(emoticonPath);

                // 支持的图片格式
                string[] supportedExtensions = { "*.png", "*.jpg", "*.jpeg" };

                // 扫描所有图片文件
                foreach (string ext in supportedExtensions)
                {
                    var files = Directory.GetFiles(emoticonPath, ext, SearchOption.TopDirectoryOnly);
                    
                    foreach (string file in files)
                    {
                        try
                        {
                            string fileName = Path.GetFileNameWithoutExtension(file);
                            
                            // 跳过元数据文件
                            if (fileName.Equals("emoticons", StringComparison.OrdinalIgnoreCase))
                                continue;

                            var emoticon = new EmoticonData(fileName, file);

                            // 应用元数据（如果有）
                            if (metadata.ContainsKey(fileName))
                            {
                                var meta = metadata[fileName];
                                emoticon.displayName = meta.displayName ?? fileName;
                                emoticon.tags = meta.tags;
                                emoticon.description = meta.description ?? "";
                            }

                            // 加载纹理
                            emoticon.texture = LoadTextureFromFile(file);

                            if (emoticon.texture != null)
                            {
                                emoticons.Add(emoticon);
                                Log.Message($"[EmoticonLoader] 已加载表情包: {emoticon.id} (标签: {string.Join(", ", emoticon.tags)})");
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.Error($"[EmoticonLoader] 加载表情包失败 {file}: {ex.Message}");
                        }
                    }
                }

                Log.Message($"[EmoticonLoader] 成功加载 {emoticons.Count} 个表情包");
            }
            catch (Exception ex)
            {
                Log.Error($"[EmoticonLoader] 加载表情包时发生错误: {ex}");
            }

            return emoticons;
        }

        /// <summary>
        /// 从文件加载纹理
        /// </summary>
        private static Texture2D LoadTextureFromFile(string filePath)
        {
            try
            {
                byte[] fileData = File.ReadAllBytes(filePath);
                
                // 创建纹理
                Texture2D texture = new Texture2D(2, 2);
                
                // 加载图片数据
                if (texture.LoadImage(fileData))
                {
                    texture.name = Path.GetFileNameWithoutExtension(filePath);
                    return texture;
                }
                else
                {
                    Log.Error($"[EmoticonLoader] 无法解析图片: {filePath}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[EmoticonLoader] 加载纹理失败 {filePath}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 加载元数据文件
        /// </summary>
        private static Dictionary<string, EmoticonMetadata> LoadMetadata(string emoticonPath)
        {
            var metadata = new Dictionary<string, EmoticonMetadata>();
            string metaFile = Path.Combine(emoticonPath, METADATA_FILE);

            if (!File.Exists(metaFile))
            {
                return metadata;
            }

            try
            {
                var lines = File.ReadAllLines(metaFile);
                EmoticonMetadata currentMeta = null;

                foreach (string line in lines)
                {
                    string trimmed = line.Trim();

                    // 跳过空行和注释
                    if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#"))
                        continue;

                    // 新表情包定义
                    if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
                    {
                        string id = trimmed.Substring(1, trimmed.Length - 2);
                        currentMeta = new EmoticonMetadata { id = id };
                        metadata[id] = currentMeta;
                    }
                    else if (currentMeta != null && trimmed.Contains("="))
                    {
                        // 属性定义
                        string[] parts = trimmed.Split(new[] { '=' }, 2);
                        string key = parts[0].Trim().ToLower();
                        string value = parts.Length > 1 ? parts[1].Trim() : "";

                        switch (key)
                        {
                            case "name":
                                currentMeta.displayName = value;
                                break;
                            case "tags":
                                currentMeta.tags = value.Split(',')
                                    .Select(t => t.Trim().ToLower())
                                    .Where(t => !string.IsNullOrEmpty(t))
                                    .ToList();
                                break;
                            case "description":
                                currentMeta.description = value;
                                break;
                        }
                    }
                }

                Log.Message($"[EmoticonLoader] 已加载 {metadata.Count} 个表情包元数据");
            }
            catch (Exception ex)
            {
                Log.Error($"[EmoticonLoader] 加载元数据失败: {ex.Message}");
            }

            return metadata;
        }

        /// <summary>
        /// 创建示例元数据文件
        /// </summary>
        private static void CreateSampleMetadataFile(string emoticonPath)
        {
            string metaFile = Path.Combine(emoticonPath, METADATA_FILE);

            try
            {
                string sampleContent = @"# 表情包元数据文件
# 格式说明：
# [表情包ID]  - 必须与文件名（不含扩展名）一致
# name = 显示名称
# tags = 标签1, 标签2, 标签3  - 用逗号分隔
# description = 描述文字

# 可用标签：
# happy, joy, excited - 开心、喜悦
# sad, disappointed, crying - 难过、失望
# angry, frustrated - 生气、沮丧
# surprised, shocked - 惊讶、震惊
# confused, thinking - 困惑、思考
# love, affection - 爱、亲昵
# neutral, calm - 中性、平静
# smug, proud - 得意、自豪
# embarrassed, shy - 尴尬、害羞
# tired, sleepy - 疲惫、困倦

# 示例（请根据实际文件名修改）：
# [smile]
# name = 微笑
# tags = happy, joy
# description = 开心的微笑

# [cry]
# name = 哭泣
# tags = sad, crying
# description = 伤心地哭泣

# [think]
# name = 思考
# tags = thinking, confused
# description = 陷入思考
";

                File.WriteAllText(metaFile, sampleContent);
                Log.Message($"[EmoticonLoader] 已创建示例元数据文件: {metaFile}");
            }
            catch (Exception ex)
            {
                Log.Error($"[EmoticonLoader] 创建元数据文件失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取模组根目录
        /// </summary>
        private static string GetModRootPath()
        {
            try
            {
                // 通过 ModContentPack 获取
                var mod = LoadedModManager.RunningMods.FirstOrDefault(m => 
                    m.PackageId.ToLower().Contains("thesecondseat") || 
                    m.Name.Contains("The Second Seat"));

                if (mod != null)
                {
                    return mod.RootDir;
                }

                Log.Warning("[EmoticonLoader] 无法通过 PackageId 找到模组");

                // 备用方案：从 Assembly 位置推断
                string assemblyPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                string modRoot = Path.GetDirectoryName(Path.GetDirectoryName(assemblyPath));
                
                return modRoot;
            }
            catch (Exception ex)
            {
                Log.Error($"[EmoticonLoader] 获取模组路径失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 元数据结构
        /// </summary>
        private class EmoticonMetadata
        {
            public string id;
            public string displayName;
            public List<string> tags = new List<string>();
            public string description;
        }
    }
}
