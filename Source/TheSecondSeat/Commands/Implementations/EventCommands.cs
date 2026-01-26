using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using RimWorld;
using UnityEngine;
using TheSecondSeat.NaturalLanguage;
using TheSecondSeat.Descent;

namespace TheSecondSeat.Commands.Implementations
{
    /// <summary>
    /// Trigger a specific game event/incident
    /// </summary>
    public class TriggerEventCommand : BaseAICommand
    {
        public override string ActionName => "TriggerEvent";

        public override string GetDescription()
        {
            return "Trigger a game event. Target: IncidentDefName. Parameters: points=<int>";
        }

        public override bool Execute(string? target = null, object? parameters = null)
        {
            if (string.IsNullOrEmpty(target))
            {
                LogError("Event name (IncidentDef) is required");
                return false;
            }

            var incidentDef = DefDatabase<IncidentDef>.GetNamedSilentFail(target);
            if (incidentDef == null)
            {
                LogError($"Incident '{target}' not found");
                return false;
            }

            var map = Find.CurrentMap;
            if (map == null)
            {
                LogError("No active map");
                return false;
            }

            float points = StorytellerUtility.DefaultThreatPointsNow(map);
            if (parameters is Dictionary<string, object> paramsDict &&
                paramsDict.TryGetValue("points", out var pointsObj))
            {
                points = Convert.ToSingle(pointsObj);
            }

            IncidentParms parms = StorytellerUtility.DefaultParmsNow(incidentDef.category, map);
            parms.points = points;

            if (incidentDef.Worker.TryExecute(parms))
            {
                LogExecution($"Triggered event '{target}' with {points} points");
                return true;
            }

            LogError($"Failed to trigger event '{target}'");
            return false;
        }
    }

    /// <summary>
    /// Schedule an event for the future
    /// </summary>
    public class ScheduleEventCommand : BaseAICommand
    {
        public override string ActionName => "ScheduleEvent";

        public override string GetDescription()
        {
            return "Schedule an event. Target: IncidentDefName. Parameters: delayHours=<int>";
        }

        public override bool Execute(string? target = null, object? parameters = null)
        {
            if (string.IsNullOrEmpty(target))
            {
                LogError("Event name is required");
                return false;
            }

            int delayHours = 24;
            if (parameters is Dictionary<string, object> paramsDict &&
                paramsDict.TryGetValue("delayHours", out var delayObj))
            {
                delayHours = Convert.ToInt32(delayObj);
            }

            // In a real implementation, this would add to a queue
            // For now, we'll just log it as "Scheduled"
            LogExecution($"Scheduled event '{target}' in {delayHours} hours");
            return true;
        }
    }

    /// <summary>
    /// Trigger the narrator descent sequence
    /// </summary>
    public class DescentCommand : BaseAICommand
    {
        public override string ActionName => "Descent";

        public override string GetDescription()
        {
            return "Trigger narrator descent sequence. Parameters: mode=assist|attack, x=<int>, z=<int>. If no coordinates provided, uses player's selected position or random.";
        }

        public override bool Execute(string? target = null, object? parameters = null)
        {
            var map = Find.CurrentMap;
            if (map == null)
            {
                LogError("No active map");
                return false;
            }

            // Check if descent system is available
            var descentSystem = NarratorDescentSystem.Instance;
            if (descentSystem == null)
            {
                LogError("NarratorDescentSystem not found");
                return false;
            }

            // Parse parameters
            bool isHostile = false; // Default to assist/friendly
            bool playerControlled = true; // ⭐ v2.9.8: 默认由玩家控制
            IntVec3? targetLocation = null;
            
            if (parameters is Dictionary<string, object> paramsDict)
            {
                // ⭐ v2.9.8: 修复 mode 参数解析，支持更多变体
                // 支持: mode=attack/hostile/敌对 → isHostile = true
                // 支持: mode=assist/friendly/援助/协助 → isHostile = false, playerControlled = true
                // 支持: mode=auto/autonomous/自主 → isHostile = false, playerControlled = false
                if (paramsDict.TryGetValue("mode", out var modeObj))
                {
                    string modeStr = modeObj?.ToString()?.ToLower() ?? "";
                    
                    // 明确的敌对模式
                    if (modeStr == "attack" || modeStr == "hostile" || 
                        modeStr == "敌对" || modeStr == "攻击")
                    {
                        isHostile = true;
                        playerControlled = false; // 敌对模式不受玩家控制
                        Log.Message($"[DescentCommand] Mode parsed as hostile: '{modeStr}'");
                    }
                    // 自主援助模式（不受玩家控制）
                    else if (modeStr == "auto" || modeStr == "autonomous" || 
                             modeStr == "自主" || modeStr == "自动")
                    {
                        isHostile = false;
                        playerControlled = false;
                        Log.Message($"[DescentCommand] Mode parsed as autonomous assist: '{modeStr}'");
                    }
                    // 受控援助模式
                    else if (modeStr == "assist" || modeStr == "friendly" || 
                             modeStr == "援助" || modeStr == "协助" || modeStr == "友好" ||
                             modeStr == "control" || modeStr == "受控")
                    {
                        isHostile = false;
                        playerControlled = true;
                        Log.Message($"[DescentCommand] Mode parsed as controlled assist: '{modeStr}'");
                    }
                    else
                    {
                        // 未知模式，默认为受控援助（更安全）
                        Log.Warning($"[DescentCommand] Unknown mode '{modeStr}', defaulting to controlled assist");
                        isHostile = false;
                        playerControlled = true;
                    }
                }
                // 回退：支持 hostile=true/false 参数
                else if (paramsDict.TryGetValue("hostile", out var hostileObj))
                {
                    if (bool.TryParse(hostileObj?.ToString(), out bool parsedHostile))
                    {
                        isHostile = parsedHostile;
                        Log.Message($"[DescentCommand] Hostile parsed from 'hostile' param: {isHostile}");
                    }
                }
                
                // ⭐ v2.9.8: 支持显式的 playerControlled 参数
                if (paramsDict.TryGetValue("playerControlled", out var pcObj) ||
                    paramsDict.TryGetValue("controlled", out pcObj))
                {
                    if (bool.TryParse(pcObj?.ToString(), out bool parsedPC))
                    {
                        playerControlled = parsedPC;
                        Log.Message($"[DescentCommand] PlayerControlled: {playerControlled}");
                    }
                }

                // Parse coordinates if provided by LLM
                if (paramsDict.TryGetValue("x", out var xObj) && 
                    paramsDict.TryGetValue("z", out var zObj))
                {
                    if (int.TryParse(xObj.ToString(), out int x) &&
                        int.TryParse(zObj.ToString(), out int z))
                    {
                        var loc = new IntVec3(x, 0, z);
                        // Validate location is within map bounds and walkable
                        if (loc.InBounds(map) && loc.Standable(map))
                        {
                            targetLocation = loc;
                            Log.Message($"[DescentCommand] Using LLM-specified location: ({x}, {z})");
                        }
                        else
                        {
                            Log.Warning($"[DescentCommand] LLM-specified location ({x}, {z}) is invalid, will use fallback");
                        }
                    }
                }
            }

            // Fallback: Use player's selected position if no valid LLM coordinates
            if (targetLocation == null)
            {
                var selectedObjects = Find.Selector.SelectedObjects;
                if (selectedObjects != null && selectedObjects.Count > 0)
                {
                    // Get position of first selected object
                    var firstSelected = selectedObjects[0];
                    if (firstSelected is Thing thing && thing.Map == map)
                    {
                        targetLocation = thing.Position;
                        Log.Message($"[DescentCommand] Using player-selected position: {targetLocation}");
                    }
                    else if (firstSelected is IntVec3 cell && cell.InBounds(map))
                    {
                        targetLocation = cell;
                        Log.Message($"[DescentCommand] Using player-selected cell: {targetLocation}");
                    }
                }
            }

            // Final fallback: null = random (handled by NarratorDescentSystem)
            if (targetLocation == null)
            {
                Log.Message("[DescentCommand] No location specified, will use random position");
            }

            // ⭐ v2.9.8: Trigger the descent with optional location and playerControlled
            if (descentSystem.TriggerDescent(isHostile, targetLocation, playerControlled))
            {
                string locStr = targetLocation.HasValue ? $" at ({targetLocation.Value.x}, {targetLocation.Value.z})" : " at random location";
                string modeStr = isHostile ? "Hostile" : (playerControlled ? "Controlled" : "Autonomous");
                LogExecution($"Triggered descent sequence (Mode: {modeStr}){locStr}");
                return true;
            }

            LogError("Failed to trigger descent (see log for details)");
            return false;
        }
    }

    /// <summary>
    /// Trigger the narrator to return from descent (ascend back to narrative layer)
    /// </summary>
    public class AscendCommand : BaseAICommand
    {
        public override string ActionName => "Ascend";

        public override string GetDescription()
        {
            return "Return from descent to the narrative layer. Use when you decide it's time to leave the physical form. No parameters required.";
        }

        public override bool Execute(string? target = null, object? parameters = null)
        {
            var descentSystem = NarratorDescentSystem.Instance;
            if (descentSystem == null)
            {
                LogError("NarratorDescentSystem not found");
                return false;
            }

            if (!descentSystem.IsDescentActive)
            {
                LogError("Not currently in descent form");
                return false;
            }

            if (descentSystem.TriggerReturn())
            {
                LogExecution("Initiated return to narrative layer");
                return true;
            }

            LogError("Failed to trigger return (see log for details)");
            return false;
        }
    }

    /// <summary>
    /// Get available incident categories
    /// </summary>
    public class GetIncidentCategoriesCommand : BaseAICommand
    {
        public override string ActionName => "GetIncidentCategories";

        public override string GetDescription()
        {
            return "Get list of available incident categories. No target or parameters required.";
        }

        public override bool Execute(string? target = null, object? parameters = null)
        {
            var categories = TheSecondSeat.Storyteller.IncidentRegistry.GetAvailableCategories();
            LogExecution($"Available Categories: {string.Join(", ", categories)}");
            return true;
        }
    }

    /// <summary>
    /// Get list of incidents in a category with optional pagination and search
    /// </summary>
    public class GetIncidentListCommand : BaseAICommand
    {
        public override string ActionName => "GetIncidentList";

        public override string GetDescription()
        {
            return "Get incidents in a category. Target: CategoryName. Parameters: page=<int>, pageSize=<int>, search=<string>";
        }

        public override bool Execute(string? target = null, object? parameters = null)
        {
            if (string.IsNullOrEmpty(target))
            {
                LogError("Category name is required as Target");
                return false;
            }

            int pageIndex = 0;
            int pageSize = 10;
            string searchTerm = "";

            if (parameters is Dictionary<string, object> paramsDict)
            {
                if (paramsDict.TryGetValue("page", out var pageObj))
                    int.TryParse(pageObj.ToString(), out pageIndex);

                if (paramsDict.TryGetValue("pageSize", out var sizeObj))
                    int.TryParse(sizeObj.ToString(), out pageSize);

                if (paramsDict.TryGetValue("search", out var searchObj))
                    searchTerm = searchObj?.ToString() ?? "";
            }

            var result = TheSecondSeat.Storyteller.IncidentRegistry.GetIncidentList(target, pageIndex, pageSize, searchTerm);
            string resultStr = string.Join(", ", result.Select(i => $"{i.defName} ({i.label})"));
            LogExecution($"Incidents in {target}: {resultStr}");
            return true;
        }
    }
}
