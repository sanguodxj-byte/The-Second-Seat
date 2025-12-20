using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Verse;

namespace TheSecondSeat.WebSearch
{
    /// <summary>
    /// 网络搜索服务 - 支持多种搜索引擎
    /// ? 添加延时控制，防止请求过快
    /// </summary>
    public class WebSearchService
    {
        private static WebSearchService? instance;
        public static WebSearchService Instance => instance ??= new WebSearchService();

        private readonly HttpClient httpClient;
        private readonly Dictionary<string, SearchResultCache> searchCache;
        private const int CacheExpirationMinutes = 60;
        private const int MaxCacheEntries = 100;
        
        // ? 请求限流
        private DateTime lastRequestTime = DateTime.MinValue;
        private const int MinRequestIntervalMs = 1000; // 最小请求间隔（毫秒）
        private int requestDelayMs = 1000; // 默认延时 1 秒

        // 配置
        private string searchEngine = "bing"; // "bing", "google", "duckduckgo"
        private string? bingApiKey;
        private string? googleApiKey;
        private string? googleSearchEngineId;

        public WebSearchService()
        {
            httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(30) // ? 15秒 → 30秒
            };
            httpClient.DefaultRequestHeaders.Add("User-Agent", "TheSecondSeat-RimWorld-Mod/1.0");
            searchCache = new Dictionary<string, SearchResultCache>();
        }

        /// <summary>
        /// 配置搜索服务
        /// </summary>
        public void Configure(string engine, string? apiKey = null, string? searchEngineId = null, int delayMs = 1000)
        {
            searchEngine = engine.ToLower();
            requestDelayMs = Math.Max(MinRequestIntervalMs, delayMs); // 确保至少 1 秒
            
            switch (searchEngine)
            {
                case "bing":
                    bingApiKey = apiKey;
                    break;
                case "google":
                    googleApiKey = apiKey;
                    googleSearchEngineId = searchEngineId;
                    break;
                case "duckduckgo":
                    // DuckDuckGo 不需要 API Key
                    break;
                default:
                    Log.Warning($"[WebSearch] 未知搜索引擎: {engine}，使用 DuckDuckGo");
                    searchEngine = "duckduckgo";
                    break;
            }

            Log.Message($"[WebSearch] 搜索引擎已配置: {searchEngine}, 延时: {requestDelayMs}ms");
        }

        /// <summary>
        /// ? 请求限流：确保请求间隔不小于设定值
        /// </summary>
        private async Task EnsureRequestDelay()
        {
            var timeSinceLastRequest = DateTime.Now - lastRequestTime;
            var requiredDelay = TimeSpan.FromMilliseconds(requestDelayMs);
            
            if (timeSinceLastRequest < requiredDelay)
            {
                var waitTime = requiredDelay - timeSinceLastRequest;
                Log.Message($"[WebSearch] 限流等待 {waitTime.TotalMilliseconds:F0}ms");
                await Task.Delay(waitTime);
            }
            
            lastRequestTime = DateTime.Now;
        }

        /// <summary>
        /// 执行搜索
        /// ? 添加延时保护
        /// </summary>
        public async Task<SearchResult?> SearchAsync(string query, int maxResults = 5)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return null;
            }

            // 检查缓存
            if (TryGetFromCache(query, out var cachedResult))
            {
                Log.Message($"[WebSearch] 使用缓存结果: {query}");
                return cachedResult;
            }

            try
            {
                // ? 请求前延时
                await EnsureRequestDelay();
                
                SearchResult? result = searchEngine switch
                {
                    "bing" => await SearchBingAsync(query, maxResults),
                    "google" => await SearchGoogleAsync(query, maxResults),
                    "duckduckgo" => await SearchDuckDuckGoAsync(query, maxResults),
                    _ => await SearchDuckDuckGoAsync(query, maxResults)
                };

                // 缓存结果
                if (result != null && result.Results.Count > 0)
                {
                    AddToCache(query, result);
                }

                return result;
            }
            catch (Exception ex)
            {
                Log.Error($"[WebSearch] 搜索失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Bing 搜索
        /// </summary>
        private async Task<SearchResult?> SearchBingAsync(string query, int maxResults)
        {
            if (string.IsNullOrEmpty(bingApiKey))
            {
                Log.Warning("[WebSearch] Bing API Key 未配置");
                return null;
            }

            var url = $"https://api.bing.microsoft.com/v7.0/search?q={Uri.EscapeDataString(query)}&count={maxResults}";
            
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("Ocp-Apim-Subscription-Key", bingApiKey);

            var response = await httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var json = JObject.Parse(content);

            var result = new SearchResult
            {
                Query = query,
                Engine = "Bing",
                Timestamp = DateTime.Now
            };

            var webPages = json["webPages"]?["value"] as JArray;
            if (webPages != null)
            {
                foreach (var page in webPages.Take(maxResults))
                {
                    result.Results.Add(new SearchResultItem
                    {
                        Title = page["name"]?.ToString() ?? "",
                        Url = page["url"]?.ToString() ?? "",
                        Snippet = page["snippet"]?.ToString() ?? ""
                    });
                }
            }

            Log.Message($"[WebSearch] Bing 搜索完成: {query} - {result.Results.Count} 个结果");
            return result;
        }

        /// <summary>
        /// Google 搜索
        /// </summary>
        private async Task<SearchResult?> SearchGoogleAsync(string query, int maxResults)
        {
            if (string.IsNullOrEmpty(googleApiKey) || string.IsNullOrEmpty(googleSearchEngineId))
            {
                Log.Warning("[WebSearch] Google API Key 或 Search Engine ID 未配置");
                return null;
            }

            var url = $"https://www.googleapis.com/customsearch/v1?key={googleApiKey}&cx={googleSearchEngineId}&q={Uri.EscapeDataString(query)}&num={maxResults}";

            var response = await httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var json = JObject.Parse(content);

            var result = new SearchResult
            {
                Query = query,
                Engine = "Google",
                Timestamp = DateTime.Now
            };

            var items = json["items"] as JArray;
            if (items != null)
            {
                foreach (var item in items.Take(maxResults))
                {
                    result.Results.Add(new SearchResultItem
                    {
                        Title = item["title"]?.ToString() ?? "",
                        Url = item["link"]?.ToString() ?? "",
                        Snippet = item["snippet"]?.ToString() ?? ""
                    });
                }
            }

            Log.Message($"[WebSearch] Google 搜索完成: {query} - {result.Results.Count} 个结果");
            return result;
        }

        /// <summary>
        /// DuckDuckGo 即时回答 API（免费，无需 API Key）
        /// </summary>
        private async Task<SearchResult?> SearchDuckDuckGoAsync(string query, int maxResults)
        {
            var url = $"https://api.duckduckgo.com/?q={Uri.EscapeDataString(query)}&format=json&no_html=1&skip_disambig=1";

            var response = await httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var json = JObject.Parse(content);

            var result = new SearchResult
            {
                Query = query,
                Engine = "DuckDuckGo",
                Timestamp = DateTime.Now
            };

            // 即时回答
            var abstractText = json["Abstract"]?.ToString();
            var abstractUrl = json["AbstractURL"]?.ToString();
            
            if (!string.IsNullOrEmpty(abstractText))
            {
                result.Results.Add(new SearchResultItem
                {
                    Title = json["Heading"]?.ToString() ?? query,
                    Url = abstractUrl ?? "",
                    Snippet = abstractText
                });
            }

            // 相关话题
            var relatedTopics = json["RelatedTopics"] as JArray;
            if (relatedTopics != null)
            {
                foreach (var topic in relatedTopics.Take(maxResults - result.Results.Count))
                {
                    if (topic["Text"] != null)
                    {
                        result.Results.Add(new SearchResultItem
                        {
                            Title = topic["Text"]?.ToString()?.Split('.')[0] ?? "",
                            Url = topic["FirstURL"]?.ToString() ?? "",
                            Snippet = topic["Text"]?.ToString() ?? ""
                        });
                    }
                }
            }

            Log.Message($"[WebSearch] DuckDuckGo 搜索完成: {query} - {result.Results.Count} 个结果");
            return result;
        }

        /// <summary>
        /// 检测查询是否需要联网搜索
        /// </summary>
        public static bool ShouldSearch(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return false;

            var lowerQuery = query.ToLower();

            // 检测关键词
            var searchTriggers = new[]
            {
                "搜索", "查找", "什么是", "谁是", "如何", "怎么",
                "最新", "新闻", "当前", "现在",
                "search", "find", "what is", "who is", "how to",
                "latest", "news", "current", "recent"
            };

            return searchTriggers.Any(trigger => lowerQuery.Contains(trigger));
        }

        /// <summary>
        /// 将搜索结果格式化为上下文
        /// </summary>
        public static string FormatResultsForContext(SearchResult searchResult, int maxLength = 800)
        {
            if (searchResult == null || searchResult.Results.Count == 0)
            {
                return "";
            }

            var context = $"=== 网络搜索结果: \"{searchResult.Query}\" ({searchResult.Engine}) ===\n";
            int currentLength = context.Length;

            foreach (var result in searchResult.Results)
            {
                var entry = $"\n【{result.Title}】\n{result.Snippet}\n来源: {result.Url}\n";
                
                if (currentLength + entry.Length > maxLength)
                {
                    break;
                }

                context += entry;
                currentLength += entry.Length;
            }

            context += "=== 搜索结果结束 ===\n\n";
            return context;
        }

        // 缓存管理
        private bool TryGetFromCache(string query, out SearchResult? result)
        {
            if (searchCache.TryGetValue(query, out var cache))
            {
                if (DateTime.Now - cache.Timestamp < TimeSpan.FromMinutes(CacheExpirationMinutes))
                {
                    result = cache.Result;
                    return true;
                }
                else
                {
                    searchCache.Remove(query);
                }
            }

            result = null;
            return false;
        }

        private void AddToCache(string query, SearchResult result)
        {
            if (searchCache.Count >= MaxCacheEntries)
            {
                // 移除最旧的条目
                var oldest = searchCache.OrderBy(x => x.Value.Timestamp).First();
                searchCache.Remove(oldest.Key);
            }

            searchCache[query] = new SearchResultCache
            {
                Result = result,
                Timestamp = DateTime.Now
            };
        }

        public void ClearCache()
        {
            searchCache.Clear();
            Log.Message("[WebSearch] 搜索缓存已清空");
        }
    }

    /// <summary>
    /// 搜索结果
    /// </summary>
    public class SearchResult
    {
        public string Query { get; set; } = "";
        public string Engine { get; set; } = "";
        public DateTime Timestamp { get; set; }
        public List<SearchResultItem> Results { get; set; } = new List<SearchResultItem>();
    }

    /// <summary>
    /// 单个搜索结果项
    /// </summary>
    public class SearchResultItem
    {
        public string Title { get; set; } = "";
        public string Url { get; set; } = "";
        public string Snippet { get; set; } = "";
    }

    /// <summary>
    /// 搜索结果缓存
    /// </summary>
    internal class SearchResultCache
    {
        public SearchResult Result { get; set; } = new SearchResult();
        public DateTime Timestamp { get; set; }
    }
}
