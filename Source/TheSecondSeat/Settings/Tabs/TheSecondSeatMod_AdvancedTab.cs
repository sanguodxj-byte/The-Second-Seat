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
                    SettingsUIComponents.DrawInfoBox(infoRect, "配置 LLM API、多模态分析、Agent 重试机制、并发管理", InfoBoxType.Info);
                    
                    Rect buttonRect = new Rect(contentRect.x, contentRect.y + 44f, contentRect.width, 32f);
                    if (SettingsUIComponents.DrawButton(buttonRect, "打开 Agent 配置面板", SettingsUIComponents.AccentOrange))
                    {
                        Find.WindowStack.Add(new UI.Dialog_UnifiedAgentSettings());
                    }
                });
                y += agentHeight + SettingsUIComponents.MediumGap;
                
                // === 网络搜索设置 ===
                float webSearchHeight = 220f;
                Rect webSearchRect = new Rect(viewRect.x, y, cardWidth, webSearchHeight);
                SettingsUIComponents.DrawSettingsGroup(webSearchRect, "网络搜索", SettingsUIComponents.AccentOrange, (contentRect) =>
                {
                    float cy = contentRect.y;
                    
                    // 启用开关
                    Rect enableRect = new Rect(contentRect.x, cy, contentRect.width, 28f);
                    SettingsUIComponents.DrawToggleSetting(enableRect, "启用网络搜索", 
                        "允许 AI 搜索网络获取信息", ref Settings.enableWebSearch);
                    cy += 34f;
                    
                    if (Settings.enableWebSearch)
                    {
                        // 搜索引擎选择
                        string[] engines = { "duckduckgo", "bing", "google" };
                        string[] engineNames = { "DuckDuckGo (免费)", "Bing (需API)", "Google (需API)" };
                        int currentIndex = Array.IndexOf(engines, Settings.searchEngine);
                        if (currentIndex < 0) currentIndex = 0;
                        
                        Rect engineRect = new Rect(contentRect.x, cy, contentRect.width, 28f);
                        SettingsUIComponents.DrawDropdownSetting(engineRect, "搜索引擎", 
                            "选择搜索服务", engineNames[currentIndex], engineNames, 
                            (selected) => {
                                int idx = Array.IndexOf(engineNames, selected);
                                if (idx >= 0) Settings.searchEngine = engines[idx];
                            });
                        cy += 36f;
                        
                        // 搜索延迟
                        float delayFloat = Settings.searchDelayMs;
                        Rect delayRect = new Rect(contentRect.x, cy, contentRect.width, 28f);
                        SettingsUIComponents.DrawSliderSetting(delayRect, "搜索延迟 (ms)", 
                            "搜索请求间隔", ref delayFloat, 0f, 5000f, "F0");
                        Settings.searchDelayMs = (int)delayFloat;
                    }
                });
                y += webSearchHeight + SettingsUIComponents.MediumGap;
                
                
                // === 开发者工具 ===
                float devToolsHeight = 240f; // Increased height for more buttons
                Rect devToolsRect = new Rect(viewRect.x, y, cardWidth, devToolsHeight);
                SettingsUIComponents.DrawSettingsGroup(devToolsRect, "开发者工具", SettingsUIComponents.AccentPurple, (contentRect) =>
                {
                    float cy = contentRect.y;
                    
                    Rect infoRect = new Rect(contentRect.x, cy, contentRect.width, 36f);
                    SettingsUIComponents.DrawInfoBox(infoRect, "高级配置与调试工具", InfoBoxType.Info);
                    cy += 44f;
                    
                    // 工程师模式开关
                    Rect engineerModeRect = new Rect(contentRect.x, cy, contentRect.width, 28f);
                    SettingsUIComponents.DrawToggleSetting(engineerModeRect, "工程师模式", 
                        "开启详细错误监听和高级调试功能（仅限开发人员）", ref Settings.engineerMode);
                    cy += 34f;
                    
                    Rect renderTreeBtnRect = new Rect(contentRect.x, cy, contentRect.width, 32f);
                    if (SettingsUIComponents.DrawButton(renderTreeBtnRect, "打开渲染树编辑器", SettingsUIComponents.AccentPurple))
                    {
                        // v1.11.0: 使用新的 RenderTreeConfigWindow
                        Find.WindowStack.Add(new PersonaGeneration.RenderTreeConfigWindow());
                    }
                    
                    cy += 40f;

                    Rect promptBtnRect = new Rect(contentRect.x, cy, contentRect.width, 32f);
                    if (SettingsUIComponents.DrawButton(promptBtnRect, "打开提示词管理", SettingsUIComponents.AccentGreen))
                    {
                        Find.WindowStack.Add(new UI.PromptManagementWindow());
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
