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
    /// 支持多个 TTS 提供商：Azure TTS, Edge TTS, Local TTS, OpenAI, SiliconFlow
    /// 注意：RimWorld 不直接支持音频播放，TTS 音频将保存为文件
    /// </summary>
    public class TTSService
    {
        private static TTSService? instance;
        public static TTSService Instance => instance ??= new TTSService();

        private string ttsProvider = "edge"; // "azure", "edge", "local", "openai", "siliconflow"
        private string apiKey = "";
        private string apiRegion = "eastus";
        private string voiceName = "zh-CN-XiaoxiaoNeural"; // 默认中文女声
        private float speechRate = 1.0f;
        private float volume = 1.0f;
        private float pitch = 0.0f; // -50% to +50% (Hz shift logic)

        // OpenAI 兼容接口配置
        private string openAI_ApiUrl = "http://127.0.0.1:9880/v1/audio/speech";
        private string openAI_Model = "gpt-sovits";

        // SiliconFlow (IndexTTS) 配置
        private string siliconFlow_ApiUrl = "https://api.siliconflow.cn/v1/audio/speech";
        private string siliconFlow_Model = "IndexTeam/IndexTTS-2";
        
        // v1.15.0: SiliconFlow 音色克隆 URI
        private string siliconFlow_AudioUri = "";

        private string audioOutputDir = "";
        
        // 语音播放状态（用于唇形同步）
        public bool IsSpeaking { get; private set; } = false;

        public TTSService()
        {
            audioOutputDir = Path.Combine(GenFilePaths.SaveDataFolderPath, "TheSecondSeat", "TTS");
            if (!Directory.Exists(audioOutputDir))
            {
                Directory.CreateDirectory(audioOutputDir);
            }
        }

        /// <summary>
        /// 配置 TTS 服务
        /// </summary>
        public void Configure(
            string provider,
            string key = "",
            string region = "eastus",
            string voice = "zh-CN-XiaoxiaoNeural",
            float rate = 1.0f,
            float vol = 1.0f,
            float pitchVal = 0.0f,
            string apiUrl = "",
            string modelName = "",
            string audioUri = "")
        {
            ttsProvider = provider;
            apiKey = key;
            apiRegion = region;
            voiceName = voice;
            speechRate = UnityEngine.Mathf.Clamp(rate, 0.5f, 2.0f);
            volume = UnityEngine.Mathf.Clamp(vol, 0.0f, 1.0f);
            pitch = UnityEngine.Mathf.Clamp(pitchVal, -0.5f, 0.5f);

            if (ttsProvider == "openai")
            {
                if (!string.IsNullOrEmpty(apiUrl)) openAI_ApiUrl = apiUrl;
                if (!string.IsNullOrEmpty(modelName)) openAI_Model = modelName;
            }
            else if (ttsProvider == "siliconflow")
            {
                if (!string.IsNullOrEmpty(apiUrl)) siliconFlow_ApiUrl = apiUrl;
                if (!string.IsNullOrEmpty(modelName)) siliconFlow_Model = modelName;
                siliconFlow_AudioUri = audioUri ?? "";
                
                if (Prefs.DevMode && !string.IsNullOrEmpty(siliconFlow_AudioUri))
                {
                    Log.Message($"[TTSService] SiliconFlow audio_uri configured: {siliconFlow_AudioUri}");
                }
            }
        }
        
        public void SetSiliconFlowAudioUri(string audioUri)
        {
            siliconFlow_AudioUri = audioUri ?? "";
            if (Prefs.DevMode)
            {
                Log.Message(!string.IsNullOrEmpty(siliconFlow_AudioUri) 
                    ? $"[TTSService] SiliconFlow audio_uri set: {siliconFlow_AudioUri}"
                    : "[TTSService] SiliconFlow audio_uri cleared, using default voice");
            }
        }
        
        public string GetSiliconFlowAudioUri() => siliconFlow_AudioUri;

        /// <summary>
        /// 将文本转换为语音并保存为文件
        /// </summary>
        public async Task<string?> SpeakAsync(string text, string personaDefName = "")
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                if (Prefs.DevMode) Log.Warning("[TTSService] Text is empty");
                return null;
            }

            try
            {
                string cleanText = CleanTextForTTS(text);
                if (string.IsNullOrWhiteSpace(cleanText))
                {
                    if (Prefs.DevMode) Log.Warning("[TTSService] Text is empty after cleaning");
                    return null;
                }
                
                // ✅ v2.8.2: 统一使用 WAV 格式
                string fileName = $"tts_{DateTime.Now:yyyyMMdd_HHmmss}.wav";
                string filePath = Path.Combine(audioOutputDir, fileName);
                byte[]? audioData = null;

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
                        if (Prefs.DevMode) Log.Error($"[TTSService] Unknown provider: {ttsProvider}");
                        return null;
                }

                if (audioData == null || audioData.Length == 0)
                {
                    if (Prefs.DevMode) Log.Error($"[TTSService] Failed to generate audio with {ttsProvider}");
                    return null;
                }

                File.WriteAllBytes(filePath, audioData);

                if (!string.IsNullOrEmpty(personaDefName))
                {
                    GenerateSimpleVisemeSequence(cleanText, personaDefName);
                }

                string capturedPersonaDefName = personaDefName;
                Verse.LongEventHandler.ExecuteWhenFinished(() =>
                {
                    AutoPlayAudioFile(filePath, capturedPersonaDefName);
                });

                return filePath;
            }
            catch (Exception ex)
            {
                if (Prefs.DevMode) Log.Error($"[TTSService] Error: {ex.Message}");
                return null;
            }
        }

        private Task<byte[]?> GenerateLocalTTSAsync(string text)
        {
            // ⭐ v1.7.0: 检查操作系统是否为 Windows
            if (UnityEngine.Application.platform != UnityEngine.RuntimePlatform.WindowsEditor &&
                UnityEngine.Application.platform != UnityEngine.RuntimePlatform.WindowsPlayer)
            {
                if (Prefs.DevMode) Log.Warning("[TTSService] Local TTS is only supported on Windows.");
                return Task.FromResult<byte[]?>(null);
            }

            return Task.Run(() =>
            {
                object? synthesizer = null;
                try
                {
                    var assembly = System.Reflection.Assembly.Load("System.Speech, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35");
                    var synthesizerType = assembly.GetType("System.Speech.Synthesis.SpeechSynthesizer");
                    synthesizer = Activator.CreateInstance(synthesizerType);

                    using (var stream = new MemoryStream())
                    {
                        synthesizerType.GetMethod("SetOutputToWaveStream", new[] { typeof(Stream) })
                            ?.Invoke(synthesizer, new object[] { stream });
                        synthesizerType.GetMethod("set_Volume")?.Invoke(synthesizer, new object[] { (int)(volume * 100) });
                        int rate = (int)((speechRate - 1.0f) * 10);
                        synthesizerType.GetMethod("set_Rate")?.Invoke(synthesizer, new object[] { rate });
                        synthesizerType.GetMethod("Speak", new[] { typeof(string) })
                            ?.Invoke(synthesizer, new object[] { text });
                        return stream.ToArray();
                    }
                }
                catch (Exception ex)
                {
                    if (Prefs.DevMode) Log.Error($"[TTSService] Local TTS error: {ex.Message}");
                    return null;
                }
                finally
                {
                    // ✅ v2.7.2: 确保 SpeechSynthesizer 被正确释放
                    if (synthesizer is IDisposable disposable)
                    {
                        disposable.Dispose();
                    }
                }
            });
        }

        /// <summary>
        /// 生成 Edge TTS 语音（使用 WebSocket）
        /// ⭐ v2.8.0: 使用纯 C# WebSocket 实现，无需 Python
        /// </summary>
        private async Task<byte[]?> GenerateEdgeTTSAsync(string text)
        {
            try
            {
                using (var client = new EdgeTTSWebSocketClient())
                {
                    // 转换语速格式
                    string rateStr;
                    if (speechRate >= 1.0f)
                    {
                        int percent = (int)((speechRate - 1.0f) * 100);
                        rateStr = percent == 0 ? "+0%" : $"+{percent}%";
                    }
                    else
                    {
                        int percent = (int)((speechRate - 1.0f) * 100);
                        rateStr = $"{percent}%";
                    }
                    
                    // 转换音量格式
                    string volumeStr = $"+{(int)(volume * 100 - 100)}%";
                    if (volume >= 1.0f) volumeStr = "+0%";

                    // 转换音高格式 (使用百分比)
                    int pitchPercent = (int)(pitch * 100);
                    string pitchStr = pitchPercent >= 0 ? $"+{pitchPercent}%" : $"{pitchPercent}%";
                    
                    byte[] audioData = await client.SynthesizeAsync(text, voiceName, rateStr, volumeStr, pitchStr);
                    
                    if (audioData != null && audioData.Length > 0)
                    {
                        if (Prefs.DevMode)
                        {
                            Log.Message($"[TTSService] Edge TTS generated {audioData.Length} bytes");
                        }
                        return audioData;
                    }
                }
                
                return null;
            }
            catch (Exception ex)
            {
                if (Prefs.DevMode)
                {
                    Log.Error($"[TTSService] Edge TTS error: {ex.Message}");
                }
                return null;
            }
        }

        /// <summary>
        /// 生成 SiliconFlow (IndexTTS) 语音
        /// ✅ v2.8.1: 增强诊断和格式验证
        /// </summary>
        private async Task<byte[]?> GenerateSiliconFlowTTSAsync(string text)
        {
            try
            {
                object requestBody;
                
                if (!string.IsNullOrEmpty(siliconFlow_AudioUri))
                {
                    // 使用音色克隆模式
                    // ✅ v2.8.7: 请求 OGG 格式（RimWorld 原生支持 OGG Vorbis）
                    // 如果 API 不支持 OGG，会回退到 MP3 转 WAV
                    requestBody = new
                    {
                        model = siliconFlow_Model,
                        input = text,
                        voice = siliconFlow_AudioUri, // 使用 URI 作为 voice ID
                        response_format = "opus", // 尝试 opus (OGG Opus)
                        stream = false, // 显式关闭流式
                        speed = speechRate
                    };
                    
                    if (Prefs.DevMode)
                    {
                        Log.Message($"[TTSService] SiliconFlow using cloned voice: {siliconFlow_AudioUri}");
                    }
                }
                else
                {
                    // 标准模式
                    // ✅ v2.8.7: 请求 OGG 格式（RimWorld 原生支持 OGG Vorbis）
                    requestBody = new
                    {
                        model = siliconFlow_Model,
                        input = text,
                        voice = voiceName,
                        response_format = "opus", // 尝试 opus (OGG Opus)
                        speed = speechRate
                    };
                }

                string jsonBody = JsonConvert.SerializeObject(requestBody);
                
                if (Prefs.DevMode)
                {
                    Log.Message($"[TTSService] SiliconFlow request: {jsonBody}");
                }

                using var webRequest = new UnityWebRequest(siliconFlow_ApiUrl, "POST");
                webRequest.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(jsonBody));
                webRequest.downloadHandler = new DownloadHandlerBuffer();
                webRequest.SetRequestHeader("Content-Type", "application/json");
                
                if (!string.IsNullOrEmpty(apiKey))
                {
                    webRequest.SetRequestHeader("Authorization", $"Bearer {apiKey}");
                }

                await webRequest.SendWebRequest();

                if (webRequest.result != UnityWebRequest.Result.Success)
                {
                    if (Prefs.DevMode)
                    {
                        Log.Error($"[TTSService] SiliconFlow error: {webRequest.responseCode} - {webRequest.error}");
                        Log.Error($"[TTSService] Details: {webRequest.downloadHandler.text}");
                    }
                    return null;
                }

                byte[] responseData = webRequest.downloadHandler.data;
                
                // ✅ v2.8.1: 诊断返回的音频数据
                if (Prefs.DevMode)
                {
                    Log.Message($"[TTSService] SiliconFlow response size: {responseData?.Length ?? 0} bytes");
                    
                    if (responseData != null && responseData.Length >= 12)
                    {
                        // 检查 WAV 文件头 (RIFF....WAVE)
                        string riffHeader = Encoding.ASCII.GetString(responseData, 0, 4);
                        string waveHeader = Encoding.ASCII.GetString(responseData, 8, 4);
                        Log.Message($"[TTSService] SiliconFlow audio header: '{riffHeader}'...'{waveHeader}'");
                        
                        if (riffHeader != "RIFF" || waveHeader != "WAVE")
                        {
                            // 可能是 MP3 或其他格式
                            // 检查 MP3 头 (ID3 或 0xFF 0xFB)
                            if (responseData.Length >= 3)
                            {
                                string id3Header = Encoding.ASCII.GetString(responseData, 0, 3);
                                bool isMP3Sync = responseData[0] == 0xFF && (responseData[1] & 0xE0) == 0xE0;
                                
                                if (id3Header == "ID3" || isMP3Sync)
                                {
                                    Log.Warning($"[TTSService] SiliconFlow returned MP3 instead of WAV! Header: {id3Header}");
                                }
                                else
                                {
                                    // 打印前16字节用于调试
                                    string hexHeader = BitConverter.ToString(responseData, 0, Math.Min(16, responseData.Length));
                                    Log.Warning($"[TTSService] SiliconFlow unknown format, hex: {hexHeader}");
                                }
                            }
                        }
                    }
                    else if (responseData != null && responseData.Length < 100)
                    {
                        // 数据太小，可能是错误响应
                        string textContent = Encoding.UTF8.GetString(responseData);
                        Log.Warning($"[TTSService] SiliconFlow response too small, content: {textContent}");
                    }
                }

                return responseData;
            }
            catch (Exception ex)
            {
                if (Prefs.DevMode) Log.Error($"[TTSService] SiliconFlow exception: {ex.Message}");
                return null;
            }
        }

        private async Task<byte[]?> GenerateOpenAITTSAsync(string text)
        {
            try
            {
                var requestBody = new
                {
                    model = openAI_Model,
                    input = text,
                    voice = voiceName,
                    response_format = "wav",
                    speed = speechRate
                };

                string jsonBody = JsonConvert.SerializeObject(requestBody);

                using var webRequest = new UnityWebRequest(openAI_ApiUrl, "POST");
                webRequest.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(jsonBody));
                webRequest.downloadHandler = new DownloadHandlerBuffer();
                webRequest.SetRequestHeader("Content-Type", "application/json");
                
                if (!string.IsNullOrEmpty(apiKey))
                {
                    webRequest.SetRequestHeader("Authorization", $"Bearer {apiKey}");
                }

                await webRequest.SendWebRequest();

                if (webRequest.result != UnityWebRequest.Result.Success)
                {
                    if (Prefs.DevMode)
                    {
                        Log.Error($"[TTSService] OpenAI TTS error: {webRequest.responseCode} - {webRequest.error}");
                        Log.Error($"[TTSService] Details: {webRequest.downloadHandler.text}");
                    }
                    return null;
                }

                return webRequest.downloadHandler.data;
            }
            catch (Exception ex)
            {
                if (Prefs.DevMode) Log.Error($"[TTSService] OpenAI TTS exception: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// ⭐ v2.7.1: 修改为使用 PlayAndDelete，播放后自动删除原始文件
        /// ⭐ v2.8.0: 修复 autoPlayTTS=false 时文件不删除的问题
        /// ⭐ v2.8.1: 增强诊断日志
        /// </summary>
        private void AutoPlayAudioFile(string filePath, string personaDefName = "")
        {
            try
            {
                if (Prefs.DevMode)
                {
                    Log.Message($"[TTSService] AutoPlayAudioFile called: persona={personaDefName}, file={filePath}");
                }
                
                if (!File.Exists(filePath))
                {
                    Log.Warning($"[TTSService] Audio file not found: {filePath}");
                    return;
                }
                
                // ✅ v2.8.1: 记录文件大小
                if (Prefs.DevMode)
                {
                    var fileInfo = new FileInfo(filePath);
                    Log.Message($"[TTSService] Audio file exists, size={fileInfo.Length} bytes");
                }
                
                var modSettings = LoadedModManager.GetMod<Settings.TheSecondSeatMod>()?.GetSettings<Settings.TheSecondSeatSettings>();
                
                // ✅ v2.8.1: 详细记录设置状态
                if (Prefs.DevMode)
                {
                    if (modSettings == null)
                    {
                        Log.Warning("[TTSService] modSettings is NULL!");
                    }
                    else
                    {
                        Log.Message($"[TTSService] Settings: autoPlayTTS={modSettings.autoPlayTTS}");
                    }
                }
                
                // ⭐ v2.8.0: 如果 autoPlayTTS 未启用，删除文件后返回
                if (modSettings == null || !modSettings.autoPlayTTS)
                {
                    if (Prefs.DevMode)
                    {
                        Log.Message($"[TTSService] autoPlayTTS is disabled, deleting generated audio file: {filePath}");
                    }
                    try
                    {
                        File.Delete(filePath);
                    }
                    catch (Exception deleteEx)
                    {
                        if (Prefs.DevMode) Log.Warning($"[TTSService] Failed to delete audio file: {deleteEx.Message}");
                    }
                    return;
                }

                IsSpeaking = true;
                
                if (Prefs.DevMode)
                {
                    Log.Message($"[TTSService] Calling TTSAudioPlayer.Instance.PlayAndDelete for: {filePath}");
                }
                
                // ⭐ v2.7.1: 直接使用 PlayAndDelete，播放后自动删除文件
                TTSAudioPlayer.Instance.PlayAndDelete(filePath, personaDefName, () => {
                    IsSpeaking = false;
                    if (Prefs.DevMode)
                    {
                        Log.Message("[TTSService] PlayAndDelete callback invoked - playback completed");
                    }
                });
                
                if (Prefs.DevMode)
                {
                    Log.Message("[TTSService] PlayAndDelete call returned (async playback started)");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[TTSService] AutoPlayAudioFile EXCEPTION: {ex.Message}\n{ex.StackTrace}");
                IsSpeaking = false;
            }
        }

        private string CleanTextForTTS(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            
            text = System.Text.RegularExpressions.Regex.Replace(text, @"\([^)]*\)", "");
            text = System.Text.RegularExpressions.Regex.Replace(text, @"（[^）]*）", "");
            text = System.Text.RegularExpressions.Regex.Replace(text, @"\[[^\]]*\]", "");
            text = System.Text.RegularExpressions.Regex.Replace(text, @"【[^】]*】", "");
            text = System.Text.RegularExpressions.Regex.Replace(text, @"<[^>]*>", "");
            text = System.Text.RegularExpressions.Regex.Replace(text, @"\*[^*]*\*", "");
            text = System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ");
            return text.Trim();
        }

        private async Task<byte[]?> GenerateAzureTTSAsync(string text, string personaDefName = "")
        {
            if (string.IsNullOrEmpty(apiKey))
            {
                if (Prefs.DevMode) Log.Warning("[TTSService] Azure TTS API key is not configured");
                return null;
            }

            try
            {
                string endpoint = $"https://{apiRegion}.tts.speech.microsoft.com/cognitiveservices/v1";
                string ssml = BuildSSML(text, voiceName, speechRate, pitch);

                using var webRequest = new UnityWebRequest(endpoint, "POST");
                webRequest.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(ssml));
                webRequest.downloadHandler = new DownloadHandlerBuffer();
                webRequest.SetRequestHeader("Content-Type", "application/ssml+xml");
                webRequest.SetRequestHeader("Ocp-Apim-Subscription-Key", apiKey);
                webRequest.SetRequestHeader("X-Microsoft-OutputFormat", "riff-48khz-16bit-mono-pcm");
                webRequest.SetRequestHeader("User-Agent", "RimWorld-TheSecondSeat-TTS");

                await webRequest.SendWebRequest();

                if (webRequest.result != UnityWebRequest.Result.Success)
                {
                    if (Prefs.DevMode)
                    {
                        Log.Error($"[TTSService] Azure TTS error: {webRequest.responseCode} - {webRequest.error}");
                        Log.Error($"[TTSService] Details: {webRequest.downloadHandler.text}");
                    }
                    return null;
                }

                return webRequest.downloadHandler.data;
            }
            catch (Exception ex)
            {
                if (Prefs.DevMode) Log.Error($"[TTSService] Azure TTS exception: {ex.Message}");
                return null;
            }
        }
        
        private void GenerateSimpleVisemeSequence(string text, string personaDefName)
        {
            try
            {
                // 获取配置 (Lite Mapping)
                LipSyncMappingDef mappingDef = DefDatabase<LipSyncMappingDef>.GetNamedSilentFail("DefaultLipSyncMapping");
                if (mappingDef == null)
                {
                    // Fallback to default if def is missing
                    if (Prefs.DevMode) Log.Warning("[TTSService] DefaultLipSyncMapping not found, using fallback defaults.");
                    mappingDef = new LipSyncMappingDef(); // Uses default values defined in class
                }

                var visemes = new List<PersonaGeneration.VisemeCode>();
                // 起始静默
                visemes.Add(PersonaGeneration.VisemeCode.Closed);

                foreach (char c in text)
                {
                    // 处理空白和标点：插入停顿
                    if (char.IsWhiteSpace(c))
                    {
                        visemes.Add(PersonaGeneration.VisemeCode.Closed);
                        continue;
                    }
                    
                    if (char.IsPunctuation(c))
                    {
                        visemes.Add(PersonaGeneration.VisemeCode.Closed);
                        visemes.Add(PersonaGeneration.VisemeCode.Closed);
                        continue;
                    }

                    // 确定核心口型 (Core Viseme)
                    PersonaGeneration.VisemeCode coreViseme = mappingDef.defaultViseme;
                    PhonemeGroup group = LipSyncData.GetPhonemeGroup(c);

                    if (group != PhonemeGroup.None)
                    {
                        coreViseme = mappingDef.GetVisemeFor(group);
                    }
                    // 2. 处理英文/拉丁字符 (Fallback logic kept for compatibility)
                    else if (c < 0x2E80)
                    {
                        char lower = char.ToLowerInvariant(c);
                        if ("aeiou".IndexOf(lower) >= 0)
                        {
                            // 简单映射：a/o/u -> Large/Medium/OShape based on mapping logic
                            if (lower == 'a') coreViseme = mappingDef.GetVisemeFor(PhonemeGroup.Large);
                            else if (lower == 'o') coreViseme = mappingDef.GetVisemeFor(PhonemeGroup.OShape);
                            else if (lower == 'e' || lower == 'i') coreViseme = mappingDef.GetVisemeFor(PhonemeGroup.Smile);
                            else coreViseme = mappingDef.defaultViseme;
                        }
                        else
                        {
                            coreViseme = mappingDef.defaultViseme;
                        }
                    }

                    // 构建口型序列：[Attack] -> [Sustain] -> [Release]
                    
                    // Attack (起势)
                    visemes.Add(mappingDef.attackViseme);
                    
                    // Sustain (保持)
                    for (int i = 0; i < mappingDef.sustainFrames; i++)
                    {
                        visemes.Add(coreViseme);
                    }
                    
                    // Release (收势)
                    visemes.Add(mappingDef.releaseViseme);
                }
                
                // 结束静默
                visemes.Add(PersonaGeneration.VisemeCode.Closed);
                visemes.Add(PersonaGeneration.VisemeCode.Closed);

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

        private string BuildSSML(string text, string voice, float rate, float pitchVal)
        {
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

            int pitchPercent = (int)(pitchVal * 100);
            string pitchStr = pitchPercent >= 0 ? $"+{pitchPercent}%" : $"{pitchPercent}%";
            
            string escapedText = System.Security.SecurityElement.Escape(text);
            EmotionStyle emotion = GetCurrentEmotion();
            
            string ssml;
            if (voice.Contains("Neural") && emotion.StyleName != "chat")
            {
                ssml = $@"<speak version='1.0' xmlns='http://www.w3.org/2001/10/synthesis' xmlns:mstts='https://www.w3.org/2001/mstts' xml:lang='zh-CN'>
    <voice name='{voice}'>
        <mstts:express-as style='{emotion.StyleName}' styledegree='{emotion.StyleDegree:F2}'>
            <prosody rate='{rateStr}' pitch='{pitchStr}'>
                {escapedText}
            </prosody>
        </mstts:express-as>
    </voice>
</speak>";
            }
            else
            {
                ssml = $@"<speak version='1.0' xmlns='http://www.w3.org/2001/10/synthesis' xmlns:mstts='https://www.w3.org/2001/mstts' xml:lang='zh-CN'>
    <voice name='{voice}'>
        <prosody rate='{rateStr}' pitch='{pitchStr}'>
            {escapedText}
        </prosody>
    </voice>
</speak>";
            }

            return ssml;
        }

        private EmotionStyle GetCurrentEmotion()
        {
            try
            {
                var narratorManager = Verse.Current.Game?.GetComponent<Narrator.NarratorManager>();
                if (narratorManager == null) return new EmotionStyle("chat", 1.0f);

                var persona = narratorManager.GetCurrentPersona();
                if (persona == null) return new EmotionStyle("chat", 1.0f);

                return EmotionMapper.GetCurrentEmotionStyle(persona.defName);
            }
            catch (Exception ex)
            {
                if (Prefs.DevMode) Verse.Log.Warning($"[TTSService] Failed to get emotion: {ex.Message}");
                return new EmotionStyle("chat", 1.0f);
            }
        }

        public string GetAudioOutputDirectory() => audioOutputDir;

        public void OpenAudioDirectory()
        {
            if (Directory.Exists(audioOutputDir))
            {
                System.Diagnostics.Process.Start("explorer.exe", audioOutputDir);
            }
        }

        /// <summary>
        /// ✅ v2.7.2: 改进的缓存清理，跳过正在播放的文件
        /// </summary>
        public void ClearCache()
        {
            try
            {
                if (Directory.Exists(audioOutputDir))
                {
                    var files = Directory.GetFiles(audioOutputDir, "*.wav");
                    int deletedCount = 0;
                    int skippedCount = 0;
                    
                    foreach (var file in files)
                    {
                        try
                        {
                            File.Delete(file);
                            deletedCount++;
                        }
                        catch (IOException)
                        {
                            // 文件可能正在被使用，跳过
                            skippedCount++;
                        }
                    }
                    
                    if (Prefs.DevMode && (deletedCount > 0 || skippedCount > 0))
                    {
                        Log.Message($"[TTSService] Cache cleared: {deletedCount} deleted, {skippedCount} skipped (in use)");
                    }
                }
            }
            catch (Exception ex)
            {
                if (Prefs.DevMode) Log.Error($"[TTSService] Error clearing cache: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取 SiliconFlow (IndexTTS) 可用的语音列表
        /// </summary>
        public static List<string> GetSiliconFlowVoices()
        {
            return new List<string>
            {
                // IndexTTS-2 预设音色
                "alex",
                "anna",
                "bella",
                "benjamin",
                "charlie",
                "diana",
                "emily",
                "frank",
                "grace",
                "henry",
            };
        }
        
        /// <summary>
        /// 获取 OpenAI TTS 可用的语音列表
        /// </summary>
        public static List<string> GetOpenAIVoices()
        {
            return new List<string>
            {
                "alloy",
                "echo",
                "fable",
                "onyx",
                "nova",
                "shimmer",
            };
        }
        
        /// <summary>
        /// 获取 Edge TTS 可用的语音列表
        /// </summary>
        public static List<string> GetEdgeTTSVoices()
        {
            return EdgeTTSWebSocketClient.GetAvailableVoices();
        }
        
        /// <summary>
        /// 获取可用的语音列表（根据提供商）
        /// </summary>
        public static List<string> GetAvailableVoices(string provider = "azure")
        {
            if (provider?.ToLower() == "siliconflow")
            {
                return GetSiliconFlowVoices();
            }
            
            if (provider?.ToLower() == "openai")
            {
                return GetOpenAIVoices();
            }
            
            if (provider?.ToLower() == "edge")
            {
                return GetEdgeTTSVoices();
            }
            
            // 默认返回 Azure Neural 语音列表
            return new List<string>
            {
                // 中文语音（普通话）
                "zh-CN-XiaoxiaoNeural",
                "zh-CN-XiaoyiNeural",
                "zh-CN-YunjianNeural",
                "zh-CN-YunxiNeural",
                "zh-CN-YunxiaNeural",
                "zh-CN-YunyangNeural",
                "zh-CN-liaoning-XiaobeiNeural",
                "zh-CN-shaanxi-XiaoniNeural",
                "zh-CN-XiaoxiaoMultilingualNeural",
                "zh-CN-XiaochenNeural",
                "zh-CN-XiaohanNeural",
                "zh-CN-XiaomengNeural",
                "zh-CN-XiaomoNeural",
                "zh-CN-XiaoqiuNeural",
                "zh-CN-XiaoruiNeural",
                "zh-CN-XiaoshuangNeural",
                "zh-CN-XiaoxuanNeural",
                "zh-CN-XiaoyanNeural",
                "zh-CN-XiaoyouNeural",
                "zh-CN-XiaozhenNeural",
                "zh-CN-YunfengNeural",
                "zh-CN-YunhaoNeural",
                "zh-CN-YunjieNeural",
                "zh-CN-YunzeNeural",
                // 粤语
                "zh-HK-HiuMaanNeural",
                "zh-HK-HiuGaaiNeural",
                "zh-HK-WanLungNeural",
                // 台湾国语
                "zh-TW-HsiaoChenNeural",
                "zh-TW-HsiaoYuNeural",
                "zh-TW-YunJheNeural",
                // 英文（美式）
                "en-US-JennyNeural",
                "en-US-JennyMultilingualNeural",
                "en-US-GuyNeural",
                "en-US-AriaNeural",
                "en-US-DavisNeural",
                "en-US-AmberNeural",
                "en-US-AshleyNeural",
                "en-US-BrandonNeural",
                "en-US-ChristopherNeural",
                "en-US-CoraNeural",
                "en-US-ElizabethNeural",
                "en-US-EricNeural",
                "en-US-JacobNeural",
                "en-US-MichelleNeural",
                "en-US-MonicaNeural",
                "en-US-SaraNeural",
                "en-US-TonyNeural",
                // 英文（英式）
                "en-GB-SoniaNeural",
                "en-GB-RyanNeural",
                "en-GB-LibbyNeural",
                // 日文
                "ja-JP-NanamiNeural",
                "ja-JP-KeitaNeural",
                "ja-JP-AoiNeural",
                "ja-JP-DaichiNeural",
                "ja-JP-MayuNeural",
                "ja-JP-NaokiNeural",
                "ja-JP-ShioriNeural",
                // 韩文
                "ko-KR-SunHiNeural",
                "ko-KR-InJoonNeural",
            };
        }
    
    }

    public static class UnityWebRequestExtension
    {
        public static UnityWebRequestAwaiter GetAwaiter(this UnityWebRequestAsyncOperation asyncOp)
        {
            return new UnityWebRequestAwaiter(asyncOp);
        }
    }

    public struct UnityWebRequestAwaiter : System.Runtime.CompilerServices.INotifyCompletion
    {
        private UnityWebRequestAsyncOperation asyncOp;

        public UnityWebRequestAwaiter(UnityWebRequestAsyncOperation asyncOp)
        {
            this.asyncOp = asyncOp;
        }

        public bool IsCompleted => asyncOp.isDone;

        public void GetResult() { }

        public void OnCompleted(Action continuation)
        {
            asyncOp.completed += _ => continuation();
        }
    }
}
