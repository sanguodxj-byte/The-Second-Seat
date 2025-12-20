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
                    Log.Message("[TTSAudioPlayer] Instance created");
                }
                return instance;
            }
        }

        private AudioSource? currentAudioSource;
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

            // 启动"加载-播放-删除"协程
            StartCoroutine(LoadPlayDeleteCoroutine(filePath, personaDefName, onComplete));
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
                Log.Message($"[TTSAudioPlayer] Temp file saved: {tempPath}");
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
            bool success = false;

            // === 1. 使用 UnityWebRequest 加载音频 ===
            string fileUri = "file://" + filePath.Replace("\\", "/");
            
            using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip(fileUri, AudioType.WAV))
            {
                Log.Message($"[TTSAudioPlayer] Loading audio: {fileUri}");
                
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

                Log.Message($"[TTSAudioPlayer] AudioClip loaded: {clip.length} seconds");
            }

            // === 2. 创建临时 GameObject 和 AudioSource ===
            tempGO = new GameObject("TempAudioPlayer");
            currentAudioSource = tempGO.AddComponent<AudioSource>();
            
            // 配置 AudioSource
            currentAudioSource.clip = clip;
            currentAudioSource.volume = 1.0f;
            currentAudioSource.playOnAwake = false;
            
            // === 3. 设置播放状态（开始说话） ===
            if (!string.IsNullOrEmpty(personaDefName))
            {
                SetSpeakingState(personaDefName, true);
                Log.Message($"[TTSAudioPlayer] Speaking started: {personaDefName}");
            }

            // === 4. 播放音频 ===
            currentAudioSource.Play();
            Log.Message("[TTSAudioPlayer] Playing audio...");

            // === 5. 等待播放完成（添加缓冲时间） ===
            float totalWaitTime = clip.length + BUFFER_TIME;
            yield return new WaitForSeconds(totalWaitTime);

            Log.Message("[TTSAudioPlayer] Playback finished");
            success = true;

            // === 6. 清除播放状态（停止说话） ===
            if (!string.IsNullOrEmpty(personaDefName))
            {
                SetSpeakingState(personaDefName, false);
                Log.Message($"[TTSAudioPlayer] Speaking finished: {personaDefName}");
            }

            // === 7. 清理 AudioClip 和 GameObject ===
            // ? 显式销毁 AudioClip 释放内存和文件句柄
            if (clip != null)
            {
                Destroy(clip);
                clip = null;
                Log.Message("[TTSAudioPlayer] AudioClip destroyed");
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
                    Log.Message($"[TTSAudioPlayer] Temp file deleted: {filePath}");
                }
                catch (IOException ioEx)
                {
                    retryCount++;
                    lastException = ioEx;
                    shouldRetry = (retryCount < MAX_DELETE_RETRIES);
                    
                    Log.Warning($"[TTSAudioPlayer] Delete attempt {retryCount}/{MAX_DELETE_RETRIES} failed: {ioEx.Message}");
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

            if (!deleted)
            {
                Log.Warning($"[TTSAudioPlayer] Failed to delete temp file after {MAX_DELETE_RETRIES} retries: {filePath}");
            }
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
                
                Log.Message("[TTSAudioPlayer] Playback stopped");
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
                    catch (Exception ex)
                    {
                        Log.Warning($"[TTSAudioPlayer] Failed to cleanup temp file: {ex.Message}");
                    }
                }

                if (deletedCount > 0)
                {
                    Log.Message($"[TTSAudioPlayer] Cleaned up {deletedCount} temp files");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[TTSAudioPlayer] Cleanup failed: {ex.Message}");
            }
        }
    }
}
