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
            // ⚠️ v1.6.80: 初始化主线程ID（必须在所有资源加载前调用）
            TSS_AssetLoader.InitializeMainThread();
            
            // Apply Harmony patches
            var harmony = new Harmony("yourname.thesecondseat");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
            
            // ✅ v1.6.84: 简化初始化日志，只输出一条
            Log.Message("[The Second Seat] AI Narrator Assistant v1.0.0 初始化完成");
            
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

            // ✅ v1.6.84: 静默注册 GameComponents，只在DevMode下输出详细日志
            
            // Add NarratorManager if not present
            var narratorManager = game.GetComponent<NarratorManager>();
            if (narratorManager == null)
            {
                narratorManager = new NarratorManager(game);
                game.components.Add(narratorManager);
            }

            // Add NarratorController if not present
            var narratorController = game.GetComponent<NarratorController>();
            if (narratorController == null)
            {
                narratorController = new NarratorController(game);
                game.components.Add(narratorController);
            }

            // Add AutoEventTrigger if not present
            var autoEventTrigger = game.GetComponent<AutoEventTrigger>();
            if (autoEventTrigger == null)
            {
                autoEventTrigger = new AutoEventTrigger(game);
                game.components.Add(autoEventTrigger);
            }

            // Add AutonomousBehaviorSystem if not present
            var autonomousSystem = game.GetComponent<AutonomousBehaviorSystem>();
            if (autonomousSystem == null)
            {
                autonomousSystem = new AutonomousBehaviorSystem(game);
                game.components.Add(autonomousSystem);
            }

            // Add ColonyStateMonitor if not present
            var colonyMonitor = game.GetComponent<ColonyStateMonitor>();
            if (colonyMonitor == null)
            {
                colonyMonitor = new ColonyStateMonitor(game);
                game.components.Add(colonyMonitor);
            }

            // Add PlayerInteractionMonitor if not present
            var interactionMonitor = game.GetComponent<PlayerInteractionMonitor>();
            if (interactionMonitor == null)
            {
                interactionMonitor = new PlayerInteractionMonitor(game);
                game.components.Add(interactionMonitor);
            }

            // Add OpponentEventController if not present
            var opponentController = game.GetComponent<OpponentEventController>();
            if (opponentController == null)
            {
                opponentController = new OpponentEventController(game);
                game.components.Add(opponentController);
            }

            // Add ProactiveDialogueSystem if not present
            var proactiveSystem = game.GetComponent<ProactiveDialogueSystem>();
            if (proactiveSystem == null)
            {
                proactiveSystem = new ProactiveDialogueSystem(game);
                game.components.Add(proactiveSystem);
            }

            // Add NarratorEventManager if not present
            var eventManager = game.GetComponent<Framework.NarratorEventManager>();
            if (eventManager == null)
            {
                eventManager = new Framework.NarratorEventManager(game);
                game.components.Add(eventManager);
            }

            // Add NarratorDescentSystem if not present
            var descentSystem = game.GetComponent<Descent.NarratorDescentSystem>();
            if (descentSystem == null)
            {
                descentSystem = new Descent.NarratorDescentSystem(game);
                game.components.Add(descentSystem);
            }

            componentsRegistered = true;
            
            if (Prefs.DevMode)
            {
                Log.Message("[The Second Seat] GameComponents 注册完成");
            }
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
            // Add NarratorButtonManager if not present（静默注册）
            var buttonManager = map.GetComponent<UI.NarratorButtonManager>();
            if (buttonManager == null)
            {
                buttonManager = new UI.NarratorButtonManager(map);
                map.components.Add(buttonManager);
            }
        }
    }
}
