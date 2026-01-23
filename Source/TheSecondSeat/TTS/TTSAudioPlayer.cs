using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using Verse;

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
        /// </summary>
        /// <param name="filePath">音频文件路径</param>
        /// <param name="personaDefName">人格 DefName（用于状态追踪）</param>
        /// <param name="onComplete">播放完成回调（可选）</param>
        public void PlayAndDelete(string filePath, string personaDefName, Action? onComplete = null)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                Log.Warning($"[TTSAudioPlayer] File not found: {filePath}");
                onComplete?.Invoke();
                return;
            }

        // ✅ v1.6.65: 打断机制 - 停止当前播放
        // ✅ v2.7.2: 修复打断时资源泄漏问题
        if (currentPlaybackCoroutine != null)
        {
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
        /// ? 协程：加载音频 → 播放 → 显式销毁 AudioClip → 删除文件
        /// ? v1.6.30: 追踪播放状态（支持口型同步）
        /// </summary>
        private IEnumerator LoadPlayDeleteCoroutine(string filePath, string personaDefName, Action? onComplete = null)
        {
            AudioClip? clip = null;
            GameObject? tempGO = null;
            
            // ✅ v2.7.2: 记录当前播放的文件路径（用于打断时删除）
            currentPlayingFilePath = filePath;

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
            
            using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip(fileUri, AudioType.WAV))
            {
                // 发送请求
                yield return www.SendWebRequest();

                // 检查错误
                if (www.result != UnityWebRequest.Result.Success)
                {
                    Log.Error($"[TTSAudioPlayer] Failed to load audio: {www.error}");
                    onComplete?.Invoke();
                    // 删除文件并退出
                    StartCoroutine(DeleteFileWithRetry(filePath));
                    yield break;
                }

                // 获取 AudioClip
                clip = DownloadHandlerAudioClip.GetContent(www);
                
                if (clip == null)
                {
                    Log.Error("[TTSAudioPlayer] AudioClip is null");
                    onComplete?.Invoke();
                    // 删除文件并退出
                    StartCoroutine(DeleteFileWithRetry(filePath));
                    yield break;
                }

            }

            // === 2. 创建临时 GameObject 和 AudioSource ===
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
                SetSpeakingState(personaDefName, true);
            }

            // === 4. 播放音频 ===
            currentAudioSource.Play();
            if (Prefs.DevMode) Log.Message($"[TTSAudioPlayer] Started playback. Duration: {clip.length:F2}s");

            // ⭐ 触发流式文本显示（与音频同步）
            UI.DialogueOverlayPanel.StartStreaming(clip.length);

            // === 5. 等待播放完成（添加缓冲时间） ===
            // 注意：使用 WaitForSecondsRealtime 确保暂停时也能计时
            float totalWaitTime = clip.length + BUFFER_TIME;
            yield return new WaitForSecondsRealtime(totalWaitTime);

            // === 6. 清除播放状态（停止说话） ===
            if (!string.IsNullOrEmpty(personaDefName))
            {
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
            // ? 启动独立协程删除文件，避免阻塞
            StartCoroutine(DeleteFileWithRetry(filePath));
            
            // ✅ v1.6.65: 清除协程引用
            currentPlaybackCoroutine = null;
            // ✅ v2.7.2: 清除文件路径引用
            currentPlayingFilePath = string.Empty;
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
