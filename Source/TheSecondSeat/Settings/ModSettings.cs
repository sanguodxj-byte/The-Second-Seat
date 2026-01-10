using UnityEngine;
using Verse;
using RimWorld;
using System;
using TheSecondSeat.WebSearch;

namespace TheSecondSeat.Settings
{
    /// <summary>
    /// Mod settings for The Second Seat
    /// </summary>
    public class TheSecondSeatSettings : ModSettings
    {
        // LLM Settings
        public string llmProvider = "local"; // "local", "openai", "deepseek", "gemini"
        public string apiEndpoint = "http://localhost:1234/v1/chat/completions";
        public string apiKey = "";
        public string modelName = "local-model";
        public float temperature = 0.7f;
        public int maxTokens = 500;

        // Web Search Settings
        public bool enableWebSearch = false;
        public string searchEngine = "duckduckgo"; // "bing", "google", "duckduckgo"
        public string bingApiKey = "";
        public string googleApiKey = "";
        public string googleSearchEngineId = "";
        public int searchDelayMs = 1000;

        // Multimodal Analysis Settings
        public bool enableMultimodalAnalysis = false;
        public string multimodalProvider = "openai"; // "openai", "deepseek", "gemini"
        public string multimodalApiKey = "";
        public string visionModel = "";
        public string textAnalysisModel = "";

        // TTS Settings
        public bool enableTTS = false;
        public string ttsProvider = "edge"; // "azure", "edge", "local"
        public string ttsApiKey = "";
        public string ttsRegion = "eastus";
        public string ttsVoice = "zh-CN-XiaoxiaoNeural";
        public float ttsSpeechRate = 1.0f;
        public float ttsVolume = 1.0f;
        public bool autoPlayTTS = false;
        
        // RimAgent 设置
        public string agentName = "main-narrator";
        public int maxRetries = 3;
        public float retryDelay = 2f;
        public int maxHistoryMessages = 20;
        public System.Collections.Generic.Dictionary<string, bool> toolsEnabled = new System.Collections.Generic.Dictionary<string, bool>();
        
        // 并发管理设置
        public int maxConcurrent = 5;
        public int requestTimeout = 60;
        public bool enableRetry = true;

        // UI States - 选项卡索引
        public int currentSettingsTab = 0;

        // 精简提示词
        public bool useCompactPrompt = true;
        
        // UI Button Position
        public float buttonPositionX = -1f;
        public float buttonPositionY = -1f;
        
        // 立绘模式设置
        public bool usePortraitMode = false;

        // Debug
        public bool debugMode = false;

        // 好感度系统
        public bool enableAffinitySystem = true;
        
        // 难度模式
        public PersonaGeneration.AIDifficultyMode difficultyMode = PersonaGeneration.AIDifficultyMode.Assistant;
        
        // 全局提示词
        public string globalPrompt = "";
        
        // 主动对话设置
        public bool enableProactiveDialogue = true;

        public override void ExposeData()
        {
            base.ExposeData();
            
            // LLM
            Scribe_Values.Look(ref llmProvider, "llmProvider", "local");
            Scribe_Values.Look(ref apiEndpoint, "apiEndpoint", "http://localhost:1234/v1/chat/completions");
            Scribe_Values.Look(ref apiKey, "apiKey", "");
            Scribe_Values.Look(ref modelName, "modelName", "local-model");
            Scribe_Values.Look(ref temperature, "temperature", 0.7f);
            Scribe_Values.Look(ref maxTokens, "maxTokens", 500);
            
            // Web Search
            Scribe_Values.Look(ref enableWebSearch, "enableWebSearch", false);
            Scribe_Values.Look(ref searchEngine, "searchEngine", "duckduckgo");
            Scribe_Values.Look(ref bingApiKey, "bingApiKey", "");
            Scribe_Values.Look(ref googleApiKey, "googleApiKey", "");
            Scribe_Values.Look(ref googleSearchEngineId, "googleSearchEngineId", "");
            Scribe_Values.Look(ref searchDelayMs, "searchDelayMs", 1000);
            
            // Multimodal Analysis
            Scribe_Values.Look(ref enableMultimodalAnalysis, "enableMultimodalAnalysis", false);
            Scribe_Values.Look(ref multimodalProvider, "multimodalProvider", "openai");
            Scribe_Values.Look(ref multimodalApiKey, "multimodalApiKey", "");
            Scribe_Values.Look(ref visionModel, "visionModel", "");
            Scribe_Values.Look(ref textAnalysisModel, "textAnalysisModel", "");
            
            // TTS
            Scribe_Values.Look(ref enableTTS, "enableTTS", false);
            Scribe_Values.Look(ref ttsProvider, "ttsProvider", "edge");
            Scribe_Values.Look(ref ttsApiKey, "ttsApiKey", "");
            Scribe_Values.Look(ref ttsRegion, "ttsRegion", "eastus");
            Scribe_Values.Look(ref ttsVoice, "ttsVoice", "zh-CN-XiaoxiaoNeural");
            Scribe_Values.Look(ref ttsSpeechRate, "ttsSpeechRate", 1.0f);
            Scribe_Values.Look(ref ttsVolume, "ttsVolume", 1.0f);
            Scribe_Values.Look(ref autoPlayTTS, "autoPlayTTS", false);
            
            // RimAgent 设置
            Scribe_Values.Look(ref agentName, "agentName", "main-narrator");
            Scribe_Values.Look(ref maxRetries, "maxRetries", 3);
            Scribe_Values.Look(ref retryDelay, "retryDelay", 2f);
            Scribe_Values.Look(ref maxHistoryMessages, "maxHistoryMessages", 20);
            Scribe_Collections.Look(ref toolsEnabled, "toolsEnabled", LookMode.Value, LookMode.Value);
            
            // 并发管理设置
            Scribe_Values.Look(ref maxConcurrent, "maxConcurrent", 5);
            Scribe_Values.Look(ref requestTimeout, "requestTimeout", 60);
            Scribe_Values.Look(ref enableRetry, "enableRetry", true);

            // 选项卡状态
            Scribe_Values.Look(ref currentSettingsTab, "currentSettingsTab", 0);
            
            // 精简提示词选项
            Scribe_Values.Look(ref useCompactPrompt, "useCompactPrompt", true);
            
            // UI Button Position
            Scribe_Values.Look(ref buttonPositionX, "buttonPositionX", -1f);
            Scribe_Values.Look(ref buttonPositionY, "buttonPositionY", -1f);
            
            // 立绘模式设置
            Scribe_Values.Look(ref usePortraitMode, "usePortraitMode", false);
            
            // Debug
            Scribe_Values.Look(ref debugMode, "debugMode", false);

            // 好感度系统
            Scribe_Values.Look(ref enableAffinitySystem, "enableAffinitySystem", true);
            
            // 难度模式
            Scribe_Values.Look(ref difficultyMode, "difficultyMode", PersonaGeneration.AIDifficultyMode.Assistant);
            
            // 全局提示词
            Scribe_Values.Look(ref globalPrompt, "globalPrompt", "");
            
            // 主动对话设置
            Scribe_Values.Look(ref enableProactiveDialogue, "enableProactiveDialogue", true);
        }
    }

    /// <summary>
    /// Mod instance for accessing settings - 使用选项卡布局的现代化设置界面
    /// </summary>
    public class TheSecondSeatMod : Mod
    {
        private TheSecondSeatSettings settings;
        private TabManager tabManager;
        private bool tabsInitialized = false;
        
        // 难度图标
        private Texture2D assistantModeIcon;
        private Texture2D opponentModeIcon;
        private Texture2D engineerModeIcon;

        public TheSecondSeatMod(ModContentPack content) : base(content)
        {
            this.settings = GetSettings<TheSecondSeatSettings>();
            
            // 初始化 LLM 服务
            LLM.LLMService.Instance.Configure(
                settings.apiEndpoint,
                settings.apiKey,
                settings.modelName,
                settings.llmProvider
            );

            // 初始化网络搜索
            if (settings.enableWebSearch)
            {
                ConfigureWebSearch();
            }

            // 初始化多模态分析
            if (settings.enableMultimodalAnalysis)
            {
                ConfigureMultimodalAnalysis();
            }

            // 初始化 TTS 服务
            if (settings.enableTTS)
            {
                ConfigureTTS();
            }
        }

        private void ConfigureWebSearch()
        {
            string apiKey = settings.searchEngine.ToLower() switch
            {
                "bing" => settings.bingApiKey,
                "google" => settings.googleApiKey,
                _ => null
            };

            WebSearchService.Instance.Configure(
                settings.searchEngine,
                apiKey,
                settings.googleSearchEngineId
            );

            Log.Message($"[The Second Seat] Web search configured: {settings.searchEngine}");
        }

        private void ConfigureMultimodalAnalysis()
        {
            try
            {
                PersonaGeneration.MultimodalAnalysisService.Instance.Configure(
                    settings.multimodalProvider,
                    settings.multimodalApiKey,
                    settings.visionModel,
                    settings.textAnalysisModel
                );
                
                Log.Message($"[The Second Seat] Multimodal analysis configured: {settings.multimodalProvider}");
            }
            catch (Exception ex)
            {
                Log.Error($"[The Second Seat] Multimodal analysis config failed: {ex.Message}");
            }
        }

        private void ConfigureTTS()
        {
            try
            {
                TTS.TTSService.Instance.Configure(
                    settings.ttsProvider,
                    settings.ttsApiKey,
                    settings.ttsRegion,
                    settings.ttsVoice,
                    settings.ttsSpeechRate,
                    settings.ttsVolume
                );
                
                Log.Message($"[The Second Seat] TTS configured: {settings.ttsProvider}");
            }
            catch (Exception ex)
            {
                Log.Error($"[The Second Seat] TTS config failed: {ex.Message}");
            }
        }

        private void LoadDifficultyIcons()
        {
            if (assistantModeIcon == null)
            {
                assistantModeIcon = ContentFinder<Texture2D>.Get("UI/DifficultyMode/assistant_large", false);
            }
            if (opponentModeIcon == null)
            {
                opponentModeIcon = ContentFinder<Texture2D>.Get("UI/DifficultyMode/opponent_large", false);
            }
            if (engineerModeIcon == null)
            {
                engineerModeIcon = ContentFinder<Texture2D>.Get("UI/DifficultyMode/engineer_large", false);
            }
        }

        /// <summary>
        /// 初始化选项卡
        /// </summary>
        private void InitializeTabs()
        {
            if (tabsInitialized) return;
            
            tabManager = new TabManager();
            tabManager.CurrentTab = settings.currentSettingsTab;
            
            // 基础设置选项卡
            tabManager.AddTab("基础设置", "[*]", SettingsUIComponents.AccentBlue, DrawBasicSettingsTab);
            
            // LLM 配置选项卡
            tabManager.AddTab("LLM配置", "[A]", SettingsUIComponents.AccentGreen, DrawLLMSettingsTab);
            
            // TTS 配置选项卡
            tabManager.AddTab("TTS配置", "[S]", SettingsUIComponents.AccentPurple, DrawTTSSettingsTab);
            
            // 高级选项选项卡
            tabManager.AddTab("高级选项", "[+]", SettingsUIComponents.AccentOrange, DrawAdvancedSettingsTab);
            
            tabsInitialized = true;
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            LoadDifficultyIcons();
            InitializeTabs();
            
            // 使用选项卡界面
            SettingsUIComponents.DrawTabbedInterface(inRect, tabManager, 42f);
            
            // 保存当前选项卡索引
            settings.currentSettingsTab = tabManager.CurrentTab;
            
            base.DoSettingsWindowContents(inRect);
        }

        #region 基础设置选项卡

        private void DrawBasicSettingsTab(Rect rect)
        {
            Vector2 scrollPos = tabManager.GetScrollPosition();
            float contentHeight = 900f;
            
            SettingsUIComponents.DrawScrollableCardContent(rect, ref scrollPos, contentHeight, (viewRect) =>
            {
                float y = viewRect.y + SettingsUIComponents.MediumGap;
                float cardWidth = viewRect.width - 10f;
                
                // === 调试与核心设置 ===
                float coreSettingsHeight = 180f;
                Rect coreRect = new Rect(viewRect.x, y, cardWidth, coreSettingsHeight);
                SettingsUIComponents.DrawSettingsGroup(coreRect, "核心设置", SettingsUIComponents.AccentBlue, (contentRect) =>
                {
                    float cy = contentRect.y;
                    
                    // 调试模式
                    Rect debugRect = new Rect(contentRect.x, cy, contentRect.width, 48f);
                    SettingsUIComponents.DrawCheckboxWithDescription(debugRect, "调试模式", 
                        "启用详细日志输出，用于排查问题", ref settings.debugMode);
                    cy += 52f;
                    
                    // 好感度系统
                    Rect affinityRect = new Rect(contentRect.x, cy, contentRect.width, 48f);
                    SettingsUIComponents.DrawCheckboxWithDescription(affinityRect, "启用好感度系统", 
                        "跟踪与叙事者的互动和关系", ref settings.enableAffinitySystem);
                    cy += 52f;
                    
                    if (!settings.enableAffinitySystem)
                    {
                        Rect warnRect = new Rect(contentRect.x, cy, contentRect.width, 30f);
                        SettingsUIComponents.DrawInfoBox(warnRect, "禁用好感度系统可能影响对话个性化", InfoBoxType.Warning);
                    }
                });
                y += coreSettingsHeight + SettingsUIComponents.MediumGap;
                
                // === 显示设置 ===
                float displaySettingsHeight = 150f;
                Rect displayRect = new Rect(viewRect.x, y, cardWidth, displaySettingsHeight);
                SettingsUIComponents.DrawSettingsGroup(displayRect, "显示设置", SettingsUIComponents.AccentBlue, (contentRect) =>
                {
                    float cy = contentRect.y;
                    
                    // 立绘模式
                    Rect portraitRect = new Rect(contentRect.x, cy, contentRect.width, 48f);
                    SettingsUIComponents.DrawCheckboxWithDescription(portraitRect, "使用立绘模式（1024x1572 全身立绘）", 
                        settings.usePortraitMode ? "当前: 全身立绘显示" : "当前: 头像模式 (512x512)", 
                        ref settings.usePortraitMode);
                    cy += 52f;
                    
                    // 精简提示词
                    Rect compactRect = new Rect(contentRect.x, cy, contentRect.width, 48f);
                    SettingsUIComponents.DrawCheckboxWithDescription(compactRect, "使用精简提示词", 
                        settings.useCompactPrompt ? "约500 tokens，响应更快" : "约5000 tokens，更详细", 
                        ref settings.useCompactPrompt);
                });
                y += displaySettingsHeight + SettingsUIComponents.MediumGap;
                
                // === 对话设置 ===
                float dialogSettingsHeight = 100f;
                Rect dialogRect = new Rect(viewRect.x, y, cardWidth, dialogSettingsHeight);
                SettingsUIComponents.DrawSettingsGroup(dialogRect, "对话设置", SettingsUIComponents.AccentBlue, (contentRect) =>
                {
                    float cy = contentRect.y;
                    
                    // 主动对话
                    Rect proactiveRect = new Rect(contentRect.x, cy, contentRect.width, 48f);
                    SettingsUIComponents.DrawCheckboxWithDescription(proactiveRect, "启用主动对话", 
                        "空闲5分钟/重要事件时自动发起对话", ref settings.enableProactiveDialogue);
                });
                y += dialogSettingsHeight + SettingsUIComponents.MediumGap;
                
                // === 难度模式选择 ===
                float difficultyHeight = 200f;
                Rect difficultyRect = new Rect(viewRect.x, y, cardWidth, difficultyHeight);
                SettingsUIComponents.DrawSettingsGroup(difficultyRect, "难度模式", SettingsUIComponents.AccentYellow, (contentRect) =>
                {
                    float cardSpacing = 12f;
                    float modeCardWidth = (contentRect.width - (cardSpacing * 2)) / 3f;
                    float modeCardHeight = 140f;
                    
                    // 助手模式卡片
                    Rect assistantCardRect = new Rect(contentRect.x, contentRect.y, modeCardWidth, modeCardHeight);
                    if (SettingsUIComponents.DrawModeCard(assistantCardRect, "助手", "无条件支持",
                        "主动建议、协助管理\n成为你的得力助手",
                        settings.difficultyMode == PersonaGeneration.AIDifficultyMode.Assistant,
                        SettingsUIComponents.AccentGreen, assistantModeIcon))
                    {
                        settings.difficultyMode = PersonaGeneration.AIDifficultyMode.Assistant;
                    }
                    
                    // 奕者模式卡片
                    Rect opponentCardRect = new Rect(contentRect.x + modeCardWidth + cardSpacing, contentRect.y, modeCardWidth, modeCardHeight);
                    if (SettingsUIComponents.DrawModeCard(opponentCardRect, "奕者", "挑战平衡",
                        "事件控制、难度调整\n与你展开智慧博弈",
                        settings.difficultyMode == PersonaGeneration.AIDifficultyMode.Opponent,
                        SettingsUIComponents.AccentRed, opponentModeIcon))
                    {
                        settings.difficultyMode = PersonaGeneration.AIDifficultyMode.Opponent;
                    }

                    // 工程师模式卡片
                    Rect engineerCardRect = new Rect(contentRect.x + (modeCardWidth + cardSpacing) * 2, contentRect.y, modeCardWidth, modeCardHeight);
                    if (SettingsUIComponents.DrawModeCard(engineerCardRect, "工程师", "技术支持",
                        "日志诊断、排错修复\n专业的Mod技术顾问",
                        settings.difficultyMode == PersonaGeneration.AIDifficultyMode.Engineer,
                        SettingsUIComponents.AccentBlue, engineerModeIcon))
                    {
                        settings.difficultyMode = PersonaGeneration.AIDifficultyMode.Engineer;
                    }
                });
                y += difficultyHeight + SettingsUIComponents.MediumGap;
            });
            
            tabManager.SetScrollPosition(scrollPos);
        }

        #endregion

        #region LLM 配置选项卡

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
                    
                    int currentIndex = Array.IndexOf(providers, settings.llmProvider);
                    if (currentIndex < 0) currentIndex = 0;
                    
                    Rect dropdownRect = new Rect(contentRect.x, cy, contentRect.width, 28f);
                    SettingsUIComponents.DrawDropdownSetting(dropdownRect, "服务提供商", 
                        "选择 LLM API 提供商", providerNames[currentIndex], providerNames, 
                        (selected) => {
                            int idx = Array.IndexOf(providerNames, selected);
                            if (idx >= 0) settings.llmProvider = providers[idx];
                        });
                    cy += 36f;
                    
                    // 提供商说明
                    string providerInfo = settings.llmProvider switch
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
                        "LLM API 服务器地址", ref settings.apiEndpoint);
                    cy += 34f;
                    
                    // API 密钥
                    Rect apiKeyRect = new Rect(contentRect.x, cy, contentRect.width, 28f);
                    SettingsUIComponents.DrawTextFieldSetting(apiKeyRect, "API 密钥", 
                        "API 访问密钥（本地服务可留空）", ref settings.apiKey, true);
                    cy += 34f;
                    
                    // 模型名称
                    Rect modelRect = new Rect(contentRect.x, cy, contentRect.width, 28f);
                    SettingsUIComponents.DrawTextFieldSetting(modelRect, "模型名称", 
                        "要使用的模型 ID", ref settings.modelName);
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
                        "控制回复的随机性 (0=确定性, 2=创造性)", ref settings.temperature, 0f, 2f, "F2");
                    cy += 34f;
                    
                    // 最大 Token
                    Rect tokensRect = new Rect(contentRect.x, cy, contentRect.width, 28f);
                    SettingsUIComponents.DrawIntFieldSetting(tokensRect, "最大 Token", 
                        "单次回复的最大长度", ref settings.maxTokens, 100, 4000);
                });
                y += paramsHeight + SettingsUIComponents.MediumGap;
                
                // === 操作按钮 ===
                float buttonAreaHeight = 50f;
                Rect buttonRect = new Rect(viewRect.x, y, cardWidth, buttonAreaHeight);
                SettingsUIComponents.DrawButtonGroup(buttonRect,
                    ("应用配置", SettingsUIComponents.AccentBlue, () => {
                        LLM.LLMService.Instance.Configure(
                            settings.apiEndpoint,
                            settings.apiKey,
                            settings.modelName,
                            settings.llmProvider
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

        #endregion

        #region TTS 配置选项卡

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
                        "AI 回复时生成语音", ref settings.enableTTS);
                });
                y += enableHeight + SettingsUIComponents.MediumGap;
                
                if (!settings.enableTTS)
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
                    
                    string[] providers = { "edge", "azure", "local" };
                    string[] providerNames = { "Edge TTS (免费/在线)", "Azure TTS (高质量)", "本地 TTS (离线)" };
                    
                    int currentIndex = Array.IndexOf(providers, settings.ttsProvider);
                    if (currentIndex < 0) currentIndex = 0;
                    
                    Rect dropdownRect = new Rect(contentRect.x, cy, contentRect.width, 28f);
                    SettingsUIComponents.DrawDropdownSetting(dropdownRect, "服务提供商", 
                        "选择 TTS 服务", providerNames[currentIndex], providerNames, 
                        (selected) => {
                            int idx = Array.IndexOf(providerNames, selected);
                            if (idx >= 0) settings.ttsProvider = providers[idx];
                        });
                    cy += 36f;
                    
                    // 提供商说明
                    string providerInfo = settings.ttsProvider switch
                    {
                        "edge" => "使用微软 Edge 浏览器的在线语音服务，无需 API Key",
                        "azure" => "使用 Azure Speech Services，高质量但需要 API Key",
                        "local" => "使用 Windows 系统自带的 TTS，离线可用",
                        _ => ""
                    };
                    
                    Rect infoRect = new Rect(contentRect.x, cy, contentRect.width, 36f);
                    SettingsUIComponents.DrawInfoBox(infoRect, providerInfo, InfoBoxType.Info);
                });
                y += providerHeight + SettingsUIComponents.MediumGap;
                
                // === Azure 配置（仅 Azure 提供商显示）===
                if (settings.ttsProvider == "azure")
                {
                    float azureHeight = 150f;
                    Rect azureRect = new Rect(viewRect.x, y, cardWidth, azureHeight);
                    SettingsUIComponents.DrawSettingsGroup(azureRect, "Azure 配置", SettingsUIComponents.AccentPurple, (contentRect) =>
                    {
                        float cy = contentRect.y;
                        
                        Rect apiKeyRect = new Rect(contentRect.x, cy, contentRect.width, 28f);
                        SettingsUIComponents.DrawTextFieldSetting(apiKeyRect, "API 密钥", 
                            "Azure Speech Services API 密钥", ref settings.ttsApiKey, true);
                        cy += 34f;
                        
                        Rect regionRect = new Rect(contentRect.x, cy, contentRect.width, 28f);
                        SettingsUIComponents.DrawTextFieldSetting(regionRect, "区域", 
                            "Azure 区域 (如: eastus, westeurope)", ref settings.ttsRegion);
                    });
                    y += azureHeight + SettingsUIComponents.MediumGap;
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
                        "选择 TTS 语音", settings.ttsVoice, TTS.TTSService.GetAvailableVoices().ToArray(),
                        (selected) => settings.ttsVoice = selected);
                    cy += 36f;
                    
                    // 语速
                    Rect speedRect = new Rect(contentRect.x, cy, contentRect.width, 28f);
                    SettingsUIComponents.DrawSliderSetting(speedRect, "语速", 
                        "语音播放速度", ref settings.ttsSpeechRate, 0.5f, 2f, "F2");
                    cy += 34f;
                    
                    // 音量
                    Rect volumeRect = new Rect(contentRect.x, cy, contentRect.width, 28f);
                    float volumePercent = settings.ttsVolume * 100f;
                    SettingsUIComponents.DrawSliderSetting(volumeRect, "音量", 
                        "语音音量", ref settings.ttsVolume, 0f, 1f, "P0");
                    cy += 34f;
                    
                    // 自动播放
                    Rect autoPlayRect = new Rect(contentRect.x, cy, contentRect.width, 28f);
                    SettingsUIComponents.DrawToggleSetting(autoPlayRect, "自动播放", 
                        "AI 回复时自动播放语音", ref settings.autoPlayTTS);
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
                    settings.ttsProvider,
                    settings.ttsApiKey,
                    settings.ttsRegion,
                    settings.ttsVoice,
                    settings.ttsSpeechRate,
                    settings.ttsVolume
                );
                
                // 保存到磁盘
                settings.Write();
                
                Log.Message($"[TTS Settings] Saved - Provider: {settings.ttsProvider}, Key: {(string.IsNullOrEmpty(settings.ttsApiKey) ? "empty" : "***")}, Region: {settings.ttsRegion}");
                Messages.Message("TTS 设置已保存并应用", MessageTypeDefOf.PositiveEvent);
            }
            catch (Exception ex)
            {
                Log.Error($"[TTS Settings] Save failed: {ex.Message}");
                Messages.Message($"保存 TTS 设置失败: {ex.Message}", MessageTypeDefOf.NegativeEvent);
            }
        }

        #endregion

        #region 高级选项选项卡

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
                        "允许 AI 搜索网络获取信息", ref settings.enableWebSearch);
                    cy += 34f;
                    
                    if (settings.enableWebSearch)
                    {
                        // 搜索引擎选择
                        string[] engines = { "duckduckgo", "bing", "google" };
                        string[] engineNames = { "DuckDuckGo (免费)", "Bing (需API)", "Google (需API)" };
                        int currentIndex = Array.IndexOf(engines, settings.searchEngine);
                        if (currentIndex < 0) currentIndex = 0;
                        
                        Rect engineRect = new Rect(contentRect.x, cy, contentRect.width, 28f);
                        SettingsUIComponents.DrawDropdownSetting(engineRect, "搜索引擎", 
                            "选择搜索服务", engineNames[currentIndex], engineNames, 
                            (selected) => {
                                int idx = Array.IndexOf(engineNames, selected);
                                if (idx >= 0) settings.searchEngine = engines[idx];
                            });
                        cy += 36f;
                        
                        // 搜索延迟
                        float delayFloat = settings.searchDelayMs;
                        Rect delayRect = new Rect(contentRect.x, cy, contentRect.width, 28f);
                        SettingsUIComponents.DrawSliderSetting(delayRect, "搜索延迟 (ms)", 
                            "搜索请求间隔", ref delayFloat, 0f, 5000f, "F0");
                        settings.searchDelayMs = (int)delayFloat;
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
                    SettingsUIComponents.DrawTextAreaSetting(textAreaRect, "", null, ref settings.globalPrompt, 100f);
                    cy += 110f;
                    
                    Rect exampleRect = new Rect(contentRect.x, cy, contentRect.width, 28f);
                    if (SettingsUIComponents.DrawButton(exampleRect, "加载示例提示词", SettingsUIComponents.AccentBlue))
                    {
                        settings.globalPrompt = GetExampleGlobalPrompt();
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
                if (settings.enableWebSearch && SettingsUIComponents.DrawButton(clearCacheRect, "清除搜索缓存", SettingsUIComponents.AccentYellow))
                {
                    WebSearchService.Instance.ClearCache();
                    Messages.Message("搜索缓存已清除", MessageTypeDefOf.NeutralEvent);
                }
            });
            
            tabManager.SetScrollPosition(scrollPos);
        }

        #endregion

        #region 辅助方法

        private void ApplyAllSettings()
        {
            LLM.LLMService.Instance.Configure(
                settings.apiEndpoint,
                settings.apiKey,
                settings.modelName,
                settings.llmProvider
            );

            if (settings.enableWebSearch)
            {
                ConfigureWebSearch();
            }
            
            if (settings.enableMultimodalAnalysis)
            {
                ConfigureMultimodalAnalysis();
            }

            if (settings.enableTTS)
            {
                ConfigureTTS();
            }
            
            // 保存到磁盘
            settings.Write();

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
                    settings.ttsVoice = voiceCopy;
                }));
            }

            Find.WindowStack.Add(new FloatMenu(options));
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
            catch (System.Exception ex)
            {
                Log.Error($"[ModSettings] TestTTS failed: {ex.Message}");
                Messages.Message($"TTS 测试失败: {ex.Message}", MessageTypeDefOf.NegativeEvent);
            }
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

        #endregion

        public override string SettingsCategory()
        {
            return "The Second Seat";
        }
    }
}
