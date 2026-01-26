using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;
using TheSecondSeat.PersonaGeneration.Scriban;

namespace TheSecondSeat.SmartPrompt
{
    /// <summary>
    /// SmartPromptBuilder - 智能提示词构建器
    /// 
    /// 架构层级: L3 构建层 (Builder)
    /// 核心职责: 渲染最终 Prompt
    /// 技术实现: SmartPromptBuilder + Scriban
    /// 
    /// 工作流程:
    /// 1. 接收 IntentRouter 的模块列表
    /// 2. 按优先级和类型组织模块
    /// 3. 使用 Scriban 渲染模板化模块
    /// 4. 组装最终的 System Prompt
    /// 
    /// 核心优势:
    /// - Prompt 永远只包含当前任务所需的最小集
    /// - Token 节省率预计 50% 以上
    /// - 支持 Scriban 动态模板
    /// </summary>
    public class SmartPromptBuilder
    {
        // ========== 单例 ==========
        
        private static SmartPromptBuilder _instance;
        public static SmartPromptBuilder Instance => _instance ??= new SmartPromptBuilder();
        
        // ========== 配置 ==========
        
        /// <summary>
        /// 模块之间的分隔符
        /// </summary>
        public string ModuleSeparator { get; set; } = "\n\n---\n\n";
        
        /// <summary>
        /// 是否在输出中包含模块标题
        /// </summary>
        public bool IncludeModuleHeaders { get; set; } = true;
        
        /// <summary>
        /// 最大 Prompt 长度（字符数，0 = 无限制）
        /// </summary>
        public int MaxPromptLength { get; set; } = 0;
        
        /// <summary>
        /// 是否启用调试模式（输出更多信息）
        /// </summary>
        public bool DebugMode { get; set; } = false;
        
        // ========== 统计 ==========
        
        private int _buildCount = 0;
        private int _totalModulesUsed = 0;
        private double _totalBuildTimeMs = 0;
        
        // ========== 构造函数 ==========
        
        private SmartPromptBuilder() { }
        
        // ========== 核心构建方法 ==========
        
        /// <summary>
        /// 根据用户输入构建智能 Prompt
        /// 
        /// 完整流程:
        /// 1. 调用 IntentRouter 路由模块
        /// 2. 渲染每个模块的内容
        /// 3. 按类型分组组装
        /// 4. 返回最终 Prompt
        /// </summary>
        /// <param name="userInput">用户输入文本</param>
        /// <param name="context">Scriban 渲染上下文（可选）</param>
        /// <returns>构建结果</returns>
        public BuildResult Build(string userInput, PromptContext context = null)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var result = new BuildResult();
            
            try
            {
                // 1. 路由模块
                var routeResult = IntentRouter.Instance.Route(userInput);
                result.RouteResult = routeResult;
                
                if (!routeResult.Success)
                {
                    result.Success = false;
                    result.Error = routeResult.Error;
                    return result;
                }
                
                // 2. 构建 Prompt
                result.Prompt = BuildFromModules(routeResult.Modules, context);
                result.Success = true;
                
                // 统计
                _buildCount++;
                _totalModulesUsed += routeResult.Modules.Count;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Error = ex.Message;
                Log.Error($"[SmartPromptBuilder] Build failed: {ex}");
            }
            
            sw.Stop();
            result.BuildTimeMs = sw.Elapsed.TotalMilliseconds;
            _totalBuildTimeMs += result.BuildTimeMs;
            
            return result;
        }
        
        /// <summary>
        /// 从指定模块列表构建 Prompt
        /// </summary>
        /// <param name="modules">模块列表</param>
        /// <param name="context">Scriban 渲染上下文（可选）</param>
        /// <returns>组装后的 Prompt 字符串</returns>
        public string BuildFromModules(List<PromptModuleDef> modules, PromptContext context = null)
        {
            if (modules == null || modules.Count == 0)
            {
                return "";
            }
            
            var sb = new StringBuilder();
            
            // 按模块类型分组
            var grouped = modules
                .OrderByDescending(m => m.priority)
                .GroupBy(m => m.moduleType)
                .OrderBy(g => GetTypeOrder(g.Key));
            
            foreach (var group in grouped)
            {
                // 可选：添加类型标题
                if (IncludeModuleHeaders && group.Count() > 0)
                {
                    sb.AppendLine($"## {GetTypeName(group.Key)}");
                    sb.AppendLine();
                }
                
                foreach (var module in group)
                {
                    string content = RenderModule(module, context);
                    
                    if (!string.IsNullOrWhiteSpace(content))
                    {
                        if (DebugMode)
                        {
                            sb.AppendLine($"<!-- Module: {module.defName} (Priority: {module.priority}) -->");
                        }
                        
                        sb.Append(content);
                        
                        if (!content.EndsWith("\n"))
                        {
                            sb.AppendLine();
                        }
                        sb.AppendLine();
                    }
                }
            }
            
            string prompt = sb.ToString().TrimEnd();
            
            // 长度限制
            if (MaxPromptLength > 0 && prompt.Length > MaxPromptLength)
            {
                prompt = prompt.Substring(0, MaxPromptLength);
                Log.Warning($"[SmartPromptBuilder] Prompt truncated to {MaxPromptLength} characters");
            }
            
            return prompt;
        }
        
        /// <summary>
        /// 渲染单个模块
        /// </summary>
        private string RenderModule(PromptModuleDef module, PromptContext context)
        {
            string rawContent = module.GetContent();
            
            if (string.IsNullOrEmpty(rawContent))
            {
                return "";
            }
            
            // 如果启用 Scriban 且提供了上下文，则渲染模板
            if (module.useScriban && context != null)
            {
                try
                {
                    return PromptRenderer.Render(module.defName, context);
                }
                catch (Exception ex)
                {
                    Log.Warning($"[SmartPromptBuilder] Scriban render failed for {module.defName}: {ex.Message}");
                    return rawContent;
                }
            }
            
            return rawContent;
        }
        
        // ========== 快捷方法 ==========
        
        /// <summary>
        /// 根据意图列表构建 Prompt
        /// </summary>
        public BuildResult BuildByIntents(List<string> intents, PromptContext context = null)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var result = new BuildResult();
            
            try
            {
                var routeResult = IntentRouter.Instance.RouteByIntents(intents);
                result.RouteResult = routeResult;
                
                if (!routeResult.Success)
                {
                    result.Success = false;
                    result.Error = routeResult.Error;
                    return result;
                }
                
                result.Prompt = BuildFromModules(routeResult.Modules, context);
                result.Success = true;
                
                _buildCount++;
                _totalModulesUsed += routeResult.Modules.Count;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Error = ex.Message;
            }
            
            sw.Stop();
            result.BuildTimeMs = sw.Elapsed.TotalMilliseconds;
            _totalBuildTimeMs += result.BuildTimeMs;
            
            return result;
        }
        
        /// <summary>
        /// 强制使用指定模块构建 Prompt
        /// </summary>
        public BuildResult BuildForced(List<string> moduleDefNames, PromptContext context = null)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var result = new BuildResult();
            
            try
            {
                var routeResult = IntentRouter.Instance.ForceRoute(moduleDefNames);
                result.RouteResult = routeResult;
                
                if (!routeResult.Success)
                {
                    result.Success = false;
                    result.Error = routeResult.Error;
                    return result;
                }
                
                result.Prompt = BuildFromModules(routeResult.Modules, context);
                result.Success = true;
                
                _buildCount++;
                _totalModulesUsed += routeResult.Modules.Count;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Error = ex.Message;
            }
            
            sw.Stop();
            result.BuildTimeMs = sw.Elapsed.TotalMilliseconds;
            _totalBuildTimeMs += result.BuildTimeMs;
            
            return result;
        }
        
        /// <summary>
        /// 构建仅包含核心模块的 Prompt
        /// </summary>
        public string BuildCoreOnly(PromptContext context = null)
        {
            var coreModules = DefDatabase<PromptModuleDef>.AllDefsListForReading
                .Where(m => m.alwaysActive || m.moduleType == ModuleType.Core)
                .OrderByDescending(m => m.priority)
                .ToList();
            
            return BuildFromModules(coreModules, context);
        }
        
        // ========== 工具方法 ==========
        
        /// <summary>
        /// 获取模块类型的排序优先级
        /// </summary>
        private int GetTypeOrder(ModuleType type)
        {
            return type switch
            {
                ModuleType.Core => 0,
                ModuleType.Format => 1,
                ModuleType.Context => 2,
                ModuleType.Skill => 3,
                ModuleType.Memory => 4,
                ModuleType.Extension => 5,
                _ => 99
            };
        }
        
        /// <summary>
        /// 获取模块类型的显示名称
        /// </summary>
        private string GetTypeName(ModuleType type)
        {
            return type switch
            {
                ModuleType.Core => "Core Identity",
                ModuleType.Format => "Output Format",
                ModuleType.Context => "Current Context",
                ModuleType.Skill => "Skills & Knowledge",
                ModuleType.Memory => "Memories",
                ModuleType.Extension => "Extensions",
                _ => "Other"
            };
        }
        
        /// <summary>
        /// 获取统计信息
        /// </summary>
        public string GetStats()
        {
            double avgTime = _buildCount > 0 ? _totalBuildTimeMs / _buildCount : 0;
            double avgModules = _buildCount > 0 ? (double)_totalModulesUsed / _buildCount : 0;
            
            return $"[SmartPromptBuilder] Builds: {_buildCount}, " +
                   $"Avg Time: {avgTime:F2}ms, " +
                   $"Avg Modules: {avgModules:F1}";
        }
        
        /// <summary>
        /// 重置统计
        /// </summary>
        public void ClearStats()
        {
            _buildCount = 0;
            _totalModulesUsed = 0;
            _totalBuildTimeMs = 0;
        }
        
        /// <summary>
        /// 估算 Prompt 的 Token 数量（粗略估计）
        /// </summary>
        public int EstimateTokens(string prompt)
        {
            if (string.IsNullOrEmpty(prompt)) return 0;
            
            // 粗略估计：英文约 4 字符/token，中文约 1.5 字符/token
            // 这里使用 3 字符/token 作为折中
            return prompt.Length / 3;
        }
    }
    
    /// <summary>
    /// 构建结果
    /// </summary>
    public class BuildResult
    {
        /// <summary>是否成功</summary>
        public bool Success { get; set; }
        
        /// <summary>错误信息</summary>
        public string Error { get; set; }
        
        /// <summary>构建耗时（毫秒）</summary>
        public double BuildTimeMs { get; set; }
        
        /// <summary>最终生成的 Prompt</summary>
        public string Prompt { get; set; }
        
        /// <summary>路由结果（包含模块列表等详细信息）</summary>
        public RouteResult RouteResult { get; set; }
        
        /// <summary>
        /// 获取 Prompt 长度
        /// </summary>
        public int PromptLength => Prompt?.Length ?? 0;
        
        /// <summary>
        /// 估算 Token 数量
        /// </summary>
        public int EstimatedTokens => SmartPromptBuilder.Instance.EstimateTokens(Prompt);
        
        /// <summary>
        /// 获取使用的模块数量
        /// </summary>
        public int ModuleCount => RouteResult?.Modules?.Count ?? 0;
        
        /// <summary>
        /// 获取调试信息
        /// </summary>
        public string GetDebugInfo()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== Build Result ===");
            sb.AppendLine($"Success: {Success}");
            sb.AppendLine($"Build Time: {BuildTimeMs:F2}ms");
            sb.AppendLine($"Prompt Length: {PromptLength} chars");
            sb.AppendLine($"Estimated Tokens: {EstimatedTokens}");
            sb.AppendLine($"Modules Used: {ModuleCount}");
            
            if (RouteResult != null)
            {
                sb.AppendLine($"Intents: [{string.Join(", ", RouteResult.SelectedIntents)}]");
                sb.AppendLine($"Modules: [{string.Join(", ", RouteResult.Modules.Select(m => m.defName))}]");
            }
            
            if (!string.IsNullOrEmpty(Error))
            {
                sb.AppendLine($"\nError: {Error}");
            }
            
            return sb.ToString();
        }
    }
    
    /// <summary>
    /// 智能 Prompt 系统的门面类
    /// 提供简化的 API 供外部调用
    /// </summary>
    public static class SmartPrompt
    {
        /// <summary>
        /// 初始化智能 Prompt 系统
        /// 应在游戏启动时调用
        /// </summary>
        public static void Initialize()
        {
            Log.Message("[SmartPrompt] Initializing Orchestrator-Worker architecture...");
            
            // 构建 AC 自动机
            FlashMatcher.Instance.Build();
            
            Log.Message("[SmartPrompt] Initialization complete.");
            Log.Message($"  {FlashMatcher.Instance.GetStats()}");
        }
        
        /// <summary>
        /// 重建系统（用于热重载）
        /// </summary>
        public static void Rebuild()
        {
            Log.Message("[SmartPrompt] Rebuilding...");
            FlashMatcher.Instance.Rebuild();
            SmartPromptBuilder.Instance.ClearStats();
            Log.Message("[SmartPrompt] Rebuild complete.");
        }
        
        /// <summary>
        /// 根据用户输入生成智能 Prompt
        /// 这是最常用的入口方法
        /// </summary>
        /// <param name="userInput">用户输入文本</param>
        /// <param name="context">Scriban 渲染上下文（可选）</param>
        /// <returns>生成的 Prompt 字符串</returns>
        public static string Generate(string userInput, PromptContext context = null)
        {
            var result = SmartPromptBuilder.Instance.Build(userInput, context);
            
            if (result.Success)
            {
                if (Prefs.DevMode)
                {
                    Log.Message($"[SmartPrompt] Generated prompt: {result.PromptLength} chars, " +
                               $"{result.ModuleCount} modules, {result.BuildTimeMs:F2}ms");
                }
                return result.Prompt;
            }
            else
            {
                Log.Error($"[SmartPrompt] Generation failed: {result.Error}");
                return SmartPromptBuilder.Instance.BuildCoreOnly(context);
            }
        }
        
        /// <summary>
        /// 根据意图列表生成 Prompt
        /// </summary>
        public static string GenerateByIntents(List<string> intents, PromptContext context = null)
        {
            var result = SmartPromptBuilder.Instance.BuildByIntents(intents, context);
            return result.Success ? result.Prompt : "";
        }
        
        /// <summary>
        /// 分析用户输入，返回匹配的意图列表
        /// </summary>
        public static List<string> AnalyzeIntents(string userInput, float minScore = 0.5f)
        {
            return FlashMatcher.Instance.GetMatchedIntents(userInput, minScore);
        }
        
        /// <summary>
        /// 获取系统统计信息
        /// </summary>
        public static string GetStats()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== Smart Prompt System Stats ===");
            sb.AppendLine(FlashMatcher.Instance.GetStats());
            sb.AppendLine(SmartPromptBuilder.Instance.GetStats());
            return sb.ToString();
        }
    }
}
