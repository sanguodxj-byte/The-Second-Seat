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
using System.Reflection;

namespace TheSecondSeat
{
    /// <summary>
    /// Main mod entry point with Harmony patching and GameComponent registration
    /// </summary>
    [StaticConstructorOnStartup]
    public static class TheSecondSeatCore
    {
        static TheSecondSeatCore()
        {
            // Apply Harmony patches
            var harmony = new Harmony("yourname.thesecondseat");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
            
            Log.Message("[The Second Seat] ================================================");
            Log.Message("[The Second Seat] AI Narrator Assistant 初始化中...");
            Log.Message("[The Second Seat] 版本: 1.0.0");
            Log.Message("[The Second Seat] Harmony补丁已应用");
            Log.Message("[The Second Seat] ================================================");
            
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
                Log.Message("[The Second Seat] ================================================");
                Log.Message("[The Second Seat] 🔧 开始注册 RimAgent 工具...");
                
                // 注册工具
                RimAgentTools.RegisterTool("search", new SearchTool());
                RimAgentTools.RegisterTool("read_log", new LogReaderTool()); // ⭐ 新增：日志读取工具
                // RimAgentTools.RegisterTool("file_access", new FileAccessTool()); // 可选：文件访问工具（如果需要）
                
                Log.Message("[The Second Seat] ✅ 工具注册完成！");
                Log.Message("[The Second Seat]   • search - 搜索游戏数据");
                Log.Message("[The Second Seat]   • read_log - 读取游戏日志（诊断报错）");
                Log.Message("[The Second Seat] ================================================");
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
                Log.Message("[The Second Seat] ================================================");
                Log.Message("[The Second Seat] 📊 开始检查 NarratorPersonaDef 加载情况...");
                
                var allDefs = DefDatabase<NarratorPersonaDef>.AllDefsListForReading;
                
                if (allDefs == null || allDefs.Count == 0)
                {
                    Log.Warning("[The Second Seat] ❌ 未找到任何 NarratorPersonaDef！");
                    Log.Warning("[The Second Seat] 可能原因：");
                    Log.Warning("[The Second Seat]   1. XML 文件未正确放置在 Defs/ 文件夹");
                    Log.Warning("[The Second Seat]   2. XML 类型声明错误");
                    Log.Warning("[The Second Seat]   3. Mod 加载顺序问题");
                }
                else
                {
                    Log.Message($"[The Second Seat] ✅ 成功加载 {allDefs.Count} 个 NarratorPersonaDef");
                    Log.Message("[The Second Seat] ------------------------------------------------");
                    
                    foreach (var def in allDefs)
                    {
                        // 获取 Mod 来源信息
                        string modName = def.modContentPack?.Name ?? "未知Mod";
                        string modPackageId = def.modContentPack?.PackageId ?? "未知PackageId";
                        
                        // 检查是否有立绘路径
                        string portraitStatus = string.IsNullOrEmpty(def.portraitPath) 
                            ? "❌ 无立绘路径" 
                            : $"✅ {def.portraitPath}";
                        
                        // 检查是否启用分层立绘
                        string layeredStatus = def.useLayeredPortrait ? "🎨 分层立绘" : "📷 单图";
                        
                        Log.Message($"[The Second Seat] 人格: {def.defName}");
                        Log.Message($"[The Second Seat]   • 名称: {def.narratorName}");
                        Log.Message($"[The Second Seat]   • 来源: {modName} ({modPackageId})");
                        Log.Message($"[The Second Seat]   • 立绘: {portraitStatus}");
                        Log.Message($"[The Second Seat]   • 类型: {layeredStatus}");
                        Log.Message($"[The Second Seat]   • 主题色: {def.primaryColor}");
                        Log.Message("[The Second Seat] ------------------------------------------------");
                    }
                }
                
                Log.Message("[The Second Seat] 📊 NarratorPersonaDef 检查完成");
                Log.Message("[The Second Seat] ================================================");
            }
            catch (System.Exception ex)
            {
                Log.Error($"[The Second Seat] ❌ LogLoadedPersonaDefs 异常: {ex.Message}");
                Log.Error($"[The Second Seat] 堆栈跟踪: {ex.StackTrace}");
            }
        }
    }

    /// <summary>
    /// Patch to register our GameComponents when a new game starts
    /// </summary>
    [HarmonyPatch(typeof(Game), "InitNewGame")]
    public static class Game_InitNewGame_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(Game __instance)
        {
            GameComponentRegistration.Reset();
            GameComponentRegistration.RegisterGameComponents(__instance);
        }
    }

    /// <summary>
    /// Patch to register our GameComponents when a game is loaded
    /// </summary>
    [HarmonyPatch(typeof(Game), "LoadGame")]
    public static class Game_LoadGame_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(Game __instance)
        {
            GameComponentRegistration.Reset();
            GameComponentRegistration.RegisterGameComponents(__instance);
        }
    }

    /// <summary>
    /// Patch to register MapComponents when a map is created
    /// </summary>
    [HarmonyPatch(typeof(Map), "ConstructComponents")]
    public static class Map_ConstructComponents_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(Map __instance)
        {
            MapComponentRegistration.RegisterMapComponents(__instance);
        }
    }

    /// <summary>
    /// Helper to register game components
    /// </summary>
    public static class GameComponentRegistration
    {
        private static bool componentsRegistered = false;

        public static void RegisterGameComponents(Game game)
        {
            if (componentsRegistered)
            {
                return;
            }

            Log.Message("[The Second Seat] 开始注册 GameComponents...");

            // Add NarratorManager if not present
            var narratorManager = game.GetComponent<NarratorManager>();
            if (narratorManager == null)
            {
                narratorManager = new NarratorManager(game);
                game.components.Add(narratorManager);
                Log.Message("[The Second Seat] ? NarratorManager 已注册");
            }

            // Add NarratorController if not present
            var narratorController = game.GetComponent<NarratorController>();
            if (narratorController == null)
            {
                narratorController = new NarratorController(game);
                game.components.Add(narratorController);
                Log.Message("[The Second Seat] ? NarratorController 已注册");
            }

            // Add AutoEventTrigger if not present
            var autoEventTrigger = game.GetComponent<AutoEventTrigger>();
            if (autoEventTrigger == null)
            {
                autoEventTrigger = new AutoEventTrigger(game);
                game.components.Add(autoEventTrigger);
                Log.Message("[The Second Seat] ? AutoEventTrigger 已注册");
            }

            // Add AutonomousBehaviorSystem if not present
            var autonomousSystem = game.GetComponent<AutonomousBehaviorSystem>();
            if (autonomousSystem == null)
            {
                autonomousSystem = new AutonomousBehaviorSystem(game);
                game.components.Add(autonomousSystem);
                Log.Message("[The Second Seat] ? AutonomousBehaviorSystem 已注册");
            }

            // Add ColonyStateMonitor if not present
            var colonyMonitor = game.GetComponent<ColonyStateMonitor>();
            if (colonyMonitor == null)
            {
                colonyMonitor = new ColonyStateMonitor(game);
                game.components.Add(colonyMonitor);
                Log.Message("[The Second Seat] ? ColonyStateMonitor 已注册 (监控殖民地状态变化)");
            }

            // Add PlayerInteractionMonitor if not present
            var interactionMonitor = game.GetComponent<PlayerInteractionMonitor>();
            if (interactionMonitor == null)
            {
                interactionMonitor = new PlayerInteractionMonitor(game);
                game.components.Add(interactionMonitor);
                Log.Message("[The Second Seat] ? PlayerInteractionMonitor 已注册 (监控玩家互动)");
            }

            // Add OpponentEventController if not present (对弈者模式事件控制器)
            var opponentController = game.GetComponent<OpponentEventController>();
            if (opponentController == null)
            {
                opponentController = new OpponentEventController(game);
                game.components.Add(opponentController);
                Log.Message("[The Second Seat] ? OpponentEventController 已注册 (对弈者模式事件控制)");
            }

            componentsRegistered = true;
            Log.Message("[The Second Seat] 所有 GameComponents 注册完成！");
            Log.Message("[The Second Seat] ================================================");
        }

        public static void Reset()
        {
            componentsRegistered = false;
        }
    }

    /// <summary>
    /// Helper to register map components
    /// </summary>
    public static class MapComponentRegistration
    {
        public static void RegisterMapComponents(Map map)
        {
            // Add NarratorButtonManager if not present
            var buttonManager = map.GetComponent<UI.NarratorButtonManager>();
            if (buttonManager == null)
            {
                buttonManager = new UI.NarratorButtonManager(map);
                map.components.Add(buttonManager);
                Log.Message("[The Second Seat] ? NarratorButtonManager 已注册到地图");
            }
        }
    }
}
