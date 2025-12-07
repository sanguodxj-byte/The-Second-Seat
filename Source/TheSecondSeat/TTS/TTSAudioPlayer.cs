using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using Verse;

namespace TheSecondSeat.TTS
{
    /// <summary>
    /// TTS 音频播放器（Unity AudioSource）
    /// 使用 MonoBehaviour 在 Unity 内部播放音频
    /// </summary>
    public class TTSAudioPlayer : MonoBehaviour
    {
        private static TTSAudioPlayer? instance;
        
        /// <summary>
        /// 单例访问
        /// </summary>
        public static TTSAudioPlayer Instance
        {
            get
            {
                if (instance == null)
                {
                    // 在主线程创建 GameObject
                    var go = new GameObject("TTSAudioPlayer");
                    instance = go.AddComponent<TTSAudioPlayer>();
                    DontDestroyOnLoad(go); // 持久化
                    Log.Message("[TTSAudioPlayer] Instance created");
                }
                return instance;
            }
        }

        private AudioSource? currentAudioSource;

        /// <summary>
        /// 从字节数组播放音频
        /// 必须在主线程调用
        /// </summary>
        public void PlayFromBytes(byte[] audioData)
        {
            if (audioData == null || audioData.Length == 0)
            {
                Log.Warning("[TTSAudioPlayer] Audio data is empty");
                return;
            }

            try
            {
                // 1. 保存为临时文件
                string tempFilePath = SaveToTempFile(audioData);
                
                if (string.IsNullOrEmpty(tempFilePath))
                {
                    Log.Error("[TTSAudioPlayer] Failed to save temp file");
                    return;
                }

                // 2. 使用协程加载和播放
                StartCoroutine(LoadAndPlayCoroutine(tempFilePath));
            }
            catch (Exception ex)
            {
                Log.Error($"[TTSAudioPlayer] Error: {ex.Message}");
            }
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
        /// 协程：使用 UnityWebRequest 加载音频并播放
        /// </summary>
        private IEnumerator LoadAndPlayCoroutine(string filePath)
        {
            // 1. 使用 UnityWebRequestMultimedia 加载 WAV 文件
            string fileUri = "file://" + filePath;
            
            using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip(fileUri, AudioType.WAV))
            {
                Log.Message($"[TTSAudioPlayer] Loading audio: {fileUri}");
                
                // 发送请求
                yield return www.SendWebRequest();

                // 检查错误
                if (www.result != UnityWebRequest.Result.Success)
                {
                    Log.Error($"[TTSAudioPlayer] Failed to load audio: {www.error}");
                    yield break;
                }

                // 2. 获取 AudioClip
                AudioClip clip = DownloadHandlerAudioClip.GetContent(www);
                
                if (clip == null)
                {
                    Log.Error("[TTSAudioPlayer] AudioClip is null");
                    yield break;
                }

                Log.Message($"[TTSAudioPlayer] AudioClip loaded: {clip.length} seconds");

                // 3. 创建临时 GameObject 和 AudioSource
                GameObject tempGO = new GameObject("TempAudioPlayer");
                currentAudioSource = tempGO.AddComponent<AudioSource>();
                
                // 配置 AudioSource
                currentAudioSource.clip = clip;
                currentAudioSource.volume = 1.0f;
                currentAudioSource.playOnAwake = false;
                
                // 播放
                currentAudioSource.Play();
                Log.Message("[TTSAudioPlayer] Playing audio...");

                // 4. 等待播放完成
                yield return new WaitForSeconds(clip.length);

                // 5. 清理
                Destroy(tempGO);
                currentAudioSource = null;
                
                Log.Message("[TTSAudioPlayer] Playback finished");

                // 6. 删除临时文件
                try
                {
                    if (File.Exists(filePath))
                    {
                        File.Delete(filePath);
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning($"[TTSAudioPlayer] Failed to delete temp file: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 停止当前播放
        /// </summary>
        public void Stop()
        {
            if (currentAudioSource != null && currentAudioSource.isPlaying)
            {
                currentAudioSource.Stop();
                Log.Message("[TTSAudioPlayer] Playback stopped");
            }
        }

        /// <summary>
        /// 组件销毁时清理
        /// </summary>
        private void OnDestroy()
        {
            Stop();
            instance = null;
        }
    }
}
