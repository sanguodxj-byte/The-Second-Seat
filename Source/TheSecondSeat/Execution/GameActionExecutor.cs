using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;
using TheSecondSeat.NaturalLanguage;
using TheSecondSeat.Commands.Implementations;

namespace TheSecondSeat.Execution
{
    /// <summary>
    /// 游戏动作执行器 - 重构为命令路由器
    /// ? v1.6.39: 将执行逻辑委托给 ConcreteCommands.cs 中的命令类
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
                // ? 转换参数为 Dictionary<string, object>
                Dictionary<string, object> paramsDict = ConvertParams(command.parameters);

                // ? 根据 action 字符串实例化对应的命令类并执行
                bool success = command.action switch
                {
                    // === 批量操作命令 ===
                    "BatchHarvest" => new BatchHarvestCommand().Execute(command.parameters.target, paramsDict),
                    "BatchEquip" => new BatchEquipCommand().Execute(command.parameters.target, paramsDict),
                    "BatchCapture" => new BatchCaptureCommand().Execute(command.parameters.target, paramsDict),
                    "BatchMine" => new BatchMineCommand().Execute(command.parameters.target, paramsDict),
                    "BatchLogging" => new BatchLoggingCommand().Execute(command.parameters.target, paramsDict),
                    "PriorityRepair" => new PriorityRepairCommand().Execute(command.parameters.target, paramsDict),
                    "EmergencyRetreat" => new EmergencyRetreatCommand().Execute(command.parameters.target, paramsDict),
                    "DesignatePlantCut" => new DesignatePlantCutCommand().Execute(command.parameters.target, paramsDict),
                    
                    // === 对弈者模式事件命令 ===
                    "TriggerEvent" => new TriggerEventCommand().Execute(command.parameters.target, paramsDict),
                    "ScheduleEvent" => new ScheduleEventCommand().Execute(command.parameters.target, paramsDict),
                    
                    // === 殖民者操作命令（保留旧逻辑，待迁移） ===
                    "DraftPawn" => ExecuteDraftPawn(command.parameters),
                    "MovePawn" => ExecuteMovePawn(command.parameters),
                    "HealPawn" => ExecuteHealPawn(command.parameters),
                    "SetWorkPriority" => ExecuteSetWorkPriority(command.parameters),
                    "EquipWeapon" => ExecuteEquipWeapon(command.parameters),
                    
                    // === 资源管理命令（保留旧逻辑，待迁移） ===
                    "ForbidItems" => ExecuteForbidItems(command.parameters),
                    "AllowItems" => ExecuteAllowItems(command.parameters),
                    
                    // === 政策修改命令（保留旧逻辑，待迁移） ===
                    "ChangePolicy" => ExecuteChangePolicy(command.parameters),
                    
                    // === 暂不支持的命令 ===
                    "DesignateConstruction" => throw new NotImplementedException("建造命令需要更多的建筑数据，暂不支持"),
                    "AssignWork" => throw new NotImplementedException("工作分配需要更多的工作类型，暂不支持"),
                    
                    _ => throw new NotImplementedException($"未知命令: {command.action}")
                };

                return success 
                    ? ExecutionResult.Success($"命令 {command.action} 执行成功")
                    : ExecutionResult.Failed($"命令 {command.action} 执行失败");
            }
            catch (NotImplementedException ex)
            {
                Log.Warning($"[GameActionExecutor] {ex.Message}");
                return ExecutionResult.Failed(ex.Message);
            }
            catch (Exception ex)
            {
                Log.Error($"[GameActionExecutor] 执行失败: {ex.Message}\n{ex.StackTrace}");
                return ExecutionResult.Failed($"执行异常: {ex.Message}");
            }
        }

        #region 参数转换辅助方法

        /// <summary>
        /// ? 将 AdvancedCommandParams 转换为 Dictionary<string, object>
        /// 合并 target, scope, filters, count 到一个字典中
        /// </summary>
        private static Dictionary<string, object> ConvertParams(AdvancedCommandParams p)
        {
            var dict = new Dictionary<string, object>();

            // 1. 添加 target
            if (!string.IsNullOrEmpty(p.target))
            {
                dict["target"] = p.target;
            }

            // 2. 添加 scope
            if (!string.IsNullOrEmpty(p.scope))
            {
                dict["scope"] = p.scope;
            }

            // 3. 合并 filters 中的所有键值对
            if (p.filters != null)
            {
                foreach (var kvp in p.filters)
                {
                    dict[kvp.Key] = kvp.Value;
                }
            }

            // 4. ? 映射 p.count → "limit"
            if (p.count != null && p.count > 0)
            {
                dict["limit"] = p.count;
            }

            // 5. ? 添加其他可能的参数（从 scope 解析）
            // 例如：如果 scope 是 "delay=30" 或 "comment=AI评论"
            if (!string.IsNullOrEmpty(p.scope) && p.scope.Contains("="))
            {
                var parts = p.scope.Split('=');
                if (parts.Length == 2)
                {
                    string key = parts[0].Trim().ToLower();
                    string value = parts[1].Trim();
                    
                    // 避免覆盖已存在的 scope 键
                    if (key != "scope")
                    {
                        dict[key] = value;
                    }
                }
            }

            return dict;
        }

        #endregion

        #region 殖民者管理命令（旧逻辑，待迁移到 ConcreteCommands.cs）

        /// <summary>
        /// ? 征召/解除征召殖民者
        /// </summary>
        private static bool ExecuteDraftPawn(AdvancedCommandParams parameters)
        {
            var map = Find.CurrentMap;
            if (map == null) return false;

            bool shouldDraft = parameters.target?.ToLower() != "false" && parameters.target?.ToLower() != "undraft";
            string pawnName = parameters.scope;
            
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

            return count > 0;
        }

        /// <summary>
        /// ? 移动殖民者到指定位置
        /// </summary>
        private static bool ExecuteMovePawn(AdvancedCommandParams parameters)
        {
            var map = Find.CurrentMap;
            if (map == null) return false;

            string pawnName = parameters.target ?? "";
            if (string.IsNullOrEmpty(pawnName)) return false;

            // 从 filters 中解析坐标
            int x = 0, z = 0;
            if (parameters.filters != null)
            {
                if (parameters.filters.TryGetValue("x", out var xObj))
                    int.TryParse(xObj?.ToString(), out x);
                if (parameters.filters.TryGetValue("z", out var zObj))
                    int.TryParse(zObj?.ToString(), out z);
            }

            if (x == 0 && z == 0) return false;

            var targetPos = new IntVec3(x, 0, z);
            if (!targetPos.InBounds(map) || !targetPos.Walkable(map)) return false;

            // 查找殖民者
            var colonist = map.mapPawns.FreeColonistsSpawned
                .FirstOrDefault(p => p.Name.ToStringShort.Contains(pawnName) || 
                                    p.LabelShort.Contains(pawnName));

            if (colonist == null) return false;

            // 必须先征召
            if (colonist.drafter == null || !colonist.drafter.Drafted)
            {
                if (colonist.drafter != null)
                    colonist.drafter.Drafted = true;
                else
                    return false;
            }

            // 下达移动命令
            var job = JobMaker.MakeJob(JobDefOf.Goto, targetPos);
            colonist.jobs?.TryTakeOrderedJob(job, JobTag.DraftedOrder);

            return true;
        }

        /// <summary>
        /// ? 治疗殖民者
        /// </summary>
        private static bool ExecuteHealPawn(AdvancedCommandParams parameters)
        {
            var map = Find.CurrentMap;
            if (map == null) return false;

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

            if (injuredColonists.Count == 0) return false;

            // 查找可用的医生
            var doctors = map.mapPawns.FreeColonistsSpawned
                .Where(p => !p.Downed && 
                           !p.Dead && 
                           p.workSettings?.WorkIsActive(WorkTypeDefOf.Doctor) == true)
                .OrderByDescending(p => p.skills?.GetSkill(SkillDefOf.Medicine)?.Level ?? 0)
                .ToList();

            if (doctors.Count == 0) return false;

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

            return count > 0;
        }

        /// <summary>
        /// ? 设置工作优先级
        /// </summary>
        private static bool ExecuteSetWorkPriority(AdvancedCommandParams parameters)
        {
            var map = Find.CurrentMap;
            if (map == null) return false;

            string pawnName = parameters.target ?? "";
            if (string.IsNullOrEmpty(pawnName)) return false;

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

            if (string.IsNullOrEmpty(workTypeName)) return false;

            // 查找工作类型
            var workTypeDef = DefDatabase<WorkTypeDef>.AllDefs
                .FirstOrDefault(w => w.defName.Equals(workTypeName, StringComparison.OrdinalIgnoreCase) ||
                                    (w.labelShort != null && w.labelShort.Equals(workTypeName, StringComparison.OrdinalIgnoreCase)) ||
                                    (w.label != null && w.label.ToLower().Contains(workTypeName.ToLower())));

            if (workTypeDef == null) return false;

            // 查找殖民者
            var colonists = map.mapPawns.FreeColonistsSpawned
                .Where(p => pawnName == "All" || 
                           p.Name.ToStringShort.Contains(pawnName) || 
                           p.LabelShort.Contains(pawnName))
                .ToList();

            if (colonists.Count == 0) return false;

            int count = 0;
            foreach (var colonist in colonists)
            {
                if (colonist.workSettings != null && !colonist.WorkTypeIsDisabled(workTypeDef))
                {
                    colonist.workSettings.SetPriority(workTypeDef, priority);
                    count++;
                }
            }

            return count > 0;
        }

        /// <summary>
        /// ? 装备武器（单个殖民者）
        /// </summary>
        private static bool ExecuteEquipWeapon(AdvancedCommandParams parameters)
        {
            var map = Find.CurrentMap;
            if (map == null) return false;

            string pawnName = parameters.target ?? "";
            if (string.IsNullOrEmpty(pawnName)) return false;

            // 查找殖民者
            var colonist = map.mapPawns.FreeColonistsSpawned
                .FirstOrDefault(p => p.Name.ToStringShort.Contains(pawnName) || 
                                    p.LabelShort.Contains(pawnName));

            if (colonist == null) return false;

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
                
                if (weapon == null) return false;
            }
            else
            {
                // 自动选择最佳武器
                weapon = availableWeapons
                    .OrderByDescending(w => w.GetStatValue(StatDefOf.RangedWeapon_DamageMultiplier, true))
                    .FirstOrDefault();
                
                if (weapon == null) return false;
            }

            // 下达装备命令
            var job = JobMaker.MakeJob(JobDefOf.Equip, weapon);
            colonist.jobs?.TryTakeOrderedJob(job);

            return true;
        }

        #endregion

        #region 资源管理命令（旧逻辑，待迁移到 ConcreteCommands.cs）

        /// <summary>
        /// 禁止物品
        /// </summary>
        private static bool ExecuteForbidItems(AdvancedCommandParams parameters)
        {
            var map = Find.CurrentMap;
            if (map == null) return false;

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

            return count > 0;
        }

        /// <summary>
        /// 允许物品
        /// </summary>
        private static bool ExecuteAllowItems(AdvancedCommandParams parameters)
        {
            var map = Find.CurrentMap;
            if (map == null) return false;

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

            return count > 0;
        }

        #endregion

        #region 政策修改命令（旧逻辑，待迁移到 ConcreteCommands.cs）

        private static bool ExecuteChangePolicy(AdvancedCommandParams parameters)
        {
            string policyName = parameters.target ?? "";
            if (string.IsNullOrEmpty(policyName)) return false;

            string description = parameters.scope ?? "";

            string message = $"收到政策修改请求: {policyName}";
            if (!string.IsNullOrEmpty(description))
            {
                message += $" ({description})";
            }

            Messages.Message(message, MessageTypeDefOf.NeutralEvent);
            Log.Message($"[GameActionExecutor] 政策修改请求: {policyName}");

            return true;
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
