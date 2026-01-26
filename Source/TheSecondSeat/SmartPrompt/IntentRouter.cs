using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace TheSecondSeat.SmartPrompt
{
    /// <summary>
    /// IntentRouter - 意图路由层
    /// 
    /// 架构层级: L2 路由层 (Router)
    /// 核心职责: 决定加载哪些模块
    /// 技术实现: 逻辑判断 + 依赖解析
    /// 
    /// 工作流程:
    /// 1. 接收 FlashMatcher 的匹配结果
    /// 2. 根据环境条件过滤模块
    /// 3. 解析依赖链
    /// 4. 处理互斥关系
    /// 5. 返回最终的模块加载列表
    /// </summary>
    public class IntentRouter
    {
        // ========== 单例 ==========
        
        private static IntentRouter _instance;
        public static IntentRouter Instance => _instance ??= new IntentRouter();
        
        // ========== 配置 ==========
        
        /// <summary>
        /// 默认最小分数阈值
        /// </summary>
        public float DefaultMinScore { get; set; } = 0.5f;
        
        /// <summary>
        /// 最大加载模块数量（防止 Prompt 过长）
        /// </summary>
        public int MaxModules { get; set; } = 10;
        
        // ========== 构造函数 ==========
        
        private IntentRouter() { }
        
        // ========== 核心路由方法 ==========
        
        /// <summary>
        /// 根据用户输入路由到需要加载的模块
        /// 
        /// 完整流程:
        /// 1. FlashMatcher 匹配意图
        /// 2. 过滤环境条件
        /// 3. 添加 AlwaysActive 模块
        /// 4. 解析依赖链
        /// 5. 处理互斥关系
        /// 6. 按优先级排序
        /// </summary>
        /// <param name="userInput">用户输入文本</param>
        /// <param name="context">当前游戏上下文（可选）</param>
        /// <returns>路由结果，包含模块列表和调试信息</returns>
        public RouteResult Route(string userInput, GameContext context = null)
        {
            var result = new RouteResult();
            var sw = System.Diagnostics.Stopwatch.StartNew();
            
            try
            {
                // 1. 获取匹配结果
                var matchResults = FlashMatcher.Instance.Search(userInput);
                result.MatchResults = matchResults;
                
                // 2. 获取当前环境状态
                bool isInCombat = context?.IsInCombat ?? DetectCombatState();
                result.IsInCombat = isInCombat;
                
                // 3. 收集候选模块
                var candidateModules = new HashSet<string>();
                var selectedIntents = new HashSet<string>();
                
                // 3.1 添加匹配到的模块
                foreach (var match in matchResults.Where(m => m.Score >= DefaultMinScore))
                {
                    var module = FlashMatcher.Instance.GetModule(match.ModuleDefName);
                    if (module != null && module.CanActivate(isInCombat))
                    {
                        candidateModules.Add(match.ModuleDefName);
                        if (!string.IsNullOrEmpty(match.Intent))
                        {
                            selectedIntents.Add(match.Intent);
                        }
                    }
                }
                
                // 3.2 添加 AlwaysActive 模块
                var allModules = DefDatabase<PromptModuleDef>.AllDefsListForReading;
                foreach (var module in allModules.Where(m => m.alwaysActive))
                {
                    candidateModules.Add(module.defName);
                }
                
                result.SelectedIntents = selectedIntents.ToList();
                
                // 4. 解析依赖链
                var finalModules = ResolveDependencies(candidateModules, isInCombat);
                
                // 5. 处理互斥关系
                finalModules = ResolveExclusions(finalModules);
                
                // 6. 按优先级排序并限制数量
                var sortedModules = finalModules
                    .Select(defName => FlashMatcher.Instance.GetModule(defName))
                    .Where(m => m != null)
                    .OrderByDescending(m => m.priority)
                    .ThenBy(m => m.moduleType)
                    .Take(MaxModules)
                    .ToList();
                
                result.Modules = sortedModules;
                result.Success = true;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Error = ex.Message;
                Log.Error($"[IntentRouter] Route failed: {ex}");
            }
            
            sw.Stop();
            result.RouteTimeMs = sw.Elapsed.TotalMilliseconds;
            
            return result;
        }
        
        /// <summary>
        /// 快速路由（仅返回模块 defName 列表，不返回详细信息）
        /// </summary>
        public List<string> QuickRoute(string userInput)
        {
            var result = Route(userInput);
            return result.Modules.Select(m => m.defName).ToList();
        }
        
        // ========== 依赖解析 ==========
        
        /// <summary>
        /// 解析依赖链，递归添加所有依赖模块
        /// </summary>
        private HashSet<string> ResolveDependencies(HashSet<string> initialModules, bool isInCombat)
        {
            var resolved = new HashSet<string>();
            var pending = new Queue<string>(initialModules);
            var visited = new HashSet<string>();
            
            while (pending.Count > 0)
            {
                var defName = pending.Dequeue();
                if (visited.Contains(defName)) continue;
                visited.Add(defName);
                
                var module = FlashMatcher.Instance.GetModule(defName);
                if (module == null) continue;
                
                // 检查环境条件
                if (!module.CanActivate(isInCombat) && !module.alwaysActive)
                {
                    continue;
                }
                
                resolved.Add(defName);
                
                // 添加依赖
                foreach (var dep in module.dependencies)
                {
                    if (!visited.Contains(dep))
                    {
                        pending.Enqueue(dep);
                    }
                }
            }
            
            return resolved;
        }
        
        /// <summary>
        /// 处理互斥关系
        /// 当存在互斥模块时，保留优先级较高的模块
        /// </summary>
        private HashSet<string> ResolveExclusions(HashSet<string> modules)
        {
            var result = new HashSet<string>(modules);
            var toRemove = new HashSet<string>();
            
            foreach (var defName in modules)
            {
                if (toRemove.Contains(defName)) continue;
                
                var module = FlashMatcher.Instance.GetModule(defName);
                if (module == null) continue;
                
                foreach (var exclusion in module.exclusiveWith)
                {
                    if (modules.Contains(exclusion) && !toRemove.Contains(exclusion))
                    {
                        var other = FlashMatcher.Instance.GetModule(exclusion);
                        if (other == null) continue;
                        
                        // 保留优先级较高的模块
                        if (module.priority >= other.priority)
                        {
                            toRemove.Add(exclusion);
                        }
                        else
                        {
                            toRemove.Add(defName);
                            break;
                        }
                    }
                }
            }
            
            result.ExceptWith(toRemove);
            return result;
        }
        
        // ========== 环境检测 ==========
        
        /// <summary>
        /// 检测当前是否处于战斗状态
        /// </summary>
        private bool DetectCombatState()
        {
            try
            {
                var map = Find.CurrentMap;
                if (map == null) return false;
                
                // 检查是否有敌对目标
                return map.attackTargetsCache.TargetsHostileToColony.Any();
            }
            catch
            {
                return false;
            }
        }
        
        // ========== 高级路由方法 ==========
        
        /// <summary>
        /// 根据特定意图路由
        /// </summary>
        /// <param name="intents">意图标签列表</param>
        /// <param name="isInCombat">是否处于战斗状态</param>
        public RouteResult RouteByIntents(List<string> intents, bool isInCombat = false)
        {
            var result = new RouteResult
            {
                SelectedIntents = intents,
                IsInCombat = isInCombat
            };
            
            var sw = System.Diagnostics.Stopwatch.StartNew();
            
            try
            {
                var candidateModules = new HashSet<string>();
                
                // 查找包含指定意图的模块
                var allModules = DefDatabase<PromptModuleDef>.AllDefsListForReading;
                foreach (var module in allModules)
                {
                    if (module.alwaysActive)
                    {
                        candidateModules.Add(module.defName);
                        continue;
                    }
                    
                    if (module.triggerIntents.Any(i => intents.Contains(i, StringComparer.OrdinalIgnoreCase)))
                    {
                        if (module.CanActivate(isInCombat))
                        {
                            candidateModules.Add(module.defName);
                        }
                    }
                }
                
                // 解析依赖
                var finalModules = ResolveDependencies(candidateModules, isInCombat);
                
                // 处理互斥
                finalModules = ResolveExclusions(finalModules);
                
                // 排序
                result.Modules = finalModules
                    .Select(defName => FlashMatcher.Instance.GetModule(defName))
                    .Where(m => m != null)
                    .OrderByDescending(m => m.priority)
                    .Take(MaxModules)
                    .ToList();
                
                result.Success = true;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Error = ex.Message;
            }
            
            sw.Stop();
            result.RouteTimeMs = sw.Elapsed.TotalMilliseconds;
            
            return result;
        }
        
        /// <summary>
        /// 强制加载指定模块（忽略环境条件，但仍解析依赖）
        /// </summary>
        public RouteResult ForceRoute(List<string> moduleDefNames)
        {
            var result = new RouteResult();
            var sw = System.Diagnostics.Stopwatch.StartNew();
            
            try
            {
                var modules = new HashSet<string>(moduleDefNames);
                
                // 添加 AlwaysActive 模块
                var allModules = DefDatabase<PromptModuleDef>.AllDefsListForReading;
                foreach (var module in allModules.Where(m => m.alwaysActive))
                {
                    modules.Add(module.defName);
                }
                
                // 解析依赖（忽略环境条件）
                var resolved = new HashSet<string>();
                var pending = new Queue<string>(modules);
                var visited = new HashSet<string>();
                
                while (pending.Count > 0)
                {
                    var defName = pending.Dequeue();
                    if (visited.Contains(defName)) continue;
                    visited.Add(defName);
                    
                    var module = FlashMatcher.Instance.GetModule(defName);
                    if (module == null) continue;
                    
                    resolved.Add(defName);
                    
                    foreach (var dep in module.dependencies)
                    {
                        if (!visited.Contains(dep))
                        {
                            pending.Enqueue(dep);
                        }
                    }
                }
                
                // 处理互斥
                resolved = ResolveExclusions(resolved);
                
                // 排序
                result.Modules = resolved
                    .Select(defName => FlashMatcher.Instance.GetModule(defName))
                    .Where(m => m != null)
                    .OrderByDescending(m => m.priority)
                    .ToList();
                
                result.Success = true;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Error = ex.Message;
            }
            
            sw.Stop();
            result.RouteTimeMs = sw.Elapsed.TotalMilliseconds;
            
            return result;
        }
    }
    
    /// <summary>
    /// 路由结果
    /// </summary>
    public class RouteResult
    {
        /// <summary>是否成功</summary>
        public bool Success { get; set; }
        
        /// <summary>错误信息</summary>
        public string Error { get; set; }
        
        /// <summary>路由耗时（毫秒）</summary>
        public double RouteTimeMs { get; set; }
        
        /// <summary>最终选中的模块列表（按优先级排序）</summary>
        public List<PromptModuleDef> Modules { get; set; } = new List<PromptModuleDef>();
        
        /// <summary>识别到的意图列表</summary>
        public List<string> SelectedIntents { get; set; } = new List<string>();
        
        /// <summary>当前是否处于战斗状态</summary>
        public bool IsInCombat { get; set; }
        
        /// <summary>FlashMatcher 的原始匹配结果</summary>
        public List<MatchResult> MatchResults { get; set; } = new List<MatchResult>();
        
        /// <summary>
        /// 获取调试信息
        /// </summary>
        public string GetDebugInfo()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"=== Route Result ===");
            sb.AppendLine($"Success: {Success}");
            sb.AppendLine($"Time: {RouteTimeMs:F3}ms");
            sb.AppendLine($"Combat: {IsInCombat}");
            sb.AppendLine($"Intents: [{string.Join(", ", SelectedIntents)}]");
            sb.AppendLine($"Modules ({Modules.Count}): [{string.Join(", ", Modules.Select(m => m.defName))}]");
            
            if (MatchResults.Count > 0)
            {
                sb.AppendLine($"\nTop Matches:");
                foreach (var match in MatchResults.Take(5))
                {
                    sb.AppendLine($"  {match}");
                }
            }
            
            if (!string.IsNullOrEmpty(Error))
            {
                sb.AppendLine($"\nError: {Error}");
            }
            
            return sb.ToString();
        }
    }
    
    /// <summary>
    /// 游戏上下文（用于路由决策）
    /// </summary>
    public class GameContext
    {
        /// <summary>是否处于战斗状态</summary>
        public bool IsInCombat { get; set; }
        
        /// <summary>当前殖民者数量</summary>
        public int ColonistCount { get; set; }
        
        /// <summary>当前财富值</summary>
        public float Wealth { get; set; }
        
        /// <summary>当前季节</summary>
        public string Season { get; set; }
        
        /// <summary>自定义标签（用于扩展）</summary>
        public HashSet<string> Tags { get; set; } = new HashSet<string>();
        
        /// <summary>
        /// 从当前游戏状态创建上下文
        /// </summary>
        public static GameContext FromCurrentGame()
        {
            var ctx = new GameContext();
            
            try
            {
                var map = Find.CurrentMap;
                if (map != null)
                {
                    ctx.IsInCombat = map.attackTargetsCache.TargetsHostileToColony.Any();
                    ctx.ColonistCount = map.mapPawns.FreeColonistsCount;
                    ctx.Wealth = map.wealthWatcher.WealthTotal;
                    ctx.Season = GenDate.Season(Find.TickManager.TicksAbs, Find.WorldGrid.LongLatOf(map.Tile)).Label();
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[GameContext] Failed to create context: {ex.Message}");
            }
            
            return ctx;
        }
    }
}
