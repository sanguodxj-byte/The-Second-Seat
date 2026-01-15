using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using RimWorld;

namespace TheSecondSeat.RimAgent.UI
{
    public class RimAgentDebugWindow : Window
    {
        private RimAgent selectedAgent;
        private Vector2 agentListScrollPos;
        private Vector2 debugInfoScrollPos;
        
        public override Vector2 InitialSize => new Vector2(1000f, 700f);
        
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
            Widgets.Label(new Rect(0, 0, 300, 30), "RimAgent Debugger");
            Text.Font = GameFont.Small;
            
            float contentY = 40f;
            float leftWidth = 250f;
            float rightX = leftWidth + 10f;
            float rightWidth = inRect.width - rightX;
            float contentHeight = inRect.height - contentY;
            
            // Left: Agent List
            Rect leftRect = new Rect(0, contentY, leftWidth, contentHeight);
            DrawAgentList(leftRect);
            
            // Right: Debug Info
            Rect rightRect = new Rect(rightX, contentY, rightWidth, contentHeight);
            DrawDebugInfo(rightRect);
        }
        
        private void DrawAgentList(Rect rect)
        {
            Widgets.DrawMenuSection(rect);
            
            var agents = RimAgent.ActiveAgents;
            
            Rect viewRect = new Rect(0, 0, rect.width - 16, agents.Count * 30f);
            Widgets.BeginScrollView(rect, ref agentListScrollPos, viewRect);
            
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
                }
                
                y += 30f;
            }
            
            Widgets.EndScrollView();
        }
        
        private void DrawDebugInfo(Rect rect)
        {
            if (selectedAgent == null)
            {
                Widgets.Label(rect, "Select an agent to view details.");
                return;
            }
            
            Widgets.DrawMenuSection(rect);
            
            // Calculate content height (approximate)
            float totalHeight = 1000f; // Simplified, dynamic height is better but complex
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
            Widgets.Label(new Rect(x, y, width, 24f), $"<b>Agent ID:</b> {selectedAgent.AgentId}"); y += 24f;
            Widgets.Label(new Rect(x, y, width, 24f), $"<b>State:</b> {selectedAgent.State}"); y += 24f;
            Widgets.Label(new Rect(x, y, width, 24f), $"<b>Requests:</b> {selectedAgent.SuccessfulRequests} success / {selectedAgent.FailedRequests} failed / {selectedAgent.TotalRequests} total"); y += 24f;
            Widgets.Label(new Rect(x, y, width, 24f), $"<b>Context Usage:</b> ~{EstimateTokens(selectedAgent.Summary)} summary / ~{EstimateHistoryTokens(selectedAgent.ConversationHistory)} history tokens"); y += 30f;
            
            // Last Prompt
            Widgets.Label(new Rect(x, y, width, 24f), "<b>--- Last Prompt Sent ---</b>"); y += 24f;
            string prompt = selectedAgent.LastPrompt ?? "(No prompt sent yet)";
            float promptHeight = Text.CalcHeight(prompt, width) + 20f;
            Widgets.TextArea(new Rect(x, y, width, promptHeight), prompt, true);
            y += promptHeight + 10f;
            
            // Last Response
            Widgets.Label(new Rect(x, y, width, 24f), "<b>--- Last Response Received ---</b>"); y += 24f;
            string response = selectedAgent.LastResponseContent ?? "(No response received yet)";
            
            // Calculate approx token usage for response
            int responseTokens = response.Length / 3;
            Widgets.Label(new Rect(x, y, width, 24f), $"<i>Approx. Response Tokens: {responseTokens}</i>"); y += 24f;
            
            float responseHeight = Text.CalcHeight(response, width) + 20f;
            Widgets.TextArea(new Rect(x, y, width, responseHeight), response, true);
            y += responseHeight + 10f;
            
            Widgets.EndScrollView();
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