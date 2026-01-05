using System;
using System.Collections.Generic;
using Verse;
using HarmonyLib;
using TheSecondSeat.Narrator;
using TheSecondSeat.Core;
using TheSecondSeat.Events;
using TheSecondSeat.Autonomous;
using TheSecondSeat.Monitoring;
using TheSecondSeat.Descent;
using TheSecondSeat.Storyteller;
using TheSecondSeat.UI;
using TheSecondSeat.Framework;
using TheSecondSeat.PersonaGeneration;

namespace TheSecondSeat.Core
{
    /// <summary>
    /// 组件注册器 - 集中管理所有动态 GameComponent 和 MapComponent 的注册。
    /// 确保组件在运行时注入，无需修改 XML。
    /// </summary>
    public static class ComponentRegistrar
    {
        private static bool componentsRegistered = false;

        public static void RegisterGameComponents(Game game)
        {
            if (componentsRegistered) return;

            // 辅助方法：如果组件缺失则添加
            void EnsureComponent<T>(Game g) where T : GameComponent
            {
                if (g.GetComponent<T>() == null)
                {
                    try 
                    {
                        // 优先尝试带 Game 参数的构造函数
                        var comp = (T)Activator.CreateInstance(typeof(T), new object[] { g });
                        g.components.Add(comp);
                    }
                    catch
                    {
                        // 失败则尝试无参构造函数
                        try
                        {
                             var comp = (T)Activator.CreateInstance(typeof(T));
                             g.components.Add(comp);
                        }
                        catch (Exception ex)
                        {
                            Log.Error($"[The Second Seat] 注册 GameComponent {typeof(T).Name} 失败: {ex.Message}");
                        }
                    }
                }
            }

            // 1. NarratorManager (叙事者管理器)
            EnsureComponent<NarratorManager>(game);
            
            // 2. NarratorController (叙事者控制器)
            EnsureComponent<NarratorController>(game);

            // 3. AutoEventTrigger (自动事件触发器)
            EnsureComponent<AutoEventTrigger>(game);

            // 4. AutonomousBehaviorSystem (自主行为系统)
            EnsureComponent<AutonomousBehaviorSystem>(game);

            // 5. ColonyStateMonitor (殖民地状态监控)
            EnsureComponent<ColonyStateMonitor>(game);

            // 6. PlayerInteractionMonitor (玩家交互监控)
            EnsureComponent<PlayerInteractionMonitor>(game);

            // 7. OpponentEventController (对手事件控制器)
            EnsureComponent<OpponentEventController>(game);

            // 8. ProactiveDialogueSystem (主动对话系统)
            EnsureComponent<ProactiveDialogueSystem>(game);

            // 9. NarratorEventManager (叙事者事件管理器)
            EnsureComponent<NarratorEventManager>(game);

            // 10. NarratorDescentSystem (叙事者降临系统)
            EnsureComponent<NarratorDescentSystem>(game);

            // 11. StorytellerAgent (叙事者代理 - 新增)
            EnsureComponent<StorytellerAgent>(game);

            // 12. GameStateObserver (游戏状态观察者 - 新增)
            EnsureComponent<GameStateObserver>(game);

            // 13. EmotionTracker (情绪追踪器 - 新增)
            EnsureComponent<EmotionTracker>(game);

            componentsRegistered = true;
            if (Prefs.DevMode) Log.Message($"[The Second Seat] 已注册 13 个 GameComponents。");
        }

        public static void Reset()
        {
            componentsRegistered = false;
        }

        public static void RegisterMapComponents(Map map)
        {
             // 辅助方法：如果组件缺失则添加
            void EnsureMapComponent<T>(Map m) where T : MapComponent
            {
                if (m.GetComponent<T>() == null)
                {
                    try 
                    {
                        var comp = (T)Activator.CreateInstance(typeof(T), new object[] { m });
                        m.components.Add(comp);
                    }
                    catch (Exception ex)
                    {
                         Log.Error($"[The Second Seat] 注册 MapComponent {typeof(T).Name} 失败: {ex.Message}");
                    }
                }
            }

            // 1. NarratorButtonManager (按钮管理器)
            EnsureMapComponent<NarratorButtonManager>(map);

            // 2. DragonShadowRenderer (龙影渲染器 - 新增)
            EnsureMapComponent<DragonShadowRenderer>(map);

            if (Prefs.DevMode) Log.Message($"[The Second Seat] MapComponents registered check complete for map {map.uniqueID}");
        }
    }

    // Harmony 补丁

    [HarmonyPatch(typeof(Game), "InitNewGame")]
    public static class Game_InitNewGame_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(Game __instance)
        {
            ComponentRegistrar.Reset();
            ComponentRegistrar.RegisterGameComponents(__instance);
        }
    }

    [HarmonyPatch(typeof(Game), "LoadGame")]
    public static class Game_LoadGame_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(Game __instance)
        {
            // 修正顺序：先清理缓存，再注册组件。
            // 否则组件初始化时预加载的纹理会被紧接着的 ClearCache 清空，导致立绘消失。
            // [暂定] 暂停缓存清除功能，等待进一步指令
            // TheSecondSeat.PersonaGeneration.PortraitLoader.ClearCache();
            // TheSecondSeat.PersonaGeneration.LayeredPortraitCompositor.ClearAllCache();
            // TheSecondSeat.PersonaGeneration.ExpressionCompositor.ClearCache();

            ComponentRegistrar.Reset();
            ComponentRegistrar.RegisterGameComponents(__instance);

            if (__instance.Maps != null)
            {
                foreach (var map in __instance.Maps)
                {
                    ComponentRegistrar.RegisterMapComponents(map);
                }
            }
        }
    }

    [HarmonyPatch(typeof(Map), "ConstructComponents")]
    public static class Map_ConstructComponents_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(Map __instance)
        {
            ComponentRegistrar.RegisterMapComponents(__instance);
        }
    }
}