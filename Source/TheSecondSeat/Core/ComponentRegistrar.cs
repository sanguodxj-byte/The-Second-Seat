using System;
using Verse;
using TheSecondSeat.Narrator;
using TheSecondSeat.Monitoring;
using TheSecondSeat.Performance;

namespace TheSecondSeat.Core
{
    /// <summary>
    /// Centralized registrar for all GameComponents
    /// Ensures all required components are properly injected
    /// </summary>
    public static class ComponentRegistrar
    {
        public static void EnsureComponents(Game game)
        {
            if (game == null) return;

            // 1. NarratorManager
            if (game.GetComponent<NarratorManager>() == null)
            {
                Log.Message("[The Second Seat] Injecting NarratorManager...");
                game.components.Add(new NarratorManager(game));
            }

            // 2. NarratorController
            if (game.GetComponent<NarratorController>() == null)
            {
                Log.Message("[The Second Seat] Injecting NarratorController...");
                game.components.Add(new NarratorController(game));
            }

            // 3. PlayerInteractionMonitor
            if (game.GetComponent<PlayerInteractionMonitor>() == null)
            {
                Log.Message("[The Second Seat] Injecting PlayerInteractionMonitor...");
                game.components.Add(new PlayerInteractionMonitor(game));
            }
            
            // 4. PerformanceManager (New)
            if (game.GetComponent<PerformanceManager>() == null)
            {
                Log.Message("[The Second Seat] Injecting PerformanceManager...");
                game.components.Add(new PerformanceManager(game));
            }

            // 5. SemanticRadarSystem (New)
            if (game.GetComponent<TheSecondSeat.Monitoring.SemanticRadarSystem>() == null)
            {
                Log.Message("[The Second Seat] Injecting SemanticRadarSystem...");
                game.components.Add(new TheSecondSeat.Monitoring.SemanticRadarSystem(game));
            }
        }
    }
}
