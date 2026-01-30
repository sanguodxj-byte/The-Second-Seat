using System;
using Verse;
using RimWorld;
using LudeonTK;
using HarmonyLib;

namespace TheSecondSeat.SmartPrompt
{
    /// <summary>
    /// SmartPrompt 系统初始化器
    /// 
    /// 负责在游戏启动时初始化 Orchestrator-Worker 架构：
    /// 1. 构建 FlashMatcher 的 AC 自动机
    /// 2. 预热核心模块
    /// 3. 注册热重载支持
    /// </summary>
    [StaticConstructorOnStartup]
    public static class SmartPromptInitializer
    {
        static SmartPromptInitializer()
        {
            // 延迟初始化，确保所有 Def 已加载
            LongEventHandler.QueueLongEvent(() =>
            {
                try
                {
                    Initialize();
                }
                catch (Exception ex)
                {
                    Log.Error($"[SmartPrompt] Initialization failed: {ex}");
                }
            }, "SmartPromptInitializing", false, null);
        }
        
        /// <summary>
        /// 初始化 SmartPrompt 系统
        /// </summary>
        private static void Initialize()
        {
            Log.Message("[SmartPrompt] ========================================");
            Log.Message("[SmartPrompt] Initializing Orchestrator-Worker Architecture");
            Log.Message("[SmartPrompt] ========================================");
            
            var sw = System.Diagnostics.Stopwatch.StartNew();
            
            // 0. 自动加载 Prompt 模块 (Auto-Discovery)
            PromptAutoLoader.AutoLoadDefs();
            
            // 1. 初始化 FlashMatcher (AC 自动机)
            SmartPrompt.Initialize();
            
            // 2. 统计信息
            var moduleCount = DefDatabase<PromptModuleDef>.DefCount;
            var keywordCount = 0;
            foreach (var module in DefDatabase<PromptModuleDef>.AllDefsListForReading)
            {
                keywordCount += module.expandedKeywords.Count;
            }
            
            sw.Stop();
            
            Log.Message($"[SmartPrompt] Loaded {moduleCount} prompt modules");
            Log.Message($"[SmartPrompt] Indexed {keywordCount} keywords in AC automaton");
            Log.Message($"[SmartPrompt] Initialization completed in {sw.ElapsedMilliseconds}ms");
            Log.Message("[SmartPrompt] ========================================");
            
            // 3. 开发模式下输出详细信息
            if (Prefs.DevMode)
            {
                Log.Message("[SmartPrompt] Registered Modules:");
                foreach (var module in DefDatabase<PromptModuleDef>.AllDefsListForReading)
                {
                    Log.Message($"  - {module.GetDebugInfo()}");
                }
            }
        }
        
        /// <summary>
        /// ⭐ v3.1.0: 手动重建系统（支持运行时热重载）
        /// 当用户在 PromptManagementWindow 中禁用/启用提示词时调用
        /// </summary>
        public static void RebuildSystem()
        {
            Log.Message("[SmartPrompt] Manual rebuild triggered...");
            
            try
            {
                // 1. 清除 DefDatabase 中的动态加载模块（保留 XML 定义的模块）
                // 注意：由于 RimWorld 不支持从 DefDatabase 移除 Def，
                // 我们只能重新加载内容，但禁用的模块会被 FlashMatcher 忽略
                
                // 2. 清除 PromptLoader 缓存（确保读取最新文件）
                PersonaGeneration.PromptLoader.ClearCache();
                
                // 3. 重新加载模块内容（刷新被编辑的内容）
                foreach (var module in DefDatabase<PromptModuleDef>.AllDefsListForReading)
                {
                    module.ReloadContent();
                }
                
                // 4. 重建 FlashMatcher AC 自动机
                SmartPrompt.Rebuild();
                
                Log.Message("[SmartPrompt] Rebuild complete. Modules reloaded.");
            }
            catch (Exception ex)
            {
                Log.Error($"[SmartPrompt] Rebuild failed: {ex.Message}");
            }
        }
    }
    
    /// <summary>
    /// 开发者控制台命令
    /// </summary>
    public static class SmartPromptDebugActions
    {
        /// <summary>
        /// 控制台命令：重建 SmartPrompt 系统
        /// 用法：在游戏控制台输入 SmartPromptRebuild
        /// </summary>
        [DebugAction("The Second Seat", "Rebuild SmartPrompt", allowedGameStates = AllowedGameStates.Playing)]
        public static void RebuildSmartPrompt()
        {
            SmartPromptInitializer.RebuildSystem();
            Messages.Message("SmartPrompt system rebuilt.", MessageTypeDefOf.TaskCompletion);
        }
        
        /// <summary>
        /// 控制台命令：显示 SmartPrompt 统计信息
        /// </summary>
        [DebugAction("The Second Seat", "SmartPrompt Stats", allowedGameStates = AllowedGameStates.Playing)]
        public static void ShowSmartPromptStats()
        {
            string stats = SmartPrompt.GetStats();
            Log.Message(stats);
            Messages.Message("SmartPrompt stats logged.", MessageTypeDefOf.TaskCompletion);
        }
        
        /// <summary>
        /// 控制台命令：测试意图识别
        /// </summary>
        [DebugAction("The Second Seat", "Test Intent Recognition", allowedGameStates = AllowedGameStates.Playing)]
        public static void TestIntentRecognition()
        {
            // 测试用例
            var testInputs = new[]
            {
                "帮我把这片水稻收了",
                "harvest all the corn",
                "征召所有人准备战斗",
                "build a wall here",
                "去打那只鹿"
            };
            
            Log.Message("[SmartPrompt] === Intent Recognition Test ===");
            
            foreach (var input in testInputs)
            {
                var result = SmartPromptBuilder.Instance.Build(input);
                Log.Message($"\nInput: \"{input}\"");
                Log.Message($"  Intents: [{string.Join(", ", result.RouteResult?.SelectedIntents ?? new System.Collections.Generic.List<string>())}]");
                Log.Message($"  Modules: [{string.Join(", ", result.RouteResult?.Modules?.ConvertAll(m => m.defName) ?? new System.Collections.Generic.List<string>())}]");
                Log.Message($"  Time: {result.BuildTimeMs:F2}ms");
            }
            
            Messages.Message("Intent recognition test completed. See log for results.", MessageTypeDefOf.TaskCompletion);
        }
        
        /// <summary>
        /// 控制台命令：生成并显示 Prompt
        /// </summary>
        [DebugAction("The Second Seat", "Generate Test Prompt", allowedGameStates = AllowedGameStates.Playing)]
        public static void GenerateTestPrompt()
        {
            string testInput = "帮我收割水稻然后准备战斗";
            var result = SmartPromptBuilder.Instance.Build(testInput);
            
            Log.Message($"\n=== Generated Prompt for: \"{testInput}\" ===");
            Log.Message($"Modules: {result.ModuleCount}");
            Log.Message($"Length: {result.PromptLength} chars");
            Log.Message($"Est. Tokens: {result.EstimatedTokens}");
            Log.Message($"Time: {result.BuildTimeMs:F2}ms");
            Log.Message($"\n--- Prompt Content ---\n{result.Prompt}");
            
            Messages.Message($"Test prompt generated ({result.PromptLength} chars). See log.", MessageTypeDefOf.TaskCompletion);
        }
    }
}
