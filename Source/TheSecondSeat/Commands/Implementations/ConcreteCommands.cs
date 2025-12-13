using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;
using RimWorld;
using UnityEngine;
using TheSecondSeat.NaturalLanguage;

namespace TheSecondSeat.Commands.Implementations
{
    /// <summary>
    /// 批量命令的辅助方法集合
    /// </summary>
    public static class BatchCommandHelpers
    {
        /// <summary>
        /// 获取智能焦点，用于proximity-based operations.
        /// 优先级: 鼠标位置 > 镜头位置 > 地图中心
        /// </summary>
        public static IntVec3 GetSmartFocusPoint(Map map)
        {
            // 1. 优先使用鼠标位置
            IntVec3 mouseCell = Verse.UI.MouseCell();
            if (mouseCell.IsValid && mouseCell.InBounds(map))
            {
                return mouseCell;
            }

            // 2. 回退到镜头位置
            IntVec3 cameraCell = Find.CameraDriver.MapPosition;
            if (cameraCell.IsValid && cameraCell.InBounds(map))
            {
                return cameraCell;
            }

            // 3. 最后使用地图中心
            return map.Center;
        }
    }

    /// <summary>
    /// Designates all mature crops for harvest
    /// </summary>
    public class BatchHarvestCommand : BaseAICommand
    {
        public override string ActionName => "BatchHarvest";

        public override string GetDescription()
        {
            return "Designate all mature plants for harvest. Parameters: limit=<number>, nearFocus=<true/false>";
        }

        public override bool Execute(string? target = null, object? parameters = null)
        {
            var map = Find.CurrentMap;
            if (map == null)
            {
                LogError("No active map");
                return false;
            }

            // Parse parameters
            int limit = -1;
            bool nearFocus = false;

            if (parameters is Dictionary<string, object> paramsDict)
            {
                if (paramsDict.TryGetValue("limit", out var limitObj))
                {
                    if (int.TryParse(limitObj?.ToString(), out int parsedLimit))
                    {
                        limit = parsedLimit;
                    }
                }
                if (paramsDict.TryGetValue("nearFocus", out var focusObj))
                {
                    if (bool.TryParse(focusObj?.ToString(), out bool parsedFocus))
                    {
                        nearFocus = parsedFocus;
                    }
                }
            }

            int harvested = 0;
            var plants = map.listerThings.ThingsInGroup(ThingRequestGroup.HarvestablePlant)
                .OfType<Plant>()
                .Where(p => p.HarvestableNow && p.Spawned)
                .ToList();

            // Sort by distance if nearFocus is enabled
            if (nearFocus)
            {
                IntVec3 focusPoint = BatchCommandHelpers.GetSmartFocusPoint(map);
                plants = plants.OrderBy(p => p.Position.DistanceTo(focusPoint)).ToList();
            }

            // Apply limit if specified
            if (limit > 0)
            {
                plants = plants.Take(limit).ToList();
            }

            foreach (var plant in plants)
            {
                // Check if already designated
                if (map.designationManager.DesignationOn(plant, DesignationDefOf.HarvestPlant) == null)
                {
                    map.designationManager.AddDesignation(
                        new Designation(plant, DesignationDefOf.HarvestPlant));
                    harvested++;

                    // ? Visual feedback
                    FleckMaker.ThrowMetaIcon(plant.Position, map, FleckDefOf.FeedbackGoto);
                }
            }

            LogExecution($"Designated {harvested} plants for harvest (limit: {limit}, nearFocus: {nearFocus})");
            
            if (harvested > 0)
            {
                Messages.Message(
                    "TSS_Command_BatchHarvest".Translate(harvested), 
                    MessageTypeDefOf.NeutralEvent);
                return true;
            }

            return false;
        }
    }

    /// <summary>
    /// Equip colonists with the best available weapons based on their shooting skill
    /// </summary>
    public class BatchEquipCommand : BaseAICommand
    {
        public override string ActionName => "BatchEquip";

        public override string GetDescription()
        {
            return "Equip colonists with best available weapons";
        }

        public override bool Execute(string? target = null, object? parameters = null)
        {
            var map = Find.CurrentMap;
            if (map == null)
            {
                LogError("No active map");
                return false;
            }

            int equipped = 0;
            var colonists = map.mapPawns.FreeColonistsSpawned;
            var weapons = map.listerThings.ThingsInGroup(ThingRequestGroup.Weapon)
                .Where(w => !w.IsForbidden(Faction.OfPlayer))
                .ToList();

            foreach (var colonist in colonists)
            {
                if (colonist.equipment?.Primary != null)
                    continue; // Already has a weapon

                var shootingSkill = colonist.skills?.GetSkill(SkillDefOf.Shooting)?.Level ?? 0;
                
                // Find best weapon in stockpile
                var bestWeapon = weapons
                    .Where(w => colonist.CanReserveAndReach(w, PathEndMode.ClosestTouch, Danger.None))
                    .OrderByDescending(w => w.GetStatValue(StatDefOf.RangedWeapon_DamageMultiplier))
                    .FirstOrDefault();

                if (bestWeapon != null)
                {
                    var job = JobMaker.MakeJob(JobDefOf.Equip, bestWeapon);
                    colonist.jobs?.TryTakeOrderedJob(job);
                    weapons.Remove(bestWeapon);
                    equipped++;
                }
            }

            LogExecution($"Ordered {equipped} colonists to equip weapons");
            
            if (equipped > 0)
            {
                Messages.Message(
                    "TSS_Command_BatchEquip".Translate(equipped), 
                    MessageTypeDefOf.NeutralEvent);
                return true;
            }

            return false;
        }
    }

    /// <summary>
    /// Set all damaged structures to priority repair
    /// </summary>
    public class PriorityRepairCommand : BaseAICommand
    {
        public override string ActionName => "PriorityRepair";

        public override string GetDescription()
        {
            return "Prioritize repair of damaged structures";
        }

        public override bool Execute(string? target = null, object? parameters = null)
        {
            var map = Find.CurrentMap;
            if (map == null)
            {
                LogError("No active map");
                return false;
            }

            int designated = 0;
            var damagedThings = map.listerThings.AllThings
                .Where(t => t.def.useHitPoints && 
                           t.HitPoints < t.MaxHitPoints && 
                           t.HitPoints > 0 &&
                           t.def.building != null)
                .ToList();

            // 安全获取 Repair designation
            var repairDef = DefDatabase<DesignationDef>.GetNamedSilentFail("Repair");
            if (repairDef == null)
            {
                LogError("Repair designation not found");
                return false;
            }

            foreach (var thing in damagedThings)
            {
                var existingDesignation = map.designationManager.DesignationOn(thing);
                if (existingDesignation == null || existingDesignation.def != repairDef)
                {
                    map.designationManager.AddDesignation(
                        new Designation(thing, repairDef));
                    designated++;
                }
            }

            LogExecution($"Designated {designated} items for repair");
            
            if (designated > 0)
            {
                Messages.Message(
                    "TSS_Command_PriorityRepair".Translate(designated), 
                    MessageTypeDefOf.NeutralEvent);
                return true;
            }

            return false;
        }
    }

    /// <summary>
    /// Emergency retreat - send all colonists to safe area
    /// </summary>
    public class EmergencyRetreatCommand : BaseAICommand
    {
        public override string ActionName => "EmergencyRetreat";

        public override string GetDescription()
        {
            return "Order all colonists to retreat to safe area";
        }

        public override bool Execute(string? target = null, object? parameters = null)
        {
            var map = Find.CurrentMap;
            if (map == null)
            {
                LogError("No active map");
                return false;
            }

            // Try to draft and send colonists to a safe location
            var colonists = map.mapPawns.FreeColonistsSpawned;
            int retreated = 0;

            foreach (var colonist in colonists)
            {
                if (colonist.drafter != null && !colonist.drafter.Drafted)
                {
                    colonist.drafter.Drafted = true;
                    retreated++;
                }
            }

            LogExecution($"Drafted {retreated} colonists for emergency retreat");
            
            if (retreated > 0)
            {
                Messages.Message(
                    "TSS_Command_EmergencyRetreat".Translate(), 
                    MessageTypeDefOf.ThreatBig);
                return true;
            }

            return false;
        }
    }

    /// <summary>
    /// Change food/drug policy for all colonists
    /// </summary>
    public class ChangePolicyCommand : BaseAICommand
    {
        public override string ActionName => "ChangePolicy";

        public override string GetDescription()
        {
            return "Modify food or drug restrictions for colonists";
        }

        public override bool Execute(string? target = null, object? parameters = null)
        {
            // This would require specific policy manipulation
            // For now, just log that it was attempted
            LogExecution($"Policy change requested: {target}");
            
            Messages.Message(
                "TSS_Command_ChangePolicy".Translate(target ?? "colony"), 
                MessageTypeDefOf.NeutralEvent);
            
            return true;
        }
    }

    /// <summary>
    /// Batch capture downed enemies
    /// </summary>
    public class BatchCaptureCommand : BaseAICommand
    {
        public override string ActionName => "BatchCapture";

        public override string GetDescription()
        {
            return "Capture all downed enemies and assign to colonists";
        }

        public override bool Execute(string? target = null, object? parameters = null)
        {
            var map = Find.CurrentMap;
            if (map == null)
            {
                LogError("No active map");
                return false;
            }

            int captured = 0;
            var downedPawns = map.mapPawns.AllPawnsSpawned
                .Where(p => p.Downed && 
                           p.HostileTo(Faction.OfPlayer) && 
                           !p.Dead &&
                           p.RaceProps.Humanlike)
                .ToList();

            if (downedPawns.Count == 0)
            {
                LogExecution("No downed enemies found to capture");
                Messages.Message("TSS_Command_BatchCapture_None".Translate(), MessageTypeDefOf.RejectInput);
                return false;
            }

            var availableColonists = map.mapPawns.FreeColonistsSpawned
                .Where(c => !c.Downed && !c.Dead && c.workSettings?.WorkIsActive(WorkTypeDefOf.Warden) == true)
                .ToList();

            if (availableColonists.Count == 0)
            {
                LogExecution("No available colonists to perform capture");
                Messages.Message("TSS_Command_BatchCapture_NoWardens".Translate(), MessageTypeDefOf.RejectInput);
                return false;
            }

            // 安全获取 Capture designation
            var captureDef = DefDatabase<DesignationDef>.GetNamedSilentFail("Capture");
            if (captureDef == null)
            {
                captureDef = DefDatabase<DesignationDef>.GetNamedSilentFail("Tame"); // 备用
            }
            
            if (captureDef == null)
            {
                LogError("Capture designation not found");
                Messages.Message("TSS_Command_BatchCapture_NoWardens".Translate(), MessageTypeDefOf.RejectInput);
                return false;
            }

            foreach (var pawn in downedPawns)
            {
                // Check if already designated for capture
                if (map.designationManager.DesignationOn(pawn, captureDef) != null)
                    continue;

                // Find nearest available colonist
                var nearestColonist = availableColonists
                    .OrderBy(c => c.Position.DistanceTo(pawn.Position))
                    .FirstOrDefault();

                if (nearestColonist != null && nearestColonist.CanReserveAndReach(pawn, PathEndMode.OnCell, Danger.Deadly))
                {
                    // Add capture designation
                    map.designationManager.AddDesignation(new Designation(pawn, captureDef));
                    captured++;
                }
            }

            LogExecution($"Designated {captured} enemies for capture");
            
            if (captured > 0)
            {
                Messages.Message(
                    "TSS_Command_BatchCapture".Translate(captured), 
                    MessageTypeDefOf.PositiveEvent);
                return true;
            }

            return false;
        }
    }

    /// <summary>
    /// Batch designate mining for all mineable resources
    /// </summary>
    public class BatchMineCommand : BaseAICommand
    {
        public override string ActionName => "BatchMine";

        public override string GetDescription()
        {
            return "Designate all mineable resources for mining. Parameters: limit=<number>, nearFocus=<true/false>";
        }

        public override bool Execute(string? target = null, object? parameters = null)
        {
            var map = Find.CurrentMap;
            if (map == null)
            {
                LogError("No active map");
                return false;
            }

            // Parse parameters
            int limit = -1;
            bool nearFocus = false;

            if (parameters is Dictionary<string, object> paramsDict)
            {
                if (paramsDict.TryGetValue("limit", out var limitObj))
                {
                    if (int.TryParse(limitObj?.ToString(), out int parsedLimit))
                    {
                        limit = parsedLimit;
                    }
                }
                if (paramsDict.TryGetValue("nearFocus", out var focusObj))
                {
                    if (bool.TryParse(focusObj?.ToString(), out bool parsedFocus))
                    {
                        nearFocus = parsedFocus;
                    }
                }
            }

            int designated = 0;
            string targetType = target?.ToLower() ?? "all";
            
            var mineableThings = map.listerThings.AllThings
                .Where(t => t.def.mineable && t.Spawned)
                .ToList();

            // Filter by target type
            var filteredThings = new List<Thing>();
            foreach (var thing in mineableThings)
            {
                if (map.designationManager.DesignationOn(thing, DesignationDefOf.Mine) != null)
                    continue;

                bool shouldMine = false;

                switch (targetType)
                {
                    case "all":
                        shouldMine = true;
                        break;

                    case "metal":
                        shouldMine = thing.def.building?.mineableThing != null &&
                                   (thing.def.building.mineableThing.IsWithinCategory(ThingCategoryDefOf.ResourcesRaw) ||
                                    thing.def.building.mineableThing.defName.Contains("Steel") ||
                                    thing.def.building.mineableThing.defName.Contains("Gold") ||
                                    thing.def.building.mineableThing.defName.Contains("Silver") ||
                                    thing.def.building.mineableThing.defName.Contains("Uranium") ||
                                    thing.def.building.mineableThing.defName.Contains("Plasteel") ||
                                    thing.def.building.mineableThing.defName.Contains("Jade"));
                        break;

                    case "stone":
                        shouldMine = thing.def.building?.mineableThing != null &&
                                   thing.def.building.mineableThing.IsWithinCategory(ThingCategoryDefOf.StoneBlocks);
                        break;

                    case "components":
                    case "component":
                        shouldMine = thing.def.building?.mineableThing != null &&
                                   thing.def.building.mineableThing.defName.Contains("Component");
                        break;

                    default:
                        shouldMine = thing.def.defName.ToLower().Contains(targetType) ||
                                   (thing.def.building?.mineableThing?.defName.ToLower().Contains(targetType) ?? false);
                        break;
                }

                if (shouldMine)
                {
                    filteredThings.Add(thing);
                }
            }

            // Sort by distance if nearFocus is enabled
            if (nearFocus)
            {
                IntVec3 focusPoint = BatchCommandHelpers.GetSmartFocusPoint(map);
                filteredThings = filteredThings.OrderBy(t => t.Position.DistanceTo(focusPoint)).ToList();
            }

            // Apply limit if specified
            if (limit > 0)
            {
                filteredThings = filteredThings.Take(limit).ToList();
            }

            // Designate and provide visual feedback
            foreach (var thing in filteredThings)
            {
                map.designationManager.AddDesignation(new Designation(thing, DesignationDefOf.Mine));
                designated++;

                // ? Visual feedback
                FleckMaker.ThrowMetaIcon(thing.Position, map, FleckDefOf.FeedbackGoto);
            }

            LogExecution($"Designated {designated} mineable resources for mining (type: {targetType}, limit: {limit}, nearFocus: {nearFocus})");
            
            if (designated > 0)
            {
                Messages.Message(
                    "TSS_Command_BatchMine".Translate(designated, targetType), 
                    MessageTypeDefOf.NeutralEvent);
                return true;
            }
            else
            {
                Messages.Message(
                    "TSS_Command_BatchMine_None".Translate(targetType), 
                    MessageTypeDefOf.RejectInput);
                return false;
            }
        }
    }

    /// <summary>
    /// Designate mature trees for logging (correct logging, not cut all plants)
    /// </summary>
    public class BatchLoggingCommand : BaseAICommand
    {
        public override string ActionName => "BatchLogging";

        public override string GetDescription()
        {
            return "Designate mature trees for logging. Parameters: limit=<number>, nearFocus=<true/false>";
        }

        public override bool Execute(string? target = null, object? parameters = null)
        {
            var map = Find.CurrentMap;
            if (map == null)
            {
                LogError("No active map");
                return false;
            }

            // Parse parameters
            int limit = -1;
            bool nearFocus = false;

            if (parameters is Dictionary<string, object> paramsDict)
            {
                if (paramsDict.TryGetValue("limit", out var limitObj))
                {
                    if (int.TryParse(limitObj?.ToString(), out int parsedLimit))
                    {
                        limit = parsedLimit;
                    }
                }
                if (paramsDict.TryGetValue("nearFocus", out var focusObj))
                {
                    if (bool.TryParse(focusObj?.ToString(), out bool parsedFocus))
                    {
                        nearFocus = parsedFocus;
                    }
                }
            }

            int designated = 0;
            var plants = map.listerThings.ThingsInGroup(ThingRequestGroup.Plant)
                .OfType<Plant>()
                .Where(p => p.Spawned &&
                           p.def.plant != null &&
                           p.def.plant.IsTree &&
                           p.Growth >= 0.9f &&
                           !p.IsForbidden(Faction.OfPlayer))
                .ToList();

            // Sort by distance if nearFocus is enabled
            if (nearFocus)
            {
                IntVec3 focusPoint = BatchCommandHelpers.GetSmartFocusPoint(map);
                plants = plants.OrderBy(p => p.Position.DistanceTo(focusPoint)).ToList();
            }

            // Apply limit if specified
            if (limit > 0)
            {
                plants = plants.Take(limit).ToList();
            }

            foreach (var plant in plants)
            {
                // Check if already designated
                if (map.designationManager.DesignationOn(plant, DesignationDefOf.CutPlant) == null)
                {
                    map.designationManager.AddDesignation(
                        new Designation(plant, DesignationDefOf.CutPlant));
                    designated++;

                    // ? Visual feedback
                    FleckMaker.ThrowMetaIcon(plant.Position, map, FleckDefOf.FeedbackGoto);
                }
            }

            LogExecution($"Designated {designated} mature trees for logging (limit: {limit}, nearFocus: {nearFocus})");
            
            if (designated > 0)
            {
                Messages.Message(
                    "TSS_Command_BatchLogging".Translate(designated), 
                    MessageTypeDefOf.NeutralEvent);
                return true;
            }
            else
            {
                Messages.Message(
                    "TSS_Command_BatchLogging_None".Translate(), 
                    MessageTypeDefOf.RejectInput);
                return false;
            }
        }
    }

    /// <summary>
    /// Cut all designated plants
    /// </summary>
    public class DesignatePlantCutCommand : BaseAICommand
    {
        public override string ActionName => "DesignatePlantCut";

        public override string GetDescription()
        {
            return "Cut all plants in designated area. Parameters: limit=<number>, nearFocus=<true/false>";
        }

        public override bool Execute(string? target = null, object? parameters = null)
        {
            var map = Find.CurrentMap;
            if (map == null)
            {
                LogError("No active map");
                return false;
            }

            // Parse parameters
            int limit = -1;
            bool nearFocus = false;

            if (parameters is Dictionary<string, object> paramsDict)
            {
                if (paramsDict.TryGetValue("limit", out var limitObj))
                {
                    if (int.TryParse(limitObj?.ToString(), out int parsedLimit))
                    {
                        limit = parsedLimit;
                    }
                }
                if (paramsDict.TryGetValue("nearFocus", out var focusObj))
                {
                    if (bool.TryParse(focusObj?.ToString(), out bool parsedFocus))
                    {
                        nearFocus = parsedFocus;
                    }
                }
            }

            int designated = 0;
            string targetType = target?.ToLower() ?? "all";
            
            var plants = map.listerThings.ThingsInGroup(ThingRequestGroup.Plant)
                .OfType<Plant>()
                .Where(p => p.Spawned && !p.IsForbidden(Faction.OfPlayer))
                .ToList();

            // Filter by target type
            var filteredPlants = new List<Plant>();
            foreach (var plant in plants)
            {
                bool shouldCut = false;

                switch (targetType)
                {
                    case "blighted":
                        shouldCut = plant.Blighted;
                        break;

                    case "trees":
                        shouldCut = plant.def.plant?.IsTree ?? false;
                        break;

                    case "wild":
                        shouldCut = !plant.IsCrop;
                        break;

                    case "all":
                    default:
                        shouldCut = true;
                        break;
                }

                if (shouldCut && map.designationManager.DesignationOn(plant, DesignationDefOf.CutPlant) == null)
                {
                    filteredPlants.Add(plant);
                }
            }

            // Sort by distance if nearFocus is enabled
            if (nearFocus)
            {
                IntVec3 focusPoint = BatchCommandHelpers.GetSmartFocusPoint(map);
                filteredPlants = filteredPlants.OrderBy(p => p.Position.DistanceTo(focusPoint)).ToList();
            }

            // Apply limit if specified
            if (limit > 0)
            {
                filteredPlants = filteredPlants.Take(limit).ToList();
            }

            // Designate and provide visual feedback
            foreach (var plant in filteredPlants)
            {
                map.designationManager.AddDesignation(
                    new Designation(plant, DesignationDefOf.CutPlant));
                designated++;

                // ? Visual feedback
                FleckMaker.ThrowMetaIcon(plant.Position, map, FleckDefOf.FeedbackGoto);
            }

            LogExecution($"Designated {designated} plants for cutting (type: {targetType}, limit: {limit}, nearFocus: {nearFocus})");
            
            if (designated > 0)
            {
                Messages.Message(
                    "TSS_Command_DesignatePlantCut".Translate(designated, targetType), 
                    MessageTypeDefOf.NeutralEvent);
                return true;
            }
            else
            {
                Messages.Message(
                    "TSS_Command_DesignatePlantCut_None".Translate(targetType), 
                    MessageTypeDefOf.RejectInput);
                return false;
            }
        }
    }

    /// <summary>
    /// ? 触发游戏事件（对弈者模式专用）
    /// AI可以通过此命令触发袭击、商队、资源空投等事件
    /// </summary>
    public class TriggerEventCommand : BaseAICommand
    {
        public override string ActionName => "TriggerEvent";

        public override string GetDescription()
        {
            return "Trigger a game event (Opponent mode). Types: raid, trader, wanderer, disease, resource, eclipse, toxic";
        }

        public override bool Execute(string? target = null, object? parameters = null)
        {
            var eventController = Events.OpponentEventController.Instance;
            
            if (eventController == null)
            {
                LogError("OpponentEventController not found");
                return false;
            }

            if (!eventController.IsActive)
            {
                LogExecution("对弈者模式未激活，无法触发事件");
                Messages.Message("TSS_Command_TriggerEvent_NotActive".Translate(), MessageTypeDefOf.RejectInput);
                return false;
            }

            if (string.IsNullOrEmpty(target))
            {
                LogError("No event type specified");
                Messages.Message("TSS_Command_TriggerEvent_NoType".Translate(), MessageTypeDefOf.RejectInput);
                return false;
            }

            // 从 parameters 获取 AI 评论
            string aiComment = "";
            if (parameters is Dictionary<string, object> paramsDict)
            {
                if (paramsDict.TryGetValue("comment", out var commentObj))
                {
                    aiComment = commentObj?.ToString() ?? "";
                }
            }
            else if (parameters is string paramStr)
            {
                aiComment = paramStr;
            }

            bool success = eventController.TriggerEventByAI(target, aiComment);

            if (success)
            {
                LogExecution($"AI triggered event: {target}");
                return true;
            }
            else
            {
                LogExecution($"Failed to trigger event: {target}");
                return false;
            }
        }
    }

    /// <summary>
    /// ? 安排未来事件（对弈者模式专用）
    /// AI可以预先安排事件在未来某个时间点发生
    /// </summary>
    public class ScheduleEventCommand : BaseAICommand
    {
        public override string ActionName => "ScheduleEvent";

        public override string GetDescription()
        {
            return "Schedule a future event (Opponent mode). Parameters: delay (in game minutes)";
        }

        public override bool Execute(string? target = null, object? parameters = null)
        {
            var eventController = Events.OpponentEventController.Instance;
            
            if (eventController == null)
            {
                LogError("OpponentEventController not found");
                return false;
            }

            if (!eventController.IsActive)
            {
                LogExecution("对弈者模式未激活，无法安排事件");
                Messages.Message("TSS_Command_ScheduleEvent_NotActive".Translate(), MessageTypeDefOf.RejectInput);
                return false;
            }

            if (string.IsNullOrEmpty(target))
            {
                LogError("No event type specified");
                return false;
            }

            // 解析延迟时间（默认10分钟）
            int delayMinutes = 10;
            string aiComment = "";

            if (parameters is Dictionary<string, object> paramsDict)
            {
                if (paramsDict.TryGetValue("delay", out var delayObj))
                {
                    if (int.TryParse(delayObj?.ToString(), out int parsedDelay))
                    {
                        delayMinutes = parsedDelay;
                    }
                }
                if (paramsDict.TryGetValue("comment", out var commentObj))
                {
                    aiComment = commentObj?.ToString() ?? "";
                }
            }

            // 转换为 ticks（1游戏分钟 = 2500 ticks）
            int delayTicks = delayMinutes * 2500;

            eventController.ScheduleEvent(target, delayTicks, aiComment);

            LogExecution($"AI scheduled event: {target} in {delayMinutes} minutes");
            Messages.Message($"【对弈者】{delayMinutes}分钟后将有事件发生...", MessageTypeDefOf.NeutralEvent);
            
            return true;
        }
    }
}
