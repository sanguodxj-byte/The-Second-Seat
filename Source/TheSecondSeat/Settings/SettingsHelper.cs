using Verse;
using RimWorld;
using System;
using System.Collections.Generic;
using TheSecondSeat.WebSearch;

namespace TheSecondSeat.Settings
{
    /// <summary>
    /// 设置辅助类 - 配置和测试方法
    /// 提取自 ModSettings.cs
    /// </summary>
    public static class SettingsHelper
    {
        /// <summary>
        /// 配置网络搜索
        /// </summary>
        public static void ConfigureWebSearch(TheSecondSeatSettings settings)
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

            Log.Message($"[The Second Seat] Web search configured: {settings.searchEngine}");
        }
        
        /// <summary>
        /// 配置多模态分析
        /// </summary>
        public static void ConfigureMultimodalAnalysis(TheSecondSeatSettings settings)
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
                Log.Error($"[The Second Seat] Multimodal analysis configuration failed: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 配置 TTS
        /// </summary>
        public static void ConfigureTTS(TheSecondSeatSettings settings)
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
                Log.Error($"[The Second Seat] TTS configuration failed: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 测试连接
        /// </summary>
        public static async void TestConnection()
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
            catch (Exception ex)
            {
                Messages.Message($"Connection test failed: {ex.Message}", MessageTypeDefOf.NegativeEvent);
            }
        }
        
        /// <summary>
        /// 测试 TTS
        /// </summary>
        public static async void TestTTS()
        {
            try
            {
                Messages.Message("Testing TTS...", MessageTypeDefOf.NeutralEvent);
                
                string testText = "Hello, this is a voice test.";
                string? filePath = await TTS.TTSService.Instance.SpeakAsync(testText);
                
                if (!string.IsNullOrEmpty(filePath))
                {
                    Messages.Message("TTS test successful!", MessageTypeDefOf.PositiveEvent);
                }
                else
                {
                    Messages.Message("TTS test failed", MessageTypeDefOf.NegativeEvent);
                }
            }
            catch (Exception ex)
            {
                Messages.Message($"TTS test failed: {ex.Message}", MessageTypeDefOf.NegativeEvent);
            }
        }
        
        /// <summary>
        /// 显示语音选择菜单
        /// </summary>
        public static void ShowVoiceSelectionMenu(TheSecondSeatSettings settings)
        {
            var voices = TTS.TTSService.GetAvailableVoices();
            var options = new List<FloatMenuOption>();

            foreach (var voice in voices)
            {
                string voiceCopy = voice;
                options.Add(new FloatMenuOption(voice, () => {
                    settings.ttsVoice = voiceCopy;
                }));
            }

            Find.WindowStack.Add(new FloatMenu(options));
        }
        
        /// <summary>
        /// 获取示例全局提示词
        /// </summary>
        public static string GetExampleGlobalPrompt()
        {
            return "# 全局提示词示例\n\n" +
                   "你可以在这里添加全局指令来影响AI的行为。\n\n" +
                   "例如：\n" +
                   "- 使用友好轻松的语气\n" +
                   "- 优先考虑玩家的安全\n" +
                   "- 在危险情况下提供警告";
        }
    }
}
