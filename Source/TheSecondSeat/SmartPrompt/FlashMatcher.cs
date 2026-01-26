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
        /// 初始化并构建 AC 自动机
        /// 应在游戏启动时调用（如 ModContentPack.PostLoadContent）
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
            
            // 遍历所有 PromptModuleDef，插入关键词
            var allModules = DefDatabase<PromptModuleDef>.AllDefsListForReading;
            
            foreach (var module in allModules)
            {
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
            
            Log.Message($"[FlashMatcher] Built AC automaton: {_totalKeywords} keywords from {allModules.Count} modules in {sw.ElapsedMilliseconds}ms");
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
