using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Verse;
using RimWorld;

namespace TheSecondSeat.Commands
{
    /// <summary>
    /// ⭐ 通用指令包装器
    /// 将 XML 定义的 CommandDef 包装为可执行的 IAICommand
    /// 
    /// 核心功能:
    /// 1. 解析 XML 中的 CommandDef
    /// 2. 通过反射调用 DebugAction
    /// 3. 执行简单操作 (SimpleActionType)
    /// 4. 委托给现有 C# 指令
    /// </summary>
    public class Command_GenericDefWrapper : BaseAICommand
    {
        private readonly CommandDef _def;
        private static Dictionary<string, DateTime> _cooldownTracker = new Dictionary<string, DateTime>();
        private static Dictionary<string, int> _dailyUsageTracker = new Dictionary<string, int>();
        private static DateTime _lastResetDate = DateTime.MinValue;
        
        public Command_GenericDefWrapper(CommandDef def)
        {
            _def = def ?? throw new ArgumentNullException(nameof(def));
        }
        
        public override string ActionName => _def.actionName;
        
        public override string GetDescription() => _def.GetFullDescription();
        
        public override bool Execute(string target = null, object parameters = null)
        {
            // 1. 检查权限
            if (!CheckPermissions(out string permError))
            {
                LogError(permError);
                return false;
            }
            
            // 2. 检查冷却和使用限制
            if (!CheckUsageLimits(out string limitError))
            {
                LogError(limitError);
                return false;
            }
            
            // 3. 解析目标
            object resolvedTarget = null;
            if (_def.requiresTarget)
            {
                resolvedTarget = ResolveTarget(target);
                if (resolvedTarget == null)
                {
                    LogError($"Could not find target: {target}");
                    return false;
                }
            }
            
            // 4. 解析参数
            var resolvedParams = ResolveParameters(parameters);
            
            // 5. 执行指令
            bool success = false;
            string resultMessage = "";
            
            try
            {
                if (!string.IsNullOrEmpty(_def.delegateToCommand))
                {
                    success = ExecuteDelegate(target, parameters);
                }
                else if (!string.IsNullOrEmpty(_def.debugActionClass))
                {
                    success = ExecuteDebugAction(resolvedTarget, resolvedParams);
                }
                else if (_def.simpleAction != SimpleActionType.None)
                {
                    success = ExecuteSimpleAction(resolvedTarget, resolvedParams, out resultMessage);
                }
                else
                {
                    LogError("No execution method configured");
                    return false;
                }
            }
            catch (Exception ex)
            {
                LogError($"Execution failed: {ex.Message}");
                return false;
            }
            
            // 6. 记录使用
            if (success)
            {
                RecordUsage();
                
                // 7. 发送反馈
                if (_def.showMessage)
                {
                    string message = success 
                        ? string.Format(_def.successMessage ?? $"{ActionName} executed successfully", target, resultMessage)
                        : string.Format(_def.failureMessage ?? $"{ActionName} failed", target);
                    
                    Messages.Message($"TSS: {message}", MessageTypeDefOf.NeutralEvent);
                }
            }
            
            return success;
        }
        
        // ============================================
        // 权限检查
        // ============================================
        
        private bool CheckPermissions(out string error)
        {
            error = null;
            
            if (_def.requiresDevMode && !Prefs.DevMode)
            {
                error = "This command requires Developer Mode";
                return false;
            }
            
            if (_def.minAffinity > -100f)
            {
                // TODO: 获取当前好感度并检查
                // var agent = NarratorManager.Instance?.CurrentAgent;
                // if (agent != null && agent.Affinity < _def.minAffinity)
                // {
                //     error = $"Requires affinity >= {_def.minAffinity}";
                //     return false;
                // }
            }
            
            return true;
        }
        
        private bool CheckUsageLimits(out string error)
        {
            error = null;
            
            // 重置每日计数器
            if (DateTime.Now.Date != _lastResetDate.Date)
            {
                _dailyUsageTracker.Clear();
                _lastResetDate = DateTime.Now;
            }
            
            // 检查冷却
            if (_def.cooldownTicks > 0)
            {
                string cooldownKey = $"{_def.defName}_cooldown";
                if (_cooldownTracker.TryGetValue(cooldownKey, out DateTime lastUsed))
                {
                    double ticksSinceUse = (DateTime.Now - lastUsed).TotalSeconds * 60; // 近似 tick
                    if (ticksSinceUse < _def.cooldownTicks)
                    {
                        int remainingTicks = (int)(_def.cooldownTicks - ticksSinceUse);
                        error = $"Command on cooldown. {remainingTicks} ticks remaining";
                        return false;
                    }
                }
            }
            
            // 检查每日限制
            if (_def.dailyLimit > 0)
            {
                string usageKey = $"{_def.defName}_daily";
                int usageCount = _dailyUsageTracker.ContainsKey(usageKey) ? _dailyUsageTracker[usageKey] : 0;
                if (usageCount >= _def.dailyLimit)
                {
                    error = $"Daily limit reached ({_def.dailyLimit} uses)";
                    return false;
                }
            }
            
            return true;
        }
        
        private void RecordUsage()
        {
            if (_def.cooldownTicks > 0)
            {
                _cooldownTracker[$"{_def.defName}_cooldown"] = DateTime.Now;
            }
            
            if (_def.dailyLimit > 0)
            {
                string usageKey = $"{_def.defName}_daily";
                int currentCount = _dailyUsageTracker.ContainsKey(usageKey) ? _dailyUsageTracker[usageKey] : 0;
                _dailyUsageTracker[usageKey] = currentCount + 1;
            }
        }
        
        // ============================================
        // 目标解析
        // ============================================
        
        private object ResolveTarget(string targetStr)
        {
            if (string.IsNullOrEmpty(targetStr)) return null;
            
            var map = Find.CurrentMap;
            if (map == null) return null;
            
            switch (_def.targetType)
            {
                case CommandTargetType.Pawn:
                    return map.mapPawns.AllPawns
                        .FirstOrDefault(p => MatchesPawnName(p, targetStr));
                    
                case CommandTargetType.Colonist:
                    return map.mapPawns.FreeColonists
                        .FirstOrDefault(p => MatchesPawnName(p, targetStr));
                    
                case CommandTargetType.DeadPawn:
                    return map.mapPawns.AllPawns
                        .Where(p => p.Dead)
                        .FirstOrDefault(p => MatchesPawnName(p, targetStr));
                    
                case CommandTargetType.Prisoner:
                    return map.mapPawns.PrisonersOfColony
                        .FirstOrDefault(p => MatchesPawnName(p, targetStr));
                    
                case CommandTargetType.Animal:
                    return map.mapPawns.AllPawns
                        .Where(p => p.RaceProps.Animal)
                        .FirstOrDefault(p => MatchesPawnName(p, targetStr));
                    
                case CommandTargetType.Thing:
                    return map.listerThings.AllThings
                        .FirstOrDefault(t => (t.Label != null && t.Label.IndexOf(targetStr, StringComparison.OrdinalIgnoreCase) >= 0) ||
                                           (t.def != null && t.def.defName != null && t.def.defName.IndexOf(targetStr, StringComparison.OrdinalIgnoreCase) >= 0));
                    
                case CommandTargetType.Building:
                    return map.listerBuildings.allBuildingsColonist
                        .FirstOrDefault(b => (b.Label != null && b.Label.IndexOf(targetStr, StringComparison.OrdinalIgnoreCase) >= 0) ||
                                           (b.def != null && b.def.defName != null && b.def.defName.IndexOf(targetStr, StringComparison.OrdinalIgnoreCase) >= 0));
                    
                case CommandTargetType.Cell:
                    // 解析 "x,z" 格式
                    var parts = targetStr.Split(',');
                    if (parts.Length == 2 && int.TryParse(parts[0], out int x) && int.TryParse(parts[1], out int z))
                    {
                        return new IntVec3(x, 0, z);
                    }
                    return null;
                    
                case CommandTargetType.Faction:
                    return Find.FactionManager.AllFactions
                        .FirstOrDefault(f => f.Name != null && f.Name.IndexOf(targetStr, StringComparison.OrdinalIgnoreCase) >= 0);
                    
                default:
                    return null;
            }
        }
        
        private bool MatchesPawnName(Pawn pawn, string name)
        {
            return (pawn.Name != null && pawn.Name.ToStringFull != null && pawn.Name.ToStringFull.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0) ||
                   (pawn.LabelShort != null && pawn.LabelShort.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0);
        }
        
        // ============================================
        // 参数解析
        // ============================================
        
        private Dictionary<string, object> ResolveParameters(object parameters)
        {
            var result = new Dictionary<string, object>();
            
            if (parameters is Dictionary<string, object> dict)
            {
                foreach (var kvp in dict)
                {
                    result[kvp.Key] = kvp.Value;
                }
            }
            
            // 填充默认值
            foreach (var paramDef in _def.parameters)
            {
                if (!result.ContainsKey(paramDef.name) && !string.IsNullOrEmpty(paramDef.defaultValue))
                {
                    result[paramDef.name] = ConvertParameter(paramDef.defaultValue, paramDef.type);
                }
            }
            
            return result;
        }
        
        private object ConvertParameter(string value, string type)
        {
            return type.ToLowerInvariant() switch
            {
                "int" => int.TryParse(value, out int i) ? i : 0,
                "float" => float.TryParse(value, out float f) ? f : 0f,
                "bool" => bool.TryParse(value, out bool b) && b,
                "thingdef" => DefDatabase<ThingDef>.GetNamedSilentFail(value),
                "pawnkinddef" => DefDatabase<PawnKindDef>.GetNamedSilentFail(value),
                _ => value
            };
        }
        
        // ============================================
        // 执行方法
        // ============================================
        
        private bool ExecuteDelegate(string target, object parameters)
        {
            var delegateCommand = CommandRegistry.GetCommand(_def.delegateToCommand);
            if (delegateCommand == null)
            {
                LogError($"Delegate command not found: {_def.delegateToCommand}");
                return false;
            }
            
            return delegateCommand.Execute(target, parameters);
        }
        
        private bool ExecuteDebugAction(object target, Dictionary<string, object> parameters)
        {
            try
            {
                // 查找类型
                Type actionClass = null;
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    actionClass = assembly.GetType(_def.debugActionClass);
                    if (actionClass != null) break;
                }
                
                if (actionClass == null)
                {
                    LogError($"Debug action class not found: {_def.debugActionClass}");
                    return false;
                }
                
                // 查找方法
                var method = actionClass.GetMethod(_def.debugActionMethod, 
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
                
                if (method == null)
                {
                    LogError($"Debug action method not found: {_def.debugActionMethod}");
                    return false;
                }
                
                // 准备参数
                var methodParams = method.GetParameters();
                object[] args = new object[methodParams.Length];
                
                for (int i = 0; i < methodParams.Length; i++)
                {
                    var param = methodParams[i];
                    
                    // 尝试从目标或参数中获取值
                    if (target != null && param.ParameterType.IsAssignableFrom(target.GetType()))
                    {
                        args[i] = target;
                    }
                    else if (parameters.TryGetValue(param.Name, out object value))
                    {
                        args[i] = value;
                    }
                    else if (param.HasDefaultValue)
                    {
                        args[i] = param.DefaultValue;
                    }
                    else
                    {
                        args[i] = param.ParameterType.IsValueType 
                            ? Activator.CreateInstance(param.ParameterType) 
                            : null;
                    }
                }
                
                // 执行
                object instance = method.IsStatic ? null : Activator.CreateInstance(actionClass);
                method.Invoke(instance, args);
                
                return true;
            }
            catch (Exception ex)
            {
                LogError($"Debug action failed: {ex.Message}");
                return false;
            }
        }
        
        private bool ExecuteSimpleAction(object target, Dictionary<string, object> parameters, out string resultMessage)
        {
            resultMessage = "";
            var map = Find.CurrentMap;
            if (map == null) return false;
            
            switch (_def.simpleAction)
            {
                // ---- Pawn 操作 ----
                case SimpleActionType.HealPawn:
                    if (target is Pawn healPawn)
                    {
                        var injuries = healPawn.health.hediffSet.hediffs
                            .Where(h => h is Hediff_Injury)
                            .ToList();
                        foreach (var injury in injuries)
                        {
                            healPawn.health.RemoveHediff(injury);
                        }
                        resultMessage = $"Healed {injuries.Count} injuries";
                        return true;
                    }
                    return false;
                    
                case SimpleActionType.KillPawn:
                    if (target is Pawn killPawn)
                    {
                        killPawn.Kill(null);
                        resultMessage = $"Killed {killPawn.LabelShort}";
                        return true;
                    }
                    return false;
                    
                case SimpleActionType.DraftPawn:
                    if (target is Pawn draftPawn && draftPawn.drafter != null)
                    {
                        draftPawn.drafter.Drafted = true;
                        resultMessage = $"Drafted {draftPawn.LabelShort}";
                        return true;
                    }
                    return false;
                    
                case SimpleActionType.UndraftPawn:
                    if (target is Pawn undraftPawn && undraftPawn.drafter != null)
                    {
                        undraftPawn.drafter.Drafted = false;
                        resultMessage = $"Undrafted {undraftPawn.LabelShort}";
                        return true;
                    }
                    return false;
                    
                // ---- 物品操作 ----
                case SimpleActionType.SpawnThing:
                    if (parameters.TryGetValue("thingDef", out object thingDefObj) && 
                        parameters.TryGetValue("count", out object countObj))
                    {
                        ThingDef thingDef = thingDefObj as ThingDef ?? 
                            DefDatabase<ThingDef>.GetNamedSilentFail(thingDefObj?.ToString() ?? "");
                        int count = Convert.ToInt32(countObj);
                        
                        if (thingDef != null)
                        {
                            IntVec3 spawnPos = target is IntVec3 cell ? cell : 
                                CellFinder.RandomClosewalkCellNear(map.Center, map, 10);
                            
                            Thing thing = ThingMaker.MakeThing(thingDef);
                            thing.stackCount = Math.Min(count, thingDef.stackLimit);
                            GenSpawn.Spawn(thing, spawnPos, map);
                            resultMessage = $"Spawned {thing.Label}";
                            return true;
                        }
                    }
                    return false;
                    
                case SimpleActionType.DestroyThing:
                    if (target is Thing destroyThing)
                    {
                        destroyThing.Destroy();
                        resultMessage = $"Destroyed {destroyThing.Label}";
                        return true;
                    }
                    return false;
                    
                case SimpleActionType.ForbidThing:
                    if (target is Thing forbidThing)
                    {
                        forbidThing.SetForbidden(true);
                        resultMessage = $"Forbade {forbidThing.Label}";
                        return true;
                    }
                    return false;
                    
                case SimpleActionType.UnforbidThing:
                    if (target is Thing unforbidThing)
                    {
                        unforbidThing.SetForbidden(false);
                        resultMessage = $"Unforbade {unforbidThing.Label}";
                        return true;
                    }
                    return false;
                    
                // ---- 资源操作 ----
                case SimpleActionType.AddSilver:
                    if (parameters.TryGetValue("amount", out object silverAmount))
                    {
                        int amount = Convert.ToInt32(silverAmount);
                        Thing silver = ThingMaker.MakeThing(ThingDefOf.Silver);
                        silver.stackCount = amount;
                        IntVec3 pos = CellFinder.RandomClosewalkCellNear(map.Center, map, 10);
                        GenSpawn.Spawn(silver, pos, map);
                        resultMessage = $"Added {amount} silver";
                        return true;
                    }
                    return false;
                    
                // ---- 事件操作 ----
                case SimpleActionType.TriggerIncident:
                    if (parameters.TryGetValue("incidentDef", out object incidentDefObj))
                    {
                        string incidentName = incidentDefObj?.ToString() ?? "";
                        var incidentDef = DefDatabase<IncidentDef>.GetNamedSilentFail(incidentName);
                        
                        if (incidentDef != null)
                        {
                            IncidentParms parms = StorytellerUtility.DefaultParmsNow(incidentDef.category, map);
                            incidentDef.Worker.TryExecute(parms);
                            resultMessage = $"Triggered {incidentDef.label}";
                            return true;
                        }
                    }
                    return false;
                    
                // ---- 派系操作 ----
                case SimpleActionType.ImproveRelation:
                    if (target is Faction faction && parameters.TryGetValue("amount", out object relAmount))
                    {
                        int amount = Convert.ToInt32(relAmount);
                        faction.TryAffectGoodwillWith(Faction.OfPlayer, amount);
                        resultMessage = $"Improved relation with {faction.Name} by {amount}";
                        return true;
                    }
                    return false;
                    
                case SimpleActionType.WorsenRelation:
                    if (target is Faction badFaction && parameters.TryGetValue("amount", out object badAmount))
                    {
                        int amount = Convert.ToInt32(badAmount);
                        badFaction.TryAffectGoodwillWith(Faction.OfPlayer, -amount);
                        resultMessage = $"Worsened relation with {badFaction.Name} by {amount}";
                        return true;
                    }
                    return false;
                    
                default:
                    LogError($"Unsupported simple action: {_def.simpleAction}");
                    return false;
            }
        }
    }
    
    /// <summary>
    /// ⭐ CommandDef 加载器
    /// 在游戏启动时扫描所有 CommandDef 并注册为可执行指令
    /// </summary>
    [StaticConstructorOnStartup]
    public static class CommandDefLoader
    {
        static CommandDefLoader()
        {
            LongEventHandler.QueueLongEvent(LoadAllCommandDefs, "TSS_LoadingCommandDefs", false, null);
        }
        
        private static void LoadAllCommandDefs()
        {
            int count = 0;
            
            try
            {
                var allDefs = DefDatabase<CommandDef>.AllDefsListForReading;
                
                foreach (var def in allDefs)
                {
                    try
                    {
                        var wrapper = new Command_GenericDefWrapper(def);
                        CommandRegistry.RegisterCommand(wrapper);
                        count++;
                        
                        if (Prefs.DevMode)
                        {
                            Log.Message($"[TSS] Loaded CommandDef: {def.actionName}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"[TSS] Failed to load CommandDef {def.defName}: {ex.Message}");
                    }
                }
                
                Log.Message($"[TSS] CommandDefLoader: Registered {count} XML-defined commands");
            }
            catch (Exception ex)
            {
                Log.Error($"[TSS] CommandDefLoader failed: {ex.Message}");
            }
        }
    }
}