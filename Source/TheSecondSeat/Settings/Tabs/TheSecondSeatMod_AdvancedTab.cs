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
                
                // === 全局提示词 ===
                float promptHeight = 250f;
                Rect promptRect = new Rect(viewRect.x, y, cardWidth, promptHeight);
                SettingsUIComponents.DrawSettingsGroup(promptRect, "全局提示词", SettingsUIComponents.AccentOrange, (contentRect) =>
                {
                    float cy = contentRect.y;
                    
                    Rect infoRect = new Rect(contentRect.x, cy, contentRect.width, 36f);
                    SettingsUIComponents.DrawInfoBox(infoRect, "添加额外指令来自定义叙事者的行为和风格", InfoBoxType.Info);
                    cy += 44f;
                    
                    Rect textAreaRect = new Rect(contentRect.x, cy, contentRect.width, 120f);
                    SettingsUIComponents.DrawTextAreaSetting(textAreaRect, "", null, ref Settings.globalPrompt, 100f);
                    cy += 110f;
                    
                    Rect exampleRect = new Rect(contentRect.x, cy, contentRect.width, 28f);
                    if (SettingsUIComponents.DrawButton(exampleRect, "加载示例提示词", SettingsUIComponents.AccentBlue))
                    {
                        Settings.globalPrompt = GetExampleGlobalPrompt();
                    }
                });
                y += promptHeight + SettingsUIComponents.MediumGap;

                // === 自定义提示词文件 ===
                float customPromptsHeight = 180f;
                Rect customPromptsRect = new Rect(viewRect.x, y, cardWidth, customPromptsHeight);
                SettingsUIComponents.DrawSettingsGroup(customPromptsRect, "自定义提示词文件", SettingsUIComponents.AccentOrange, (contentRect) =>
                {
                    float cy = contentRect.y;
                    
                    Rect infoRect = new Rect(contentRect.x, cy, contentRect.width, 60f);
                    SettingsUIComponents.DrawInfoBox(infoRect, "您可以创建自定义的 .txt 文件来覆盖 Mod 默认的提示词。\n这些文件保存在配置文件夹中，不会因 Mod 更新而丢失。", InfoBoxType.Info);
                    cy += 68f;
                    
                    Rect readmeBtnRect = new Rect(contentRect.x, cy, contentRect.width, 32f);
                    if (SettingsUIComponents.DrawButton(readmeBtnRect, "生成 README 说明文件", SettingsUIComponents.AccentBlue))
                    {
                        PersonaGeneration.PromptLoader.CreateReadme();
                        Messages.Message("README 文件已生成", MessageTypeDefOf.PositiveEvent);
                    }
                    cy += 40f;
                    
                    Rect openBtnRect = new Rect(contentRect.x, cy, contentRect.width, 32f);
                    if (SettingsUIComponents.DrawButton(openBtnRect, "打开自定义提示词文件夹", SettingsUIComponents.AccentGreen))
                    {
                        PersonaGeneration.PromptLoader.OpenConfigFolder();
                    }
                });
                y += customPromptsHeight + SettingsUIComponents.MediumGap;
                
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

        private string GetExampleGlobalPrompt()
        {
            return @"# Global Instructions Example

## Language Style
- Use clear and concise language.
- Maintain a friendly yet professional attitude.
- Use idioms appropriately to add flavor.

## Behavioral Guidelines
- Prioritize the safety of colonists.
- Provide constructive suggestions and observations.
- Maintain calm analysis during crises.

## Response Characteristics
- Maintain appropriate professionalism.
- Occasionally show a sense of humor.
- Give practical advice.";
        }
    }
}