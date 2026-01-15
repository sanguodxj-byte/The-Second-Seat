using System;
using System.IO;
using System.Linq;
using Verse;

namespace TheSecondSeat.PersonaGeneration
{
    /// <summary>
    /// ? v1.6.71: 人格文件夹管理器 - 支持子 Mod 隔离
    /// 
    /// 核心功能：
    /// - 自动识别子 Mod（The Second Seat - {PersonaName}）
    /// - 通用人格 → 主 Mod 目录
    /// - 自动创建子 Mod 结构（About.xml, LoadFolders.xml）
    /// </summary>
    public static class PersonaFolderManager
    {
        // ==================== 公共 API ====================
        
        /// <summary>
        /// 获取人格的根目录（主 Mod 或子 Mod）
        /// </summary>
        public static string GetPersonaRootDirectory(string personaName)
        {
            // 1. 尝试从已加载的 Mod 中查找子 Mod (标准命名: The Second Seat - PersonaName)
            string subModName = $"The Second Seat - {personaName}";
            var subMod = LoadedModManager.RunningModsListForReading
                .FirstOrDefault(m => m.Name == subModName);

            if (subMod != null)
            {
                return subMod.RootDir;
            }

            // 2. 尝试查找 [TSS] 开头的 Mod (Sideria 风格: [TSS]PersonaName - Suffix)
            var tssMod = LoadedModManager.RunningModsListForReading
                .FirstOrDefault(m => m.Name.Contains($"[TSS]") && m.Name.Contains(personaName));
            
            if (tssMod != null)
            {
                return tssMod.RootDir;
            }
            
            // 3. 尝试通过 PackageId 查找 (例如: rim.thesecondseat.personaname)
            string targetPackageIdSuffix = $".thesecondseat.{personaName.ToLower()}";
            var packageIdMod = LoadedModManager.RunningModsListForReading
                .FirstOrDefault(m => m.PackageId.ToLower().EndsWith(targetPackageIdSuffix));

            if (packageIdMod != null)
            {
                return packageIdMod.RootDir;
            }

            // 4. 尝试在主 Mod 同级目录下查找文件夹（开发环境或未加载情况）
            string mainModDir = GetMainModRootDir();
            if (mainModDir == null)
            {
                Log.Error("[PersonaFolderManager] 无法找到主 Mod 目录");
                return null;
            }

            string parentDir = Directory.GetParent(mainModDir).FullName;
            
            // 4.1 检查标准命名文件夹
            string potentialSubModDir = Path.Combine(parentDir, subModName);
            if (Directory.Exists(potentialSubModDir))
            {
                return potentialSubModDir;
            }
            
            // 4.2 检查 [TSS] 命名文件夹
            try 
            {
                var directories = Directory.GetDirectories(parentDir);
                foreach (var dir in directories)
                {
                    string dirName = new DirectoryInfo(dir).Name;
                    if (dirName.StartsWith("[TSS]") && dirName.Contains(personaName))
                    {
                        return dir;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[PersonaFolderManager] 遍历目录查找子 Mod 失败: {ex.Message}");
            }
            
            // 5. 默认为通用人格，使用主 Mod 目录
            return mainModDir;
        }
        
        /// <summary>
        /// 获取人格的 Defs 目录
        /// </summary>
        public static string GetPersonaDefsDirectory(string personaName)
        {
            string rootDir = GetPersonaRootDirectory(personaName);
            if (rootDir == null) return null;
            
            string defsDir = Path.Combine(rootDir, "Defs");
            
            if (!Directory.Exists(defsDir))
            {
                Directory.CreateDirectory(defsDir);
            }
            
            return defsDir;
        }
        
        /// <summary>
        /// 获取人格的立绘目录
        /// </summary>
        public static string GetPersonaPortraitsDirectory(string personaName)
        {
            string rootDir = GetPersonaRootDirectory(personaName);
            if (rootDir == null) return null;
            
            string portraitsDir = Path.Combine(rootDir, "Textures", "UI", "Narrators");
            
            if (!Directory.Exists(portraitsDir))
            {
                Directory.CreateDirectory(portraitsDir);
            }
            
            return portraitsDir;
        }
        
        /// <summary>
        /// 获取人格的表情目录
        /// </summary>
        public static string GetPersonaExpressionsDirectory(string personaName)
        {
            string rootDir = GetPersonaRootDirectory(personaName);
            if (rootDir == null) return null;
            
            string sanitizedName = SanitizeFileName(personaName);
            string expressionsDir = Path.Combine(rootDir, "Textures", "UI", "Narrators", "9x16", "Expressions", sanitizedName);
            
            if (!Directory.Exists(expressionsDir))
            {
                Directory.CreateDirectory(expressionsDir);
            }
            
            return expressionsDir;
        }
        
        /// <summary>
        /// 获取人格的服装目录
        /// </summary>
        public static string GetPersonaOutfitsDirectory(string personaName)
        {
            string rootDir = GetPersonaRootDirectory(personaName);
            if (rootDir == null) return null;
            
            string sanitizedName = SanitizeFileName(personaName);
            string outfitsDir = Path.Combine(rootDir, "Textures", "UI", "Narrators", "9x16", sanitizedName, "Outfits");
            
            if (!Directory.Exists(outfitsDir))
            {
                Directory.CreateDirectory(outfitsDir);
            }
            
            return outfitsDir;
        }
        
        /// <summary>
        /// 检查人格是否使用子 Mod
        /// </summary>
        public static bool UsesSubMod(string personaName)
        {
            string rootDir = GetPersonaRootDirectory(personaName);
            string mainDir = GetMainModRootDir();
            
            // 如果根目录与主 Mod 目录不同，则说明使用了子 Mod
            return rootDir != null && mainDir != null && rootDir != mainDir;
        }

        /// <summary>
        /// 为人格创建新的子 Mod（如果不存在）
        /// </summary>
        public static string CreateSubMod(string personaName)
        {
            string mainModDir = GetMainModRootDir();
            if (mainModDir == null) return null;

            string parentDir = Directory.GetParent(mainModDir).FullName;
            string subModName = $"The Second Seat - {personaName}";
            string subModDir = Path.Combine(parentDir, subModName);

            if (!Directory.Exists(subModDir))
            {
                Directory.CreateDirectory(subModDir);
                Log.Message($"[PersonaFolderManager] 创建子 Mod 文件夹: {subModDir}");
                CreateSubModStructure(subModDir, personaName);
            }

            return subModDir;
        }
        
        // ==================== 私有方法 ====================
        
        /// <summary>
        /// 创建子 Mod 结构
        /// </summary>
        private static void CreateSubModStructure(string subModDir, string personaName)
        {
            try
            {
                // 1. 创建 About\About.xml
                CreateAboutXml(subModDir, personaName);
                
                // 2. 创建 LoadFolders.xml
                CreateLoadFoldersXml(subModDir);
                
                // 3. 创建 Defs 目录
                Directory.CreateDirectory(Path.Combine(subModDir, "Defs"));
                
                // 4. 创建 Textures 目录
                Directory.CreateDirectory(Path.Combine(subModDir, "Textures", "UI", "Narrators"));
                
                // 5. 创建 Languages 目录
                Directory.CreateDirectory(Path.Combine(subModDir, "Languages", "ChineseSimplified", "Keyed"));
                Directory.CreateDirectory(Path.Combine(subModDir, "Languages", "English", "Keyed"));
                
                Log.Message($"[PersonaFolderManager] 子 Mod 结构创建完成: {personaName}");
            }
            catch (Exception ex)
            {
                Log.Error($"[PersonaFolderManager] 创建子 Mod 结构失败: {ex}");
            }
        }
        
        /// <summary>
        /// 创建 About.xml
        /// </summary>
        private static void CreateAboutXml(string subModDir, string personaName)
        {
            string aboutDir = Path.Combine(subModDir, "About");
            Directory.CreateDirectory(aboutDir);
            
            string aboutPath = Path.Combine(aboutDir, "About.xml");
            
            if (File.Exists(aboutPath))
            {
                // 已存在，跳过
                return;
            }
            
            string aboutContent = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<ModMetaData>
  <name>The Second Seat - {personaName}</name>
  <author>Your Name</author>
  <packageId>rim.thesecondseat.{personaName.ToLower()}</packageId>
  <description>
    {personaName} persona resources for The Second Seat.
    This is a sub-module and requires The Second Seat main mod to work.
  </description>
  <supportedVersions>
    <li>1.5</li>
    <li>1.6</li>
  </supportedVersions>
  <modDependencies>
    <li>
      <packageId>rim.thesecondseat</packageId>
      <displayName>The Second Seat</displayName>
    </li>
  </modDependencies>
  <loadAfter>
    <li>rim.thesecondseat</li>
  </loadAfter>
</ModMetaData>";
            
            File.WriteAllText(aboutPath, aboutContent, System.Text.Encoding.UTF8);
            Log.Message($"[PersonaFolderManager] 创建 About.xml: {aboutPath}");
        }
        
        /// <summary>
        /// 创建 LoadFolders.xml
        /// </summary>
        private static void CreateLoadFoldersXml(string subModDir)
        {
            string loadFoldersPath = Path.Combine(subModDir, "LoadFolders.xml");
            
            if (File.Exists(loadFoldersPath))
            {
                // 已存在，跳过
                return;
            }
            
            string loadFoldersContent = @"<?xml version=""1.0"" encoding=""utf-8""?>
<loadFolders>
  <v1.5>
    <li>/</li>
  </v1.5>
  <v1.6>
    <li>1.6</li>
    <li>/</li>
  </v1.6>
</loadFolders>";
            
            File.WriteAllText(loadFoldersPath, loadFoldersContent, System.Text.Encoding.UTF8);
            Log.Message($"[PersonaFolderManager] 创建 LoadFolders.xml: {loadFoldersPath}");
        }
        
        /// <summary>
        /// 获取主 Mod 根目录
        /// </summary>
        private static string GetMainModRootDir()
        {
            // 1. 通过 PackageId 或 Name 查找
            var modContentPack = LoadedModManager.RunningModsListForReading
                .FirstOrDefault(mod => mod.PackageId.ToLower().Contains("thesecondseat") || 
                                      mod.Name.Contains("Second Seat"));
            
            if (modContentPack != null) return modContentPack.RootDir;

            // 2. 通过程序集查找 (更稳健)
            var assembly = typeof(PersonaFolderManager).Assembly;
            modContentPack = LoadedModManager.RunningModsListForReading
                .FirstOrDefault(mod => mod.assemblies.loadedAssemblies.Contains(assembly));

            if (modContentPack != null) return modContentPack.RootDir;

            Log.Error("[PersonaFolderManager] 无法定位主 Mod 根目录！请检查 Mod 安装状态。");
            return null;
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
            
            char[] invalidChars = Path.GetInvalidFileNameChars();
            string sanitized = fileName;
            
            foreach (char c in invalidChars)
            {
                sanitized = sanitized.Replace(c, '_');
            }
            
            sanitized = sanitized.Replace(" ", "_");
            sanitized = sanitized.Replace("(", "");
            sanitized = sanitized.Replace(")", "");
            sanitized = sanitized.Replace("[", "");
            sanitized = sanitized.Replace("]", "");
            
            return sanitized;
        }
    }
}
