using UnityEngine;
using Verse;
using RimWorld;
using System;
using TheSecondSeat.WebSearch;

namespace TheSecondSeat.Settings
{
    public partial class TheSecondSeatMod
    {
        private void DrawAdvancedSettingsTab(Rect rect)
        {
            Vector2 scrollPos = tabManager.GetScrollPosition();
            float contentHeight = 800f;
            
            SettingsUIComponents.DrawScrollableCardContent(rect, ref scrollPos, contentHeight, (viewRect) =>
            {
                float y = viewRect.y + SettingsUIComponents.MediumGap;
                float cardWidth = viewRect.width - 10f;
                
                // === Agent 配置入口 ===
                float agentHeight = 120f;
                Rect agentRect = new Rect(viewRect.x, y, cardWidth, agentHeight);
                SettingsUIComponents.DrawSettingsGroup(agentRect, "Agent 高级配置", SettingsUIComponents.AccentOrange, (contentRect) =>
                {
                    Rect infoRect = new Rect(contentRect.x, contentRect.y, contentRect.width, 36f);
                    SettingsUIComponents.DrawInfoBox(infoRect, "配置多模态分析、Agent 重试机制、并发管理", InfoBoxType.Info);
                    
                    Rect buttonRect = new Rect(contentRect.x, contentRect.y + 44f, contentRect.width, 32f);
                    if (SettingsUIComponents.DrawButton(buttonRect, "打开 Agent 配置面板", SettingsUIComponents.AccentOrange))
                    {
                        Find.WindowStack.Add(new UI.Dialog_UnifiedAgentSettings());
                    }
                });
                y += agentHeight + SettingsUIComponents.MediumGap;
                
                // === 系统查看器 ===
                float sysViewHeight = 140f;
                Rect sysViewRect = new Rect(viewRect.x, y, cardWidth, sysViewHeight);
                SettingsUIComponents.DrawSettingsGroup(sysViewRect, "系统状态查看器", SettingsUIComponents.AccentBlue, (contentRect) =>
                {
                    float cy = contentRect.y;
                    
                    // Command Viewer
                    Rect cmdBtnRect = new Rect(contentRect.x, cy, contentRect.width, 32f);
                    if (SettingsUIComponents.DrawButton(cmdBtnRect, "查看注册指令 (Commands)", SettingsUIComponents.AccentGreen))
                    {
                        Find.WindowStack.Add(new UI.Dialog_CommandViewer());
                    }
                    cy += 40f;
                    
                    // Memory Viewer
                    Rect memBtnRect = new Rect(contentRect.x, cy, contentRect.width, 32f);
                    if (SettingsUIComponents.DrawButton(memBtnRect, "查看叙事记忆 (Memory)", SettingsUIComponents.AccentPurple))
                    {
                        Find.WindowStack.Add(new UI.Dialog_MemoryViewer());
                    }
                    cy += 40f;
                    
                    // SmartPrompt Debugger
                    Rect promptBtnRect = new Rect(contentRect.x, cy, contentRect.width, 32f);
                    if (SettingsUIComponents.DrawButton(promptBtnRect, "调试 SmartPrompt (Intent)", SettingsUIComponents.AccentOrange))
                    {
                        Find.WindowStack.Add(new UI.Dialog_SmartPromptDebugger());
                    }
                });
                y += sysViewHeight + SettingsUIComponents.MediumGap;


                // === 开发者工具 ===
                float devToolsHeight = 220f;
                Rect devToolsRect = new Rect(viewRect.x, y, cardWidth, devToolsHeight);
                SettingsUIComponents.DrawSettingsGroup(devToolsRect, "开发者工具", SettingsUIComponents.AccentPurple, (contentRect) =>
                {
                    float cy = contentRect.y;
                    
                    Rect infoRect = new Rect(contentRect.x, cy, contentRect.width, 36f);
                    SettingsUIComponents.DrawInfoBox(infoRect, "高级配置与调试工具", InfoBoxType.Info);
                    cy += 44f;
                    
                    Rect renderTreeBtnRect = new Rect(contentRect.x, cy, contentRect.width, 32f);
                    if (SettingsUIComponents.DrawButton(renderTreeBtnRect, "打开渲染树编辑器", SettingsUIComponents.AccentPurple))
                    {
                        // v1.11.0: 使用新的 RenderTreeConfigWindow
                        Find.WindowStack.Add(new PersonaGeneration.RenderTreeConfigWindow());
                    }
                    
                    cy += 40f;

                    // ⭐ v3.0: Unified Prompt Manager
                    Rect presetBtnRect = new Rect(contentRect.x, cy, contentRect.width, 32f);
                    if (SettingsUIComponents.DrawButton(presetBtnRect, "TSS_PromptManager_Title".Translate(), SettingsUIComponents.AccentOrange))
                    {
                        Find.WindowStack.Add(new UI.PromptPresetsWindow());
                    }
                    cy += 40f;
                    
                    Rect debugAgentBtnRect = new Rect(contentRect.x, cy, contentRect.width, 32f);
                    if (SettingsUIComponents.DrawButton(debugAgentBtnRect, "打开 RimAgent 调试器", SettingsUIComponents.AccentBlue))
                    {
                        Find.WindowStack.Add(new RimAgent.UI.RimAgentDebugWindow());
                    }
                });
                y += devToolsHeight + SettingsUIComponents.MediumGap;

                // === 操作按钮 ===
                float buttonAreaHeight = 80f;
                Rect buttonGroupRect = new Rect(viewRect.x, y, cardWidth, buttonAreaHeight);
                
                Rect applyRect = new Rect(buttonGroupRect.x, buttonGroupRect.y, buttonGroupRect.width, 32f);
                if (SettingsUIComponents.DrawButton(applyRect, "应用所有设置", SettingsUIComponents.AccentGreen))
                {
                    ApplyAllSettings();
                }
                
                Rect clearCacheRect = new Rect(buttonGroupRect.x, buttonGroupRect.y + 40f, buttonGroupRect.width, 32f);
                if (Settings.enableWebSearch && SettingsUIComponents.DrawButton(clearCacheRect, "清除搜索缓存", SettingsUIComponents.AccentYellow))
                {
                    WebSearchService.Instance.ClearCache();
                    Messages.Message("搜索缓存已清除", MessageTypeDefOf.NeutralEvent);
                }
            });
            
            tabManager.SetScrollPosition(scrollPos);
        }

        private void ApplyAllSettings()
        {
            LLM.LLMService.Instance.Configure(
                Settings.apiEndpoint,
                Settings.apiKey,
                Settings.modelName,
                Settings.llmProvider
            );

            if (Settings.enableWebSearch)
            {
                ConfigureWebSearch();
            }
            
            if (Settings.enableMultimodalAnalysis)
            {
                ConfigureMultimodalAnalysis();
            }

            if (Settings.enableTTS)
            {
                ConfigureTTS();
            }
            
            // 保存到磁盘
            Settings.Write();

            Messages.Message("所有设置已应用并保存", MessageTypeDefOf.PositiveEvent);
        }

        private async System.Threading.Tasks.Task TestConnectionAsync()
        {
            try
            {
                Messages.Message("正在测试连接...", MessageTypeDefOf.NeutralEvent);
                
                var success = await LLM.LLMService.Instance.TestConnectionAsync();
                
                if (success)
                {
                    Messages.Message("连接测试成功！", MessageTypeDefOf.PositiveEvent);
                }
                else
                {
                    Messages.Message("连接测试失败", MessageTypeDefOf.NegativeEvent);
                }
            }
            catch (System.Exception ex)
            {
                Log.Error($"[ModSettings] TestConnection failed: {ex.Message}");
                Messages.Message($"连接测试失败: {ex.Message}", MessageTypeDefOf.NegativeEvent);
            }
        }

        private void ShowVoiceSelectionMenu()
        {
            var voices = TTS.TTSService.GetAvailableVoices();
            var options = new System.Collections.Generic.List<FloatMenuOption>();

            foreach (var voice in voices)
            {
                string voiceCopy = voice;
                options.Add(new FloatMenuOption(voice, () => {
                    Settings.ttsVoice = voiceCopy;
                }));
            }

            Find.WindowStack.Add(new FloatMenu(options));
        }

    }
}
