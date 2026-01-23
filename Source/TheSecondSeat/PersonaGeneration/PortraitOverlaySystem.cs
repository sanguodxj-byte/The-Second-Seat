using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using TheSecondSeat.UI;

namespace TheSecondSeat.Core
{
    /// <summary>
    /// ? v1.6.42: 立绘渲染叠层系统
    /// 通过 Harmony 在 OnGUI 后直接绘制立绘，独立于 Window 系统（避免遮挡物、图层问题）
    /// </summary>
    [StaticConstructorOnStartup]
    public static class PortraitOverlaySystem
    {
        private static FullBodyPortraitPanel portraitPanel;
        private static bool isEnabled = false;
        
        static PortraitOverlaySystem()
        {
            // 应用 Harmony 补丁
            var harmony = new Harmony("TheSecondSeat.PortraitOverlay");
            harmony.PatchAll();
            
            // ? 移除日志输出
            // if (Prefs.DevMode)
            // {
            //     Log.Message("[PortraitOverlaySystem] Harmony 补丁已应用");
            // }
        }
        
        /// <summary>
        /// 初始化立绘面板实例
        /// </summary>
        public static void Initialize()
        {
            if (portraitPanel == null)
            {
                portraitPanel = new FullBodyPortraitPanel();
                
                // ? 移除日志输出
                // if (Prefs.DevMode)
                // {
                //     Log.Message("[PortraitOverlaySystem] 立绘面板已初始化");
                // }
            }
        }
        
        /// <summary>
        /// 切换立绘显示状态
        /// </summary>
        public static void Toggle(bool show)
        {
            isEnabled = show;
            
            if (isEnabled)
            {
                Initialize();
            }
            
            // ? 移除日志输出
            // if (Prefs.DevMode)
            // {
            //     Log.Message($"[PortraitOverlaySystem] 立绘显示状态: {(isEnabled ? "开启" : "关闭")}");
            // }
        }
        
        /// <summary>
        /// 获取当前显示状态
        /// </summary>
        public static bool IsEnabled()
        {
            return isEnabled;
        }
        
        /// <summary>
        /// 获取立绘面板实例（供外部调用）
        /// </summary>
        public static FullBodyPortraitPanel GetPanel()
        {
            return portraitPanel;
        }
        
        /// <summary>
        /// ? Harmony 补丁：在 UIRoot_Play.UIRootOnGUI 后绘制立绘
        /// </summary>
        [HarmonyPatch(typeof(UIRoot_Play), "UIRootOnGUI")]
        public static class UIRoot_Play_UIRootOnGUI_Patch
        {
            [HarmonyPostfix]
            public static void Postfix()
            {
                // ? 1. 检查游戏状态
                if (Current.ProgramState != ProgramState.Playing)
                {
                    return;
                }
                
                // ? 2. 检查立绘是否启用
                if (!isEnabled || portraitPanel == null)
                {
                    return;
                }
                
                // ? 3. 检查是否有全屏 UI 打开（如主菜单、设置）
                if (Find.WindowStack.IsOpen<Page>())
                {
                    return;
                }
                
                // ? 4. 检查是否有模态对话框
                if (Find.WindowStack.IsOpen<Dialog_MessageBox>())
                {
                    return;
                }
                
                // ? 5. 绘制立绘面板
                try
                {
                    portraitPanel.Draw();
                }
                catch (System.Exception ex)
                {
                    Log.Error($"[PortraitOverlaySystem] 绘制立绘时发生错误: {ex.Message}\n{ex.StackTrace}");
                }

                // ? 6. DialogueOverlayPanel 是一个 Window，它会通过 Find.WindowStack 自动绘制
                // 不需要手动调用 Draw()，Window 的 DoWindowContents() 会自动被调用
            }
        }
    }
}
