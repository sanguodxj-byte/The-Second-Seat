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
        public int searchDelayMs = 1000; // ? ������������ʱ�����룩

        // Multimodal Analysis Settings
        public bool enableMultimodalAnalysis = false;
        public string multimodalProvider = "openai"; // "openai", "deepseek", "gemini"
        public string multimodalApiKey = "";
        public string visionModel = "";
        public string textAnalysisModel = "";

        // ? TTS Settings
        public bool enableTTS = false;
        public string ttsProvider = "edge"; // "azure", "edge", "local"
        public string ttsApiKey = "";
        public string ttsRegion = "eastus";
        public string ttsVoice = "zh-CN-XiaoxiaoNeural";
        public float ttsSpeechRate = 1.0f;
        public float ttsVolume = 1.0f;
        public bool autoPlayTTS = false; // ✅ 新增：自动播放 TTS

        // ? UI Collapse States (�５�״̬)
        public bool collapseLLMSettings = false;
        public bool collapseWebSearchSettings = false;
        public bool collapseMultimodalSettings = false;
        public bool collapseTTSSettings = false;
        public bool collapseDifficultySettings = false;  // ✅ 新增：难度设置折叠状态

        // ✅ 精简提示词（加快响应速度）
        public bool useCompactPrompt = true;  // 默认使用精简版
        
        // ✅ UI Button Position
        public float buttonPositionX = -1f; // -1 = use default position
        public float buttonPositionY = -1f;
        
        // ✅ 立绘模式设置
        public bool usePortraitMode = false;  // 默认使用头像模式（512x512）

        // Debug
        public bool debugMode = false;

        // ? �øж�ϵͳ
        public bool enableAffinitySystem = true;
        
        // ? �Ѷ�ģʽ
        public PersonaGeneration.AIDifficultyMode difficultyMode = PersonaGeneration.AIDifficultyMode.Assistant;
        
        // ? ȫ����ʾ��
        public string globalPrompt = "";

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
            Scribe_Values.Look(ref searchDelayMs, "searchDelayMs", 1000); // ? ������ʱ����
            
            // Multimodal Analysis
            Scribe_Values.Look(ref enableMultimodalAnalysis, "enableMultimodalAnalysis", false);
            Scribe_Values.Look(ref multimodalProvider, "multimodalProvider", "openai");
            Scribe_Values.Look(ref multimodalApiKey, "multimodalApiKey", "");
            Scribe_Values.Look(ref visionModel, "visionModel", "");
            Scribe_Values.Look(ref textAnalysisModel, "textAnalysisModel", "");
            
            // ? TTS
            Scribe_Values.Look(ref enableTTS, "enableTTS", false);
            Scribe_Values.Look(ref ttsProvider, "ttsProvider", "edge");
            Scribe_Values.Look(ref ttsApiKey, "ttsApiKey", "");
            Scribe_Values.Look(ref ttsRegion, "ttsRegion", "eastus");
            Scribe_Values.Look(ref ttsVoice, "ttsVoice", "zh-CN-XiaoxiaoNeural");
            Scribe_Values.Look(ref ttsSpeechRate, "ttsSpeechRate", 1.0f);
            Scribe_Values.Look(ref ttsVolume, "ttsVolume", 1.0f);
            Scribe_Values.Look(ref autoPlayTTS, "autoPlayTTS", false); // ✅ 新增

            // ? Collapse States
            Scribe_Values.Look(ref collapseLLMSettings, "collapseLLMSettings", false);
            Scribe_Values.Look(ref collapseWebSearchSettings, "collapseWebSearchSettings", false);
            Scribe_Values.Look(ref collapseMultimodalSettings, "collapseMultimodalSettings", false);
            Scribe_Values.Look(ref collapseTTSSettings, "collapseTTSSettings", false);
            Scribe_Values.Look(ref collapseDifficultySettings, "collapseDifficultySettings", false);  // ✅ 新增
            
            // ✅ 精简提示词选项
            Scribe_Values.Look(ref useCompactPrompt, "useCompactPrompt", true);
            
            // ✅ UI Button Position
            Scribe_Values.Look(ref buttonPositionX, "buttonPositionX", -1f);
            Scribe_Values.Look(ref buttonPositionY, "buttonPositionY", -1f);
            
            // ✅ 立绘模式设置
            Scribe_Values.Look(ref usePortraitMode, "usePortraitMode", false);
            
            // Debug
            Scribe_Values.Look(ref debugMode, "debugMode", false);

            // ? �øж�ϵͳ
            Scribe_Values.Look(ref enableAffinitySystem, "enableAffinitySystem", true);
            
            // ? �Ѷ�ģʽ
            Scribe_Values.Look(ref difficultyMode, "difficultyMode", PersonaGeneration.AIDifficultyMode.Assistant);
            
            // ? ȫ����ʾ��
            Scribe_Values.Look(ref globalPrompt, "globalPrompt", "");
        }
    }

    /// <summary>
    /// Mod instance for accessing settings
    /// </summary>
    public class TheSecondSeatMod : Mod
    {
        private TheSecondSeatSettings settings;
        private Vector2 scrollPosition = Vector2.zero;

        public TheSecondSeatMod(ModContentPack content) : base(content)
        {
            this.settings = GetSettings<TheSecondSeatSettings>();
            
            // ��ʼ�� LLM ����
            LLM.LLMService.Instance.Configure(
                settings.apiEndpoint,
                settings.apiKey,
                settings.modelName,
                settings.llmProvider  // ✅ 新增：传递 provider
            );

            // ��ʼ����������
            if (settings.enableWebSearch)
            {
                ConfigureWebSearch();
            }

            // ��ʼ����ģ̬��������
            if (settings.enableMultimodalAnalysis)
            {
                ConfigureMultimodalAnalysis();
            }

            // ? ��ʼ�� TTS ����
            if (settings.enableTTS)
            {
                ConfigureTTS();
            }
        }

        private void ConfigureWebSearch()
        {
            string? apiKey = settings.searchEngine.ToLower() switch
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

            Log.Message($"[The Second Seat] ��������������: {settings.searchEngine}");
        }

        private void ConfigureMultimodalAnalysis()
        {
            // �������ö�ģ̬��������
            try
            {
                PersonaGeneration.MultimodalAnalysisService.Instance.Configure(
                    settings.multimodalProvider,
                    settings.multimodalApiKey,
                    settings.visionModel,
                    settings.textAnalysisModel
                );
                
                Log.Message($"[The Second Seat] ��ģ̬����������: {settings.multimodalProvider}");
            }
            catch (Exception ex)
            {
                Log.Error($"[The Second Seat] ��ģ̬��������ʧ��: {ex.Message}");
            }
        }

        // ? ���� TTS ����
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
                
                Log.Message($"[The Second Seat] TTS ������: {settings.ttsProvider}");
            }
            catch (Exception ex)
            {
                Log.Error($"[The Second Seat] TTS ����ʧ��: {ex.Message}");
            }
        }

        /// <summary>
        /// ✅ 绘制难度模式选项（带图标）
        /// </summary>
        private void DrawDifficultyOption(Rect rect, Texture2D? icon, string title, string subtitle, string description, bool isSelected, Color accentColor)
        {
            // 背景
            if (isSelected)
            {
                Widgets.DrawBoxSolid(rect, new Color(accentColor.r * 0.3f, accentColor.g * 0.3f, accentColor.b * 0.3f, 0.5f));
            }
            else if (Mouse.IsOver(rect))
            {
                Widgets.DrawBoxSolid(rect, new Color(0.25f, 0.25f, 0.25f, 0.5f));
            }
            
            // 边框
            if (isSelected)
            {
                GUI.color = accentColor;
                Widgets.DrawBox(rect, 2);
                GUI.color = Color.white;
            }
            else
            {
                Widgets.DrawBox(rect, 1);
            }
            
            var innerRect = rect.ContractedBy(5f);
            
            // 图标区域（左侧）
            float iconSize = 50f;
            var iconRect = new Rect(innerRect.x, innerRect.y, iconSize, iconSize);
            
            if (icon != null)
            {
                GUI.DrawTexture(iconRect, icon, ScaleMode.ScaleToFit);
            }
            else
            {
                // 占位符：绘制带颜色的方块
                Widgets.DrawBoxSolid(iconRect, accentColor * 0.5f);
                
                // 绘制模式首字母
                Text.Font = GameFont.Medium;
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = Color.white;
                Widgets.Label(iconRect, title.Substring(0, 1));
                Text.Anchor = TextAnchor.UpperLeft;
                Text.Font = GameFont.Small;
            }
            
            // 文字区域（右侧）
            float textX = innerRect.x + iconSize + 10f;
            float textWidth = innerRect.width - iconSize - 10f;
            
            // 标题
            Text.Font = GameFont.Small;
            GUI.color = isSelected ? accentColor : Color.white;
            var titleRect = new Rect(textX, innerRect.y, textWidth, 20f);
            Widgets.Label(titleRect, title + (isSelected ? " [已选择]" : ""));
            
            // 副标题
            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.7f, 0.7f, 0.7f);
            var subtitleRect = new Rect(textX, innerRect.y + 18f, textWidth, 16f);
            Widgets.Label(subtitleRect, subtitle);
            
            // 描述（悬停时显示）
            if (Mouse.IsOver(rect))
            {
                GUI.color = new Color(0.6f, 0.6f, 0.6f);
                var descRect = new Rect(textX, innerRect.y + 34f, textWidth, 20f);
                Widgets.Label(descRect, description);
            }
            
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
        }

        /// <summary>
        /// 绘制难度模式卡片（大卡片样式）
        /// </summary>
        private void DrawDifficultyCard(Rect rect, Texture2D? icon, string title, string description, bool isSelected, Color accentColor)
        {
            // 背景
            if (isSelected)
            {
                Widgets.DrawBoxSolid(rect, new Color(accentColor.r * 0.2f, accentColor.g * 0.2f, accentColor.b * 0.2f, 0.8f));
            }
            else if (Mouse.IsOver(rect))
            {
                Widgets.DrawBoxSolid(rect, new Color(0.2f, 0.2f, 0.2f, 0.6f));
            }
            else
            {
                Widgets.DrawBoxSolid(rect, new Color(0.15f, 0.15f, 0.15f, 0.5f));
            }
            
            // 边框
            if (isSelected)
            {
                GUI.color = accentColor;
                Widgets.DrawBox(rect, 3);
                GUI.color = Color.white;
            }
            else
            {
                Widgets.DrawBox(rect, 1);
            }
            
            var innerRect = rect.ContractedBy(10f);
            
            // 图标区域（顶部居中）
            float iconSize = 64f;
            var iconRect = new Rect(innerRect.x + (innerRect.width - iconSize) / 2f, innerRect.y + 10f, iconSize, iconSize);
            
            if (icon != null)
            {
                GUI.DrawTexture(iconRect, icon, ScaleMode.ScaleToFit);
            }
            else
            {
                // 占位符：绘制带颜色的圆形
                Widgets.DrawBoxSolid(iconRect, accentColor * 0.5f);
                
                // 绘制模式首字母
                Text.Font = GameFont.Medium;
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = Color.white;
                Widgets.Label(iconRect, title.Substring(0, 1));
                Text.Anchor = TextAnchor.UpperLeft;
                Text.Font = GameFont.Small;
            }
            
            // 标题（图标下方居中）- 修复：移除emoji
            float titleY = iconRect.yMax + 15f;
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.UpperCenter;
            GUI.color = isSelected ? accentColor : Color.white;
            var titleRect = new Rect(innerRect.x, titleY, innerRect.width, 25f);
            Widgets.Label(titleRect, title + (isSelected ? " [OK]" : ""));
            
            // 描述（标题下方居中）
            float descY = titleY + 28f;
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.UpperCenter;
            GUI.color = new Color(0.7f, 0.7f, 0.7f);
            var descRect = new Rect(innerRect.x, descY, innerRect.width, 50f);
            Widgets.Label(descRect, description);
            
            // 重置
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;
        }

        // 折叠区域辅助方法（已存在，保持不变）
        private void DrawCollapsibleSection(Listing_Standard listing, string title, ref bool collapsed, Action drawContent)
        {
            var headerRect = listing.GetRect(30f);
            
            // 绘制标题背景
            Widgets.DrawBoxSolid(headerRect, new Color(0.2f, 0.2f, 0.2f, 0.5f));
            
            // 绘制箭头和标题
            var arrowRect = new Rect(headerRect.x + 5f, headerRect.y + 5f, 20f, 20f);
            var titleRect = new Rect(headerRect.x + 30f, headerRect.y, headerRect.width - 30f, headerRect.height);
            
            Text.Font = GameFont.Medium;
            Widgets.Label(titleRect, title);
            Text.Font = GameFont.Small;
            
            // 绘制箭头 - 修复：使用ASCII字符
            string arrow = collapsed ? ">" : "v";
            Widgets.Label(arrowRect, arrow);
            
            // 点击切换折叠
            if (Widgets.ButtonInvisible(headerRect))
            {
                collapsed = !collapsed;
            }
            
            // 如果未折叠，绘制内容
            if (!collapsed)
            {
                listing.Gap(8f);
                drawContent();
                listing.Gap(12f);
            }
            
            listing.GapLine();
        }

        // ✅ 静态纹理缓存（难度模式图标）
        private static Texture2D? assistantModeIcon;
        private static Texture2D? opponentModeIcon;
        
        private static void LoadDifficultyIcons()
        {
            if (assistantModeIcon == null)
            {
                // ✅ 使用 large 版本
                assistantModeIcon = ContentFinder<Texture2D>.Get("UI/DifficultyMode/assistant_large", false);
            }
            if (opponentModeIcon == null)
            {
                // ✅ 使用 large 版本
                opponentModeIcon = ContentFinder<Texture2D>.Get("UI/DifficultyMode/opponent_large", false);
            }
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            // ✅ 加载难度图标
            LoadDifficultyIcons();
            
            Listing_Standard listingStandard = new Listing_Standard();
            
            // 增加滚动视图高度
            var scrollViewRect = new Rect(0f, 0f, inRect.width - 20f, 2600f);
            var outRect = new Rect(0f, 0f, inRect.width, inRect.height);
            
            Widgets.BeginScrollView(outRect, ref scrollPosition, scrollViewRect);
            listingStandard.Begin(scrollViewRect);

            // === 基础设置（不折叠）===
            listingStandard.Label("TSS_Settings_Basic_Title".Translate());
            listingStandard.Gap(12f);
            
            listingStandard.CheckboxLabeled("TSS_Settings_DebugMode".Translate(), ref settings.debugMode);
            listingStandard.CheckboxLabeled("TSS_Settings_EnableAffinity".Translate(), ref settings.enableAffinitySystem);
            
            if (!settings.enableAffinitySystem)
            {
                var oldFont = Text.Font;
                Text.Font = GameFont.Tiny;
                GUI.color = Color.yellow;
                listingStandard.Label("TSS_Settings_AffinityDisabled_Warning".Translate());
                GUI.color = Color.white;
                Text.Font = oldFont;
            }
            
            // ✅ 立绘模式设置
            listingStandard.Gap(12f);
            listingStandard.CheckboxLabeled("使用立绘模式（1024x1572 全身立绘）", ref settings.usePortraitMode);
            if (settings.usePortraitMode)
            {
                var oldFont = Text.Font;
                Text.Font = GameFont.Tiny;
                GUI.color = new Color(0.6f, 0.8f, 1.0f);
                listingStandard.Label("  启用后，AI 按钮将显示完整立绘而非头像");
                listingStandard.Label("  立绘尺寸：1024x1572（全身）");
                GUI.color = Color.white;
                Text.Font = oldFont;
            }
            else
            {
                var oldFont = Text.Font;
                Text.Font = GameFont.Tiny;
                GUI.color = new Color(0.8f, 0.8f, 0.6f);
                listingStandard.Label("  当前使用头像模式：512x512（脸部特写）");
                GUI.color = Color.white;
                Text.Font = oldFont;
            }
            
            // ✅ 精简提示词选项
            listingStandard.Gap(8f);
            listingStandard.CheckboxLabeled("使用精简提示词（加快响应速度）", ref settings.useCompactPrompt);
            if (settings.useCompactPrompt)
            {
                var oldFont = Text.Font;
                Text.Font = GameFont.Tiny;
                GUI.color = new Color(0.6f, 0.8f, 0.6f);
                listingStandard.Label("  精简模式：约500 tokens，响应更快");
                GUI.color = Color.white;
                Text.Font = oldFont;
            }
            else
            {
                var oldFont = Text.Font;
                Text.Font = GameFont.Tiny;
                GUI.color = new Color(0.8f, 0.8f, 0.6f);
                listingStandard.Label("  完整模式：约5000 tokens，更详细但较慢");
                GUI.color = Color.white;
                Text.Font = oldFont;
            }
            
            listingStandard.GapLine();

            // ✅ === 难度模式设置（可折叠，小图标样式）===
            DrawCollapsibleSection(listingStandard, "难度选择", ref settings.collapseDifficultySettings, () =>
            {
                // 小图标样式：两行横向排列
                float totalWidth = listingStandard.ColumnWidth;
                float optionWidth = (totalWidth - 10f) / 2f;  // 两个选项，中间留10px间距
                float optionHeight = 60f;  // ✅ 小图标样式高度
                
                var optionsRect = listingStandard.GetRect(optionHeight);
                
                // 左边选项：助手模式
                var assistantRect = new Rect(optionsRect.x, optionsRect.y, optionWidth, optionHeight);
                DrawDifficultyOption(
                    assistantRect,
                    assistantModeIcon,
                    "助手",
                    "无条件支持",
                    "主动建议、协助管理",
                    settings.difficultyMode == PersonaGeneration.AIDifficultyMode.Assistant,
                    new Color(0.3f, 0.7f, 0.4f)
                );
                if (Widgets.ButtonInvisible(assistantRect))
                {
                    settings.difficultyMode = PersonaGeneration.AIDifficultyMode.Assistant;
                }
                
                // 右边选项：奕者模式（原对弈者）
                var opponentRect = new Rect(optionsRect.x + optionWidth + 10f, optionsRect.y, optionWidth, optionHeight);
                DrawDifficultyOption(
                    opponentRect,
                    opponentModeIcon,
                    "奕者",
                    "挑战平衡",
                    "事件控制、难度调整",
                    settings.difficultyMode == PersonaGeneration.AIDifficultyMode.Opponent,
                    new Color(0.7f, 0.3f, 0.3f)
                );
                if (Widgets.ButtonInvisible(opponentRect))
                {
                    settings.difficultyMode = PersonaGeneration.AIDifficultyMode.Opponent;
                }
            });

            // === LLM 设置（可折叠）===
            DrawCollapsibleSection(listingStandard, "TSS_Settings_LLM_Title".Translate(), ref settings.collapseLLMSettings, () =>
            {
                // LLM �ṩ��ѡ��
                listingStandard.Label("TSS_Settings_LLMProvider".Translate());
                if (listingStandard.RadioButton("TSS_Settings_LLMProvider_Local".Translate(), settings.llmProvider == "local"))
                {
                    settings.llmProvider = "local";
                    settings.apiEndpoint = "http://localhost:1234/v1/chat/completions";
                    settings.modelName = "local-model";
                }
                if (listingStandard.RadioButton("TSS_Settings_LLMProvider_OpenAI".Translate(), settings.llmProvider == "openai"))
                {
                    settings.llmProvider = "openai";
                    settings.apiEndpoint = "https://api.openai.com/v1/chat/completions";
                    settings.modelName = "gpt-4";
                }
                if (listingStandard.RadioButton("TSS_Settings_LLMProvider_DeepSeek".Translate(), settings.llmProvider == "deepseek"))
                {
                    settings.llmProvider = "deepseek";
                    settings.apiEndpoint = "https://api.deepseek.com/v1/chat/completions";
                    settings.modelName = "deepseek-chat";
                }
                if (listingStandard.RadioButton("TSS_Settings_LLMProvider_Gemini".Translate(), settings.llmProvider == "gemini"))
                {
                    settings.llmProvider = "gemini";
                    settings.apiEndpoint = "https://generativelanguage.googleapis.com/v1/models/gemini-2.0-flash-exp:generateContent";
                    settings.modelName = "gemini-2.0-flash-exp";
                }
                
                listingStandard.Gap(12f);
                
                listingStandard.Label("TSS_Settings_APIEndpoint".Translate());
                settings.apiEndpoint = listingStandard.TextEntry(settings.apiEndpoint);
                
                listingStandard.Label("TSS_Settings_APIKey".Translate());
                settings.apiKey = listingStandard.TextEntry(settings.apiKey);
                
                listingStandard.Label("TSS_Settings_ModelName".Translate());
                settings.modelName = listingStandard.TextEntry(settings.modelName);

                listingStandard.Gap(12f);
                listingStandard.Label($"{"TSS_Settings_Temperature".Translate()}: {settings.temperature:F2}");
                settings.temperature = listingStandard.Slider(settings.temperature, 0f, 2f);

                listingStandard.Label($"{"TSS_Settings_MaxTokens".Translate()}: {settings.maxTokens}");
                settings.maxTokens = (int)listingStandard.Slider(settings.maxTokens, 100, 2000);
            });

            // === �����������ã����５���===
            DrawCollapsibleSection(listingStandard, "TSS_Settings_WebSearch_Title".Translate(), ref settings.collapseWebSearchSettings, () =>
            {
                bool oldEnableWebSearch = settings.enableWebSearch;
                listingStandard.CheckboxLabeled("TSS_Settings_EnableWebSearch".Translate(), ref settings.enableWebSearch);

                if (settings.enableWebSearch)
                {
                    // ��������ѡ��
                    listingStandard.Label("TSS_Settings_SearchEngine".Translate());
                    if (listingStandard.RadioButton("TSS_Settings_SearchEngine_DuckDuckGo".Translate(), settings.searchEngine == "duckduckgo"))
                    {
                        settings.searchEngine = "duckduckgo";
                    }
                    if (listingStandard.RadioButton("TSS_Settings_SearchEngine_Bing".Translate(), settings.searchEngine == "bing"))
                    {
                        settings.searchEngine = "bing";
                    }
                    if (listingStandard.RadioButton("TSS_Settings_SearchEngine_Google".Translate(), settings.searchEngine == "google"))
                    {
                        settings.searchEngine = "google";
                    }

                    listingStandard.Gap(12f);

                    // API Key ����
                    if (settings.searchEngine == "bing")
                    {
                        listingStandard.Label("TSS_Settings_BingAPIKey".Translate());
                        settings.bingApiKey = listingStandard.TextEntry(settings.bingApiKey);
                    }
                    else if (settings.searchEngine == "google")
                    {
                        listingStandard.Label("TSS_Settings_GoogleAPIKey".Translate());
                        settings.googleApiKey = listingStandard.TextEntry(settings.googleApiKey);
                        
                        listingStandard.Label("TSS_Settings_GoogleSearchEngineID".Translate());
                        settings.googleSearchEngineId = listingStandard.TextEntry(settings.googleSearchEngineId);
                    }

                    listingStandard.Gap(12f);
                    listingStandard.Label($"{"TSS_Settings_SearchDelay".Translate()}: {settings.searchDelayMs} ms");
                    settings.searchDelayMs = (int)listingStandard.Slider(settings.searchDelayMs, 0, 5000);

                    if (settings.enableWebSearch != oldEnableWebSearch)
                    {
                        ConfigureWebSearch();
                    }
                }
            });

            // === ��ģ̬�������ã����５���===
            DrawCollapsibleSection(listingStandard, "TSS_Settings_Multimodal_Title".Translate(), ref settings.collapseMultimodalSettings, () =>
            {
                listingStandard.CheckboxLabeled("TSS_Settings_EnableMultimodal".Translate(), ref settings.enableMultimodalAnalysis);

                if (settings.enableMultimodalAnalysis)
                {
                    // ģ���ṩ��ѡ��
                    listingStandard.Label("TSS_Settings_MultimodalProvider".Translate());
                    
                    // ✅ 修复：每次更改提供商时立即重新配置
                    bool providerChanged = false;
                    
                    if (listingStandard.RadioButton("TSS_Settings_MultimodalProvider_OpenAI".Translate(), settings.multimodalProvider == "openai"))
                    {
                        if (settings.multimodalProvider != "openai")
                        {
                            settings.multimodalProvider = "openai";
                            providerChanged = true;
                        }
                    }
                    if (listingStandard.RadioButton("TSS_Settings_MultimodalProvider_DeepSeek".Translate(), settings.multimodalProvider == "deepseek"))
                    {
                        if (settings.multimodalProvider != "deepseek")
                        {
                            settings.multimodalProvider = "deepseek";
                            providerChanged = true;
                        }
                    }
                    if (listingStandard.RadioButton("TSS_Settings_MultimodalProvider_Gemini".Translate(), settings.multimodalProvider == "gemini"))
                    {
                        if (settings.multimodalProvider != "gemini")
                        {
                            settings.multimodalProvider = "gemini";
                            providerChanged = true;
                        }
                    }

                    // ✅ 如果提供商改变，立即重新配置
                    if (providerChanged)
                    {
                        ConfigureMultimodalAnalysis();
                        Messages.Message($"多模态分析提供商已切换到: {settings.multimodalProvider}", MessageTypeDefOf.NeutralEvent);
                    }

                    listingStandard.Gap(12f);

                    // API Key ��ģ������
                    listingStandard.Label("TSS_Settings_MultimodalAPIKey".Translate());
                    settings.multimodalApiKey = listingStandard.TextEntry(settings.multimodalApiKey);
                    
                    listingStandard.Label("TSS_Settings_VisionModel".Translate());
                    settings.visionModel = listingStandard.TextEntry(settings.visionModel);
                    
                    listingStandard.Label("TSS_Settings_TextAnalysisModel".Translate());
                    settings.textAnalysisModel = listingStandard.TextEntry(settings.textAnalysisModel);
                }
            });

            // ? === TTS ���ã����５���===
            DrawCollapsibleSection(listingStandard, "语音合成（TTS）", ref settings.collapseTTSSettings, () =>
            {
                bool oldEnableTTS = settings.enableTTS;
                listingStandard.CheckboxLabeled("启用语音合成（TTS）", ref settings.enableTTS);

                if (settings.enableTTS)
                {
                    // ✅ 恢复所有 TTS 提供商选项
                    listingStandard.Label("TTS 提供商");
                    if (listingStandard.RadioButton("Azure TTS (高质量/需API Key)", settings.ttsProvider == "azure"))
                    {
                        settings.ttsProvider = "azure";
                    }
                    if (listingStandard.RadioButton("Edge TTS (免费/在线)", settings.ttsProvider == "edge"))
                    {
                        settings.ttsProvider = "edge";
                    }
                    if (listingStandard.RadioButton("本地 TTS (离线/系统语音)", settings.ttsProvider == "local"))
                    {
                        settings.ttsProvider = "local";
                    }

                    listingStandard.Gap(12f);

                    if (settings.ttsProvider == "azure")
                    {
                        // Azure TTS 配置
                        listingStandard.Label("Azure TTS API 密钥");
                        settings.ttsApiKey = listingStandard.TextEntry(settings.ttsApiKey);
                        
                        if (string.IsNullOrEmpty(settings.ttsApiKey))
                        {
                            var oldFont = Text.Font;
                            Text.Font = GameFont.Tiny;
                            GUI.color = Color.yellow;
                            listingStandard.Label("  请输入 Azure Speech Services API 密钥");
                            listingStandard.Label("  获取地址: https://azure.microsoft.com/");
                            GUI.color = Color.white;
                            Text.Font = oldFont;
                        }
                        
                        listingStandard.Gap(8f);
                        listingStandard.Label("Azure 区域（如: eastus, westeurope）");
                        settings.ttsRegion = listingStandard.TextEntry(settings.ttsRegion);
                    }
                    else if (settings.ttsProvider == "edge")
                    {
                        var oldFont = Text.Font;
                        Text.Font = GameFont.Tiny;
                        GUI.color = new Color(0.6f, 0.8f, 1.0f);
                        listingStandard.Label("  Edge TTS 使用微软 Edge 浏览器的在线语音服务");
                        listingStandard.Label("  无需 API Key，但需要网络连接");
                        GUI.color = Color.white;
                        Text.Font = oldFont;
                    }
                    else if (settings.ttsProvider == "local")
                    {
                        var oldFont = Text.Font;
                        Text.Font = GameFont.Tiny;
                        GUI.color = new Color(0.6f, 1.0f, 0.6f);
                        listingStandard.Label("  使用 Windows 系统自带的 TTS 语音");
                        listingStandard.Label("  无需网络，速度快，但音质取决于系统安装的语音包");
                        GUI.color = Color.white;
                        Text.Font = oldFont;
                    }

                    // 语音选择
                    listingStandard.Gap(12f);
                    listingStandard.Label("语音选择");
                    if (listingStandard.ButtonText(settings.ttsVoice))
                    {
                        ShowVoiceSelectionMenu();
                    }

                    // 语速和音量
                    listingStandard.Gap(12f);
                    listingStandard.Label($"语速: {settings.ttsSpeechRate:F2}x");
                    settings.ttsSpeechRate = listingStandard.Slider(settings.ttsSpeechRate, 0.5f, 2.0f);

                    listingStandard.Label($"音量: {(int)(settings.ttsVolume * 100)}%");
                    settings.ttsVolume = listingStandard.Slider(settings.ttsVolume, 0f, 1f);
                    
                    listingStandard.Gap(8f);
                    
                    // ✅ 新增：自动播放 TTS 复选框
                    listingStandard.CheckboxLabeled("自动播放 TTS（叙事者发言时）", ref settings.autoPlayTTS);
                    
                    if (settings.autoPlayTTS)
                    {
                        var oldFont = Text.Font;
                        Text.Font = GameFont.Tiny;
                        GUI.color = new Color(0.6f, 0.8f, 0.6f);
                        listingStandard.Label("  启用后，AI 回复时自动生成语音文件");
                        GUI.color = Color.white;
                        Text.Font = oldFont;
                    }
                    
                    listingStandard.Gap(8f);
                    
                    // ✅ 测试按钮
                    if (listingStandard.ButtonText("测试 TTS"))
                    {
                        TestTTS();
                    }

                    if (settings.enableTTS != oldEnableTTS)
                    {
                        ConfigureTTS();
                    }
                }
            });

            // === ȫ����ʾ�ʣ����５���===
            listingStandard.Label("TSS_Settings_GlobalPrompt_Title".Translate());
            listingStandard.Gap(12f);
            
            var oldFont2 = Text.Font;
            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.7f, 0.7f, 0.7f);
            listingStandard.Label("TSS_Settings_GlobalPrompt_Description".Translate());
            GUI.color = Color.white;
            Text.Font = oldFont2;
            
            listingStandard.Gap(8f);
            
            // �ı���������Ӹ߶ȣ�
            var promptRect = listingStandard.GetRect(150f);
            settings.globalPrompt = Widgets.TextArea(promptRect, settings.globalPrompt ?? "");
            
            listingStandard.Gap(8f);
            
            // ʾ����ť
            if (listingStandard.ButtonText("TSS_Settings_GlobalPrompt_LoadExample".Translate()))
            {
                settings.globalPrompt = GetExampleGlobalPrompt();
            }

            listingStandard.Gap(20f);

            // === ������ť ===
            listingStandard.GapLine();
            
            // 应用按钮
            if (listingStandard.ButtonText("TSS_Settings_Apply".Translate()))
            {
                LLM.LLMService.Instance.Configure(
                    settings.apiEndpoint,
                    settings.apiKey,
                    settings.modelName,
                    settings.llmProvider  // ✅ 新增：传递 provider
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

                Messages.Message("TSS_Settings_Applied".Translate(), MessageTypeDefOf.PositiveEvent);
            }

            // �������Ӱ�ť
            if (listingStandard.ButtonText("TSS_Settings_TestConnection".Translate()))
            {
                TestConnection();
            }

            // ����������水ť
            if (settings.enableWebSearch && listingStandard.ButtonText("TSS_Settings_ClearSearchCache".Translate()))
            {
                WebSearchService.Instance.ClearCache();
                Messages.Message("TSS_Settings_CacheCleared".Translate(), MessageTypeDefOf.NeutralEvent);
            }

            // ? ���� TTS ��ť
            if (settings.enableTTS && listingStandard.ButtonText("TSS_Settings_TestTTS".Translate()))
            {
                TestTTS();
            }
            
            listingStandard.Gap(30f);

            listingStandard.End();
            Widgets.EndScrollView();
            
            base.DoSettingsWindowContents(inRect);
        }

        private async void TestConnection()
        {
            try
            {
                Messages.Message("TSS_Settings_Testing".Translate(), MessageTypeDefOf.NeutralEvent);
                
                var success = await LLM.LLMService.Instance.TestConnectionAsync();
                
                if (success)
                {
                    Messages.Message("TSS_Settings_TestSuccess".Translate(), MessageTypeDefOf.PositiveEvent);
                }
                else
                {
                    Messages.Message("TSS_Settings_TestFailed".Translate(), MessageTypeDefOf.NegativeEvent);
                }
            }
            catch (System.Exception ex)
            {
                Messages.Message($"���Ӳ���ʧ��: {ex.Message}", MessageTypeDefOf.NegativeEvent);
            }
        }

        // ? ��ʾ����ѡ��˵�
        private void ShowVoiceSelectionMenu()
        {
            var voices = TTS.TTSService.GetAvailableVoices();
            var options = new System.Collections.Generic.List<FloatMenuOption>();

            foreach (var voice in voices)
            {
                string voiceCopy = voice; // ����հ����`;
                options.Add(new FloatMenuOption(voice, () => {
                    settings.ttsVoice = voiceCopy;
                }));
            }

            Find.WindowStack.Add(new FloatMenu(options));
        }

        // ? ���� TTS
        private async void TestTTS()
        {
            try
            {
                Messages.Message("���ڲ��� TTS...", MessageTypeDefOf.NeutralEvent);
                
                string testText = "��ã�����������ԡ�Hello, this is a voice test.";
                string? filePath = await TTS.TTSService.Instance.SpeakAsync(testText);
                
                if (!string.IsNullOrEmpty(filePath))
                {
                    Messages.Message("TTS ���Գɹ�����Ƶ�ļ��ѱ��档", MessageTypeDefOf.PositiveEvent);
                }
                else
                {
                    Messages.Message("TTS ����ʧ��", MessageTypeDefOf.NegativeEvent);
                }
            }
            catch (System.Exception ex)
            {
                Messages.Message($"TTS ����ʧ��: {ex.Message}", MessageTypeDefOf.NegativeEvent);
            }
        }

        /// <summary>
        /// ��ȡʾ��ȫ����ʾ��
        /// </summary>
        private string GetExampleGlobalPrompt()
        {
            return @"# ȫ��ָ��ʾ��

## ���Է��
- ʹ�ü������������
- ��������߳�������
- ����ʹ��һЩ���ĳ������������Ȥζ��

## ��Ϊ׼��
- ���ȿ���ֳ���ߵİ�ȫ
- �ṩ�����Խ�����ǵ�������
- ��Σ���������������

## �����ص�
- �������Ƶ�ר�ProfessionaL����
- ż��չ����Ĭ��
- ����.enterprise ʾ����";
        }

        public override string SettingsCategory()
        {
            return "The Second Seat";
        }
    }
}
