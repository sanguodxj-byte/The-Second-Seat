using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Verse;
using RimWorld;
using TheSecondSeat.Narrator;
using TheSecondSeat.PersonaGeneration;
using TheSecondSeat.PersonaGeneration.Scriban;
using TheSecondSeat.Storyteller;
using TheSecondSeat.LLM;  // ⭐ v2.9.5: 添加 LLM 命名空间

namespace TheSecondSeat.RimAgent.UI
{
    public class RimAgentDebugWindow : Window
    {
        private RimAgent selectedAgent;
        private Vector2 agentListScrollPos;
        private Vector2 debugInfoScrollPos;
        private Vector2 promptPreviewScrollPos;
        private Vector2 contextDataScrollPos;
        private Vector2 llmHistoryScrollPos;  // ⭐ v2.9.5: LLM 请求历史滚动
        private Vector2 llmDetailScrollPos;   // ⭐ v2.9.5: LLM 请求详情滚动
        
        // Tab system - ⭐ v2.9.5: 新增 LLMHistory Tab
        private enum DebugTab { AgentInfo, SystemPrompt, ContextData, LLMHistory }
        private DebugTab currentTab = DebugTab.LLMHistory;  // ⭐ 默认显示 LLM 历史
        
        // ⭐ v2.9.5: 选中的请求日志
        private RequestLog selectedRequestLog = null;
        
        // Cached prompt for preview
        private string cachedMasterPrompt = "";
        private string cachedEventDirectorPrompt = "";
        private bool promptNeedsRefresh = true;
        
        // Cached data for display
        private NarratorManager cachedManager = null;
        private StorytellerAgent cachedStorytellerAgent = null;
        
        public override Vector2 InitialSize => new Vector2(1100f, 750f);
        
        public RimAgentDebugWindow()
        {
            this.doCloseX = true;
            this.forcePause = true;
            this.absorbInputAroundWindow = false;
            this.resizeable = true;
            this.draggable = true;
        }
        
        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0, 0, 300, 30), "TSS_Debug_RimAgent_Title".Translate());
            Text.Font = GameFont.Small;
            
            float contentY = 40f;
            float leftWidth = 250f;
            float rightX = leftWidth + 10f;
            float rightWidth = inRect.width - rightX;
            float contentHeight = inRect.height - contentY;
            
            // Left: Agent List
            Rect leftRect = new Rect(0, contentY, leftWidth, contentHeight);
            DrawAgentList(leftRect);
            
            // Right: Tabbed Content
            Rect rightRect = new Rect(rightX, contentY, rightWidth, contentHeight);
            DrawTabbedContent(rightRect);
        }
        
        private void DrawAgentList(Rect rect)
        {
            Widgets.DrawMenuSection(rect);
            
            var agents = RimAgent.ActiveAgents;
            
            // 刷新按钮放在顶部
            if (Widgets.ButtonText(new Rect(rect.x + 10, rect.y + 5, rect.width - 20, 25), "TSS_Debug_Refresh".Translate()))
            {
                promptNeedsRefresh = true;
            }
            
            float listY = rect.y + 35;
            float listHeight = rect.height - 40;
            
            if (agents == null || agents.Count == 0)
            {
                Widgets.Label(new Rect(rect.x + 10, listY, rect.width - 20, 30), "TSS_Debug_NoAgents".Translate());
                return;
            }

            Rect listRect = new Rect(rect.x, listY, rect.width, listHeight);
            Rect viewRect = new Rect(0, 0, rect.width - 16, agents.Count * 30f);
            Widgets.BeginScrollView(listRect, ref agentListScrollPos, viewRect);
            
            float y = 0;
            foreach (var agent in agents)
            {
                Rect rowRect = new Rect(0, y, viewRect.width, 30f);
                if (agent == selectedAgent)
                {
                    Widgets.DrawHighlightSelected(rowRect);
                }
                
                string label = $"{agent.AgentId} ({agent.State})";
                if (Widgets.ButtonText(rowRect, label, false, true, true))
                {
                    selectedAgent = agent;
                    promptNeedsRefresh = true;
                }
                
                y += 30f;
            }
            
            Widgets.EndScrollView();
        }
        
        private void DrawTabbedContent(Rect rect)
        {
            Widgets.DrawMenuSection(rect);
            
            // Tab bar - ⭐ v2.9.5: 新增 LLM History Tab
            float tabWidth = 100f;
            float tabY = rect.y + 5f;
            float tabX = rect.x + 10f;
            
            var tabs = new[] { DebugTab.LLMHistory, DebugTab.AgentInfo, DebugTab.SystemPrompt, DebugTab.ContextData };
            var tabNames = new[] { "📡 LLM History", "TSS_Tab_AgentInfo".Translate().ToString(), "TSS_Tab_SystemPrompt".Translate().ToString(), "TSS_Tab_ContextData".Translate().ToString() };
            
            for (int i = 0; i < tabs.Length; i++)
            {
                Rect tabRect = new Rect(tabX + i * (tabWidth + 5), tabY, tabWidth, 25f);
                bool isSelected = currentTab == tabs[i];
                
                if (isSelected)
                {
                    Widgets.DrawBoxSolid(tabRect, new Color(0.3f, 0.5f, 0.7f, 0.5f));
                }
                
                if (Widgets.ButtonText(tabRect, tabNames[i], true, true, !isSelected))
                {
                    currentTab = tabs[i];
                    if (tabs[i] == DebugTab.SystemPrompt || tabs[i] == DebugTab.ContextData)
                    {
                        RefreshPromptCache();
                    }
                }
            }
            
            // Content area
            float contentY = tabY + 35f;
            Rect contentRect = new Rect(rect.x + 5, contentY, rect.width - 10, rect.height - 45);
            
            switch (currentTab)
            {
                case DebugTab.LLMHistory:
                    DrawLLMHistoryTab(contentRect);
                    break;
                case DebugTab.AgentInfo:
                    DrawAgentInfoTab(contentRect);
                    break;
                case DebugTab.SystemPrompt:
                    DrawSystemPromptTab(contentRect);
                    break;
                case DebugTab.ContextData:
                    DrawContextDataTab(contentRect);
                    break;
            }
        }
        
        /// <summary>
        /// ⭐ v2.9.5: 新增 LLM 请求历史 Tab
        /// 显示叙事者和其他 LLM 请求的记录
        /// </summary>
        private void DrawLLMHistoryTab(Rect rect)
        {
            float x = 5f;
            float y = 5f;
            float listWidth = 250f;
            float detailWidth = rect.width - listWidth - 20f;
            
            // 获取请求历史
            var logs = LLMRequestHistory.Logs;
            
            // 左侧：请求列表
            Rect listRect = new Rect(rect.x + x, rect.y + y, listWidth, rect.height - 10);
            DrawLLMRequestList(listRect, logs);
            
            // 右侧：请求详情
            Rect detailRect = new Rect(rect.x + x + listWidth + 10, rect.y + y, detailWidth, rect.height - 10);
            DrawLLMRequestDetail(detailRect);
        }
        
        private void DrawLLMRequestList(Rect rect, List<RequestLog> logs)
        {
            Widgets.DrawBoxSolid(rect, new Color(0.08f, 0.08f, 0.1f, 0.9f));
            
            // 标题和清除按钮
            float y = rect.y + 5;
            Widgets.Label(new Rect(rect.x + 5, y, 150, 20), $"<b>请求记录 ({logs.Count}/20)</b>");
            
            if (Widgets.ButtonText(new Rect(rect.x + rect.width - 55, y, 50, 20), "清除"))
            {
                LLMRequestHistory.Clear();
                selectedRequestLog = null;
            }
            y += 25;
            
            if (logs.Count == 0)
            {
                GUI.color = new Color(0.6f, 0.6f, 0.6f);
                Widgets.Label(new Rect(rect.x + 10, y, rect.width - 20, 30), "(暂无请求记录)");
                GUI.color = Color.white;
                return;
            }
            
            // 滚动列表（从新到旧）
            Rect scrollRect = new Rect(rect.x, y, rect.width, rect.height - 35);
            Rect viewRect = new Rect(0, 0, rect.width - 16, logs.Count * 50f);
            
            Widgets.BeginScrollView(scrollRect, ref llmHistoryScrollPos, viewRect);
            
            float itemY = 0;
            // 倒序显示（最新的在上面）
            for (int i = logs.Count - 1; i >= 0; i--)
            {
                var log = logs[i];
                Rect rowRect = new Rect(0, itemY, viewRect.width, 48f);
                
                // 背景
                Color bgColor = (log == selectedRequestLog) 
                    ? new Color(0.3f, 0.5f, 0.7f, 0.5f) 
                    : (i % 2 == 0 ? new Color(0.12f, 0.12f, 0.15f, 0.8f) : new Color(0.1f, 0.1f, 0.12f, 0.8f));
                Widgets.DrawBoxSolid(rowRect, bgColor);
                
                // 状态指示条
                Color statusColor = log.Success ? new Color(0.4f, 0.8f, 0.4f) : new Color(0.9f, 0.3f, 0.3f);
                Widgets.DrawBoxSolid(new Rect(rowRect.x, rowRect.y, 4, rowRect.height), statusColor);
                
                // 内容
                float textX = rowRect.x + 8;
                
                // 第一行：时间 + 类型 + 模型
                GUI.color = new Color(0.9f, 0.9f, 0.7f);
                Widgets.Label(new Rect(textX, rowRect.y + 2, rowRect.width - 15, 20), 
                    $"[{log.Timestamp:HH:mm:ss}] {log.DisplayLabel}");
                
                // 第二行：Token 和耗时
                GUI.color = new Color(0.7f, 0.8f, 0.9f);
                string tokenInfo = log.TotalTokens > 0 
                    ? $"📊 {log.TotalTokens} tokens (↑{log.PromptTokens}/↓{log.CompletionTokens})" 
                    : "📊 N/A";
                string duration = log.DurationSeconds > 0 ? $"⏱️ {log.DurationSeconds:F1}s" : "";
                Widgets.Label(new Rect(textX, rowRect.y + 22, rowRect.width - 15, 20), 
                    $"{tokenInfo} {duration}");
                
                GUI.color = Color.white;
                
                // 点击选择
                if (Widgets.ButtonInvisible(rowRect))
                {
                    selectedRequestLog = log;
                }
                
                itemY += 50f;
            }
            
            Widgets.EndScrollView();
        }
        
        private void DrawLLMRequestDetail(Rect rect)
        {
            Widgets.DrawBoxSolid(rect, new Color(0.08f, 0.08f, 0.1f, 0.9f));
            
            if (selectedRequestLog == null)
            {
                GUI.color = new Color(0.6f, 0.6f, 0.6f);
                Widgets.Label(new Rect(rect.x + 10, rect.y + 10, rect.width - 20, 30), 
                    "← 点击左侧请求查看详情");
                GUI.color = Color.white;
                return;
            }
            
            var log = selectedRequestLog;
            float x = rect.x + 10;
            float y = rect.y + 5;
            float width = rect.width - 20;
            float halfHeight = (rect.height - 80) / 2;
            
            // 标题栏
            Widgets.Label(new Rect(x, y, 300, 20), $"<b>{log.DisplayLabel}</b> - {log.Timestamp:yyyy-MM-dd HH:mm:ss}");
            
            // 复制按钮
            if (Widgets.ButtonText(new Rect(rect.x + rect.width - 170, y, 80, 20), "复制请求"))
            {
                GUIUtility.systemCopyBuffer = log.RequestJson ?? "";
                Messages.Message("请求 JSON 已复制", MessageTypeDefOf.NeutralEvent);
            }
            if (Widgets.ButtonText(new Rect(rect.x + rect.width - 85, y, 80, 20), "复制响应"))
            {
                GUIUtility.systemCopyBuffer = log.ResponseJson ?? "";
                Messages.Message("响应 JSON 已复制", MessageTypeDefOf.NeutralEvent);
            }
            y += 25;
            
            // 状态信息
            string statusText = log.Success ? "<color=#88ff88>✓ 成功</color>" : $"<color=#ff8888>✗ 失败: {log.ErrorMessage}</color>";
            Widgets.Label(new Rect(x, y, width, 20), statusText);
            y += 22;
            
            // Token 和耗时统计
            if (log.TotalTokens > 0)
            {
                GUI.color = new Color(0.7f, 0.8f, 1f);
                Widgets.Label(new Rect(x, y, width, 20), 
                    $"📊 Tokens: {log.TotalTokens} (Prompt: {log.PromptTokens}, Completion: {log.CompletionTokens}) | ⏱️ {log.DurationSeconds:F2}s");
                GUI.color = Color.white;
            }
            y += 25;
            
            // 请求 JSON
            Widgets.Label(new Rect(x, y, width, 20), "<b>📤 Request:</b>");
            y += 22;
            
            Rect requestRect = new Rect(x, y, width, halfHeight);
            Widgets.DrawBoxSolid(requestRect, new Color(0.05f, 0.05f, 0.08f, 0.9f));
            DrawScrollableText(requestRect, FormatJson(log.RequestJson), ref llmDetailScrollPos);
            y += halfHeight + 10;
            
            // 响应 JSON
            Widgets.Label(new Rect(x, y, width, 20), "<b>📥 Response:</b>");
            y += 22;
            
            Rect responseRect = new Rect(x, y, width, halfHeight);
            Widgets.DrawBoxSolid(responseRect, new Color(0.05f, 0.05f, 0.08f, 0.9f));
            
            Vector2 responseScrollPos = Vector2.zero;
            DrawScrollableText(responseRect, FormatJson(log.ResponseJson), ref responseScrollPos);
        }
        
        /// <summary>
        /// 格式化 JSON 字符串以便阅读（简单处理）
        /// </summary>
        private string FormatJson(string json)
        {
            if (string.IsNullOrEmpty(json)) return "(empty)";
            
            // 简单格式化：在关键符号后添加换行
            try
            {
                // 尝试使用 Newtonsoft.Json 格式化
                var obj = Newtonsoft.Json.JsonConvert.DeserializeObject(json);
                return Newtonsoft.Json.JsonConvert.SerializeObject(obj, Newtonsoft.Json.Formatting.Indented);
            }
            catch
            {
                // 如果解析失败，返回原始字符串
                return json;
            }
        }
        
        private void DrawAgentInfoTab(Rect rect)
        {
            if (selectedAgent == null)
            {
                Widgets.Label(rect, "TSS_Debug_SelectAgent".Translate());
                return;
            }
            
            // Calculate content height
            float totalHeight = 600f;
            if (!string.IsNullOrEmpty(selectedAgent.LastPrompt))
                totalHeight += selectedAgent.LastPrompt.Split('\n').Length * 20f;
            if (!string.IsNullOrEmpty(selectedAgent.LastResponseContent))
                totalHeight += selectedAgent.LastResponseContent.Split('\n').Length * 20f;
                
            Rect viewRect = new Rect(0, 0, rect.width - 16, totalHeight);
            Widgets.BeginScrollView(rect, ref debugInfoScrollPos, viewRect);
            
            float y = 10f;
            float width = viewRect.width - 20f;
            float x = 10f;
            
            // Basic Info
            Widgets.Label(new Rect(x, y, width, 24f), $"<b>{"TSS_Debug_AgentId".Translate()}:</b> {selectedAgent.AgentId}"); y += 24f;
            Widgets.Label(new Rect(x, y, width, 24f), $"<b>{"TSS_Debug_State".Translate()}:</b> {selectedAgent.State}"); y += 24f;
            Widgets.Label(new Rect(x, y, width, 24f), $"<b>{"TSS_Debug_Requests".Translate()}:</b> {selectedAgent.SuccessfulRequests} {"TSS_Debug_Success".Translate()} / {selectedAgent.FailedRequests} {"TSS_Debug_Failed".Translate()} / {selectedAgent.TotalRequests} {"TSS_Debug_Total".Translate()}"); y += 24f;
            Widgets.Label(new Rect(x, y, width, 24f), $"<b>{"TSS_Debug_ContextUsage".Translate()}:</b> ~{EstimateTokens(selectedAgent.Summary)} {"TSS_Debug_SummaryTokens".Translate()} / ~{EstimateHistoryTokens(selectedAgent.ConversationHistory)} {"TSS_Debug_HistoryTokens".Translate()}"); y += 30f;
            
            // Last Prompt
            Widgets.Label(new Rect(x, y, width, 24f), $"<b>--- {"TSS_Debug_LastPrompt".Translate()} ---</b>"); y += 24f;
            
            // Copy button
            if (Widgets.ButtonText(new Rect(x, y, 100, 22), "TSS_Debug_Copy".Translate()))
            {
                GUIUtility.systemCopyBuffer = selectedAgent.LastPrompt ?? "";
                Messages.Message("TSS_Debug_Copied".Translate(), MessageTypeDefOf.NeutralEvent);
            }
            y += 28f;
            
            string prompt = selectedAgent.LastPrompt ?? "TSS_Debug_NoPrompt".Translate();
            float promptHeight = Math.Max(Text.CalcHeight(prompt, width), 100f);
            Widgets.TextArea(new Rect(x, y, width, promptHeight), prompt, true);
            y += promptHeight + 10f;
            
            // Last Response
            Widgets.Label(new Rect(x, y, width, 24f), $"<b>--- {"TSS_Debug_LastResponse".Translate()} ---</b>"); y += 24f;
            string response = selectedAgent.LastResponseContent ?? "TSS_Debug_NoResponse".Translate();
            
            // Token info and copy button
            int responseTokens = response.Length / 3;
            Widgets.Label(new Rect(x, y, 200, 24f), $"<i>{"TSS_Debug_ApproxTokens".Translate()}: {responseTokens}</i>");
            if (Widgets.ButtonText(new Rect(x + 210, y, 100, 22), "TSS_Debug_Copy".Translate()))
            {
                GUIUtility.systemCopyBuffer = response;
                Messages.Message("TSS_Debug_Copied".Translate(), MessageTypeDefOf.NeutralEvent);
            }
            y += 28f;
            
            float responseHeight = Math.Max(Text.CalcHeight(response, width), 100f);
            Widgets.TextArea(new Rect(x, y, width, responseHeight), response, true);
            y += responseHeight + 10f;
            
            Widgets.EndScrollView();
        }
        
        private void DrawSystemPromptTab(Rect rect)
        {
            float x = 10f;
            float y = 5f;
            float width = rect.width - 20f;
            
            // Toolbar
            if (Widgets.ButtonText(new Rect(x, rect.y + y, 100, 25), "TSS_Debug_Refresh".Translate()))
            {
                RefreshPromptCache();
                Messages.Message("TSS_Debug_PromptRefreshed".Translate(), MessageTypeDefOf.NeutralEvent);
            }
            
            if (Widgets.ButtonText(new Rect(x + 110, rect.y + y, 120, 25), "TSS_Debug_CopyMaster".Translate()))
            {
                GUIUtility.systemCopyBuffer = cachedMasterPrompt;
                Messages.Message("TSS_Debug_Copied".Translate(), MessageTypeDefOf.NeutralEvent);
            }
            
            if (Widgets.ButtonText(new Rect(x + 240, rect.y + y, 150, 25), "TSS_Debug_CopyEventDir".Translate()))
            {
                GUIUtility.systemCopyBuffer = cachedEventDirectorPrompt;
                Messages.Message("TSS_Debug_Copied".Translate(), MessageTypeDefOf.NeutralEvent);
            }
            
            // ⭐ v2.0.0: 热重载模板缓存按钮
            if (Widgets.ButtonText(new Rect(x + 400, rect.y + y, 140, 25), "🔄 Reload Templates"))
            {
                PromptRenderer.ClearTemplateCache();
                RefreshPromptCache();
                Messages.Message("模板编译缓存已清除，所有模板将重新编译", MessageTypeDefOf.TaskCompletion);
            }
            
            // Token counts & Cache stats
            int masterTokens = EstimateTokens(cachedMasterPrompt);
            int eventTokens = EstimateTokens(cachedEventDirectorPrompt);
            Widgets.Label(new Rect(x + 550, rect.y + y, 250, 25), 
                $"Master: ~{masterTokens} | EventDir: ~{eventTokens}");
            
            y += 28f;
            
            // ⭐ v2.0.0: 显示缓存统计
            string cacheStats = PromptRenderer.GetCacheStats();
            Widgets.Label(new Rect(rect.x + x, rect.y + y, width, 20), 
                $"<color=#88ff88>📊 Cache: {cacheStats}</color>");
            
            y += 35f;
            
            // Two-column layout for prompts
            float colWidth = (width - 10) / 2f;
            float colHeight = rect.height - 50f;
            
            // Left: Master Prompt
            Rect masterRect = new Rect(rect.x + x, rect.y + y, colWidth, colHeight);
            Widgets.Label(new Rect(masterRect.x, masterRect.y, colWidth, 20), "<b>Master Prompt</b>");
            Rect masterTextRect = new Rect(masterRect.x, masterRect.y + 22, colWidth, colHeight - 25);
            Widgets.DrawBoxSolid(masterTextRect, new Color(0.1f, 0.1f, 0.1f, 0.8f));
            DrawScrollableText(masterTextRect, cachedMasterPrompt, ref promptPreviewScrollPos);
            
            // Right: EventDirector Prompt  
            Rect eventRect = new Rect(rect.x + x + colWidth + 10, rect.y + y, colWidth, colHeight);
            Widgets.Label(new Rect(eventRect.x, eventRect.y, colWidth, 20), "<b>EventDirector Prompt</b>");
            Rect eventTextRect = new Rect(eventRect.x, eventRect.y + 22, colWidth, colHeight - 25);
            Widgets.DrawBoxSolid(eventTextRect, new Color(0.1f, 0.1f, 0.1f, 0.8f));
            
            Vector2 eventScrollPos = Vector2.zero;
            DrawScrollableText(eventTextRect, cachedEventDirectorPrompt, ref eventScrollPos);
        }
        
        private void DrawContextDataTab(Rect rect)
        {
            float x = 10f;
            float y = 5f;
            float width = rect.width - 20f;
            
            // Toolbar
            if (Widgets.ButtonText(new Rect(x, rect.y + y, 100, 25), "TSS_Debug_Refresh".Translate()))
            {
                RefreshPromptCache();
            }
            y += 35f;
            
            if (cachedManager == null)
            {
                Widgets.Label(new Rect(rect.x + x, rect.y + y, width, 30), "TSS_Debug_NoContext".Translate());
                return;
            }
            
            // Build context data display
            var sb = new StringBuilder();
            
            // NarratorManager 数据
            sb.AppendLine("=== NarratorManager ===");
            sb.AppendLine($"  CurrentPersona: {cachedManager.GetCurrentPersona()?.narratorName ?? "(null)"}");
            sb.AppendLine($"  Favorability: {cachedManager.Favorability:F0}");
            sb.AppendLine($"  CurrentTier: {cachedManager.CurrentTier}");
            
            sb.AppendLine();
            sb.AppendLine("=== PersonaDef ===");
            var persona = cachedManager.GetCurrentPersona();
            if (persona != null)
            {
                sb.AppendLine($"  defName: {persona.defName}");
                sb.AppendLine($"  narratorName: {persona.narratorName}");
                sb.AppendLine($"  label: {persona.label}");
                sb.AppendLine($"  mercyLevel: {persona.mercyLevel:F2}");
                sb.AppendLine($"  narratorChaosLevel: {persona.narratorChaosLevel:F2}");
                sb.AppendLine($"  dominanceLevel: {persona.dominanceLevel:F2}");
                sb.AppendLine($"  descentAnimationType: {persona.descentAnimationType}");
                if (persona.visualElements != null && persona.visualElements.Count > 0)
                {
                    sb.AppendLine($"  visualElements: [{string.Join(", ", persona.visualElements)}]");
                }
            }
            else
            {
                sb.AppendLine("  (null)");
            }
            
            sb.AppendLine();
            sb.AppendLine("=== StorytellerAgent ===");
            if (cachedStorytellerAgent != null)
            {
                sb.AppendLine($"  affinity: {cachedStorytellerAgent.affinity:F0}");
                sb.AppendLine($"  currentMood: {cachedStorytellerAgent.currentMood}");
                if (cachedStorytellerAgent.dialogueStyle != null)
                {
                    var style = cachedStorytellerAgent.dialogueStyle;
                    sb.AppendLine($"  dialogueStyle.formalityLevel: {style.formalityLevel:F2}");
                    sb.AppendLine($"  dialogueStyle.emotionalExpression: {style.emotionalExpression:F2}");
                    sb.AppendLine($"  dialogueStyle.verbosity: {style.verbosity:F2}");
                    sb.AppendLine($"  dialogueStyle.humorLevel: {style.humorLevel:F2}");
                    sb.AppendLine($"  dialogueStyle.sarcasmLevel: {style.sarcasmLevel:F2}");
                }
            }
            else
            {
                sb.AppendLine("  (null)");
            }
            
            sb.AppendLine();
            sb.AppendLine("=== GameState ===");
            if (Find.CurrentMap != null)
            {
                var map = Find.CurrentMap;
                sb.AppendLine($"  Map: {map.Tile}");
                sb.AppendLine($"  ColonistCount: {map.mapPawns?.FreeColonistsCount ?? 0}");
                sb.AppendLine($"  GameTicks: {Find.TickManager?.TicksGame ?? 0}");
                sb.AppendLine($"  Hour: {GenLocalDate.HourOfDay(map)}");
            }
            else
            {
                sb.AppendLine("  (No map loaded)");
            }
            
            // Display scrollable
            Rect viewRect = new Rect(rect.x + x, rect.y + y, width, rect.height - 50);
            DrawScrollableText(viewRect, sb.ToString(), ref contextDataScrollPos);
        }
        
        private void DrawScrollableText(Rect rect, string text, ref Vector2 scrollPos)
        {
            if (string.IsNullOrEmpty(text))
            {
                text = "(empty)";
            }
            
            float textHeight = Text.CalcHeight(text, rect.width - 20);
            Rect viewRect = new Rect(0, 0, rect.width - 16, textHeight + 20);
            
            Widgets.BeginScrollView(rect, ref scrollPos, viewRect);
            
            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.9f, 0.9f, 0.9f);
            Widgets.Label(new Rect(5, 5, viewRect.width - 10, textHeight), text);
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            
            Widgets.EndScrollView();
        }
        
        private void RefreshPromptCache()
        {
            try
            {
                // 获取当前 NarratorManager (GameComponent)
                cachedManager = Current.Game?.GetComponent<NarratorManager>();
                if (cachedManager == null)
                {
                    cachedMasterPrompt = "[No NarratorManager active]";
                    cachedEventDirectorPrompt = "[No NarratorManager active]";
                    cachedStorytellerAgent = null;
                    return;
                }
                
                // 获取所需数据
                var personaDef = cachedManager.GetCurrentPersona();
                cachedStorytellerAgent = cachedManager.GetStorytellerAgent();
                
                if (personaDef == null || cachedStorytellerAgent == null)
                {
                    cachedMasterPrompt = "[No persona or StorytellerAgent available]";
                    cachedEventDirectorPrompt = "[No persona or StorytellerAgent available]";
                    return;
                }
                
                // 获取人格分析结果
                PersonaAnalysisResult analysis = null;
                try
                {
                    analysis = PersonaAnalyzer.AnalyzePersonaDef(personaDef);
                }
                catch
                {
                    analysis = new PersonaAnalysisResult(); // 使用默认值
                }
                
                // 渲染 Master Prompt
                try
                {
                    cachedMasterPrompt = SystemPromptGenerator.GenerateSystemPrompt(
                        personaDef, 
                        analysis, 
                        cachedStorytellerAgent, 
                        AIDifficultyMode.Assistant);
                }
                catch (Exception ex)
                {
                    cachedMasterPrompt = $"[Render Error: {ex.Message}]";
                }
                
                // 渲染 EventDirector Prompt
                try
                {
                    cachedEventDirectorPrompt = SystemPromptGenerator.GenerateEventDirectorPrompt(
                        personaDef, 
                        analysis, 
                        cachedStorytellerAgent,
                        AIDifficultyMode.Assistant);
                }
                catch (Exception ex)
                {
                    cachedEventDirectorPrompt = $"[Render Error: {ex.Message}]";
                }
                
                promptNeedsRefresh = false;
            }
            catch (Exception ex)
            {
                cachedMasterPrompt = $"[Error: {ex.Message}]";
                cachedEventDirectorPrompt = $"[Error: {ex.Message}]";
                Log.Error($"[TSS Debug] Failed to refresh prompt cache: {ex}");
            }
        }
        
        private int EstimateTokens(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            return text.Length / 3;
        }
        
        private int EstimateHistoryTokens(List<AgentMessage> history)
        {
            if (history == null) return 0;
            int chars = 0;
            foreach (var msg in history)
            {
                chars += msg.Content?.Length ?? 0;
            }
            return chars / 3;
        }
    }
}
