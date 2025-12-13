using System.Collections.Generic;

namespace TheSecondSeat.Commands
{
    /// <summary>
    /// 命令工具库 - 批量命令部分
    /// </summary>
    public static partial class CommandToolLibrary
    {
        /// <summary>
        /// 注册批量操作命令
        /// ? v1.6.27: 添加 limit 和 nearFocus 参数
        /// </summary>
        private static void RegisterBatchCommands()
        {
            // 6.1 批量收获
            Register(new CommandDefinition
            {
                commandId = "BatchHarvest",
                category = "Batch",
                displayName = "批量收获",
                description = "指派所有成熟的作物进行收获",
                parameters = new List<ParameterDef>
                {
                    new ParameterDef { name = "limit", type = "int", required = false, defaultValue = "-1", description = "限制数量（-1=全部）" },
                    new ParameterDef { name = "nearFocus", type = "bool", required = false, defaultValue = "false", description = "优先选择靠近鼠标/镜头的目标" }
                },
                example = "{ \"action\": \"BatchHarvest\", \"limit\": 10, \"nearFocus\": true }",
                notes = "自动选择最近的10个成熟作物收获"
            });
            
            // 6.2 批量装备
            Register(new CommandDefinition
            {
                commandId = "BatchEquip",
                category = "Batch",
                displayName = "批量装备",
                description = "为所有未装备的殖民者装备最佳武器",
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
                description = "指派所有可采矿资源",
                parameters = new List<ParameterDef>
                {
                    new ParameterDef { name = "target", type = "string", required = false, defaultValue = "all",
                        validValues = new List<string> { "all", "metal", "stone", "components" }, description = "采矿目标类型" },
                    new ParameterDef { name = "limit", type = "int", required = false, defaultValue = "-1", description = "限制数量（-1=全部）" },
                    new ParameterDef { name = "nearFocus", type = "bool", required = false, defaultValue = "false", description = "优先选择靠近鼠标/镜头的目标" }
                },
                example = "{ \"action\": \"BatchMine\", \"target\": \"metal\", \"limit\": 5, \"nearFocus\": true }",
                notes = "采矿最近的5个金属矿"
            });
            
            // 6.4 批量伐木
            Register(new CommandDefinition
            {
                commandId = "BatchLogging",
                category = "Batch",
                displayName = "批量伐木",
                description = "指派所有成熟树木进行砍伐",
                parameters = new List<ParameterDef>
                {
                    new ParameterDef { name = "limit", type = "int", required = false, defaultValue = "-1", description = "限制数量（-1=全部）" },
                    new ParameterDef { name = "nearFocus", type = "bool", required = false, defaultValue = "false", description = "优先选择靠近鼠标/镜头的目标" }
                },
                example = "{ \"action\": \"BatchLogging\", \"limit\": 20, \"nearFocus\": true }",
                notes = "只砍伐90%以上成熟的树木，优先砍伐最近的20棵"
            });
            
            // 6.5 批量俘虏
            Register(new CommandDefinition
            {
                commandId = "BatchCapture",
                category = "Batch",
                displayName = "批量俘虏",
                description = "指派所有倒地敌人进行俘虏",
                parameters = new List<ParameterDef>(),
                example = "{ \"action\": \"BatchCapture\" }",
                notes = "需要有可用的看守员"
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
                notes = "会将所有殖民者设置为征召状态"
            });
            
            // 6.7 优先修复
            Register(new CommandDefinition
            {
                commandId = "PriorityRepair",
                category = "Batch",
                displayName = "优先修复",
                description = "指派所有受损建筑进行修复",
                parameters = new List<ParameterDef>(),
                example = "{ \"action\": \"PriorityRepair\" }",
                notes = ""
            });
        }
    }
}
