using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace TheSecondSeat.PersonaGeneration
{
    // =========================================================================
    // ⭐ v2.5.0: OutfitDef - 服装定义系统 (LLM驱动版)
    // =========================================================================
    //
    // 设计理念：
    // - 服装定义仅描述服装本身（纹理、名称、描述）
    // - 服装更换完全由 LLM 通过 ChangeOutfit 命令控制
    // - LLM 根据提示词中的规则自主判断何时切换服装
    // - 无硬编码的时间或条件触发
    //
    // 使用方式：
    // 1. 通过 XML 定义服装（OutfitDef）
    // 2. 提示词模块告知 LLM 可用服装和切换规则
    // 3. LLM 在对话中自主调用 ChangeOutfit 命令
    //
    // =========================================================================

    /// <summary>
    /// ⭐ v2.5.0: 服装附加图层
    /// 用于定义服装的附加层（如配饰、外套）
    /// </summary>
    public class OutfitLayer
    {
        /// <summary>图层名称（标识用）</summary>
        public string name = "";

        /// <summary>纹理文件名（相对于人格的 Layered 文件夹）</summary>
        public string textureName = "";

        /// <summary>图层排序（越大越在上层）</summary>
        public int zOrder = 0;

        /// <summary>是否替换身体纹理（false = 叠加）</summary>
        public bool replaceBody = false;
    }

    /// <summary>
    /// ⭐ v2.5.0: 服装定义 (Def)
    /// 纯粹的服装数据定义，不包含触发条件
    /// 服装切换由 LLM 通过命令控制
    /// </summary>
    public class OutfitDef : Def
    {
        // ─────────────────────────────────────────────
        // 基础信息
        // ─────────────────────────────────────────────

        /// <summary>
        /// 服装标签（LLM 使用此标签调用 ChangeOutfit）
        /// 例如: "Pajamas", "Casual", "Formal", "Default"
        /// </summary>
        public string outfitTag = "";

        /// <summary>
        /// 关联的人格 DefName
        /// 为空表示通用服装（所有人格可用）
        /// </summary>
        public string personaDefName = "";

        /// <summary>
        /// 服装描述（会包含在提示词中，帮助 LLM 理解）
        /// </summary>
        public string outfitDescription = "";

        // ─────────────────────────────────────────────
        // 纹理配置
        // ─────────────────────────────────────────────

        /// <summary>
        /// 主体纹理（替换 base_body）
        /// 路径相对于人格的 Layered 文件夹
        /// 为空表示使用默认 base_body
        /// </summary>
        public string bodyTexture = "";

        /// <summary>
        /// 附加图层列表
        /// </summary>
        public List<OutfitLayer> layers = new List<OutfitLayer>();

        // ─────────────────────────────────────────────
        // 表情系统配置
        // ─────────────────────���───────────────────────

        /// <summary>
        /// 是否保留原有表情系统
        /// </summary>
        public bool preserveExpressions = true;

        /// <summary>
        /// 表情纹理后缀（用于服装专用表情）
        /// 例如: "_pajamas" → 会查找 happy_pajamas 等
        /// </summary>
        public string expressionSuffix = "";

        // ─────────────────────────────────────────────
        // 切换效果
        // ─────────────────────────────────────────────

        /// <summary>
        /// 切换动画类型: "Instant", "Fade", "Slide"
        /// </summary>
        public string transitionType = "Instant";

        /// <summary>
        /// 切换动画时长（秒）
        /// </summary>
        public float transitionDuration = 0.3f;

        // ─────────────────────────────────────────────
        // 优先级（用于同标签服装的选择）
        // ─────────────────────────────────────────────

        /// <summary>
        /// 优先级（同标签存在多个定义时使用）
        /// </summary>
        public int priority = 0;

        // ─────────────────────────────────────────────
        // 运行时方法
        // ─────────────────────────────────────────────

        /// <summary>
        /// 获取主体纹理路径
        /// </summary>
        public string GetBodyTexturePath(string personaName)
        {
            if (string.IsNullOrEmpty(bodyTexture))
            {
                return null; // 使用默认
            }
            return $"{personaName}/Narrators/Layered/{bodyTexture}";
        }

        /// <summary>
        /// 获取格式化的描述（用于提示词）
        /// </summary>
        public string GetFormattedDescription()
        {
            if (!string.IsNullOrEmpty(outfitDescription))
            {
                return $"{outfitTag}: {outfitDescription}";
            }
            return $"{outfitTag}: {label}";
        }
    }

    /// <summary>
    /// ⭐ v2.5.0: 服装定义管理器
    /// </summary>
    [StaticConstructorOnStartup]
    public static class OutfitDefManager
    {
        private static Dictionary<string, List<OutfitDef>> personaOutfitCache;
        private static bool initialized = false;

        static OutfitDefManager()
        {
            Initialize();
        }

        /// <summary>
        /// 初始化缓存
        /// </summary>
        public static void Initialize()
        {
            if (initialized) return;

            personaOutfitCache = new Dictionary<string, List<OutfitDef>>();

            foreach (var outfitDef in DefDatabase<OutfitDef>.AllDefs)
            {
                string key = outfitDef.personaDefName ?? "";

                if (!personaOutfitCache.ContainsKey(key))
                {
                    personaOutfitCache[key] = new List<OutfitDef>();
                }

                personaOutfitCache[key].Add(outfitDef);
            }

            initialized = true;

            int totalOutfits = DefDatabase<OutfitDef>.AllDefs.Count();
            Log.Message($"[OutfitDefManager] 已加载 {totalOutfits} 个服装定义");
        }

        /// <summary>
        /// 获取指定人格的所有可用服装
        /// </summary>
        public static List<OutfitDef> GetOutfitsForPersona(string personaDefName)
        {
            var result = new List<OutfitDef>();

            // 添加人格专属服装
            if (!string.IsNullOrEmpty(personaDefName) && 
                personaOutfitCache.TryGetValue(personaDefName, out var personaOutfits))
            {
                result.AddRange(personaOutfits);
            }

            // 添加通用服装
            if (personaOutfitCache.TryGetValue("", out var genericOutfits))
            {
                // 避免重复标签
                var existingTags = new HashSet<string>(result.Select(o => o.outfitTag));
                foreach (var outfit in genericOutfits)
                {
                    if (!existingTags.Contains(outfit.outfitTag))
                    {
                        result.Add(outfit);
                    }
                }
            }

            return result.OrderBy(o => o.priority).ToList();
        }

        /// <summary>
        /// 根据标签获取服装定义
        /// </summary>
        public static OutfitDef GetByTag(string personaDefName, string outfitTag)
        {
            if (string.IsNullOrEmpty(outfitTag)) return null;

            // 优先查找人格专属
            if (!string.IsNullOrEmpty(personaDefName) && 
                personaOutfitCache.TryGetValue(personaDefName, out var personaOutfits))
            {
                var match = personaOutfits
                    .Where(o => o.outfitTag.Equals(outfitTag, StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(o => o.priority)
                    .FirstOrDefault();

                if (match != null) return match;
            }

            // 查找通用服装
            if (personaOutfitCache.TryGetValue("", out var genericOutfits))
            {
                return genericOutfits
                    .Where(o => o.outfitTag.Equals(outfitTag, StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(o => o.priority)
                    .FirstOrDefault();
            }

            return null;
        }

        /// <summary>
        /// 获取格式化的可用服装列表（用于提示词）
        /// </summary>
        public static string GetFormattedOutfitList(string personaDefName)
        {
            var outfits = GetOutfitsForPersona(personaDefName);

            if (outfits.Count == 0)
            {
                return "（暂无可用服装）";
            }

            var lines = new List<string>();
            foreach (var outfit in outfits)
            {
                lines.Add($"- {outfit.GetFormattedDescription()}");
            }

            return string.Join("\n", lines);
        }

        /// <summary>
        /// 清除缓存（重新加载时调用）
        /// </summary>
        public static void ClearCache()
        {
            personaOutfitCache?.Clear();
            initialized = false;
        }
    }
}
