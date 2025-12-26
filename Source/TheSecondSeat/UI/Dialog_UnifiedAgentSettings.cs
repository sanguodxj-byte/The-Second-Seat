using UnityEngine;
using Verse;
using RimWorld;
using System;
using System.Collections.Generic;
using TheSecondSeat.RimAgent;

namespace TheSecondSeat.UI
{
    /// <summary>
    /// v1.6.65: 统一的 Agent 配置窗口
    /// 整合：LLM API 配置 + 多模态分析 + RimAgent 设置 + 并发管理
    /// 禁止使用 emoji
    /// </summary>
    public class Dialog_UnifiedAgentSettings : Window
    {
        private Settings.TheSecondSeatSettings settings;
        private Vector2 scrollPosition = Vector2.zero;
        
        public override Vector2 InitialSize => new Vector2(800f, 700f);

        public Dialog_UnifiedAgentSettings()
        {
            var mod = LoadedModManager.GetMod<Settings.TheSecondSeatMod>();
            settings = mod?.GetSettings<Settings.TheSecondSeatSettings>();
            
            this.doCloseX = true;
            this.doCloseButton = true;
            this.forcePause = true;
            this.absorbInputAroundWindow = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            // 标题
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleCenter;
            Rect titleRect = new Rect(0f, 0f, inRect.width, 40f);
            Widgets.Label(titleRect, "Agent 高级配置");
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;

            // 内容区域
            Rect contentRect = new Rect(0f, 50f, inRect.width, inRect.height - 100f);
            Rect viewRect = new Rect(0f, 0f, contentRect.width - 20f, 1800f);
            
            Widgets.BeginScrollView(contentRect, ref scrollPosition, viewRect);
            
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(viewRect);
            
            // ===== 1. LLM API 配置 =====
            DrawSection(listing, "LLM API 配置", () =>
            {
                // LLM 提供商
                listing.Label("LLM 提供商:");
                if (listing.RadioButton("本地 LLM (LM Studio / Ollama)", settings.llmProvider == "local"))
                {
                    settings.llmProvider = "local";
                    settings.apiEndpoint = "http://localhost:1234/v1/chat/completions";
                    settings.modelName = "local-model";
                }
                if (listing.RadioButton("OpenAI (GPT-4)", settings.llmProvider == "openai"))
                {
                    settings.llmProvider = "openai";
                    settings.apiEndpoint = "https://api.openai.com/v1/chat/completions";
                    settings.modelName = "gpt-4";
                }
                if (listing.RadioButton("DeepSeek", settings.llmProvider == "deepseek"))
                {
                    settings.llmProvider = "deepseek";
                    settings.apiEndpoint = "https://api.deepseek.com/v1/chat/completions";
                    settings.modelName = "deepseek-chat";
                }
                if (listing.RadioButton("Gemini", settings.llmProvider == "gemini"))
                {
                    settings.llmProvider = "gemini";
                    settings.apiEndpoint = "https://generativelanguage.googleapis.com/v1/models/gemini-2.0-flash-exp:generateContent";
                    settings.modelName = "gemini-2.0-flash-exp";
                }
                
                listing.Gap(12f);
                
                // API 端点
                listing.Label("API 端点:");
                settings.apiEndpoint = listing.TextEntry(settings.apiEndpoint);
                
                // API 密钥
                listing.Label("API 密钥:");
                settings.apiKey = listing.TextEntry(settings.apiKey);
                
                // 模型名称
                listing.Label("模型名称:");
                settings.modelName = listing.TextEntry(settings.modelName);
                
                listing.Gap(12f);
                
                // 温度
                listing.Label($"温度 (Temperature): {settings.temperature:F2}");
                settings.temperature = listing.Slider(settings.temperature, 0f, 2f);
                
                // 最大 Token
                listing.Label($"最大 Token: {settings.maxTokens}");
                settings.maxTokens = (int)listing.Slider(settings.maxTokens, 100, 2000);
            });
            
            // ===== 2. 多模态分析配置 =====
            DrawSection(listing, "多模态分析配置", () =>
            {
                bool oldEnable = settings.enableMultimodalAnalysis;
                listing.CheckboxLabeled("启用多模态分析", ref settings.enableMultimodalAnalysis);
                
                if (settings.enableMultimodalAnalysis)
                {
                    listing.Gap(8f);
                    
                    // 提供商
                    listing.Label("多模态提供商:");
                    if (listing.RadioButton("OpenAI (GPT-4 Vision)", settings.multimodalProvider == "openai"))
                    {
                        settings.multimodalProvider = "openai";
                    }
                    if (listing.RadioButton("DeepSeek (janus-pro-7b)", settings.multimodalProvider == "deepseek"))
                    {
                        settings.multimodalProvider = "deepseek";
                    }
                    if (listing.RadioButton("Gemini (gemini-pro-vision)", settings.multimodalProvider == "gemini"))
                    {
                        settings.multimodalProvider = "gemini";
                    }
                    
                    listing.Gap(8f);
                    
                    // API 密钥
                    listing.Label("多模态 API 密钥:");
                    settings.multimodalApiKey = listing.TextEntry(settings.multimodalApiKey);
                    
                    // 视觉模型
                    listing.Label("视觉模型:");
                    settings.visionModel = listing.TextEntry(settings.visionModel);
                    
                    // 文本分析模型
                    listing.Label("文本分析模型:");
                    settings.textAnalysisModel = listing.TextEntry(settings.textAnalysisModel);
                }
            });
            
            // ===== 3. RimAgent 设置 =====
            DrawSection(listing, "RimAgent 设置", () =>
            {
                // Agent 名称
                listing.Label("Agent 名称:");
                settings.agentName = listing.TextEntry(settings.agentName);
                
                listing.Gap(8f);
                
                // 最大重试次数
                listing.Label($"最大重试次数: {settings.maxRetries}");
                settings.maxRetries = (int)listing.Slider(settings.maxRetries, 1, 10);
                
                // 重试延迟
                listing.Label($"重试延迟: {settings.retryDelay:F1} 秒");
                settings.retryDelay = listing.Slider(settings.retryDelay, 0.5f, 10f);
                
                // 历史消息数
                listing.Label($"历史消息数: {settings.maxHistoryMessages}");
                settings.maxHistoryMessages = (int)listing.Slider(settings.maxHistoryMessages, 5, 100);
            });
            
            // ===== 4. 并发管理设置 =====
            DrawSection(listing, "并发管理设置", () =>
            {
                // 最大并发数
                listing.Label($"最大并发请求数: {settings.maxConcurrent}");
                settings.maxConcurrent = (int)listing.Slider(settings.maxConcurrent, 1, 20);
                
                // 请求超时
                listing.Label($"请求超时: {settings.requestTimeout} 秒");
                settings.requestTimeout = (int)listing.Slider(settings.requestTimeout, 10, 300);
                
                listing.Gap(8f);
                
                // 启用重试
                listing.CheckboxLabeled("启用重试机制", ref settings.enableRetry);
            });
            
            listing.Gap(20f);
            
            // ===== 底部按钮 =====
            if (listing.ButtonText("应用设置"))
            {
                ApplySettings();
                Messages.Message("Agent 配置已应用", MessageTypeDefOf.PositiveEvent);
            }
            
            if (listing.ButtonText("测试连接"))
            {
                _ = TestConnectionAsync();
            }
            
            listing.End();
            Widgets.EndScrollView();
            
            // 关闭按钮
            if (Widgets.ButtonText(new Rect(inRect.width - 120f, inRect.height - 45f, 110f, 35f), "关闭"))
            {
                Close();
            }
        }
        
        /// <summary>
        /// 绘制分组区域
        /// </summary>
        private void DrawSection(Listing_Standard listing, string title, Action drawContent)
        {
            // 标题背景
            var titleRect = listing.GetRect(30f);
            Widgets.DrawBoxSolid(titleRect, new Color(0.2f, 0.2f, 0.2f, 0.5f));
            
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleLeft;
            var labelRect = titleRect.ContractedBy(5f);
            Widgets.Label(labelRect, title);
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;
            
            listing.Gap(8f);
            
            // 内容
            drawContent();
            
            listing.Gap(12f);
            listing.GapLine();
        }
        
        /// <summary>
        /// 应用设置
        /// </summary>
        private void ApplySettings()
        {
            try
            {
                // 配置 LLM
                LLM.LLMService.Instance.Configure(
                    settings.apiEndpoint,
                    settings.apiKey,
                    settings.modelName,
                    settings.llmProvider
                );
                
                // 配置多模态分析
                if (settings.enableMultimodalAnalysis)
                {
                    PersonaGeneration.MultimodalAnalysisService.Instance.Configure(
                        settings.multimodalProvider,
                        settings.multimodalApiKey,
                        settings.visionModel,
                        settings.textAnalysisModel
                    );
                }
                
                // 更新并发管理器设置
                ConcurrentRequestManager.Instance.UpdateSettings(
                    settings.maxConcurrent,
                    settings.requestTimeout,
                    settings.enableRetry
                );
                
                Log.Message("[UnifiedAgentSettings] 配置已应用");
            }
            catch (Exception ex)
            {
                Log.Error($"[UnifiedAgentSettings] 应用设置失败: {ex.Message}");
                Messages.Message($"应用设置失败: {ex.Message}", MessageTypeDefOf.NegativeEvent);
            }
        }
        
        /// <summary>
        /// 测试连接
        /// </summary>
        private async System.Threading.Tasks.Task TestConnectionAsync()
        {
            try
            {
                Messages.Message("正在测试 LLM 连接...", MessageTypeDefOf.NeutralEvent);
                
                var success = await LLM.LLMService.Instance.TestConnectionAsync();
                
                if (success)
                {
                    Messages.Message("LLM 连接测试成功！", MessageTypeDefOf.PositiveEvent);
                }
                else
                {
                    Messages.Message("LLM 连接测试失败", MessageTypeDefOf.NegativeEvent);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[Dialog_UnifiedAgentSettings] TestConnection failed: {ex.Message}");
                Messages.Message($"连接测试失败: {ex.Message}", MessageTypeDefOf.NegativeEvent);
            }
        }
    }
}
