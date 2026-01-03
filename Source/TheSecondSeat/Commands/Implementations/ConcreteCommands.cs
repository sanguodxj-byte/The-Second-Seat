using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;
using RimWorld;
using UnityEngine;
using TheSecondSeat.NaturalLanguage;
using TheSecondSeat.Descent;

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

    /// <summary>
    /// ? 叙事者降临
    /// AI 触发实体化身降临
    /// </summary>
    public class DescentCommand : BaseAICommand
    {
        public override string ActionName => "Descent";

        public override string GetDescription()
        {
            return "Descend as an avatar to assist or interact. Parameters: mode=<assist/hostile>";
        }

        public override bool Execute(string? target = null, object? parameters = null)
        {
            var descentSystem = NarratorDescentSystem.Instance;
            
            if (descentSystem == null)
            {
                LogError("NarratorDescentSystem not found");
                return false;
            }

            // 检查冷却
            if (!descentSystem.CanDescendNow(out string reason))
            {
                LogExecution($"无法降临: {reason}");
                Messages.Message($"无法降临: {reason}", MessageTypeDefOf.RejectInput);
                return false;
            }

            // 解析模式
            string mode = "assist";
            if (parameters is Dictionary<string, object> paramsDict)
            {
                if (paramsDict.TryGetValue("mode", out var modeObj))
                {
                    mode = modeObj?.ToString()?.ToLower() ?? "assist";
                }
            }
            else if (target != null)
            {
                mode = target.ToLower();
            }

            bool isHostile = mode == "hostile";

            // 触发降临
            bool success = descentSystem.TriggerDescent(isHostile);

            if (success)
            {
                string typeStr = isHostile ? "敌对" : "协助";
                LogExecution($"AI triggered descent ({typeStr})");
                // 具体的降临消息由 NarratorDescentSystem 处理
                return true;
            }
            else
            {
                LogExecution("Failed to trigger descent");
                return false;
            }
        }
    }

    #region ? v1.6.40: 殖民者管理命令（从 GameActionExecutor 迁移）

    /// <summary>
    /// ? 征召/解除征召殖民者
    /// </summary>
    public class DraftPawnCommand : BaseAICommand
    {
        public override string ActionName => "DraftPawn";

        public override string GetDescription()
        {
            return "Draft or undraft colonists. Parameters: drafted=<true/false>, pawnName=<name>";
        }

        public override bool Execute(string? target = null, object? parameters = null)
        {
            var map = Find.CurrentMap;
            if (map == null)
            {
                LogError("No active map");
                return false;
            }

            // 解析参数
            bool shouldDraft = target?.ToLower() != "false" && target?.ToLower() != "undraft";
            string pawnName = "";

            if (parameters is Dictionary<string, object> paramsDict)
            {
                if (paramsDict.TryGetValue("pawnName", out var nameObj))
                    pawnName = nameObj?.ToString() ?? "";
                if (paramsDict.TryGetValue("scope", out var scopeObj))
                    pawnName = scopeObj?.ToString() ?? "";
            }

            int count = 0;
            var colonists = map.mapPawns.FreeColonistsSpawned;

            foreach (var colonist in colonists)
            {
                // 如果指定了名字，只处理匹配的殖民者
                if (!string.IsNullOrEmpty(pawnName) && pawnName != "All")
                {
                    if (colonist.Name.ToStringShort.IndexOf(pawnName, StringComparison.OrdinalIgnoreCase) < 0 &&
                        colonist.LabelShort.IndexOf(pawnName, StringComparison.OrdinalIgnoreCase) < 0)
                        continue;
                }

                if (colonist.drafter != null)
                {
                    if (shouldDraft && !colonist.drafter.Drafted)
                    {
                        colonist.drafter.Drafted = true;
                        count++;
                    }
                    else if (!shouldDraft && colonist.drafter.Drafted)
                    {
                        colonist.drafter.Drafted = false;
                        count++;
                    }
                }
            }

            string action = shouldDraft ? "征召" : "解除征召";
            LogExecution($"{action} {count} 名殖民者");

            if (count > 0)
            {
                Messages.Message($"已{action} {count} 名殖民者", MessageTypeDefOf.NeutralEvent);
                return true;
            }

            return false;
        }
    }

    /// <summary>
    /// ? 移动殖民者到指定位置
    /// </summary>
    public class MovePawnCommand : BaseAICommand
    {
        public override string ActionName => "MovePawn";

        public override string GetDescription()
        {
            return "Move a drafted colonist to a specific location. Parameters: x=<int>, z=<int>";
        }

        public override bool Execute(string? target = null, object? parameters = null)
        {
            var map = Find.CurrentMap;
            if (map == null)
            {
                LogError("No active map");
                return false;
            }

            string pawnName = target ?? "";
            if (string.IsNullOrEmpty(pawnName))
            {
                LogError("未指定殖民者名称");
                return false;
            }

            // 从 parameters 解析坐标
            int x = 0, z = 0;
            if (parameters is Dictionary<string, object> paramsDict)
            {
                if (paramsDict.TryGetValue("x", out var xObj))
                    int.TryParse(xObj?.ToString(), out x);
                if (paramsDict.TryGetValue("z", out var zObj))
                    int.TryParse(zObj?.ToString(), out z);
            }

            if (x == 0 && z == 0)
            {
                LogError("未指定目标坐标（需要x和z参数）");
                return false;
            }

            var targetPos = new IntVec3(x, 0, z);
            if (!targetPos.InBounds(map))
            {
                LogError($"目标位置 ({x}, {z}) 超出地图边界");
                return false;
            }

            // ⭐ v1.6.95: 模糊寻路 - 如果目标不可走，寻找附近可走的点
            if (!targetPos.Walkable(map))
            {
                if (!CellFinder.TryFindRandomCellNear(targetPos, map, 5, c => c.Walkable(map), out IntVec3 nearPos))
                {
                    LogError($"目标位置 ({x}, {z}) 及附近不可到达");
                    return false;
                }
                targetPos = nearPos;
            }

            // 查找殖民者 (忽略大小写)
            var colonist = map.mapPawns.FreeColonistsSpawned
                .FirstOrDefault(p => p.Name.ToStringShort.IndexOf(pawnName, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    p.LabelShort.IndexOf(pawnName, StringComparison.OrdinalIgnoreCase) >= 0);

            if (colonist == null)
            {
                LogError($"找不到名为 '{pawnName}' 的殖民者");
                return false;
            }

            // 确保已征召
            if (colonist.drafter == null || !colonist.drafter.Drafted)
            {
                if (colonist.drafter != null)
                    colonist.drafter.Drafted = true;
                else
                {
                    LogError($"{pawnName} 无法被征召");
                    return false;
                }
            }

            // 下达移动命令
            var job = JobMaker.MakeJob(JobDefOf.Goto, targetPos);
            colonist.jobs?.TryTakeOrderedJob(job, JobTag.DraftedOrder);

            LogExecution($"已命令 {colonist.LabelShort} 移动到 ({x}, {z})");
            Messages.Message($"已命令 {colonist.LabelShort} 移动到 ({x}, {z})", MessageTypeDefOf.NeutralEvent);
            return true;
        }
    }

    /// <summary>
    /// ? 治疗殖民者
    /// </summary>
    public class HealPawnCommand : BaseAICommand
    {
        public override string ActionName => "HealPawn";

        public override string GetDescription()
        {
            return "Tend to injured colonists. Parameters: pawnName=<name> (optional, 'All' for all injured)";
        }

        public override bool Execute(string? target = null, object? parameters = null)
        {
            var map = Find.CurrentMap;
            if (map == null)
            {
                LogError("No active map");
                return false;
            }

            string pawnName = target;
            int count = 0;

            // 获取所有需要治疗的殖民者
            var injuredColonists = map.mapPawns.FreeColonistsSpawned
                .Where(p => p.health.HasHediffsNeedingTend())
                .ToList();

            if (!string.IsNullOrEmpty(pawnName) && pawnName != "All")
            {
                injuredColonists = injuredColonists
                    .Where(p => p.Name.ToStringShort.IndexOf(pawnName, StringComparison.OrdinalIgnoreCase) >= 0 ||
                               p.LabelShort.IndexOf(pawnName, StringComparison.OrdinalIgnoreCase) >= 0)
                    .ToList();
            }

            if (injuredColonists.Count == 0)
            {
                LogExecution("无需要治疗的殖民者");
                Messages.Message("无需要治疗的殖民者", MessageTypeDefOf.RejectInput);
                return false;
            }

            // 查找可用的医生
            var doctors = map.mapPawns.FreeColonistsSpawned
                .Where(p => !p.Downed && 
                           !p.Dead && 
                           p.workSettings?.WorkIsActive(WorkTypeDefOf.Doctor) == true)
                .OrderByDescending(p => p.skills?.GetSkill(SkillDefOf.Medicine)?.Level ?? 0)
                .ToList();

            if (doctors.Count == 0)
            {
                LogError("无可用的医生");
                Messages.Message("无可用的医生", MessageTypeDefOf.RejectInput);
                return false;
            }

            foreach (var patient in injuredColonists)
            {
                // 找最近的医生
                var doctor = doctors
                    .Where(d => d != patient)
                    .OrderBy(d => d.Position.DistanceTo(patient.Position))
                    .FirstOrDefault();

                if (doctor != null && doctor.CanReserveAndReach(patient, PathEndMode.ClosestTouch, Danger.Some))
                {
                    var job = JobMaker.MakeJob(JobDefOf.TendPatient, patient);
                    doctor.jobs?.TryTakeOrderedJob(job);
                    count++;
                }
            }

            LogExecution($"已安排治疗 {count} 名伤员");

            if (count > 0)
            {
                Messages.Message($"已安排治疗 {count} 名伤员", MessageTypeDefOf.PositiveEvent);
                return true;
            }

            return false;
        }
    }

    /// <summary>
    /// ? 设置工作优先级
    /// </summary>
    public class SetWorkPriorityCommand : BaseAICommand
    {
        public override string ActionName => "SetWorkPriority";

        public override string GetDescription()
        {
            return "Set work priority for colonists. Parameters: workType=<type>, priority=<1-4>, pawnName=<name>";
        }

        public override bool Execute(string? target = null, object? parameters = null)
        {
            var map = Find.CurrentMap;
            if (map == null)
            {
                LogError("No active map");
                return false;
            }

            string pawnName = target ?? "";
            if (string.IsNullOrEmpty(pawnName))
            {
                LogError("未指定殖民者名称");
                return false;
            }

            // 从 parameters 解析工作类型和优先级
            string workTypeName = "";
            int priority = 1;
            
            if (parameters is Dictionary<string, object> paramsDict)
            {
                if (paramsDict.TryGetValue("workType", out var wtObj))
                    workTypeName = wtObj?.ToString() ?? "";
                if (paramsDict.TryGetValue("priority", out var pObj))
                    int.TryParse(pObj?.ToString(), out priority);
                if (paramsDict.TryGetValue("scope", out var scopeObj) && string.IsNullOrEmpty(workTypeName))
                    workTypeName = scopeObj?.ToString() ?? "";
            }

            if (string.IsNullOrEmpty(workTypeName))
            {
                LogError("未指定工作类型（需要workType参数）");
                return false;
            }

            // 查找工作类型
            var workTypeDef = DefDatabase<WorkTypeDef>.AllDefs
                .FirstOrDefault(w => w.defName.Equals(workTypeName, StringComparison.OrdinalIgnoreCase) ||
                                    (w.labelShort != null && w.labelShort.Equals(workTypeName, StringComparison.OrdinalIgnoreCase)) ||
                                    (w.label != null && w.label.ToLower().Contains(workTypeName.ToLower())));

            if (workTypeDef == null)
            {
                LogError($"找不到工作类型: {workTypeName}");
                return false;
            }

            // 查找殖民者
            var colonists = map.mapPawns.FreeColonistsSpawned
                .Where(p => pawnName == "All" ||
                           p.Name.ToStringShort.IndexOf(pawnName, StringComparison.OrdinalIgnoreCase) >= 0 ||
                           p.LabelShort.IndexOf(pawnName, StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();

            if (colonists.Count == 0)
            {
                LogError($"找不到名为 '{pawnName}' 的殖民者");
                return false;
            }

            int count = 0;
            foreach (var colonist in colonists)
            {
                if (colonist.workSettings != null && !colonist.WorkTypeIsDisabled(workTypeDef))
                {
                    colonist.workSettings.SetPriority(workTypeDef, priority);
                    count++;
                }
            }

            string priorityText = priority == 0 ? "禁用" : $"优先级{priority}";
            LogExecution($"已将 {count} 名殖民者的 {workTypeDef.labelShort} 设为{priorityText}");

            if (count > 0)
            {
                Messages.Message($"已将 {count} 名殖民者的 {workTypeDef.labelShort} 设为{priorityText}", 
                    MessageTypeDefOf.NeutralEvent);
                return true;
            }

            return false;
        }
    }

    /// <summary>
    /// ? 装备武器（单个殖民者）
    /// 注意：这个命令与 BatchEquipCommand 不同
    /// - EquipWeaponCommand: 单个殖民者装备指定武器
    /// - BatchEquipCommand: 所有殖民者自动装备最佳武器
    /// </summary>
    public class EquipWeaponCommand : BaseAICommand
    {
        public override string ActionName => "EquipWeapon";

        public override string GetDescription()
        {
            return "Equip a specific weapon to a colonist. Parameters: weaponDef=<defName> (optional)";
        }

        public override bool Execute(string? target = null, object? parameters = null)
        {
            var map = Find.CurrentMap;
            if (map == null)
            {
                LogError("No active map");
                return false;
            }

            string pawnName = target ?? "";
            if (string.IsNullOrEmpty(pawnName))
            {
                LogError("未指定殖民者名称");
                return false;
            }

            // 查找殖民者
            var colonist = map.mapPawns.FreeColonistsSpawned
                .FirstOrDefault(p => p.Name.ToStringShort.IndexOf(pawnName, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    p.LabelShort.IndexOf(pawnName, StringComparison.OrdinalIgnoreCase) >= 0);

            if (colonist == null)
            {
                LogError($"找不到名为 '{pawnName}' 的殖民者");
                return false;
            }

            // 从 parameters 获取武器DefName（如果指定）
            string weaponDefName = "";
            if (parameters is Dictionary<string, object> paramsDict)
            {
                if (paramsDict.TryGetValue("weaponDef", out var wdObj))
                    weaponDefName = wdObj?.ToString() ?? "";
                if (paramsDict.TryGetValue("scope", out var scopeObj) && string.IsNullOrEmpty(weaponDefName))
                    weaponDefName = scopeObj?.ToString() ?? "";
            }

            // 查找武器
            Thing? weapon = null;
            var availableWeapons = map.listerThings.ThingsInGroup(ThingRequestGroup.Weapon)
                .Where(w => !w.IsForbidden(Faction.OfPlayer) &&
                           colonist.CanReserveAndReach(w, PathEndMode.ClosestTouch, Danger.None))
                .ToList();

            if (!string.IsNullOrEmpty(weaponDefName))
            {
                weapon = availableWeapons.FirstOrDefault(w => 
                    w.def.defName.Equals(weaponDefName, StringComparison.OrdinalIgnoreCase) ||
                    w.LabelShort.ToLower().Contains(weaponDefName.ToLower()));
                
                if (weapon == null)
                {
                    LogError($"找不到可用的武器: {weaponDefName}");
                    return false;
                }
            }
            else
            {
                // 自动选择最佳武器
                weapon = availableWeapons
                    .OrderByDescending(w => w.GetStatValue(StatDefOf.RangedWeapon_DamageMultiplier, true))
                    .FirstOrDefault();
                
                if (weapon == null)
                {
                    LogError("无可用武器");
                    return false;
                }
            }

            // 下达装备命令
            var job = JobMaker.MakeJob(JobDefOf.Equip, weapon);
            colonist.jobs?.TryTakeOrderedJob(job);

            LogExecution($"已命令 {colonist.LabelShort} 装备 {weapon.LabelShort}");
            Messages.Message($"已命令 {colonist.LabelShort} 装备 {weapon.LabelShort}", MessageTypeDefOf.NeutralEvent);
            return true;
        }
    }

    #endregion

    #region ? v1.6.40: 资源管理命令（从 GameActionExecutor 迁移）

    /// <summary>
    /// ? 禁止物品
    /// </summary>
    public class ForbidItemsCommand : BaseAICommand
    {
        public override string ActionName => "ForbidItems";

        public override string GetDescription()
        {
            return "Forbid haulable items. Parameters: limit=<number> (optional)";
        }

        public override bool Execute(string? target = null, object? parameters = null)
        {
            var map = Find.CurrentMap;
            if (map == null)
            {
                LogError("No active map");
                return false;
            }

            // 解析 limit 参数
            int limit = -1;
            if (parameters is Dictionary<string, object> paramsDict)
            {
                if (paramsDict.TryGetValue("limit", out var limitObj))
                    int.TryParse(limitObj?.ToString(), out limit);
            }

            int count = 0;
            var items = map.listerThings.AllThings
                .Where(t => !t.IsForbidden(Faction.OfPlayer) && t.def.EverHaulable);

            foreach (var item in items)
            {
                item.SetForbidden(true, false);
                count++;

                if (limit > 0 && count >= limit)
                    break;
            }

            LogExecution($"已禁止 {count} 个物品");

            if (count > 0)
            {
                Messages.Message($"已禁止 {count} 个物品", MessageTypeDefOf.NeutralEvent);
                return true;
            }

            return false;
        }
    }

    /// <summary>
    /// ? 允许物品
    /// </summary>
    public class AllowItemsCommand : BaseAICommand
    {
        public override string ActionName => "AllowItems";

        public override string GetDescription()
        {
            return "Allow forbidden items. Parameters: limit=<number> (optional)";
        }

        public override bool Execute(string? target = null, object? parameters = null)
        {
            var map = Find.CurrentMap;
            if (map == null)
            {
                LogError("No active map");
                return false;
            }

            // 解析 limit 参数
            int limit = -1;
            if (parameters is Dictionary<string, object> paramsDict)
            {
                if (paramsDict.TryGetValue("limit", out var limitObj))
                    int.TryParse(limitObj?.ToString(), out limit);
            }

            int count = 0;
            var items = map.listerThings.AllThings
                .Where(t => t.IsForbidden(Faction.OfPlayer));

            foreach (var item in items)
            {
                item.SetForbidden(false, false);
                count++;

                if (limit > 0 && count >= limit)
                    break;
            }

            LogExecution($"已解除 {count} 个物品");

            if (count > 0)
            {
                Messages.Message($"已解除 {count} 个物品", MessageTypeDefOf.NeutralEvent);
                return true;
            }

            return false;
        }
    }

    #endregion

    #region ? v1.6.90: 查询命令

    /// <summary>
    /// ? 获取地图位置信息（以居住区为锚点）
    /// </summary>
    public class GetMapLocationCommand : BaseAICommand
    {
        public override string ActionName => "GetMapLocation";

        public override string GetDescription()
        {
            return "Get map size and home area center coordinates.";
        }

        public override bool Execute(string? target = null, object? parameters = null)
        {
            var map = Find.CurrentMap;
            if (map == null)
            {
                LogError("No active map");
                return false;
            }

            // 计算居住区中心
            IntVec3 homeCenter = map.Center;
            var homeCells = map.areaManager.Home.ActiveCells;
            if (homeCells.Any())
            {
                long sumX = 0;
                long sumZ = 0;
                int count = 0;
                foreach (var cell in homeCells)
                {
                    sumX += cell.x;
                    sumZ += cell.z;
                    count++;
                }
                homeCenter = new IntVec3((int)(sumX / count), 0, (int)(sumZ / count));
            }

            // 构建返回数据
            var result = new
            {
                MapSize = new { x = map.Size.x, z = map.Size.z },
                HomeCenter = new { x = homeCenter.x, z = homeCenter.z },
                CardinalDirections = new
                {
                    North = new { x = homeCenter.x, z = Math.Min(map.Size.z - 1, homeCenter.z + 20) },
                    South = new { x = homeCenter.x, z = Math.Max(0, homeCenter.z - 20) },
                    East = new { x = Math.Min(map.Size.x - 1, homeCenter.x + 20), z = homeCenter.z },
                    West = new { x = Math.Max(0, homeCenter.x - 20), z = homeCenter.z }
                },
                Description = $"Map Size: {map.Size.x}x{map.Size.z}. Home Center: ({homeCenter.x}, {homeCenter.z})."
            };

            string jsonResult = Newtonsoft.Json.JsonConvert.SerializeObject(result, Newtonsoft.Json.Formatting.None);
            
            LogExecution($"Map Location: {jsonResult}");
            return true;
        }
    }

    /// <summary>
    /// ? 扫描地图态势（威胁、访客、资源）
    /// </summary>
    public class ScanMapCommand : BaseAICommand
    {
        public override string ActionName => "ScanMap";

        public override string GetDescription()
        {
            return "Scan map for specific targets (hostiles, friendlies, resources).";
        }

        public override bool Execute(string? target = null, object? parameters = null)
        {
            var map = Find.CurrentMap;
            if (map == null)
            {
                LogError("No active map");
                return false;
            }

            string scanTarget = "hostiles"; // 默认
            if (parameters is Newtonsoft.Json.Linq.JObject jObj && jObj.TryGetValue("target", out var val))
            {
                scanTarget = val.ToString().ToLower();
            }

            // 计算居住区中心作为锚点
            IntVec3 homeCenter = map.Center;
            var homeCells = map.areaManager.Home.ActiveCells;
            if (homeCells.Any())
            {
                long sumX = 0; long sumZ = 0; int count = 0;
                foreach (var cell in homeCells) { sumX += cell.x; sumZ += cell.z; count++; }
                homeCenter = new IntVec3((int)(sumX / count), 0, (int)(sumZ / count));
            }

            var result = new Dictionary<string, object>();
            result["HomeCenter"] = new { x = homeCenter.x, z = homeCenter.z };
            string description = $"Scan Target: {scanTarget}. Home: ({homeCenter.x}, {homeCenter.z}). ";

            bool scanAll = scanTarget == "all";

            // 1. 扫描威胁 (Hostiles)
            if (scanAll || scanTarget == "hostiles")
            {
                var hostiles = map.mapPawns.AllPawnsSpawned
                    .Where(p => p.HostileTo(Faction.OfPlayer) && !p.Downed && !p.Dead)
                    .ToList();
                
                if (hostiles.Any())
                {
                    var center = GetCentroid(hostiles.Select(p => p.Position));
                    string info = $"{hostiles.Count} enemies at {GetDirection(homeCenter, center)} ({center.x}, {center.z})";
                    result["Threats"] = info;
                    description += $"Threats: {info}. ";
                }
                else
                {
                    result["Threats"] = "None";
                    description += "Threats: None. ";
                }
            }

            // 2. 扫描访客/商队 (Neutrals/Friendlies)
            if (scanAll || scanTarget == "friendlies")
            {
                var neutrals = map.mapPawns.AllPawnsSpawned
                    .Where(p => !p.HostileTo(Faction.OfPlayer) && !p.IsColonist && !p.IsSlaveOfColony && !p.Downed && !p.Dead && p.RaceProps.Humanlike)
                    .ToList();

                if (neutrals.Any())
                {
                    var center = GetCentroid(neutrals.Select(p => p.Position));
                    string info = $"{neutrals.Count} visitors at {GetDirection(homeCenter, center)} ({center.x}, {center.z})";
                    result["Visitors"] = info;
                    description += $"Visitors: {info}. ";
                }
                else
                {
                    result["Visitors"] = "None";
                    description += "Visitors: None. ";
                }
            }

            // 3. 扫描高价值矿物 (Resources)
            if (scanAll || scanTarget == "resources")
            {
                var valuableDefNames = new HashSet<string> { "MineableSteel", "MineableGold", "MineableSilver", "MineableJade", "MineableUranium", "MineablePlasteel", "MineableComponentsIndustrial" };
                var resources = map.listerThings.AllThings
                    .Where(t => t.def.mineable && valuableDefNames.Contains(t.def.defName))
                    .GroupBy(t => t.def.label)
                    .Select(g => new
                    {
                        Name = g.Key,
                        Count = g.Count(),
                        Center = GetCentroid(g.Select(t => t.Position))
                    })
                    .Select(r => $"{r.Name}: ~{r.Count} veins at {GetDirection(homeCenter, r.Center)}")
                    .ToList();

                string resourceInfo = resources.Any() ? string.Join("; ", resources) : "No valuable resources visible";
                result["Resources"] = resourceInfo;
                description += $"Resources: {resourceInfo}.";
            }

            result["Description"] = description;
            string jsonResult = Newtonsoft.Json.JsonConvert.SerializeObject(result, Newtonsoft.Json.Formatting.None);
            
            LogExecution($"Map Scan ({scanTarget}): {jsonResult}");
            return true;
        }

        private IntVec3 GetCentroid(IEnumerable<IntVec3> positions)
        {
            if (!positions.Any()) return IntVec3.Zero;
            long sumX = 0, sumZ = 0;
            int count = 0;
            foreach (var pos in positions)
            {
                sumX += pos.x;
                sumZ += pos.z;
                count++;
            }
            return new IntVec3((int)(sumX / count), 0, (int)(sumZ / count));
        }

        private string GetDirection(IntVec3 from, IntVec3 to)
        {
            float dx = to.x - from.x;
            float dz = to.z - from.z;
            double angle = Math.Atan2(dz, dx) * (180 / Math.PI); // -180 to 180

            if (angle > -22.5 && angle <= 22.5) return "East";
            if (angle > 22.5 && angle <= 67.5) return "North-East";
            if (angle > 67.5 && angle <= 112.5) return "North";
            if (angle > 112.5 && angle <= 157.5) return "North-West";
            if (angle > 157.5 || angle <= -157.5) return "West";
            if (angle > -157.5 && angle <= -112.5) return "South-West";
            if (angle > -112.5 && angle <= -67.5) return "South";
            if (angle > -67.5 && angle <= -22.5) return "South-East";
            return "Unknown";
        }
    }

    #endregion

    #region ? v1.6.40: 政策修改命令（从 GameActionExecutor 迁移）

    /// <summary>
    /// ? 修改政策（目前为通知实现）
    /// </summary>
    public class ChangePolicyCommand_New : BaseAICommand
    {
        public override string ActionName => "ChangePolicy";

        public override string GetDescription()
        {
            return "Change colony policy (currently notification only). Parameters: description=<text>";
        }

        public override bool Execute(string? target = null, object? parameters = null)
        {
            string policyName = target ?? "";
            if (string.IsNullOrEmpty(policyName))
            {
                LogError("未指定政策名称");
                return false;
            }

            string description = "";
            if (parameters is Dictionary<string, object> paramsDict)
            {
                if (paramsDict.TryGetValue("description", out var descObj))
                    description = descObj?.ToString() ?? "";
                if (paramsDict.TryGetValue("scope", out var scopeObj) && string.IsNullOrEmpty(description))
                    description = scopeObj?.ToString() ?? "";
            }

            string message = $"收到政策修改请求: {policyName}";
            if (!string.IsNullOrEmpty(description))
            {
                message += $" ({description})";
            }

            Messages.Message(message, MessageTypeDefOf.NeutralEvent);
            LogExecution($"政策修改请求: {policyName}");

            return true;
        }
    }

    #endregion
}
