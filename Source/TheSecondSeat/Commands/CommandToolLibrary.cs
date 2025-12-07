using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;
using RimWorld;

namespace TheSecondSeat.Commands
{
    /// <summary>
    /// ?? AI 命令工具库
    /// 提供标准化的命令定义和执行方法，供 LLM 检索与调用
    /// 
    /// ? 使用方式：
    /// 1. LLM 通过 GetAllCommands() 获取可用命令列表
    /// 2. LLM 构造 JSON 格式的命令请求
    /// 3. 系统通过 ExecuteCommand() 执行命令
    /// </summary>
    public static class CommandToolLibrary
    {
        #region 命令定义数据结构
        
        /// <summary>
        /// 命令定义
        /// </summary>
        public class CommandDefinition
        {
            public string commandId;           // 命令唯一标识
            public string category;            // 分类
            public string displayName;         // 显示名称
            public string description;         // 描述
            public List<ParameterDef> parameters = new List<ParameterDef>();  // 参数列表
            public string example;             // 使用示例
            public string notes;               // 备注
        }
        
        /// <summary>
        /// 参数定义
        /// </summary>
        public class ParameterDef
        {
            public string name;                // 参数名
            public string type;                // 类型 (string, int, float, bool, IntVec3, etc.)
            public bool required;              // 是否必需
            public string defaultValue;        // 默认值
            public string description;         // 描述
            public List<string> validValues;   // 有效值列表（枚举类型）
        }
        
        /// <summary>
        /// 命令执行结果
        /// </summary>
        public class CommandResult
        {
            public bool success;
            public string message;
            public object data;                // 返回的数据
        }
        
        #endregion
        
        #region 命令注册表
        
        private static Dictionary<string, CommandDefinition> commandRegistry = new Dictionary<string, CommandDefinition>();
        private static bool initialized = false;
        
        /// <summary>
        /// 初始化命令库
        /// </summary>
        public static void Initialize()
        {
            if (initialized) return;
            
            RegisterAllCommands();
            initialized = true;
            
            Log.Message($"[CommandToolLibrary] 已注册 {commandRegistry.Count} 个命令");
        }
        
        /// <summary>
        /// 注册所有命令
        /// </summary>
        private static void RegisterAllCommands()
        {
            // === 1. 殖民者与单位管理 ===
            RegisterPawnManagementCommands();
            
            // === 2. 资源与物品管理 ===
            RegisterResourceCommands();
            
            // === 3. 建筑与区域管理 ===
            RegisterBuildingCommands();
            
            // === 4. 工作与任务管理 ===
            RegisterWorkCommands();
            
            // === 5. 事件与叙事控制 ===
            RegisterEventCommands();
            
            // === 6. 批量操作命令 ===
            RegisterBatchCommands();
            
            // === 7. 查询与信息获取 ===
            RegisterQueryCommands();
        }
        
        #endregion
        
        #region 1. 殖民者与单位管理命令
        
        private static void RegisterPawnManagementCommands()
        {
            // 1.1 征召/解除征召
            Register(new CommandDefinition
            {
                commandId = "DraftPawn",
                category = "PawnManagement",
                displayName = "征召殖民者",
                description = "将指定殖民者设为征召状态，可进行手动战斗控制",
                parameters = new List<ParameterDef>
                {
                    new ParameterDef { name = "pawnName", type = "string", required = false, description = "殖民者名字（为空则征召全部）" },
                    new ParameterDef { name = "drafted", type = "bool", required = false, defaultValue = "true", description = "true=征召, false=解除" }
                },
                example = "{ \"action\": \"DraftPawn\", \"pawnName\": \"张三\", \"drafted\": true }",
                notes = "征召后殖民者会停止当前工作"
            });
            
            // 1.2 移动到位置
            Register(new CommandDefinition
            {
                commandId = "MovePawn",
                category = "PawnManagement",
                displayName = "移动殖民者",
                description = "命令已征召的殖民者移动到指定位置",
                parameters = new List<ParameterDef>
                {
                    new ParameterDef { name = "pawnName", type = "string", required = true, description = "殖民者名字" },
                    new ParameterDef { name = "x", type = "int", required = true, description = "目标X坐标" },
                    new ParameterDef { name = "z", type = "int", required = true, description = "目标Z坐标" }
                },
                example = "{ \"action\": \"MovePawn\", \"pawnName\": \"张三\", \"x\": 50, \"z\": 50 }",
                notes = "殖民者必须处于征召状态"
            });
            
            // 1.3 治疗殖民者
            Register(new CommandDefinition
            {
                commandId = "HealPawn",
                category = "PawnManagement",
                displayName = "治疗殖民者",
                description = "优先为指定殖民者安排医疗救治",
                parameters = new List<ParameterDef>
                {
                    new ParameterDef { name = "pawnName", type = "string", required = false, description = "殖民者名字（为空则处理所有伤员）" },
                    new ParameterDef { name = "priority", type = "bool", required = false, defaultValue = "true", description = "是否设为紧急优先" }
                },
                example = "{ \"action\": \"HealPawn\", \"pawnName\": \"李四\" }",
                notes = "需要有可用的医生和医疗用品"
            });
            
            // 1.4 设置工作优先级
            Register(new CommandDefinition
            {
                commandId = "SetWorkPriority",
                category = "PawnManagement",
                displayName = "设置工作优先级",
                description = "调整殖民者特定工作类型的优先级",
                parameters = new List<ParameterDef>
                {
                    new ParameterDef { name = "pawnName", type = "string", required = true, description = "殖民者名字" },
                    new ParameterDef { name = "workType", type = "string", required = true, description = "工作类型",
                        validValues = new List<string> { "Firefighter", "Doctor", "PatientBedRest", "BasicWorker", 
                            "Warden", "Handling", "Cooking", "Hunting", "Construction", "Growing", 
                            "Mining", "PlantCutting", "Smithing", "Tailoring", "Art", "Crafting", 
                            "Hauling", "Cleaning", "Research" } },
                    new ParameterDef { name = "priority", type = "int", required = true, description = "优先级 (1最高, 4最低, 0禁用)" }
                },
                example = "{ \"action\": \"SetWorkPriority\", \"pawnName\": \"张三\", \"workType\": \"Doctor\", \"priority\": 1 }",
                notes = "优先级1-4，0表示禁用该工作"
            });
            
            // 1.5 装备武器
            Register(new CommandDefinition
            {
                commandId = "EquipWeapon",
                category = "PawnManagement",
                displayName = "装备武器",
                description = "命令殖民者装备指定武器",
                parameters = new List<ParameterDef>
                {
                    new ParameterDef { name = "pawnName", type = "string", required = true, description = "殖民者名字" },
                    new ParameterDef { name = "weaponDef", type = "string", required = false, description = "武器DefName（为空则自动选择最佳）" }
                },
                example = "{ \"action\": \"EquipWeapon\", \"pawnName\": \"张三\", \"weaponDef\": \"Gun_AssaultRifle\" }",
                notes = "武器必须在殖民地内且未被禁止"
            });
        }
        
        #endregion
        
        #region 2. 资源与物品管理命令
        
        private static void RegisterResourceCommands()
        {
            // 2.1 禁止/解禁物品
            Register(new CommandDefinition
            {
                commandId = "ForbidItem",
                category = "ResourceManagement",
                displayName = "禁止/解禁物品",
                description = "设置物品的禁止状态",
                parameters = new List<ParameterDef>
                {
                    new ParameterDef { name = "thingDef", type = "string", required = true, description = "物品DefName" },
                    new ParameterDef { name = "forbidden", type = "bool", required = false, defaultValue = "true", description = "true=禁止, false=解禁" },
                    new ParameterDef { name = "scope", type = "string", required = false, defaultValue = "All", 
                        validValues = new List<string> { "All", "Selected", "Area" }, description = "范围" }
                },
                example = "{ \"action\": \"ForbidItem\", \"thingDef\": \"Steel\", \"forbidden\": false }",
                notes = "禁止的物品不会被搬运"
            });
            
            // 2.2 搬运到储存区
            Register(new CommandDefinition
            {
                commandId = "HaulToStorage",
                category = "ResourceManagement",
                displayName = "搬运到储存区",
                description = "将物品搬运到指定储存区",
                parameters = new List<ParameterDef>
                {
                    new ParameterDef { name = "thingDef", type = "string", required = false, description = "物品DefName（为空则搬运所有可搬运物品）" },
                    new ParameterDef { name = "storageZone", type = "string", required = false, description = "目标储存区名称" }
                },
                example = "{ \"action\": \"HaulToStorage\", \"thingDef\": \"MealSimple\" }",
                notes = "需要有可用的搬运工"
            });
            
            // 2.3 丢弃物品
            Register(new CommandDefinition
            {
                commandId = "DropItem",
                category = "ResourceManagement",
                displayName = "丢弃物品",
                description = "命令殖民者丢弃随身携带的物品",
                parameters = new List<ParameterDef>
                {
                    new ParameterDef { name = "pawnName", type = "string", required = true, description = "殖民者名字" },
                    new ParameterDef { name = "thingDef", type = "string", required = false, description = "物品DefName（为空则丢弃全部）" }
                },
                example = "{ \"action\": \"DropItem\", \"pawnName\": \"张三\" }",
                notes = ""
            });
        }
        
        #endregion
        
        #region 3. 建筑与区域管理命令
        
        private static void RegisterBuildingCommands()
        {
            // 3.1 指定建造
            Register(new CommandDefinition
            {
                commandId = "DesignateBuild",
                category = "Building",
                displayName = "指定建造",
                description = "在指定位置规划建造建筑",
                parameters = new List<ParameterDef>
                {
                    new ParameterDef { name = "buildingDef", type = "string", required = true, description = "建筑DefName" },
                    new ParameterDef { name = "x", type = "int", required = true, description = "X坐标" },
                    new ParameterDef { name = "z", type = "int", required = true, description = "Z坐标" },
                    new ParameterDef { name = "rotation", type = "string", required = false, defaultValue = "North",
                        validValues = new List<string> { "North", "South", "East", "West" }, description = "朝向" },
                    new ParameterDef { name = "stuffDef", type = "string", required = false, description = "材料DefName" }
                },
                example = "{ \"action\": \"DesignateBuild\", \"buildingDef\": \"Wall\", \"x\": 10, \"z\": 10, \"stuffDef\": \"BlocksGranite\" }",
                notes = "需要有足够的材料和建造工人"
            });
            
            // 3.2 取消建造
            Register(new CommandDefinition
            {
                commandId = "CancelBuild",
                category = "Building",
                displayName = "取消建造",
                description = "取消指定位置的建造计划",
                parameters = new List<ParameterDef>
                {
                    new ParameterDef { name = "x", type = "int", required = true, description = "X坐标" },
                    new ParameterDef { name = "z", type = "int", required = true, description = "Z坐标" }
                },
                example = "{ \"action\": \"CancelBuild\", \"x\": 10, \"z\": 10 }",
                notes = ""
            });
            
            // 3.3 拆除建筑
            Register(new CommandDefinition
            {
                commandId = "Deconstruct",
                category = "Building",
                displayName = "拆除建筑",
                description = "指定拆除建筑物",
                parameters = new List<ParameterDef>
                {
                    new ParameterDef { name = "x", type = "int", required = true, description = "X坐标" },
                    new ParameterDef { name = "z", type = "int", required = true, description = "Z坐标" }
                },
                example = "{ \"action\": \"Deconstruct\", \"x\": 10, \"z\": 10 }",
                notes = "拆除会回收部分材料"
            });
            
            // 3.4 创建区域
            Register(new CommandDefinition
            {
                commandId = "CreateZone",
                category = "Building",
                displayName = "创建区域",
                description = "创建储存区、种植区或其他区域",
                parameters = new List<ParameterDef>
                {
                    new ParameterDef { name = "zoneType", type = "string", required = true, 
                        validValues = new List<string> { "Stockpile", "Growing", "Dumping" }, description = "区域类型" },
                    new ParameterDef { name = "x1", type = "int", required = true, description = "起始X坐标" },
                    new ParameterDef { name = "z1", type = "int", required = true, description = "起始Z坐标" },
                    new ParameterDef { name = "x2", type = "int", required = true, description = "结束X坐标" },
                    new ParameterDef { name = "z2", type = "int", required = true, description = "结束Z坐标" },
                    new ParameterDef { name = "zoneName", type = "string", required = false, description = "区域名称" }
                },
                example = "{ \"action\": \"CreateZone\", \"zoneType\": \"Stockpile\", \"x1\": 10, \"z1\": 10, \"x2\": 20, \"z2\": 20 }",
                notes = ""
            });
        }
        
        #endregion
        
        #region 4. 工作与任务管理命令
        
        private static void RegisterWorkCommands()
        {
            // 4.1 指定采矿
            Register(new CommandDefinition
            {
                commandId = "DesignateMine",
                category = "Work",
                displayName = "指定采矿",
                description = "指定矿物进行开采",
                parameters = new List<ParameterDef>
                {
                    new ParameterDef { name = "target", type = "string", required = false, defaultValue = "all",
                        validValues = new List<string> { "all", "metal", "stone", "components" }, description = "采矿目标类型" },
                    new ParameterDef { name = "x", type = "int", required = false, description = "特定X坐标" },
                    new ParameterDef { name = "z", type = "int", required = false, description = "特定Z坐标" }
                },
                example = "{ \"action\": \"DesignateMine\", \"target\": \"metal\" }",
                notes = "metal=钢铁/金/银/铀等，stone=石料，components=组件矿"
            });
            
            // 4.2 指定砍伐
            Register(new CommandDefinition
            {
                commandId = "DesignateCut",
                category = "Work",
                displayName = "指定砍伐",
                description = "指定植物进行砍伐",
                parameters = new List<ParameterDef>
                {
                    new ParameterDef { name = "target", type = "string", required = false, defaultValue = "trees",
                        validValues = new List<string> { "trees", "blighted", "wild", "all" }, description = "砍伐目标类型" }
                },
                example = "{ \"action\": \"DesignateCut\", \"target\": \"trees\" }",
                notes = "trees=成熟树木，blighted=枯萎植物，wild=野生植物"
            });
            
            // 4.3 指定收获
            Register(new CommandDefinition
            {
                commandId = "DesignateHarvest",
                category = "Work",
                displayName = "指定收获",
                description = "指定成熟作物进行收获",
                parameters = new List<ParameterDef>
                {
                    new ParameterDef { name = "plantDef", type = "string", required = false, description = "特定植物DefName（为空则收获所有成熟作物）" }
                },
                example = "{ \"action\": \"DesignateHarvest\" }",
                notes = "只会标记已成熟的作物"
            });
            
            // 4.4 设置生产账单
            Register(new CommandDefinition
            {
                commandId = "AddBill",
                category = "Work",
                displayName = "添加生产账单",
                description = "在工作台添加生产任务",
                parameters = new List<ParameterDef>
                {
                    new ParameterDef { name = "workbenchDef", type = "string", required = true, description = "工作台DefName" },
                    new ParameterDef { name = "recipeDef", type = "string", required = true, description = "配方DefName" },
                    new ParameterDef { name = "count", type = "int", required = false, defaultValue = "-1", description = "生产数量 (-1=无限)" },
                    new ParameterDef { name = "targetCount", type = "int", required = false, description = "目标库存数量" }
                },
                example = "{ \"action\": \"AddBill\", \"workbenchDef\": \"ElectricStove\", \"recipeDef\": \"CookMealSimple\", \"count\": 10 }",
                notes = ""
            });
        }
        
        #endregion
        
        #region 5. 事件与叙事控制命令
        
        private static void RegisterEventCommands()
        {
            // 5.1 触发事件
            Register(new CommandDefinition
            {
                commandId = "TriggerEvent",
                category = "Event",
                displayName = "触发事件",
                description = "触发游戏事件（对弈者模式）",
                parameters = new List<ParameterDef>
                {
                    new ParameterDef { name = "eventType", type = "string", required = true,
                        validValues = new List<string> { "raid", "trader", "wanderer", "disease", "resource", "eclipse", "toxic" },
                        description = "事件类型" },
                    new ParameterDef { name = "comment", type = "string", required = false, description = "AI评论" }
                },
                example = "{ \"action\": \"TriggerEvent\", \"eventType\": \"raid\", \"comment\": \"来吧，展现你的能力\" }",
                notes = "仅在对弈者模式下可用"
            });
            
            // 5.2 安排事件
            Register(new CommandDefinition
            {
                commandId = "ScheduleEvent",
                category = "Event",
                displayName = "安排事件",
                description = "在未来某时刻触发事件",
                parameters = new List<ParameterDef>
                {
                    new ParameterDef { name = "eventType", type = "string", required = true,
                        validValues = new List<string> { "raid", "trader", "wanderer", "disease", "resource", "eclipse", "toxic" },
                        description = "事件类型" },
                    new ParameterDef { name = "delayMinutes", type = "int", required = false, defaultValue = "10", description = "延迟时间（游戏分钟）" },
                    new ParameterDef { name = "comment", type = "string", required = false, description = "AI评论" }
                },
                example = "{ \"action\": \"ScheduleEvent\", \"eventType\": \"raid\", \"delayMinutes\": 30 }",
                notes = "仅在对弈者模式下可用"
            });
            
            // 5.3 修改天气
            Register(new CommandDefinition
            {
                commandId = "ChangeWeather",
                category = "Event",
                displayName = "修改天气",
                description = "改变当前地图的天气",
                parameters = new List<ParameterDef>
                {
                    new ParameterDef { name = "weatherDef", type = "string", required = true,
                        validValues = new List<string> { "Clear", "Rain", "RainyThunderstorm", "DryThunderstorm", "FoggyRain", "Fog", "SnowGentle", "SnowHard" },
                        description = "天气DefName" }
                },
                example = "{ \"action\": \"ChangeWeather\", \"weatherDef\": \"Rain\" }",
                notes = "仅在对弈者模式下可用"
            });
        }
        
        #endregion
        
        #region 6. 批量操作命令
        
        private static void RegisterBatchCommands()
        {
            // 6.1 批量收获
            Register(new CommandDefinition
            {
                commandId = "BatchHarvest",
                category = "Batch",
                displayName = "批量收获",
                description = "指定所有成熟作物进行收获",
                parameters = new List<ParameterDef>(),
                example = "{ \"action\": \"BatchHarvest\" }",
                notes = ""
            });
            
            // 6.2 批量装备
            Register(new CommandDefinition
            {
                commandId = "BatchEquip",
                category = "Batch",
                displayName = "批量装备",
                description = "为所有无武器殖民者装备最佳武器",
                parameters = new List<ParameterDef>(),
                example = "{ \"action\": \"BatchEquip\" }",
                notes = ""
            });
            
            // 6.3 批量采矿
            Register(new CommandDefinition
            {
                commandId = "BatchMine",
                category = "Batch",
                displayName = "批量采矿",
                description = "指定所有可采矿资源",
                parameters = new List<ParameterDef>
                {
                    new ParameterDef { name = "target", type = "string", required = false, defaultValue = "all",
                        validValues = new List<string> { "all", "metal", "stone", "components" }, description = "采矿目标类型" }
                },
                example = "{ \"action\": \"BatchMine\", \"target\": \"metal\" }",
                notes = ""
            });
            
            // 6.4 批量伐木
            Register(new CommandDefinition
            {
                commandId = "BatchLogging",
                category = "Batch",
                displayName = "批量伐木",
                description = "指定所有成熟树木进行砍伐",
                parameters = new List<ParameterDef>(),
                example = "{ \"action\": \"BatchLogging\" }",
                notes = "只砍伐90%以上成熟的树木"
            });
            
            // 6.5 批量俘获
            Register(new CommandDefinition
            {
                commandId = "BatchCapture",
                category = "Batch",
                displayName = "批量俘获",
                description = "指定所有倒地敌人进行俘获",
                parameters = new List<ParameterDef>(),
                example = "{ \"action\": \"BatchCapture\" }",
                notes = "需要有可用的看守者"
            });
            
            // 6.6 紧急撤退
            Register(new CommandDefinition
            {
                commandId = "EmergencyRetreat",
                category = "Batch",
                displayName = "紧急撤退",
                description = "征召所有殖民者准备撤退",
                parameters = new List<ParameterDef>(),
                example = "{ \"action\": \"EmergencyRetreat\" }",
                notes = "会将所有殖民者设为征召状态"
            });
            
            // 6.7 优先修复
            Register(new CommandDefinition
            {
                commandId = "PriorityRepair",
                category = "Batch",
                displayName = "优先修复",
                description = "指定所有受损建筑进行修复",
                parameters = new List<ParameterDef>(),
                example = "{ \"action\": \"PriorityRepair\" }",
                notes = ""
            });
        }
        
        #endregion
        
        #region 7. 查询与信息获取命令
        
        private static void RegisterQueryCommands()
        {
            // 7.1 获取殖民者列表
            Register(new CommandDefinition
            {
                commandId = "GetColonists",
                category = "Query",
                displayName = "获取殖民者列表",
                description = "获取所有殖民者的信息",
                parameters = new List<ParameterDef>
                {
                    new ParameterDef { name = "includeDetails", type = "bool", required = false, defaultValue = "false", description = "是否包含详细信息" }
                },
                example = "{ \"action\": \"GetColonists\", \"includeDetails\": true }",
                notes = "返回殖民者名单、健康状态、当前任务等"
            });
            
            // 7.2 获取资源统计
            Register(new CommandDefinition
            {
                commandId = "GetResources",
                category = "Query",
                displayName = "获取资源统计",
                description = "获取殖民地资源库存",
                parameters = new List<ParameterDef>
                {
                    new ParameterDef { name = "category", type = "string", required = false, 
                        validValues = new List<string> { "all", "food", "medicine", "weapons", "materials" },
                        description = "资源类别" }
                },
                example = "{ \"action\": \"GetResources\", \"category\": \"food\" }",
                notes = ""
            });
            
            // 7.3 获取威胁评估
            Register(new CommandDefinition
            {
                commandId = "GetThreats",
                category = "Query",
                displayName = "获取威胁评估",
                description = "获取当前地图上的威胁信息",
                parameters = new List<ParameterDef>(),
                example = "{ \"action\": \"GetThreats\" }",
                notes = "返回敌人数量、位置、威胁等级等"
            });
            
            // 7.4 获取殖民地状态
            Register(new CommandDefinition
            {
                commandId = "GetColonyStatus",
                category = "Query",
                displayName = "获取殖民地状态",
                description = "获取殖民地整体状态概览",
                parameters = new List<ParameterDef>(),
                example = "{ \"action\": \"GetColonyStatus\" }",
                notes = "返回财富、人口、电力、食物储备等"
            });
        }
        
        #endregion
        
        #region 公共 API
        
        /// <summary>
        /// 注册命令
        /// </summary>
        public static void Register(CommandDefinition def)
        {
            if (string.IsNullOrEmpty(def.commandId))
            {
                Log.Warning("[CommandToolLibrary] 命令ID不能为空");
                return;
            }
            
            commandRegistry[def.commandId] = def;
        }
        
        /// <summary>
        /// 获取所有命令定义
        /// </summary>
        public static List<CommandDefinition> GetAllCommands()
        {
            Initialize();
            return commandRegistry.Values.ToList();
        }
        
        /// <summary>
        /// 获取指定分类的命令
        /// </summary>
        public static List<CommandDefinition> GetCommandsByCategory(string category)
        {
            Initialize();
            return commandRegistry.Values.Where(c => c.category == category).ToList();
        }
        
        /// <summary>
        /// 获取命令定义
        /// </summary>
        public static CommandDefinition? GetCommand(string commandId)
        {
            Initialize();
            return commandRegistry.TryGetValue(commandId, out var def) ? def : null;
        }
        
        /// <summary>
        /// 获取所有分类
        /// </summary>
        public static List<string> GetCategories()
        {
            Initialize();
            return commandRegistry.Values.Select(c => c.category).Distinct().ToList();
        }
        
        /// <summary>
        /// 生成 LLM 可用的命令文档（JSON 格式）
        /// </summary>
        public static string GenerateCommandDocumentation()
        {
            Initialize();
            
            var doc = new
            {
                version = "1.0",
                description = "The Second Seat 命令工具库 - 供 LLM 检索与调用",
                categories = GetCategories(),
                commands = commandRegistry.Values.Select(c => new
                {
                    id = c.commandId,
                    category = c.category,
                    name = c.displayName,
                    description = c.description,
                    parameters = c.parameters.Select(p => new
                    {
                        name = p.name,
                        type = p.type,
                        required = p.required,
                        defaultValue = p.defaultValue,
                        description = p.description,
                        validValues = p.validValues
                    }),
                    example = c.example,
                    notes = c.notes
                })
            };
            
            return Newtonsoft.Json.JsonConvert.SerializeObject(doc, Newtonsoft.Json.Formatting.Indented);
        }
        
        /// <summary>
        /// 生成精简版命令列表（供 LLM 快速参考）
        /// </summary>
        public static string GenerateCompactCommandList()
        {
            Initialize();
            
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("# 可用命令列表");
            sb.AppendLine();
            
            foreach (var category in GetCategories())
            {
                sb.AppendLine($"## {category}");
                foreach (var cmd in GetCommandsByCategory(category))
                {
                    var paramStr = string.Join(", ", cmd.parameters.Select(p => 
                        p.required ? p.name : $"[{p.name}]"));
                    sb.AppendLine($"- **{cmd.commandId}**({paramStr}): {cmd.description}");
                }
                sb.AppendLine();
            }
            
            return sb.ToString();
        }
        
        #endregion
    }
}
