using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using Verse;
using NAudio.Wave;

namespace TheSecondSeat.TTS
{
    /// <summary>
    /// TTS 音频播放器（基于 Unity AudioSource）
    /// ? v1.6.27: 实现"播放后删除"生命周期管理
    /// ? v1.6.30: 添加播放状态追踪（支持口型同步）
    /// - 显式销毁 AudioClip 释放内存和文件锁
    /// - 添加缓冲时间确保播放完全结束
    /// - 实现重试删除机制
    /// - 追踪当前播放的人格DefName
    /// </summary>
    public class TTSAudioPlayer : MonoBehaviour
    {
        private static TTSAudioPlayer? instance;
        
        /// <summary>
        /// 单例实例
        /// </summary>
        public static TTSAudioPlayer Instance
        {
            get
            {
                if (instance == null)
                {
                    // 主线程创建 GameObject
                    var go = new GameObject("TTSAudioPlayer");
                    instance = go.AddComponent<TTSAudioPlayer>();
                    DontDestroyOnLoad(go); // 持久化
                }
                return instance;
            }
        }

        private AudioSource? currentAudioSource;
        private Coroutine? currentPlaybackCoroutine; // ✅ v1.6.65: 追踪当前协程
        private string currentPlayingFilePath = string.Empty; // ✅ v2.7.2: 追踪当前播放的文件路径
        private int currentPlaybackId = 0; // ✅ v2.8.3: 用于诊断的播放 ID
        private const float BUFFER_TIME = 0.5f; // 播放结束后的缓冲时间
        private const int MAX_DELETE_RETRIES = 3; // 最大删除重试次数
        private const float DELETE_RETRY_DELAY = 0.2f; // 删除重试延迟

        // ? v1.6.30: 播放状态追踪
        private static readonly Dictionary<string, bool> speakingStates = new Dictionary<string, bool>();
        private static string currentSpeakingPersona = string.Empty;

        /// <summary>
        /// ? v1.6.30: 检查指定人格是否正在说话（用于口型同步）
        /// </summary>
        /// <param name="personaDefName">人格 DefName</param>
        /// <returns>是否正在说话</returns>
        public static bool IsSpeaking(string personaDefName)
        {
            if (string.IsNullOrEmpty(personaDefName))
            {
                return false;
            }

            // 检查是否正在播放该人格的音频
            return speakingStates.TryGetValue(personaDefName, out bool isSpeaking) && isSpeaking;
        }

        /// <summary>
        /// ? v1.6.30: 检查是否有任何人格正在说话
        /// </summary>
        /// <returns>是否有音频正在播放</returns>
        public static bool IsAnyoneSpeaking()
        {
            return !string.IsNullOrEmpty(currentSpeakingPersona);
        }

        /// <summary>
        /// ? v1.6.30: 获取当前正在说话的人格DefName
        /// </summary>
        /// <returns>人格DefName，如果没有则返回空字符串</returns>
        public static string GetCurrentSpeaker()
        {
            return currentSpeakingPersona;
        }

        /// <summary>
        /// ? v1.6.30: 获取当前音频的 RMS 值（用于口型同步频谱模式）
        /// </summary>
        /// <param name="personaDefName">可选：指定人格DefName以确保只获取该人格的音频强度</param>
        /// <returns>音频 RMS 值 (0.0 - 1.0)</returns>
        public static float GetAudioRMS(string personaDefName = "")
        {
            // 如果指定了人格，且该人格不在说话，则返回 0
            if (!string.IsNullOrEmpty(personaDefName) && !IsSpeaking(personaDefName))
            {
                return 0f;
            }

            var player = Instance;
            if (player == null || player.currentAudioSource == null || !player.currentAudioSource.isPlaying)
            {
                return 0f;
            }

            // 获取音频采样数据
            // 使用 256 个样本进行计算
            float[] samples = new float[256];
            player.currentAudioSource.GetOutputData(samples, 0);

            float sum = 0f;
            for (int i = 0; i < samples.Length; i++)
            {
                sum += samples[i] * samples[i]; // 平方
            }

            // 均方根
            return Mathf.Sqrt(sum / samples.Length);
        }

        /// <summary>
        /// ? v1.6.30: 设置播放状态
        /// </summary>
        private static void SetSpeakingState(string personaDefName, bool isSpeaking)
        {
            if (string.IsNullOrEmpty(personaDefName))
            {
                return;
            }

            speakingStates[personaDefName] = isSpeaking;

            if (isSpeaking)
            {
                currentSpeakingPersona = personaDefName;
            }
            else if (currentSpeakingPersona == personaDefName)
            {
                currentSpeakingPersona = string.Empty;
            }
        }

        /// <summary>
        /// 从字节数组播放音频
        /// 必须在主线程调用
        /// </summary>
        /// <param name="audioData">音频数据</param>
        /// <param name="onComplete">播放完成回调（可选）</param>
        public void PlayFromBytes(byte[] audioData, Action? onComplete = null)
        {
            PlayFromBytes(audioData, string.Empty, onComplete);
        }

        /// <summary>
        /// ? v1.6.30: 从字节数组播放音频（支持人格追踪）
        /// </summary>
        /// <param name="audioData">音频数据</param>
        /// <param name="personaDefName">人格 DefName（用于状态追踪）</param>
        /// <param name="onComplete">播放完成回调（可选）</param>
        public void PlayFromBytes(byte[] audioData, string personaDefName, Action? onComplete = null)
        {
            if (audioData == null || audioData.Length == 0)
            {
                Log.Warning("[TTSAudioPlayer] Audio data is empty");
                onComplete?.Invoke();
                return;
            }

            try
            {
                // 1. 保存为临时文件
                string tempFilePath = SaveToTempFile(audioData);
                
                if (string.IsNullOrEmpty(tempFilePath))
                {
                    Log.Error("[TTSAudioPlayer] Failed to save temp file");
                    onComplete?.Invoke();
                    return;
                }

                // 2. 使用"播放后删除"模式
                PlayAndDelete(tempFilePath, personaDefName, onComplete);
            }
            catch (Exception ex)
            {
                Log.Error($"[TTSAudioPlayer] Error: {ex.Message}");
                onComplete?.Invoke();
            }
        }

        /// <summary>
        /// ? 播放音频并在完成后删除文件
        /// </summary>
        /// <param name="filePath">音频文件路径</param>
        /// <param name="onComplete">播放完成回调（可选）</param>
        public void PlayAndDelete(string filePath, Action? onComplete = null)
        {
            PlayAndDelete(filePath, string.Empty, onComplete);
        }

        /// <summary>
        /// ? v1.6.30: 播放音频并在完成后删除文件（支持人格追踪）
        /// ? v2.8.0: 增强诊断日志
        /// </summary>
        /// <param name="filePath">音频文件路径</param>
        /// <param name="personaDefName">人格 DefName（用于状态追踪）</param>
        /// <param name="onComplete">播放完成回调（可选）</param>
        public void PlayAndDelete(string filePath, string personaDefName, Action? onComplete = null)
        {
            // ✅ v2.8.3: 生成唯一播放 ID 用于诊断追踪
            int playbackId = ++currentPlaybackId;
            
            if (string.IsNullOrEmpty(filePath))
            {
                Log.Warning($"[TTSAudioPlayer] PlayAndDelete[{playbackId}] called with empty path");
                onComplete?.Invoke();
                return;
            }
            
            if (!File.Exists(filePath))
            {
                Log.Warning($"[TTSAudioPlayer] PlayAndDelete[{playbackId}] File not found: {filePath}");
                onComplete?.Invoke();
                return;
            }
            
            if (Prefs.DevMode)
            {
                var fileInfo = new FileInfo(filePath);
                Log.Message($"[TTSAudioPlayer] PlayAndDelete[{playbackId}] ENTERED: persona={personaDefName}, file={Path.GetFileName(filePath)}, size={fileInfo.Length} bytes");
            }

        // ✅ v1.6.65: 打断机制 - 停止当前播放
        // ✅ v2.7.2: 修复打断时资源泄漏问题
        if (currentPlaybackCoroutine != null)
        {
            if (Prefs.DevMode)
            {
                Log.Message($"[TTSAudioPlayer] Interrupting previous playback, deleting: {currentPlayingFilePath}");
            }
            
            StopCoroutine(currentPlaybackCoroutine);
            currentPlaybackCoroutine = null;
            
            // 立即停止当前音频并清理资源
            if (currentAudioSource != null)
            {
                if (currentAudioSource.isPlaying)
                {
                    currentAudioSource.Stop();
                }
                
                // 销毁 AudioClip 释放文件句柄
                if (currentAudioSource.clip != null)
                {
                    var clipToDestroy = currentAudioSource.clip;
                    currentAudioSource.clip = null;
                    Destroy(clipToDestroy);
                }
                
                // 销毁临时 GameObject
                if (currentAudioSource.gameObject != null && currentAudioSource.gameObject != this.gameObject)
                {
                    Destroy(currentAudioSource.gameObject);
                }
                currentAudioSource = null;
            }
            
            // ✅ v2.8.6: 恢复删除逻辑（MP3 转换已实现）
            // 删除被打断的音频文件
            if (!string.IsNullOrEmpty(currentPlayingFilePath) && File.Exists(currentPlayingFilePath))
            {
                StartCoroutine(DeleteFileWithRetry(currentPlayingFilePath));
                currentPlayingFilePath = string.Empty;
            }
            
            // 清除旧的播放状态
            if (!string.IsNullOrEmpty(currentSpeakingPersona))
            {
                SetSpeakingState(currentSpeakingPersona, false);
            }
            
            // 停止嘴部动画
            PersonaGeneration.MouthAnimationSystem.StopAnimation();
        }

            // 启动新的播放协程
            if (Prefs.DevMode)
            {
                Log.Message($"[TTSAudioPlayer] Starting LoadPlayDeleteCoroutine for: {filePath}");
            }
            currentPlaybackCoroutine = StartCoroutine(LoadPlayDeleteCoroutine(filePath, personaDefName, onComplete));
        }

        /// <summary>
        /// 保存字节数组到临时 WAV 文件
        /// </summary>
        private string SaveToTempFile(byte[] data)
        {
            try
            {
                string tempPath = Path.Combine(Application.temporaryCachePath, $"tts_temp_{DateTime.Now:yyyyMMddHHmmss}.wav");
                File.WriteAllBytes(tempPath, data);
                return tempPath;
            }
            catch (Exception ex)
            {
                Log.Error($"[TTSAudioPlayer] Failed to save temp file: {ex.Message}");
                return string.Empty;
            }
        }

        /// <summary>
        /// ✅ 协程：加载音频 → 播放 → 显式销毁 AudioClip → 删除文件
        /// ✅ v1.6.30: 追踪播放状态（支持口型同步）
        /// ✅ v2.8.0: 修复文件写入未完成就尝试加载的问题
        /// ✅ v2.8.1: 增强诊断日志
        /// </summary>
        private IEnumerator LoadPlayDeleteCoroutine(string filePath, string personaDefName, Action? onComplete = null)
        {
            if (Prefs.DevMode)
            {
                Log.Message($"[TTSAudioPlayer] LoadPlayDeleteCoroutine STARTED for persona={personaDefName}, file={filePath}");
            }
            
            AudioClip? clip = null;
            GameObject? tempGO = null;
            
            // ✅ v2.7.2: 记录当前播放的文件路径（用于打断时删除）
            currentPlayingFilePath = filePath;

            // ✅ v2.8.0: 等待文件系统完全写入（短暂延迟）
            // 确保文件句柄已释放且内容完整
            if (Prefs.DevMode)
            {
                Log.Message($"[TTSAudioPlayer] Waiting 0.1s for file system sync...");
            }
            yield return new WaitForSecondsRealtime(0.1f);
            
            // ✅ v2.8.0: 验证文件仍然存在且大小合理
            if (!File.Exists(filePath))
            {
                Log.Error($"[TTSAudioPlayer] File disappeared before playback: {filePath}");
                onComplete?.Invoke();
                yield break;
            }
            
            FileInfo fileInfo = null;
            try
            {
                fileInfo = new FileInfo(filePath);
            }
            catch (Exception ex)
            {
                Log.Error($"[TTSAudioPlayer] Cannot access file info: {ex.Message}");
            }
            
            if (fileInfo == null || fileInfo.Length < 100)
            {
                Log.Warning($"[TTSAudioPlayer] Audio file too small or invalid ({fileInfo?.Length ?? 0} bytes), waiting...");
                yield return new WaitForSecondsRealtime(0.3f); // 额外等待
            }
            
            // ✅ v2.8.6: 检测 MP3 格式并转换为 WAV
            // SiliconFlow 返回 MP3 格式（尽管请求了 WAV/opus），Unity 无法播放 MP3
            // ✅ v2.8.7: 也检测 OGG/Opus 格式，Unity 可以直接播放
            bool needsConversion = false;
            string detectedFormat = "unknown";
            
            if (IsMp3File(filePath))
            {
                needsConversion = true;
                detectedFormat = "MP3";
            }
            else if (IsOggFile(filePath))
            {
                needsConversion = false; // Unity 可以直接播放 OGG
                detectedFormat = "OGG";
                if (Prefs.DevMode)
                {
                    Log.Message($"[TTSAudioPlayer] Detected OGG file (despite .wav extension), Unity can play directly");
                }
            }
            
            if (needsConversion)
            {
                if (Prefs.DevMode)
                {
                    Log.Message($"[TTSAudioPlayer] Detected {detectedFormat} file (despite .wav extension), converting to WAV...");
                }
                
                string wavPath = filePath.Replace(".wav", "_converted.wav");
                if (Path.GetExtension(filePath).ToLowerInvariant() == ".mp3")
                {
                    wavPath = filePath.Replace(".mp3", "_converted.wav");
                }
                
                bool convertSuccess = TryConvertMp3ToWav(filePath, wavPath);
                
                if (convertSuccess && File.Exists(wavPath))
                {
                    if (Prefs.DevMode)
                    {
                        Log.Message($"[TTSAudioPlayer] MP3->WAV conversion successful, using: {wavPath}");
                    }
                    
                    // 删除原始 MP3 文件
                    try { File.Delete(filePath); } catch { }
                    
                    // 更新文件路径为转换后的 WAV
                    filePath = wavPath;
                    currentPlayingFilePath = filePath;
                }
                else
                {
                    Log.Error($"[TTSAudioPlayer] Failed to convert MP3 to WAV, cannot play audio");
                    onComplete?.Invoke();
                    StartCoroutine(DeleteFileWithRetry(filePath));
                    yield break;
                }
            }

            // === 1. 使用 UnityWebRequest 加载音频 ===
            // ⭐ v2.2.2: 修复路径包含特殊字符（如空格）导致加载失败的问题
            string fileUri = "file://" + filePath.Replace("\\", "/");
            try
            {
                fileUri = new System.Uri(filePath).AbsoluteUri;
            }
            catch
            {
                // 回退到简单拼接
                fileUri = "file://" + filePath.Replace("\\", "/");
            }
            
            // ✅ v2.8.1: 根据文件扩展名检测音频格式
            // ✅ v2.8.7: 优先使用实际检测的格式，而非扩展名
            AudioType audioType = AudioType.WAV;
            string ext = Path.GetExtension(filePath).ToLowerInvariant();
            
            // 如果检测到 OGG 格式（即使扩展名是 .wav），使用 OGG Vorbis
            if (detectedFormat == "OGG")
            {
                audioType = AudioType.OGGVORBIS;
                if (Prefs.DevMode) Log.Message("[TTSAudioPlayer] Using OGG Vorbis audio type (detected by file header)");
            }
            else
            {
                switch (ext)
                {
                    case ".mp3":
                        audioType = AudioType.MPEG;
                        if (Prefs.DevMode) Log.Message("[TTSAudioPlayer] Using MPEG (MP3) audio type");
                        break;
                    case ".ogg":
                        audioType = AudioType.OGGVORBIS;
                        if (Prefs.DevMode) Log.Message("[TTSAudioPlayer] Using OGG Vorbis audio type");
                        break;
                    case ".wav":
                    default:
                        audioType = AudioType.WAV;
                        if (Prefs.DevMode) Log.Message("[TTSAudioPlayer] Using WAV audio type");
                        break;
                }
            }
            
            // ✅ v2.8.0: 增加重试机制（最多3次）
            const int maxLoadRetries = 3;
            int loadRetryCount = 0;
            bool loadSuccess = false;
            
            while (loadRetryCount < maxLoadRetries && !loadSuccess)
            {
                using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip(fileUri, audioType))
                {
                    // 发送请求
                    yield return www.SendWebRequest();

                    // 检查错误
                    if (www.result != UnityWebRequest.Result.Success)
                    {
                        loadRetryCount++;
                        if (loadRetryCount < maxLoadRetries)
                        {
                            if (Prefs.DevMode)
                            {
                                Log.Warning($"[TTSAudioPlayer] Load failed (attempt {loadRetryCount}/{maxLoadRetries}): {www.error}, retrying...");
                            }
                            yield return new WaitForSecondsRealtime(0.2f);
                            continue;
                        }
                        else
                        {
                            Log.Error($"[TTSAudioPlayer] Failed to load audio after {maxLoadRetries} attempts: {www.error}");
                            onComplete?.Invoke();
                            StartCoroutine(DeleteFileWithRetry(filePath));
                            yield break;
                        }
                    }
                    
                    // 加载成功
                    loadSuccess = true;

                    // 获取 AudioClip
                    clip = DownloadHandlerAudioClip.GetContent(www);
                    
                    if (clip == null)
                    {
                        Log.Error("[TTSAudioPlayer] AudioClip is null");
                        onComplete?.Invoke();
                        StartCoroutine(DeleteFileWithRetry(filePath));
                        yield break;
                    }
                }
            }

            // === 2. 创建临时 GameObject 和 AudioSource ===
            if (Prefs.DevMode)
            {
                Log.Message($"[TTSAudioPlayer] Creating AudioSource, clip duration={clip.length:F2}s, samples={clip.samples}, channels={clip.channels}, frequency={clip.frequency}");
            }
            
            // ✅ v2.8.2: 如果 clip.length 为 0，说明 Unity 无法解析 WAV 文件
            if (clip.length <= 0f || clip.samples <= 0)
            {
                Log.Error($"[TTSAudioPlayer] AudioClip loaded but has no audio data! length={clip.length}, samples={clip.samples}");
                Log.Error("[TTSAudioPlayer] This usually means the WAV format (sample rate, bit depth, or channels) is not supported by Unity.");
                Log.Error("[TTSAudioPlayer] Unity AudioClip supports: 8/16/24/32-bit PCM, 22050/44100/48000Hz are most reliable.");
                
                // 尝试解析 WAV 头获取更多信息
                int wavSampleRate = 0;
                int wavChannels = 0;
                int wavBitsPerSample = 0;
                try
                {
                    byte[] wavData = File.ReadAllBytes(filePath);
                    if (wavData.Length >= 44)
                    {
                        // WAV 头解析
                        wavChannels = BitConverter.ToInt16(wavData, 22);
                        wavSampleRate = BitConverter.ToInt32(wavData, 24);
                        wavBitsPerSample = BitConverter.ToInt16(wavData, 34);
                        Log.Error($"[TTSAudioPlayer] WAV header: channels={wavChannels}, sampleRate={wavSampleRate}Hz, bitsPerSample={wavBitsPerSample}");
                    }
                }
                catch (Exception wavEx)
                {
                    Log.Error($"[TTSAudioPlayer] Failed to parse WAV header: {wavEx.Message}");
                }
                
                // ✅ v2.8.4: 尝试手动重采样 WAV 文件
                if (wavSampleRate > 0 && wavSampleRate != 22050 && wavSampleRate != 44100 && wavSampleRate != 48000)
                {
                    Log.Message($"[TTSAudioPlayer] Attempting to resample WAV from {wavSampleRate}Hz to 44100Hz...");
                    
                    string resampledPath = filePath.Replace(".wav", "_resampled.wav");
                    bool resampleSuccess = TryResampleWav(filePath, resampledPath, wavSampleRate, 44100, wavChannels, wavBitsPerSample);
                    
                    if (resampleSuccess && File.Exists(resampledPath))
                    {
                        Log.Message($"[TTSAudioPlayer] Resample successful, trying to load resampled file...");
                        
                        // 销毁原始 clip
                        Destroy(clip);
                        clip = null;
                        
                        // 删除原始文件
                        try { File.Delete(filePath); } catch { }
                        
                        // 更新文件路径
                        filePath = resampledPath;
                        currentPlayingFilePath = filePath;
                        
                        // 重新加载重采样后的文件
                        string resampledUri = new System.Uri(resampledPath).AbsoluteUri;
                        using (UnityWebRequest www2 = UnityWebRequestMultimedia.GetAudioClip(resampledUri, AudioType.WAV))
                        {
                            yield return www2.SendWebRequest();
                            
                            if (www2.result == UnityWebRequest.Result.Success)
                            {
                                clip = DownloadHandlerAudioClip.GetContent(www2);
                                if (clip != null && clip.length > 0)
                                {
                                    Log.Message($"[TTSAudioPlayer] Resampled audio loaded successfully: {clip.length:F2}s, {clip.frequency}Hz");
                                    // 继续播放流程
                                }
                                else
                                {
                                    Log.Error("[TTSAudioPlayer] Resampled audio still has no data!");
                                    onComplete?.Invoke();
                                    if (clip != null) Destroy(clip);
                                    StartCoroutine(DeleteFileWithRetry(resampledPath));
                                    yield break;
                                }
                            }
                            else
                            {
                                Log.Error($"[TTSAudioPlayer] Failed to load resampled audio: {www2.error}");
                                onComplete?.Invoke();
                                StartCoroutine(DeleteFileWithRetry(resampledPath));
                                yield break;
                            }
                        }
                    }
                    else
                    {
                        Log.Error("[TTSAudioPlayer] Resample failed, cannot play this audio format.");
                        onComplete?.Invoke();
                        Destroy(clip);
                        StartCoroutine(DeleteFileWithRetry(filePath));
                        yield break;
                    }
                }
                else
                {
                    // 无法处理的格式
                    onComplete?.Invoke();
                    Destroy(clip);
                    StartCoroutine(DeleteFileWithRetry(filePath));
                    yield break;
                }
            }
            
            tempGO = new GameObject("TempAudioPlayer");
            currentAudioSource = tempGO.AddComponent<AudioSource>();
            
            // 配置 AudioSource
            currentAudioSource.clip = clip;
            currentAudioSource.volume = 1.0f;
            currentAudioSource.playOnAwake = false;
            // ⭐ v2.2.2: 允许在游戏暂停时播放
            currentAudioSource.ignoreListenerPause = true;
            
            // === 3. 设置播放状态（开始说话） ===
            if (!string.IsNullOrEmpty(personaDefName))
            {
                if (Prefs.DevMode)
                {
                    Log.Message($"[TTSAudioPlayer] Setting speaking state TRUE for persona={personaDefName}");
                }
                SetSpeakingState(personaDefName, true);
            }

            // === 4. 播放音频 ===
            if (Prefs.DevMode)
            {
                Log.Message($"[TTSAudioPlayer] Calling AudioSource.Play()...");
            }
            currentAudioSource.Play();
            
            // ✅ v2.8.1: 验证播放是否真的开始了
            if (Prefs.DevMode)
            {
                Log.Message($"[TTSAudioPlayer] AudioSource.isPlaying={currentAudioSource.isPlaying}, time={currentAudioSource.time:F3}s");
            }

            // ⭐ 触发流式文本显示（与音频同步）
            UI.DialogueOverlayPanel.StartStreaming(clip.length);

            // === 5. 等待播放完成（添加缓冲时间） ===
            // 注意：使用 WaitForSecondsRealtime 确保暂停时也能计时
            float totalWaitTime = clip.length + BUFFER_TIME;
            if (Prefs.DevMode)
            {
                Log.Message($"[TTSAudioPlayer] Waiting {totalWaitTime:F2}s for playback to complete (clip={clip.length:F2}s + buffer={BUFFER_TIME}s)");
            }
            yield return new WaitForSecondsRealtime(totalWaitTime);

            // === 6. 清除播放状态（停止说话） ===
            if (Prefs.DevMode)
            {
                Log.Message($"[TTSAudioPlayer] Playback wait completed, cleaning up...");
            }
            if (!string.IsNullOrEmpty(personaDefName))
            {
                if (Prefs.DevMode)
                {
                    Log.Message($"[TTSAudioPlayer] Setting speaking state FALSE for persona={personaDefName}");
                }
                SetSpeakingState(personaDefName, false);
            }

            // === 7. 清理 AudioClip 和 GameObject ===
            // ? 显式销毁 AudioClip 释放内存和文件句柄
            if (clip != null)
            {
                Destroy(clip);
                clip = null;
            }

            // 销毁临时 GameObject
            if (tempGO != null)
            {
                Destroy(tempGO);
                tempGO = null;
            }

            currentAudioSource = null;

            // === 8. 调用播放完成回调 ===
            onComplete?.Invoke();

            // === 9. 删除临时文件（带重试机制） ===
            // ✅ v2.8.6: 恢复删除逻辑（MP3 转换已实现）
            StartCoroutine(DeleteFileWithRetry(filePath));
            
            // ✅ v1.6.65: 清除协程引用
            currentPlaybackCoroutine = null;
            // ✅ v2.7.2: 清除文件路径引用
            currentPlayingFilePath = string.Empty;
            
            if (Prefs.DevMode)
            {
                Log.Message($"[TTSAudioPlayer] LoadPlayDeleteCoroutine COMPLETED successfully for persona={personaDefName}");
            }
        }

        /// <summary>
        /// ? 带重试机制的文件删除
        /// </summary>
        private IEnumerator DeleteFileWithRetry(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                yield break;
            }

            int retryCount = 0;
            bool deleted = false;

            while (retryCount < MAX_DELETE_RETRIES && !deleted)
            {
                bool shouldRetry = false;
                Exception? lastException = null;

                try
                {
                    File.Delete(filePath);
                    deleted = true;
                }
                catch (IOException ioEx)
                {
                    retryCount++;
                    lastException = ioEx;
                    shouldRetry = (retryCount < MAX_DELETE_RETRIES);
                }
                catch (Exception ex)
                {
                    Log.Error($"[TTSAudioPlayer] Failed to delete temp file: {ex.Message}");
                    break; // 非IO异常，直接退出
                }

                // ? 在try-catch外部处理等待
                if (shouldRetry && !deleted)
                {
                    yield return new WaitForSeconds(DELETE_RETRY_DELAY);
                }
            }

            // 静默处理删除失败
        }

        /// <summary>
        /// 停止当前播放
        /// ? v1.6.30: 清除播放状态
        /// </summary>
        public void Stop()
        {
            if (currentAudioSource != null && currentAudioSource.isPlaying)
            {
                currentAudioSource.Stop();
                
                // 清除播放状态
                if (!string.IsNullOrEmpty(currentSpeakingPersona))
                {
                    SetSpeakingState(currentSpeakingPersona, false);
                }
            }

            // 如果有正在运行的协程，则停止协程
            if (currentPlaybackCoroutine != null)
            {
                StopCoroutine(currentPlaybackCoroutine);
                currentPlaybackCoroutine = null;
            }
        }

        /// <summary>
        /// 销毁时清理
        /// </summary>
        private void OnDestroy()
        {
            Stop();
            speakingStates.Clear();
            currentSpeakingPersona = string.Empty;
            instance = null;
        }

        /// <summary>
        /// ✅ v2.8.6: 使用 NAudio 将 MP3 转换为 WAV
        /// 解决 SiliconFlow 返回 MP3 但 Unity 无法播放的问题
        /// </summary>
        /// <param name="mp3Path">MP3 文件路径</param>
        /// <param name="wavPath">输出 WAV 文件路径</param>
        /// <returns>是否成功</returns>
        private bool TryConvertMp3ToWav(string mp3Path, string wavPath)
        {
            try
            {
                if (Prefs.DevMode)
                {
                    Log.Message($"[TTSAudioPlayer] Converting MP3 to WAV: {mp3Path} -> {wavPath}");
                }
                
                using (var mp3Reader = new Mp3FileReader(mp3Path))
                {
                    // 使用 WaveFormatConversionStream 转换为 PCM
                    // 目标格式: 44100Hz, 16-bit, Mono (Unity 友好格式)
                    var targetFormat = new WaveFormat(44100, 16, 1);
                    
                    using (var conversionStream = new WaveFormatConversionStream(targetFormat, mp3Reader))
                    {
                        WaveFileWriter.CreateWaveFile(wavPath, conversionStream);
                    }
                }
                
                if (File.Exists(wavPath))
                {
                    var wavInfo = new FileInfo(wavPath);
                    if (Prefs.DevMode)
                    {
                        Log.Message($"[TTSAudioPlayer] MP3->WAV conversion successful: {wavInfo.Length} bytes");
                    }
                    return true;
                }
                
                return false;
            }
            catch (Exception ex)
            {
                Log.Error($"[TTSAudioPlayer] MP3->WAV conversion failed: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// ✅ v2.8.6: 检测文件是否为 MP3 格式（通过文件头）
        /// </summary>
        private bool IsMp3File(string filePath)
        {
            try
            {
                byte[] header = new byte[4];
                using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                {
                    if (fs.Read(header, 0, 4) < 4) return false;
                }
                
                // 检查 ID3 标签 (ID3v2)
                if (header[0] == 'I' && header[1] == 'D' && header[2] == '3')
                {
                    return true;
                }
                
                // 检查 MP3 帧同步字 (0xFF 0xFB, 0xFF 0xFA, 0xFF 0xF3, 0xFF 0xF2)
                if (header[0] == 0xFF && (header[1] & 0xE0) == 0xE0)
                {
                    return true;
                }
                
                return false;
            }
            catch
            {
                return false;
            }
        }
        
        /// <summary>
        /// ✅ v2.8.7: 检测文件是否为 OGG 格式（通过文件头）
        /// OGG 文件以 "OggS" 开头
        /// </summary>
        private bool IsOggFile(string filePath)
        {
            try
            {
                byte[] header = new byte[4];
                using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                {
                    if (fs.Read(header, 0, 4) < 4) return false;
                }
                
                // 检查 OGG 魔数 "OggS" (0x4F 0x67 0x67 0x53)
                if (header[0] == 'O' && header[1] == 'g' && header[2] == 'g' && header[3] == 'S')
                {
                    return true;
                }
                
                return false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// ✅ v2.8.4: 尝试重采样 WAV 文件到 Unity 兼容的采样率
        /// 使用简单的线性插值进行重采样
        /// </summary>
        /// <param name="inputPath">输入 WAV 文件路径</param>
        /// <param name="outputPath">输出 WAV 文件路径</param>
        /// <param name="sourceSampleRate">源采样率</param>
        /// <param name="targetSampleRate">目标采样率</param>
        /// <param name="channels">声道数</param>
        /// <param name="bitsPerSample">位深度</param>
        /// <returns>是否成功</returns>
        private bool TryResampleWav(string inputPath, string outputPath, int sourceSampleRate, int targetSampleRate, int channels, int bitsPerSample)
        {
            try
            {
                byte[] inputData = File.ReadAllBytes(inputPath);
                
                // 验证 WAV 头
                if (inputData.Length < 44)
                {
                    Log.Error("[TTSAudioPlayer] WAV file too small for valid header");
                    return false;
                }
                
                string riff = System.Text.Encoding.ASCII.GetString(inputData, 0, 4);
                string wave = System.Text.Encoding.ASCII.GetString(inputData, 8, 4);
                
                if (riff != "RIFF" || wave != "WAVE")
                {
                    Log.Error($"[TTSAudioPlayer] Invalid WAV header: {riff}...{wave}");
                    return false;
                }
                
                // 找到 data chunk
                int dataOffset = 12;
                int dataSize = 0;
                
                while (dataOffset < inputData.Length - 8)
                {
                    string chunkId = System.Text.Encoding.ASCII.GetString(inputData, dataOffset, 4);
                    int chunkSize = BitConverter.ToInt32(inputData, dataOffset + 4);
                    
                    if (chunkId == "data")
                    {
                        dataOffset += 8;
                        dataSize = chunkSize;
                        break;
                    }
                    
                    dataOffset += 8 + chunkSize;
                    
                    // 确保对齐到 2 字节边界
                    if (chunkSize % 2 == 1) dataOffset++;
                }
                
                if (dataSize == 0 || dataOffset + dataSize > inputData.Length)
                {
                    Log.Error("[TTSAudioPlayer] Could not find valid data chunk in WAV");
                    return false;
                }
                
                // 计算采样数
                int bytesPerSample = bitsPerSample / 8;
                int frameSize = bytesPerSample * channels;
                int sourceFrameCount = dataSize / frameSize;
                
                // 计算目标采样数
                double ratio = (double)targetSampleRate / sourceSampleRate;
                int targetFrameCount = (int)(sourceFrameCount * ratio);
                
                if (Prefs.DevMode)
                {
                    Log.Message($"[TTSAudioPlayer] Resampling: {sourceFrameCount} frames @ {sourceSampleRate}Hz -> {targetFrameCount} frames @ {targetSampleRate}Hz");
                }
                
                // 提取源采样数据为 float 数组
                float[][] sourceChannels = new float[channels][];
                for (int c = 0; c < channels; c++)
                {
                    sourceChannels[c] = new float[sourceFrameCount];
                }
                
                for (int i = 0; i < sourceFrameCount; i++)
                {
                    int offset = dataOffset + i * frameSize;
                    
                    for (int c = 0; c < channels; c++)
                    {
                        int sampleOffset = offset + c * bytesPerSample;
                        
                        float sample = 0f;
                        if (bytesPerSample == 2)
                        {
                            short s = BitConverter.ToInt16(inputData, sampleOffset);
                            sample = s / 32768f;
                        }
                        else if (bytesPerSample == 3)
                        {
                            // 24-bit
                            int s = inputData[sampleOffset] | (inputData[sampleOffset + 1] << 8) | (inputData[sampleOffset + 2] << 16);
                            if ((s & 0x800000) != 0) s |= unchecked((int)0xFF000000); // 符号扩展
                            sample = s / 8388608f;
                        }
                        else if (bytesPerSample == 4)
                        {
                            int s = BitConverter.ToInt32(inputData, sampleOffset);
                            sample = s / 2147483648f;
                        }
                        else if (bytesPerSample == 1)
                        {
                            // 8-bit unsigned
                            sample = (inputData[sampleOffset] - 128) / 128f;
                        }
                        
                        sourceChannels[c][i] = sample;
                    }
                }
                
                // 线性插值重采样
                float[][] targetChannels = new float[channels][];
                for (int c = 0; c < channels; c++)
                {
                    targetChannels[c] = new float[targetFrameCount];
                    
                    for (int i = 0; i < targetFrameCount; i++)
                    {
                        double srcIndex = i / ratio;
                        int idx0 = (int)srcIndex;
                        int idx1 = Math.Min(idx0 + 1, sourceFrameCount - 1);
                        double frac = srcIndex - idx0;
                        
                        targetChannels[c][i] = (float)(sourceChannels[c][idx0] * (1.0 - frac) + sourceChannels[c][idx1] * frac);
                    }
                }
                
                // 构建输出 WAV
                int targetDataSize = targetFrameCount * frameSize;
                int outputSize = 44 + targetDataSize;
                byte[] outputData = new byte[outputSize];
                
                // RIFF header
                System.Text.Encoding.ASCII.GetBytes("RIFF").CopyTo(outputData, 0);
                BitConverter.GetBytes(outputSize - 8).CopyTo(outputData, 4);
                System.Text.Encoding.ASCII.GetBytes("WAVE").CopyTo(outputData, 8);
                
                // fmt chunk
                System.Text.Encoding.ASCII.GetBytes("fmt ").CopyTo(outputData, 12);
                BitConverter.GetBytes(16).CopyTo(outputData, 16); // chunk size
                BitConverter.GetBytes((short)1).CopyTo(outputData, 20); // audio format (PCM)
                BitConverter.GetBytes((short)channels).CopyTo(outputData, 22);
                BitConverter.GetBytes(targetSampleRate).CopyTo(outputData, 24);
                int byteRate = targetSampleRate * channels * bytesPerSample;
                BitConverter.GetBytes(byteRate).CopyTo(outputData, 28);
                BitConverter.GetBytes((short)frameSize).CopyTo(outputData, 32); // block align
                BitConverter.GetBytes((short)bitsPerSample).CopyTo(outputData, 34);
                
                // data chunk
                System.Text.Encoding.ASCII.GetBytes("data").CopyTo(outputData, 36);
                BitConverter.GetBytes(targetDataSize).CopyTo(outputData, 40);
                
                // 写入采样数据
                for (int i = 0; i < targetFrameCount; i++)
                {
                    int offset = 44 + i * frameSize;
                    
                    for (int c = 0; c < channels; c++)
                    {
                        int sampleOffset = offset + c * bytesPerSample;
                        float sample = Mathf.Clamp(targetChannels[c][i], -1f, 1f);
                        
                        if (bytesPerSample == 2)
                        {
                            short s = (short)(sample * 32767f);
                            BitConverter.GetBytes(s).CopyTo(outputData, sampleOffset);
                        }
                        else if (bytesPerSample == 3)
                        {
                            int s = (int)(sample * 8388607f);
                            outputData[sampleOffset] = (byte)(s & 0xFF);
                            outputData[sampleOffset + 1] = (byte)((s >> 8) & 0xFF);
                            outputData[sampleOffset + 2] = (byte)((s >> 16) & 0xFF);
                        }
                        else if (bytesPerSample == 4)
                        {
                            int s = (int)(sample * 2147483647f);
                            BitConverter.GetBytes(s).CopyTo(outputData, sampleOffset);
                        }
                        else if (bytesPerSample == 1)
                        {
                            byte s = (byte)((sample + 1f) * 127.5f);
                            outputData[sampleOffset] = s;
                        }
                    }
                }
                
                // 写入文件
                File.WriteAllBytes(outputPath, outputData);
                
                if (Prefs.DevMode)
                {
                    Log.Message($"[TTSAudioPlayer] Resample complete: {outputPath} ({outputData.Length} bytes)");
                }
                
                return true;
            }
            catch (Exception ex)
            {
                Log.Error($"[TTSAudioPlayer] Resample failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// ? 清理所有临时音频文件
        /// </summary>
        public void CleanupTempFiles()
        {
            try
            {
                string tempDir = Application.temporaryCachePath;
                var files = Directory.GetFiles(tempDir, "tts_temp_*.wav");
                
                int deletedCount = 0;
                foreach (var file in files)
                {
                    try
                    {
                        File.Delete(file);
                        deletedCount++;
                    }
                    catch
                    {
                        // 静默处理清理失败
                    }
                }

                // 静默清理
            }
            catch (Exception ex)
            {
                Log.Error($"[TTSAudioPlayer] Cleanup failed: {ex.Message}");
            }
        }
    }
}
