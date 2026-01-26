using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;
using Verse.AI;
using RimWorld;
using UnityEngine;
using TheSecondSeat.NaturalLanguage;

namespace TheSecondSeat.Commands.Implementations
{
    /// <summary>
    /// 查询殖民者技能信息 - 用于智能工作分配
    /// </summary>
    public class GetPawnSkillsCommand : BaseAICommand
    {
        public override string ActionName => "GetPawnSkills";

        public override string GetDescription()
        {
            return "Get skill levels for a colonist. Returns all skills with levels and passions.";
        }

        public override bool Execute(string? target = null, object? parameters = null)
        {
            var map = Find.CurrentMap;
            if (map == null)
            {
                LogError("No map available");
                return false;
            }

            List<Pawn> pawns;
            
            // 如果没有指定目标，返回所有殖民者的技能概览
            if (string.IsNullOrEmpty(target) || target.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                pawns = map.mapPawns.FreeColonists.ToList();
            }
            else
            {
                var pawn = map.mapPawns.FreeColonists
                    .FirstOrDefault(p => p.Name.ToStringFull.Contains(target) || 
                                       p.LabelShort.Equals(target, StringComparison.OrdinalIgnoreCase));
                
                if (pawn == null)
                {
                    LogError($"Colonist '{target}' not found");
                    return false;
                }
                pawns = new List<Pawn> { pawn };
            }

            var sb = new StringBuilder();
            
            foreach (var pawn in pawns)
            {
                if (pawn.skills == null) continue;
                
                sb.AppendLine($"=== {pawn.LabelShort} ===");
                
                // 按技能等级排序，显示最高的技能
                var orderedSkills = pawn.skills.skills
                    .OrderByDescending(s => s.Level)
                    .ToList();
                
                foreach (var skill in orderedSkills)
                {
                    string passionStr = skill.passion switch
                    {
                        Passion.Minor => "☆",
                        Passion.Major => "★★",
                        _ => ""
                    };
                    
                    bool disabled = skill.TotallyDisabled;
                    string levelStr = disabled ? "X" : skill.Level.ToString();
                    
                    sb.AppendLine($"  {skill.def.skillLabel}: {levelStr} {passionStr}");
                }
                
                // 推荐的最佳工作类型
                var bestWorkTypes = GetRecommendedWorkTypes(pawn);
                if (bestWorkTypes.Any())
                {
                    sb.AppendLine($"  [推荐工作]: {string.Join(", ", bestWorkTypes)}");
                }
                
                sb.AppendLine();
            }
            
            LogExecution(sb.ToString());
            Messages.Message($"TSS: Skill info for {pawns.Count} colonist(s) logged", MessageTypeDefOf.NeutralEvent);
            return true;
        }
        
        /// <summary>
        /// 根据技能推荐最佳工作类型
        /// </summary>
        private List<string> GetRecommendedWorkTypes(Pawn pawn)
        {
            var recommendations = new List<(string work, float score)>();
            
            if (pawn.skills == null) return new List<string>();
            
            // 技能到工作类型的映射
            var skillToWork = new Dictionary<SkillDef, string[]>
            {
                { SkillDefOf.Shooting, new[] { "Hunting" } },
                { SkillDefOf.Melee, new[] { "Hunting" } },
                { SkillDefOf.Construction, new[] { "Construction" } },
                { SkillDefOf.Mining, new[] { "Mining" } },
                { SkillDefOf.Cooking, new[] { "Cooking" } },
                { SkillDefOf.Plants, new[] { "Growing" } },
                { SkillDefOf.Animals, new[] { "Handling" } },
                { SkillDefOf.Crafting, new[] { "Crafting", "Smithing", "Tailoring" } },
                { SkillDefOf.Artistic, new[] { "Art" } },
                { SkillDefOf.Medicine, new[] { "Doctor" } },
                { SkillDefOf.Social, new[] { "Warden" } },
                { SkillDefOf.Intellectual, new[] { "Research" } }
            };
            
            foreach (var skill in pawn.skills.skills)
            {
                if (skill.TotallyDisabled) continue;
                
                if (skillToWork.TryGetValue(skill.def, out var workTypes))
                {
                    float score = skill.Level;
                    if (skill.passion == Passion.Minor) score += 3;
                    if (skill.passion == Passion.Major) score += 6;
                    
                    foreach (var work in workTypes)
                    {
                        recommendations.Add((work, score));
                    }
                }
            }
            
            return recommendations
                .OrderByDescending(r => r.score)
                .Take(3)
                .Select(r => r.work)
                .ToList();
        }
    }
    
    /// <summary>
    /// 自动根据技能分配工作优先级
    /// </summary>
    public class AutoAssignWorkCommand : BaseAICommand
    {
        public override string ActionName => "AutoAssignWork";

        public override string GetDescription()
        {
            return "Automatically assign work priorities based on colonist skills and passions";
        }

        public override bool Execute(string? target = null, object? parameters = null)
        {
            var map = Find.CurrentMap;
            if (map == null)
            {
                LogError("No map available");
                return false;
            }

            List<Pawn> pawns;
            
            if (string.IsNullOrEmpty(target) || target.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                pawns = map.mapPawns.FreeColonists.Where(p => p.workSettings != null).ToList();
            }
            else
            {
                var pawn = map.mapPawns.FreeColonists
                    .FirstOrDefault(p => p.Name.ToStringFull.Contains(target) || 
                                       p.LabelShort.Equals(target, StringComparison.OrdinalIgnoreCase));
                
                if (pawn == null)
                {
                    LogError($"Colonist '{target}' not found");
                    return false;
                }
                if (pawn.workSettings == null)
                {
                    LogError($"Colonist '{target}' cannot work");
                    return false;
                }
                pawns = new List<Pawn> { pawn };
            }

            int assignedCount = 0;
            
            foreach (var pawn in pawns)
            {
                AssignWorkBySkills(pawn);
                assignedCount++;
            }
            
            LogExecution($"Auto-assigned work priorities for {assignedCount} colonist(s)");
            Messages.Message($"TSS: Work priorities auto-assigned for {assignedCount} colonist(s)", MessageTypeDefOf.NeutralEvent);
            return true;
        }
        
        private void AssignWorkBySkills(Pawn pawn)
        {
            if (pawn.skills == null || pawn.workSettings == null) return;
            
            // 获取所有工作类型
            var workTypes = DefDatabase<WorkTypeDef>.AllDefs.ToList();
            
            foreach (var workType in workTypes)
            {
                if (!pawn.WorkTypeIsDisabled(workType))
                {
                    // 计算该工作类型的适合度
                    float aptitude = 0;
                    
                    if (workType.relevantSkills != null)
                    {
                        foreach (var skillDef in workType.relevantSkills)
                        {
                            var skill = pawn.skills.GetSkill(skillDef);
                            if (skill != null && !skill.TotallyDisabled)
                            {
                                aptitude += skill.Level;
                                if (skill.passion == Passion.Minor) aptitude += 5;
                                if (skill.passion == Passion.Major) aptitude += 10;
                            }
                        }
                        
                        // 按相关技能数量平均
                        if (workType.relevantSkills.Count > 0)
                        {
                            aptitude /= workType.relevantSkills.Count;
                        }
                    }
                    
                    // 根据适合度设置优先级
                    int priority;
                    if (aptitude >= 15) priority = 1;      // 非常擅长
                    else if (aptitude >= 10) priority = 2; // 擅长
                    else if (aptitude >= 5) priority = 3;  // 一般
                    else priority = 4;                      // 不擅长但可以做
                    
                    pawn.workSettings.SetPriority(workType, priority);
                }
            }
        }
    }

    /// <summary>
    /// Toggle draft status for a colonist
    /// </summary>
    public class DraftPawnCommand : BaseAICommand
    {
        public override string ActionName => "DraftPawn";

        public override string GetDescription()
        {
            return "Toggle draft status for a specific colonist";
        }

        public override bool Execute(string? target = null, object? parameters = null)
        {
            if (string.IsNullOrEmpty(target))
            {
                LogError("Target colonist name is required");
                return false;
            }

            var pawn = Find.CurrentMap.mapPawns.FreeColonists
                .FirstOrDefault(p => p.Name.ToStringFull.Contains(target) || 
                                   p.LabelShort.Equals(target, StringComparison.OrdinalIgnoreCase));

            if (pawn == null)
            {
                LogError($"Colonist '{target}' not found");
                return false;
            }

            if (pawn.drafter == null)
            {
                LogError($"Colonist '{target}' cannot be drafted");
                return false;
            }

            // Toggle draft status
            bool newDraftStatus = !pawn.drafter.Drafted;
            pawn.drafter.Drafted = newDraftStatus;

            LogExecution($"Draft status for {pawn.LabelShort} set to {newDraftStatus}");
            return true;
        }
    }

    /// <summary>
    /// Order a drafted colonist to move to a location
    /// </summary>
    public class MovePawnCommand : BaseAICommand
    {
        public override string ActionName => "MovePawn";

        public override string GetDescription()
        {
            return "Order a drafted colonist to move to a specific location (x, z)";
        }

        public override bool Execute(string? target = null, object? parameters = null)
        {
            if (string.IsNullOrEmpty(target))
            {
                LogError("Target colonist name is required");
                return false;
            }

            var pawn = Find.CurrentMap.mapPawns.FreeColonists
                .FirstOrDefault(p => p.Name.ToStringFull.Contains(target) || 
                                   p.LabelShort.Equals(target, StringComparison.OrdinalIgnoreCase));

            if (pawn == null)
            {
                LogError($"Colonist '{target}' not found");
                return false;
            }

            if (!pawn.Drafted)
            {
                LogError($"Colonist '{target}' must be drafted to move");
                return false;
            }

            if (parameters is Dictionary<string, object> locationData &&
                locationData.TryGetValue("x", out var xObj) &&
                locationData.TryGetValue("z", out var zObj))
            {
                int x = Convert.ToInt32(xObj);
                int z = Convert.ToInt32(zObj);
                IntVec3 dest = new IntVec3(x, 0, z);

                if (!dest.InBounds(pawn.Map))
                {
                    LogError($"Destination {dest} is out of bounds");
                    return false;
                }

                Job job = JobMaker.MakeJob(JobDefOf.Goto, dest);
                pawn.jobs.TryTakeOrderedJob(job);
                
                // ? Visual feedback
                FleckMaker.ThrowMetaIcon(dest, pawn.Map, FleckDefOf.FeedbackGoto);
                
                LogExecution($"Ordered {pawn.LabelShort} to move to {dest}");
                return true;
            }

            LogError("Invalid location parameters");
            return false;
        }
    }

    /// <summary>
    /// Attempt to heal a pawn (using debug/god mode or assigning doctor)
    /// Currently implements a cheat heal for testing/god mode purposes as requested
    /// </summary>
    public class HealPawnCommand : BaseAICommand
    {
        public override string ActionName => "HealPawn";

        public override string GetDescription()
        {
            return "Heal a specific pawn (God Mode)";
        }

        public override bool Execute(string? target = null, object? parameters = null)
        {
            if (string.IsNullOrEmpty(target))
            {
                LogError("Target pawn name is required");
                return false;
            }

            var pawn = Find.CurrentMap.mapPawns.AllPawns
                .FirstOrDefault(p => p.Name.ToStringFull.Contains(target) || 
                                   p.LabelShort.Equals(target, StringComparison.OrdinalIgnoreCase));

            if (pawn == null)
            {
                LogError($"Pawn '{target}' not found");
                return false;
            }

            // Restore body parts and heal injuries
            var injuries = pawn.health.hediffSet.hediffs
                .Where(h => h is Hediff_Injury || h is Hediff_MissingPart)
                .ToList();

            foreach (var injury in injuries)
            {
                pawn.health.RemoveHediff(injury);
            }

            LogExecution($"Healed {pawn.LabelShort}");
            return true;
        }
    }

    /// <summary>
    /// Set work priorities for a colonist
    /// </summary>
    public class SetWorkPriorityCommand : BaseAICommand
    {
        public override string ActionName => "SetWorkPriority";

        public override string GetDescription()
        {
            return "Set work priority for a colonist. Parameters: workType=<string>, priority=<1-4>";
        }

        public override bool Execute(string? target = null, object? parameters = null)
        {
            if (string.IsNullOrEmpty(target))
            {
                LogError("Target colonist name is required");
                return false;
            }

            var pawn = Find.CurrentMap.mapPawns.FreeColonists
                .FirstOrDefault(p => p.Name.ToStringFull.Contains(target) || 
                                   p.LabelShort.Equals(target, StringComparison.OrdinalIgnoreCase));

            if (pawn == null)
            {
                LogError($"Colonist '{target}' not found");
                return false;
            }

            if (parameters is Dictionary<string, object> paramsDict &&
                paramsDict.TryGetValue("workType", out var workTypeObj) &&
                paramsDict.TryGetValue("priority", out var priorityObj))
            {
                string workTypeStr = workTypeObj.ToString();
                int priority = Convert.ToInt32(priorityObj);

                var workDef = DefDatabase<WorkTypeDef>.AllDefs
                    .FirstOrDefault(w => w.defName.Equals(workTypeStr, StringComparison.OrdinalIgnoreCase) ||
                                       w.labelShort.Equals(workTypeStr, StringComparison.OrdinalIgnoreCase));

                if (workDef == null)
                {
                    LogError($"Work type '{workTypeStr}' not found");
                    return false;
                }

                if (pawn.workSettings == null)
                {
                    LogError($"Colonist '{target}' cannot work");
                    return false;
                }

                pawn.workSettings.SetPriority(workDef, priority);
                LogExecution($"Set {workDef.labelShort} priority to {priority} for {pawn.LabelShort}");
                return true;
            }

            LogError("Invalid parameters. Required: workType, priority");
            return false;
        }
    }

    /// <summary>
    /// Order a colonist to equip a specific weapon
    /// </summary>
    public class EquipWeaponCommand : BaseAICommand
    {
        public override string ActionName => "EquipWeapon";

        public override string GetDescription()
        {
            return "Order a colonist to equip a specific weapon";
        }

        public override bool Execute(string? target = null, object? parameters = null)
        {
            if (string.IsNullOrEmpty(target))
            {
                LogError("Target colonist name is required");
                return false;
            }

            var pawn = Find.CurrentMap.mapPawns.FreeColonists
                .FirstOrDefault(p => p.Name.ToStringFull.Contains(target) || 
                                   p.LabelShort.Equals(target, StringComparison.OrdinalIgnoreCase));

            if (pawn == null)
            {
                LogError($"Colonist '{target}' not found");
                return false;
            }

            string weaponName = "";
            if (parameters is Dictionary<string, object> paramsDict &&
                paramsDict.TryGetValue("weapon", out var weaponObj))
            {
                weaponName = weaponObj.ToString();
            }
            else
            {
                LogError("Weapon name is required in parameters");
                return false;
            }

            var weapon = Find.CurrentMap.listerThings.ThingsInGroup(ThingRequestGroup.Weapon)
                .Where(w => !w.IsForbidden(Faction.OfPlayer) && 
                           (w.Label.Contains(weaponName) || w.def.defName.Contains(weaponName)))
                .OrderBy(w => w.Position.DistanceTo(pawn.Position))
                .FirstOrDefault();

            if (weapon == null)
            {
                LogError($"Weapon '{weaponName}' not found or forbidden");
                return false;
            }

            if (!pawn.CanReserveAndReach(weapon, PathEndMode.ClosestTouch, Danger.None))
            {
                LogError($"Colonist cannot reach weapon '{weapon.Label}'");
                return false;
            }

            var job = JobMaker.MakeJob(JobDefOf.Equip, weapon);
            pawn.jobs.TryTakeOrderedJob(job);
            
            LogExecution($"Ordered {pawn.LabelShort} to equip {weapon.Label}");
            return true;
        }
    }
}
