using System;
using TheSecondSeat.RimAgent;
using TheSecondSeat.RimAgent.Tools;

namespace TheSecondSeat
{
    /// <summary>
    /// Main mod static initializer (separate from the Mod class)
    /// ⭐ v1.6.65: 注册 RimAgent 工具
    /// </summary>
    [Verse.StaticConstructorOnStartup]
    public static class TheSecondSeatInit
    {
        static TheSecondSeatInit()
        {
            Verse.Log.Message("[The Second Seat] AI Narrator Assistant initialized");
            
            // ⭐ v1.6.65: 初始化 LLM Provider
            try
            {
                LLMProviderFactory.Initialize();
                Verse.Log.Message("[The Second Seat] ⭐ LLM Providers initialized");
            }
            catch (Exception ex)
            {
                Verse.Log.Error($"[The Second Seat] Failed to initialize LLM Providers: {ex.Message}");
            }
            
            // ⭐ v1.6.65: 注册工具
            try
            {
                RimAgentTools.RegisterTool("search", new SearchTool());
                RimAgentTools.RegisterTool("analyze", new AnalyzeTool());
                RimAgentTools.RegisterTool("command", new CommandTool());
                
                Verse.Log.Message("[The Second Seat] ⭐ RimAgent tools registered: search, analyze, command");
            }
            catch (Exception ex)
            {
                Verse.Log.Error($"[The Second Seat] Failed to register RimAgent tools: {ex.Message}");
            }
        }
    }
}
