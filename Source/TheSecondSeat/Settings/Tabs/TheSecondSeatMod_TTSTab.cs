using UnityEngine;
using Verse;
using RimWorld;
using System;
using TheSecondSeat.TTS;

namespace TheSecondSeat.Settings
{
    public partial class TheSecondSeatMod
    {
        private void DrawTTSSettingsTab(Rect rect)
        {
            Vector2 scrollPos = tabManager.GetScrollPosition();
            float contentHeight = 650f;
            
            SettingsUIComponents.DrawScrollableCardContent(rect, ref scrollPos, contentHeight, (viewRect) =>
            {
                float y = viewRect.y + SettingsUIComponents.MediumGap;
                float cardWidth = viewRect.width - 10f;
                
                // === TTS 开关 ===
                float enableHeight = 100f;
                Rect enableRect = new Rect(viewRect.x, y, cardWidth, enableHeight);
                SettingsUIComponents.DrawSettingsGroup(enableRect, "语音合成（TTS）", SettingsUIComponents.AccentPurple, (contentRect) =>
                {
                    Rect toggleRect = new Rect(contentRect.x, contentRect.y, contentRect.width, 48f);
                    SettingsUIComponents.DrawCheckboxWithDescription(toggleRect, "启用语音合成", 
                        "AI 回复时生成语音", ref Settings.enableTTS);
                });
                y += enableHeight + SettingsUIComponents.MediumGap;
                
                if (!Settings.enableTTS)
                {
                    Rect disabledRect = new Rect(viewRect.x, y, cardWidth, 40f);
                    SettingsUIComponents.DrawInfoBox(disabledRect, "启用 TTS 后可配置语音合成选项", InfoBoxType.Info);
                    tabManager.SetScrollPosition(scrollPos);
                    return;
                }
                
                // === TTS 提供商 ===
                float providerHeight = 180f;
                Rect providerRect = new Rect(viewRect.x, y, cardWidth, providerHeight);
                SettingsUIComponents.DrawSettingsGroup(providerRect, "TTS 提供商", SettingsUIComponents.AccentPurple, (contentRect) =>
                {
                    float cy = contentRect.y;
                    
                    string[] providers = { "edge", "azure", "local", "openai", "siliconflow" };
                    string[] providerNames = { "Edge TTS (免费/在线)", "Azure TTS (高质量)", "本地 TTS (离线)", "OpenAI TTS", "SiliconFlow (IndexTTS)" };
                    
                    int currentIndex = Array.IndexOf(providers, Settings.ttsProvider);
                    if (currentIndex < 0) currentIndex = 0;
                    
                    Rect dropdownRect = new Rect(contentRect.x, cy, contentRect.width, 28f);
                    SettingsUIComponents.DrawDropdownSetting(dropdownRect, "服务提供商", 
                        "选择 TTS 服务", providerNames[currentIndex], providerNames, 
                        (selected) => {
                            int idx = Array.IndexOf(providerNames, selected);
                            if (idx >= 0) Settings.ttsProvider = providers[idx];
                        });
                    cy += 36f;
                    
                    // 提供商说明
                    string providerInfo = Settings.ttsProvider switch
                    {
                        "edge" => "使用微软 Edge 浏览器的在线语音服务，无需 API Key",
                        "azure" => "使用 Azure Speech Services，高质量但需要 API Key",
                        "local" => "使用 Windows 系统自带的 TTS，离线可用",
                        "openai" => "使用 OpenAI 兼容的 TTS API",
                        "siliconflow" => "使用 SiliconFlow API (支持 IndexTTS)",
                        _ => ""
                    };
                    
                    Rect infoRect = new Rect(contentRect.x, cy, contentRect.width, 36f);
                    SettingsUIComponents.DrawInfoBox(infoRect, providerInfo, InfoBoxType.Info);
                });
                y += providerHeight + SettingsUIComponents.MediumGap;
                
                // === Azure 配置（仅 Azure 提供商显示）===
                if (Settings.ttsProvider == "azure")
                {
                    float azureHeight = 150f;
                    Rect azureRect = new Rect(viewRect.x, y, cardWidth, azureHeight);
                    SettingsUIComponents.DrawSettingsGroup(azureRect, "Azure 配置", SettingsUIComponents.AccentPurple, (contentRect) =>
                    {
                        float cy = contentRect.y;
                        
                        Rect apiKeyRect = new Rect(contentRect.x, cy, contentRect.width, 28f);
                        SettingsUIComponents.DrawTextFieldSetting(apiKeyRect, "API 密钥", 
                            "Azure Speech Services API 密钥", ref Settings.ttsApiKey, true);
                        cy += 34f;
                        
                        Rect regionRect = new Rect(contentRect.x, cy, contentRect.width, 28f);
                        SettingsUIComponents.DrawTextFieldSetting(regionRect, "区域", 
                            "Azure 区域 (如: eastus, westeurope)", ref Settings.ttsRegion);
                    });
                    y += azureHeight + SettingsUIComponents.MediumGap;
                }
                // === OpenAI / SiliconFlow 配置 ===
                else if (Settings.ttsProvider == "openai" || Settings.ttsProvider == "siliconflow")
                {
                    float apiHeight = 220f;
                    Rect apiRect = new Rect(viewRect.x, y, cardWidth, apiHeight);
                    SettingsUIComponents.DrawSettingsGroup(apiRect, "API 配置", SettingsUIComponents.AccentPurple, (contentRect) =>
                    {
                        float cy = contentRect.y;
                        
                        Rect apiKeyRect = new Rect(contentRect.x, cy, contentRect.width, 28f);
                        SettingsUIComponents.DrawTextFieldSetting(apiKeyRect, "API 密钥",
                            "API 访问密钥", ref Settings.ttsApiKey, true);
                        cy += 34f;
                        
                        // 默认值填充
                        if (string.IsNullOrEmpty(Settings.ttsApiEndpoint))
                        {
                            Settings.ttsApiEndpoint = Settings.ttsProvider == "siliconflow"
                                ? "https://api.siliconflow.cn/v1/audio/speech"
                                : "https://api.openai.com/v1/audio/speech";
                        }
                        
                        if (string.IsNullOrEmpty(Settings.ttsModelName))
                        {
                            Settings.ttsModelName = Settings.ttsProvider == "siliconflow"
                                ? "IndexTeam/IndexTTS-2"
                                : "tts-1";
                        }

                        Rect endpointRect = new Rect(contentRect.x, cy, contentRect.width, 28f);
                        SettingsUIComponents.DrawTextFieldSetting(endpointRect, "API 端点",
                            "API URL", ref Settings.ttsApiEndpoint);
                        cy += 34f;

                        Rect modelRect = new Rect(contentRect.x, cy, contentRect.width, 28f);
                        SettingsUIComponents.DrawTextFieldSetting(modelRect, "模型名称",
                            "如 tts-1 或 IndexTeam/IndexTTS-2", ref Settings.ttsModelName);
                    });
                    y += apiHeight + SettingsUIComponents.MediumGap;
                }
                
                // === 语音参数 ===
                float voiceHeight = 200f;
                Rect voiceRect = new Rect(viewRect.x, y, cardWidth, voiceHeight);
                SettingsUIComponents.DrawSettingsGroup(voiceRect, "语音参数", SettingsUIComponents.AccentPurple, (contentRect) =>
                {
                    float cy = contentRect.y;
                    
                    // 语音选择
                    Rect voiceSelectRect = new Rect(contentRect.x, cy, contentRect.width, 28f);
                    SettingsUIComponents.DrawDropdownSetting(voiceSelectRect, "语音",
                        "选择 TTS 语音", Settings.ttsVoice, TTS.TTSService.GetAvailableVoices().ToArray(),
                        (selected) => Settings.ttsVoice = selected);
                    cy += 36f;
                    
                    // 语速
                    Rect speedRect = new Rect(contentRect.x, cy, contentRect.width, 28f);
                    SettingsUIComponents.DrawSliderSetting(speedRect, "语速", 
                        "语音播放速度", ref Settings.ttsSpeechRate, 0.5f, 2f, "F2");
                    cy += 34f;
                    
                    // 音量
                    Rect volumeRect = new Rect(contentRect.x, cy, contentRect.width, 28f);
                    float volumePercent = Settings.ttsVolume * 100f;
                    SettingsUIComponents.DrawSliderSetting(volumeRect, "音量", 
                        "语音音量", ref Settings.ttsVolume, 0f, 1f, "P0");
                    cy += 34f;
                    
                    // 自动播放
                    Rect autoPlayRect = new Rect(contentRect.x, cy, contentRect.width, 28f);
                    SettingsUIComponents.DrawToggleSetting(autoPlayRect, "自动播放", 
                        "AI 回复时自动播放语音", ref Settings.autoPlayTTS);
                });
                y += voiceHeight + SettingsUIComponents.MediumGap;
                
                // === 操作按钮 ===
                float buttonHeight = 80f;
                Rect buttonAreaRect = new Rect(viewRect.x, y, cardWidth, buttonHeight);
                
                // 保存 TTS 设置按钮
                Rect saveRect = new Rect(viewRect.x, y, cardWidth / 2 - 5, 32f);
                if (SettingsUIComponents.DrawButton(saveRect, "保存 TTS 设置", SettingsUIComponents.AccentGreen))
                {
                    SaveTTSSettings();
                }
                
                // 测试 TTS 按钮
                Rect testRect = new Rect(viewRect.x + cardWidth / 2 + 5, y, cardWidth / 2 - 5, 32f);
                if (SettingsUIComponents.DrawButton(testRect, "测试 TTS", SettingsUIComponents.AccentPurple))
                {
                    _ = TestTTSAsync();
                }
            });
            
            tabManager.SetScrollPosition(scrollPos);
        }
        
        /// <summary>
        /// 保存 TTS 设置并重新配置服务
        /// </summary>
        private void SaveTTSSettings()
        {
            try
            {
                // 配置 TTS 服务
                TTS.TTSService.Instance.Configure(
                    Settings.ttsProvider,
                    Settings.ttsApiKey,
                    Settings.ttsRegion,
                    Settings.ttsVoice,
                    Settings.ttsSpeechRate,
                    Settings.ttsVolume,
                    Settings.ttsApiEndpoint,
                    Settings.ttsModelName
                );
                
                // 保存到磁盘
                Settings.Write();
                
                Log.Message($"[TTS Settings] Saved - Provider: {Settings.ttsProvider}, Key: {(string.IsNullOrEmpty(Settings.ttsApiKey) ? "empty" : "***")}, Region: {Settings.ttsRegion}");
                Messages.Message("TTS 设置已保存并应用", MessageTypeDefOf.PositiveEvent);
            }
            catch (Exception ex)
            {
                Log.Error($"[TTS Settings] Save failed: {ex.Message}");
                Messages.Message($"保存 TTS 设置失败: {ex.Message}", MessageTypeDefOf.NegativeEvent);
            }
        }

        private async System.Threading.Tasks.Task TestTTSAsync()
        {
            try
            {
                Messages.Message("正在测试 TTS...", MessageTypeDefOf.NeutralEvent);
                
                string testText = "你好，这是语音测试。Hello, this is a voice test.";
                string filePath = await TTS.TTSService.Instance.SpeakAsync(testText);
                
                if (!string.IsNullOrEmpty(filePath))
                {
                    Messages.Message("TTS 测试成功！音频文件已保存。", MessageTypeDefOf.PositiveEvent);
                }
                else
                {
                    Messages.Message("TTS 测试失败", MessageTypeDefOf.NegativeEvent);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[ModSettings] TestTTS failed: {ex.Message}");
                Messages.Message($"TTS 测试失败: {ex.Message}", MessageTypeDefOf.NegativeEvent);
            }
        }
    }
}