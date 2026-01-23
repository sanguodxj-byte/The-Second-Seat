using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using RimWorld;
using TheSecondSeat.LLM;

namespace TheSecondSeat.UI
{
    /// <summary>
    /// LLM 调试与 Token 监控器
    /// 用于查看历史 API 请求记录、Token 消耗及原始 JSON 数据
    /// </summary>
    public class Dialog_LLMDebugger : Window
    {
        private Vector2 scrollPositionLeft;
        private Vector2 scrollPositionRight;
        private RequestLog selectedLog;

        public override Vector2 InitialSize => new Vector2(950f, 700f);

        public Dialog_LLMDebugger()
        {
            this.doCloseButton = true;
            this.doCloseX = true;
            this.closeOnCancel = false;
            this.draggable = true;
            this.resizeable = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            // Title
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0, 0, inRect.width, 35f), "LLM Request History & Token Monitor");
            Text.Font = GameFont.Small;

            float topMargin = 45f;
            float leftWidth = 350f;
            float gap = 10f;
            float rightWidth = inRect.width - leftWidth - gap;
            float bottomHeight = 40f; 
            float mainHeight = inRect.height - topMargin - bottomHeight;

            Rect leftRect = new Rect(0, topMargin, leftWidth, mainHeight);
            Rect rightRect = new Rect(leftWidth + gap, topMargin, rightWidth, mainHeight);
            Rect bottomRect = new Rect(0, inRect.height - bottomHeight, inRect.width, bottomHeight);

            // ============ Left Panel: History List ============
            Widgets.DrawMenuSection(leftRect);
            var logs = LLMRequestHistory.Logs; // 获取最新的日志列表
            // 倒序显示（最新的在最上面）
            var displayLogs = logs.AsEnumerable().Reverse().ToList();

            float rowHeight = 40f;
            Rect viewRectLeft = new Rect(0, 0, leftWidth - 16f, displayLogs.Count * rowHeight);
            
            Widgets.BeginScrollView(leftRect, ref scrollPositionLeft, viewRectLeft);
            
            // UI Virtualization
            float visibleTop = scrollPositionLeft.y;
            float visibleBottom = scrollPositionLeft.y + leftRect.height;
            int startIndex = Mathf.FloorToInt(visibleTop / rowHeight);
            int endIndex = Mathf.CeilToInt(visibleBottom / rowHeight);
            
            startIndex = Mathf.Clamp(startIndex, 0, displayLogs.Count);
            endIndex = Mathf.Clamp(endIndex, 0, displayLogs.Count);

            for (int i = startIndex; i < endIndex; i++)
            {
                var log = displayLogs[i];
                float y = i * rowHeight;
                Rect rowRect = new Rect(0, y, viewRectLeft.width, rowHeight);
                
                // Highlight selected
                if (selectedLog == log)
                {
                    Widgets.DrawHighlightSelected(rowRect);
                }
                else if (Mouse.IsOver(rowRect))
                {
                    Widgets.DrawHighlight(rowRect);
                }

                // Row Content
                GUI.BeginGroup(rowRect);
                
                // Status Color
                GUI.color = log.Success ? Color.green : Color.red;
                Widgets.Label(new Rect(5, 5, 20, 20), log.Success ? "✔" : "✘");
                GUI.color = Color.white;

                // v2.7.0: 显示请求类型和模型名称
                Widgets.Label(new Rect(30, 2, 180, 20), log.DisplayLabel);
                
                // Timestamp (右上角)
                Text.Anchor = TextAnchor.MiddleRight;
                GUI.color = new Color(0.7f, 0.7f, 0.7f);
                Widgets.Label(new Rect(rowRect.width - 70, 2, 60, 20), log.Timestamp.ToString("HH:mm:ss"));
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;

                // Duration / Token Count / Endpoint (Small text)
                Text.Font = GameFont.Tiny;
                GUI.color = Color.gray;
                string tokenInfo = log.TotalTokens > 0 ? $"{log.TotalTokens}T" : "-";
                Widgets.Label(new Rect(30, 20, rowRect.width - 40, 15), $"{log.DurationSeconds:F1}s | {tokenInfo} | {GetShortEndpoint(log.Endpoint)}");
                GUI.color = Color.white;
                Text.Font = GameFont.Small;

                if (Widgets.ButtonInvisible(new Rect(0, 0, rowRect.width, rowHeight)))
                {
                    if (selectedLog != log)
                    {
                        selectedLog = log;
                        scrollPositionRight = Vector2.zero;
                    }
                }

                GUI.EndGroup();
                
                Widgets.DrawLineHorizontal(0, rowHeight - 1, rowRect.width, new Color(1f,1f,1f,0.1f));
            }
            
            Widgets.EndScrollView();

            // ============ Right Panel: Details ============
            Widgets.DrawMenuSection(rightRect);
            
            if (selectedLog != null)
            {
                Rect detailsInner = rightRect.ContractedBy(10f);
                float contentWidth = detailsInner.width - 16f;
                
                // Calculate content height dynamically
                // Ensure correct font for calculation
                Text.Font = GameFont.Small;
                string reqJson = selectedLog.RequestJson ?? "";
                string respJson = selectedLog.ResponseJson ?? "";
                float reqHeight = Text.CalcHeight(reqJson, contentWidth) + 40f; // Add extra padding
                float respHeight = Text.CalcHeight(respJson, contentWidth) + 40f;
                
                // Header (30) + Status (25) + Error (25 if exists) + Tokens (35) +
                // Req Label (25) + Req Content + Gap (20) +
                // Resp Label (25) + Resp Content + Padding (50)
                float contentHeight = 30f + 25f + 35f + 25f + reqHeight + 20f + 25f + respHeight + 50f;
                if (!string.IsNullOrEmpty(selectedLog.ErrorMessage)) contentHeight += 25f;

                Rect viewRectRight = new Rect(0, 0, contentWidth, contentHeight);
                
                Widgets.BeginScrollView(detailsInner, ref scrollPositionRight, viewRectRight);
                
                float cy = 0f;
                
                // Summary Header
                Widgets.Label(new Rect(0, cy, viewRectRight.width, 30f), $"<b>Request Details</b> ({selectedLog.Timestamp})");
                cy += 30f;
                
                Widgets.Label(new Rect(0, cy, viewRectRight.width, 25f), $"Status: {(selectedLog.Success ? "Success" : "Failed")}");
                cy += 25f;
                
                if (!string.IsNullOrEmpty(selectedLog.ErrorMessage))
                {
                    GUI.color = Color.red;
                    Widgets.Label(new Rect(0, cy, viewRectRight.width, 25f), $"Error: {selectedLog.ErrorMessage}");
                    GUI.color = Color.white;
                    cy += 25f;
                }

                Widgets.Label(new Rect(0, cy, viewRectRight.width, 25f), $"Tokens: Input {selectedLog.PromptTokens} + Output {selectedLog.CompletionTokens} = Total {selectedLog.TotalTokens}");
                cy += 35f;

                // Request Body
                Widgets.Label(new Rect(0, cy, viewRectRight.width, 25f), "<b>Request Body (JSON):</b>");
                cy += 25f;
                // Height already calculated above
                Widgets.TextArea(new Rect(0, cy, viewRectRight.width, reqHeight), reqJson, true);
                cy += reqHeight + 20f;

                // Response Body
                Widgets.Label(new Rect(0, cy, viewRectRight.width, 25f), "<b>Response Body (JSON):</b>");
                cy += 25f;
                // Height already calculated above
                Widgets.TextArea(new Rect(0, cy, viewRectRight.width, respHeight), respJson, true);
                
                Widgets.EndScrollView();
            }
            else
            {
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(rightRect, "Select a log entry to view details.");
                Text.Anchor = TextAnchor.UpperLeft;
            }

            // ============ Bottom Actions ============
            if (Widgets.ButtonText(new Rect(bottomRect.x, bottomRect.y, 120f, 30f), "Clear History"))
            {
                LLMRequestHistory.Clear();
                selectedLog = null;
            }
        }

        private string GetShortEndpoint(string url)
        {
            if (string.IsNullOrEmpty(url)) return "-";
            try
            {
                return new Uri(url).Host;
            }
            catch
            {
                return url;
            }
        }
    }
}
