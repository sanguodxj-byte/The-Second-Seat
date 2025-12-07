using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;
using TheSecondSeat.NaturalLanguage;

namespace TheSecondSeat.Execution
{
    /// <summary>
    /// 游戏动作执行器 - 高级命令的入口点
    /// </summary>
    public static class GameActionExecutor
    {
        /// <summary>
        /// 执行解析后的命令
        /// ? 线程安全：必须在主线程执行
        /// </summary>
        public static ExecutionResult Execute(ParsedCommand command)
        {
            if (command == null)
            {
                return ExecutionResult.Failed("命令为空");
            }

            Log.Message($"[GameActionExecutor] 执行命令: {command.action} (Target={command.parameters.target}, Scope={command.parameters.scope})");

            // ? 检查是否在主线程
            if (!UnityEngine.Application.isPlaying)
            {
                return ExecutionResult.Failed("游戏未运行");
            }

            try
            {
                return command.action switch
                {
                    // === 批量操作 ===
                    "BatchHarvest" => ExecuteBatchHarvest(command.parameters),
                    "BatchEquip" => ExecuteBatchEquip(command.parameters),
                    "BatchCapture" => ExecuteBatchCapture(command.parameters),
                    "BatchMine" => ExecuteBatchMine(command.parameters),
                    "BatchLogging" => ExecuteBatchLogging(command.parameters),
                    "PriorityRepair" => ExecutePriorityRepair(command.parameters),
                    "EmergencyRetreat" => ExecuteEmergencyRetreat(command.parameters),
                    "DesignatePlantCut" => ExecuteDesignatePlantCut(command.parameters),
                    
                    // === 殖民者管理 ===
                    "DraftPawn" => ExecuteDraftPawn(command.parameters),
                    "MovePawn" => ExecuteMovePawn(command.parameters),
                    "HealPawn" => ExecuteHealPawn(command.parameters),
                    "SetWorkPriority" => ExecuteSetWorkPriority(command.parameters),
                    "EquipWeapon" => ExecuteEquipWeapon(command.parameters),
                    
                    // === 资源管理 ===
                    "ForbidItems" => ExecuteForbidItems(command.parameters),
                    "AllowItems" => ExecuteAllowItems(command.parameters),
                    
                    // === 暂不支持 ===
                    "DesignateConstruction" => ExecutionResult.Failed("建造功能需要复杂的建筑蓝图，暂不支持"),
                    "AssignWork" => ExecutionResult.Failed("分配工作功能需要复杂的工作类型，暂不支持"),
                    
                    _ => ExecutionResult.Failed($"未知命令: {command.action}")
                };
            }
            catch (Exception ex)
            {
                Log.Error($"[GameActionExecutor] 执行失败: {ex.Message}\n{ex.StackTrace}");
                return ExecutionResult.Failed($"执行异常: {ex.Message}");
            }
        }

        #region 殖民者管理命令

        /// <summary>
        /// ? 征召/解除征召殖民者
        /// </summary>
        private static ExecutionResult ExecuteDraftPawn(AdvancedCommandParams parameters)
        {
            var map = Find.CurrentMap;
            if (map == null) return ExecutionResult.Failed("无当前地图");

            bool shouldDraft = parameters.target?.ToLower() != "false" && parameters.target?.ToLower() != "undraft";
            string pawnName = parameters.scope; // 使用 scope 作为殖民者名字
            
            int count = 0;
            var colonists = map.mapPawns.FreeColonistsSpawned;

            foreach (var colonist in colonists)
            {
                // 如果指定了名字，只处理匹配的殖民者
                if (!string.IsNullOrEmpty(pawnName) && pawnName != "All")
                {
                    if (!colonist.Name.ToStringShort.Contains(pawnName) && 
                        !colonist.LabelShort.Contains(pawnName))
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
            return count > 0 
                ? ExecutionResult.Success($"已{action} {count} 名殖民者")
                : ExecutionResult.Failed($"无可{action}的殖民者");
        }

        /// <summary>
        /// ? 移动殖民者到指定位置
        /// </summary>
        private static ExecutionResult ExecuteMovePawn(AdvancedCommandParams parameters)
        {
            var map = Find.CurrentMap;
            if (map == null) return ExecutionResult.Failed("无当前地图");

            string pawnName = parameters.target ?? "";
            if (string.IsNullOrEmpty(pawnName))
                return ExecutionResult.Failed("未指定殖民者名字");

            // 从 filters 中解析坐标
            int x = 0, z = 0;
            if (parameters.filters != null)
            {
                if (parameters.filters.TryGetValue("x", out var xObj))
                    int.TryParse(xObj?.ToString(), out x);
                if (parameters.filters.TryGetValue("z", out var zObj))
                    int.TryParse(zObj?.ToString(), out z);
            }

            if (x == 0 && z == 0)
                return ExecutionResult.Failed("未指定目标坐标（需要在filters中提供x和z）");

            var targetPos = new IntVec3(x, 0, z);
            if (!targetPos.InBounds(map) || !targetPos.Walkable(map))
                return ExecutionResult.Failed($"目标位置 ({x}, {z}) 不可到达");

            // 查找殖民者
            var colonist = map.mapPawns.FreeColonistsSpawned
                .FirstOrDefault(p => p.Name.ToStringShort.Contains(pawnName) || 
                                    p.LabelShort.Contains(pawnName));

            if (colonist == null)
                return ExecutionResult.Failed($"找不到名为 '{pawnName}' 的殖民者");

            // 必须先征召
            if (colonist.drafter == null || !colonist.drafter.Drafted)
            {
                if (colonist.drafter != null)
                    colonist.drafter.Drafted = true;
                else
                    return ExecutionResult.Failed($"{pawnName} 无法被征召");
            }

            // 下达移动命令
            var job = JobMaker.MakeJob(JobDefOf.Goto, targetPos);
            colonist.jobs?.TryTakeOrderedJob(job, JobTag.DraftedOrder);

            return ExecutionResult.Success($"已命令 {colonist.LabelShort} 移动到 ({x}, {z})");
        }

        /// <summary>
        /// ? 治疗殖民者
        /// </summary>
        private static ExecutionResult ExecuteHealPawn(AdvancedCommandParams parameters)
        {
            var map = Find.CurrentMap;
            if (map == null) return ExecutionResult.Failed("无当前地图");

            string pawnName = parameters.target;
            int count = 0;

            // 获取所有需要治疗的殖民者
            var injuredColonists = map.mapPawns.FreeColonistsSpawned
                .Where(p => p.health.HasHediffsNeedingTend())
                .ToList();

            if (!string.IsNullOrEmpty(pawnName) && pawnName != "All")
            {
                injuredColonists = injuredColonists
                    .Where(p => p.Name.ToStringShort.Contains(pawnName) || 
                               p.LabelShort.Contains(pawnName))
                    .ToList();
            }

            if (injuredColonists.Count == 0)
                return ExecutionResult.Failed("无需要治疗的殖民者");

            // 查找可用的医生
            var doctors = map.mapPawns.FreeColonistsSpawned
                .Where(p => !p.Downed && 
                           !p.Dead && 
                           p.workSettings?.WorkIsActive(WorkTypeDefOf.Doctor) == true)
                .OrderByDescending(p => p.skills?.GetSkill(SkillDefOf.Medicine)?.Level ?? 0)
                .ToList();

            if (doctors.Count == 0)
                return ExecutionResult.Failed("无可用的医生");

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

            return count > 0 
                ? ExecutionResult.Success($"已安排治疗 {count} 名伤员")
                : ExecutionResult.Failed("无法安排治疗（医生无法到达或忙碌）");
        }

        /// <summary>
        /// ? 设置工作优先级
        /// </summary>
        private static ExecutionResult ExecuteSetWorkPriority(AdvancedCommandParams parameters)
        {
            var map = Find.CurrentMap;
            if (map == null) return ExecutionResult.Failed("无当前地图");

            string pawnName = parameters.target ?? "";
            if (string.IsNullOrEmpty(pawnName))
                return ExecutionResult.Failed("未指定殖民者名字");

            // 从 filters 中解析工作类型和优先级
            string workTypeName = "";
            int priority = 1;
            
            if (parameters.filters != null)
            {
                if (parameters.filters.TryGetValue("workType", out var wtObj))
                    workTypeName = wtObj?.ToString() ?? "";
                if (parameters.filters.TryGetValue("priority", out var pObj))
                    int.TryParse(pObj?.ToString(), out priority);
            }

            // 如果 filters 中没有，尝试从 scope 中获取工作类型
            if (string.IsNullOrEmpty(workTypeName) && !string.IsNullOrEmpty(parameters.scope))
            {
                workTypeName = parameters.scope;
            }

            if (string.IsNullOrEmpty(workTypeName))
                return ExecutionResult.Failed("未指定工作类型（需要在filters中提供workType）");

            // 查找工作类型
            var workTypeDef = DefDatabase<WorkTypeDef>.AllDefs
                .FirstOrDefault(w => w.defName.Equals(workTypeName, StringComparison.OrdinalIgnoreCase) ||
                                    (w.labelShort != null && w.labelShort.Equals(workTypeName, StringComparison.OrdinalIgnoreCase)) ||
                                    (w.label != null && w.label.ToLower().Contains(workTypeName.ToLower())));

            if (workTypeDef == null)
                return ExecutionResult.Failed($"找不到工作类型: {workTypeName}");

            // 查找殖民者
            var colonists = map.mapPawns.FreeColonistsSpawned
                .Where(p => pawnName == "All" || 
                           p.Name.ToStringShort.Contains(pawnName) || 
                           p.LabelShort.Contains(pawnName))
                .ToList();

            if (colonists.Count == 0)
                return ExecutionResult.Failed($"找不到名为 '{pawnName}' 的殖民者");

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
            return count > 0 
                ? ExecutionResult.Success($"已将 {count} 名殖民者的 {workTypeDef.labelShort} 设为{priorityText}")
                : ExecutionResult.Failed($"无法设置工作优先级（殖民者可能无法从事该工作）");
        }

        /// <summary>
        /// ? 装备武器（单个殖民者）
        /// </summary>
        private static ExecutionResult ExecuteEquipWeapon(AdvancedCommandParams parameters)
        {
            var map = Find.CurrentMap;
            if (map == null) return ExecutionResult.Failed("无当前地图");

            string pawnName = parameters.target ?? "";
            if (string.IsNullOrEmpty(pawnName))
                return ExecutionResult.Failed("未指定殖民者名字");

            // 查找殖民者
            var colonist = map.mapPawns.FreeColonistsSpawned
                .FirstOrDefault(p => p.Name.ToStringShort.Contains(pawnName) || 
                                    p.LabelShort.Contains(pawnName));

            if (colonist == null)
                return ExecutionResult.Failed($"找不到名为 '{pawnName}' 的殖民者");

            // 从 filters 获取武器DefName（如果指定）
            string weaponDefName = "";
            if (parameters.filters?.TryGetValue("weaponDef", out var wdObj) == true)
                weaponDefName = wdObj?.ToString() ?? "";

            // 也可以从 scope 获取
            if (string.IsNullOrEmpty(weaponDefName) && !string.IsNullOrEmpty(parameters.scope))
                weaponDefName = parameters.scope;

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
                    return ExecutionResult.Failed($"找不到可用的武器: {weaponDefName}");
            }
            else
            {
                // 自动选择最佳武器
                weapon = availableWeapons
                    .OrderByDescending(w => w.GetStatValue(StatDefOf.RangedWeapon_DamageMultiplier, true))
                    .FirstOrDefault();
                
                if (weapon == null)
                    return ExecutionResult.Failed("无可用武器");
            }

            // 下达装备命令
            var job = JobMaker.MakeJob(JobDefOf.Equip, weapon);
            colonist.jobs?.TryTakeOrderedJob(job);

            return ExecutionResult.Success($"已命令 {colonist.LabelShort} 装备 {weapon.LabelShort}");
        }

        #endregion

        #region 批量操作命令

        /// <summary>
        /// 批量收获
        /// </summary>
        private static ExecutionResult ExecuteBatchHarvest(AdvancedCommandParams parameters)
        {
            var map = Find.CurrentMap;
            if (map == null) return ExecutionResult.Failed("无当前地图");

            int count = 0;
            var plants = map.listerThings.ThingsInGroup(ThingRequestGroup.HarvestablePlant);

            foreach (var thing in plants)
            {
                if (thing is Plant plant && ShouldHarvest(plant, parameters))
                {
                    if (map.designationManager.DesignationOn(plant, DesignationDefOf.HarvestPlant) == null)
                    {
                        map.designationManager.AddDesignation(new Designation(plant, DesignationDefOf.HarvestPlant));
                        count++;
                    }
                }
            }

            return count > 0 
                ? ExecutionResult.Success($"已指定 {count} 株植物收获")
                : ExecutionResult.Failed("未找到可收获植物");
        }

        private static bool ShouldHarvest(Plant plant, AdvancedCommandParams parameters)
        {
            if (!plant.HarvestableNow || !plant.Spawned) return false;

            if (parameters.target == "Blighted")
                return plant.Blighted;
            else if (parameters.target == "Mature")
                return plant.Growth >= 1f;

            return true;
        }

        /// <summary>
        /// ? 批量伐木
        /// </summary>
        private static ExecutionResult ExecuteBatchLogging(AdvancedCommandParams parameters)
        {
            var map = Find.CurrentMap;
            if (map == null) return ExecutionResult.Failed("无当前地图");

            int count = 0;
            var plants = map.listerThings.ThingsInGroup(ThingRequestGroup.Plant);

            foreach (var thing in plants)
            {
                if (thing is Plant plant && plant.Spawned)
                {
                    // 只选择成熟的树木
                    if (plant.def.plant?.IsTree == true && 
                        plant.Growth >= 0.9f &&
                        !plant.IsForbidden(Faction.OfPlayer))
                    {
                        if (map.designationManager.DesignationOn(plant, DesignationDefOf.CutPlant) == null)
                        {
                            map.designationManager.AddDesignation(new Designation(plant, DesignationDefOf.CutPlant));
                            count++;
                        }
                    }
                }
            }

            return count > 0 
                ? ExecutionResult.Success($"已指定 {count} 棵成熟树木砍伐")
                : ExecutionResult.Failed("未找到成熟树木");
        }

        /// <summary>
        /// 指定砍伐植物
        /// </summary>
        private static ExecutionResult ExecuteDesignatePlantCut(AdvancedCommandParams parameters)
        {
            var map = Find.CurrentMap;
            if (map == null) return ExecutionResult.Failed("无当前地图");

            int count = 0;
            var plants = map.listerThings.AllThings.OfType<Plant>();

            foreach (var plant in plants)
            {
                if (ShouldCutPlant(plant, parameters))
                {
                    if (map.designationManager.DesignationOn(plant, DesignationDefOf.CutPlant) == null)
                    {
                        map.designationManager.AddDesignation(new Designation(plant, DesignationDefOf.CutPlant));
                        count++;
                    }
                }

                if (parameters.count != null && count >= parameters.count)
                    break;
            }

            return count > 0 
                ? ExecutionResult.Success($"已指定砍伐 {count} 株植物")
                : ExecutionResult.Failed("未找到符合条件的植物");
        }

        private static bool ShouldCutPlant(Plant plant, AdvancedCommandParams parameters)
        {
            if (!plant.Spawned) return false;

            if (parameters.target == "Blighted")
                return plant.Blighted;
            else if (parameters.target == "Trees")
                return plant.def.plant?.IsTree == true;
            else if (parameters.target == "Wild")
                return !plant.IsCrop;
            else if (parameters.target == "All")
            {
                var map = Find.CurrentMap;
                if (map == null) return true;
                var zone = map.zoneManager.ZoneAt(plant.Position);
                return zone is not Zone_Growing;
            }

            return true;
        }

        /// <summary>
        /// 批量装备
        /// </summary>
        private static ExecutionResult ExecuteBatchEquip(AdvancedCommandParams parameters)
        {
            var map = Find.CurrentMap;
            if (map == null) return ExecutionResult.Failed("无当前地图");

            int count = 0;
            var colonists = map.mapPawns.FreeColonistsSpawned;
            
            ThingRequestGroup requestGroup = parameters.target == "Armor" 
                ? ThingRequestGroup.Apparel 
                : ThingRequestGroup.Weapon;

            var items = map.listerThings.ThingsInGroup(requestGroup)
                .Where(t => !t.IsForbidden(Faction.OfPlayer))
                .ToList();

            foreach (var colonist in colonists)
            {
                if (parameters.target == "Weapon" && colonist.equipment?.Primary != null)
                    continue;

                Thing? bestItem = FindBestItem(items, colonist, parameters.target == "Armor");
                
                if (bestItem != null)
                {
                    var job = parameters.target == "Armor"
                        ? JobMaker.MakeJob(JobDefOf.Wear, bestItem)
                        : JobMaker.MakeJob(JobDefOf.Equip, bestItem);
                    
                    colonist.jobs?.TryTakeOrderedJob(job);
                    items.Remove(bestItem);
                    count++;
                }
            }

            return count > 0 
                ? ExecutionResult.Success($"已命令 {count} 名殖民者装备")
                : ExecutionResult.Failed("无可装备的殖民者");
        }

        private static Thing? FindBestItem(List<Thing> items, Pawn pawn, bool isApparel)
        {
            if (isApparel)
            {
                return items.OfType<Apparel>()
                    .Where(a => ApparelUtility.HasPartsToWear(pawn, a.def))
                    .OrderByDescending(a => a.GetStatValue(StatDefOf.ArmorRating_Sharp))
                    .FirstOrDefault();
            }
            else
            {
                return items
                    .Where(w => pawn.CanReserveAndReach(w, PathEndMode.ClosestTouch, Danger.None))
                    .OrderByDescending(w => w.GetStatValue(StatDefOf.RangedWeapon_DamageMultiplier, true))
                    .FirstOrDefault();
            }
        }

        /// <summary>
        /// 优先修复
        /// </summary>
        private static ExecutionResult ExecutePriorityRepair(AdvancedCommandParams parameters)
        {
            var map = Find.CurrentMap;
            if (map == null) return ExecutionResult.Failed("无当前地图");

            int count = 0;
            
            var repairDef = DefDatabase<DesignationDef>.GetNamedSilentFail("Repair");
            if (repairDef == null)
                return ExecutionResult.Failed("修复功能不可用");
            
            var damaged = map.listerThings.AllThings
                .Where(t => t.def.useHitPoints && 
                           t.HitPoints < t.MaxHitPoints && 
                           t.HitPoints > 0 &&
                           ShouldRepair(t, parameters))
                .ToList();

            foreach (var thing in damaged)
            {
                if (map.designationManager.DesignationOn(thing, repairDef) == null)
                {
                    map.designationManager.AddDesignation(new Designation(thing, repairDef));
                    count++;
                }

                if (parameters.count != null && count >= parameters.count)
                    break;
            }

            return count > 0 
                ? ExecutionResult.Success($"已指定修复 {count} 个建筑")
                : ExecutionResult.Failed("未找到需要修复的建筑");
        }

        private static bool ShouldRepair(Thing thing, AdvancedCommandParams parameters)
        {
            if (parameters.target == "Damaged")
                return thing.HitPoints < thing.MaxHitPoints * 0.8f;
            return thing.def.building != null;
        }

        /// <summary>
        /// 紧急撤退
        /// </summary>
        private static ExecutionResult ExecuteEmergencyRetreat(AdvancedCommandParams parameters)
        {
            var map = Find.CurrentMap;
            if (map == null) return ExecutionResult.Failed("无当前地图");

            int count = 0;
            var colonists = map.mapPawns.FreeColonistsSpawned;

            foreach (var colonist in colonists)
            {
                if (colonist.drafter != null && !colonist.drafter.Drafted)
                {
                    colonist.drafter.Drafted = true;
                    count++;
                }
            }

            return count > 0 
                ? ExecutionResult.Success($"已征召 {count} 名殖民者")
                : ExecutionResult.Failed("无可征召的殖民者");
        }

        /// <summary>
        /// 批量俘获
        /// </summary>
        private static ExecutionResult ExecuteBatchCapture(AdvancedCommandParams parameters)
        {
            var map = Find.CurrentMap;
            if (map == null) return ExecutionResult.Failed("无当前地图");

            var captureDef = DefDatabase<DesignationDef>.GetNamedSilentFail("Capture");
            if (captureDef == null)
                return ExecutionResult.Failed("俘获功能不可用");

            int count = 0;
            
            var downedPawns = map.mapPawns.AllPawnsSpawned
                .Where(p => p != null && 
                           p.Spawned &&
                           p.Downed && 
                           p.HostileTo(Faction.OfPlayer) && 
                           !p.Dead &&
                           p.RaceProps?.Humanlike == true)
                .ToList();

            if (downedPawns.Count == 0)
                return ExecutionResult.Failed("未找到可俘获的倒地敌人");

            var availableColonists = map.mapPawns.FreeColonistsSpawned
                .Where(c => c != null && 
                           c.Spawned &&
                           !c.Downed && 
                           !c.Dead && 
                           c.workSettings?.WorkIsActive(WorkTypeDefOf.Warden) == true)
                .ToList();

            if (availableColonists.Count == 0)
                return ExecutionResult.Failed("无可用的看守执行俘获任务");

            foreach (var pawn in downedPawns)
            {
                if (map.designationManager.DesignationOn(pawn, captureDef) != null)
                    continue;

                var nearestColonist = availableColonists
                    .Where(c => c.Spawned)
                    .OrderBy(c => c.Position.DistanceTo(pawn.Position))
                    .FirstOrDefault();

                if (nearestColonist != null && nearestColonist.CanReserveAndReach(pawn, PathEndMode.OnCell, Danger.Deadly))
                {
                    map.designationManager.AddDesignation(new Designation(pawn, captureDef));
                    count++;
                }

                if (parameters.count != null && count >= parameters.count)
                    break;
            }

            return count > 0 
                ? ExecutionResult.Success($"已指定俘获 {count} 名敌人")
                : ExecutionResult.Failed("无法指定俘获（敌人无法到达或已被占用）");
        }

        /// <summary>
        /// 批量采矿
        /// </summary>
        private static ExecutionResult ExecuteBatchMine(AdvancedCommandParams parameters)
        {
            var map = Find.CurrentMap;
            if (map == null) return ExecutionResult.Failed("无当前地图");

            int count = 0;
            string targetType = parameters.target?.ToLower() ?? "all";
            
            var mineableThings = map.listerThings.AllThings
                .Where(t => t.def.mineable && t.Spawned)
                .ToList();

            foreach (var thing in mineableThings)
            {
                if (map.designationManager.DesignationOn(thing, DesignationDefOf.Mine) != null)
                    continue;

                if (ShouldMine(thing, targetType))
                {
                    map.designationManager.AddDesignation(new Designation(thing, DesignationDefOf.Mine));
                    count++;
                }

                if (parameters.count != null && count >= parameters.count)
                    break;
            }

            return count > 0 
                ? ExecutionResult.Success($"已指定采矿 {count} 处资源点 (类型: {targetType})")
                : ExecutionResult.Failed($"未找到符合条件的可采矿资源 (类型: {targetType})");
        }

        private static bool ShouldMine(Thing thing, string targetType)
        {
            if (thing.def.building?.mineableThing == null)
                return false;

            var mineableItem = thing.def.building.mineableThing;

            return targetType switch
            {
                "all" => true,
                "metal" => mineableItem.IsWithinCategory(ThingCategoryDefOf.ResourcesRaw) ||
                          mineableItem.defName.Contains("Steel") ||
                          mineableItem.defName.Contains("Gold") ||
                          mineableItem.defName.Contains("Silver") ||
                          mineableItem.defName.Contains("Uranium") ||
                          mineableItem.defName.Contains("Plasteel") ||
                          mineableItem.defName.Contains("Jade"),
                "stone" => mineableItem.IsWithinCategory(ThingCategoryDefOf.StoneBlocks),
                "component" or "components" => mineableItem.defName.Contains("Component"),
                _ => thing.def.defName.ToLower().Contains(targetType) ||
                     mineableItem.defName.ToLower().Contains(targetType)
            };
        }

        #endregion

        #region 资源管理命令

        /// <summary>
        /// 禁止物品
        /// </summary>
        private static ExecutionResult ExecuteForbidItems(AdvancedCommandParams parameters)
        {
            var map = Find.CurrentMap;
            if (map == null) return ExecutionResult.Failed("无当前地图");

            int count = 0;
            var items = map.listerThings.AllThings
                .Where(t => !t.IsForbidden(Faction.OfPlayer) && t.def.EverHaulable);

            foreach (var item in items)
            {
                item.SetForbidden(true, false);
                count++;

                if (parameters.count != null && count >= parameters.count)
                    break;
            }

            return count > 0 
                ? ExecutionResult.Success($"已禁止 {count} 个物品")
                : ExecutionResult.Failed("未找到可禁止的物品");
        }

        /// <summary>
        /// 解禁物品
        /// </summary>
        private static ExecutionResult ExecuteAllowItems(AdvancedCommandParams parameters)
        {
            var map = Find.CurrentMap;
            if (map == null) return ExecutionResult.Failed("无当前地图");

            int count = 0;
            var items = map.listerThings.AllThings
                .Where(t => t.IsForbidden(Faction.OfPlayer));

            foreach (var item in items)
            {
                item.SetForbidden(false, false);
                count++;

                if (parameters.count != null && count >= parameters.count)
                    break;
            }

            return count > 0 
                ? ExecutionResult.Success($"已解禁 {count} 个物品")
                : ExecutionResult.Failed("未找到被禁止的物品");
        }

        #endregion
    }

    /// <summary>
    /// 执行结果
    /// </summary>
    public class ExecutionResult
    {
        public bool success { get; set; }
        public string message { get; set; } = "";
        public int affectedCount { get; set; } = 0;

        public static ExecutionResult Success(string message, int count = 0)
        {
            return new ExecutionResult
            {
                success = true,
                message = message,
                affectedCount = count
            };
        }

        public static ExecutionResult Failed(string message)
        {
            return new ExecutionResult
            {
                success = false,
                message = message
            };
        }
    }
}
