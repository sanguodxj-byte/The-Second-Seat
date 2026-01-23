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
            // 4.1 指派植物砍伐 (DesignatePlantCut)
            Register(new CommandDefinition
            {
                commandId = "DesignatePlantCut",
                category = "Work",
                displayName = "指派砍伐",
                description = "指派植物进行砍伐（支持树木、枯萎植物、野生植物）",
                parameters = new List<ParameterDef>
                {
                    new ParameterDef { name = "target", type = "string", required = false, defaultValue = "all",
                        validValues = new List<string> { "trees", "blighted", "wild", "all" }, description = "砍伐目标类型" },
                    new ParameterDef { name = "limit", type = "int", required = false, defaultValue = "-1", description = "限制数量（-1=全部）" },
                    new ParameterDef { name = "nearFocus", type = "bool", required = false, defaultValue = "false", description = "优先选择靠近鼠标/镜头的目标" }
                },
                example = "{ \"action\": \"DesignatePlantCut\", \"target\": \"blighted\", \"limit\": 10, \"nearFocus\": true }",
                notes = "target可选：trees(树木), blighted(枯萎), wild(野生), all(所有)。"
            });
        }
    }
}
