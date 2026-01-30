using UnityEngine;
using Verse;
using RimWorld;
using System;
using System.Linq;
using TheSecondSeat.Commands;

namespace TheSecondSeat.UI
{
    /// <summary>
    /// 指令查看器窗口
    /// 查看已注册的所有 AI 指令
    /// </summary>
    public class Dialog_CommandViewer : Window
    {
        private Vector2 scrollPosition = Vector2.zero;
        private string searchFilter = "";
        
        public override Vector2 InitialSize => new Vector2(800f, 600f);

        public Dialog_CommandViewer()
        {
            this.doCloseX = true;
            this.doCloseButton = true;
            this.forcePause = true;
            this.absorbInputAroundWindow = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            // 标题
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, 0f, inRect.width, 30f), "已注册指令 (Command Registry)");
            Text.Font = GameFont.Small;

            // 搜索框
            string filterLabel = "搜索指令 (Search):";
            float labelWidth = Text.CalcSize(filterLabel).x + 10f;
            Widgets.Label(new Rect(0f, 40f, labelWidth, 30f), filterLabel);
            searchFilter = Widgets.TextField(new Rect(labelWidth, 40f, 300f, 30f), searchFilter);

            // 列表内容
            Rect listRect = new Rect(0f, 80f, inRect.width, inRect.height - 90f);
            Rect viewRect = new Rect(0f, 0f, listRect.width - 16f, 1000f); // 初始高度，稍后计算

            // 获取过滤后的指令
            var commands = CommandRegistry.GetAllCommands()
                .Where(c => string.IsNullOrEmpty(searchFilter) || 
                           c.ActionName.IndexOf(searchFilter, StringComparison.OrdinalIgnoreCase) >= 0)
                .OrderBy(c => c.ActionName)
                .ToList();

            float totalHeight = commands.Count * 60f;
            if (totalHeight < listRect.height) totalHeight = listRect.height;
            viewRect.height = totalHeight;

            Widgets.BeginScrollView(listRect, ref scrollPosition, viewRect);

            float y = 0f;
            float rowHeight = 50f;

            for (int i = 0; i < commands.Count; i++)
            {
                var cmd = commands[i];
                Rect rowRect = new Rect(0f, y, viewRect.width, rowHeight);
                
                // 背景交替
                if (i % 2 == 0)
                {
                    Widgets.DrawHighlight(rowRect);
                }

                // 指令名称
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(new Rect(10f, y + 2f, 220f, 24f), cmd.ActionName);
                
                // 类型标记
                string typeLabel = cmd.GetType().Name;
                if (cmd is Command_GenericDefWrapper) typeLabel = "XML Command";
                
                Text.Font = GameFont.Tiny;
                GUI.color = new Color(0.6f, 0.6f, 0.6f);
                Widgets.Label(new Rect(10f, y + 26f, 220f, 20f), typeLabel);
                GUI.color = Color.white;

                // 描述
                Text.Font = GameFont.Small;
                Rect descRect = new Rect(240f, y, viewRect.width - 360f, rowHeight);
                Widgets.Label(descRect, cmd.GetDescription());

                // 测试按钮
                Rect btnRect = new Rect(viewRect.width - 110f, y + 10f, 100f, 30f);
                if (Widgets.ButtonText(btnRect, "测试执行"))
                {
                    // 尝试执行（无参数）
                    // 这是一个危险操作，仅在 DevMode 下允许，或者弹出确认
                    if (Prefs.DevMode)
                    {
                        bool result = cmd.Execute(null, null);
                        string msg = result ? "执行成功" : "执行失败 (可能需要参数)";
                        Messages.Message(msg, result ? MessageTypeDefOf.PositiveEvent : MessageTypeDefOf.RejectInput);
                    }
                    else
                    {
                        Messages.Message("仅开发者模式允许直接测试指令", MessageTypeDefOf.RejectInput);
                    }
                }

                y += rowHeight;
            }

            Widgets.EndScrollView();
            Text.Anchor = TextAnchor.UpperLeft;
        }
    }
}