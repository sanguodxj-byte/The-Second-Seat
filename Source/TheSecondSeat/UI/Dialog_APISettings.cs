using System;
using UnityEngine;
using Verse;
using RimWorld;
using TheSecondSeat.RimAgent;

namespace TheSecondSeat.UI
{
    /// <summary>
    /// ? v1.6.65: API 设置弹窗
    /// 包含 LLM API、TTS API 和并发管理器配置
    /// </summary>
    public class Dialog_APISettings : Window
    {
        private Vector2 scrollPosition = Vector2.zero;
        
        // LLM 设置
        private string llmProvider = "openai";
        private string llmApiKey = "";
        private string llmEndpoint = "https://api.openai.com/v1/chat/completions";
        private string llmModel = "gpt-4";
        
        // TTS 设置
        private string ttsProvider = "edge";
        private string ttsApiKey = "";
        private string ttsVoice = "zh-CN-XiaoxiaoNeural";
        
        // 并发管理设置
        private int maxConcurrent = 5;
        private int requestTimeout = 60;
        private bool enableRetry = true;
        
        public override Vector2 InitialSize => new Vector2(700f, 700f);

        public Dialog_APISettings()
        {
            doCloseButton = true;
            doCloseX = true;
            forcePause = true;
            absorbInputAroundWindow = true;
            
            LoadSettings();
        }

        private void LoadSettings()
        {
            var settings = LoadedModManager.GetMod<Settings.TheSecondSeatMod>()?
                .GetSettings<Settings.TheSecondSeatSettings>();
            
            if (settings != null)
            {
                // LLM
                llmProvider = settings.llmProvider ?? "openai";
                llmApiKey = settings.apiKey ?? "";
                llmEndpoint = settings.apiEndpoint ?? "https://api.openai.com/v1/chat/completions";
                llmModel = settings.modelName ?? "gpt-4";
                
                // TTS
                ttsProvider = settings.ttsProvider ?? "edge";
                ttsApiKey = settings.ttsApiKey ?? "";
                ttsVoice = settings.ttsVoice ?? "zh-CN-XiaoxiaoNeural";
                
                // 并发
                maxConcurrent = settings.maxConcurrent;
                requestTimeout = settings.requestTimeout;
                enableRetry = settings.enableRetry;
            }
        }

        private void SaveSettings()
        {
            var settings = LoadedModManager.GetMod<Settings.TheSecondSeatMod>()?
                .GetSettings<Settings.TheSecondSeatSettings>();
            
            if (settings != null)
            {
                // LLM
                settings.llmProvider = llmProvider;
                settings.apiKey = llmApiKey;
                settings.apiEndpoint = llmEndpoint;
                settings.modelName = llmModel;
                
                // TTS
                settings.ttsProvider = ttsProvider;
                settings.ttsApiKey = ttsApiKey;
                settings.ttsVoice = ttsVoice;
                
                // 并发
                settings.maxConcurrent = maxConcurrent;
                settings.requestTimeout = requestTimeout;
                settings.enableRetry = enableRetry;
                
                settings.Write();
                
                // 应用设置到服务
                ApplySettings();
            }
        }
        
        private void ApplySettings()
        {
            // 更新 LLM Service
            LLM.LLMService.Instance.Configure(llmEndpoint, llmApiKey, llmModel, llmProvider);
            
            // 更新 TTS Service
            TTS.TTSService.Instance.Configure(ttsProvider, ttsApiKey, ttsVoice);
            
            // 更新并发管理器
            ConcurrentRequestManager.Instance.UpdateSettings(maxConcurrent, requestTimeout, enableRetry);
        }

        public override void DoWindowContents(Rect inRect)
        {
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(inRect);
            
            // 标题
            Text.Font = GameFont.Medium;
            listing.Label("?? API 配置");
            Text.Font = GameFont.Small;
            listing.Gap(10f);
            
            // 滚动区域
            Rect scrollRect = new Rect(0f, listing.CurHeight, inRect.width, inRect.height - listing.CurHeight - 60f);
            Rect viewRect = new Rect(0f, 0f, scrollRect.width - 20f, 1000f);
            
            Widgets.BeginScrollView(scrollRect, ref scrollPosition, viewRect);
            
            Listing_Standard scrollListing = new Listing_Standard();
            scrollListing.Begin(viewRect);
            
            // ========================================
            // LLM API 配置
            // ========================================
            scrollListing.Label("?? LLM API 配置");
            scrollListing.GapLine(12f);
            
            // Provider 选择
            Rect providerRect = scrollListing.GetRect(30f);
            Widgets.Label(providerRect.LeftHalf(), "Provider:");
            
            Rect providerButtonRect = providerRect.RightHalf();
            if (Widgets.ButtonText(providerButtonRect, llmProvider))
            {
                var options = new System.Collections.Generic.List<FloatMenuOption>
                {
                    new FloatMenuOption("OpenAI", () => {
                        llmProvider = "openai";
                        llmEndpoint = "https://api.openai.com/v1/chat/completions";
                        llmModel = "gpt-4";
                    }),
                    new FloatMenuOption("DeepSeek", () => {
                        llmProvider = "deepseek";
                        llmEndpoint = "https://api.deepseek.com/v1/chat/completions";
                        llmModel = "deepseek-chat";
                    }),
                    new FloatMenuOption("Gemini", () => {
                        llmProvider = "gemini";
                        llmEndpoint = "https://generativelanguage.googleapis.com/v1/models";
                        llmModel = "gemini-pro";
                    }),
                    new FloatMenuOption("本地 LLM", () => {
                        llmProvider = "local";
                        llmEndpoint = "http://localhost:1234/v1/chat/completions";
                        llmModel = "local-model";
                    })
                };
                
                Find.WindowStack.Add(new FloatMenu(options));
            }
            scrollListing.Gap(5f);
            
            // API Key
            Rect keyRect = scrollListing.GetRect(30f);
            Widgets.Label(keyRect.LeftHalf(), "API Key:");
            llmApiKey = Widgets.TextField(keyRect.RightHalf(), llmApiKey);
            scrollListing.Gap(5f);
            
            // Endpoint
            Rect endpointRect = scrollListing.GetRect(30f);
            Widgets.Label(endpointRect.LeftHalf(), "Endpoint:");
            llmEndpoint = Widgets.TextField(endpointRect.RightHalf(), llmEndpoint);
            scrollListing.Gap(5f);
            
            // Model
            Rect modelRect = scrollListing.GetRect(30f);
            Widgets.Label(modelRect.LeftHalf(), "Model:");
            llmModel = Widgets.TextField(modelRect.RightHalf(), llmModel);
            scrollListing.Gap(5f);
            
            // 测试连接按钮
            if (scrollListing.ButtonText("?? 测试 LLM 连接", "测试 LLM API 是否可用"))
            {
                TestLLMConnection();
            }
            
            scrollListing.Gap(15f);
            
            // ========================================
            // TTS API 配置
            // ========================================
            scrollListing.Label("?? TTS API 配置");
            scrollListing.GapLine(12f);
            
            // TTS Provider 选择
            Rect ttsProviderRect = scrollListing.GetRect(30f);
            Widgets.Label(ttsProviderRect.LeftHalf(), "Provider:");
            
            Rect ttsProviderButtonRect = ttsProviderRect.RightHalf();
            if (Widgets.ButtonText(ttsProviderButtonRect, ttsProvider))
            {
                var options = new System.Collections.Generic.List<FloatMenuOption>
                {
                    new FloatMenuOption("Edge TTS (免费)", () => {
                        ttsProvider = "edge";
                        ttsVoice = "zh-CN-XiaoxiaoNeural";
                    }),
                    new FloatMenuOption("Azure TTS", () => {
                        ttsProvider = "azure";
                        ttsVoice = "zh-CN-XiaoxiaoNeural";
                    }),
                    new FloatMenuOption("OpenAI TTS", () => {
                        ttsProvider = "openai";
                        ttsVoice = "alloy";
                    })
                };
                
                Find.WindowStack.Add(new FloatMenu(options));
            }
            scrollListing.Gap(5f);
            
            // TTS API Key（某些 Provider 需要）
            if (ttsProvider != "edge")
            {
                Rect ttsKeyRect = scrollListing.GetRect(30f);
                Widgets.Label(ttsKeyRect.LeftHalf(), "API Key:");
                ttsApiKey = Widgets.TextField(ttsKeyRect.RightHalf(), ttsApiKey);
                scrollListing.Gap(5f);
            }
            
            // Voice
            Rect voiceRect = scrollListing.GetRect(30f);
            Widgets.Label(voiceRect.LeftHalf(), "Voice:");
            ttsVoice = Widgets.TextField(voiceRect.RightHalf(), ttsVoice);
            scrollListing.Gap(5f);
            
            // 测试 TTS 按钮
            if (scrollListing.ButtonText("?? 测试 TTS", "播放测试语音"))
            {
                TestTTS();
            }
            
            scrollListing.Gap(15f);
            
            // ========================================
            // 并发管理器配置
            // ========================================
            scrollListing.Label("? 并发管理器");
            scrollListing.GapLine(12f);
            
            // 最大并发数
            Rect concurrentRect = scrollListing.GetRect(30f);
            Widgets.Label(concurrentRect.LeftHalf(), $"最大并发数: {maxConcurrent}");
            maxConcurrent = (int)Widgets.HorizontalSlider(
                concurrentRect.RightHalf(), 
                maxConcurrent, 
                1f, 
                10f, 
                middleAlignment: true, 
                label: maxConcurrent.ToString()
            );
            scrollListing.Gap(5f);
            
            // 请求超时
            Rect timeoutRect = scrollListing.GetRect(30f);
            Widgets.Label(timeoutRect.LeftHalf(), $"请求超时(秒): {requestTimeout}");
            requestTimeout = (int)Widgets.HorizontalSlider(
                timeoutRect.RightHalf(), 
                requestTimeout, 
                30f, 
                120f, 
                middleAlignment: true, 
                label: $"{requestTimeout}s"
            );
            scrollListing.Gap(5f);
            
            // 启用重试
            Rect retryRect = scrollListing.GetRect(30f);
            Widgets.Label(retryRect.LeftHalf(), "启用自动重试:");
            Widgets.Checkbox(retryRect.RightHalf().position, ref enableRetry);
            scrollListing.Gap(10f);
            
            // 并发管理器统计
            scrollListing.Label("?? 并发统计:");
            try
            {
                string stats = ConcurrentRequestManager.Instance.GetStats();
                
                Rect statsRect = scrollListing.GetRect(60f);
                Widgets.DrawBoxSolid(statsRect, new Color(0.1f, 0.1f, 0.1f, 0.5f));
                Text.Font = GameFont.Tiny;
                Widgets.Label(statsRect.ContractedBy(5f), stats);
                Text.Font = GameFont.Small;
                
                scrollListing.Gap(10f);
                
                // 重置统计按钮
                if (scrollListing.ButtonText("?? 重置统计", "清除并发管理器统计"))
                {
                    ConcurrentRequestManager.Instance.ResetStats();
                    Messages.Message("并发统计已重置", MessageTypeDefOf.PositiveEvent);
                }
            }
            catch (Exception ex)
            {
                scrollListing.Label($"? 获取统计失败: {ex.Message}");
            }
            
            scrollListing.End();
            Widgets.EndScrollView();
            
            // 底部按钮
            listing.End();
            
            Rect bottomRect = new Rect(inRect.x, inRect.yMax - 50f, inRect.width, 50f);
            
            // 保存并应用按钮
            if (Widgets.ButtonText(new Rect(bottomRect.x, bottomRect.y, 150f, 35f), "?? 保存并应用"))
            {
                SaveSettings();
                Messages.Message("API 设置已保存并应用", MessageTypeDefOf.PositiveEvent);
            }
            
            // 关闭按钮
            if (Widgets.ButtonText(new Rect(bottomRect.xMax - 150f, bottomRect.y, 150f, 35f), "关闭"))
            {
                Close();
            }
        }
        
        private async void TestLLMConnection()
        {
            Messages.Message("正在测试 LLM 连接...", MessageTypeDefOf.NeutralEvent);
            
            try
            {
                // 临时应用设置
                LLM.LLMService.Instance.Configure(llmEndpoint, llmApiKey, llmModel, llmProvider);
                
                bool success = await LLM.LLMService.Instance.TestConnectionAsync();
                
                if (success)
                {
                    Messages.Message("? LLM 连接测试成功！", MessageTypeDefOf.PositiveEvent);
                }
                else
                {
                    Messages.Message("? LLM 连接测试失败，请检查配置", MessageTypeDefOf.RejectInput);
                }
            }
            catch (Exception ex)
            {
                Messages.Message($"? 测试异常: {ex.Message}", MessageTypeDefOf.RejectInput);
                Log.Error($"[API Settings] LLM test failed: {ex.Message}");
            }
        }
        
        private async void TestTTS()
        {
            Messages.Message("正在测试 TTS...", MessageTypeDefOf.NeutralEvent);
            
            try
            {
                // 临时应用设置
                TTS.TTSService.Instance.Configure(ttsProvider, ttsApiKey, ttsVoice);
                
                // 播放测试语音（使用 SpeakAsync 而不是 SynthesizeSpeechAsync）
                string testText = "这是一条测试语音消息。";
                string? filePath = await TTS.TTSService.Instance.SpeakAsync(testText);
                
                if (!string.IsNullOrEmpty(filePath))
                {
                    Messages.Message("? TTS 测试完成！", MessageTypeDefOf.PositiveEvent);
                }
                else
                {
                    Messages.Message("? TTS 测试失败", MessageTypeDefOf.RejectInput);
                }
            }
            catch (Exception ex)
            {
                Messages.Message($"? TTS 测试失败: {ex.Message}", MessageTypeDefOf.RejectInput);
                Log.Error($"[API Settings] TTS test failed: {ex.Message}");
            }
        }
    }
}
