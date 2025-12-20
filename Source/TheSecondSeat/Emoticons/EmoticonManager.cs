using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace TheSecondSeat.Emoticons
{
    /// <summary>
    /// 表情包管理器 - 管理所有加载的表情包，提供查询和选择功能
    /// </summary>
    public class EmoticonManager
    {
        private static EmoticonManager instance;
        public static EmoticonManager Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new EmoticonManager();
                }
                return instance;
            }
        }

        // 所有表情包
        private List<EmoticonData> allEmoticons = new List<EmoticonData>();

        // 按标签索引
        private Dictionary<string, List<EmoticonData>> emoticonsByTag = new Dictionary<string, List<EmoticonData>>();

        // 是否已初始化
        private bool initialized = false;

        /// <summary>
        /// 初始化表情包管理器
        /// </summary>
        public void Initialize()
        {
            if (initialized)
            {
                Log.Warning("[EmoticonManager] 已经初始化过了");
                return;
            }

            Log.Message("[EmoticonManager] 开始加载表情包...");

            // 加载所有表情包
            allEmoticons = EmoticonLoader.LoadAllEmoticons();

            // 建立标签索引
            BuildTagIndex();

            initialized = true;

            Log.Message($"[EmoticonManager] 初始化完成，共 {allEmoticons.Count} 个表情包，{emoticonsByTag.Count} 个标签");
        }

        /// <summary>
        /// 建立标签索引
        /// </summary>
        private void BuildTagIndex()
        {
            emoticonsByTag.Clear();

            foreach (var emoticon in allEmoticons)
            {
                foreach (var tag in emoticon.tags)
                {
                    string normalizedTag = tag.ToLower().Trim();
                    
                    if (!emoticonsByTag.ContainsKey(normalizedTag))
                    {
                        emoticonsByTag[normalizedTag] = new List<EmoticonData>();
                    }

                    emoticonsByTag[normalizedTag].Add(emoticon);
                }
            }
        }

        /// <summary>
        /// 根据标签获取表情包（单个）
        /// </summary>
        public EmoticonData GetEmoticonByTag(string tag)
        {
            if (!initialized)
            {
                Initialize();
            }

            string normalizedTag = tag.ToLower().Trim();

            if (emoticonsByTag.ContainsKey(normalizedTag))
            {
                var candidates = emoticonsByTag[normalizedTag];
                if (candidates.Count > 0)
                {
                    // 随机选择一个
                    return candidates.RandomElement();
                }
            }

            return null;
        }

        /// <summary>
        /// 根据标签获取表情包（所有）
        /// </summary>
        public List<EmoticonData> GetAllEmoticonsByTag(string tag)
        {
            if (!initialized)
            {
                Initialize();
            }

            string normalizedTag = tag.ToLower().Trim();

            if (emoticonsByTag.ContainsKey(normalizedTag))
            {
                return emoticonsByTag[normalizedTag];
            }

            return new List<EmoticonData>();
        }

        /// <summary>
        /// 根据ID获取表情包
        /// </summary>
        public EmoticonData GetEmoticonById(string id)
        {
            if (!initialized)
            {
                Initialize();
            }

            return allEmoticons.FirstOrDefault(e => e.id.Equals(id, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// 获取所有表情包
        /// </summary>
        public List<EmoticonData> GetAllEmoticons()
        {
            if (!initialized)
            {
                Initialize();
            }

            return new List<EmoticonData>(allEmoticons);
        }

        /// <summary>
        /// 获取所有可用标签
        /// </summary>
        public List<string> GetAllTags()
        {
            if (!initialized)
            {
                Initialize();
            }

            return new List<string>(emoticonsByTag.Keys);
        }

        /// <summary>
        /// 获取表情包数量
        /// </summary>
        public int GetEmoticonCount()
        {
            if (!initialized)
            {
                Initialize();
            }

            return allEmoticons.Count;
        }

        /// <summary>
        /// 根据情感分析选择合适的表情包
        /// </summary>
        public EmoticonData SelectEmoticonBySentiment(string dialogue, float affinity, string mood)
        {
            if (!initialized)
            {
                Initialize();
            }

            // 如果没有表情包，返回null
            if (allEmoticons.Count == 0)
            {
                return null;
            }

            // 简单的情感分析
            var selectedTags = new List<string>();

            // 根据好感度
            if (affinity >= 60f)
            {
                selectedTags.AddRange(new[] { "happy", "joy", "love", "affection" });
            }
            else if (affinity >= 30f)
            {
                selectedTags.AddRange(new[] { "happy", "neutral", "calm" });
            }
            else if (affinity >= -10f)
            {
                selectedTags.AddRange(new[] { "neutral", "calm", "thinking" });
            }
            else if (affinity >= -50f)
            {
                selectedTags.AddRange(new[] { "frustrated", "disappointed", "neutral" });
            }
            else
            {
                selectedTags.AddRange(new[] { "angry", "frustrated", "smug" });
            }

            // 根据情绪
            if (mood != null)
            {
                string lowerMood = mood.ToLower();
                if (lowerMood.Contains("joy") || lowerMood.Contains("喜"))
                {
                    selectedTags.Add("happy");
                    selectedTags.Add("joy");
                }
                else if (lowerMood.Contains("angry") || lowerMood.Contains("怒"))
                {
                    selectedTags.Add("angry");
                    selectedTags.Add("frustrated");
                }
                else if (lowerMood.Contains("sad") || lowerMood.Contains("忧"))
                {
                    selectedTags.Add("sad");
                    selectedTags.Add("disappointed");
                }
            }

            // 根据对话内容关键词
            string lowerDialogue = dialogue.ToLower();
            
            // 开心相关
            if (lowerDialogue.Contains("哈哈") || lowerDialogue.Contains("haha") || 
                lowerDialogue.Contains("太好了") || lowerDialogue.Contains("great") ||
                lowerDialogue.Contains("棒") || lowerDialogue.Contains("excellent"))
            {
                selectedTags.Add("happy");
                selectedTags.Add("joy");
                selectedTags.Add("excited");
            }

            // 惊讶相关
            if (lowerDialogue.Contains("！！") || lowerDialogue.Contains("!") ||
                lowerDialogue.Contains("天哪") || lowerDialogue.Contains("oh my") ||
                lowerDialogue.Contains("什么") || lowerDialogue.Contains("what"))
            {
                selectedTags.Add("surprised");
                selectedTags.Add("shocked");
            }

            // 思考相关
            if (lowerDialogue.Contains("...") || lowerDialogue.Contains("嗯") ||
                lowerDialogue.Contains("hmm") || lowerDialogue.Contains("思考") ||
                lowerDialogue.Contains("think"))
            {
                selectedTags.Add("thinking");
                selectedTags.Add("confused");
            }

            // 悲伤相关
            if (lowerDialogue.Contains("唉") || lowerDialogue.Contains("sigh") ||
                lowerDialogue.Contains("遗憾") || lowerDialogue.Contains("sorry") ||
                lowerDialogue.Contains("可惜") || lowerDialogue.Contains("unfortunately"))
            {
                selectedTags.Add("sad");
                selectedTags.Add("disappointed");
            }

            // 尝试根据收集的标签找表情包
            foreach (var tag in selectedTags)
            {
                var emoticon = GetEmoticonByTag(tag);
                if (emoticon != null)
                {
                    Log.Message($"[EmoticonManager] 选择表情包: {emoticon.id} (标签: {tag})");
                    return emoticon;
                }
            }

            // 如果都没找到，随机返回一个
            if (allEmoticons.Count > 0)
            {
                var randomEmoticon = allEmoticons.RandomElement();
                Log.Message($"[EmoticonManager] 随机选择表情包: {randomEmoticon.id}");
                return randomEmoticon;
            }

            return null;
        }

        /// <summary>
        /// 生成表情包列表供LLM选择（用于System Prompt）
        /// </summary>
        public string GenerateEmoticonListForPrompt()
        {
            if (!initialized)
            {
                Initialize();
            }

            if (allEmoticons.Count == 0)
            {
                return "No emoticons available.";
            }

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Available emoticons (optional, choose ONE if appropriate):");

            // 按标签分组
            var groupedByTag = new Dictionary<string, List<string>>();

            foreach (var emoticon in allEmoticons)
            {
                foreach (var tag in emoticon.tags)
                {
                    if (!groupedByTag.ContainsKey(tag))
                    {
                        groupedByTag[tag] = new List<string>();
                    }
                    groupedByTag[tag].Add(emoticon.id);
                }
            }

            // 限制列表长度（避免Prompt过长）
            int maxTags = 10;
            int count = 0;

            foreach (var kvp in groupedByTag.OrderByDescending(x => x.Value.Count))
            {
                if (count >= maxTags) break;

                sb.AppendLine($"  - {kvp.Key}: {string.Join(", ", kvp.Value.Take(3))}");
                count++;
            }

            sb.AppendLine();
            sb.AppendLine("To use an emoticon, include in JSON: \"emoticon\": \"emoticon_id\"");
            sb.AppendLine("Only use emoticons when they naturally fit the emotion of your response.");

            return sb.ToString();
        }
    }
}
