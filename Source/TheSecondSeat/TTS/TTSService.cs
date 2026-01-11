using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine.Networking;
using Verse;

namespace TheSecondSeat.TTS
{
    /// <summary>
    /// TTS（文本转语音）服务
    /// 支持多个 TTS 提供商：Azure TTS, Edge TTS, Local TTS
    /// 注意：RimWorld 不直接支持音频播放，TTS 音频将保存为文件
    /// </summary>
    public class TTSService
    {
        private static TTSService? instance;
        public static TTSService Instance => instance ??= new TTSService();

        private string ttsProvider = "edge"; // "azure", "edge", "local", "openai"
        private string apiKey = "";
        private string apiRegion = "eastus";
        private string voiceName = "zh-CN-XiaoxiaoNeural"; // 默认中文女声
        private float speechRate = 1.0f;
        private float volume = 1.0f;

        // ? 新增：OpenAI 兼容接口配置
        private string openAI_ApiUrl = "http://127.0.0.1:9880/v1/audio/speech"; // 默认本地 GPT-SoVITS
        private string openAI_Model = "gpt-sovits"; // 模型名称

        // ? 新增：SiliconFlow (IndexTTS) 配置
        private string siliconFlow_ApiUrl = "https://api.siliconflow.cn/v1/audio/speech";
        private string siliconFlow_Model = "IndexTeam/IndexTTS-2";

        private string audioOutputDir = "";
        
        // ? 语音播放状态（用于唇形同步）
        public bool IsSpeaking { get; private set; } = false;

        public TTSService()
        {
            // 创建音频输出目录
            audioOutputDir = Path.Combine(GenFilePaths.SaveDataFolderPath, "TheSecondSeat", "TTS");
            if (!Directory.Exists(audioOutputDir))
            {
                Directory.CreateDirectory(audioOutputDir);
            }
        }

        /// <summary>
        /// 配置 TTS 服务
        /// v2.0.1: 新增 apiUrl 和 modelName 参数，支持 OpenAI 兼容接口
        /// </summary>
        /// <param name="provider">TTS 提供商: "azure", "edge", "local", "openai"</param>
        /// <param name="key">API 密钥（可选）</param>
        /// <param name="region">区域（Azure 专用）</param>
        /// <param name="voice">语音名称</param>
        /// <param name="rate">语速（0.5-2.0）</param>
        /// <param name="vol">音量（0.0-1.0）</param>
        /// <param name="apiUrl">API 端点 URL（OpenAI 专用，可选）</param>
        /// <param name="modelName">模型名称（OpenAI 专用，可选）</param>
        public void Configure(
            string provider,
            string key = "",
            string region = "eastus",
            string voice = "zh-CN-XiaoxiaoNeural",
            float rate = 1.0f,
            float vol = 1.0f,
            string apiUrl = "",
            string modelName = "")
        {
            ttsProvider = provider;
            apiKey = key;
            apiRegion = region;
            voiceName = voice;
            speechRate = UnityEngine.Mathf.Clamp(rate, 0.5f, 2.0f);
            volume = UnityEngine.Mathf.Clamp(vol, 0.0f, 1.0f);

            // 配置 OpenAI 兼容接口
            if (ttsProvider == "openai")
            {
                if (!string.IsNullOrEmpty(apiUrl))
                {
                    openAI_ApiUrl = apiUrl;
                }
                
                if (!string.IsNullOrEmpty(modelName))
                {
                    openAI_Model = modelName;
                }
            }
            // 配置 SiliconFlow
            else if (ttsProvider == "siliconflow")
            {
                // 允许覆盖默认 URL 和 Model (虽然通常固定)
                if (!string.IsNullOrEmpty(apiUrl)) siliconFlow_ApiUrl = apiUrl;
                if (!string.IsNullOrEmpty(modelName)) siliconFlow_Model = modelName;
            }
        }

        /// <summary>
        /// 将文本转换为语音并保存为文件
        /// ⭐ v1.6.74: 添加 personaDefName 参数，用于口型同步追踪和 Viseme 序列推送
        /// </summary>
        /// <param name="text">要转换的文本</param>
        /// <param name="personaDefName">人格 DefName（用于状态追踪，可选）</param>
        /// <returns>生成的音频文件路径，失败则返回 null</returns>
        public async Task<string?> SpeakAsync(string text, string personaDefName = "")
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                if (Prefs.DevMode)
                {
                    Log.Warning("[TTSService] Text is empty");
                }
                return null;
            }

            try
            {
                // 清理文本：移除括号内的动作和表情描写
                string cleanText = CleanTextForTTS(text);
                
                if (string.IsNullOrWhiteSpace(cleanText))
                {
                    if (Prefs.DevMode)
                    {
                        Log.Warning("[TTSService] Text is empty after cleaning");
                    }
                    return null;
                }
                
                // 生成 WAV 文件名
                string fileName = $"tts_{DateTime.Now:yyyyMMdd_HHmmss}.wav";
                string filePath = Path.Combine(audioOutputDir, fileName);

                byte[]? audioData = null;

                // ⭐ v1.6.74: 根据提供商选择生成方式（传递 personaDefName）
                switch (ttsProvider.ToLower())
                {
                    case "azure":
                        audioData = await GenerateAzureTTSAsync(cleanText, personaDefName);
                        break;
                    case "local":
                        audioData = await GenerateLocalTTSAsync(cleanText);
                        break;
                    case "edge":
                        audioData = await GenerateEdgeTTSAsync(cleanText);
                        break;
                    case "openai":
                        audioData = await GenerateOpenAITTSAsync(cleanText);
                        break;
                    case "siliconflow":
                        audioData = await GenerateSiliconFlowTTSAsync(cleanText);
                        break;
                    default:
                        if (Prefs.DevMode)
                        {
                            Log.Error($"[TTSService] Unknown provider: {ttsProvider}");
                        }
                        return null;
                }

                if (audioData == null || audioData.Length == 0)
                {
                    if (Prefs.DevMode)
                    {
                        Log.Error($"[TTSService] Failed to generate audio with {ttsProvider}");
                    }
                    return null;
                }

                // 保存音频文件
                File.WriteAllBytes(filePath, audioData);
                
                // 静默保存

                // ⭐ v1.8.6: 统一生成模拟 Viseme 序列（如果提供了 personaDefName）
                // 确保所有 TTS Provider (Azure, Edge, Local, OpenAI) 都能有口型动画
                if (!string.IsNullOrEmpty(personaDefName))
                {
                    GenerateSimpleVisemeSequence(cleanText, personaDefName);
                }

                // 在主线程自动打开音频播放器（传递 personaDefName）
                string capturedPersonaDefName = personaDefName;  // 捕获变量用于闭包
                Verse.LongEventHandler.ExecuteWhenFinished(() =>
                {
                    AutoPlayAudioFile(filePath, capturedPersonaDefName);
                });

                return filePath;
            }
            catch (Exception ex)
            {
                // 仅在 DevMode 昒示详细错误
                if (Prefs.DevMode)
                {
                    Log.Error($"[TTSService] Error: {ex.Message}");
                }
                return null;
            }
        }

        /// <summary>
        /// 生成本地 TTS (使用 System.Speech)
        /// </summary>
        private Task<byte[]?> GenerateLocalTTSAsync(string text)
        {
            return Task.Run(() =>
            {
                try
                {
                    // 使用反射加载 System.Speech，避免硬依赖
                    var assembly = System.Reflection.Assembly.Load("System.Speech, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35");
                    var synthesizerType = assembly.GetType("System.Speech.Synthesis.SpeechSynthesizer");
                    var synthesizer = Activator.CreateInstance(synthesizerType);

                    using (var stream = new MemoryStream())
                    {
                        // 设置输出到流
                        synthesizerType.GetMethod("SetOutputToWaveStream", new[] { typeof(Stream) })
                            .Invoke(synthesizer, new object[] { stream });

                        // 设置音量 (0-100)
                        synthesizerType.GetMethod("set_Volume").Invoke(synthesizer, new object[] { (int)(volume * 100) });

                        // 设置语速 (-10 to 10)
                        int rate = (int)((speechRate - 1.0f) * 10);
                        synthesizerType.GetMethod("set_Rate").Invoke(synthesizer, new object[] { rate });

                        // 合成语音
                        synthesizerType.GetMethod("Speak", new[] { typeof(string) })
                            .Invoke(synthesizer, new object[] { text });

                        return stream.ToArray();
                    }
                }
                catch (Exception ex)
                {
                    // 仅在 DevMode 显示错误
                    if (Prefs.DevMode)
                    {
                        Log.Error($"[TTSService] Local TTS error: {ex.Message}");
                    }
                    return null;
                }
            });
        }

        /// <summary>
        /// 生成 Edge TTS (占位符/简单实现)
        /// </summary>
        private Task<byte[]?> GenerateEdgeTTSAsync(string text)
        {
            // 仅在 DevMode 显示提示信息
            if (Prefs.DevMode)
            {
                Log.Warning("[TTSService] Edge TTS implementation requires WebSocket support which is complex to embed.");
                Log.Warning("[TTSService] Please use Azure TTS or Local TTS for now.");
            }
            return Task.FromResult<byte[]?>(null);
        }

        /// <summary>
        /// ? 生成 SiliconFlow (IndexTTS) 语音
        /// 兼容 OpenAI Speech API 格式
        /// </summary>
        private async Task<byte[]?> GenerateSiliconFlowTTSAsync(string text)
        {
            try
            {
                // IndexTTS 特有参数处理
                // 注意：SiliconFlow 文档可能要求特定的 voice 名称
                // 如果 voiceName 是 Azure 格式 (zh-CN-...), 可能需要映射或直接使用
                // IndexTTS 通常支持 "alex", "anna" 等，或者特定中文名。
                // 暂时直接透传 voiceName，由用户在设置中配置正确的 IndexTTS 语音名。

                var requestBody = new
                {
                    model = siliconFlow_Model,      // "IndexTeam/IndexTTS-2"
                    input = text,
                    voice = voiceName,              // e.g. "alex"
                    response_format = "wav",        // 推荐 wav 以获得最佳兼容性
                    speed = speechRate,
                    sample_rate = 24000             // IndexTTS 可能支持采样率设置
                };

                string jsonBody = JsonConvert.SerializeObject(requestBody);

                using var webRequest = new UnityWebRequest(siliconFlow_ApiUrl, "POST");
                webRequest.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(jsonBody));
                webRequest.downloadHandler = new DownloadHandlerBuffer();
                webRequest.SetRequestHeader("Content-Type", "application/json");
                
                if (!string.IsNullOrEmpty(apiKey))
                {
                    webRequest.SetRequestHeader("Authorization", $"Bearer {apiKey}");
                }

                var op = webRequest.SendWebRequest();
                while (!op.isDone) await Task.Delay(50);

                if (webRequest.result != UnityWebRequest.Result.Success)
                {
                    if (Prefs.DevMode)
                    {
                        Log.Error($"[TTSService] SiliconFlow error: {webRequest.responseCode} - {webRequest.error}");
                        Log.Error($"[TTSService] Details: {webRequest.downloadHandler.text}");
                    }
                    return null;
                }

                return webRequest.downloadHandler.data;
            }
            catch (Exception ex)
            {
                if (Prefs.DevMode)
                {
                    Log.Error($"[TTSService] SiliconFlow exception: {ex.Message}");
                }
                return null;
            }
        }

        /// <summary>
        /// ? 生成 OpenAI 兼容的 TTS (如 GPT-SoVITS)
        /// 使用 OpenAI Speech API 格式：
        /// POST /v1/audio/speech
        /// Body: { "model": "gpt-sovits", "input": "文本", "voice": "zh-CN-XiaoxiaoNeural", "response_format": "wav" }
        /// </summary>
        private async Task<byte[]?> GenerateOpenAITTSAsync(string text)
        {
            try
            {

                // 构建 OpenAI Speech API 兼容的请求体
                var requestBody = new
                {
                    model = openAI_Model,           // 模型名称（如 "gpt-sovits"）
                    input = text,                   // 要合成的文本
                    voice = voiceName,              // 语音名称
                    response_format = "wav",        // 输出格式（WAV）
                    speed = speechRate              // 语速（可选）
                };

                string jsonBody = JsonConvert.SerializeObject(requestBody);
                

                // 使用 UnityWebRequest
                using var webRequest = new UnityWebRequest(openAI_ApiUrl, "POST");
                webRequest.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(jsonBody));
                webRequest.downloadHandler = new DownloadHandlerBuffer();
                webRequest.SetRequestHeader("Content-Type", "application/json");
                
                if (!string.IsNullOrEmpty(apiKey))
                {
                    webRequest.SetRequestHeader("Authorization", $"Bearer {apiKey}");
                }

                var op = webRequest.SendWebRequest();
                while (!op.isDone) await Task.Delay(50);

                if (webRequest.result != UnityWebRequest.Result.Success)
                {
                    // 仅在 DevMode 显示详细错误
                    if (Prefs.DevMode)
                    {
                        Log.Error($"[TTSService] OpenAI TTS error: {webRequest.responseCode} - {webRequest.error}");
                        Log.Error($"[TTSService] Error details: {webRequest.downloadHandler.text}");
                        Log.Error($"[TTSService] Request URL: {openAI_ApiUrl}");
                    }
                    return null;
                }

                // 读取音频数据（应该是 WAV 格式）
                byte[] audioData = webRequest.downloadHandler.data;
                
                
                return audioData;
            }
            catch (Exception ex)
            {
                // 仅在 DevMode 显示异常堆栈
                if (Prefs.DevMode)
                {
                    Log.Error($"[TTSService] OpenAI TTS exception: {ex.Message}");
                    Log.Error($"[TTSService] Stack trace: {ex.StackTrace}");
                }
                return null;
            }
        }

        /// <summary>
        /// 自动打开音频播放器播放文件
        /// ? v1.6.30: 添加 personaDefName 参数，用于口型同步追踪
        /// </summary>
        private void AutoPlayAudioFile(string filePath, string personaDefName = "")
        {
            try
            {
                // 检查是否启用自动播放
                var modSettings = LoadedModManager.GetMod<Settings.TheSecondSeatMod>()?.GetSettings<Settings.TheSecondSeatSettings>();
                
                if (modSettings == null || !modSettings.autoPlayTTS)
                {
                    return; // 未启用自动播放
                }
                
                if (!File.Exists(filePath))
                {
                    if (Prefs.DevMode)
                    {
                        Log.Warning($"[TTSService] Audio file not found: {filePath}");
                    }
                    return;
                }

                // 读取音频文件字节
                byte[] audioData = File.ReadAllBytes(filePath);
                
                // 播放前：设置正在说话状态
                IsSpeaking = true;
                
                // v1.6.30: 使用 Unity AudioSource 播放，并传递 personaDefName
                TTSAudioPlayer.Instance.PlayFromBytes(audioData, personaDefName, () => {
                    // 播放结束：清除正在说话状态
                    IsSpeaking = false;
                    // 静默完成
                });
                
                // 静默播放
            }
            catch (Exception ex)
            {
                // 仅在 DevMode 显示警告
                if (Prefs.DevMode)
                {
                    Log.Warning($"[TTSService] Failed to auto-play audio: {ex.Message}");
                }
                // 异常时也要重置状态
                IsSpeaking = false;
            }
        }

        /// <summary>
        /// 清理文本用于 TTS：移除括号内的动作和表情描写
        /// ⭐ v1.6.75: 修复 - 支持所有括号类型
        /// </summary>
        private string CleanTextForTTS(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return text;
            }

            // ⭐ v1.6.75: 移除所有类型的括号内容
            // (动作描写)、（中文括号）、[旁白]、【注释】、<标记>
            text = System.Text.RegularExpressions.Regex.Replace(text, @"\([^)]*\)", "");      // (英文括号)
            text = System.Text.RegularExpressions.Regex.Replace(text, @"（[^）]*）", "");      // （中文括号）
            text = System.Text.RegularExpressions.Regex.Replace(text, @"\[[^\]]*\]", "");     // [方括号] ⭐ 新增
            text = System.Text.RegularExpressions.Regex.Replace(text, @"【[^】]*】", "");      // 【中文方括号】
            text = System.Text.RegularExpressions.Regex.Replace(text, @"<[^>]*>", "");        // <尖括号>
            text = System.Text.RegularExpressions.Regex.Replace(text, @"\*[^*]*\*", "");      // *星号* ⭐ 新增（Markdown 动作）
            
            // ⭐ 移除多余的空格和换行
            text = System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ");
            
            // ⭐ 移除首尾空格
            text = text.Trim();
            
            return text;
        }

        /// <summary>
        /// ⭐ v1.6.74: 使用 Azure TTS 生成语音（支持 Viseme 数据）
        /// 注意：Azure TTS REST API 不直接返回 Viseme 事件
        /// 需要使用 WebSocket 或 Speech SDK 才能获取实时 Viseme
        /// 当前实现：仅生成音频，Viseme 支持待后续升级
        /// </summary>
        private async Task<byte[]?> GenerateAzureTTSAsync(string text, string personaDefName = "")
        {
            if (string.IsNullOrEmpty(apiKey))
            {
                if (Prefs.DevMode)
                {
                    Log.Warning("[TTSService] Azure TTS API key is not configured");
                }
                return null;
            }

            try
            {
                string endpoint = $"https://{apiRegion}.tts.speech.microsoft.com/cognitiveservices/v1";

                // 构建 SSML
                string ssml = BuildSSML(text, voiceName, speechRate);
                

                // 使用 UnityWebRequest
                using var webRequest = new UnityWebRequest(endpoint, "POST");
                webRequest.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(ssml));
                webRequest.downloadHandler = new DownloadHandlerBuffer();
                
                // 设置 Header
                webRequest.SetRequestHeader("Content-Type", "application/ssml+xml");
                webRequest.SetRequestHeader("Ocp-Apim-Subscription-Key", apiKey);
                webRequest.SetRequestHeader("X-Microsoft-OutputFormat", "riff-48khz-16bit-mono-pcm");
                webRequest.SetRequestHeader("User-Agent", "RimWorld-TheSecondSeat-TTS");

                // ⭐ v1.6.74: TODO - Azure TTS Viseme 支持
                // Azure TTS REST API 不支持直接返回 Viseme 事件
                // 需要使用以下方式之一：
                // 1. WebSocket API (wss://{region}.tts.speech.microsoft.com/cognitiveservices/websocket/v1)
                // 2. Speech SDK (Microsoft.CognitiveServices.Speech)
                // 3. 使用备用方案：基于音频振幅分析估算开合度
                
                // 当前：仅生成音频
                var op = webRequest.SendWebRequest();
                while (!op.isDone) await Task.Delay(50);

                if (webRequest.result != UnityWebRequest.Result.Success)
                {
                    // 仅在 DevMode 显示详细错误日志
                    if (Prefs.DevMode)
                    {
                        Log.Error($"[TTSService] Azure TTS error: {webRequest.responseCode} - {webRequest.error}");
                        Log.Error($"[TTSService] Error details: {webRequest.downloadHandler.text}");
                        Log.Error($"[TTSService] Region: {apiRegion}");
                        Log.Error($"[TTSService] Voice: {voiceName}");
                        Log.Error($"[TTSService] SSML length: {ssml.Length} characters");
                    }
                    
                    return null;
                }

                byte[] audioData = webRequest.downloadHandler.data;
                
                
                // ⭐ v1.6.74: 备用方案 - 使用简单的 Viseme 序列模拟
                // 已移至 SpeakAsync 统一处理
                
                return audioData;
            }
            catch (Exception ex)
            {
                // 仅在 DevMode 显示异常堆栈
                if (Prefs.DevMode)
                {
                    Log.Error($"[TTSService] Azure TTS exception: {ex.Message}");
                    Log.Error($"[TTSService] Stack trace: {ex.StackTrace}");
                }
                return null;
            }
        }
        
        /// <summary>
        /// ⭐ v1.6.74: 【备用方案】生成简单的 Viseme 序列
        /// 基于文本分析粗略估算口型变化
        /// 注意：这不是真正的音素同步，仅作为演示和备用方案
        /// ⭐ v1.8.6: 改进算法 - 引入"辅音-元音"结构，增加口型动态感
        /// </summary>
        private void GenerateSimpleVisemeSequence(string text, string personaDefName)
        {
            try
            {
                var visemes = new List<PersonaGeneration.VisemeCode>();
                
                // 预设缓冲
                visemes.Add(PersonaGeneration.VisemeCode.Closed);

                foreach (char c in text)
                {
                    if (char.IsWhiteSpace(c) || char.IsPunctuation(c))
                    {
                        visemes.Add(PersonaGeneration.VisemeCode.Closed);
                        visemes.Add(PersonaGeneration.VisemeCode.Closed);
                        continue;
                    }

                    // 1. 核心口型选择 (Core Viseme)
                    // 增加大口型 (Large, OShape) 和特色口型 (Smile) 的权重，避免单调
                    int hash = Math.Abs(c.GetHashCode());
                    PersonaGeneration.VisemeCode coreViseme;
                    
                    // 中文/日文/韩文等宽字符 (Unicode 范围粗略判断)
                    if (c >= 0x2E80)
                    {
                        int val = hash % 100;
                        if (val < 30) coreViseme = PersonaGeneration.VisemeCode.Large;      // A (30%)
                        else if (val < 55) coreViseme = PersonaGeneration.VisemeCode.OShape; // O (25%)
                        else if (val < 80) coreViseme = PersonaGeneration.VisemeCode.Smile;  // E (25%)
                        else coreViseme = PersonaGeneration.VisemeCode.Medium;               // U (20%)
                        // Small (Neutral) 很少作为核心口型，因为不够明显
                    }
                    else // 英文/拉丁字符
                    {
                         // 简单元音映射
                         char lower = char.ToLowerInvariant(c);
                         if ("aeiou".IndexOf(lower) >= 0)
                         {
                             coreViseme = lower switch
                             {
                                 'a' => PersonaGeneration.VisemeCode.Large,
                                 'e' => PersonaGeneration.VisemeCode.Smile,
                                 'i' => PersonaGeneration.VisemeCode.Smile,
                                 'o' => PersonaGeneration.VisemeCode.OShape,
                                 'u' => PersonaGeneration.VisemeCode.Medium,
                                 _ => PersonaGeneration.VisemeCode.Medium
                             };
                         }
                         else
                         {
                             // 辅音：随机 Small 或 Closed
                             coreViseme = (hash % 2 == 0) ? PersonaGeneration.VisemeCode.Small : PersonaGeneration.VisemeCode.Closed;
                         }
                    }

                    // 2. 构建 "辅音-元音-保持" 结构 (每字约 3 帧 / 0.3s)
                    
                    // 帧1: 起始/辅音 (Onset) - 快速过渡
                    // 如果核心是大口型，起始帧用 Small/Smile 过渡；如果是辅音，直接 Closed
                    if (coreViseme == PersonaGeneration.VisemeCode.Large || coreViseme == PersonaGeneration.VisemeCode.OShape)
                    {
                        visemes.Add(PersonaGeneration.VisemeCode.Small);
                    }
                    else if (coreViseme == PersonaGeneration.VisemeCode.Smile)
                    {
                        visemes.Add(PersonaGeneration.VisemeCode.Small);
                    }
                    else
                    {
                        // 辅音或小口型，起始帧可以是闭合
                        visemes.Add(PersonaGeneration.VisemeCode.Closed);
                    }

                    // 帧2: 核心口型 (Nucleus) - 爆发
                    visemes.Add(coreViseme);

                    // 帧3: 保持或收尾 (Coda)
                    // 大口型保持一帧，增强视觉冲击力
                    if (coreViseme == PersonaGeneration.VisemeCode.Large || coreViseme == PersonaGeneration.VisemeCode.OShape || coreViseme == PersonaGeneration.VisemeCode.Smile)
                    {
                        visemes.Add(coreViseme);
                    }
                    else
                    {
                        // 辅音或小口型，快速收尾
                        visemes.Add(PersonaGeneration.VisemeCode.Small);
                    }
                }
                
                // 结束缓冲
                visemes.Add(PersonaGeneration.VisemeCode.Closed);
                visemes.Add(PersonaGeneration.VisemeCode.Closed);

                // 如果序列不为空，推送到动画系统
                if (visemes.Count > 0)
                {
                    PersonaGeneration.MouthAnimationSystem.PushVisemeSequence(personaDefName, visemes);
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[TTSService] Failed to generate Viseme sequence: {ex.Message}");
            }
        }

        /// <summary>
        /// 构建 SSML（Azure TTS 使用）
        /// ? 支持情感表达（mstts:express-as）
        /// </summary>
        private string BuildSSML(string text, string voice, float rate)
        {
            // ? 修复：速率格式化（Azure 要求精确格式）
            string rateStr;
            if (rate >= 1.0f)
            {
                int percent = (int)((rate - 1.0f) * 100);
                rateStr = percent == 0 ? "0%" : $"+{percent}%";
            }
            else
            {
                int percent = (int)((rate - 1.0f) * 100);
                rateStr = $"{percent}%";
            }
            
            // ? 修复：转义文本（防止 XML 注入）
            string escapedText = System.Security.SecurityElement.Escape(text);
            
            // ? 新增：获取当前情感风格
            EmotionStyle emotion = GetCurrentEmotion();
            
            // ? 构建 SSML（带情感）
            string ssml;
            
            // 检查语音是否支持情感（Neural 语音才支持）
            if (voice.Contains("Neural") && emotion.StyleName != "chat")
            {
                // 带情感的 SSML
                ssml = $@"<speak version='1.0' xmlns='http://www.w3.org/2001/10/synthesis' xmlns:mstts='https://www.w3.org/2001/mstts' xml:lang='zh-CN'>
    <voice name='{voice}'>
        <mstts:express-as style='{emotion.StyleName}' styledegree='{emotion.StyleDegree:F2}'>
            <prosody rate='{rateStr}'>
                {escapedText}
            </prosody>
        </mstts:express-as>
    </voice>
</speak>";
            }
            else
            {
                // 不带情感的 SSML（兼容旧语音）
                ssml = $@"<speak version='1.0' xmlns='http://www.w3.org/2001/10/synthesis' xmlns:mstts='https://www.w3.org/2001/mstts' xml:lang='zh-CN'>
    <voice name='{voice}'>
        <prosody rate='{rateStr}'>
            {escapedText}
        </prosody>
    </voice>
</speak>";
            }

            return ssml;
        }

        /// <summary>
        /// 获取当前情感风格（从表情系统）
        /// </summary>
        private EmotionStyle GetCurrentEmotion()
        {
            try
            {
                // 获取当前人格
                var narratorManager = Verse.Current.Game?.GetComponent<Narrator.NarratorManager>();
                if (narratorManager == null)
                {
                    return new EmotionStyle("chat", 1.0f);
                }

                var persona = narratorManager.GetCurrentPersona();
                if (persona == null)
                {
                    return new EmotionStyle("chat", 1.0f);
                }

                // 从表情系统获取情感
                return EmotionMapper.GetCurrentEmotionStyle(persona.defName);
            }
            catch (Exception ex)
            {
                // 仅在 DevMode 显示警告
                if (Prefs.DevMode)
                {
                    Verse.Log.Warning($"[TTSService] Failed to get emotion: {ex.Message}");
                }
                return new EmotionStyle("chat", 1.0f);
            }
        }

        /// <summary>
        /// 获取音频输出目录
        /// </summary>
        public string GetAudioOutputDirectory()
        {
            return audioOutputDir;
        }

        /// <summary>
        /// 打开音频输出目录
        /// </summary>
        public void OpenAudioDirectory()
        {
            if (Directory.Exists(audioOutputDir))
            {
                System.Diagnostics.Process.Start("explorer.exe", audioOutputDir);
            }
        }

        /// <summary>
        /// 清空音频缓存
        /// </summary>
        public void ClearCache()
        {
            try
            {
                if (Directory.Exists(audioOutputDir))
                {
                    var files = Directory.GetFiles(audioOutputDir, "*.wav");
                    foreach (var file in files)
                    {
                        File.Delete(file);
                    }
                    
                }
            }
            catch (Exception ex)
            {
                // 仅在 DevMode 显示错误
                if (Prefs.DevMode)
                {
                    Log.Error($"[TTSService] Error clearing cache: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 获取可用的语音列表
        /// </summary>
        public static List<string> GetAvailableVoices()
        {
            return new List<string>
            {
                // ? 中文语音（普通话）
                "zh-CN-XiaoxiaoNeural",      // 女声（通用，温暖）? 默认
                "zh-CN-XiaoyiNeural",        // 女声（儿童，活泼）
                "zh-CN-YunjianNeural",       // 男声（体育解说，激情）
                "zh-CN-YunxiNeural",         // 男声（通用，自然）
                "zh-CN-YunxiaNeural",        // 男声（儿童，可爱）
                "zh-CN-YunyangNeural",       // 男声（新闻播报，专业）
                "zh-CN-liaoning-XiaobeiNeural", // 女声（东北方言）
                "zh-CN-shaanxi-XiaoniNeural",   // 女声（陕西方言）
                
                // ? 中文语音（多风格 - 晒晓增强版）
                "zh-CN-XiaoxiaoMultilingualNeural", // 女声（多语言，流畅）
                "zh-CN-XiaochenNeural",      // 女声（客服，温柔）
                "zh-CN-XiaohanNeural",       // 女声（通用，自然）
                "zh-CN-XiaomengNeural",      // 女声（儿童，活泼）
                "zh-CN-XiaomoNeural",        // 女声（通用，清晰）
                "zh-CN-XiaoqiuNeural",       // 女声（通用，温和）
                "zh-CN-XiaoruiNeural",       // 女声（通用，自信）
                "zh-CN-XiaoshuangNeural",    // 女声（儿童，可爱）
                "zh-CN-XiaoxuanNeural",      // 女声（通用，优雅）
                "zh-CN-XiaoyanNeural",       // 女声（通用，柔和）
                "zh-CN-XiaoyouNeural",       // 女声（儿童，活泼）
                "zh-CN-XiaozhenNeural",      // 女声（方言）
                "zh-CN-YunfengNeural",       // 男声（通用，沉稳）
                "zh-CN-YunhaoNeural",        // 男声（通用，自然）
                "zh-CN-YunjieNeural",        // 男声（通用，阳光）
                "zh-CN-YunzeNeural",         // 男声（通用，成熟）
                
                // ? 粤语（广东话）
                "zh-HK-HiuMaanNeural",       // 女声（香港粤语）
                "zh-HK-HiuGaaiNeural",       // 女声（香港粤语，温柔）
                "zh-HK-WanLungNeural",       // 男声（香港粤语）
                
                // ? 台湾国语
                "zh-TW-HsiaoChenNeural",     // 女声（台湾国语）
                "zh-TW-HsiaoYuNeural",       // 女声（台湾国语，温柔）
                "zh-TW-YunJheNeural",        // 男声（台湾国语）
                
                // ? 英文语音（美式）
                "en-US-JennyNeural",         // 女声（通用）
                "en-US-JennyMultilingualNeural", // 女声（多语言）
                "en-US-GuyNeural",           // 男声（通用）
                "en-US-AriaNeural",          // 女声（新闻）
                "en-US-DavisNeural",         // 男声（成熟）
                "en-US-AmberNeural",         // 女声（年轻）
                "en-US-AshleyNeural",        // 女声（活泼）
                "en-US-BrandonNeural",       // 男声（年轻）
                "en-US-ChristopherNeural",   // 男声（沉稳）
                "en-US-CoraNeural",          // 女声（温和）
                "en-US-ElizabethNeural",     // 女声（优雅）
                "en-US-EricNeural",          // 男声（自然）
                "en-US-JacobNeural",         // 男声（阳光）
                "en-US-MichelleNeural",      // 女声（专业）
                "en-US-MonicaNeural",        // 女声（友好）
                "en-US-SaraNeural",          // 女声（温柔）
                "en-US-TonyNeural",          // 男声（播报）
                
                // ? 英文语音（英式）
                "en-GB-SoniaNeural",         // 女声（英式，通用）
                "en-GB-RyanNeural",          // 男声（英式，通用）
                "en-GB-LibbyNeural",         // 女声（英式，温柔）
                "en-GB-AbbiNeural",          // 女声（英式，年轻）
                "en-GB-AlfieNeural",         // 男声（英式，活泼）
                "en-GB-BellaNeural",         // 女声（英式，优雅）
                "en-GB-ElliotNeural",        // 男声（英式，自然）
                "en-GB-EthanNeural",         // 男声（英式，沉稳）
                "en-GB-HollieNeural",        // 女声（英式，友好）
                "en-GB-MaisieNeural",        // 女声（英式，可爱）
                "en-GB-NoahNeural",          // 男声（英式，自信）
                "en-GB-OliverNeural",        // 男声（英式，成熟）
                "en-GB-OliviaNeural",        // 女声（英式，专业）
                "en-GB-ThomasNeural",        // 男声（英式，播报）
                
                // ? 日文语音
                "ja-JP-NanamiNeural",        // 女声（通用，自然）
                "ja-JP-KeitaNeural",         // 男声（通用，自然）
                "ja-JP-AoiNeural",           // 女声（可爱）
                "ja-JP-DaichiNeural",        // 男声（阳光）
                "ja-JP-MayuNeural",          // 女声（温柔）
                "ja-JP-NaokiNeural",         // 男声（成熟）
                "ja-JP-ShioriNeural",        // 女声（优雅）
                
                // ? 韩文语音
                "ko-KR-SunHiNeural",         // 女声（通用）
                "ko-KR-InJoonNeural",        // 男声（通用）
                "ko-KR-BongJinNeural",       // 男声（播报）
                "ko-KR-GookMinNeural",       // 男声（自然）
                "ko-KR-JiMinNeural",         // 女声（温柔）
                "ko-KR-SeoHyeonNeural",      // 女声（年轻）
                "ko-KR-SoonBokNeural",       // 女声（成熟）
                "ko-KR-YuJinNeural",         // 女声（活泼）
                
                // ? 法语
                "fr-FR-DeniseNeural",        // 女声（法式）
                "fr-FR-HenriNeural",         // 男声（法式）
                "fr-FR-AlainNeural",         // 男声（播报）
                "fr-FR-BrigitteNeural",      // 女声（优雅）
                "fr-FR-CelesteNeural",       // 女声（温柔）
                "fr-FR-ClaudeNeural",        // 男声（成熟）
                "fr-FR-CoralieNeural",       // 女声（活泼）
                
                // ? 德语
                "de-DE-KatjaNeural",         // 女声（德式）
                "de-DE-ConradNeural",        // 男声（德式）
                "de-DE-AmalaNeural",         // 女声（温柔）
                "de-DE-BerndNeural",         // 男声（播报）
                "de-DE-ChristophNeural",     // 男声（自然）
                "de-DE-ElkeNeural",          // 女声（成熟）
                "de-DE-GiselaNeural",        // 女声（友好）
                
                // ? 西班牙语
                "es-ES-ElviraNeural",        // 女声（西班牙）
                "es-ES-AlvaroNeural",        // 男声（西班牙）
                "es-MX-DaliaNeural",         // 女声（墨西哥）
                "es-MX-JorgeNeural",         // 男声（墨西哥）
                
                // ? 俄语
                "ru-RU-SvetlanaNeural",      // 女声（俄式）
                "ru-RU-DmitryNeural",        // 男声（俄式）
                "ru-RU-DariyaNeural",        // 女声（温柔）
                
                // ? 意大利语
                "it-IT-ElsaNeural",          // 女声（意式）
                "it-IT-IsabellaNeural",      // 女声（温柔）
                "it-IT-DiegoNeural",         // 男声（意式）
                
                // ? 葡萄牙语
                "pt-BR-FranciscaNeural",     // 女声（巴西）
                "pt-BR-AntonioNeural",       // 男声（巴西）
                "pt-PT-RaquelNeural",        // 女声（葡萄牙）
                "pt-PT-DuarteNeural",        // 男声（葡萄牙）
            };
        }
    }
}
