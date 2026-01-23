using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine.Networking;
using Verse;

namespace TheSecondSeat.WebSearch
{
    /// <summary>
    /// 网络搜索服务 - 支持多种搜索引擎
    /// 添加延时控制，防止请求过快
    /// </summary>
    public class WebSearchService
    {
        private static WebSearchService? instance;
        public static WebSearchService Instance => instance ??= new WebSearchService();

        private readonly Dictionary<string, SearchResultCache> searchCache;
        private const int CacheExpirationMinutes = 60;
        private const int MaxCacheEntries = 100;
        
        // 请求限流
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
            }
        }

        /// <summary>
        /// 检查是否应该触发搜索
        /// </summary>
        public static bool ShouldSearch(string userMessage)
        {
            if (string.IsNullOrEmpty(userMessage)) return false;
            
            // 简单的触发词检测
            // TODO: 使用 LLM 判断意图会更准确，但这里为了性能先用关键词
            string[] triggers = { "search", "google", "bing", "find", "who is", "what is", "搜索", "查询", "查找", "是谁", "是什么" };
            return triggers.Any(t => userMessage.ToLower().StartsWith(t));
        }

        /// <summary>
        /// 格式化搜索结果为 Context 字符串
        /// </summary>
        public static string FormatResultsForContext(SearchResult result)
        {
            if (result == null || result.Results.Count == 0) return "";

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("=== Web Search Results ===");
            sb.AppendLine($"Query: {result.Query}");
            sb.AppendLine($"Source: {result.Source}");
            sb.AppendLine();

            for (int i = 0; i < result.Results.Count; i++)
            {
                var item = result.Results[i];
                sb.AppendLine($"[{i+1}] {item.Title}");
                sb.AppendLine($"URL: {item.Url}");
                sb.AppendLine($"Snippet: {item.Snippet}");
                sb.AppendLine();
            }
            sb.AppendLine("==========================");
            return sb.ToString();
        }

        /// <summary>
        /// 请求限流：确保请求间隔不小于设定值
        /// </summary>
        private async Task EnsureRequestDelay()
        {
            var now = DateTime.Now;
            var timeSinceLast = (now - lastRequestTime).TotalMilliseconds;
            
            if (timeSinceLast < requestDelayMs)
            {
                int waitMs = requestDelayMs - (int)timeSinceLast;
                if (waitMs > 0)
                {
                    await Task.Delay(waitMs);
                }
            }
            
            lastRequestTime = DateTime.Now;
        }

        /// <summary>
        /// 执行搜索
        /// 添加延时保护
        /// </summary>
        public async Task<SearchResult?> SearchAsync(string query, int maxResults = 5)
        {
            if (!isConfigured) return null;

            try
            {
                // 请求前延时
                await EnsureRequestDelay();

                SearchResult? result = searchEngine switch
                {
                    "bing" => await SearchBingAsync(query, maxResults),
                    "google" => await SearchGoogleAsync(query, maxResults),
                    "duckduckgo" => await SearchDuckDuckGoAsync(query, maxResults),
                    _ => null
                };

                return result;
            }
            catch (Exception ex)
            {
                Log.Error($"[WebSearch] Search failed: {ex.Message}");
                return null;
            }
        }

        private bool isConfigured => searchEngine switch
        {
            "bing" => !string.IsNullOrEmpty(bingApiKey),
            "google" => !string.IsNullOrEmpty(googleApiKey) && !string.IsNullOrEmpty(googleSearchEngineId),
            "duckduckgo" => true,
            _ => false
        };

        /// <summary>
        /// Bing Search API
        /// </summary>
        private async Task<SearchResult?> SearchBingAsync(string query, int maxResults)
        {
            if (string.IsNullOrEmpty(bingApiKey)) return null;

            // 检查缓存
            if (TryGetFromCache(query, out var cachedResult)) return cachedResult;

            var url = $"https://api.bing.microsoft.com/v7.0/search?q={Uri.EscapeDataString(query)}&count={maxResults}";

            using (var request = UnityWebRequest.Get(url))
            {
                request.SetRequestHeader("Ocp-Apim-Subscription-Key", bingApiKey);
                var op = request.SendWebRequest();

                while (!op.isDone) await Task.Delay(50);

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Log.Warning($"[WebSearch] Bing API error: {request.error}");
                    return null;
                }

                var json = JObject.Parse(request.downloadHandler.text);
                var result = new SearchResult { Query = query, Source = "Bing" };

                var webPages = json["webPages"]?["value"] as JArray;
                if (webPages != null)
                {
                    foreach (var page in webPages)
                    {
                        result.Results.Add(new SearchItem
                        {
                            Title = page["name"]?.ToString() ?? "",
                            Url = page["url"]?.ToString() ?? "",
                            Snippet = page["snippet"]?.ToString() ?? ""
                        });
                    }
                }

                CacheResult(query, result);
                return result;
            }
        }

        /// <summary>
        /// Google Custom Search API
        /// </summary>
        private async Task<SearchResult?> SearchGoogleAsync(string query, int maxResults)
        {
            if (string.IsNullOrEmpty(googleApiKey) || string.IsNullOrEmpty(googleSearchEngineId)) return null;

            // 检查缓存
            if (TryGetFromCache(query, out var cachedResult)) return cachedResult;

            var url = $"https://www.googleapis.com/customsearch/v1?key={googleApiKey}&cx={googleSearchEngineId}&q={Uri.EscapeDataString(query)}&num={maxResults}";

            using (var request = UnityWebRequest.Get(url))
            {
                var op = request.SendWebRequest();

                while (!op.isDone) await Task.Delay(50);

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Log.Warning($"[WebSearch] Google API error: {request.error}");
                    return null;
                }

                var json = JObject.Parse(request.downloadHandler.text);
                var result = new SearchResult { Query = query, Source = "Google" };

                var items = json["items"] as JArray;
                if (items != null)
                {
                    foreach (var item in items)
                    {
                        result.Results.Add(new SearchItem
                        {
                            Title = item["title"]?.ToString() ?? "",
                            Url = item["link"]?.ToString() ?? "",
                            Snippet = item["snippet"]?.ToString() ?? ""
                        });
                    }
                }

                CacheResult(query, result);
                return result;
            }
        }

        /// <summary>
        /// DuckDuckGo Instant Answer API (Limited)
        /// 注意：DDG 的 API 主要返回摘要，不是完整的网页搜索
        /// </summary>
        private async Task<SearchResult?> SearchDuckDuckGoAsync(string query, int maxResults)
        {
            // 检查缓存
            if (TryGetFromCache(query, out var cachedResult)) return cachedResult;

            var url = $"https://api.duckduckgo.com/?q={Uri.EscapeDataString(query)}&format=json&no_html=1&skip_disambig=1";

            using (var request = UnityWebRequest.Get(url))
            {
                var op = request.SendWebRequest();

                while (!op.isDone) await Task.Delay(50);

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Log.Warning($"[WebSearch] DuckDuckGo error: {request.error}");
                    return null;
                }

                // DDG API 返回的是 JSON
                // 但 UnityWebRequest 可能会把 content-type 识别错，这里手动解析
                try 
                {
                    var json = JObject.Parse(request.downloadHandler.text);
                    var result = new SearchResult { Query = query, Source = "DuckDuckGo" };

                    // 即时回答
                    var abstractText = json["Abstract"]?.ToString();
                    var abstractUrl = json["AbstractURL"]?.ToString();

                    if (!string.IsNullOrEmpty(abstractText))
                    {
                        result.Results.Add(new SearchItem
                        {
                            Title = json["Heading"]?.ToString() ?? query,
                            Url = abstractUrl ?? "",
                            Snippet = abstractText
                        });
                    }

                    // 相关话题 (RelatedTopics)
                    var relatedTopics = json["RelatedTopics"] as JArray;
                    if (relatedTopics != null)
                    {
                        int count = 0;
                        foreach (var topic in relatedTopics)
                        {
                            if (count >= maxResults) break;
                            
                            // DDG API 有时返回嵌套的 Topic，这里只取简单的
                            if (topic["Text"] != null)
                            {
                                result.Results.Add(new SearchItem
                                {
                                    Title = topic["Text"]?.ToString()?.Split('.')[0] ?? "",
                                    Url = topic["FirstURL"]?.ToString() ?? "",
                                    Snippet = topic["Text"]?.ToString() ?? ""
                                });
                                count++;
                            }
                        }
                    }

                    CacheResult(query, result);
                    return result;
                }
                catch (Exception ex)
                {
                    Log.Warning($"[WebSearch] DuckDuckGo parse error: {ex.Message}");
                    return null;
                }
            }
        }

        /// <summary>
        /// 清除搜索缓存
        /// </summary>
        public void ClearCache()
        {
            searchCache.Clear();
        }

        // 缓存管理
        private bool TryGetFromCache(string query, out SearchResult? result)
        {
            if (searchCache.TryGetValue(query, out var cacheItem))
            {
                if ((DateTime.Now - cacheItem.Timestamp).TotalMinutes < CacheExpirationMinutes)
                {
                    result = cacheItem.Result;
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

        private void CacheResult(string query, SearchResult result)
        {
            if (searchCache.Count >= MaxCacheEntries)
            {
                // 简单清理：移除最早的一个（这里简单清空，或者可以用 LRU）
                searchCache.Clear();
            }
            searchCache[query] = new SearchResultCache { Result = result, Timestamp = DateTime.Now };
        }

        private class SearchResultCache
        {
            public SearchResult Result { get; set; } = new SearchResult();
            public DateTime Timestamp { get; set; }
        }
    }

    public class SearchResult
    {
        public string Query { get; set; } = "";
        public string Source { get; set; } = "";
        public List<SearchItem> Results { get; set; } = new List<SearchItem>();
    }

    public class SearchItem
    {
        public string Title { get; set; } = "";
        public string Url { get; set; } = "";
        public string Snippet { get; set; } = "";
    }
}
