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

    // ===== 📌 v1.9.7: Provider 抽象基类 - 消除代码重复 =====

    /// <summary>
    /// LLM Provider 抽象基类，封装通用逻辑
    /// </summary>
    public abstract class BaseLLMProvider : ILLMProvider
    {
        /// <summary>
        /// 提供程序显示名称
        /// </summary>
        public abstract string ProviderName { get; }
        
        /// <summary>
        /// 配置中使用的提供程序标识符（小写）
        /// </summary>
        protected abstract string ProviderIdentifier { get; }
        
        /// <summary>
        /// 是否需要 API Key（本地模型不需要）
        /// </summary>
        protected virtual bool RequiresApiKey => true;

        public bool IsAvailable
        {
            get
            {
                var settings = LoadedModManager.GetMod<Settings.TheSecondSeatMod>()?.GetSettings<Settings.TheSecondSeatSettings>();
                if (settings?.llmProvider != ProviderIdentifier)
                    return false;
                    
                if (RequiresApiKey && string.IsNullOrEmpty(settings?.apiKey))
                    return false;
                    
                return true;
            }
        }

        public async Task<string> SendMessageAsync(string systemPrompt, string gameState, string userMessage, float temperature = 0.7f, int maxTokens = 500)
        {
            try
            {
                // ⭐ v1.9.6: 使用 LLMService.SendMessageAsync 而不是 SendStateAndGetActionAsync
                // 这样可以正确处理 ReAct 格式响应（rawContent 优先于 dialogue）
                return await LLM.LLMService.Instance.SendMessageAsync(systemPrompt, gameState ?? "", userMessage, temperature, maxTokens);
            }
            catch (Exception ex)
            {
                Log.Error($"[{ProviderName}Provider] Error: {ex.Message}");
                throw;
            }
        }

        public async Task<bool> TestConnectionAsync()
        {
            return await LLM.LLMService.Instance.TestConnectionAsync();
        }
    }

    /// <summary>
    /// OpenAI 提供程序实现
    /// </summary>
    public class OpenAIProvider : BaseLLMProvider
    {
        public override string ProviderName => "OpenAI";
        protected override string ProviderIdentifier => "openai";
    }

    /// <summary>
    /// DeepSeek 提供程序实现
    /// </summary>
    public class DeepSeekProvider : BaseLLMProvider
    {
        public override string ProviderName => "DeepSeek";
        protected override string ProviderIdentifier => "deepseek";
    }

    /// <summary>
    /// Gemini 提供程序实现
    /// </summary>
    public class GeminiProvider : BaseLLMProvider
    {
        public override string ProviderName => "Gemini";
        protected override string ProviderIdentifier => "gemini";
    }

    /// <summary>
    /// 本地模型提供程序实现
    /// </summary>
    public class LocalProvider : BaseLLMProvider
    {
        public override string ProviderName => "Local";
        protected override string ProviderIdentifier => "local";
        protected override bool RequiresApiKey => false;
    }
}
