using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Verse;

namespace TheSecondSeat.RimAgent
{
    /// <summary>
    /// ? v1.6.65: LLM 提供商工厂（修复版）
    /// </summary>
    public static class LLMProviderFactory
    {
        private static readonly Dictionary<string, ILLMProvider> providers = new Dictionary<string, ILLMProvider>();
        private static readonly object lockObj = new object();

        public static void Initialize()
        {
            lock (lockObj)
            {
                if (providers.Count > 0) return;

                try
                {
                    providers["openai"] = new OpenAIProvider();
                    providers["deepseek"] = new DeepSeekProvider();
                    providers["gemini"] = new GeminiProvider();
                    providers["local"] = new LocalProvider();

                    Log.Message($"[LLMProviderFactory] Initialized {providers.Count} providers");
                }
                catch (Exception ex)
                {
                    Log.Error($"[LLMProviderFactory] Initialization failed: {ex.Message}");
                }
            }
        }

        public static ILLMProvider GetProvider(string providerName)
        {
            Initialize();

            if (providerName == "auto")
            {
                return GetBestAvailableProvider();
            }

            if (providers.TryGetValue(providerName.ToLower(), out var provider))
            {
                return provider;
            }

            Log.Warning($"[LLMProviderFactory] Provider '{providerName}' not found, using fallback");
            return GetBestAvailableProvider();
        }

        private static ILLMProvider GetBestAvailableProvider()
        {
            string[] priority = { "deepseek", "openai", "gemini", "local" };

            foreach (var name in priority)
            {
                if (providers.TryGetValue(name, out var provider) && provider.IsAvailable)
                {
                    return provider;
                }
            }

            var fallback = providers.Values.FirstOrDefault();
            if (fallback != null)
            {
                return fallback;
            }

            throw new Exception("No LLM provider available");
        }

        public static List<ILLMProvider> GetAllAvailableProviders()
        {
            Initialize();
            return providers.Values.Where(p => p.IsAvailable).ToList();
        }
    }

    // ===== 提供商实现（修复版：返回 response.Content） =====

    public class OpenAIProvider : ILLMProvider
    {
        public string ProviderName => "OpenAI";

        public bool IsAvailable
        {
            get
            {
                var settings = LoadedModManager.GetMod<Settings.TheSecondSeatMod>()?.GetSettings<Settings.TheSecondSeatSettings>();
                return settings?.llmProvider == "openai" && !string.IsNullOrEmpty(settings?.apiKey);
            }
        }

        public async Task<string> SendMessageAsync(string systemPrompt, string gameState, string userMessage, float temperature = 0.7f, int maxTokens = 500)
        {
            try
            {
                // ? 修复：传递完整的 gameState
                var response = await LLM.LLMService.Instance.SendStateAndGetActionAsync(systemPrompt, gameState ?? "", userMessage);
                return response.dialogue ?? string.Empty;
            }
            catch (Exception ex)
            {
                Log.Error($"[OpenAIProvider] Error: {ex.Message}");
                throw;
            }
        }

        public async Task<bool> TestConnectionAsync()
        {
            return await LLM.LLMService.Instance.TestConnectionAsync();
        }
    }

    public class DeepSeekProvider : ILLMProvider
    {
        public string ProviderName => "DeepSeek";

        public bool IsAvailable
        {
            get
            {
                var settings = LoadedModManager.GetMod<Settings.TheSecondSeatMod>()?.GetSettings<Settings.TheSecondSeatSettings>();
                return settings?.llmProvider == "deepseek" && !string.IsNullOrEmpty(settings?.apiKey);
            }
        }

        public async Task<string> SendMessageAsync(string systemPrompt, string gameState, string userMessage, float temperature = 0.7f, int maxTokens = 500)
        {
            try
            {
                // ? 修复：传递完整的 gameState
                var response = await LLM.LLMService.Instance.SendStateAndGetActionAsync(systemPrompt, gameState ?? "", userMessage);
                return response.dialogue ?? string.Empty;
            }
            catch (Exception ex)
            {
                Log.Error($"[DeepSeekProvider] Error: {ex.Message}");
                throw;
            }
        }

        public async Task<bool> TestConnectionAsync()
        {
            return await LLM.LLMService.Instance.TestConnectionAsync();
        }
    }

    public class GeminiProvider : ILLMProvider
    {
        public string ProviderName => "Gemini";

        public bool IsAvailable
        {
            get
            {
                var settings = LoadedModManager.GetMod<Settings.TheSecondSeatMod>()?.GetSettings<Settings.TheSecondSeatSettings>();
                return settings?.llmProvider == "gemini" && !string.IsNullOrEmpty(settings?.apiKey);
            }
        }

        public async Task<string> SendMessageAsync(string systemPrompt, string gameState, string userMessage, float temperature = 0.7f, int maxTokens = 500)
        {
            try
            {
                // ? 修复：传递完整的 gameState
                var response = await LLM.LLMService.Instance.SendStateAndGetActionAsync(systemPrompt, gameState ?? "", userMessage);
                return response.dialogue ?? string.Empty;
            }
            catch (Exception ex)
            {
                Log.Error($"[GeminiProvider] Error: {ex.Message}");
                throw;
            }
        }

        public async Task<bool> TestConnectionAsync()
        {
            return await LLM.LLMService.Instance.TestConnectionAsync();
        }
    }

    public class LocalProvider : ILLMProvider
    {
        public string ProviderName => "Local";

        public bool IsAvailable
        {
            get
            {
                var settings = LoadedModManager.GetMod<Settings.TheSecondSeatMod>()?.GetSettings<Settings.TheSecondSeatSettings>();
                return settings?.llmProvider == "local";
            }
        }

        public async Task<string> SendMessageAsync(string systemPrompt, string gameState, string userMessage, float temperature = 0.7f, int maxTokens = 500)
        {
            try
            {
                // ? 修复：传递完整的 gameState
                var response = await LLM.LLMService.Instance.SendStateAndGetActionAsync(systemPrompt, gameState ?? "", userMessage);
                return response.dialogue ?? string.Empty;
            }
            catch (Exception ex)
            {
                Log.Error($"[LocalProvider] Error: {ex.Message}");
                throw;
            }
        }

        public async Task<bool> TestConnectionAsync()
        {
            return await LLM.LLMService.Instance.TestConnectionAsync();
        }
    }
}
