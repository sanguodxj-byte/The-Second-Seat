using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using RimWorld;
using TheSecondSeat.RimAgent;

namespace TheSecondSeat.UI
{
    /// <summary>
    /// ? v1.6.65: RimAgent 设置弹窗
    /// 包含 Agent 配置和工具库管理
    /// </summary>
    public class Dialog_RimAgentSettings : Window
    {
        private Vector2 scrollPosition = Vector2.zero;
        private string agentName = "main-narrator";
        private int maxRetries = 3;
        private float retryDelay = 2f;
        private int maxHistoryMessages = 20;
        
        // 工具启用状态
        private Dictionary<string, bool> toolsEnabled = new Dictionary<string, bool>();
        
        public override Vector2 InitialSize => new Vector2(700f, 600f);

        public Dialog_RimAgentSettings()
        {
            doCloseButton = true;
            doCloseX = true;
            forcePause = true;
            absorbInputAroundWindow = true;
            
            // 加载设置
            LoadSettings();
            
            // 初始化工具启用状态
            var registeredTools = RimAgentTools.GetRegisteredToolNames();
            foreach (var toolName in registeredTools)
            {
                if (!toolsEnabled.ContainsKey(toolName))
                {
                    toolsEnabled[toolName] = true; // 默认启用
                }
            }
        }

        private void LoadSettings()
        {
            var settings = LoadedModManager.GetMod<Settings.TheSecondSeatMod>()?
                .GetSettings<Settings.TheSecondSeatSettings>();
            
            if (settings != null)
            {
                agentName = settings.agentName ?? "main-narrator";
                maxRetries = settings.maxRetries;
                retryDelay = settings.retryDelay;
                maxHistoryMessages = settings.maxHistoryMessages;
                toolsEnabled = new Dictionary<string, bool>(settings.toolsEnabled ?? new Dictionary<string, bool>());
            }
        }

        private void SaveSettings()
        {
            var settings = LoadedModManager.GetMod<Settings.TheSecondSeatMod>()?
                .GetSettings<Settings.TheSecondSeatSettings>();
            
            if (settings != null)
            {
                settings.agentName = agentName;
                settings.maxRetries = maxRetries;
                settings.retryDelay = retryDelay;
                settings.maxHistoryMessages = maxHistoryMessages;
                settings.toolsEnabled = new Dictionary<string, bool>(toolsEnabled);
                settings.Write();
            }
        }

        public override void DoWindowContents(Rect inRect)
        {
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(inRect);
            
            // 标题
            Text.Font = GameFont.Medium;
            listing.Label("? RimAgent 设置");
            Text.Font = GameFont.Small;
            listing.Gap(10f);
            
            // 滚动区域
            Rect scrollRect = new Rect(0f, listing.CurHeight, inRect.width, inRect.height - listing.CurHeight - 60f);
            Rect viewRect = new Rect(0f, 0f, scrollRect.width - 20f, 800f);
            
            Widgets.BeginScrollView(scrollRect, ref scrollPosition, viewRect);
            
            Listing_Standard scrollListing = new Listing_Standard();
            scrollListing.Begin(viewRect);
            
            // ========================================
            // Agent 基础配置
            // ========================================
            scrollListing.Label("?? Agent 基础配置");
            scrollListing.GapLine(12f);
            
            // Agent 名称
            Rect nameRect = scrollListing.GetRect(30f);
            Widgets.Label(nameRect.LeftHalf(), "Agent 名称:");
            agentName = Widgets.TextField(nameRect.RightHalf(), agentName);
            scrollListing.Gap(5f);
            
            // 最大重试次数
            Rect retriesRect = scrollListing.GetRect(30f);
            Widgets.Label(retriesRect.LeftHalf(), $"最大重试次数: {maxRetries}");
            maxRetries = (int)Widgets.HorizontalSlider(
                retriesRect.RightHalf(), 
                maxRetries, 
                1f, 
                5f, 
                middleAlignment: true, 
                label: maxRetries.ToString()
            );
            scrollListing.Gap(5f);
            
            // 重试延迟
            Rect delayRect = scrollListing.GetRect(30f);
            Widgets.Label(delayRect.LeftHalf(), $"重试延迟(秒): {retryDelay:F1}");
            retryDelay = Widgets.HorizontalSlider(
                delayRect.RightHalf(), 
                retryDelay, 
                1f, 
                10f, 
                middleAlignment: true, 
                label: $"{retryDelay:F1}s"
            );
            scrollListing.Gap(5f);
            
            // 历史消息数量
            Rect historyRect = scrollListing.GetRect(30f);
            Widgets.Label(historyRect.LeftHalf(), $"历史消息数量: {maxHistoryMessages}");
            maxHistoryMessages = (int)Widgets.HorizontalSlider(
                historyRect.RightHalf(), 
                maxHistoryMessages, 
                5f, 
                50f, 
                middleAlignment: true, 
                label: maxHistoryMessages.ToString()
            );
            scrollListing.Gap(15f);
            
            // ========================================
            // 工具库管理
            // ========================================
            scrollListing.Label("?? 工具库管理");
            scrollListing.GapLine(12f);
            
            var registeredTools = RimAgentTools.GetRegisteredToolNames();
            
            if (registeredTools.Count == 0)
            {
                scrollListing.Label("?? 未找到已注册的工具");
            }
            else
            {
                foreach (var toolName in registeredTools)
                {
                    // 确保工具在字典中
                    if (!toolsEnabled.ContainsKey(toolName))
                    {
                        toolsEnabled[toolName] = true;
                    }
                    
                    Rect toolRect = scrollListing.GetRect(30f);
                    
                    // 工具图标（简单的颜色块）
                    Rect iconRect = new Rect(toolRect.x, toolRect.y, 24f, 24f);
                    Color iconColor = toolsEnabled[toolName] ? Color.green : Color.gray;
                    GUI.color = iconColor;
                    Widgets.DrawBoxSolid(iconRect, iconColor);
                    GUI.color = Color.white;
                    
                    // 工具名称
                    Rect nameRectTool = new Rect(toolRect.x + 30f, toolRect.y, toolRect.width / 2 - 40f, toolRect.height);
                    Widgets.Label(nameRectTool, toolName);
                    
                    // 工具描述（获取）
                    string description = GetToolDescription(toolName);
                    Rect descRect = new Rect(toolRect.x + toolRect.width / 2, toolRect.y, toolRect.width / 3, toolRect.height);
                    Text.Font = GameFont.Tiny;
                    Widgets.Label(descRect, description);
                    Text.Font = GameFont.Small;
                    
                    // 启用/禁用开关
                    Rect toggleRect = new Rect(toolRect.xMax - 80f, toolRect.y, 80f, toolRect.height);
                    bool oldEnabled = toolsEnabled[toolName];
                    bool newEnabled = oldEnabled;
                    
                    if (Widgets.ButtonText(toggleRect, oldEnabled ? "? 启用" : "? 禁用"))
                    {
                        newEnabled = !oldEnabled;
                        toolsEnabled[toolName] = newEnabled;
                    }
                    
                    scrollListing.Gap(5f);
                }
            }
            
            scrollListing.Gap(15f);
            
            // ========================================
            // Agent 统计信息
            // ========================================
            scrollListing.Label("?? Agent 统计");
            scrollListing.GapLine(12f);
            
            try
            {
                var manager = Current.Game?.GetComponent<Narrator.NarratorManager>();
                if (manager != null)
                {
                    string stats = manager.GetAgentStats();
                    
                    // 显示统计信息（多行文本框）
                    Rect statsRect = scrollListing.GetRect(100f);
                    Widgets.DrawBoxSolid(statsRect, new Color(0.1f, 0.1f, 0.1f, 0.5f));
                    Text.Font = GameFont.Tiny;
                    Widgets.Label(statsRect.ContractedBy(5f), stats);
                    Text.Font = GameFont.Small;
                    
                    scrollListing.Gap(10f);
                    
                    // 重置 Agent 按钮
                    if (scrollListing.ButtonText("?? 重置 Agent", "清除对话历史和统计"))
                    {
                        manager.ResetAgent();
                        Messages.Message("Agent 已重置", MessageTypeDefOf.PositiveEvent);
                    }
                }
                else
                {
                    scrollListing.Label("?? 未找到 NarratorManager（需要在游戏中使用）");
                }
            }
            catch (Exception ex)
            {
                scrollListing.Label($"? 获取统计失败: {ex.Message}");
            }
            
            scrollListing.End();
            Widgets.EndScrollView();
            
            // 底部按钮
            listing.End();
            
            Rect bottomRect = new Rect(inRect.x, inRect.yMax - 50f, inRect.width, 50f);
            
            // 保存按钮
            if (Widgets.ButtonText(new Rect(bottomRect.x, bottomRect.y, 150f, 35f), "?? 保存设置"))
            {
                SaveSettings();
                Messages.Message("RimAgent 设置已保存", MessageTypeDefOf.PositiveEvent);
            }
            
            // 关闭按钮
            if (Widgets.ButtonText(new Rect(bottomRect.xMax - 150f, bottomRect.y, 150f, 35f), "关闭"))
            {
                Close();
            }
        }
        
        private string GetToolDescription(string toolName)
        {
            try
            {
                var tool = RimAgentTools.GetTool(toolName);
                if (tool != null)
                {
                    return tool.Description;
                }
            }
            catch
            {
                // Ignore
            }
            
            return "无描述";
        }
    }
}
