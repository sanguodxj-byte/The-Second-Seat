using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;
using Verse;
using TheSecondSeat.Core;

namespace TheSecondSeat.Utils
{
    /// <summary>
    /// 静默资源加载器 - 使用 reportFailure: false 防止红字日志
    /// 使用 TSSFrameworkConfig 支持附属 Mod 扩展
    /// v1.6.80: 所有资源加载方法必须在主线程调用
    /// </summary>
    [StaticConstructorOnStartup]
    public static class TSS_AssetLoader
    {
        // v1.6.80: 主线程ID，用于检测跨线程调用
        private static int? mainThreadId;

        static TSS_AssetLoader()
        {
            // 静态构造函数由 RimWorld 在主线程调用
            InitializeMainThread();
        }
        
        /// <summary>初始化主线程ID（必须在游戏启动时调用）</summary>
        public static void InitializeMainThread()
        {
            if (mainThreadId == null)
            {
                mainThreadId = Thread.CurrentThread.ManagedThreadId;
                // Log.Message($"[TSS_AssetLoader] Main thread ID initialized: {mainThreadId}");
            }
        }
        
        /// <summary>检查是否在主线程</summary>
        public static bool IsMainThread => mainThreadId == null || Thread.CurrentThread.ManagedThreadId == mainThreadId;
        
        /// <summary>确保在主线程调用，否则记录警告并返回null</summary>
        private static bool EnsureMainThread(string methodName)
        {
            if (!IsMainThread)
            {
                if (Prefs.DevMode)
                {
                    Log.Warning($"[TSS_AssetLoader] {methodName} called from non-main thread. " +
                               $"Thread: {Thread.CurrentThread.ManagedThreadId}, Main: {mainThreadId}. " +
                               "Resource loading must happen on main thread!");
                }
                return false;
            }
            return true;
        }
        
        private class CacheEntry<T> where T : class
        {
            public T Value;
            public int LastAccessTick;
            public bool IsNull;
        }
        
        private static readonly Dictionary<string, CacheEntry<Texture2D>> textureCache = new();
        private static readonly Dictionary<string, CacheEntry<AudioClip>> audioCache = new();
        private static readonly HashSet<string> missingPaths = new();
        
        // 使用配置类替代硬编码常量
        private static int MaxCacheSize => TSSFrameworkConfig.Cache.MaxCacheSize;
        private static int CacheExpireTicks => TSSFrameworkConfig.Cache.CacheExpireTicks;
        
        public static string DefaultPlaceholderPath => TSSFrameworkConfig.AssetPaths.DefaultPlaceholderPath;
        
        private static int CurrentTick => Current.Game?.tickManager?.TicksGame ?? 0;
        
        /// <summary>静默加载纹理（必须在主线程调用）</summary>
        public static Texture2D LoadTexture(string path, Texture2D fallback = null)
        {
            if (string.IsNullOrEmpty(path) || missingPaths.Contains(path))
                return fallback;
            
            // v1.6.80: 检查缓存可以在任何线程
            if (textureCache.TryGetValue(path, out var cached))
            {
                cached.LastAccessTick = CurrentTick;
                return cached.IsNull ? fallback : (cached.Value ?? fallback);
            }
            
            // v1.6.80: ContentFinder必须在主线程调用
            if (!EnsureMainThread("LoadTexture"))
            {
                return fallback;
            }
            
            var tex = ContentFinder<Texture2D>.Get(path, reportFailure: false);
            CacheAsset(textureCache, path, tex);
            return tex ?? fallback;
        }
        
        /// <summary>多路径回退加载</summary>
        public static Texture2D LoadTextureWithFallback(IEnumerable<string> paths, Texture2D fallback = null)
        {
            foreach (var path in paths)
            {
                var tex = LoadTexture(path, null);
                if (tex != null) return tex;
            }
            return fallback;
        }
        
        /// <summary>加载人格立绘（智能路径搜索）</summary>
        public static Texture2D LoadPortrait(string personaName, Texture2D fallback = null)
        {
            if (string.IsNullOrEmpty(personaName))
                return fallback ?? GetDefaultPlaceholder();
            
            // 使用配置类的路径模板
            var paths = TSSFrameworkConfig.AssetPaths.PortraitSearchPaths
                .Select(template => string.Format(template, personaName));
            
            return LoadTextureWithFallback(paths, fallback ?? GetDefaultPlaceholder());
        }
        
        /// <summary>加载降临特效</summary>
        public static Texture2D LoadDescentEffect(string personaName, string effectName, Texture2D fallback = null)
            => LoadDescentAsset("Effects", personaName, effectName, fallback);
        
        /// <summary>加载降临姿态</summary>
        public static Texture2D LoadDescentPosture(string personaName, string postureName, Texture2D fallback = null)
            => LoadDescentAsset("Postures", personaName, postureName, fallback);
        
        private static Texture2D LoadDescentAsset(string category, string personaName, string assetName, Texture2D fallback)
        {
            if (string.IsNullOrEmpty(assetName)) return fallback;
            
            // 使用配置类的降临资源路径模板
            var paths = TSSFrameworkConfig.AssetPaths.DescentAssetPaths
                .Select(template => string.Format(template, category, personaName ?? "", assetName))
                .Where(p => !string.IsNullOrEmpty(personaName) || !p.Contains($"/{personaName}/"));
            
            return LoadTextureWithFallback(paths, fallback);
        }
        
        /// <summary>静默加载音频</summary>
        public static AudioClip LoadAudio(string path, AudioClip fallback = null)
        {
            if (string.IsNullOrEmpty(path) || missingPaths.Contains(path))
                return fallback;
            
            if (audioCache.TryGetValue(path, out var cached))
            {
                cached.LastAccessTick = CurrentTick;
                return cached.IsNull ? fallback : (cached.Value ?? fallback);
            }
            
            var clip = ContentFinder<AudioClip>.Get(path, reportFailure: false);
            CacheAsset(audioCache, path, clip);
            return clip ?? fallback;
        }
        
        /// <summary>检查纹理存在性（必须在主线程调用）</summary>
        public static bool TextureExists(string path)
        {
            if (string.IsNullOrEmpty(path) || missingPaths.Contains(path))
                return false;
            if (textureCache.TryGetValue(path, out var cached))
                return !cached.IsNull && cached.Value != null;
            
            // v1.6.80: 如果不在主线程，只能依赖缓存
            if (!IsMainThread)
            {
                return false; // 无法确定，返回false避免错误
            }
            
            return LoadTexture(path, null) != null;
        }
        
        /// <summary>检查人格是否有立绘</summary>
        public static bool HasPortrait(string personaName)
        {
            if (string.IsNullOrEmpty(personaName)) return false;
            
            // 使用配置类的路径模板检查
            return TSSFrameworkConfig.AssetPaths.PortraitSearchPaths
                .Select(template => string.Format(template, personaName))
                .Any(TextureExists);
        }
        
        /// <summary>检查降临资源</summary>
        public static bool HasDescentResources(string personaName)
        {
            if (string.IsNullOrEmpty(personaName)) return false;
            
            // 使用配置类的降临检查路径
            return TSSFrameworkConfig.AssetPaths.DescentPostureCheckPaths
                .Select(template => string.Format(template, personaName))
                .Any(TextureExists);
        }
        
        private static Texture2D defaultPlaceholder;
        
        public static Texture2D GetDefaultPlaceholder()
        {
            if (defaultPlaceholder != null)
                return defaultPlaceholder;
            
            // v1.6.80: ContentFinder必须在主线程
            if (!IsMainThread)
            {
                return GeneratePlaceholder(); // 返回动态生成的占位图
            }
            
            defaultPlaceholder = ContentFinder<Texture2D>.Get(DefaultPlaceholderPath, reportFailure: false) ?? GeneratePlaceholder();
            return defaultPlaceholder;
        }
        
        private static Texture2D GeneratePlaceholder()
        {
            var tex = new Texture2D(256, 256, TextureFormat.RGBA32, false);
            // 使用配置类的颜色
            var bg = TSSFrameworkConfig.Colors.PlaceholderBackground;
            var border = TSSFrameworkConfig.Colors.PlaceholderBorder;
            
            for (int y = 0; y < 256; y++)
                for (int x = 0; x < 256; x++)
                    tex.SetPixel(x, y, (x < 4 || x >= 252 || y < 4 || y >= 252) ? border : bg);
            
            tex.Apply();
            tex.filterMode = FilterMode.Bilinear;
            return tex;
        }
        
        private static void CacheAsset<T>(Dictionary<string, CacheEntry<T>> cache, string path, T asset) where T : class
        {
            if (cache.Count >= MaxCacheSize) CleanCache(cache);
            cache[path] = new CacheEntry<T> { Value = asset, IsNull = asset == null, LastAccessTick = CurrentTick };
            if (asset == null) missingPaths.Add(path);
        }
        
        private static void CleanCache<T>(Dictionary<string, CacheEntry<T>> cache) where T : class
        {
            int tick = CurrentTick;
            var expired = new List<string>();
            foreach (var kv in cache)
                if (tick - kv.Value.LastAccessTick > CacheExpireTicks)
                    expired.Add(kv.Key);
            foreach (var key in expired)
                cache.Remove(key);
        }
        
        public static void ClearAllCache()
        {
            textureCache.Clear();
            audioCache.Clear();
            missingPaths.Clear();
            
            if (defaultPlaceholder != null)
            {
                UnityEngine.Object.Destroy(defaultPlaceholder);
                defaultPlaceholder = null;
            }
        }
        
        public static void InvalidatePath(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            textureCache.Remove(path);
            audioCache.Remove(path);
            missingPaths.Remove(path);
        }
    }
}
