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
            // ✅ 线程诊断日志
            int threadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
            if (Prefs.DevMode)
            {
                Log.Message($"[BatchEquipCommand] Execute on Thread ID: {threadId}");
            }
            
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
    /// ⭐ v1.6.84: 修复 - RimWorld 没有 Repair designation，改用允许并分配建筑工修复
    /// </summary>
    public class PriorityRepairCommand : BaseAICommand
    {
        public override string ActionName => "PriorityRepair";

        public override string GetDescription()
        {
            return "Prioritize repair of damaged structures by unforbidding them and assigning constructors";
        }

        public override bool Execute(string? target = null, object? parameters = null)
        {
            var map = Find.CurrentMap;
            if (map == null)
            {
                LogError("No active map");
                return false;
            }

            int repairCount = 0;
            var damagedBuildings = map.listerThings.AllThings
                .Where(t => t.def.useHitPoints &&
                           t.HitPoints < t.MaxHitPoints &&
                           t.HitPoints > 0 &&
                           t.def.building != null &&
                           t.Spawned)
                .ToList();

            if (damagedBuildings.Count == 0)
            {
                LogExecution("No damaged buildings found");
                Messages.Message("TSS_Command_PriorityRepair_None".Translate(), MessageTypeDefOf.RejectInput);
                return false;
            }

            // ⭐ v1.6.84: RimWorld 自动修复未被禁止的建筑
            // 我们需要：1) 取消禁止 2) 查找可用建筑工 3) 创建修理工作
            foreach (var building in damagedBuildings)
            {
                // 1. 取消禁止
                if (building.IsForbidden(Faction.OfPlayer))
                {
                    building.SetForbidden(false, false);
                }
                
                // 2. 标记需要修复（通过取消任何阻止修复的状态）
                repairCount++;
                
                // 视觉反馈
                FleckMaker.ThrowMetaIcon(building.Position, map, FleckDefOf.FeedbackGoto);
            }

            // 3. 找建筑工来修复
            var constructors = map.mapPawns.FreeColonistsSpawned
                .Where(p => !p.Downed &&
                           !p.Dead &&
                           p.workSettings?.WorkIsActive(WorkTypeDefOf.Construction) == true)
                .OrderByDescending(p => p.skills?.GetSkill(SkillDefOf.Construction)?.Level ?? 0)
                .ToList();

            if (constructors.Count == 0)
            {
                LogExecution($"Found {repairCount} damaged buildings but no constructors available");
                Messages.Message("TSS_Command_PriorityRepair_NoConstructors".Translate(repairCount), MessageTypeDefOf.CautionInput);
                return repairCount > 0;
            }

            // 4. 为每个损坏的建筑分配最近的建筑工（建筑工会自动寻找修复工作）
            int assignedJobs = 0;
            foreach (var building in damagedBuildings.OrderByDescending(b => b.MaxHitPoints - b.HitPoints))
            {
                var nearestConstructor = constructors
                    .Where(c => c.CanReserveAndReach(building, PathEndMode.Touch, Danger.Some))
                    .OrderBy(c => c.Position.DistanceTo(building.Position))
                    .FirstOrDefault();

                if (nearestConstructor != null)
                {
                    // 尝试创建修复工作
                    var repairJob = JobMaker.MakeJob(JobDefOf.Repair, building);
                    if (nearestConstructor.jobs?.TryTakeOrderedJob(repairJob) == true)
                    {
                        assignedJobs++;
                        if (assignedJobs >= constructors.Count)
                            break; // 所有建筑工都已分配
                    }
                }
            }

            LogExecution($"Found {repairCount} damaged buildings, assigned {assignedJobs} repair jobs");
            
            Messages.Message(
                "TSS_Command_PriorityRepair".Translate(repairCount),
                MessageTypeDefOf.NeutralEvent);
            return true;
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
    /// Batch capture downed enemies
    /// ⭐ v1.6.84: 修复 - 使用 Job 而非 Designation 来俘虏敌人
    /// </summary>
    public class BatchCaptureCommand : BaseAICommand
    {
        public override string ActionName => "BatchCapture";

        public override string GetDescription()
        {
            return "Capture all downed enemies by assigning colonists to carry them to prison beds";
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
                // 也尝试没有看守工作但能搬运的殖民者
                availableColonists = map.mapPawns.FreeColonistsSpawned
                    .Where(c => !c.Downed && !c.Dead)
                    .ToList();
            }

            if (availableColonists.Count == 0)
            {
                LogExecution("No available colonists to perform capture");
                Messages.Message("TSS_Command_BatchCapture_NoWardens".Translate(), MessageTypeDefOf.RejectInput);
                return false;
            }

            // ⭐ v1.6.84: 查找可用的囚犯床
            var prisonBeds = map.listerBuildings.AllBuildingsColonistOfClass<Building_Bed>()
                .Where(b => b.ForPrisoners && !b.Medical && b.AnyUnoccupiedSleepingSlot)
                .ToList();

            if (prisonBeds.Count == 0)
            {
                LogExecution("No prison beds available");
                Messages.Message("TSS_Command_BatchCapture_NoBeds".Translate(), MessageTypeDefOf.CautionInput);
                // 继续执行，殖民者会尝试找地方放
            }

            // ⭐ v1.6.84: 使用 Capture Job 而非 Designation
            foreach (var pawn in downedPawns)
            {
                // 找最近的可用殖民者
                var nearestColonist = availableColonists
                    .Where(c => c.CanReserveAndReach(pawn, PathEndMode.ClosestTouch, Danger.Deadly))
                    .OrderBy(c => c.Position.DistanceTo(pawn.Position))
                    .FirstOrDefault();

                if (nearestColonist != null)
                {
                    // 创建俘虏工作
                    var captureJob = JobMaker.MakeJob(JobDefOf.Capture, pawn);
                    
                    // 如果有可用的囚犯床，指定目标床
                    var nearestBed = prisonBeds
                        .Where(b => b.AnyUnoccupiedSleepingSlot)
                        .OrderBy(b => b.Position.DistanceTo(pawn.Position))
                        .FirstOrDefault();
                    
                    if (nearestBed != null)
                    {
                        captureJob = JobMaker.MakeJob(JobDefOf.Capture, pawn, nearestBed);
                    }
                    
                    if (nearestColonist.jobs?.TryTakeOrderedJob(captureJob) == true)
                    {
                        captured++;
                        
                        // 从可用列表移除已分配的殖民者
                        availableColonists.Remove(nearestColonist);
                        
                        // 视觉反馈
                        FleckMaker.ThrowMetaIcon(pawn.Position, map, FleckDefOf.FeedbackGoto);
                        
                        if (availableColonists.Count == 0)
                            break;
                    }
                }
            }

            LogExecution($"Assigned {captured} capture jobs for downed enemies");
            
            if (captured > 0)
            {
                Messages.Message(
                    "TSS_Command_BatchCapture".Translate(captured),
                    MessageTypeDefOf.PositiveEvent);
                return true;
            }
            else
            {
                Messages.Message("TSS_Command_BatchCapture_Failed".Translate(), MessageTypeDefOf.RejectInput);
                return false;
            }
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
}