using UnityEngine;
using Verse;
using RimWorld;
using System;
using System.Linq;
using TheSecondSeat.TTS;

namespace TheSecondSeat.Settings
{
    public partial class TheSecondSeatMod
    {
        private void DrawTTSSettingsTab(Rect rect)
        {
            Vector2 scrollPos = tabManager.GetScrollPosition();
            float contentHeight = 700f;
            
            SettingsUIComponents.DrawScrollableCardContent(rect, ref scrollPos, contentHeight, (viewRect) =>
            {
                float y = viewRect.y + SettingsUIComponents.MediumGap;
                float cardWidth = viewRect.width - 10f;
                
                // === TTS 开关与提供商（紧凑合并） ===
                float headerHeight = 130f;
                Rect headerRect = new Rect(viewRect.x, y, cardWidth, headerHeight);
                SettingsUIComponents.DrawSettingsGroup(headerRect, "语音合成（TTS）", SettingsUIComponents.AccentPurple, (contentRect) =>
                {
                    float cy = contentRect.y;
                    float halfWidth = (contentRect.width - 10f) / 2f;
                    
                    // 启用开关和自动播放（同一行）
                    SettingsUIComponents.DrawToggleSetting(new Rect(contentRect.x, cy, halfWidth, 24f), 
                        "启用 TTS", "生成语音回复", ref Settings.enableTTS);
                    SettingsUIComponents.DrawToggleSetting(new Rect(contentRect.x + halfWidth + 10f, cy, halfWidth, 24f), 
                        "自动播放", "自动播放语音", ref Settings.autoPlayTTS);
                    cy += 28f;
                    
                    if (Settings.enableTTS)
                    {
                        // 提供商选择（紧凑按钮组）
                        Widgets.Label(new Rect(contentRect.x, cy, 60f, 24f), "提供商:");
                        string[] providers = { "edge", "azure", "local", "openai", "siliconflow" };
                        string[] names = { "Edge", "Azure", "本地", "OpenAI", "SiliconFlow" };
                        float btnWidth = (contentRect.width - 70f) / 5f;
                        for (int i = 0; i < providers.Length; i++)
                        {
                            bool isSelected = Settings.ttsProvider == providers[i];
                            Rect btnRect = new Rect(contentRect.x + 60f + i * btnWidth, cy, btnWidth - 2f, 22f);
                            if (isSelected) Widgets.DrawBoxSolid(btnRect, new Color(0.3f, 0.5f, 0.7f, 0.5f));
                            // fix: active 参数应始终为 true，否则未选中的按钮无法点击
                            if (Widgets.ButtonText(btnRect, names[i], true, true, true))
                                Settings.ttsProvider = providers[i];
                        }
                    }
                });
                y += headerHeight + SettingsUIComponents.SmallGap;
                
                if (!Settings.enableTTS)
                {
                    Rect disabledRect = new Rect(viewRect.x, y, cardWidth, 36f);
                    SettingsUIComponents.DrawInfoBox(disabledRect, "启用 TTS 后可配置语音合成选项", InfoBoxType.Info);
                    tabManager.SetScrollPosition(scrollPos);
                    return;
                }
                
                // === Edge TTS 说明（免费，无需配置）===
                if (Settings.ttsProvider == "edge")
                {
                    float edgeInfoHeight = 50f;
                    Rect edgeInfoRect = new Rect(viewRect.x, y, cardWidth, edgeInfoHeight);
                    SettingsUIComponents.DrawSettingsGroup(edgeInfoRect, "Edge TTS (免费)", SettingsUIComponents.AccentGreen, (contentRect) =>
                    {
                        GUI.color = new Color(0.7f, 1f, 0.7f);
                        Text.Font = GameFont.Small;
                        Widgets.Label(new Rect(contentRect.x, contentRect.y, contentRect.width, 20f), 
                            "✓ 免费使用，无需 API 密钥，直接选择语音即可使用");
                        GUI.color = Color.white;
                    });
                    y += edgeInfoHeight + SettingsUIComponents.SmallGap;
                }
                
                // === API 配置（根据提供商显示不同选项）===
                if (Settings.ttsProvider == "azure" || Settings.ttsProvider == "openai" || Settings.ttsProvider == "siliconflow")
                {
                    float apiHeight = Settings.ttsProvider == "siliconflow" ? 180f : 130f;
                    Rect apiRect = new Rect(viewRect.x, y, cardWidth, apiHeight);
                    SettingsUIComponents.DrawSettingsGroup(apiRect, "API 配置", SettingsUIComponents.AccentPurple, (contentRect) =>
                    {
                        float cy = contentRect.y;
                        
                        // API 密钥
                        SettingsUIComponents.DrawTextFieldSetting(new Rect(contentRect.x, cy, contentRect.width, 26f), 
                            "API 密钥", "API 访问密钥", ref Settings.ttsApiKey, true);
                        cy += 30f;
                        
                        if (Settings.ttsProvider == "azure")
                        {
                            // Azure 区域
                            SettingsUIComponents.DrawTextFieldSetting(new Rect(contentRect.x, cy, contentRect.width, 26f), 
                                "区域", "Azure 区域 (如 eastus)", ref Settings.ttsRegion);
                        }
                        else
                        {
                            // OpenAI / SiliconFlow 配置
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
                            
                            // API 端点和模型（同一行）
                            float halfW = (contentRect.width - 10f) / 2f;
                            SettingsUIComponents.DrawTextFieldSetting(new Rect(contentRect.x, cy, halfW, 26f),
                                "API 端点", "", ref Settings.ttsApiEndpoint);
                            SettingsUIComponents.DrawTextFieldSetting(new Rect(contentRect.x + halfW + 10f, cy, halfW, 26f),
                                "模型", "", ref Settings.ttsModelName);
                            cy += 30f;

                            // ⭐ v3.0.0: GPT-SoVITS 快速配置按钮
                            if (Settings.ttsProvider == "openai")
                            {
                                if (Widgets.ButtonText(new Rect(contentRect.x, cy, 180f, 24f), "使用 GPT-SoVITS 预设"))
                                {
                                    Settings.ttsApiEndpoint = "http://127.0.0.1:9880/v1/audio/speech";
                                    Settings.ttsModelName = "gpt-sovits"; // 或者是 GPT-SoVITS WebUI 默认的模型名
                                    Messages.Message("已应用 GPT-SoVITS 本地配置", MessageTypeDefOf.PositiveEvent, false);
                                }
                                cy += 30f;
                            }
                            
                            // SiliconFlow 特有：音色克隆 URI
                            if (Settings.ttsProvider == "siliconflow")
                            {
                                SettingsUIComponents.DrawTextFieldSetting(new Rect(contentRect.x, cy, contentRect.width, 26f), 
                                    "音色URI (克隆)", "上传音频到 SiliconFlow 获取的 audio_uri", ref Settings.ttsAudioUri);
                                cy += 30f;
                                
                                // 提示信息
                                Rect tipRect = new Rect(contentRect.x, cy, contentRect.width, 20f);
                                GUI.color = Color.gray;
                                Text.Font = GameFont.Tiny;
                                Widgets.Label(tipRect, "留空使用预设音色，填入 URI 使用克隆音色");
                                Text.Font = GameFont.Small;
                                GUI.color = Color.white;
                            }
                        }
                    });
                    y += apiHeight + SettingsUIComponents.SmallGap;
                }
                
                // === 语音参数（紧凑布局）===
                float voiceHeight = 120f;
                Rect voiceRect = new Rect(viewRect.x, y, cardWidth, voiceHeight);
                SettingsUIComponents.DrawSettingsGroup(voiceRect, "语音参数", SettingsUIComponents.AccentPurple, (contentRect) =>
                {
                    float cy = contentRect.y;
                    float halfWidth = (contentRect.width - 10f) / 2f;
                    
                    // 语音选择（根据提供商动态显示语音列表）
                    var voiceList = TTSService.GetAvailableVoices(Settings.ttsProvider).ToArray();
                    // 如果当前语音不在列表中，自动选择第一个
                    if (voiceList.Length > 0 && !voiceList.Contains(Settings.ttsVoice))
                    {
                        Settings.ttsVoice = voiceList[0];
                    }
                    SettingsUIComponents.DrawDropdownSetting(new Rect(contentRect.x, cy, contentRect.width, 26f), 
                        "语音", "选择 TTS 语音", Settings.ttsVoice, voiceList,
                        (selected) => Settings.ttsVoice = selected);
                    cy += 30f;
                    
                    // 语速和音量（同一行）
                    Widgets.Label(new Rect(contentRect.x, cy, 50f, 24f), $"语速:");
                    Settings.ttsSpeechRate = Widgets.HorizontalSlider(
                        new Rect(contentRect.x + 50f, cy + 4f, halfWidth - 80f, 16f),
                        Settings.ttsSpeechRate, 0.5f, 2f);
                    Widgets.Label(new Rect(contentRect.x + halfWidth - 25f, cy, 35f, 24f), $"{Settings.ttsSpeechRate:F2}");
                    
                    Widgets.Label(new Rect(contentRect.x + halfWidth + 10f, cy, 50f, 24f), $"音量:");
                    Settings.ttsVolume = Widgets.HorizontalSlider(
                        new Rect(contentRect.x + halfWidth + 60f, cy + 4f, halfWidth - 90f, 16f),
                        Settings.ttsVolume, 0f, 1f);
                    Widgets.Label(new Rect(contentRect.width - 25f, cy, 35f, 24f), $"{Settings.ttsVolume:P0}");
                    cy += 30f;

                    // ⭐ v3.0.0: 音高控制 (Pitch)
                    Widgets.Label(new Rect(contentRect.x, cy, 50f, 24f), $"音高:");
                    // 音高范围 -50% 到 +50% (Hz)
                    if (!Settings.ttsPitch.HasValue) Settings.ttsPitch = 0f;
                    float currentPitch = Settings.ttsPitch.Value;
                    float newPitch = Widgets.HorizontalSlider(
                        new Rect(contentRect.x + 50f, cy + 4f, contentRect.width - 90f, 16f),
                        currentPitch, -0.5f, 0.5f);
                    Settings.ttsPitch = newPitch;
                    
                    string pitchText = newPitch >= 0 ? $"+{newPitch:P0}" : $"{newPitch:P0}";
                    Widgets.Label(new Rect(contentRect.width - 35f, cy, 40f, 24f), pitchText);
                });
                y += voiceHeight + SettingsUIComponents.SmallGap;
                
                // === 操作按钮（紧凑行）===
                float buttonHeight = 40f;
                Rect buttonRect = new Rect(viewRect.x, y, cardWidth, buttonHeight);
                
                float btnW = (cardWidth - 10f) / 2f;
                if (SettingsUIComponents.DrawButton(new Rect(viewRect.x, y, btnW, 30f), "保存 TTS 设置", SettingsUIComponents.AccentGreen))
                {
                    SaveTTSSettings();
                }
                if (SettingsUIComponents.DrawButton(new Rect(viewRect.x + btnW + 10f, y, btnW, 30f), "测试 TTS", SettingsUIComponents.AccentPurple))
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
                // 配置 TTS 服务（包含 audioUri）
                TTSService.Instance.Configure(
                    Settings.ttsProvider,
                    Settings.ttsApiKey,
                    Settings.ttsRegion,
                    Settings.ttsVoice,
                    Settings.ttsSpeechRate,
                    Settings.ttsVolume,
                    Settings.ttsPitch ?? 0f, // ⭐ v3.0.0
                    Settings.ttsApiEndpoint,
                    Settings.ttsModelName,
                    Settings.ttsAudioUri
                );
                
                // 保存到磁盘
                Settings.Write();
                
                Log.Message($"[TTS Settings] Saved - Provider: {Settings.ttsProvider}, AudioUri: {(string.IsNullOrEmpty(Settings.ttsAudioUri) ? "none" : "set")}");
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

                // 临时应用当前 UI 设置进行测试，无需先保存
                TTSService.Instance.Configure(
                    Settings.ttsProvider,
                    Settings.ttsApiKey,
                    Settings.ttsRegion,
                    Settings.ttsVoice,
                    Settings.ttsSpeechRate,
                    Settings.ttsVolume,
                    Settings.ttsPitch ?? 0f, // ⭐ v3.0.0
                    Settings.ttsApiEndpoint,
                    Settings.ttsModelName,
                    Settings.ttsAudioUri
                );
                
                string testText = "你好，这是语音测试。";
                string filePath = await TTSService.Instance.SpeakAsync(testText);
                
                if (!string.IsNullOrEmpty(filePath))
                {
                    Messages.Message("TTS 测试成功！", MessageTypeDefOf.PositiveEvent);
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
