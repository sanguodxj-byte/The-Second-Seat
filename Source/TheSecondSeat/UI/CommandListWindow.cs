using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using RimWorld;
using TheSecondSeat.Commands;

namespace TheSecondSeat.UI
{
    /// <summary>
    /// 显示所有可用AI指令的窗口
    /// ? 点击命令行自动输入到聊天窗口
    /// </summary>
    public class CommandListWindow : Window
    {
        private Vector2 scrollPosition = Vector2.zero;
        private string searchFilter = "";
        private string selectedCategory = "全部";
        
        private static readonly Color HeaderColor = new Color(0.15f, 0.60f, 0.70f, 1f);
        private static readonly Color RowColor1 = new Color(0.12f, 0.13f, 0.14f, 0.8f);
        private static readonly Color RowColor2 = new Color(0.10f, 0.11f, 0.12f, 0.8f);
        private static readonly Color RowHoverColor = new Color(0.20f, 0.25f, 0.30f, 0.9f);
        private static readonly Color ImplementedColor = new Color(0.4f, 0.8f, 0.4f);
        private static readonly Color NotImplementedColor = new Color(0.8f, 0.4f, 0.4f);

        // ? 回调：输入到聊天窗口
        public static Action<string>? OnCommandSelected;

        public override Vector2 InitialSize => new Vector2(900f, 650f);

        public CommandListWindow()
        {
            doCloseButton = true;
            doCloseX = true;
            draggable = true;
            resizeable = true;
            forcePause = false;
            closeOnClickedOutside = false;
        }

        public override void DoWindowContents(Rect inRect)
        {
            float curY = 0f;
            
            // 标题
            Text.Font = GameFont.Medium;
            GUI.color = HeaderColor;
            Widgets.Label(new Rect(0f, curY, inRect.width, 35f), "AI 可用指令列表 (点击命令自动输入)");
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            curY += 40f;
            
            // 搜索和筛选
            DrawSearchAndFilter(new Rect(0f, curY, inRect.width, 30f));
            curY += 35f;
            
            // 提示
            GUI.color = new Color(0.6f, 0.8f, 0.6f);
            Text.Font = GameFont.Tiny;
            Widgets.Label(new Rect(0f, curY, inRect.width, 20f), "[提示] 点击任意命令行，将自动输入到聊天窗口。绿色=已实现，红色=未实现");
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            curY += 25f;

            // 指令列表
            var commands = GetAllCommands();
            
            // 应用筛选
            if (!string.IsNullOrEmpty(searchFilter))
            {
                commands = commands.Where(c => 
                    c.CommandName.ToLower().Contains(searchFilter.ToLower()) ||
                    c.DisplayName.Contains(searchFilter) ||
                    c.Description.Contains(searchFilter) ||
                    c.ExamplePrompt.ToLower().Contains(searchFilter.ToLower())
                ).ToList();
            }
            
            if (selectedCategory != "全部")
            {
                commands = commands.Where(c => c.Category == selectedCategory).ToList();
            }

            var contentRect = new Rect(0f, curY, inRect.width, inRect.height - curY - 10f);
            DrawCommandTable(contentRect, commands);
        }
        
        private void DrawSearchAndFilter(Rect rect)
        {
            // 搜索框
            var searchRect = new Rect(rect.x, rect.y, 250f, rect.height);
            GUI.SetNextControlName("CommandSearch");
            searchFilter = Widgets.TextField(searchRect, searchFilter);
            if (string.IsNullOrEmpty(searchFilter) && GUI.GetNameOfFocusedControl() != "CommandSearch")
            {
                GUI.color = new Color(0.5f, 0.5f, 0.5f);
                Widgets.Label(new Rect(searchRect.x + 5f, searchRect.y, searchRect.width, searchRect.height), "搜索命令...");
                GUI.color = Color.white;
            }
            
            // 分类筛选
            var categories = new List<string> { "全部", "批量操作", "单位管理", "资源管理", "事件控制", "查询" };
            float btnWidth = 70f;
            float startX = rect.x + 260f;
            
            for (int i = 0; i < categories.Count; i++)
            {
                var btnRect = new Rect(startX + i * (btnWidth + 5f), rect.y, btnWidth, rect.height);
                bool isSelected = selectedCategory == categories[i];
                
                if (isSelected)
                {
                    GUI.color = HeaderColor;
                }
                
                if (Widgets.ButtonText(btnRect, categories[i], true, true, isSelected ? Color.white : Color.gray))
                {
                    selectedCategory = categories[i];
                }
                
                GUI.color = Color.white;
            }
        }

        private List<CommandInfo> GetAllCommands()
        {
            return new List<CommandInfo>
            {
                // === 批量操作（已实现）===
                new CommandInfo("BatchHarvest", "批量收获", "批量操作",
                    "指定所有成熟作物进行收获", 
                    "帮我收获所有成熟的作物",
                    "All/Mature/Blighted", true),
                
                new CommandInfo("BatchMine", "批量采矿", "批量操作",
                    "指定所有可采矿资源进行开采", 
                    "帮我把所有金属矿都标记采矿",
                    "all/metal/stone/components", true),
                
                new CommandInfo("BatchLogging", "批量伐木", "批量操作",
                    "指定所有成熟树木进行砍伐", 
                    "帮我砍伐所有成熟的树",
                    "无参数（≥90%成熟）", true),
                
                new CommandInfo("BatchEquip", "批量装备", "批量操作",
                    "为所有无武器殖民者装备最佳武器", 
                    "给所有殖民者装备武器",
                    "Weapon/Armor", true),
                
                new CommandInfo("BatchCapture", "批量俘获", "批量操作",
                    "俘获所有倒地的敌方人形单位", 
                    "俘获所有倒地的敌人",
                    "无参数（需要看守）", true),
                
                new CommandInfo("PriorityRepair", "优先修复", "批量操作",
                    "指定所有受损建筑进行修复", 
                    "修复所有损坏的建筑",
                    "All/Damaged(<80%)", true),
                
                new CommandInfo("EmergencyRetreat", "紧急撤退", "批量操作",
                    "征召所有未征召的殖民者", 
                    "紧急撤退！征召所有人",
                    "无参数", true),
                
                new CommandInfo("DesignatePlantCut", "清理植物", "批量操作",
                    "指定植物进行砍伐清理", 
                    "清理所有枯萎的植物",
                    "All/Blighted/Trees/Wild", true),
                
                // === 殖民者管理（? 已实现）===
                new CommandInfo("DraftPawn", "征召殖民者", "单位管理",
                    "将指定殖民者设为征召状态", 
                    "征召张三",
                    "pawnName/drafted(bool)", true),
                
                new CommandInfo("MovePawn", "移动殖民者", "单位管理",
                    "命令已征召殖民者移动到指定位置", 
                    "让张三移动到坐标50,50",
                    "pawnName + x,z坐标", true),
                
                new CommandInfo("HealPawn", "治疗殖民者", "单位管理",
                    "优先为指定殖民者安排医疗", 
                    "治疗李四",
                    "pawnName(空=全部伤员)", true),
                
                new CommandInfo("SetWorkPriority", "设置工作优先级", "单位管理",
                    "调整殖民者工作优先级", 
                    "把张三的医疗设为最高优先",
                    "pawnName+workType+priority", true),
                
                new CommandInfo("EquipWeapon", "装备武器", "单位管理",
                    "命令殖民者装备指定武器", 
                    "让张三装备突击步枪",
                    "pawnName/weaponDef(可选)", true),
                
                // === 资源管理（已实现）===
                new CommandInfo("ForbidItems", "禁止物品", "资源管理",
                    "禁止地图上的可搬运物品", 
                    "禁止所有掉落的物品",
                    "无参数/可选count", true),
                
                new CommandInfo("AllowItems", "解禁物品", "资源管理",
                    "解除所有被禁止的物品", 
                    "解禁所有物品",
                    "无参数/可选count", true),
                
                // === 事件控制（对弈者模式）===
                new CommandInfo("TriggerEvent", "触发事件", "事件控制",
                    "立即触发游戏事件（仅对弈者模式）", 
                    "发动一次袭击",
                    "raid/trader/wanderer/disease/resource/eclipse/toxic", true),
                
                new CommandInfo("ScheduleEvent", "安排事件", "事件控制",
                    "在未来某时刻触发事件", 
                    "10分钟后发动袭击",
                    "事件类型 + delayMinutes", true),
                
                new CommandInfo("ChangeWeather", "修改天气", "事件控制",
                    "改变当前地图天气（仅对弈者模式）", 
                    "把天气改成下雨",
                    "Clear/Rain/Fog/Snow等", false),
                
                // === 查询命令（通过对话实现）===
                new CommandInfo("GetColonists", "获取殖民者", "查询",
                    "获取所有殖民者信息（通过对话）", 
                    "告诉我殖民地有哪些人",
                    "直接询问AI即可", true),
                
                new CommandInfo("GetResources", "获取资源", "查询",
                    "获取殖民地资源库存（通过对话）", 
                    "我们还有多少食物？",
                    "直接询问AI即可", true),
                
                new CommandInfo("GetThreats", "获取威胁", "查询",
                    "获取当前地图威胁信息（通过对话）", 
                    "有敌人吗？",
                    "直接询问AI即可", true),
                
                new CommandInfo("GetColonyStatus", "殖民地状态", "查询",
                    "获取殖民地整体状态（通过对话）", 
                    "殖民地现在怎么样？",
                    "直接询问AI即可", true),
                
                // === 暂不支持 ===
                new CommandInfo("DesignateConstruction", "指定建造", "资源管理",
                    "【暂不支持】需要复杂的建筑蓝图", 
                    "在(10,10)建一堵墙",
                    "需要坐标+建筑类型+材料", false),
            };
        }

        private void DrawCommandTable(Rect rect, List<CommandInfo> commands)
        {
            var innerRect = rect.ContractedBy(5f);
            
            // 计算内容高度
            float contentHeight = 40f + commands.Count * 75f;
            var viewRect = new Rect(0f, 0f, innerRect.width - 20f, contentHeight);

            Widgets.BeginScrollView(innerRect, ref scrollPosition, viewRect);

            float curY = 0f;

            // 表头
            DrawTableHeader(new Rect(0f, curY, viewRect.width, 35f));
            curY += 40f;

            // 数据行
            for (int i = 0; i < commands.Count; i++)
            {
                var rowColor = i % 2 == 0 ? RowColor1 : RowColor2;
                var rowRect = new Rect(0f, curY, viewRect.width, 70f);
                
                // ? 鼠标悬停效果
                if (Mouse.IsOver(rowRect))
                {
                    rowColor = RowHoverColor;
                }
                
                DrawCommandRow(rowRect, commands[i], rowColor, i);
                
                // ? 点击事件：输入到聊天窗口
                if (Widgets.ButtonInvisible(rowRect))
                {
                    OnCommandClicked(commands[i]);
                }
                
                curY += 75f;
            }

            Widgets.EndScrollView();
        }

        private void DrawTableHeader(Rect rect)
        {
            Widgets.DrawBoxSolid(rect, new Color(0.08f, 0.09f, 0.10f, 0.95f));
            Widgets.DrawBox(rect, 2);

            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = HeaderColor;

            float[] colWidths = { 130f, 80f, 80f, 200f, 200f, 100f };
            string[] headers = { "命令ID", "显示名", "分类", "描述", "示例提示词", "参数格式" };
            float x = rect.x + 10f;
            
            for (int i = 0; i < headers.Length; i++)
            {
                Widgets.Label(new Rect(x, rect.y, colWidths[i], rect.height), headers[i]);
                x += colWidths[i] + 10f;
            }

            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
        }

        private void DrawCommandRow(Rect rect, CommandInfo command, Color bgColor, int index)
        {
            Widgets.DrawBoxSolid(rect, bgColor);
            
            // 状态指示条
            var statusColor = command.IsImplemented ? ImplementedColor : NotImplementedColor;
            Widgets.DrawBoxSolid(new Rect(rect.x, rect.y, 4f, rect.height), statusColor);

            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;

            float[] colWidths = { 130f, 80f, 80f, 200f, 200f, 100f };
            float x = rect.x + 14f;

            // 命令名（黄色高亮）
            GUI.color = new Color(0.95f, 0.90f, 0.50f);
            Widgets.Label(new Rect(x, rect.y + 5f, colWidths[0], 25f), command.CommandName);
            x += colWidths[0] + 10f;

            // 显示名
            GUI.color = Color.white;
            Widgets.Label(new Rect(x, rect.y + 5f, colWidths[1], 25f), command.DisplayName);
            x += colWidths[1] + 10f;

            // 分类
            GUI.color = new Color(0.7f, 0.8f, 0.9f);
            Widgets.Label(new Rect(x, rect.y + 5f, colWidths[2], 25f), command.Category);
            x += colWidths[2] + 10f;

            // 描述
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = new Color(0.85f, 0.85f, 0.85f);
            Widgets.Label(new Rect(x, rect.y + 5f, colWidths[3], 60f), command.Description);
            x += colWidths[3] + 10f;

            // 示例提示词（绿色高亮，可点击）
            GUI.color = new Color(0.5f, 0.9f, 0.5f);
            Widgets.Label(new Rect(x, rect.y + 5f, colWidths[4], 60f), $"「{command.ExamplePrompt}」");
            x += colWidths[4] + 10f;

            // 参数格式
            GUI.color = new Color(0.70f, 0.80f, 0.90f);
            Widgets.Label(new Rect(x, rect.y + 5f, colWidths[5], 60f), command.TargetFormat);

            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
        }

        /// <summary>
        /// ? 点击命令时的处理
        /// </summary>
        private void OnCommandClicked(CommandInfo command)
        {
            // 复制示例提示词到剪贴板（备用）
            GUIUtility.systemCopyBuffer = command.ExamplePrompt;
            
            // ? 直接发送到 AI（不需要手动确认）
            TryInputAndSendToNarratorWindow(command.ExamplePrompt);
        }
        
        /// <summary>
        /// ? 输入文本并自动发送到叙事者窗口
        /// </summary>
        private void TryInputAndSendToNarratorWindow(string text)
        {
            // 查找已打开的 NarratorWindow
            var narratorWindow = Find.WindowStack?.Windows
                .OfType<NarratorWindow>()
                .FirstOrDefault();
            
            if (narratorWindow != null)
            {
                // ? 直接发送（不需要手动点击发送按钮）
                NarratorWindow.SetInputTextAndSend(text);
                Log.Message($"[CommandListWindow] 已自动发送命令: {text}");
                
                // 显示确认消息
                Messages.Message($"? 已发送: {text}", MessageTypeDefOf.PositiveEvent);
                
                // ? 关闭命令列表窗口（可选）
                this.Close();
            }
            else
            {
                // 如果窗口未打开，先打开窗口再发送
                Find.WindowStack.Add(new NarratorWindow());
                
                // 延迟一帧后发送（确保窗口已初始化）
                Verse.LongEventHandler.ExecuteWhenFinished(() => 
                {
                    NarratorWindow.SetInputTextAndSend(text);
                    Messages.Message($"? 已发送: {text}", MessageTypeDefOf.PositiveEvent);
                });
                
                Log.Message($"[CommandListWindow] 已打开对话窗口并发送命令: {text}");
                
                // 关闭命令列表窗口
                this.Close();
            }
        }

        /// <summary>
        /// 命令信息结构
        /// </summary>
        private class CommandInfo
        {
            public string CommandName;
            public string DisplayName;
            public string Category;
            public string Description;
            public string ExamplePrompt;  // ? 示例提示词
            public string TargetFormat;
            public bool IsImplemented;    // ? 是否已实现

            public CommandInfo(string commandName, string displayName, string category,
                string description, string examplePrompt, string targetFormat, bool isImplemented)
            {
                CommandName = commandName;
                DisplayName = displayName;
                Category = category;
                Description = description;
                ExamplePrompt = examplePrompt;
                TargetFormat = targetFormat;
                IsImplemented = isImplemented;
            }
        }
    }

    /// <summary>
    /// 打开指令列表的 Gizmo
    /// </summary>
    public class Command_OpenCommandList : Command
    {
        public Command_OpenCommandList()
        {
            defaultLabel = "查看指令列表";
            defaultDesc = "显示所有可用的AI指令";
            icon = ContentFinder<Texture2D>.Get("UI/Commands/InfoButton", false);
        }

        public override void ProcessInput(Event ev)
        {
            base.ProcessInput(ev);
            Find.WindowStack.Add(new CommandListWindow());
        }
    }
}
