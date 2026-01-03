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
            var categories = new List<string> { "全部", "批量操作", "单位管理", "资源管理", "事件控制", "事件调试", "查询" };
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
                    "批量收获所有成熟作物",
                    "All/Mature/Blighted", true),
                
                new CommandInfo("BatchMine", "批量采矿", "批量操作",
                    "指定所有可采矿资源进行开采", 
                    "把野外所有金属矿都标记采矿",
                    "all/metal/stone/components", true),
                
                new CommandInfo("BatchLogging", "批量伐木", "批量操作",
                    "指定所有成年树木进行开采", 
                    "砍掉地图上所有树木",
                    "无参数（默认90%成熟）", true),
                
                new CommandInfo("BatchEquip", "批量装备", "批量操作",
                    "为所有适合的殖民者装备武器或护甲", 
                    "让所有殖民者装备最好武器",
                    "Weapon/Armor", true),
                
                new CommandInfo("BatchCapture", "批量俘虏", "批量操作",
                    "俘虏所有击倒的敌方单位", 
                    "俘虏所有击倒的敌人",
                    "无参数（自动俘虏）", true),
                
                new CommandInfo("PriorityRepair", "优先修复", "批量操作",
                    "指定所有受损建筑进行修复", 
                    "修复所有破损的建筑",
                    "All/Damaged(<80%)", true),
                
                new CommandInfo("EmergencyRetreat", "紧急撤退", "批量操作",
                    "命令所有未受伤的殖民者撤退", 
                    "所有人撤退，快跑",
                    "无参数", true),
                
                new CommandInfo("DesignatePlantCut", "批量植物", "批量操作",
                    "指定植物进行开采清理", 
                    "清理所有枯萎植物",
                    "All/Blighted/Trees/Wild", true),
                
                // === 殖民者管理（已实现）===
                new CommandInfo("DraftPawn", "征召殖民者", "单位管理",
                    "将指定殖民者设为征召状态", 
                    "征召所有人",
                    "pawnName/drafted(bool)", true),
                
                new CommandInfo("MovePawn", "移动殖民者", "单位管理",
                    "命令战斗中的殖民者移动到指定位置", 
                    "让张三移动到坐标50,50",
                    "pawnName + x,z坐标", true),
                
                new CommandInfo("HealPawn", "治疗殖民者", "单位管理",
                    "优先为指定殖民者安排医疗", 
                    "治疗张三",
                    "pawnName(空=全体成员)", true),
                
                new CommandInfo("SetWorkPriority", "设置工作优先级", "单位管理",
                    "调整殖民者工作优先级", 
                    "把张三的医疗设为最优先",
                    "pawnName+workType+priority", true),
                
                new CommandInfo("EquipWeapon", "装备武器", "单位管理",
                    "让殖民者装备指定武器", 
                    "让张三装备突击步枪",
                    "pawnName/weaponDef(可选)", true),
                
                // === 资源管理（已实现）===
                new CommandInfo("ForbidItems", "禁止物品", "资源管理",
                    "禁止地图上的可搬运物品", 
                    "禁止所有的腐烂物品",
                    "无参数/可选count", true),
                
                new CommandInfo("AllowItems", "允许物品", "资源管理",
                    "允许所有被禁止的物品", 
                    "允许所有物品",
                    "无参数/可选count", true),
                
                // === 事件控制（对弈者模式）===
                new CommandInfo("TriggerEvent", "触发事件", "事件控制",
                    "触发指定游戏事件（对弈者模式）", 
                    "触发一场袭击",
                    "raid/trader/wanderer/disease/resource/eclipse/toxic", true),
                
                new CommandInfo("ScheduleEvent", "预约事件", "事件控制",
                    "在未来某时刻触发事件", 
                    "10分钟后发动袭击",
                    "事件类型 + delayMinutes", true),
                
                new CommandInfo("ChangeWeather", "修改天气", "事件控制",
                    "改变当前地图的天气（对弈者模式）", 
                    "把天气改成晴天",
                    "Clear/Rain/Fog/Snow等", false),
                
                // === 🎭 事件调试（开发者工具）===
                new CommandInfo("TSS_TestWelcomeGift", "🎁 触发见面礼", "事件调试",
                    "【测试】触发见面礼事件（+500银 +10好感）",
                    "触发见面礼事件",
                    "无参数", true),
                
                new CommandInfo("TSS_TestDivineWrath", "⚡ 触发神罚", "事件调试",
                    "【测试】触发神罚事件（雷击 中毒 -20好感）",
                    "触发神罚事件",
                    "无参数", true),
                
                new CommandInfo("TSS_TestMechRaid", "🤖 触发敌袭", "事件调试",
                    "【测试】触发敌袭警报事件（5秒后袭击）",
                    "触发敌袭警报",
                    "无参数", true),
                
                new CommandInfo("TSS_ListAllEvents", "📋 列出所有事件", "事件调试",
                    "【测试】列出所有已加载的自定义事件",
                    "列出所有事件",
                    "无参数", true),
                
                new CommandInfo("TSS_CheckEventSystem", "🔍 检查事件系统", "事件调试",
                    "【测试】检查事件系统状态和完整性",
                    "检查事件系统",
                    "无参数", true),
                
                // === ⭐ 降临调试（v1.6.81）===
                new CommandInfo("TSS_DescentFriendly", "🌟 友好降临", "事件调试",
                    "【测试】触发叙事者友好降临（援助模式）",
                    "触发友好降临",
                    "无参数", true),
                
                new CommandInfo("TSS_DescentHostile", "💀 敌对降临", "事件调试",
                    "【测试】触发叙事者敌对降临（袭击模式）",
                    "触发敌对降临",
                    "无参数", true),
                
                new CommandInfo("TSS_DescentReturn", "🔙 叙事者回归", "事件调试",
                    "【测试】强制叙事者回归虚空",
                    "强制叙事者回归",
                    "无参数", true),
                
                new CommandInfo("TSS_CheckDescentSystem", "⚙️ 检查降临系统", "事件调试",
                    "【测试】检查降临系统状态和配置",
                    "检查降临系统",
                    "无参数", true),
                
                // === ⭐ v1.6.82: 降临动画类型测试 ===
                new CommandInfo("TSS_DescentDropPod", "📦 空投仓降临", "事件调试",
                    "【测试】使用空投仓动画触发降临（默认类型）",
                    "测试空投仓降临",
                    "无参数", true),
                
                new CommandInfo("TSS_DescentDragonFlyby", "🦅 实体飞掠降临", "事件调试",
                    "【测试】使用实体飞掠动画触发降临",
                    "测试实体飞掠降临",
                    "无参数", true),
                
                new CommandInfo("TSS_DescentPortal", "🌀 传送门降临", "事件调试",
                    "【测试】使用传送门（折跃）动画触发降临",
                    "测试传送门降临",
                    "无参数", true),
                
                new CommandInfo("TSS_DescentLightning", "⚡ 闪电降临", "事件调试",
                    "【测试】使用闪电动画触发降临",
                    "测试闪电降临",
                    "无参数", true),
                
                // === 查询（通过对话实现）===
                new CommandInfo("GetColonists", "获取殖民者", "查询",
                    "获取所有殖民者信息（通过对话）", 
                    "我们有哪些殖民者",
                    "直接询问AI即可", true),
                
                new CommandInfo("GetResources", "获取资源", "查询",
                    "获取殖民地资源库存（通过对话）", 
                    "我们还有多少食物",
                    "直接询问AI即可", true),
                
                new CommandInfo("GetThreats", "获取威胁", "查询",
                    "获取当前地图威胁信息（通过对话）", 
                    "有敌人吗",
                    "直接询问AI即可", true),
                
                new CommandInfo("GetColonyStatus", "殖民地状态", "查询",
                    "获取殖民地总体状态（通过对话）", 
                    "殖民地现在怎么样",
                    "直接询问AI即可", true),
                
                // === 暂不支持 ===
                new CommandInfo("DesignateConstruction", "指定建造", "资源管理",
                    "【暂不支持】需要添加的建造蓝图", 
                    "在(10,10)建一堵墙",
                    "需要坐标+建筑类型+朝向", false),
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
        /// 点击命令时的处理
        /// </summary>
        private void OnCommandClicked(CommandInfo command)
        {
            // ⭐ 检查是否为事件调试命令（包括降临调试）
            if (command.CommandName.StartsWith("TSS_Test") ||
                command.CommandName.StartsWith("TSS_List") ||
                command.CommandName.StartsWith("TSS_Check") ||
                command.CommandName.StartsWith("TSS_Descent"))
            {
                // 直接调用 EventTester 的方法
                HandleEventDebugCommand(command.CommandName);
                return;
            }
            
            // 复制示例提示词到剪贴板（可选）
            GUIUtility.systemCopyBuffer = command.ExamplePrompt;
            
            // 直接发送到 AI（不需要手动确认）
            TryInputAndSendToNarratorWindow(command.ExamplePrompt);
        }
        
        /// <summary>
        /// ⭐ 处理事件调试命令
        /// </summary>
        private void HandleEventDebugCommand(string commandName)
        {
            try
            {
                switch (commandName)
                {
                    // === 原有事件调试 ===
                    case "TSS_TestWelcomeGift":
                        Testing.EventTester.TriggerWelcomeGift();
                        break;
                    
                    case "TSS_TestDivineWrath":
                        Testing.EventTester.TriggerDivineWrath();
                        break;
                    
                    case "TSS_TestMechRaid":
                        Testing.EventTester.TriggerMechRaid();
                        break;
                    
                    case "TSS_ListAllEvents":
                        Testing.EventTester.ListAllEvents();
                        break;
                    
                    case "TSS_CheckEventSystem":
                        Testing.EventTester.CheckEventSystem();
                        break;
                    
                    // === ⭐ v1.6.81: 降临调试 ===
                    case "TSS_DescentFriendly":
                        Testing.EventTester.TriggerDescent(isHostile: false);
                        break;
                    
                    case "TSS_DescentHostile":
                        Testing.EventTester.TriggerDescent(isHostile: true);
                        break;
                    
                    case "TSS_DescentReturn":
                        Testing.EventTester.TriggerDescentReturn();
                        break;
                    
                    case "TSS_CheckDescentSystem":
                        Testing.EventTester.CheckDescentSystem();
                        break;
                    
                    // === ⭐ v1.6.82: 降临动画类型测试 ===
                    case "TSS_DescentDropPod":
                        Testing.EventTester.TriggerDescentWithAnimation("DropPod");
                        break;
                    
                    case "TSS_DescentDragonFlyby":
                        Testing.EventTester.TriggerDescentWithAnimation("DragonFlyby");
                        break;
                    
                    case "TSS_DescentPortal":
                        Testing.EventTester.TriggerDescentWithAnimation("Portal");
                        break;
                    
                    case "TSS_DescentLightning":
                        Testing.EventTester.TriggerDescentWithAnimation("Lightning");
                        break;
                    
                    default:
                        Messages.Message($"未知的事件调试命令: {commandName}", MessageTypeDefOf.RejectInput);
                        break;
                }
                
                // 关闭指令列表窗口
                this.Close();
            }
            catch (System.Exception ex)
            {
                Log.Error($"[CommandListWindow] 执行事件调试命令失败: {ex.Message}");
                Messages.Message($"执行失败: {ex.Message}", MessageTypeDefOf.RejectInput);
            }
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
