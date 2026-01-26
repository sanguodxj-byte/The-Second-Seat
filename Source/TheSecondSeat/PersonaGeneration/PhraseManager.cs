using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Verse;
using RimWorld;
using UnityEngine;

namespace TheSecondSeat.PersonaGeneration
{
    /// <summary>
    /// 短语库管理器 - 运行时短语选择和触发
    /// 单例模式，负责加载、缓存和选择短语
    /// </summary>
    public class PhraseManager
    {
        private static PhraseManager instance;
        public static PhraseManager Instance => instance ??= new PhraseManager();

        // 短语库缓存 (personaDefName -> PhraseLibraryDef)
        private Dictionary<string, PhraseLibraryDef> libraryCache = new Dictionary<string, PhraseLibraryDef>();

        // 最近使用的短语（避免重复）
        private Dictionary<string, Queue<string>> recentPhrases = new Dictionary<string, Queue<string>>();
        private const int MAX_RECENT_PHRASES = 10;

        // 好感度缓存 (personaDefName -> affinity)
        private Dictionary<string, float> affinityCache = new Dictionary<string, float>();

        /// <summary>
        /// 短语显示事件
        /// </summary>
        public event Action<string, string> OnPhraseTriggered; // personaName, phrase

        /// <summary>
        /// 初始化管理器，加载所有短语库
        /// </summary>
        public void Initialize()
        {
            Log.Message("[PhraseManager] 初始化短语库管理器...");

            // 从 Defs 加载所有短语库
            LoadAllLibrariesFromDefs();

            // 从缓存文件加载运行时生成的短语库
            LoadCachedLibraries();

            Log.Message($"[PhraseManager] 加载了 {libraryCache.Count} 个短语库");
        }

        /// <summary>
        /// 从 DefDatabase 加载短语库
        /// </summary>
        private void LoadAllLibrariesFromDefs()
        {
            foreach (var lib in DefDatabase<PhraseLibraryDef>.AllDefs)
            {
                if (!string.IsNullOrEmpty(lib.personaDefName))
                {
                    libraryCache[lib.personaDefName] = lib;
                    if (Prefs.DevMode)
                    {
                        Log.Message($"[PhraseManager] 加载短语库: {lib.defName} (人格: {lib.personaDefName}, 短语数: {lib.GetTotalPhraseCount()})");
                    }
                }
            }
        }

        /// <summary>
        /// 从缓存目录加载运行时生成的短语库
        /// </summary>
        private void LoadCachedLibraries()
        {
            string cacheDir = GetCacheDirectory();
            if (!Directory.Exists(cacheDir))
            {
                Directory.CreateDirectory(cacheDir);
                return;
            }

            foreach (var file in Directory.GetFiles(cacheDir, "*.xml"))
            {
                try
                {
                    // 使用 RimWorld 的 XML 加载器
                    // 注意：这里需要手动解析，因为不是标准 Def 加载
                    if (Prefs.DevMode)
                    {
                        Log.Message($"[PhraseManager] 发现缓存文件: {file}");
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning($"[PhraseManager] 加载缓存文件失败: {file}, {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 注册运行时生成的短语库
        /// </summary>
        public void RegisterLibrary(PhraseLibraryDef library)
        {
            if (library == null || string.IsNullOrEmpty(library.personaDefName))
            {
                Log.Warning("[PhraseManager] 尝试注册无效的短语库");
                return;
            }

            libraryCache[library.personaDefName] = library;
            Log.Message($"[PhraseManager] 注册短语库: {library.defName} (短语数: {library.GetTotalPhraseCount()})");
        }

        /// <summary>
        /// 保存短语库到缓存
        /// </summary>
        public void SaveLibraryToCache(PhraseLibraryDef library)
        {
            try
            {
                string cacheDir = GetCacheDirectory();
                if (!Directory.Exists(cacheDir))
                {
                    Directory.CreateDirectory(cacheDir);
                }

                string filePath = Path.Combine(cacheDir, $"{library.defName}.xml");
                string xml = PhraseLibraryGenerator.Instance.ExportToXml(library);
                File.WriteAllText(filePath, xml, System.Text.Encoding.UTF8);

                Log.Message($"[PhraseManager] 保存短语库到: {filePath}");
            }
            catch (Exception ex)
            {
                Log.Error($"[PhraseManager] 保存短语库失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取短语库
        /// </summary>
        public PhraseLibraryDef GetLibrary(string personaDefName)
        {
            if (libraryCache.TryGetValue(personaDefName, out var lib))
            {
                return lib;
            }
            return null;
        }

        /// <summary>
        /// 检查是否有短语库
        /// </summary>
        public bool HasLibrary(string personaDefName)
        {
            return libraryCache.ContainsKey(personaDefName);
        }

        /// <summary>
        /// 设置人格的好感度（用于短语选择）
        /// </summary>
        public void SetAffinity(string personaDefName, float affinity)
        {
            affinityCache[personaDefName] = Mathf.Clamp(affinity, -100f, 100f);
        }

        /// <summary>
        /// 获取人格的好感度
        /// </summary>
        public float GetAffinity(string personaDefName)
        {
            if (affinityCache.TryGetValue(personaDefName, out var aff))
            {
                return aff;
            }
            return 0f; // 默认中立
        }

        /// <summary>
        /// 触发摸头反应短语
        /// </summary>
        public string TriggerHeadPat(string personaDefName)
        {
            return TriggerPhrase(personaDefName, PhraseCategory.HeadPat);
        }

        /// <summary>
        /// 触发戳身体反应短语
        /// </summary>
        public string TriggerBodyPoke(string personaDefName)
        {
            return TriggerPhrase(personaDefName, PhraseCategory.BodyPoke);
        }

        /// <summary>
        /// 触发问候短语
        /// </summary>
        public string TriggerGreeting(string personaDefName)
        {
            return TriggerPhrase(personaDefName, PhraseCategory.Greeting);
        }

        /// <summary>
        /// 触发事件反馈短语
        /// </summary>
        public string TriggerEventReaction(string personaDefName, bool isGoodEvent)
        {
            var category = isGoodEvent ? PhraseCategory.GoodEventReaction : PhraseCategory.BadEventReaction;
            return TriggerPhrase(personaDefName, category);
        }

        /// <summary>
        /// 触发战斗短语
        /// </summary>
        public string TriggerCombat(string personaDefName, bool isStart)
        {
            var category = isStart ? PhraseCategory.CombatStart : PhraseCategory.CombatVictory;
            return TriggerPhrase(personaDefName, category);
        }

        /// <summary>
        /// 触发闲聊短语
        /// </summary>
        public string TriggerIdle(string personaDefName)
        {
            return TriggerPhrase(personaDefName, PhraseCategory.Idle);
        }

        /// <summary>
        /// 触发告别短语
        /// </summary>
        public string TriggerFarewell(string personaDefName)
        {
            return TriggerPhrase(personaDefName, PhraseCategory.Farewell);
        }

        /// <summary>
        /// 通用短语触发
        /// </summary>
        public string TriggerPhrase(string personaDefName, PhraseCategory category)
        {
            var library = GetLibrary(personaDefName);
            if (library == null)
            {
                // 没有短语库，使用备用
                return GetFallbackPhrase(personaDefName, category);
            }

            float affinity = GetAffinity(personaDefName);
            string phrase = GetNonRepeatingPhrase(personaDefName, library, affinity, category);

            if (!string.IsNullOrEmpty(phrase))
            {
                OnPhraseTriggered?.Invoke(personaDefName, phrase);
            }

            return phrase;
        }

        /// <summary>
        /// 获取非重复短语
        /// </summary>
        private string GetNonRepeatingPhrase(string personaDefName, PhraseLibraryDef library, float affinity, PhraseCategory category)
        {
            var tier = library.GetTierFromAffinity(affinity);
            var tierPhrases = library.GetTierPhrases(tier);

            if (tierPhrases == null)
            {
                return "";
            }

            var phrases = tierPhrases.GetPhrasesByCategory(category);
            if (phrases == null || phrases.Count == 0)
            {
                // 回退到通用事件反馈
                phrases = tierPhrases.eventReactionPhrases;
            }

            if (phrases == null || phrases.Count == 0)
            {
                return "";
            }

            // 避免重复
            string cacheKey = $"{personaDefName}_{category}";
            if (!recentPhrases.TryGetValue(cacheKey, out var recent))
            {
                recent = new Queue<string>();
                recentPhrases[cacheKey] = recent;
            }

            // 过滤掉最近使用的
            var available = phrases.Where(p => !recent.Contains(p)).ToList();
            if (available.Count == 0)
            {
                // 所有都用过了，清空历史
                recent.Clear();
                available = phrases.ToList();
            }

            // 随机选择
            string selected = available.RandomElement();

            // 记录使用历史
            recent.Enqueue(selected);
            while (recent.Count > MAX_RECENT_PHRASES)
            {
                recent.Dequeue();
            }

            return selected;
        }

        /// <summary>
        /// 获取备用短语（当没有短语库时）
        /// </summary>
        private string GetFallbackPhrase(string personaDefName, PhraseCategory category)
        {
            // 尝试从 NarratorPersonaDef 的 phraseLibrary 获取
            var personaDef = DefDatabase<NarratorPersonaDef>.GetNamed(personaDefName, false);
            if (personaDef != null && personaDef.phraseLibrary != null)
            {
                string key = category switch
                {
                    PhraseCategory.HeadPat => "HeadPat",
                    PhraseCategory.BodyPoke => "BodyPoke",
                    PhraseCategory.Greeting => "Greeting",
                    _ => "Greeting"
                };

                string phrase = personaDef.GetRandomPhrase(key);
                if (!string.IsNullOrEmpty(phrase))
                {
                    return phrase;
                }
            }

            // 完全没有短语，返回默认
            bool isChinese = LanguageDatabase.activeLanguage?.folderName?.Contains("Chinese") ?? false;
            
            return category switch
            {
                PhraseCategory.HeadPat => "...",
                PhraseCategory.BodyPoke => "!",
                PhraseCategory.Greeting => isChinese ? "你好" : "Hello",
                PhraseCategory.Farewell => isChinese ? "再见" : "Goodbye",
                _ => "..."
            };
        }

        /// <summary>
        /// 获取缓存目录
        /// </summary>
        private string GetCacheDirectory()
        {
            return Path.Combine(GenFilePaths.ConfigFolderPath, "TheSecondSeat", "PhraseLibraries");
        }

        /// <summary>
        /// 清理缓存
        /// </summary>
        public void ClearCache()
        {
            libraryCache.Clear();
            recentPhrases.Clear();
            affinityCache.Clear();
        }

        /// <summary>
        /// 获取统计信息
        /// </summary>
        public string GetStatistics()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("=== PhraseManager Statistics ===");
            sb.AppendLine($"Loaded Libraries: {libraryCache.Count}");

            foreach (var kvp in libraryCache)
            {
                var lib = kvp.Value;
                sb.AppendLine($"  - {kvp.Key}: {lib.GetTotalPhraseCount()} phrases, complete={lib.isComplete}");
            }

            sb.AppendLine($"Affinity Cache Entries: {affinityCache.Count}");
            sb.AppendLine($"Recent Phrases Tracked: {recentPhrases.Sum(x => x.Value.Count)}");

            return sb.ToString();
        }
    }

}
