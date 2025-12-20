using HarmonyLib;
using Verse;
using TheSecondSeat.Settings;
using TheSecondSeat.Narrator;
using TheSecondSeat.Core;
using TheSecondSeat.Events;
using TheSecondSeat.Autonomous;
using TheSecondSeat.Monitoring;
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
