using HarmonyLib;
using Verse;
using TheSecondSeat.Settings;
using TheSecondSeat.Narrator;
using TheSecondSeat.Core;
using TheSecondSeat.Events;
using TheSecondSeat.Autonomous;
using TheSecondSeat.Monitoring;
using TheSecondSeat.PersonaGeneration;
using TheSecondSeat.RimAgent; // ⭐ v1.6.77: 新增 - 引入 RimAgent
using TheSecondSeat.RimAgent.Tools; // ⭐ v1.6.77: 新增 - 引入 Tools
using TheSecondSeat.Utils; // ⭐ v1.6.80: 新增 - 引入 Utils
using TheSecondSeat.Framework; // ⭐ v1.6.83: 新增 - 引入 Framework
using TheSecondSeat.Descent; // ⭐ v1.6.83: 新增 - 引入 Descent
using TheSecondSeat.Components; // ⭐ v1.6.97: 新增 - 引入 Components (DraftableAnimal)
using System.Reflection;

namespace TheSecondSeat
{
    /// <summary>
    /// Main mod entry point with Harmony patching and initialization
    /// </summary>
    [StaticConstructorOnStartup]
    public static class TheSecondSeatCore
    {
        static TheSecondSeatCore()
        {
            // ⚠️ v1.6.80: 初始化主线程ID（必须在所有资源加载前调用）
            // ? 优化：添加异常捕获，防止初始化失败导致 Mod 加载崩溃
            try
            {
                TSS_AssetLoader.InitializeMainThread();
            }
            catch (System.Exception ex)
            {
                Log.Warning($"[The Second Seat] 主线程ID初始化警告: {ex.Message}. 将在后续通过 lazy load 重试。");
            }
            
            // Apply Harmony patches
            // This will also apply patches in ComponentRegistrar
            var harmony = new Harmony("yourname.thesecondseat");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
            
            // ⭐ v1.6.97: 手动应用 DraftableAnimal Patches
            DraftableAnimalHarmonyPatches.ApplyPatches(harmony);
            
            // ✅ v1.6.84: 简化初始化日志，只输出一条
            Log.Message("[The Second Seat] AI Narrator Assistant v1.0.0 初始化完成");
            
            // ⭐ v1.6.96: 初始化日志分析工具
            LogAnalysisTool.Init();

            // ⭐ v1.6.77: 注册 RimAgent 工具
            RegisterTools();
            
            // ⭐ 新增：调试日志 - 列出所有已加载的 NarratorPersonaDef
            LogLoadedPersonaDefs();
        }
        
        /// <summary>
        /// ⭐ v1.6.77: 注册所有 RimAgent 工具
        /// </summary>
        private static void RegisterTools()
        {
            try
            {
                // 注册工具（静默）
                RimAgentTools.RegisterTool("search", new SearchTool());
                RimAgentTools.RegisterTool("read_log", new LogReaderTool());
                RimAgentTools.RegisterTool("analyze_last_error", new LogAnalysisTool());
                RimAgentTools.RegisterTool("patch_file", new FilePatcherTool());
            }
            catch (System.Exception ex)
            {
                Log.Error($"[The Second Seat] ❌ 工具注册失败: {ex.Message}");
                Log.Error($"[The Second Seat] 堆栈跟踪: {ex.StackTrace}");
            }
        }
        
        /// <summary>
        /// ⭐ 调试方法：列出所有已加载的 NarratorPersonaDef
        /// </summary>
        private static void LogLoadedPersonaDefs()
        {
            try
            {
                var allDefs = DefDatabase<NarratorPersonaDef>.AllDefsListForReading;
                
                if (allDefs == null || allDefs.Count == 0)
                {
                    Log.Warning("[The Second Seat] ❌ 未找到任何 NarratorPersonaDef！");
                }
                else if (Prefs.DevMode)
                {
                    // ✅ v1.6.84: 仅在 DevMode 下输出详细人格信息
                    Log.Message($"[The Second Seat] 成功加载 {allDefs.Count} 个 NarratorPersonaDef");
                    
                    foreach (var def in allDefs)
                    {
                        string modName = def.modContentPack?.Name ?? "未知Mod";
                        Log.Message($"[The Second Seat]   • {def.defName} ({modName})");
                    }
                }
            }
            catch (System.Exception ex)
            {
                Log.Error($"[The Second Seat] ❌ LogLoadedPersonaDefs 异常: {ex.Message}");
                Log.Error($"[The Second Seat] 堆栈跟踪: {ex.StackTrace}");
            }
        }
    }
}
