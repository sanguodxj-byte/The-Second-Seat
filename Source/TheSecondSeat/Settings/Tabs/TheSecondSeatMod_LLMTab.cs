using UnityEngine;
using Verse;
using RimWorld;
using System;

namespace TheSecondSeat.Settings
{
    public partial class TheSecondSeatMod
    {
        private void DrawLLMSettingsTab(Rect rect)
        {
            Vector2 scrollPos = tabManager.GetScrollPosition();
            float contentHeight = 700f;
            
            SettingsUIComponents.DrawScrollableCardContent(rect, ref scrollPos, contentHeight, (viewRect) =>
            {
                float y = viewRect.y + SettingsUIComponents.MediumGap;
                float cardWidth = viewRect.width - 10f;
                
                // === LLM 提供商选择 ===
                float providerHeight = 180f;
                Rect providerRect = new Rect(viewRect.x, y, cardWidth, providerHeight);
                SettingsUIComponents.DrawSettingsGroup(providerRect, "LLM 提供商", SettingsUIComponents.AccentGreen, (contentRect) =>
                {
                    float cy = contentRect.y;
                    
                    string[] providers = { "local", "openai", "deepseek", "gemini" };
                    string[] providerNames = { "本地模型 (LM Studio)", "OpenAI", "DeepSeek", "Google Gemini" };
                    
                    int currentIndex = Array.IndexOf(providers, Settings.llmProvider);
                    if (currentIndex < 0) currentIndex = 0;
                    
                    Rect dropdownRect = new Rect(contentRect.x, cy, contentRect.width, 28f);
                    SettingsUIComponents.DrawDropdownSetting(dropdownRect, "服务提供商", 
                        "选择 LLM API 提供商", providerNames[currentIndex], providerNames, 
                        (selected) => {
                            int idx = Array.IndexOf(providerNames, selected);
                            if (idx >= 0) Settings.llmProvider = providers[idx];
                        });
                    cy += 36f;
                    
                    // 提供商说明
                    string providerInfo = Settings.llmProvider switch
                    {
                        "local" => "使用本地 LM Studio 或兼容的 OpenAI API 服务器",
                        "openai" => "使用 OpenAI 官方 API（需要 API Key）",
                        "deepseek" => "使用 DeepSeek API（性价比高）",
                        "gemini" => "使用 Google Gemini API",
                        _ => ""
                    };
                    
                    Rect infoRect = new Rect(contentRect.x, cy, contentRect.width, 36f);
                    SettingsUIComponents.DrawInfoBox(infoRect, providerInfo, InfoBoxType.Info);
                });
                y += providerHeight + SettingsUIComponents.MediumGap;
                
                // === API 配置 ===
                float apiConfigHeight = 200f;
                Rect apiRect = new Rect(viewRect.x, y, cardWidth, apiConfigHeight);
                SettingsUIComponents.DrawSettingsGroup(apiRect, "API 配置", SettingsUIComponents.AccentGreen, (contentRect) =>
                {
                    float cy = contentRect.y;
                    
                    // API 端点
                    Rect endpointRect = new Rect(contentRect.x, cy, contentRect.width, 28f);
                    SettingsUIComponents.DrawTextFieldSetting(endpointRect, "API 端点", 
                        "LLM API 服务器地址", ref Settings.apiEndpoint);
                    cy += 34f;
                    
                    // API 密钥
                    Rect apiKeyRect = new Rect(contentRect.x, cy, contentRect.width, 28f);
                    SettingsUIComponents.DrawTextFieldSetting(apiKeyRect, "API 密钥", 
                        "API 访问密钥（本地服务可留空）", ref Settings.apiKey, true);
                    cy += 34f;
                    
                    // 模型名称
                    Rect modelRect = new Rect(contentRect.x, cy, contentRect.width, 28f);
                    SettingsUIComponents.DrawTextFieldSetting(modelRect, "模型名称", 
                        "要使用的模型 ID", ref Settings.modelName);
                });
                y += apiConfigHeight + SettingsUIComponents.MediumGap;
                
                // === 生成参数 ===
                float paramsHeight = 160f;
                Rect paramsRect = new Rect(viewRect.x, y, cardWidth, paramsHeight);
                SettingsUIComponents.DrawSettingsGroup(paramsRect, "生成参数", SettingsUIComponents.AccentGreen, (contentRect) =>
                {
                    float cy = contentRect.y;
                    
                    // 温度
                    Rect tempRect = new Rect(contentRect.x, cy, contentRect.width, 28f);
                    SettingsUIComponents.DrawSliderSetting(tempRect, "温度", 
                        "控制回复的随机性 (0=确定性, 2=创造性)", ref Settings.temperature, 0f, 2f, "F2");
                    cy += 34f;
                    
                    // 最大 Token
                    Rect tokensRect = new Rect(contentRect.x, cy, contentRect.width, 28f);
                    SettingsUIComponents.DrawIntFieldSetting(tokensRect, "最大 Token", 
                        "单次回复的最大长度", ref Settings.maxTokens, 100, 4000);
                });
                y += paramsHeight + SettingsUIComponents.MediumGap;
                
                // === 操作按钮 ===
                float buttonAreaHeight = 50f;
                Rect buttonRect = new Rect(viewRect.x, y, cardWidth, buttonAreaHeight);
                SettingsUIComponents.DrawButtonGroup(buttonRect,
                    ("应用配置", SettingsUIComponents.AccentBlue, () => {
                        LLM.LLMService.Instance.Configure(
                            Settings.apiEndpoint,
                            Settings.apiKey,
                            Settings.modelName,
                            Settings.llmProvider
                        );
                        Messages.Message("LLM 配置已应用", MessageTypeDefOf.PositiveEvent);
                    }),
                    ("测试连接", SettingsUIComponents.AccentGreen, () => {
                        _ = TestConnectionAsync();
                    })
                );
            });
            
            tabManager.SetScrollPosition(scrollPos);
        }
    }
}