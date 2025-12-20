using System;
using System.IO;
using System.Text;
using System.Linq;
using UnityEngine;
using Verse;
using RimWorld;

namespace TheSecondSeat.PersonaGeneration
{
    /// <summary>
    /// 人格定义导出工具
    /// ? 将生成的人格导出为XML文件和立绘文件
    /// </summary>
    public static class PersonaDefExporter
    {
        /// <summary>
        /// 导出人格（包括立绘和XML定义）
        /// ? 同时注册到DefDatabase和保存为XML文件
        /// </summary>
        public static bool ExportPersona(NarratorPersonaDef persona, string sourcePortraitPath, Texture2D texture)
        {
            try
            {
                // 1. 复制立绘到Mod目录
                string portraitFileName = CopyPortraitToModDirectory(persona, sourcePortraitPath, texture);
                
                if (string.IsNullOrEmpty(portraitFileName))
                {
                    Log.Warning($"[PersonaDefExporter] 立绘复制失败，跳过导出: {persona.defName}");
                    return false;
                }
                
                // 2. 更新人格定义中的立绘路径
                persona.portraitPath = $"UI/Narrators/{Path.GetFileNameWithoutExtension(portraitFileName)}";
                persona.useCustomPortrait = false; // 使用Mod立绘
                
                // 3. ? 创建人格专属文件夹结构
                CreatePersonaDirectories(persona);
                
                // 4. ? 立即注册到DefDatabase（运行时可用）
                if (!DefDatabase<NarratorPersonaDef>.AllDefs.Contains(persona))
                {
                    DefDatabase<NarratorPersonaDef>.Add(persona);
                    Log.Message($"[PersonaDefExporter] 已注册人格到DefDatabase: {persona.defName}");
                }
                
                // 5. 生成XML定义文件（持久化，下次启动自动加载）
                string xmlContent = GeneratePersonaDefXml(persona);
                
                // 6. 保存XML文件
                string xmlFilePath = SavePersonaDefXml(persona.defName, xmlContent);
                
                // 7. 清除立绘缓存，强制重新加载
                PortraitLoader.ClearCache();
                
                // 8. 提示用户
                Messages.Message(
                    $"[成功] 成功导出人格：{persona.narratorName}\n" +
                    $"[文件] 定义文件: {Path.GetFileName(xmlFilePath)}\n" +
                    $"[立绘] 立绘文件: {portraitFileName}\n" +
                    $"[文件夹] 已创建表情和服装文件夹\n" +
                    $"[提示] 重启游戏后将永久保存",
                    MessageTypeDefOf.PositiveEvent
                );
                
                Log.Message($"[PersonaDefExporter] 成功导出人格: {persona.narratorName}\n" +
                           $"  立绘: {portraitFileName}\n" +
                           $"  定义: {xmlFilePath}\n" +
                           $"  注册状态: 已添加到DefDatabase");
                
                return true;
            }
            catch (Exception ex)
            {
                Log.Error($"[PersonaDefExporter] 导出人格失败: {persona.defName}\n{ex}");
                Messages.Message($"? 导出人格失败: {ex.Message}", MessageTypeDefOf.RejectInput);
                return false;
            }
        }
        
        /// <summary>
        /// ? 创建人格专属文件夹结构
        /// </summary>
        private static void CreatePersonaDirectories(NarratorPersonaDef persona)
        {
            try
            {
                // 获取 Mod 根目录
                var modContentPack = LoadedModManager.RunningModsListForReading
                    .FirstOrDefault(mod => mod.PackageId.ToLower().Contains("thesecondseat") || 
                                          mod.Name.Contains("Second Seat"));
                
                if (modContentPack == null)
                {
                    Log.Warning("[PersonaDefExporter] 无法找到Mod目录，跳过文件夹创建");
                    return;
                }
                
                string personaName = SanitizeFileName(persona.narratorName);
                
                // 创建人格主文件夹
                string personaBaseDir = Path.Combine(
                    modContentPack.RootDir, 
                    "Textures", "UI", "Narrators", "9x16", personaName
                );
                
                if (!Directory.Exists(personaBaseDir))
                {
                    Directory.CreateDirectory(personaBaseDir);
                    Log.Message($"[PersonaDefExporter] 创建人格文件夹: {personaBaseDir}");
                }
                
                // 创建表情文件夹
                string expressionsDir = Path.Combine(
                    modContentPack.RootDir, 
                    "Textures", "UI", "Narrators", "9x16", "Expressions", personaName
                );
                
                if (!Directory.Exists(expressionsDir))
                {
                    Directory.CreateDirectory(expressionsDir);
                    Log.Message($"[PersonaDefExporter] 创建表情文件夹: {expressionsDir}");
                    
                    // 创建说明文件
                    string readmePath = Path.Combine(expressionsDir, "README.txt");
                    File.WriteAllText(readmePath, 
                        $"# {persona.narratorName} 表情差分文件夹\n\n" +
                        $"将表情立绘放在此处，命名规则：\n" +
                        $"- happy.png        (开心)\n" +
                        $"- sad.png          (悲伤)\n" +
                        $"- angry.png        (愤怒)\n" +
                        $"- surprised.png    (惊讶)\n" +
                        $"- thoughtful.png   (沉思)\n" +
                        $"- annoyed.png      (烦躁)\n" +
                        $"- smug.png         (得意)\n" +
                        $"- worried.png      (担忧)\n" +
                        $"- disappointed.png (失望)\n" +
                        $"- playful.png      (调皮) ? 新增\n\n" +
                        $"注意：\n" +
                        $"1. 文件名必须小写\n" +
                        $"2. 支持 PNG 和 JPG 格式\n" +
                        $"3. 推荐尺寸与基础立绘一致（如 1024x2048）\n" +
                        $"4. 如果使用完整立绘，系统会自动裁剪面部区域\n",
                        Encoding.UTF8
                    );
                }
                
                // 创建服装文件夹
                string outfitsDir = Path.Combine(personaBaseDir, "Outfits");
                
                if (!Directory.Exists(outfitsDir))
                {
                    Directory.CreateDirectory(outfitsDir);
                    Log.Message($"[PersonaDefExporter] 创建服装文件夹: {outfitsDir}");
                    
                    // 创建说明文件
                    string readmePath = Path.Combine(outfitsDir, "README.txt");
                    File.WriteAllText(readmePath, 
                        $"# {persona.narratorName} 服装差分文件夹\n\n" +
                        $"将服装立绘放在此处，命名规则：\n" +
                        $"- neutral_1.png   (中性服装 - 好感度 0-199)\n" +
                        $"- warm_1.png      (温暖服装 - 好感度 200-499)\n" +
                        $"- intimate_1.png  (亲密服装 - 好感度 500-799)\n" +
                        $"- devoted_1.png   (挚爱服装 - 好感度 800+)\n\n" +
                        $"注意：\n" +
                        $"1. 文件名必须小写\n" +
                        $"2. 支持 PNG 和 JPG 格式\n" +
                        $"3. 推荐尺寸与基础立绘一致（如 1024x2048）\n" +
                        $"4. 服装差分会与表情差分自动合成\n",
                        Encoding.UTF8
                    );
                }
                
                Log.Message($"[PersonaDefExporter] 人格文件夹结构创建完成: {personaName}");
            }
            catch (Exception ex)
            {
                Log.Warning($"[PersonaDefExporter] 创建人格文件夹失败（非致命错误）: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 复制立绘到Mod目录
        /// </summary>
        private static string CopyPortraitToModDirectory(NarratorPersonaDef persona, string sourcePath, Texture2D texture)
        {
            try
            {
                // 获取Mod立绘目录
                string modPortraitsDir = PortraitLoader.GetModPortraitsDirectory();
                
                // 生成文件名（使用人格名称，清理非法字符）
                string safeFileName = SanitizeFileName(persona.narratorName);
                string targetFileName = $"{safeFileName}.png";
                string targetPath = Path.Combine(modPortraitsDir, targetFileName);
                
                // ? 修复：优先使用传入的 Texture2D（ContentFinder 已加载）
                if (texture != null)
                {
                    SaveTextureAsPNG(texture, targetPath);
                    Log.Message($"[PersonaDefExporter] 已从 Texture2D 保存立绘: {targetPath}");
                    return targetFileName;
                }
                
                // ? 降级：尝试作为文件路径复制（外部文件）
                if (File.Exists(sourcePath))
                {
                    // 复制文件逻辑（保留原有的重试机制）
                    bool copySuccess = false;
                    int retryCount = 0;
                    int maxRetries = 5;
                    
                    while (!copySuccess && retryCount < maxRetries)
                    {
                        try
                        {
                            // 如果目标文件存在，先尝试删除
                            if (File.Exists(targetPath))
                            {
                                try
                                {
                                    File.Delete(targetPath);
                                    System.Threading.Thread.Sleep(200);
                                }
                                catch (IOException)
                                {
                                    Log.Warning($"[PersonaDefExporter] 无法删除目标文件（占用），尝试直接覆盖... ({retryCount + 1}/{maxRetries})");
                                }
                            }
                            
                            // 执行复制
                            File.Copy(sourcePath, targetPath, overwrite: true);
                            copySuccess = true;
                            Log.Message($"[PersonaDefExporter] 已复制立绘: {sourcePath} → {targetPath}");
                        }
                        catch (IOException ex)
                        {
                            retryCount++;
                            if (retryCount >= maxRetries)
                            {
                                Log.Error($"[PersonaDefExporter] 复制立绘失败（重试{maxRetries}次后）: {ex.Message}");
                                
                                // 最后尝试：使用不同的文件名
                                string alternativeFileName = $"{safeFileName}_{DateTime.Now:yyyyMMddHHmmss}.png";
                                string alternativePath = Path.Combine(modPortraitsDir, alternativeFileName);
                                
                                try
                                {
                                    File.Copy(sourcePath, alternativePath, overwrite: false);
                                    Log.Warning($"[PersonaDefExporter] 使用备用文件名保存: {alternativeFileName}");
                                    return alternativeFileName;
                                }
                                catch
                                {
                                    throw new IOException($"无法复制立绘文件。目标文件可能被Unity编辑器占用。\n" +
                                                        $"请关闭所有图片查看器然后重试。\n" +
                                                        $"目标路径: {targetPath}\n" +
                                                        $"原始错误: {ex.Message}");
                                }
                            }
                            
                            Log.Warning($"[PersonaDefExporter] 复制失败，{500 * (retryCount + 1)}ms后重试... ({retryCount}/{maxRetries})");
                            System.Threading.Thread.Sleep(500 * (retryCount + 1));
                        }
                    }
                    
                    return targetFileName;
                }
                
                // ? 如果既没有 Texture 也不是文件路径，尝试用 ContentFinder 加载
                Log.Warning($"[PersonaDefExporter] 源路径不是文件系统路径，尝试用 ContentFinder 加载: {sourcePath}");
                
                Texture2D loadedTexture = null;
                if (sourcePath.StartsWith("UI/"))
                {
                    loadedTexture = ContentFinder<Texture2D>.Get(sourcePath, false);
                }
                
                if (loadedTexture != null)
                {
                    SaveTextureAsPNG(loadedTexture, targetPath);
                    Log.Message($"[PersonaDefExporter] 已从 ContentFinder 保存立绘: {targetPath}");
                    return targetFileName;
                }
                
                // 全部失败
                Log.Error($"[PersonaDefExporter] 无法获取立绘纹理: {sourcePath}");
                return null;
            }
            catch (Exception ex)
            {
                Log.Error($"[PersonaDefExporter] 复制立绘失败: {ex}");
                Messages.Message($"?? 立绘复制失败\n{ex.Message}\n\n请确认没有程序打开该图片", MessageTypeDefOf.RejectInput);
                return null;
            }
        }
        
        /// <summary>
        /// 保存Texture2D为PNG文件
        /// </summary>
        private static void SaveTextureAsPNG(Texture2D texture, string filePath)
        {
            // 创建可读纹理副本（因为原始纹理可能不可读）
            RenderTexture renderTex = RenderTexture.GetTemporary(texture.width, texture.height, 0, RenderTextureFormat.Default, RenderTextureReadWrite.Linear);
            Graphics.Blit(texture, renderTex);
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = renderTex;
            
            Texture2D readableTexture = new Texture2D(texture.width, texture.height);
            readableTexture.ReadPixels(new Rect(0, 0, renderTex.width, renderTex.height), 0, 0);
            readableTexture.Apply();
            
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(renderTex);
            
            // 编码为PNG
            byte[] pngData = readableTexture.EncodeToPNG();
            File.WriteAllBytes(filePath, pngData);
            
            UnityEngine.Object.Destroy(readableTexture);
        }
        
        /// <summary>
        /// 生成人格定义XML内容
        /// </summary>
        private static string GeneratePersonaDefXml(NarratorPersonaDef persona)
        {
            var sb = new StringBuilder();
            
            sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
            sb.AppendLine("<Defs>");
            sb.AppendLine();
            sb.AppendLine("  <!-- 自动生成的人格定义 -->");
            sb.AppendLine($"  <!-- 生成时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss} -->");
            sb.AppendLine();
            sb.AppendLine("  <TheSecondSeat.PersonaGeneration.NarratorPersonaDef>");
            sb.AppendLine($"    <defName>{EscapeXml(persona.defName)}</defName>");
            sb.AppendLine($"    <label>{EscapeXml(persona.label)}</label>");
            sb.AppendLine($"    <narratorName>{EscapeXml(persona.narratorName)}</narratorName>");
            sb.AppendLine();
            
            // 立绘路径
            if (!string.IsNullOrEmpty(persona.portraitPath))
            {
                sb.AppendLine($"    <portraitPath>{EscapeXml(persona.portraitPath)}</portraitPath>");
            }
            sb.AppendLine();
            
            // 颜色
            sb.AppendLine($"    <primaryColor>({persona.primaryColor.r:F2}, {persona.primaryColor.g:F2}, {persona.primaryColor.b:F2}, {persona.primaryColor.a:F2})</primaryColor>");
            sb.AppendLine($"    <accentColor>({persona.accentColor.r:F2}, {persona.accentColor.g:F2}, {persona.accentColor.b:F2}, {persona.accentColor.a:F2})</accentColor>");
            sb.AppendLine();
            
            // 简介（需要换行处理）
            sb.AppendLine("    <biography>");
            sb.AppendLine(IndentText(EscapeXml(persona.biography), 6));
            sb.AppendLine("    </biography>");
            sb.AppendLine();
            
            // ? 外观描述（Vision 分析结果 - 仅当与 biography 不同时才保存）
            if (!string.IsNullOrEmpty(persona.visualDescription) && 
                persona.visualDescription != persona.biography)
            {
                sb.AppendLine("    <!-- Vision 分析结果（AI 对自身外观的理解）-->");
                sb.AppendLine("    <visualDescription>");
                sb.AppendLine(IndentText(EscapeXml(persona.visualDescription), 6));
                sb.AppendLine("    </visualDescription>");
                sb.AppendLine();
            }
            
            if (!string.IsNullOrEmpty(persona.visualMood))
            {
                sb.AppendLine($"    <visualMood>{EscapeXml(persona.visualMood)}</visualMood>");
                sb.AppendLine();
            }
            
            if (persona.visualElements != null && persona.visualElements.Count > 0)
            {
                sb.AppendLine("    <visualElements>");
                foreach (var element in persona.visualElements)
                {
                    sb.AppendLine($"      <li>{EscapeXml(element)}</li>");
                }
                sb.AppendLine("    </visualElements>");
                sb.AppendLine();
            }
            
            // ? 人格特质（如果有）
            if (!string.IsNullOrEmpty(persona.overridePersonality))
            {
                sb.AppendLine($"    <overridePersonality>{EscapeXml(persona.overridePersonality)}</overridePersonality>");
                sb.AppendLine();
            }
            
            // ? 对话风格
            if (persona.dialogueStyle != null)
            {
                sb.AppendLine("    <dialogueStyle>");
                sb.AppendLine($"      <formalityLevel>{persona.dialogueStyle.formalityLevel:F2}</formalityLevel>");
                sb.AppendLine($"      <emotionalExpression>{persona.dialogueStyle.emotionalExpression:F2}</emotionalExpression>");
                sb.AppendLine($"      <humorLevel>{persona.dialogueStyle.humorLevel:F2}</humorLevel>");
                sb.AppendLine($"      <sarcasmLevel>{persona.dialogueStyle.sarcasmLevel:F2}</sarcasmLevel>");
                sb.AppendLine($"      <verbosity>{persona.dialogueStyle.verbosity:F2}</verbosity>");
                
                // ? 添加布尔标志
                if (persona.dialogueStyle.useEmoticons)
                {
                    sb.AppendLine($"      <useEmoticons>true</useEmoticons>");
                }
                if (persona.dialogueStyle.useEllipsis)
                {
                    sb.AppendLine($"      <useEllipsis>true</useEllipsis>");
                }
                if (persona.dialogueStyle.useExclamation)
                {
                    sb.AppendLine($"      <useExclamation>true</useExclamation>");
                }
                
                sb.AppendLine("    </dialogueStyle>");
                sb.AppendLine();
            }
            
            // ? 事件偏好（如果有）
            if (persona.eventPreferences != null)
            {
                sb.AppendLine("    <eventPreferences>");
                sb.AppendLine($"      <positiveEventBias>{persona.eventPreferences.positiveEventBias:F2}</positiveEventBias>");
                sb.AppendLine($"      <negativeEventBias>{persona.eventPreferences.negativeEventBias:F2}</negativeEventBias>");
                sb.AppendLine($"      <chaosLevel>{persona.eventPreferences.chaosLevel:F2}</chaosLevel>");
                sb.AppendLine($"      <interventionFrequency>{persona.eventPreferences.interventionFrequency:F2}</interventionFrequency>");
                sb.AppendLine("    </eventPreferences>");
                sb.AppendLine();
            }
            
            // ? 语气标签
            if (persona.toneTags != null && persona.toneTags.Count > 0)
            {
                sb.AppendLine("    <toneTags>");
                foreach (var tag in persona.toneTags)
                {
                    sb.AppendLine($"      <li>{EscapeXml(tag)}</li>");
                }
                sb.AppendLine("    </toneTags>");
                sb.AppendLine();
            }
            
            sb.AppendLine("  </TheSecondSeat.PersonaGeneration.NarratorPersonaDef>");
            sb.AppendLine();
            sb.AppendLine("</Defs>");
            
            return sb.ToString();
        }
        
        /// <summary>
        /// 保存人格定义XML文件
        /// </summary>
        private static string SavePersonaDefXml(string defName, string xmlContent)
        {
            try
            {
                // 获取Mod根目录
                var modContentPack = LoadedModManager.RunningModsListForReading
                    .FirstOrDefault(mod => mod.PackageId.ToLower().Contains("thesecondseat") || 
                                          mod.Name.Contains("Second Seat"));
                
                if (modContentPack == null)
                {
                    Log.Error("[PersonaDefExporter] 无法找到Mod目录");
                    return null;
                }
                
                // 创建Defs/NarratorPersonaDefs目录
                string defsDir = Path.Combine(modContentPack.RootDir, "Defs", "NarratorPersonaDefs");
                if (!Directory.Exists(defsDir))
                {
                    Directory.CreateDirectory(defsDir);
                    Log.Message($"[PersonaDefExporter] 创建目录: {defsDir}");
                }
                
                // 生成文件名
                string fileName = $"{SanitizeFileName(defName)}.xml";
                string filePath = Path.Combine(defsDir, fileName);
                
                // 保存文件（UTF-8编码，无BOM）
                File.WriteAllText(filePath, xmlContent, new UTF8Encoding(false));
                
                Log.Message($"[PersonaDefExporter] 已保存人格定义: {filePath}");
                
                return filePath;
            }
            catch (Exception ex)
            {
                Log.Error($"[PersonaDefExporter] 保存XML文件失败: {ex}");
                return null;
            }
        }
        
        /// <summary>
        /// 清理文件名中的非法字符
        /// </summary>
        private static string SanitizeFileName(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                return "UnnamedPersona";
            }
            
            // 移除或替换非法字符
            char[] invalidChars = Path.GetInvalidFileNameChars();
            string sanitized = fileName;
            
            foreach (char c in invalidChars)
            {
                sanitized = sanitized.Replace(c, '_');
            }
            
            // 移除空格和特殊字符
            sanitized = sanitized.Replace(" ", "_");
            sanitized = sanitized.Replace("(", "");
            sanitized = sanitized.Replace(")", "");
            sanitized = sanitized.Replace("[", "");
            sanitized = sanitized.Replace("]", "");
            
            return sanitized;
        }
        
        /// <summary>
        /// XML转义
        /// </summary>
        private static string EscapeXml(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }
            
            return text
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;")
                .Replace("'", "&apos;");
        }
        
        /// <summary>
        /// 文本缩进
        /// </summary>
        private static string IndentText(string text, int spaces)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }
            
            string indent = new string(' ', spaces);
            string[] lines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            
            var sb = new StringBuilder();
            for (int i = 0; i < lines.Length; i++)
            {
                if (i > 0) sb.AppendLine();
                sb.Append(indent);
                sb.Append(lines[i].TrimEnd());
            }
            
            return sb.ToString();
        }
        
        /// <summary>
        /// 打开Defs目录
        /// </summary>
        public static void OpenDefsDirectory()
        {
            try
            {
                var modContentPack = LoadedModManager.RunningModsListForReading
                    .FirstOrDefault(mod => mod.PackageId.ToLower().Contains("thesecondseat") || 
                                          mod.Name.Contains("Second Seat"));
                
                if (modContentPack != null)
                {
                    string defsDir = Path.Combine(modContentPack.RootDir, "Defs", "NarratorPersonaDefs");
                    
                    if (!Directory.Exists(defsDir))
                    {
                        Directory.CreateDirectory(defsDir);
                    }
                    
                    Application.OpenURL("file://" + defsDir);
                    Messages.Message($"已打开人格定义目录:\n{defsDir}", MessageTypeDefOf.NeutralEvent);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[PersonaDefExporter] 打开目录失败: {ex}");
            }
        }
    }
}
