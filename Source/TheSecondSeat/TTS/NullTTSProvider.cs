using System;
using System.Threading.Tasks;
using Verse;
using TheSecondSeat.Core;

namespace TheSecondSeat.TTS
{
    /// <summary>
    /// 🛡️ 空 TTS 提供者（Null Object Pattern）
    /// 当子 Mod 没有配置 TTS 参数或 TTS 服务不可用时使用
    /// </summary>
    public class NullTTSProvider : ITTSProvider
    {
        private static NullTTSProvider _instance;
        public static NullTTSProvider Instance => _instance ??= new NullTTSProvider();
        private NullTTSProvider() { }
        
        public string ProviderName => "NullProvider";
        public bool IsConfigured => false;
        public bool IsSpeaking => false;
        
        public Task<string> SpeakAsync(string text, string personaDefName = "")
        {
            if (Prefs.DevMode)
                Log.Message($"[NullTTSProvider] SpeakAsync called but TTS is not configured. Text length: {text?.Length ?? 0}");
            return Task.FromResult<string>(null);
        }
        
        public void Stop() { }
        
        public void Configure(TTSConfiguration config)
        {
            if (Prefs.DevMode)
                Log.Message("[NullTTSProvider] Configure called but this is a null provider");
        }
        
        public void ClearCache() { }
        
        public System.Collections.Generic.List<string> GetAvailableVoices() => new();
    }
    
    /// <summary>
    /// 🛡️ TTS 提供者接口
    /// 定义 TTS 服务的基本契约
    /// </summary>
    public interface ITTSProvider
    {
        /// <summary>
        /// 提供者名称
        /// </summary>
        string ProviderName { get; }
        
        /// <summary>
        /// 是否已配置
        /// </summary>
        bool IsConfigured { get; }
        
        /// <summary>
        /// 是否正在说话
        /// </summary>
        bool IsSpeaking { get; }
        
        /// <summary>
        /// 朗读文本
        /// </summary>
        /// <param name="text">要朗读的文本</param>
        /// <param name="personaDefName">人格 DefName（可选，用于口型同步）</param>
        /// <returns>生成的音频文件路径，失败返回 null</returns>
        Task<string> SpeakAsync(string text, string personaDefName = "");
        
        /// <summary>
        /// 停止朗读
        /// </summary>
        void Stop();
        
        /// <summary>
        /// 配置提供者
        /// </summary>
        void Configure(TTSConfiguration config);
        
        /// <summary>
        /// 清理缓存
        /// </summary>
        void ClearCache();
        
        /// <summary>
        /// 获取可用语音列表
        /// </summary>
        System.Collections.Generic.List<string> GetAvailableVoices();
    }
    
    /// <summary>🛡️ TTS 配置类（🏗️ 使用框架配置默认值）</summary>
    public class TTSConfiguration
    {
        // 🏗️ 使用 TSSFrameworkConfig 的默认值
        public string Provider { get; set; } = TSSFrameworkConfig.TTS.DefaultProvider;
        public string ApiKey { get; set; } = "";
        public string Region { get; set; } = TSSFrameworkConfig.TTS.DefaultAzureRegion;
        public string VoiceName { get; set; } = TSSFrameworkConfig.TTS.DefaultVoiceName;
        public float SpeechRate { get; set; } = TSSFrameworkConfig.TTS.DefaultSpeechRate;
        public float Volume { get; set; } = TSSFrameworkConfig.TTS.DefaultVolume;
        public float Pitch { get; set; } = TSSFrameworkConfig.TTS.DefaultPitch;
        public string ApiUrl { get; set; } = "";
        public string ModelName { get; set; } = "";
        
        public static TTSConfiguration FromPersona(PersonaGeneration.NarratorPersonaDef persona)
            => persona == null ? new TTSConfiguration() : new TTSConfiguration
            {
                VoiceName = !string.IsNullOrEmpty(persona.ttsVoiceName) ? persona.ttsVoiceName : persona.defaultVoice,
                SpeechRate = persona.ttsVoiceSpeed > 0 ? persona.ttsVoiceSpeed : persona.ttsVoiceRate,
                Pitch = persona.ttsVoicePitch
            };
    }
    
    /// <summary>🛡️ TTS 提供者工厂</summary>
    public static class TTSProviderFactory
    {
        public static ITTSProvider CreateProvider(TTSConfiguration config)
        {
            if (config == null) return NullTTSProvider.Instance;
            
            // 无有效配置时返回空提供者
            bool hasConfig = !string.IsNullOrEmpty(config.VoiceName) ||
                            !string.IsNullOrEmpty(config.ApiKey) ||
                            !string.IsNullOrEmpty(config.ApiUrl);
            
            return hasConfig ? new TTSServiceAdapter(config) : NullTTSProvider.Instance;
        }
        
        public static ITTSProvider CreateProviderForPersona(PersonaGeneration.NarratorPersonaDef persona)
            => persona == null ? NullTTSProvider.Instance : CreateProvider(TTSConfiguration.FromPersona(persona));
    }
    
    /// <summary>🛡️ TTS 服务适配器</summary>
    public class TTSServiceAdapter : ITTSProvider
    {
        private readonly TTSConfiguration config;
        private bool isConfigured;
        
        public TTSServiceAdapter(TTSConfiguration config)
        {
            this.config = config ?? new TTSConfiguration();
            ApplyConfiguration();
        }
        
        public string ProviderName => config?.Provider ?? "unknown";
        public bool IsConfigured => isConfigured;
        public bool IsSpeaking => TTSService.Instance.IsSpeaking;
        
        public async Task<string> SpeakAsync(string text, string personaDefName = "")
        {
            if (!isConfigured)
            {
                if (Prefs.DevMode) Log.Warning("[TTSServiceAdapter] TTS not configured, skipping speech");
                return null;
            }
            return await TTSService.Instance.SpeakAsync(text, personaDefName);
        }
        
        public void Stop() => TTSAudioPlayer.Instance?.Stop();
        
        public void Configure(TTSConfiguration newConfig)
        {
            if (newConfig == null) return;
            
            config.Provider = newConfig.Provider;
            config.ApiKey = newConfig.ApiKey;
            config.Region = newConfig.Region;
            config.VoiceName = newConfig.VoiceName;
            config.SpeechRate = newConfig.SpeechRate;
            config.Volume = newConfig.Volume;
            config.Pitch = newConfig.Pitch;
            config.ApiUrl = newConfig.ApiUrl;
            config.ModelName = newConfig.ModelName;
            ApplyConfiguration();
        }
        
        public void ClearCache() => TTSService.Instance.ClearCache();
        
        public System.Collections.Generic.List<string> GetAvailableVoices() => TTSService.GetAvailableVoices();
        
        private void ApplyConfiguration()
        {
            try
            {
                TTSService.Instance.Configure(
                    provider: config.Provider, key: config.ApiKey, region: config.Region,
                    voice: config.VoiceName, rate: config.SpeechRate, vol: config.Volume,
                    apiUrl: config.ApiUrl, modelName: config.ModelName
                );
                isConfigured = !string.IsNullOrEmpty(config.VoiceName);
                
                if (Prefs.DevMode)
                    Log.Message($"[TTSServiceAdapter] Configured: provider={config.Provider}, voice={config.VoiceName}");
            }
            catch (Exception ex)
            {
                isConfigured = false;
                if (Prefs.DevMode) Log.Error($"[TTSServiceAdapter] Configuration failed: {ex.Message}");
            }
        }
    }
}