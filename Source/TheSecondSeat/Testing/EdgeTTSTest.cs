using System;
using System.IO;
using System.Threading.Tasks;
using Verse;
using TheSecondSeat.TTS;

namespace TheSecondSeat.Testing
{
    /// <summary>
    /// Edge TTS 测试类
    /// 用于验证 WebSocket 连接和音频生成
    /// </summary>
    public static class EdgeTTSTest
    {
        /// <summary>
        /// 运行 Edge TTS 测试
        /// 可以在游戏中通过开发者控制台调用
        /// </summary>
        public static async void RunTest()
        {
            Log.Message("[EdgeTTSTest] 开始测试 Edge TTS...");
            
            try
            {
                using (var client = new EdgeTTSWebSocketClient())
                {
                    string testText = "你好，这是一个测试。Edge TTS 正在工作。";
                    string voice = "zh-CN-XiaoxiaoNeural";
                    
                    Log.Message($"[EdgeTTSTest] 测试文本: {testText}");
                    Log.Message($"[EdgeTTSTest] 语音: {voice}");
                    
                    var startTime = DateTime.Now;
                    byte[] audioData = await client.SynthesizeAsync(testText, voice, "+0%", "+0%");
                    var elapsed = DateTime.Now - startTime;
                    
                    if (audioData != null && audioData.Length > 0)
                    {
                        Log.Message($"[EdgeTTSTest] ✓ 成功! 生成了 {audioData.Length} 字节的音频数据");
                        Log.Message($"[EdgeTTSTest] 耗时: {elapsed.TotalSeconds:F2} 秒");
                        
                        // 保存测试文件
                        string testPath = Path.Combine(GenFilePaths.SaveDataFolderPath, "TheSecondSeat", "edge_tts_test.mp3");
                        Directory.CreateDirectory(Path.GetDirectoryName(testPath));
                        File.WriteAllBytes(testPath, audioData);
                        Log.Message($"[EdgeTTSTest] 测试音频已保存到: {testPath}");
                        
                        // 尝试播放
                        Log.Message("[EdgeTTSTest] 尝试播放音频...");
                        TTSAudioPlayer.Instance.PlayAndDelete(testPath, "", () => {
                            Log.Message("[EdgeTTSTest] 播放完成!");
                        });
                    }
                    else
                    {
                        Log.Error("[EdgeTTSTest] ✗ 失败! 未能生成音频数据");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[EdgeTTSTest] ✗ 异常: {ex.Message}");
                Log.Error($"[EdgeTTSTest] 堆栈: {ex.StackTrace}");
            }
        }
        
        /// <summary>
        /// 测试多种语音
        /// </summary>
        public static async void TestMultipleVoices()
        {
            Log.Message("[EdgeTTSTest] 测试多种语音...");
            
            var voices = new[]
            {
                ("zh-CN-XiaoxiaoNeural", "你好，我是晓晓。"),
                ("zh-CN-YunxiNeural", "你好，我是云希。"),
                ("en-US-JennyNeural", "Hello, I am Jenny."),
                ("ja-JP-NanamiNeural", "こんにちは、私は七海です。"),
            };
            
            foreach (var (voice, text) in voices)
            {
                try
                {
                    using (var client = new EdgeTTSWebSocketClient())
                    {
                        Log.Message($"[EdgeTTSTest] 测试语音: {voice}");
                        byte[] audioData = await client.SynthesizeAsync(text, voice, "+0%", "+0%");
                        
                        if (audioData != null && audioData.Length > 0)
                        {
                            Log.Message($"[EdgeTTSTest] ✓ {voice}: {audioData.Length} 字节");
                        }
                        else
                        {
                            Log.Warning($"[EdgeTTSTest] ✗ {voice}: 失败");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[EdgeTTSTest] ✗ {voice}: {ex.Message}");
                }
                
                // 避免请求过快
                await Task.Delay(1000);
            }
            
            Log.Message("[EdgeTTSTest] 多语音测试完成");
        }
    }
}
