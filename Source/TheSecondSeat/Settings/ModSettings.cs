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
        // ? 新增：TTS API 高级配置
        public string ttsApiEndpoint = "";
        public string ttsModelName = "";
        
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

        // 对话框位置
        public Rect dialogueRect = new Rect(0, 0, 600, 200);

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
            Scribe_Values.Look(ref ttsApiEndpoint, "ttsApiEndpoint", "");
            Scribe_Values.Look(ref ttsModelName, "ttsModelName", "");
            
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
            
            // 对话框位置
            Scribe_Values.Look(ref dialogueRect, "dialogueRect", new Rect(0, 0, 600, 200));
        }
    }

    /// <summary>
    /// Mod instance for accessing settings - 使用选项卡布局的现代化设置界面
    /// </summary>
    public partial class TheSecondSeatMod : Mod
    {
        public static TheSecondSeatSettings Settings { get; private set; }

        private TabManager tabManager;
        private bool tabsInitialized = false;
        
        // 难度图标
        private Texture2D assistantModeIcon;
        private Texture2D opponentModeIcon;
        private Texture2D engineerModeIcon;

        public TheSecondSeatMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<TheSecondSeatSettings>();
            
            // 初始化 LLM 服务
            LLM.LLMService.Instance.Configure(
                Settings.apiEndpoint,
                Settings.apiKey,
                Settings.modelName,
                Settings.llmProvider
            );

            // 初始化网络搜索
            if (Settings.enableWebSearch)
            {
                ConfigureWebSearch();
            }

            // 初始化多模态分析
            if (Settings.enableMultimodalAnalysis)
            {
                ConfigureMultimodalAnalysis();
            }

            // 初始化 TTS 服务
            if (Settings.enableTTS)
            {
                ConfigureTTS();
            }
        }

        private void ConfigureWebSearch()
        {
            string apiKey = Settings.searchEngine.ToLower() switch
            {
                "bing" => Settings.bingApiKey,
                "google" => Settings.googleApiKey,
                _ => null
            };

            WebSearchService.Instance.Configure(
                Settings.searchEngine,
                apiKey,
                Settings.googleSearchEngineId
            );

            Log.Message($"[The Second Seat] Web search configured: {Settings.searchEngine}");
        }

        private void ConfigureMultimodalAnalysis()
        {
            try
            {
                PersonaGeneration.MultimodalAnalysisService.Instance.Configure(
                    Settings.multimodalProvider,
                    Settings.multimodalApiKey,
                    Settings.visionModel,
                    Settings.textAnalysisModel
                );
                
                Log.Message($"[The Second Seat] Multimodal analysis configured: {Settings.multimodalProvider}");
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
                    Settings.ttsProvider,
                    Settings.ttsApiKey,
                    Settings.ttsRegion,
                    Settings.ttsVoice,
                    Settings.ttsSpeechRate,
                    Settings.ttsVolume,
                    Settings.ttsApiEndpoint,
                    Settings.ttsModelName
                );
                
                Log.Message($"[The Second Seat] TTS configured: {Settings.ttsProvider}");
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
            tabManager.CurrentTab = Settings.currentSettingsTab;
            
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
            Settings.currentSettingsTab = tabManager.CurrentTab;
            
            base.DoSettingsWindowContents(inRect);
        }

        // 基础设置已移至 TheSecondSeatMod_BasicTab.cs

        // LLM 设置已移至 TheSecondSeatMod_LLMTab.cs
        // TTS 设置已移至 TheSecondSeatMod_TTSTab.cs
        // 高级设置已移至 TheSecondSeatMod_AdvancedTab.cs

        public override string SettingsCategory()
        {
            return "The Second Seat";
        }
    }
}
