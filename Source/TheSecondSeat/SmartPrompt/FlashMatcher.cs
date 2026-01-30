using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Verse;

namespace TheSecondSeat.SmartPrompt
{
    /// <summary>
    /// FlashMatcher - 极速感知系统
    /// 
    /// 架构层级: L1 感知层 (Perception)
    /// 核心职责: 实时捕捉意图与上下文
    /// 技术实现: Aho-Corasick (AC) 自动机 / Trie 树
    /// 
    /// 性能目标: 微秒级 (0.05ms)，支持 50,000+ 关键词零延迟
    /// 
    /// 工作流程:
    /// 1. 游戏启动时读取 XML 中的 expandedKeywords
    /// 2. 构建内存中的 AC 自动机
    /// 3. 运行时 Search(userInput) 
    /// 4. 返回命中的 Module defName 列表
    /// </summary>
    public class FlashMatcher
    {
        // ========== 单例 ==========
        
        private static FlashMatcher _instance;
        public static FlashMatcher Instance => _instance ??= new FlashMatcher();
        
        // ========== AC 自动机核心数据结构 ==========
        
        /// <summary>
        /// Trie 节点
        /// </summary>
        private class TrieNode
        {
            public Dictionary<char, TrieNode> Children = new Dictionary<char, TrieNode>();
            public TrieNode Fail;  // AC 自动机的失败指针
            public List<KeywordMatch> Outputs = new List<KeywordMatch>();  // 该节点对应的完整关键词
            public int Depth;
        }
        
        /// <summary>
        /// 关键词匹配结果
        /// </summary>
        private class KeywordMatch
        {
            public string Keyword;
            public string ModuleDefName;
            public string Intent;
            public float Weight;
        }
        
        // ========== 字段 ==========
        
        private TrieNode _root;
        private bool _isBuilt = false;
        private int _totalKeywords = 0;
        
        // 缓存：模块 defName -> PromptModuleDef
        private Dictionary<string, PromptModuleDef> _moduleCache = new Dictionary<string, PromptModuleDef>();
        
        // 统计
        private int _searchCount = 0;
        private double _totalSearchTimeMs = 0;
        
        // ========== 构造函数 ==========
        
        private FlashMatcher()
        {
            _root = new TrieNode { Depth = 0 };
        }
        
        // ========== 构建 AC 自动机 ==========
        
        /// <summary>
        /// ⭐ v3.1.0: 初始化并构建 AC 自动机
        /// 支持 PromptLoader.disabledPrompts 设置，禁用的模块不会被索引
        /// </summary>
        public void Build()
        {
            if (_isBuilt)
            {
                Log.Warning("[FlashMatcher] Already built. Call Rebuild() to reconstruct.");
                return;
            }
            
            var sw = Stopwatch.StartNew();
            
            // 重置
            _root = new TrieNode { Depth = 0 };
            _moduleCache.Clear();
            _totalKeywords = 0;
            int skippedModules = 0;
            
            // 遍历所有 PromptModuleDef，插入关键词
            var allModules = DefDatabase<PromptModuleDef>.AllDefsListForReading;
            
            foreach (var module in allModules)
            {
                // ⭐ v3.1.0: 检查模块是否被禁用
                if (TheSecondSeat.PersonaGeneration.PromptLoader.IsDisabled(module.defName))
                {
                    skippedModules++;
                    continue;
                }
                
                _moduleCache[module.defName] = module;
                
                // 为每个意图创建关键词映射
                foreach (var intent in module.triggerIntents)
                {
                    // 插入意图本身作为关键词
                    InsertKeyword(intent.ToLowerInvariant(), module.defName, intent, 1.0f);
                }
                
                // 插入扩展关键词
                foreach (var keyword in module.expandedKeywords)
                {
                    float weight = module.keywordWeights.TryGetValue(keyword, out float w) ? w : 1.0f;
                    
                    // 为该关键词关联到模块的所有意图
                    foreach (var intent in module.triggerIntents)
                    {
                        InsertKeyword(keyword.ToLowerInvariant(), module.defName, intent, weight);
                    }
                    
                    // 如果没有意图，直接关联到模块
                    if (module.triggerIntents.Count == 0)
                    {
                        InsertKeyword(keyword.ToLowerInvariant(), module.defName, "", weight);
                    }
                }
            }
            
            // 构建失败指针 (BFS)
            BuildFailLinks();
            
            _isBuilt = true;
            sw.Stop();
            
            int activeModules = allModules.Count - skippedModules;
            Log.Message($"[FlashMatcher] Built AC automaton: {_totalKeywords} keywords from {activeModules}/{allModules.Count} modules in {sw.ElapsedMilliseconds}ms (skipped {skippedModules} disabled)");
        }
        
        /// <summary>
        /// 重建 AC 自动机（用于热重载）
        /// </summary>
        public void Rebuild()
        {
            _isBuilt = false;
            Build();
        }
        
        /// <summary>
        /// 插入关键词到 Trie 树
        /// </summary>
        private void InsertKeyword(string keyword, string moduleDefName, string intent, float weight)
        {
            if (string.IsNullOrWhiteSpace(keyword)) return;
            
            var node = _root;
            foreach (char c in keyword)
            {
                if (!node.Children.TryGetValue(c, out var child))
                {
                    child = new TrieNode { Depth = node.Depth + 1 };
                    node.Children[c] = child;
                }
                node = child;
            }
            
            // 在叶子节点记录匹配信息
            node.Outputs.Add(new KeywordMatch
            {
                Keyword = keyword,
                ModuleDefName = moduleDefName,
                Intent = intent,
                Weight = weight
            });
            
            _totalKeywords++;
        }
        
        /// <summary>
        /// 构建 AC 自动机的失败指针 (Failure Links)
        /// </summary>
        private void BuildFailLinks()
        {
            var queue = new Queue<TrieNode>();
            
            // 第一层节点的失败指针指向根节点
            foreach (var child in _root.Children.Values)
            {
                child.Fail = _root;
                queue.Enqueue(child);
            }
            
            // BFS 构建其余节点的失败指针
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                
                foreach (var kvp in current.Children)
                {
                    char c = kvp.Key;
                    TrieNode child = kvp.Value;
                    queue.Enqueue(child);
                    
                    // 沿着父节点的失败链找匹配
                    var fail = current.Fail;
                    while (fail != null && !fail.Children.ContainsKey(c))
                    {
                        fail = fail.Fail;
                    }
                    
                    child.Fail = (fail != null && fail.Children.TryGetValue(c, out var failChild)) ? failChild : _root;
                    if (child.Fail == child) child.Fail = _root;  // 防止自环
                    
                    // 合并失败节点的输出
                    if (child.Fail.Outputs.Count > 0)
                    {
                        child.Outputs.AddRange(child.Fail.Outputs);
                    }
                }
            }
        }
        
        // ========== 搜索接口 ==========
        
        /// <summary>
        /// 在用户输入中搜索匹配的意图
        /// 
        /// 返回格式: List of (ModuleDefName, Intent, Score)
        /// Score 由命中的关键词权重累加得到
        /// </summary>
        /// <param name="userInput">用户输入文本</param>
        /// <returns>匹配结果列表，按分数降序排列</returns>
        public List<MatchResult> Search(string userInput)
        {
            if (!_isBuilt)
            {
                Build();
            }
            
            var sw = Stopwatch.StartNew();
            
            // 规范化输入
            string normalizedInput = userInput.ToLowerInvariant();
            
            // 结果聚合：(ModuleDefName, Intent) -> Score
            var scores = new Dictionary<(string module, string intent), float>();
            var hitKeywords = new Dictionary<(string module, string intent), List<string>>();
            
            // AC 自动机搜索
            var node = _root;
            foreach (char c in normalizedInput)
            {
                // 沿着失败链查找匹配
                while (node != _root && !node.Children.ContainsKey(c))
                {
                    node = node.Fail;
                }
                
                node = node.Children.TryGetValue(c, out var nextNode) ? nextNode : _root;
                
                // 收集当前节点及其失败链上的所有输出
                var temp = node;
                while (temp != _root)
                {
                    foreach (var match in temp.Outputs)
                    {
                        var key = (match.ModuleDefName, match.Intent);
                        
                        if (!scores.ContainsKey(key))
                        {
                            scores[key] = 0;
                            hitKeywords[key] = new List<string>();
                        }
                        
                        scores[key] += match.Weight;
                        if (!hitKeywords[key].Contains(match.Keyword))
                        {
                            hitKeywords[key].Add(match.Keyword);
                        }
                    }
                    temp = temp.Fail;
                }
            }
            
            // 转换为结果列表
            var results = scores.Select(kvp => new MatchResult
            {
                ModuleDefName = kvp.Key.module,
                Intent = kvp.Key.intent,
                Score = kvp.Value,
                HitKeywords = hitKeywords[kvp.Key]
            })
            .OrderByDescending(r => r.Score)
            .ToList();
            
            sw.Stop();
            _searchCount++;
            _totalSearchTimeMs += sw.Elapsed.TotalMilliseconds;
            
            return results;
        }
        
        /// <summary>
        /// 快速检查用户输入是否包含特定意图
        /// </summary>
        /// <param name="userInput">用户输入</param>
        /// <param name="intent">意图标签</param>
        /// <returns>是否匹配</returns>
        public bool HasIntent(string userInput, string intent)
        {
            var results = Search(userInput);
            return results.Any(r => r.Intent.Equals(intent, StringComparison.OrdinalIgnoreCase));
        }
        
        /// <summary>
        /// 获取用户输入匹配的所有模块 defName
        /// </summary>
        /// <param name="userInput">用户输入</param>
        /// <param name="minScore">最低分数阈值</param>
        /// <returns>模块 defName 列表</returns>
        public List<string> GetMatchedModules(string userInput, float minScore = 0.5f)
        {
            var results = Search(userInput);
            return results
                .Where(r => r.Score >= minScore)
                .Select(r => r.ModuleDefName)
                .Distinct()
                .ToList();
        }
        
        /// <summary>
        /// 获取用户输入匹配的所有意图
        /// </summary>
        /// <param name="userInput">用户输入</param>
        /// <param name="minScore">最低分数阈值</param>
        /// <returns>意图标签列表</returns>
        public List<string> GetMatchedIntents(string userInput, float minScore = 0.5f)
        {
            var results = Search(userInput);
            return results
                .Where(r => r.Score >= minScore && !string.IsNullOrEmpty(r.Intent))
                .Select(r => r.Intent)
                .Distinct()
                .ToList();
        }
        
        // ========== 工具方法 ==========
        
        /// <summary>
        /// 获取模块定义（带缓存）
        /// </summary>
        public PromptModuleDef GetModule(string defName)
        {
            if (_moduleCache.TryGetValue(defName, out var module))
            {
                return module;
            }
            
            module = DefDatabase<PromptModuleDef>.GetNamedSilentFail(defName);
            if (module != null)
            {
                _moduleCache[defName] = module;
            }
            return module;
        }
        
        /// <summary>
        /// 获取统计信息
        /// </summary>
        public string GetStats()
        {
            double avgTime = _searchCount > 0 ? _totalSearchTimeMs / _searchCount : 0;
            return $"[FlashMatcher] Keywords: {_totalKeywords}, Modules: {_moduleCache.Count}, " +
                   $"Searches: {_searchCount}, Avg Time: {avgTime:F3}ms";
        }
        
        /// <summary>
        /// 清除统计数据
        /// </summary>
        public void ClearStats()
        {
            _searchCount = 0;
            _totalSearchTimeMs = 0;
        }
        
        /// <summary>
        /// 获取所有已加载的模块列表（调试用）
        /// </summary>
        public List<string> GetAllModuleNames()
        {
            return _moduleCache.Keys.ToList();
        }
        
        // ========== v3.1.1: 聊天/工具意图检测 ==========
        
        /// <summary>
        /// 聊天关键词列表（匹配这些时不需要工具列表）
        /// </summary>
        private static readonly HashSet<string> ChatKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            // 问候
            "你好", "早", "晚安", "早安", "嗨", "喂", "在吗", "忙吗",
            "hi", "hello", "hey", "morning", "night", "good morning", "good night",
            
            // 情感表达
            "喜欢", "爱", "讨厌", "开心", "难过", "累", "烦", "高兴", "生气", "伤心",
            "love", "like", "hate", "happy", "sad", "tired", "bored", "angry",
            
            // 闲聊
            "聊聊", "说说", "讲讲", "谈谈", "怎么看", "你觉得", "你认为", "觉得怎么样",
            "chat", "talk", "think", "feel", "opinion", "what do you think",
            
            // 询问对方
            "你呢", "你怎么样", "怎么了", "发生什么", "怎样",
            "how are you", "are you there", "what happened", "how about you",
            
            // 赞美/评价
            "好可爱", "真棒", "太厉害", "漂亮", "帅", "厉害", "聪明", "真好",
            "cute", "great", "awesome", "beautiful", "amazing", "smart", "nice",
            
            // 日常
            "吃饭", "睡觉", "休息", "无聊", "今天", "明天", "昨天", "天气",
            "eat", "sleep", "rest", "today", "tomorrow", "yesterday", "weather",
            
            // 请求陪伴
            "陪我", "跟我", "和我", "一起", "陪陪",
            "with me", "together", "stay with me",
            
            // 感谢道歉
            "谢谢", "感谢", "抱歉", "对不起", "不好意思",
            "thank", "thanks", "sorry", "apologize"
        };
        
        /// <summary>
        /// 工具触发关键词列表（匹配这些时需要工具列表）
        /// </summary>
        private static readonly HashSet<string> ToolKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            // 动作指令
            "收割", "建造", "攻击", "撤退", "征召", "取消征召", "装备", "修复",
            "harvest", "build", "attack", "retreat", "draft", "undraft", "equip", "repair",
            
            // 查询指令
            "查一下", "找找", "位置", "在哪", "哪里", "查询", "搜索",
            "where", "find", "locate", "search", "scan",
            
            // 事件相关
            "触发", "事件", "袭击", "入侵", "派遣",
            "trigger", "event", "raid", "incident", "spawn",
            
            // 降临系统
            "降临", "化身", "回来", "升天", "实体",
            "descent", "ascend", "descend", "manifest", "physical",
            
            // 服装系统
            "换衣服", "换装", "穿", "脱", "服装",
            "outfit", "wear", "dress", "change clothes",
            
            // 殖民地管理
            "殖民者", "工作", "优先级", "策略", "政策",
            "colonist", "work", "priority", "policy"
        };
        
        /// <summary>
        /// ⭐ v3.1.1: 检测用户输入是否需要工具列表
        ///
        /// 策略：
        /// 1. 如果匹配到任何工具关键词 → 需要工具
        /// 2. 如果匹配到任何技能模块 (Skill) → 需要工具
        /// 3. 如果只匹配到聊天关键词 → 不需要工具
        /// 4. 默认需要工具（保守策略）
        /// </summary>
        /// <param name="userInput">用户输入</param>
        /// <returns>是否需要加载工具列表</returns>
        public bool NeedsToolBox(string userInput)
        {
            if (string.IsNullOrWhiteSpace(userInput))
            {
                return false; // 空输入不需要工具
            }
            
            string normalized = userInput.ToLowerInvariant();
            
            // 1. 检查是否包含工具触发关键词
            foreach (var keyword in ToolKeywords)
            {
                if (normalized.Contains(keyword.ToLowerInvariant()))
                {
                    return true; // 包含工具关键词，需要工具列表
                }
            }
            
            // 2. 检查是否匹配到任何技能模块
            var matchedModules = GetMatchedModules(userInput, 0.5f);
            foreach (var moduleDefName in matchedModules)
            {
                if (_moduleCache.TryGetValue(moduleDefName, out var module))
                {
                    if (module.moduleType == ModuleType.Skill)
                    {
                        return true; // 匹配到技能模块，需要工具列表
                    }
                }
            }
            
            // 3. 检查是否只是聊天
            bool isChatOnly = false;
            foreach (var keyword in ChatKeywords)
            {
                if (normalized.Contains(keyword.ToLowerInvariant()))
                {
                    isChatOnly = true;
                    break;
                }
            }
            
            if (isChatOnly && matchedModules.Count == 0)
            {
                return false; // 纯聊天，不需要工具列表
            }
            
            // 4. 短输入（少于 10 个字符）且无匹配，视为聊天
            if (userInput.Length < 10 && matchedModules.Count == 0)
            {
                return false;
            }
            
            // 5. 默认需要工具（保守策略）
            return true;
        }
    }
    
    /// <summary>
    /// 匹配结果
    /// </summary>
    public class MatchResult
    {
        /// <summary>匹配到的模块 defName</summary>
        public string ModuleDefName { get; set; }
        
        /// <summary>匹配到的意图标签</summary>
        public string Intent { get; set; }
        
        /// <summary>匹配分数（关键词权重累加）</summary>
        public float Score { get; set; }
        
        /// <summary>命中的关键词列表</summary>
        public List<string> HitKeywords { get; set; } = new List<string>();
        
        public override string ToString()
        {
            return $"[{ModuleDefName}] Intent={Intent}, Score={Score:F2}, Hits=[{string.Join(", ", HitKeywords)}]";
        }
    }
}
