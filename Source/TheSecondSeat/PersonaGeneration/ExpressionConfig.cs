using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Verse;

namespace TheSecondSeat.PersonaGeneration
{
    /// <summary>
    /// 表情配置数据结构
    /// 对应 Defs/ExpressionConfig.json
    /// </summary>
    public class ExpressionConfig
    {
        public Dictionary<string, ExpressionDef> Expressions { get; set; } = new Dictionary<string, ExpressionDef>();

        private static ExpressionConfig _instance;
        public static ExpressionConfig Instance
        {
            get
            {
                if (_instance == null)
                {
                    Load();
                }
                return _instance;
            }
        }

        /// <summary>
        /// 获取指定表情类型的变体数量
        /// </summary>
        public int GetVariantCount(ExpressionType type)
        {
            return GetVariantCount(type.ToString());
        }
        
        /// <summary>
        /// ⭐ v2.6.0: 获取指定表情名称的变体数量（支持动态表情）
        /// </summary>
        public int GetVariantCount(string expressionName)
        {
            if (string.IsNullOrEmpty(expressionName)) return 0;
            
            if (Expressions.TryGetValue(expressionName, out var def))
            {
                return def.Variants?.Count ?? 0;
            }
            return 0;
        }
        
        /// <summary>
        /// ⭐ v2.6.0: 获取所有已配置的表情名称列表
        /// </summary>
        public List<string> GetAllExpressionNames()
        {
            return Expressions.Keys.ToList();
        }
        
        /// <summary>
        /// ⭐ v2.6.0: 检查表情是否存在
        /// </summary>
        public bool HasExpression(string expressionName)
        {
            return !string.IsNullOrEmpty(expressionName) && Expressions.ContainsKey(expressionName);
        }

        public static void Load()
        {
            // 查找 Mod
            var mod = LoadedModManager.RunningModsListForReading.FirstOrDefault(m => 
                m.PackageId.ToLower().Contains("thesecondseat") || 
                m.Name == "The Second Seat");
            
            if (mod == null)
            {
                Log.Error("[ExpressionConfig] Could not find The Second Seat mod to load ExpressionConfig.json");
                return;
            }

            string path = Path.Combine(mod.RootDir, "Defs", "ExpressionConfig.json");
            if (!File.Exists(path))
            {
                // 尝试备用路径 (开发环境)
                if (path.Contains("d:/rim mod")) // 针对当前开发环境的特殊处理
                {
                     // 开发环境路径可能不同，但这通常是发布后的路径
                }
                
                Log.Error($"[ExpressionConfig] Config file not found at {path}");
                return;
            }

            try
            {
                string json = File.ReadAllText(path);
                _instance = JsonConvert.DeserializeObject<ExpressionConfig>(json);
                if (Prefs.DevMode)
                {
                    Log.Message($"[ExpressionConfig] Loaded {_instance.Expressions.Count} expressions from {path}");
                }
            }
            catch (System.Exception ex)
            {
                Log.Error($"[ExpressionConfig] Failed to load config: {ex}");
            }
        }
    }

    public class ExpressionDef
    {
        public string Description { get; set; }
        public string Eyes { get; set; }
        public string Mouth { get; set; }
        public string Flush { get; set; }
        public float BlinkIntervalMin { get; set; }
        public float BlinkIntervalMax { get; set; }
        public float BreathingSpeed { get; set; }
        public float BreathingAmplitude { get; set; }
        public string DefaultMouthShape { get; set; }
        public List<ExpressionVariant> Variants { get; set; }
        
        /// <summary>
        /// 根据变体等级获取嘴型
        /// </summary>
        public string GetMouthForVariant(int level)
        {
            if (Variants == null || Variants.Count == 0 || level <= 0)
            {
                return Mouth;
            }
            
            // 1. 尝试精确匹配
            var variant = Variants.FirstOrDefault(v => v.Level == level);
            if (variant != null && !string.IsNullOrEmpty(variant.Mouth))
            {
                return variant.Mouth;
            }
            
            // 2. 如果没有精确匹配，且请求的等级高于最大等级，则返回最大等级的变体
            // (解决随机变体 1-5 但配置只有 1-3 时回退到默认的问题)
            var maxVariant = Variants.OrderByDescending(v => v.Level).FirstOrDefault();
            if (maxVariant != null && level > maxVariant.Level && !string.IsNullOrEmpty(maxVariant.Mouth))
            {
                return maxVariant.Mouth;
            }

            // 3. 否则返回默认
            return Mouth;
        }
    }

    public class ExpressionVariant
    {
        public int Level { get; set; }
        public string Description { get; set; }
        public string Mouth { get; set; }
    }
}
