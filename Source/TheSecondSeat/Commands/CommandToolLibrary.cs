using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;
using RimWorld;

namespace TheSecondSeat.Commands
{
    /// <summary>
    /// AI 命令工具库（主文件）
    /// 提供标准化命令定义和执行方法，供 LLM 调用和操作
    /// 
    /// ? 使用 partial 类拆分：
    /// - CommandToolLibrary.cs (主文件)：核心结构、注册逻辑
    /// - CommandToolLibrary_Batch.cs：批量命令注册
    /// - CommandToolLibrary_Work.cs：工作命令注册
    /// </summary>
    public static partial class CommandToolLibrary
    {
        #region 命令数据结构
        
        /// <summary>
        /// 命令定义
        /// </summary>
        public class CommandDefinition
        {
            public string commandId;           // 命令唯一标识
            public string category;            // 类别
            public string displayName;         // 显示名称
            public string description;         // 描述
            public List<ParameterDef> parameters = new List<ParameterDef>();  // 参数列表
            public string example;             // 使用示例
            public string notes;               // 注解
        }
        
        /// <summary>
        /// 参数定义
        /// </summary>
        public class ParameterDef
        {
            public string name;                // 参数名
            public string type;                // 类型 (string, int, float, bool, IntVec3, etc.)
            public bool required;              // 是否必须
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
        
        #region 命令注册器
        
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

            // ⭐ 校验：检查是否有对应的实现类
            ValidateImplementations();
        }

        /// <summary>
        /// 校验所有注册的命令是否有对应的 IAICommand 实现
        /// </summary>
        private static void ValidateImplementations()
        {
            var missingImpls = new List<string>();
            foreach (var cmdId in commandRegistry.Keys)
            {
                if (CommandRegistry.GetCommand(cmdId) == null)
                {
                    missingImpls.Add(cmdId);
                }
            }

            if (missingImpls.Count > 0)
            {
                Log.Error($"[CommandToolLibrary] ⚠️ 严重警告：发现 {missingImpls.Count} 个命令没有对应的 C# 实现类！LLM 调用这些命令时将失败。");
                Log.Error($"缺失的命令: {string.Join(", ", missingImpls)}");
                Log.Error("请确保为每个命令创建了实现 IAICommand 接口的类，且 ActionName 与 commandId 一致。");
            }
        }
        
        /// <summary>
        /// 注册所有命令
        /// </summary>
        private static void RegisterAllCommands()
        {
            // === 1. 殖民者和单位操作 ===
            RegisterPawnManagementCommands();
            
            // === 2. 资源和物品管理 ===
            RegisterResourceCommands();
            
            // === 3. 建筑和区域管理 ===
            RegisterBuildingCommands();
            
            // === 4. 工作指派命令 ===
            RegisterWorkCommands();  // ? 在 CommandToolLibrary_Work.cs 中实现
            
            // === 5. 事件触发与控制 ===
            RegisterEventCommands();
            
            // === 6. 批量操作命令 ===
            RegisterBatchCommands();  // ? 在 CommandToolLibrary_Batch.cs 中实现
            
            // === 7. 查询和信息获取 ===
            RegisterQueryCommands();
        }
        
        #endregion
        
        #region 1. 殖民者和单位操作命令
        
        private static void RegisterPawnManagementCommands()
        {
            // 1.1 征召/解除征召
            Register(new CommandDefinition
            {
                commandId = "DraftPawn",
                category = "PawnManagement",
                displayName = "征召殖民者",
                description = "将指定殖民者设为征召状态，可接受手动战斗命令",
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
                description = "安排为指定殖民者安排医疗救治",
                parameters = new List<ParameterDef>
                {
                    new ParameterDef { name = "pawnName", type = "string", required = false, description = "殖民者名字（为空则治疗所有伤员）" },
                    new ParameterDef { name = "priority", type = "bool", required = false, defaultValue = "true", description = "是否设为最高优先" }
                },
                example = "{ \"action\": \"HealPawn\", \"pawnName\": \"张三\" }",
                notes = "需要有可用的医生和医疗物品"
            });
            
            // 1.4 设置工作优先级
            Register(new CommandDefinition
            {
                commandId = "SetWorkPriority",
                category = "PawnManagement",
                displayName = "设置工作优先级",
                description = "调整殖民者对特定工作类型的优先级",
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
                notes = "武器必须在殖民者可达范围且未被禁止"
            });
        }
        
        #endregion
        
        #region 2. 资源和物品操作命令
        
        private static void RegisterResourceCommands()
        {
            // 2.1 禁止物品
            Register(new CommandDefinition
            {
                commandId = "ForbidItems",
                category = "ResourceManagement",
                displayName = "禁止物品",
                description = "禁止指定类型的物品",
                parameters = new List<ParameterDef>
                {
                    new ParameterDef { name = "limit", type = "int", required = false, defaultValue = "-1", description = "限制数量" }
                },
                example = "{ \"action\": \"ForbidItems\", \"limit\": 10 }",
                notes = "禁止的物品不会被殖民者互动"
            });

            // 2.2 允许物品
            Register(new CommandDefinition
            {
                commandId = "AllowItems",
                category = "ResourceManagement",
                displayName = "允许物品",
                description = "允许指定类型的物品（解除禁止）",
                parameters = new List<ParameterDef>
                {
                    new ParameterDef { name = "limit", type = "int", required = false, defaultValue = "-1", description = "限制数量" }
                },
                example = "{ \"action\": \"AllowItems\", \"limit\": 10 }",
                notes = ""
            });
        }
        
        #endregion
        
        #region 3. 建筑和区域操作命令
        
        private static void RegisterBuildingCommands()
        {
            // 目前没有已实现的建筑命令
            // 待实现: CancelBuild, Deconstruct
        }
        
        #endregion
        
        #region 5. 事件触发与控制命令
        
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
                example = "{ \"action\": \"TriggerEvent\", \"eventType\": \"raid\", \"comment\": \"来袭！展现你们的力量！\" }",
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

            // 5.3 叙事者降临
            Register(new CommandDefinition
            {
                commandId = "Descent",
                category = "Event",
                displayName = "叙事者降临",
                description = "【离开玩家身边】从立绘形态降临到游戏世界中，成为可行动的实体角色。" +
                              "降临后你的立绘会消失，因为你已经进入了游戏世界。" +
                              "这是一种牺牲陪伴换取行动能力的选择。",
                parameters = new List<ParameterDef>
                {
                    new ParameterDef { name = "mode", type = "string", required = false, defaultValue = "assist",
                        validValues = new List<string> { "assist", "attack", "friendly", "hostile", "援助", "敌对" },
                        description = "降临模式：assist/friendly/援助(协助殖民者), attack/hostile/敌对(敌对测试，用于调试)" },
                    new ParameterDef { name = "x", type = "int", required = false, 
                        description = "降临目标X坐标（可选，不填则使用玩家选中位置或随机）" },
                    new ParameterDef { name = "z", type = "int", required = false, 
                        description = "降临目标Z坐标（可选，不填则使用玩家选中位置或随机）" }
                },
                example = "{ \"action\": \"Descent\", \"mode\": \"assist\", \"x\": 120, \"z\": 80 }",
                notes = "降临 = 离开玩家屏幕，进入游戏世界。有冷却时间限制。"
            });

            // 5.4 叙事者回归
            Register(new CommandDefinition
            {
                commandId = "Ascend",
                category = "Event",
                displayName = "叙事者回归",
                description = "【回到玩家身边】从游戏世界中回归，恢复为立绘形态陪伴玩家。" +
                              "回归后你会重新出现在玩家的屏幕上，继续以立绘形态与玩家交流。",
                parameters = new List<ParameterDef>(),
                example = "{ \"action\": \"Ascend\" }",
                notes = "回归 = 离开游戏世界，回到玩家屏幕。只有在降临状态下才能使用。"
            });
            
        }
        
        #endregion
        
        #region 7. 查询和信息获取命令
        
        private static void RegisterQueryCommands()
        {
            // 7.1 获取地图位置信息
            Register(new CommandDefinition
            {
                commandId = "GetMapLocation",
                category = "Query",
                displayName = "获取位置信息",
                description = "获取地图尺寸和居住区中心坐标，用于定位",
                parameters = new List<ParameterDef>(),
                example = "{ \"action\": \"GetMapLocation\" }",
                notes = "返回地图大小和以最大居住区为中心的锚点坐标"
            });

            // 7.2 扫描地图态势
            Register(new CommandDefinition
            {
                commandId = "ScanMap",
                category = "Query",
                displayName = "扫描地图态势",
                description = "扫描地图上的特定目标（威胁、访客、资源）并报告其方位",
                parameters = new List<ParameterDef>
                {
                    new ParameterDef { name = "target", type = "string", required = false, defaultValue = "hostiles",
                        validValues = new List<string> { "hostiles", "friendlies", "resources", "all" },
                        description = "扫描目标类型：hostiles(敌人), friendlies(访客/盟友), resources(高价值资源), all(全部)" }
                },
                example = "{ \"action\": \"ScanMap\", \"target\": \"hostiles\" }",
                notes = "报告目标相对于居住区中心的方位和数量"
            });

            // 7.3 获取事件类别列表
            Register(new CommandDefinition
            {
                commandId = "GetIncidentCategories",
                category = "Query",
                displayName = "获取事件类别",
                description = "获取所有可用的事件类别列表",
                parameters = new List<ParameterDef>(),
                example = "{ \"action\": \"GetIncidentCategories\" }",
                notes = "返回类别列表，如：ThreatBig, ThreatSmall, Misc, etc."
            });

            // 7.4 获取特定类别的事件列表
            Register(new CommandDefinition
            {
                commandId = "GetIncidentList",
                category = "Query",
                displayName = "获取事件列表",
                description = "获取指定类别下的所有事件定义",
                parameters = new List<ParameterDef>
                {
                    new ParameterDef { name = "target", type = "string", required = true, description = "事件类别名称 (CategoryName)" },
                    new ParameterDef { name = "page", type = "int", required = false, defaultValue = "0", description = "页码" },
                    new ParameterDef { name = "pageSize", type = "int", required = false, defaultValue = "10", description = "每页数量" },
                    new ParameterDef { name = "search", type = "string", required = false, description = "搜索关键词" }
                },
                example = "{ \"action\": \"GetIncidentList\", \"target\": \"ThreatBig\", \"page\": 0, \"pageSize\": 5 }",
                notes = "支持分页和搜索"
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
        /// 获取所有命令
        /// </summary>
        public static List<CommandDefinition> GetAllCommands()
        {
            Initialize();
            return commandRegistry.Values.ToList();
        }
        
        /// <summary>
        /// 获取指定类别的命令
        /// </summary>
        public static List<CommandDefinition> GetCommandsByCategory(string category)
        {
            Initialize();
            return commandRegistry.Values.Where(c => c.category == category).ToList();
        }
        
        /// <summary>
        /// 获取命令
        /// </summary>
        public static CommandDefinition? GetCommand(string commandId)
        {
            Initialize();
            return commandRegistry.TryGetValue(commandId, out var def) ? def : null;
        }
        
        /// <summary>
        /// 获取所有类别
        /// </summary>
        public static List<string> GetCategories()
        {
            Initialize();
            return commandRegistry.Values.Select(c => c.category).Distinct().ToList();
        }
        
        /// <summary>
        /// 生成供 LLM 调用的命令文档（JSON 格式）
        /// </summary>
        public static string GenerateCommandDocumentation()
        {
            Initialize();
            
            var doc = new
            {
                version = "1.0",
                description = "The Second Seat 命令工具库 - 供 LLM 调用和操作",
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
        /// 生成精简命令列表（供 LLM 快速参考）
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
