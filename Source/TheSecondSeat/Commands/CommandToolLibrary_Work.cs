using System.Collections.Generic;

namespace TheSecondSeat.Commands
{
    /// <summary>
    /// 命令工具库 - 工作指派部分
    /// </summary>
    public static partial class CommandToolLibrary
    {
        /// <summary>
        /// 注册工作指派命令
        /// ? v1.6.27: 添加 limit 和 nearFocus 参数
        /// </summary>
        private static void RegisterWorkCommands()
        {
            // 4.1 指派采矿
            Register(new CommandDefinition
            {
                commandId = "DesignateMine",
                category = "Work",
                displayName = "指派采矿",
                description = "指派矿石进行开采",
                parameters = new List<ParameterDef>
                {
                    new ParameterDef { name = "target", type = "string", required = false, defaultValue = "all",
                        validValues = new List<string> { "all", "metal", "stone", "components" }, description = "采矿目标类型" },
                    new ParameterDef { name = "x", type = "int", required = false, description = "特定X坐标" },
                    new ParameterDef { name = "z", type = "int", required = false, description = "特定Z坐标" },
                    new ParameterDef { name = "limit", type = "int", required = false, defaultValue = "-1", description = "限制数量（-1=全部）" },
                    new ParameterDef { name = "nearFocus", type = "bool", required = false, defaultValue = "false", description = "优先选择靠近鼠标/镜头的目标" }
                },
                example = "{ \"action\": \"DesignateMine\", \"target\": \"metal\", \"limit\": 10, \"nearFocus\": true }",
                notes = "metal=钢铁/金/银/铀等；stone=石材；components=零件。添加limit和nearFocus可精确控制"
            });
            
            // 4.2 指派砍伐
            Register(new CommandDefinition
            {
                commandId = "DesignateCut",
                category = "Work",
                displayName = "指派砍伐",
                description = "指派植物进行砍伐",
                parameters = new List<ParameterDef>
                {
                    new ParameterDef { name = "target", type = "string", required = false, defaultValue = "trees",
                        validValues = new List<string> { "trees", "blighted", "wild", "all" }, description = "砍伐目标类型" },
                    new ParameterDef { name = "limit", type = "int", required = false, defaultValue = "-1", description = "限制数量（-1=全部）" },
                    new ParameterDef { name = "nearFocus", type = "bool", required = false, defaultValue = "false", description = "优先选择靠近鼠标/镜头的目标" }
                },
                example = "{ \"action\": \"DesignateCut\", \"target\": \"trees\", \"limit\": 5, \"nearFocus\": true }",
                notes = "trees=成熟树木；blighted=枯萎植物；wild=野生植物。砍伐最近的5棵树"
            });
            
            // 4.3 指派收获
            Register(new CommandDefinition
            {
                commandId = "DesignateHarvest",
                category = "Work",
                displayName = "指派收获",
                description = "指派成熟作物进行收获",
                parameters = new List<ParameterDef>
                {
                    new ParameterDef { name = "plantDef", type = "string", required = false, description = "特定植物DefName（为空则收获所有成熟作物）" },
                    new ParameterDef { name = "limit", type = "int", required = false, defaultValue = "-1", description = "限制数量（-1=全部）" },
                    new ParameterDef { name = "nearFocus", type = "bool", required = false, defaultValue = "false", description = "优先选择靠近鼠标/镜头的目标" }
                },
                example = "{ \"action\": \"DesignateHarvest\", \"limit\": 50, \"nearFocus\": true }",
                notes = "只收获已成熟作物，优先收获最近的50株"
            });
            
            // 4.4 添加工作单
            Register(new CommandDefinition
            {
                commandId = "AddBill",
                category = "Work",
                displayName = "添加工作单",
                description = "在工作台上添加生产订单",
                parameters = new List<ParameterDef>
                {
                    new ParameterDef { name = "workbenchDef", type = "string", required = true, description = "工作台DefName" },
                    new ParameterDef { name = "recipeDef", type = "string", required = true, description = "配方DefName" },
                    new ParameterDef { name = "count", type = "int", required = false, defaultValue = "-1", description = "生产数量 (-1=永久)" },
                    new ParameterDef { name = "targetCount", type = "int", required = false, description = "目标库存数量" }
                },
                example = "{ \"action\": \"AddBill\", \"workbenchDef\": \"ElectricStove\", \"recipeDef\": \"CookMealSimple\", \"count\": 10 }",
                notes = ""
            });
        }
    }
}
