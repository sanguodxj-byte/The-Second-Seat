using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using RimWorld;

namespace TheSecondSeat.Commands
{
    /// <summary>
    /// ⭐ XML Command Bridge - 允许 Modder 通过 XML 定义 AI 指令
    /// 无需编写 C# 代码，直接包装现有的 DebugActions 或简单逻辑
    /// 
    /// 用法示例 (XML):
    /// <CommandDef>
    ///   <defName>Command_ResurrectPawn</defName>
    ///   <actionName>ResurrectPawn</actionName>
    ///   <label>复活死者</label>
    ///   <description>复活一个已死亡的殖民者</description>
    ///   <debugActionClass>Verse.DebugToolsPawns</debugActionClass>
    ///   <debugActionMethod>Resurrect</debugActionMethod>
    ///   <requiresTarget>true</requiresTarget>
    ///   <targetType>DeadPawn</targetType>
    ///   <parameters>
    ///     <li>
    ///       <name>pawnName</name>
    ///       <type>string</type>
    ///       <required>true</required>
    ///     </li>
    ///   </parameters>
    /// </CommandDef>
    /// </summary>
    public class CommandDef : Def
    {
        // ============================================
        // 基本配置
        // ============================================
        
        /// <summary>
        /// AI 指令名称（用于 CommandRegistry 查找）
        /// 例如: "ResurrectPawn", "SpawnItem", "TriggerRaid"
        /// </summary>
        public string actionName;
        
        /// <summary>
        /// 指令描述（供 AI 理解用途）
        /// </summary>
        public string commandDescription;
        
        /// <summary>
        /// 指令分类标签（用于分组和筛选）
        /// </summary>
        public List<string> tags = new List<string>();
        
        // ============================================
        // 目标配置
        // ============================================
        
        /// <summary>
        /// 是否需要目标
        /// </summary>
        public bool requiresTarget = false;
        
        /// <summary>
        /// 目标类型（用于智能筛选）
        /// Pawn, DeadPawn, Thing, Cell, Zone 等
        /// </summary>
        public CommandTargetType targetType = CommandTargetType.None;
        
        /// <summary>
        /// 目标筛选条件（可选）
        /// 例如: "IsColonist", "IsDrafted", "HasHediff:WoundInfection"
        /// </summary>
        public List<string> targetFilters = new List<string>();
        
        // ============================================
        // 参数配置
        // ============================================
        
        /// <summary>
        /// 指令参数列表
        /// </summary>
        public List<CommandParameterDef> parameters = new List<CommandParameterDef>();
        
        // ============================================
        // 执行方式配置（三选一）
        // ============================================
        
        // ---- 方式1: 调用 DebugAction ----
        
        /// <summary>
        /// Debug 工具类的完整类名
        /// 例如: "Verse.DebugToolsPawns", "RimWorld.DebugToolsGeneral"
        /// </summary>
        public string debugActionClass;
        
        /// <summary>
        /// Debug 方法名
        /// 例如: "Resurrect", "SpawnPawn"
        /// </summary>
        public string debugActionMethod;
        
        // ---- 方式2: 执行简单操作 ----
        
        /// <summary>
        /// 简单操作类型
        /// </summary>
        public SimpleActionType simpleAction = SimpleActionType.None;
        
        /// <summary>
        /// 操作参数（根据 simpleAction 类型使用）
        /// </summary>
        public Dictionary<string, string> actionParams = new Dictionary<string, string>();
        
        // ---- 方式3: 委托给已有的 C# 指令 ----
        
        /// <summary>
        /// 委托的现有指令名称
        /// 如果设置，将直接调用已注册的 IAICommand
        /// </summary>
        public string delegateToCommand;
        
        // ============================================
        // 权限与限制
        // ============================================
        
        /// <summary>
        /// 是否需要开发者模式
        /// </summary>
        public bool requiresDevMode = false;
        
        /// <summary>
        /// 冷却时间（游戏 tick）
        /// </summary>
        public int cooldownTicks = 0;
        
        /// <summary>
        /// 每日使用次数限制（0 = 无限制）
        /// </summary>
        public int dailyLimit = 0;
        
        /// <summary>
        /// 好感度要求（低于此值不可用）
        /// </summary>
        public float minAffinity = -100f;
        
        /// <summary>
        /// 好感度成本（执行后扣除）
        /// </summary>
        public float affinityCost = 0f;
        
        // ============================================
        // 反馈配置
        // ============================================
        
        /// <summary>
        /// 成功时的消息模板（支持 {0}=target, {1}=result 等占位符）
        /// </summary>
        public string successMessage;
        
        /// <summary>
        /// 失败时的消息模板
        /// </summary>
        public string failureMessage;
        
        /// <summary>
        /// 是否显示 RimWorld 消息
        /// </summary>
        public bool showMessage = true;
        
        // ============================================
        // 方法
        // ============================================
        
        /// <summary>
        /// 验证定义是否有效
        /// </summary>
        public override IEnumerable<string> ConfigErrors()
        {
            foreach (var error in base.ConfigErrors())
            {
                yield return error;
            }
            
            if (string.IsNullOrEmpty(actionName))
            {
                yield return "CommandDef requires actionName";
            }
            
            // 检查执行方式是否配置
            bool hasDebugAction = !string.IsNullOrEmpty(debugActionClass) && !string.IsNullOrEmpty(debugActionMethod);
            bool hasSimpleAction = simpleAction != SimpleActionType.None;
            bool hasDelegate = !string.IsNullOrEmpty(delegateToCommand);
            
            int executionMethods = (hasDebugAction ? 1 : 0) + (hasSimpleAction ? 1 : 0) + (hasDelegate ? 1 : 0);
            
            if (executionMethods == 0)
            {
                yield return "CommandDef requires at least one execution method (debugAction, simpleAction, or delegateToCommand)";
            }
            
            if (executionMethods > 1)
            {
                yield return "CommandDef should only have one execution method";
            }
        }
        
        /// <summary>
        /// 获取指令的完整描述（供 AI 使用）
        /// </summary>
        public string GetFullDescription()
        {
            var sb = new System.Text.StringBuilder();
            
            sb.AppendLine($"**{actionName}**: {commandDescription ?? label ?? defName}");
            
            if (requiresTarget)
            {
                sb.AppendLine($"Target: {targetType}");
                if (targetFilters.Count > 0)
                {
                    sb.AppendLine($"Filters: {string.Join(", ", targetFilters)}");
                }
            }
            
            if (parameters.Count > 0)
            {
                sb.AppendLine("Parameters:");
                foreach (var param in parameters)
                {
                    string required = param.required ? "(required)" : "(optional)";
                    sb.AppendLine($"  - {param.name}: {param.type} {required}");
                    if (!string.IsNullOrEmpty(param.description))
                    {
                        sb.AppendLine($"    {param.description}");
                    }
                }
            }
            
            if (minAffinity > -100f)
            {
                sb.AppendLine($"Requires affinity >= {minAffinity}");
            }
            
            if (affinityCost > 0)
            {
                sb.AppendLine($"Costs {affinityCost} affinity");
            }
            
            return sb.ToString();
        }
    }
    
    /// <summary>
    /// 指令参数定义
    /// </summary>
    public class CommandParameterDef
    {
        /// <summary>参数名称</summary>
        public string name;
        
        /// <summary>参数类型（string, int, float, bool, ThingDef, PawnKindDef 等）</summary>
        public string type = "string";
        
        /// <summary>是否必填</summary>
        public bool required = true;
        
        /// <summary>默认值</summary>
        public string defaultValue;
        
        /// <summary>参数描述</summary>
        public string description;
        
        /// <summary>可选值列表（用于枚举类型）</summary>
        public List<string> allowedValues = new List<string>();
    }
    
    /// <summary>
    /// 指令目标类型
    /// </summary>
    public enum CommandTargetType
    {
        None,
        Pawn,           // 任意 Pawn
        Colonist,       // 殖民者
        DeadPawn,       // 死亡的 Pawn
        Prisoner,       // 囚犯
        Animal,         // 动物
        Thing,          // 任意物品
        Building,       // 建筑
        Cell,           // 地图格子
        Zone,           // 区域
        Faction,        // 派系
        WorldObject     // 世界物体
    }
    
    /// <summary>
    /// 简单操作类型（无需编写 C# 代码）
    /// </summary>
    public enum SimpleActionType
    {
        None,
        
        // Pawn 操作
        HealPawn,           // 治疗 Pawn
        KillPawn,           // 杀死 Pawn
        DraftPawn,          // 征召 Pawn
        UndraftPawn,        // 解除征召
        ArrestPawn,         // 逮捕 Pawn
        ReleasePrisoner,    // 释放囚犯
        RecruitPrisoner,    // 招募囚犯
        
        // 物品操作
        SpawnThing,         // 生成物品
        DestroyThing,       // 销毁物品
        ForbidThing,        // 禁止物品
        UnforbidThing,      // 解除禁止
        
        // 建筑操作
        DeconstructBuilding,    // 拆除建筑
        RepairBuilding,         // 修复建筑
        
        // 事件操作
        TriggerIncident,    // 触发事件
        EndGameCondition,   // 结束游戏条件
        
        // 资源操作
        AddSilver,          // 添加白银
        AddComponent,       // 添加零件
        
        // 环境操作
        ChangeWeather,      // 改变天气
        ChangeSeason,       // 改变季节（开发者模式）
        
        // 派系操作
        ImproveRelation,    // 改善关系
        WorsenRelation      // 恶化关系
    }
}