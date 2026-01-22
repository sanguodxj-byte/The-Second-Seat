using UnityEngine;
using Verse;
using RimWorld;
using System;
using System.Collections.Generic;
using TheSecondSeat.RimAgent;

namespace TheSecondSeat.UI
{
    /// <summary>
    /// v1.6.66: 统一的 Agent 配置窗口（紧凑版）
    /// 整合：LLM API 配置 + 多模态分析 + RimAgent 设置 + 并发管理
    /// </summary>
    public class Dialog_UnifiedAgentSettings : Window
    {
        private Settings.TheSecondSeatSettings settings;
        private Vector2 scrollPosition = Vector2.zero;
        
        // 紧凑行高
        private const float CompactRowHeight = 24f;
        private const float LabelWidth = 120f;
        
        public override Vector2 InitialSize => new Vector2(700f, 600f);

        public Dialog_UnifiedAgentSettings()
        {
            var mod = LoadedModManager.GetMod<Settings.TheSecondSeatMod>();
            settings = mod?.GetSettings<Settings.TheSecondSeatSettings>();
            
            this.doCloseX = true;
            this.doCloseButton = false;
            this.forcePause = true;
            this.absorbInputAroundWindow = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            // 标题
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleCenter;
            Rect titleRect = new Rect(0f, 0f, inRect.width, 30f);
            Widgets.Label(titleRect, "Agent 高级配置");
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;

            // 内容区域
            Rect contentRect = new Rect(0f, 35f, inRect.width, inRect.height - 80f);
            Rect viewRect = new Rect(0f, 0f, contentRect.width - 20f, 900f);
            
            Widgets.BeginScrollView(contentRect, ref scrollPosition, viewRect);
            
            float y = 0f;
            float width = viewRect.width;
            
            // ===== 1. LLM API 配置 =====
            y = DrawSectionCompact(viewRect, y, "LLM API 配置", (float startY) =>
            {
                float cy = startY;
                
                // 提供商选择 - 横向紧凑布局
                Rect providerRect = new Rect(0, cy, width, CompactRowHeight);
                Widgets.Label(new Rect(0, cy, LabelWidth, CompactRowHeight), "提供商:");
                
                float btnWidth = (width - LabelWidth - 10) / 4f;
                if (Widgets.ButtonText(new Rect(LabelWidth, cy, btnWidth - 2, CompactRowHeight - 2), "本地", true, true, settings.llmProvider == "local"))
                {
                    settings.llmProvider = "local";
                    settings.apiEndpoint = "http://localhost:1234/v1/chat/completions";
                    settings.modelName = "local-model";
                }
                if (Widgets.ButtonText(new Rect(LabelWidth + btnWidth, cy, btnWidth - 2, CompactRowHeight - 2), "OpenAI", true, true, settings.llmProvider == "openai"))
                {
                    settings.llmProvider = "openai";
                    settings.apiEndpoint = "https://api.openai.com/v1/chat/completions";
                    settings.modelName = "gpt-4";
                }
                if (Widgets.ButtonText(new Rect(LabelWidth + btnWidth * 2, cy, btnWidth - 2, CompactRowHeight - 2), "DeepSeek", true, true, settings.llmProvider == "deepseek"))
                {
                    settings.llmProvider = "deepseek";
                    settings.apiEndpoint = "https://api.deepseek.com/v1/chat/completions";
                    settings.modelName = "deepseek-chat";
                }
                if (Widgets.ButtonText(new Rect(LabelWidth + btnWidth * 3, cy, btnWidth - 2, CompactRowHeight - 2), "Gemini", true, true, settings.llmProvider == "gemini"))
                {
                    settings.llmProvider = "gemini";
                    settings.apiEndpoint = "https://generativelanguage.googleapis.com/v1/models/gemini-2.0-flash-exp:generateContent";
                    settings.modelName = "gemini-2.0-flash-exp";
                }
                cy += CompactRowHeight + 4;
                
                // API 端点
                cy = DrawCompactTextField(cy, width, "API 端点:", ref settings.apiEndpoint);
                
                // API 密钥
                cy = DrawCompactTextField(cy, width, "API 密钥:", ref settings.apiKey, true);
                
                // 模型名称
                cy = DrawCompactTextField(cy, width, "模型:", ref settings.modelName);
                
                // 温度和Token - 同一行
                Widgets.Label(new Rect(0, cy, LabelWidth, CompactRowHeight), $"温度: {settings.temperature:F2}");
                settings.temperature = Widgets.HorizontalSlider(new Rect(LabelWidth, cy + 4, (width - LabelWidth) / 2 - 10, 16f), settings.temperature, 0f, 2f);
                Widgets.Label(new Rect(width / 2, cy, 80, CompactRowHeight), $"Token: {settings.maxTokens}");
                settings.maxTokens = (int)Widgets.HorizontalSlider(new Rect(width / 2 + 80, cy + 4, width / 2 - 90, 16f), settings.maxTokens, 100, 2000);
                cy += CompactRowHeight + 4;
                
                // 保存按钮
                if (Widgets.ButtonText(new Rect(width - 80, cy, 75, 22), "保存 LLM"))
                {
                    SaveLLMSettings();
                }
                cy += 26;
                
                return cy;
            });
            
            // ===== 2. 多模态分析配置 =====
            y = DrawSectionCompact(viewRect, y, "多模态分析", (float startY) =>
            {
                float cy = startY;
                
                // 启用开关
                Rect checkRect = new Rect(0, cy, width, CompactRowHeight);
                Widgets.CheckboxLabeled(checkRect, "启用多模态分析", ref settings.enableMultimodalAnalysis);
                cy += CompactRowHeight;
                
                if (settings.enableMultimodalAnalysis)
                {
                    // 提供商
                    Widgets.Label(new Rect(0, cy, LabelWidth, CompactRowHeight), "提供商:");
                    float btnWidth = (width - LabelWidth - 10) / 3f;
                    if (Widgets.ButtonText(new Rect(LabelWidth, cy, btnWidth - 2, CompactRowHeight - 2), "OpenAI", true, true, settings.multimodalProvider == "openai"))
                        settings.multimodalProvider = "openai";
                    if (Widgets.ButtonText(new Rect(LabelWidth + btnWidth, cy, btnWidth - 2, CompactRowHeight - 2), "DeepSeek", true, true, settings.multimodalProvider == "deepseek"))
                        settings.multimodalProvider = "deepseek";
                    if (Widgets.ButtonText(new Rect(LabelWidth + btnWidth * 2, cy, btnWidth - 2, CompactRowHeight - 2), "Gemini", true, true, settings.multimodalProvider == "gemini"))
                        settings.multimodalProvider = "gemini";
                    cy += CompactRowHeight + 4;
                    
                    cy = DrawCompactTextField(cy, width, "API 密钥:", ref settings.multimodalApiKey, true);
                    cy = DrawCompactTextField(cy, width, "视觉模型:", ref settings.visionModel);
                    
                    // 保存按钮
                    if (Widgets.ButtonText(new Rect(width - 100, cy, 95, 22), "保存多模态"))
                    {
                        SaveMultimodalSettings();
                    }
                    cy += 26;
                }
                
                return cy;
            });
            
            // ===== 3. RimAgent 设置 =====
            y = DrawSectionCompact(viewRect, y, "RimAgent 设置", (float startY) =>
            {
                float cy = startY;
                
                // Agent 名称
                cy = DrawCompactTextField(cy, width, "Agent名称:", ref settings.agentName);
                
                // 重试和历史 - 同一行
                Widgets.Label(new Rect(0, cy, 80, CompactRowHeight), $"重试: {settings.maxRetries}");
                settings.maxRetries = (int)Widgets.HorizontalSlider(new Rect(80, cy + 4, 100, 16f), settings.maxRetries, 1, 10);
                Widgets.Label(new Rect(200, cy, 80, CompactRowHeight), $"延迟: {settings.retryDelay:F1}s");
                settings.retryDelay = Widgets.HorizontalSlider(new Rect(280, cy + 4, 100, 16f), settings.retryDelay, 0.5f, 10f);
                Widgets.Label(new Rect(400, cy, 80, CompactRowHeight), $"历史: {settings.maxHistoryMessages}");
                settings.maxHistoryMessages = (int)Widgets.HorizontalSlider(new Rect(480, cy + 4, width - 490, 16f), settings.maxHistoryMessages, 5, 100);
                cy += CompactRowHeight + 4;
                
                return cy;
            });
            
            // ===== 4. 并发管理设置 =====
            y = DrawSectionCompact(viewRect, y, "并发管理", (float startY) =>
            {
                float cy = startY;
                
                // 同一行布局
                Widgets.Label(new Rect(0, cy, 80, CompactRowHeight), $"并发: {settings.maxConcurrent}");
                settings.maxConcurrent = (int)Widgets.HorizontalSlider(new Rect(80, cy + 4, 120, 16f), settings.maxConcurrent, 1, 20);
                Widgets.Label(new Rect(220, cy, 100, CompactRowHeight), $"超时: {settings.requestTimeout}s");
                settings.requestTimeout = (int)Widgets.HorizontalSlider(new Rect(320, cy + 4, 120, 16f), settings.requestTimeout, 10, 300);
                Widgets.CheckboxLabeled(new Rect(460, cy, width - 470, CompactRowHeight), "启用重试", ref settings.enableRetry);
                cy += CompactRowHeight + 4;
                
                return cy;
            });
            
            // ===== 5. TTS 配置 =====
            y = DrawSectionCompact(viewRect, y, "TTS 配置", (float startY) =>
            {
                float cy = startY;
                
                // 启用开关
                Widgets.CheckboxLabeled(new Rect(0, cy, width / 2, CompactRowHeight), "启用 TTS", ref settings.enableTTS);
                Widgets.CheckboxLabeled(new Rect(width / 2, cy, width / 2, CompactRowHeight), "自动播放", ref settings.autoPlayTTS);
                cy += CompactRowHeight + 4;
                
                if (settings.enableTTS)
                {
                    // 提供商
                    Widgets.Label(new Rect(0, cy, LabelWidth, CompactRowHeight), "提供商:");
                    float btnWidth = (width - LabelWidth - 10) / 4f;
                    if (Widgets.ButtonText(new Rect(LabelWidth, cy, btnWidth - 2, CompactRowHeight - 2), "Edge", true, true, settings.ttsProvider == "edge"))
                        settings.ttsProvider = "edge";
                    if (Widgets.ButtonText(new Rect(LabelWidth + btnWidth, cy, btnWidth - 2, CompactRowHeight - 2), "Azure", true, true, settings.ttsProvider == "azure"))
                        settings.ttsProvider = "azure";
                    if (Widgets.ButtonText(new Rect(LabelWidth + btnWidth * 2, cy, btnWidth - 2, CompactRowHeight - 2), "本地", true, true, settings.ttsProvider == "local"))
                        settings.ttsProvider = "local";
                    if (Widgets.ButtonText(new Rect(LabelWidth + btnWidth * 3, cy, btnWidth - 2, CompactRowHeight - 2), "OpenAI", true, true, settings.ttsProvider == "openai"))
                        settings.ttsProvider = "openai";
                    cy += CompactRowHeight + 4;
                    
                    // Azure 配置（仅 Azure 提供商显示）
                    if (settings.ttsProvider == "azure")
                    {
                        cy = DrawCompactTextField(cy, width, "API 密钥:", ref settings.ttsApiKey, true);
                        cy = DrawCompactTextField(cy, width, "区域:", ref settings.ttsRegion);
                    }
                    
                    // 语速和音量 - 同一行
                    Widgets.Label(new Rect(0, cy, 80, CompactRowHeight), $"语速: {settings.ttsSpeechRate:F2}");
                    settings.ttsSpeechRate = Widgets.HorizontalSlider(new Rect(80, cy + 4, 150, 16f), settings.ttsSpeechRate, 0.5f, 2f);
                    Widgets.Label(new Rect(250, cy, 80, CompactRowHeight), $"音量: {settings.ttsVolume:P0}");
                    settings.ttsVolume = Widgets.HorizontalSlider(new Rect(330, cy + 4, 150, 16f), settings.ttsVolume, 0f, 1f);
                    cy += CompactRowHeight + 4;
                    
                    // 保存和测试按钮
                    if (Widgets.ButtonText(new Rect(width - 180, cy, 80, 22), "保存 TTS"))
                    {
                        SaveTTSSettings();
                    }
                    if (Widgets.ButtonText(new Rect(width - 90, cy, 85, 22), "测试 TTS"))
                    {
                        _ = TestTTSAsync();
                    }
                    cy += 26;
                }
                
                return cy;
            });
            
            Widgets.EndScrollView();
            
            // ===== 底部按钮 =====
            float btnY = inRect.height - 40f;
            if (Widgets.ButtonText(new Rect(10, btnY, 120, 32), "应用全部设置"))
            {
                ApplySettings();
                Messages.Message("Agent 配置已应用", MessageTypeDefOf.PositiveEvent);
            }
            
            if (Widgets.ButtonText(new Rect(140, btnY, 100, 32), "测试 LLM"))
            {
                _ = TestConnectionAsync();
            }
            
            if (Widgets.ButtonText(new Rect(250, btnY, 100, 32), "测试 TTS"))
            {
                _ = TestTTSAsync();
            }
            
            if (Widgets.ButtonText(new Rect(inRect.width - 80, btnY, 70, 32), "关闭"))
            {
                Close();
            }
        }
        
        /// <summary>
        /// 绘制紧凑型文本输入行
        /// </summary>
        private float DrawCompactTextField(float y, float width, string label, ref string value, bool isPassword = false)
        {
            Widgets.Label(new Rect(0, y, LabelWidth, CompactRowHeight), label);
            
            Rect inputRect = new Rect(LabelWidth, y, width - LabelWidth - 5, CompactRowHeight - 2);
            
            if (isPassword && !string.IsNullOrEmpty(value))
            {
                // 密码字段 - 显示遮罩但编辑时显示真实值
                string displayValue = new string('*', Math.Min(value.Length, 30));
                if (Mouse.IsOver(inputRect))
                {
                    value = Widgets.TextField(inputRect, value);
                }
                else
                {
                    Widgets.Label(inputRect, displayValue);
                    if (Widgets.ButtonInvisible(inputRect))
                    {
                        // 点击时启动编辑
                    }
                }
            }
            else
            {
                value = Widgets.TextField(inputRect, value ?? "");
            }
            
            return y + CompactRowHeight + 2;
        }
        
        /// <summary>
        /// 绘制紧凑型分组区域
        /// </summary>
        private float DrawSectionCompact(Rect viewRect, float startY, string title, Func<float, float> drawContent)
        {
            float y = startY;
            float width = viewRect.width;
            
            // 标题背景
            Rect titleRect = new Rect(0, y, width, 22f);
            Widgets.DrawBoxSolid(titleRect, new Color(0.15f, 0.15f, 0.15f, 0.8f));
            
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(8, y, width - 10, 22f), title);
            Text.Anchor = TextAnchor.UpperLeft;
            y += 24f;
            
            // 内容
            y = drawContent(y);
            
            y += 6f;
            
            return y;
        }
        
        /// <summary>
        /// 保存 LLM 设置
        /// </summary>
        private void SaveLLMSettings()
        {
            try
            {
                LLM.LLMService.Instance.Configure(
                    settings.apiEndpoint,
                    settings.apiKey,
                    settings.modelName,
                    settings.llmProvider
                );
                settings.Write();
                Messages.Message("LLM 设置已保存", MessageTypeDefOf.PositiveEvent);
            }
            catch (Exception ex)
            {
                Messages.Message($"保存失败: {ex.Message}", MessageTypeDefOf.NegativeEvent);
            }
        }
        
        /// <summary>
        /// 保存多模态设置
        /// </summary>
        private void SaveMultimodalSettings()
        {
            try
            {
                if (settings.enableMultimodalAnalysis)
                {
                    PersonaGeneration.MultimodalAnalysisService.Instance.Configure(
                        settings.multimodalProvider,
                        settings.multimodalApiKey,
                        settings.visionModel,
                        settings.textAnalysisModel
                    );
                }
                settings.Write();
                Messages.Message("多模态设置已保存", MessageTypeDefOf.PositiveEvent);
            }
            catch (Exception ex)
            {
                Messages.Message($"保存失败: {ex.Message}", MessageTypeDefOf.NegativeEvent);
            }
        }
        
        /// <summary>
        /// 应用并保存所有设置
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
                
                // 保存到磁盘
                settings.Write();
                
                Log.Message("[UnifiedAgentSettings] 配置已应用并保存");
            }
            catch (Exception ex)
            {
                Log.Error($"[UnifiedAgentSettings] 应用设置失败: {ex.Message}");
                Messages.Message($"应用设置失败: {ex.Message}", MessageTypeDefOf.NegativeEvent);
            }
        }
        
        /// <summary>
        /// 保存 TTS 设置
        /// </summary>
        private void SaveTTSSettings()
        {
            try
            {
                TTS.TTSService.Instance.Configure(
                    settings.ttsProvider,
                    settings.ttsApiKey,
                    settings.ttsRegion,
                    settings.ttsVoice,
                    settings.ttsSpeechRate,
                    settings.ttsVolume,
                    settings.ttsApiEndpoint,
                    settings.ttsModelName,
                    settings.ttsAudioUri
                );
                settings.Write();
                Log.Message($"[TTS Settings] Saved - Provider: {settings.ttsProvider}, AudioUri: {(string.IsNullOrEmpty(settings.ttsAudioUri) ? "none" : "set")}");
                Messages.Message("TTS 设置已保存并应用", MessageTypeDefOf.PositiveEvent);
            }
            catch (Exception ex)
            {
                Messages.Message($"保存 TTS 设置失败: {ex.Message}", MessageTypeDefOf.NegativeEvent);
            }
        }
        
        /// <summary>
        /// 测试 TTS
        /// </summary>
        private async System.Threading.Tasks.Task TestTTSAsync()
        {
            try
            {
                Messages.Message("正在测试 TTS...", MessageTypeDefOf.NeutralEvent);
                
                // 先保存当前设置
                TTS.TTSService.Instance.Configure(
                    settings.ttsProvider,
                    settings.ttsApiKey,
                    settings.ttsRegion,
                    settings.ttsVoice,
                    settings.ttsSpeechRate,
                    settings.ttsVolume,
                    settings.ttsApiEndpoint,
                    settings.ttsModelName,
                    settings.ttsAudioUri
                );
                
                string testText = "你好，这是语音测试。Hello, this is a voice test.";
                string filePath = await TTS.TTSService.Instance.SpeakAsync(testText);
                
                if (!string.IsNullOrEmpty(filePath))
                {
                    Messages.Message($"TTS 测试成功！音频已保存。", MessageTypeDefOf.PositiveEvent);
                }
                else
                {
                    Messages.Message("TTS 测试失败 - 未能生成音频", MessageTypeDefOf.NegativeEvent);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[Dialog_UnifiedAgentSettings] TestTTS failed: {ex.Message}");
                Messages.Message($"TTS 测试失败: {ex.Message}", MessageTypeDefOf.NegativeEvent);
            }
        }
        
        /// <summary>
        /// 测试 LLM 连接
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
