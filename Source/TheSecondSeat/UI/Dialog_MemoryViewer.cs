using UnityEngine;
using Verse;
using RimWorld;
using System;
using System.Linq;
using System.Text;
using TheSecondSeat.Comps;
using TheSecondSeat.Storyteller;
using TheSecondSeat.Narrator;
using TheSecondSeat.Core;

namespace TheSecondSeat.UI
{
    /// <summary>
    /// 记忆查看器窗口
    /// 查看叙事者的记忆状态 (KV Store, Events, Promises)
    /// </summary>
    public class Dialog_MemoryViewer : Window
    {
        private Vector2 scrollPosition = Vector2.zero;
        private CompNarratorMemory memory;
        
        public override Vector2 InitialSize => new Vector2(700f, 600f);

        public Dialog_MemoryViewer()
        {
            this.doCloseX = true;
            this.doCloseButton = true;
            this.forcePause = true;
            
            // 获取当前记忆组件
            // 使用 NarratorShadowManager 获取 ShadowPawn
            if (NarratorShadowManager.Instance?.ShadowPawn != null)
            {
                memory = NarratorShadowManager.Instance.ShadowPawn.GetComp<CompNarratorMemory>();
            }
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, 0f, inRect.width, 30f), "记忆查看器 (Memory Viewer)");
            Text.Font = GameFont.Small;

            if (memory == null)
            {
                GUI.color = Color.red;
                Widgets.Label(new Rect(0f, 40f, inRect.width, 30f), "未找到活动的记忆组件 (Shadow Pawn Not Found)");
                GUI.color = Color.white;
                return;
            }

            Rect contentRect = new Rect(0f, 45f, inRect.width, inRect.height - 55f);
            // 估算高度
            float viewHeight = 1000f; 
            Rect viewRect = new Rect(0f, 0f, contentRect.width - 16f, viewHeight);

            Widgets.BeginScrollView(contentRect, ref scrollPosition, viewRect);
            
            float y = 0f;
            
            // 1. KV Store
            Widgets.DrawBoxSolid(new Rect(0f, y, viewRect.width, 24f), new Color(0.2f, 0.2f, 0.2f, 0.5f));
            Widgets.Label(new Rect(5f, y, viewRect.width, 24f), "<b>键值存储 (Key-Value Store)</b>");
            y += 28f;
            
            var kv = memory.GetAllKV();
            if (kv.Count == 0)
            {
                GUI.color = Color.gray;
                Widgets.Label(new Rect(10f, y, viewRect.width, 24f), "(空 / Empty)");
                GUI.color = Color.white;
                y += 24f;
            }
            else
            {
                foreach (var pair in kv)
                {
                    Widgets.Label(new Rect(10f, y, 200f, 24f), pair.Key);
                    Widgets.Label(new Rect(220f, y, viewRect.width - 220f, 24f), pair.Value);
                    y += 24f;
                }
            }
            y += 10f;
            Widgets.DrawLineHorizontal(0f, y, viewRect.width);
            y += 10f;

            // 2. 承诺 (Promises)
            Widgets.DrawBoxSolid(new Rect(0f, y, viewRect.width, 24f), new Color(0.2f, 0.2f, 0.2f, 0.5f));
            Widgets.Label(new Rect(5f, y, viewRect.width, 24f), "<b>承诺 (Promises)</b>");
            y += 28f;
            
            var promises = memory.GetPendingPromises();
            
            if (promises.Count == 0)
            {
                GUI.color = Color.gray;
                Widgets.Label(new Rect(10f, y, viewRect.width, 24f), "(无待办承诺 / No Pending Promises)");
                GUI.color = Color.white;
                y += 24f;
            }
            else
            {
                foreach (var p in promises)
                {
                    string status = p.IsOverdue ? "[过期 / Overdue]" : "[进行中 / Active]";
                    Color color = p.IsOverdue ? Color.red : Color.yellow;
                    
                    GUI.color = color;
                    Widgets.Label(new Rect(10f, y, 80f, 24f), status);
                    GUI.color = Color.white;
                    
                    Widgets.Label(new Rect(100f, y, viewRect.width - 100f, 24f), $"{p.description} (Due: Day {p.dueDay})");
                    y += 24f;
                }
            }
            y += 10f;
            Widgets.DrawLineHorizontal(0f, y, viewRect.width);
            y += 10f;

            // 3. 事件记忆 (Recent Events)
            Widgets.DrawBoxSolid(new Rect(0f, y, viewRect.width, 24f), new Color(0.2f, 0.2f, 0.2f, 0.5f));
            Widgets.Label(new Rect(5f, y, viewRect.width, 24f), "<b>近期事件 (Recent Events)</b>");
            y += 28f;
            
            var events = memory.GetRecentEvents(20);
            if (events.Count == 0)
            {
                GUI.color = Color.gray;
                Widgets.Label(new Rect(10f, y, viewRect.width, 24f), "(无记录 / No Records)");
                GUI.color = Color.white;
                y += 24f;
            }
            else
            {
                foreach (var evt in events)
                {
                    GUI.color = Color.gray;
                    Widgets.Label(new Rect(10f, y, 60f, 24f), $"Day {evt.dayRecorded}");
                    GUI.color = new Color(0.8f, 0.8f, 1f);
                    Widgets.Label(new Rect(75f, y, 120f, 24f), evt.eventType);
                    GUI.color = Color.white;
                    Widgets.Label(new Rect(200f, y, viewRect.width - 200f, 24f), evt.description);
                    y += 24f;
                }
            }

            Widgets.EndScrollView();
        }
    }
}