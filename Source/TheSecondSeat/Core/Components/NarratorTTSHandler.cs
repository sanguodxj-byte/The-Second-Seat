using System;
using System.Threading.Tasks;
using TheSecondSeat.Narrator;
using TheSecondSeat.TTS;
using TheSecondSeat.UI;
using Verse;

namespace TheSecondSeat.Core.Components
{
    /// <summary>
    /// Handles TTS generation and playback synchronization
    /// </summary>
    public class NarratorTTSHandler
    {
        private readonly NarratorManager? narratorManager;

        public NarratorTTSHandler(NarratorManager? manager)
        {
            narratorManager = manager;
        }

        /// <summary>
        /// ⭐ 自动播放 TTS（叙事者发言时）
        /// ⭐ v1.6.90: 修复 TTS 与对话同步 - 对话框等待 TTS 加载完成后同时开始
        /// - 启用 TTS 时：由 TTSAudioPlayer 在音频开始播放时触发 StartStreaming(clip.length)
        /// - 禁用 TTS 时：使用估算时长立即开始流式显示
        /// </summary>
        public void AutoPlayTTS(string text, string emoticonId = "")
        {
            try
            {
                // 清除动作标记（括号内的内容）- 提前计算以便估算时长
                string cleanText = System.Text.RegularExpressions.Regex.Replace(text, @"\([^)]*\)", "").Trim();
                
                // ⭐ 估算文本阅读时长（中文约每秒5个字符，最少2秒）
                float estimatedDuration = Math.Max(2f, cleanText.Length / 5f);
                
                // 检查是否启用 TTS
                var modSettings = LoadedModManager.GetMod<Settings.TheSecondSeatMod>()?.GetSettings<Settings.TheSecondSeatSettings>();
                
                if (modSettings == null || !modSettings.enableTTS || !modSettings.autoPlayTTS)
                {
                    // ⭐ TTS 未启用，使用估算时长开始流式显示
                    Log.Message($"[NarratorController] TTS disabled. Streaming with estimated duration: {estimatedDuration:F2}s");
                    NarratorWindow.AddAIMessage(text, emoticonId); // 立即显示
                    DialogueOverlayPanel.StartStreaming(estimatedDuration);
                    return;
                }
                
                if (string.IsNullOrEmpty(cleanText))
                {
                    // 没有语音内容，使用最短时长显示
                    NarratorWindow.AddAIMessage(text, emoticonId); // 立即显示
                    DialogueOverlayPanel.StartStreaming(1f);
                    return;
                }
                
                // 获取当前叙事者 defName
                string personaDefName = "Cassandra_Classic";
                if (narratorManager != null)
                {
                    var persona = narratorManager.GetCurrentPersona();
                    if (persona != null)
                    {
                        personaDefName = persona.defName;
                    }
                }
                
                // 显示加载提示（使用 Log 而非 Message 避免打扰玩家）
                Log.Message("[NarratorTTSHandler] 正在生成语音...");
                
                // ⭐ 在后台线程生成 TTS 音频
                // 注意：此时不调用 StartStreaming！等待 TTSAudioPlayer 在音频播放时触发
                Task.Run(async () =>
                {
                    try
                    {
                        string? audioPath = await TTSService.Instance.SpeakAsync(cleanText, personaDefName);
                        
                        if (!string.IsNullOrEmpty(audioPath))
                        {
                            Log.Message($"[NarratorController] TTS 音频生成完成: {audioPath} (Persona: {personaDefName})");
                            
                            // ⭐ 在主线程播放音频
                            // TTSAudioPlayer.PlayAndDelete 内部会在音频开始播放时调用 StartStreaming(clip.length)
                            LongEventHandler.ExecuteWhenFinished(() =>
                            {
                                try
                                {
                                    // ⭐ 播放音频 - TTSAudioPlayer 会自动触发 StartStreaming(clip.length)
                                    NarratorWindow.AddAIMessage(text, emoticonId); // 播放时显示
                                    TTSAudioPlayer.Instance.PlayAndDelete(audioPath, personaDefName);
                                }
                                catch (Exception playEx)
                                {
                                    Log.Error($"[NarratorController] TTS playback failed: {playEx.Message}");
                                    // ⭐ 出错时使用估算时长回退
                                    float fallbackDuration = Math.Max(2f, cleanText.Length / 5f);
                                    Log.Warning($"[NarratorController] TTS playback failed. Falling back to estimated duration: {fallbackDuration:F2}s");
                                    NarratorWindow.AddAIMessage(text, emoticonId); // 回退时显示
                                    DialogueOverlayPanel.StartStreaming(fallbackDuration);
                                }
                            });
                        }
                        else
                        {
                            // ⭐ TTS 生成失败，使用估算时长回退
                            LongEventHandler.ExecuteWhenFinished(() =>
                            {
                                Log.Warning("[NarratorController] TTS audio generation returned null. Falling back to estimated duration.");
                                float fallbackDuration = Math.Max(2f, cleanText.Length / 5f);
                                NarratorWindow.AddAIMessage(text, emoticonId); // 回退时显示
                                DialogueOverlayPanel.StartStreaming(fallbackDuration);
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"[NarratorController] TTS processing task failed: {ex.Message}");
                        
                        // ⭐ TTS 异常，使用估算时长回退
                        LongEventHandler.ExecuteWhenFinished(() =>
                        {
                            float fallbackDuration = Math.Max(2f, cleanText.Length / 5f);
                            Log.Warning($"[NarratorController] TTS task failed. Falling back to estimated duration: {fallbackDuration:F2}s");
                            NarratorWindow.AddAIMessage(text, emoticonId); // 回退时显示
                            DialogueOverlayPanel.StartStreaming(fallbackDuration);
                        });
                    }
                });
            }
            catch (Exception ex)
            {
                Log.Warning($"[NarratorController] AutoPlayTTS 异常: {ex.Message}");
                // ⭐ 最外层异常使用最小时长回退
                NarratorWindow.AddAIMessage(text, emoticonId); // 异常时显示
                DialogueOverlayPanel.StartStreaming(2f);
            }
        }
    }
}
