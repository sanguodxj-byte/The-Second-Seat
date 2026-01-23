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
    /// 点击命令行自动输入到聊天窗口
    /// </summary>
    public class CommandListWindow : Window
    {
        private Vector2 scrollPosition = Vector2.zero;
        private string searchFilter = "";
        private string selectedCategory = "TSS_CmdCat_All".Translate();
        
        private static readonly Color HeaderColor = new Color(0.15f, 0.60f, 0.70f, 1f);
        private static readonly Color RowColor1 = new Color(0.12f, 0.13f, 0.14f, 0.8f);
        private static readonly Color RowColor2 = new Color(0.10f, 0.11f, 0.12f, 0.8f);
        private static readonly Color RowHoverColor = new Color(0.20f, 0.25f, 0.30f, 0.9f);
        private static readonly Color ImplementedColor = new Color(0.4f, 0.8f, 0.4f);
        private static readonly Color NotImplementedColor = new Color(0.8f, 0.4f, 0.4f);

        // 回调：输入到聊天窗口
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
            Widgets.Label(new Rect(0f, curY, inRect.width, 35f), "TSS_CmdList_Title".Translate());
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            curY += 40f;
            
            // 搜索和筛选
            DrawSearchAndFilter(new Rect(0f, curY, inRect.width, 30f));
            curY += 35f;
            
            // 提示
            GUI.color = new Color(0.6f, 0.8f, 0.6f);
            Text.Font = GameFont.Tiny;
            Widgets.Label(new Rect(0f, curY, inRect.width, 20f), "TSS_CmdList_Hint".Translate());
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
            
            if (selectedCategory != "TSS_CmdCat_All".Translate())
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
                Widgets.Label(new Rect(searchRect.x + 5f, searchRect.y, searchRect.width, searchRect.height), "TSS_CmdList_SearchPlaceholder".Translate());
                GUI.color = Color.white;
            }
            
            // 分类筛选
            var categories = new List<string> {
                "TSS_CmdCat_All".Translate(),
                "TSS_CmdCat_Batch".Translate(),
                "TSS_CmdCat_Unit".Translate(),
                "TSS_CmdCat_Resource".Translate(),
                "TSS_CmdCat_EventCtrl".Translate(),
                "TSS_CmdCat_EventDebug".Translate(),
                "TSS_CmdCat_Query".Translate()
            };
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
                new CommandInfo("BatchHarvest", "TSS_Cmd_BatchHarvest_Label".Translate(), "TSS_CmdCat_Batch".Translate(),
                    "TSS_Cmd_BatchHarvest_Desc".Translate(),
                    "TSS_Cmd_BatchHarvest_Ex".Translate(),
                    "TSS_Cmd_BatchHarvest_Fmt".Translate(), true),
                
                new CommandInfo("BatchMine", "TSS_Cmd_BatchMine_Label".Translate(), "TSS_CmdCat_Batch".Translate(),
                    "TSS_Cmd_BatchMine_Desc".Translate(),
                    "TSS_Cmd_BatchMine_Ex".Translate(),
                    "TSS_Cmd_BatchMine_Fmt".Translate(), true),
                
                new CommandInfo("BatchLogging", "TSS_Cmd_BatchLogging_Label".Translate(), "TSS_CmdCat_Batch".Translate(),
                    "TSS_Cmd_BatchLogging_Desc".Translate(),
                    "TSS_Cmd_BatchLogging_Ex".Translate(),
                    "TSS_Cmd_BatchLogging_Fmt".Translate(), true),
                
                new CommandInfo("BatchEquip", "TSS_Cmd_BatchEquip_Label".Translate(), "TSS_CmdCat_Batch".Translate(),
                    "TSS_Cmd_BatchEquip_Desc".Translate(),
                    "TSS_Cmd_BatchEquip_Ex".Translate(),
                    "TSS_Cmd_BatchEquip_Fmt".Translate(), true),
                
                new CommandInfo("BatchCapture", "TSS_Cmd_BatchCapture_Label".Translate(), "TSS_CmdCat_Batch".Translate(),
                    "TSS_Cmd_BatchCapture_Desc".Translate(),
                    "TSS_Cmd_BatchCapture_Ex".Translate(),
                    "TSS_Cmd_BatchCapture_Fmt".Translate(), true),
                
                new CommandInfo("PriorityRepair", "TSS_Cmd_PriorityRepair_Label".Translate(), "TSS_CmdCat_Batch".Translate(),
                    "TSS_Cmd_PriorityRepair_Desc".Translate(),
                    "TSS_Cmd_PriorityRepair_Ex".Translate(),
                    "TSS_Cmd_PriorityRepair_Fmt".Translate(), true),
                
                new CommandInfo("EmergencyRetreat", "TSS_Cmd_EmergencyRetreat_Label".Translate(), "TSS_CmdCat_Batch".Translate(),
                    "TSS_Cmd_EmergencyRetreat_Desc".Translate(),
                    "TSS_Cmd_EmergencyRetreat_Ex".Translate(),
                    "TSS_Cmd_EmergencyRetreat_Fmt".Translate(), true),
                
                new CommandInfo("DesignatePlantCut", "TSS_Cmd_DesignatePlantCut_Label".Translate(), "TSS_CmdCat_Batch".Translate(),
                    "TSS_Cmd_DesignatePlantCut_Desc".Translate(),
                    "TSS_Cmd_DesignatePlantCut_Ex".Translate(),
                    "TSS_Cmd_DesignatePlantCut_Fmt".Translate(), true),
                
                // === 殖民者管理（已实现）===
                new CommandInfo("DraftPawn", "TSS_Cmd_DraftPawn_Label".Translate(), "TSS_CmdCat_Unit".Translate(),
                    "TSS_Cmd_DraftPawn_Desc".Translate(),
                    "TSS_Cmd_DraftPawn_Ex".Translate(),
                    "TSS_Cmd_DraftPawn_Fmt".Translate(), true),
                
                new CommandInfo("MovePawn", "TSS_Cmd_MovePawn_Label".Translate(), "TSS_CmdCat_Unit".Translate(),
                    "TSS_Cmd_MovePawn_Desc".Translate(),
                    "TSS_Cmd_MovePawn_Ex".Translate(),
                    "TSS_Cmd_MovePawn_Fmt".Translate(), true),
                
                new CommandInfo("HealPawn", "TSS_Cmd_HealPawn_Label".Translate(), "TSS_CmdCat_Unit".Translate(),
                    "TSS_Cmd_HealPawn_Desc".Translate(),
                    "TSS_Cmd_HealPawn_Ex".Translate(),
                    "TSS_Cmd_HealPawn_Fmt".Translate(), true),
                
                new CommandInfo("SetWorkPriority", "TSS_Cmd_SetWorkPriority_Label".Translate(), "TSS_CmdCat_Unit".Translate(),
                    "TSS_Cmd_SetWorkPriority_Desc".Translate(),
                    "TSS_Cmd_SetWorkPriority_Ex".Translate(),
                    "TSS_Cmd_SetWorkPriority_Fmt".Translate(), true),
                
                new CommandInfo("EquipWeapon", "TSS_Cmd_EquipWeapon_Label".Translate(), "TSS_CmdCat_Unit".Translate(),
                    "TSS_Cmd_EquipWeapon_Desc".Translate(),
                    "TSS_Cmd_EquipWeapon_Ex".Translate(),
                    "TSS_Cmd_EquipWeapon_Fmt".Translate(), true),
                
                // === 资源管理（已实现）===
                new CommandInfo("ForbidItems", "TSS_Cmd_ForbidItems_Label".Translate(), "TSS_CmdCat_Resource".Translate(),
                    "TSS_Cmd_ForbidItems_Desc".Translate(),
                    "TSS_Cmd_ForbidItems_Ex".Translate(),
                    "TSS_Cmd_ForbidItems_Fmt".Translate(), true),
                
                new CommandInfo("AllowItems", "TSS_Cmd_AllowItems_Label".Translate(), "TSS_CmdCat_Resource".Translate(),
                    "TSS_Cmd_AllowItems_Desc".Translate(),
                    "TSS_Cmd_AllowItems_Ex".Translate(),
                    "TSS_Cmd_AllowItems_Fmt".Translate(), true),
                
                // === 事件控制（对弈者模式）===
                new CommandInfo("TriggerEvent", "TSS_Cmd_TriggerEvent_Label".Translate(), "TSS_CmdCat_EventCtrl".Translate(),
                    "TSS_Cmd_TriggerEvent_Desc".Translate(),
                    "TSS_Cmd_TriggerEvent_Ex".Translate(),
                    "TSS_Cmd_TriggerEvent_Fmt".Translate(), true),
                
                new CommandInfo("ScheduleEvent", "TSS_Cmd_ScheduleEvent_Label".Translate(), "TSS_CmdCat_EventCtrl".Translate(),
                    "TSS_Cmd_ScheduleEvent_Desc".Translate(),
                    "TSS_Cmd_ScheduleEvent_Ex".Translate(),
                    "TSS_Cmd_ScheduleEvent_Fmt".Translate(), true),
                
                new CommandInfo("ChangeWeather", "TSS_Cmd_ChangeWeather_Label".Translate(), "TSS_CmdCat_EventCtrl".Translate(),
                    "TSS_Cmd_ChangeWeather_Desc".Translate(),
                    "TSS_Cmd_ChangeWeather_Ex".Translate(),
                    "TSS_Cmd_ChangeWeather_Fmt".Translate(), false),
                
                // === 🎭 事件调试（开发者工具）===
                new CommandInfo("TSS_TestWelcomeGift", "TSS_Cmd_TestWelcomeGift_Label".Translate(), "TSS_CmdCat_EventDebug".Translate(),
                    "TSS_Cmd_TestWelcomeGift_Desc".Translate(),
                    "TSS_Cmd_TestWelcomeGift_Ex".Translate(),
                    "TSS_Cmd_Param_None".Translate(), true),
                
                new CommandInfo("TSS_TestDivineWrath", "TSS_Cmd_TestDivineWrath_Label".Translate(), "TSS_CmdCat_EventDebug".Translate(),
                    "TSS_Cmd_TestDivineWrath_Desc".Translate(),
                    "TSS_Cmd_TestDivineWrath_Ex".Translate(),
                    "TSS_Cmd_Param_None".Translate(), true),
                
                new CommandInfo("TSS_TestMechRaid", "TSS_Cmd_TestMechRaid_Label".Translate(), "TSS_CmdCat_EventDebug".Translate(),
                    "TSS_Cmd_TestMechRaid_Desc".Translate(),
                    "TSS_Cmd_TestMechRaid_Ex".Translate(),
                    "TSS_Cmd_Param_None".Translate(), true),
                
                new CommandInfo("TSS_ListAllEvents", "TSS_Cmd_ListAllEvents_Label".Translate(), "TSS_CmdCat_EventDebug".Translate(),
                    "TSS_Cmd_ListAllEvents_Desc".Translate(),
                    "TSS_Cmd_ListAllEvents_Ex".Translate(),
                    "TSS_Cmd_Param_None".Translate(), true),
                
                new CommandInfo("TSS_CheckEventSystem", "TSS_Cmd_CheckEventSystem_Label".Translate(), "TSS_CmdCat_EventDebug".Translate(),
                    "TSS_Cmd_CheckEventSystem_Desc".Translate(),
                    "TSS_Cmd_CheckEventSystem_Ex".Translate(),
                    "TSS_Cmd_Param_None".Translate(), true),
                
                // === 降临调试（v1.6.81）===
                new CommandInfo("TSS_DescentFriendly", "TSS_Cmd_DescentFriendly_Label".Translate(), "TSS_CmdCat_EventDebug".Translate(),
                    "TSS_Cmd_DescentFriendly_Desc".Translate(),
                    "TSS_Cmd_DescentFriendly_Ex".Translate(),
                    "TSS_Cmd_Param_None".Translate(), true),
                
                new CommandInfo("TSS_DescentHostile", "TSS_Cmd_DescentHostile_Label".Translate(), "TSS_CmdCat_EventDebug".Translate(),
                    "TSS_Cmd_DescentHostile_Desc".Translate(),
                    "TSS_Cmd_DescentHostile_Ex".Translate(),
                    "TSS_Cmd_Param_None".Translate(), true),
                
                new CommandInfo("TSS_DescentReturn", "TSS_Cmd_DescentReturn_Label".Translate(), "TSS_CmdCat_EventDebug".Translate(),
                    "TSS_Cmd_DescentReturn_Desc".Translate(),
                    "TSS_Cmd_DescentReturn_Ex".Translate(),
                    "TSS_Cmd_Param_None".Translate(), true),
                
                new CommandInfo("TSS_CheckDescentSystem", "TSS_Cmd_CheckDescentSystem_Label".Translate(), "TSS_CmdCat_EventDebug".Translate(),
                    "TSS_Cmd_CheckDescentSystem_Desc".Translate(),
                    "TSS_Cmd_CheckDescentSystem_Ex".Translate(),
                    "TSS_Cmd_Param_None".Translate(), true),
                
                // === v1.6.82: 降临动画类型测试 ===
                new CommandInfo("TSS_DescentDropPod", "TSS_Cmd_DescentDropPod_Label".Translate(), "TSS_CmdCat_EventDebug".Translate(),
                    "TSS_Cmd_DescentDropPod_Desc".Translate(),
                    "TSS_Cmd_DescentDropPod_Ex".Translate(),
                    "TSS_Cmd_Param_None".Translate(), true),
                
                new CommandInfo("TSS_DescentDragonFlyby", "TSS_Cmd_DescentDragonFlyby_Label".Translate(), "TSS_CmdCat_EventDebug".Translate(),
                    "TSS_Cmd_DescentDragonFlyby_Desc".Translate(),
                    "TSS_Cmd_DescentDragonFlyby_Ex".Translate(),
                    "TSS_Cmd_Param_None".Translate(), true),
                
                new CommandInfo("TSS_DescentPortal", "TSS_Cmd_DescentPortal_Label".Translate(), "TSS_CmdCat_EventDebug".Translate(),
                    "TSS_Cmd_DescentPortal_Desc".Translate(),
                    "TSS_Cmd_DescentPortal_Ex".Translate(),
                    "TSS_Cmd_Param_None".Translate(), true),
                
                new CommandInfo("TSS_DescentLightning", "TSS_Cmd_DescentLightning_Label".Translate(), "TSS_CmdCat_EventDebug".Translate(),
                    "TSS_Cmd_DescentLightning_Desc".Translate(),
                    "TSS_Cmd_DescentLightning_Ex".Translate(),
                    "TSS_Cmd_Param_None".Translate(), true),
                
                // === 查询（通过对话实现）===
                new CommandInfo("GetColonists", "TSS_Cmd_GetColonists_Label".Translate(), "TSS_CmdCat_Query".Translate(),
                    "TSS_Cmd_GetColonists_Desc".Translate(),
                    "TSS_Cmd_GetColonists_Ex".Translate(),
                    "TSS_Cmd_GetColonists_Fmt".Translate(), true),
                
                new CommandInfo("GetResources", "TSS_Cmd_GetResources_Label".Translate(), "TSS_CmdCat_Query".Translate(),
                    "TSS_Cmd_GetResources_Desc".Translate(),
                    "TSS_Cmd_GetResources_Ex".Translate(),
                    "TSS_Cmd_GetResources_Fmt".Translate(), true),
                
                new CommandInfo("GetThreats", "TSS_Cmd_GetThreats_Label".Translate(), "TSS_CmdCat_Query".Translate(),
                    "TSS_Cmd_GetThreats_Desc".Translate(),
                    "TSS_Cmd_GetThreats_Ex".Translate(),
                    "TSS_Cmd_GetThreats_Fmt".Translate(), true),
                
                new CommandInfo("GetColonyStatus", "TSS_Cmd_GetColonyStatus_Label".Translate(), "TSS_CmdCat_Query".Translate(),
                    "TSS_Cmd_GetColonyStatus_Desc".Translate(),
                    "TSS_Cmd_GetColonyStatus_Ex".Translate(),
                    "TSS_Cmd_GetColonyStatus_Fmt".Translate(), true),
                
                // === 暂不支持 ===
                new CommandInfo("DesignateConstruction", "TSS_Cmd_DesignateConstruction_Label".Translate(), "TSS_CmdCat_Resource".Translate(),
                    "TSS_Cmd_DesignateConstruction_Desc".Translate(),
                    "TSS_Cmd_DesignateConstruction_Ex".Translate(),
                    "TSS_Cmd_DesignateConstruction_Fmt".Translate(), false),
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
                
                // 鼠标悬停效果
                if (Mouse.IsOver(rowRect))
                {
                    rowColor = RowHoverColor;
                }
                
                DrawCommandRow(rowRect, commands[i], rowColor, i);
                
                // 点击事件：输入到聊天窗口
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
            string[] headers = {
                "TSS_CmdList_Header_ID".Translate(),
                "TSS_CmdList_Header_Name".Translate(),
                "TSS_CmdList_Header_Cat".Translate(),
                "TSS_CmdList_Header_Desc".Translate(),
                "TSS_CmdList_Header_Example".Translate(),
                "TSS_CmdList_Header_Format".Translate()
            };
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
            // 检查是否为事件调试命令（包括降临调试）
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
        /// 处理事件调试命令
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
                    
                    // === v1.6.81: 降临调试 ===
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
                    
                    // === v1.6.82: 降临动画类型测试 ===
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
                        Messages.Message("TSS_CmdList_UnknownCommand".Translate(commandName), MessageTypeDefOf.RejectInput);
                        break;
                }
                
                // 关闭指令列表窗口
                this.Close();
            }
            catch (System.Exception ex)
            {
                Log.Error($"[CommandListWindow] Event debug command failed: {ex.Message}");
                Messages.Message("TSS_CmdList_ExecuteFailed".Translate(ex.Message), MessageTypeDefOf.RejectInput);
            }
        }

        /// <summary>
        /// 输入文本并自动发送到叙事者窗口
        /// </summary>
        private void TryInputAndSendToNarratorWindow(string text)
        {
            // 查找已打开的 NarratorWindow
            var narratorWindow = Find.WindowStack?.Windows
                .OfType<NarratorWindow>()
                .FirstOrDefault();
            
            if (narratorWindow != null)
            {
                // 直接发送（不需要手动点击发送按钮）
                NarratorWindow.SetInputTextAndSend(text);
                Log.Message($"[CommandListWindow] 已自动发送命令: {text}");
                
                // 显示确认消息
                Messages.Message($"已发送: {text}", MessageTypeDefOf.PositiveEvent);

                // 关闭命令列表窗口（可选）
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
                    Messages.Message($"已发送: {text}", MessageTypeDefOf.PositiveEvent);
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
            public string ExamplePrompt;  // 示例提示词
            public string TargetFormat;
            public bool IsImplemented;    // 是否已实现

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
            defaultLabel = "TSS_CmdList_OpenButton".Translate();
            defaultDesc = "TSS_CmdList_OpenButton_Desc".Translate();
            icon = ContentFinder<Texture2D>.Get("UI/Commands/InfoButton", false);
        }

        public override void ProcessInput(Event ev)
        {
            base.ProcessInput(ev);
            Find.WindowStack.Add(new CommandListWindow());
        }
    }
}
