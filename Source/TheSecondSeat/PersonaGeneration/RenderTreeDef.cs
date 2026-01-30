using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace TheSecondSeat.PersonaGeneration
{
    // =========================================================================
    // RenderTreeDef v1.11.0 - 渲染树定义系统
    // =========================================================================
    // 
    // 功能说明：
    // 此系统将立绘渲染相关的映射配置从硬编码迁移到 XML 定义，
    // 使得子 Mod 可以通过 XML 自定义表情和口型纹理，无需修改代码。
    //
    // XML 配置示例：
    // <TheSecondSeat.PersonaGeneration.RenderTreeDef>
    //   <defName>Sideria_RenderTree</defName>
    //   <expressions>
    //     <li>
    //       <expression>Happy</expression>
    //       <suffix>_happy</suffix>
    //       <variants>
    //         <li><variant>1</variant><suffix>_happy1</suffix></li>
    //         <li><variant>2</variant><suffix>_happy2</suffix></li>
    //       </variants>
    //     </li>
    //   </expressions>
    //   <speaking>
    //     <visemeMap>
    //       <li><viseme>Closed</viseme><textureName>Closed_mouth</textureName></li>
    //       <li><viseme>Large</viseme><textureName>larger_mouth</textureName></li>
    //     </visemeMap>
    //     <opennessThresholds>
    //       <li><threshold>0.10</threshold><viseme>Closed</viseme></li>
    //       <li><threshold>0.30</threshold><viseme>Small</viseme></li>
    //     </opennessThresholds>
    //   </speaking>
    // </TheSecondSeat.PersonaGeneration.RenderTreeDef>
    //
    // =========================================================================

    /// <summary>
    /// 表情变体映射
    /// </summary>
    public class RenderTreeExpressionVariant
    {
        /// <summary>变体编号 (1-5)</summary>
        public int variant = 0;
        
        /// <summary>变体对应的后缀 (如 "_happy1", "_happy2")</summary>
        public string suffix = "";
        
        /// <summary>变体对应的纹理名称 (可选，用于嘴型覆盖)</summary>
        public string textureName = "";

        /// <summary>变体对应的眼睛纹理名称 (可选)</summary>
        public string eyesTexture = "";
    }

    /// <summary>
    /// 表情映射配置
    /// </summary>
    public class ExpressionMapping
    {
        /// <summary>表情类型名称 (如 "Happy", "Sad")</summary>
        public string expression = "Neutral";
        
        /// <summary>基础后缀 (如 "_happy", "_sad")</summary>
        public string suffix = "";
        
        /// <summary>表情变体列表</summary>
        public List<RenderTreeExpressionVariant> variants = new List<RenderTreeExpressionVariant>();
        
        /// <summary>
        /// 获取指定变体的后缀
        /// </summary>
        public string GetVariantSuffix(int variantIndex)
        {
            if (variantIndex <= 0 || variants == null || variants.Count == 0)
                return suffix;
            
            var variant = variants.FirstOrDefault(v => v.variant == variantIndex);
            return variant != null && !string.IsNullOrEmpty(variant.suffix)
                ? variant.suffix
                : $"{suffix}{variantIndex}";
        }

        /// <summary>
        /// 获取指定变体的眼睛纹理
        /// </summary>
        public string GetVariantEyes(int variantIndex)
        {
            if (variantIndex <= 0 || variants == null || variants.Count == 0)
                return null;
            
            var variant = variants.FirstOrDefault(v => v.variant == variantIndex);
            return variant?.eyesTexture;
        }

        /// <summary>
        /// 获取指定变体的嘴巴纹理
        /// </summary>
        public string GetVariantMouth(int variantIndex)
        {
            if (variantIndex <= 0 || variants == null || variants.Count == 0)
                return null;
            
            var variant = variants.FirstOrDefault(v => v.variant == variantIndex);
            return variant?.textureName;
        }
        
        /// <summary>
        /// 获取变体数量
        /// </summary>
        public int GetVariantCount()
        {
            return variants?.Count ?? 0;
        }
    }

    /// <summary>
    /// Viseme 纹理映射
    /// </summary>
    public class VisemeTextureMapping
    {
        /// <summary>Viseme 类型名称 (Closed, Small, Medium, Large, Smile, OShape)</summary>
        public string viseme = "Closed";
        
        /// <summary>对应的纹理文件名 (如 "Closed_mouth", "larger_mouth")</summary>
        public string textureName = "Closed_mouth";
    }

    /// <summary>
    /// 开合度阈值映射
    /// </summary>
    public class OpennessThreshold
    {
        /// <summary>开合度阈值 (0.0 - 1.0)</summary>
        public float threshold = 0f;
        
        /// <summary>低于此阈值时使用的 Viseme 类型</summary>
        public string viseme = "Closed";
    }

    /// <summary>
    /// Azure Viseme ID 映射
    /// </summary>
    public class AzureVisemeMapping
    {
        /// <summary>Azure Viseme ID (0-21)</summary>
        public int azureId = 0;
        
        /// <summary>对应的纹理文件名</summary>
        public string textureName = "Closed_mouth";
    }

    /// <summary>
    /// 身体姿态映射 (用于替换 base_body)
    /// </summary>
    public class BodyMapping
    {
        /// <summary>姿态名称 (如 Standing, Sitting, Laying)</summary>
        public string posture = "Standing";
        
        /// <summary>可选：服装标签过滤 (如果设置，仅当穿着此类服装时生效)</summary>
        public string apparelTag = "";
        
        /// <summary>替换的身体纹理名称</summary>
        public string textureName = "";
    }

    /// <summary>
    /// 配件/特效映射 (覆盖在最上层)
    /// </summary>
    public class AccessoryMapping
    {
        /// <summary>配件名称 (用于标识)</summary>
        public string name = "Accessory";
        
        /// <summary>纹理名称</summary>
        public string textureName = "";
        
        /// <summary>生效条件 (Always, Talking, Blinking)</summary>
        public string condition = "Always";
        
        /// <summary>Z轴偏移 (用于层级微调，默认在最上层)</summary>
        public float zOffset = 0f;
    }

    /// <summary>
    /// 摸头动画阶段
    /// </summary>
    public class HeadPatPhase
    {
        /// <summary>持续时间阈值 (秒)</summary>
        public float durationThreshold = 0f;
        
        /// <summary>手部覆盖纹理名称</summary>
        public string textureName = "";
        
        /// <summary>触发的表情</summary>
        public string expression = "";
        
        /// <summary>
        /// 表情变体编号 (1-5)
        /// 0 或负数表示使用基础表情（无变体）
        /// 可在渲染树编辑器中配置
        /// </summary>
        public int variant = 0;
        
        /// <summary>播放的音效 (可选)</summary>
        public string sound = "";
    }

    /// <summary>
    /// 摸头动画配置
    /// </summary>
    public class HeadPatConfig
    {
        /// <summary>是否启用递进动画</summary>
        public bool enabled = false;
        
        /// <summary>阶段列表 (按时间排序)</summary>
        public List<HeadPatPhase> phases = new List<HeadPatPhase>();
    }

    /// <summary>
    /// 交互区域矩形 (归一化坐标，左上角为原点)
    /// 坐标系：X轴从左向右 (0→1)，Y轴从上向下 (0→1)
    /// </summary>
    public class ZoneRect
    {
        /// <summary>左边界 (0.0 - 1.0)</summary>
        public float xMin = 0f;
        
        /// <summary>上边界 (0.0 - 1.0)</summary>
        public float yMin = 0f;
        
        /// <summary>右边界 (0.0 - 1.0)</summary>
        public float xMax = 1f;
        
        /// <summary>下边界 (0.0 - 1.0)</summary>
        public float yMax = 1f;
        
        /// <summary>
        /// 检查归一化坐标点是否在此区域内
        /// </summary>
        public bool Contains(float x, float y)
        {
            return x >= xMin && x <= xMax && y >= yMin && y <= yMax;
        }
    }

    /// <summary>
    /// 交互区域配置
    /// 用于定义立绘上的可交互区域（头部、身体等）
    /// 坐标使用归一化值 (0.0 - 1.0)，原点在左上角
    /// </summary>
    public class InteractionZones
    {
        /// <summary>
        /// 头部区域
        /// 默认为 null，表示使用硬编码回退逻辑
        /// </summary>
        public ZoneRect head = null;
        
        /// <summary>
        /// 身体区域
        /// 默认为 null，表示使用硬编码回退逻辑
        /// </summary>
        public ZoneRect body = null;
        
        /// <summary>
        /// 是否有自定义配置
        /// </summary>
        public bool HasCustomConfig => head != null || body != null;
    }

    /// <summary>
    /// 高级动画配置
    /// </summary>
    public class AnimationConfig
    {
        // ===== 呼吸参数 (Breathing) =====
        /// <summary>呼吸时的缩放强度 (0.0 - 0.1)</summary>
        public float breathScaleIntensity = 0.005f;
        
        /// <summary>头部(五官)相对于身体的呼吸滞后 (秒)</summary>
        public float headBreathLag = 0.05f;
        
        // ===== 眨眼参数 (Blinking) =====
        /// <summary>是否启用平滑眨眼 (缩放效果)</summary>
        public bool smoothBlinking = true;
    }

    /// <summary>
    /// 说话动画配置
    /// </summary>
    public class SpeakingConfig
    {
        /// <summary>Viseme → 纹理名称映射</summary>
        public List<VisemeTextureMapping> visemeMap = new List<VisemeTextureMapping>();
        
        /// <summary>开合度 → Viseme 阈值映射 (按阈值升序排列)</summary>
        public List<OpennessThreshold> opennessThresholds = new List<OpennessThreshold>();
        
        /// <summary>Azure Viseme ID → 纹理名称映射</summary>
        public List<AzureVisemeMapping> azureVisemeMap = new List<AzureVisemeMapping>();
        
        /// <summary>
        /// 根据 Viseme 类型获取纹理名称
        /// </summary>
        public string GetTextureName(VisemeCode viseme)
        {
            string visemeName = viseme.ToString();
            var mapping = visemeMap?.FirstOrDefault(m => m.viseme == visemeName);
            return mapping?.textureName ?? GetDefaultTextureName(viseme);
        }
        
        /// <summary>
        /// 根据开合度获取 Viseme 类型
        /// </summary>
        public VisemeCode GetVisemeFromOpenness(float openness)
        {
            if (opennessThresholds == null || opennessThresholds.Count == 0)
                return GetDefaultVisemeFromOpenness(openness);
            
            // 按阈值升序排列后查找
            var sorted = opennessThresholds.OrderBy(t => t.threshold).ToList();
            
            foreach (var threshold in sorted)
            {
                if (openness < threshold.threshold)
                {
                    if (Enum.TryParse<VisemeCode>(threshold.viseme, out var result))
                        return result;
                }
            }
            
            // 如果超过所有阈值，返回最后一个配置的 Viseme
            var last = sorted.LastOrDefault();
            if (last != null && Enum.TryParse<VisemeCode>(last.viseme, out var lastResult))
                return lastResult;
            
            return VisemeCode.OShape;
        }
        
        /// <summary>
        /// 根据 Azure Viseme ID 获取纹理名称
        /// </summary>
        public string GetTextureFromAzureId(int azureId)
        {
            var mapping = azureVisemeMap?.FirstOrDefault(m => m.azureId == azureId);
            return mapping?.textureName ?? GetDefaultAzureTexture(azureId);
        }
        
        // ─────────────────────────────────────────────
        // 默认值（兼容性回退）
        // ─────────────────────────────────────────────
        
        private static string GetDefaultTextureName(VisemeCode viseme)
        {
            return viseme switch
            {
                VisemeCode.Closed => "Closed_mouth",
                VisemeCode.Small => "USE_BASE",
                VisemeCode.Medium => "medium_mouth",
                VisemeCode.Large => "larger_mouth",
                VisemeCode.Smile => "happy_mouth",
                VisemeCode.OShape => "O_mouth",
                _ => "Closed_mouth"
            };
        }
        
        private static VisemeCode GetDefaultVisemeFromOpenness(float openness)
        {
            if (openness < 0.10f) return VisemeCode.Closed;
            if (openness < 0.30f) return VisemeCode.Small;
            if (openness < 0.55f) return VisemeCode.Medium;
            if (openness < 0.80f) return VisemeCode.Large;
            return VisemeCode.OShape;
        }
        
        private static string GetDefaultAzureTexture(int azureId)
        {
            return azureId switch
            {
                0 or 21 => "Closed_mouth",
                1 or 2 or 9 or 11 => "larger_mouth",
                6 or 7 or 8 => "happy_mouth",
                3 or 13 or 14 => "O_mouth",
                4 or 5 or 10 or 15 or 16 => "medium_mouth",
                _ => "USE_BASE"
            };
        }
    }

    /// <summary>
    /// ⭐ v1.11.0: 渲染树定义 (RenderTreeDef)
    /// 用于将立绘渲染的映射配置外部化到 XML
    /// </summary>
    public class RenderTreeDef : Def
    {
        // ─────────────────────────────────────────────
        // 表情系统配置
        // ─────────────────────────────────────────────
        
        /// <summary>表情映射列表</summary>
        public List<ExpressionMapping> expressions = new List<ExpressionMapping>();
        
        // ─────────────────────────────────────────────
        // 说话动画配置
        // ─────────────────────────────────────────────
        
        /// <summary>说话/嘴型动画配置</summary>
        public SpeakingConfig speaking = new SpeakingConfig();

        // ─────────────────────────────────────────────
        // ─────────────────────────────────────────────
        // 高级动画配置 (Advanced Animation)
        // ─────────────────────────────────────────────
        
        /// <summary>高级动画配置 (视差、呼吸增强等)</summary>
        public AnimationConfig animation = new AnimationConfig();
        // 身体与姿态配置 (Body & Posture)
        // ─────────────────────────────────────────────
        
        /// <summary>身体映射列表 (替换 base_body)</summary>
        public List<BodyMapping> bodyMappings = new List<BodyMapping>();

        // ─────────────────────────────────────────────
        // 配件与特效配置 (Accessories & Effects)
        // ─────────────────────────────────────────────
        
        /// <summary>配件映射列表 (覆盖最上层)</summary>
        public List<AccessoryMapping> accessories = new List<AccessoryMapping>();

        // ─────────────────────────────────────────────
        // 摸头动画配置 (Head Pat)
        // ─────────────────────────────────────────────
        
        /// <summary>摸头动画配置</summary>
        public HeadPatConfig headPat = new HeadPatConfig();

        // ─────────────────────────────────────────────
        // 交互区域配置 (Interaction Zones)
        // ─────────────────────────────────────────────
        
        /// <summary>
        /// 交互区域配置 (头部、身体等)
        /// 使用归一化坐标 (0.0-1.0)，原点在左上角
        /// 由多模态分析引擎提供坐标数据
        /// </summary>
        public InteractionZones interactionZones = null;
        
        // ─────────────────────────────────────────────
        // 缓存 (运行时)
        // ─────────────────────────────────────────────
        
        [Unsaved]
        private Dictionary<string, ExpressionMapping> expressionCache;
        
        /// <summary>
        /// 解析引用时初始化缓存
        /// </summary>
        public override void ResolveReferences()
        {
            base.ResolveReferences();
            
            // 初始化表情缓存
            expressionCache = new Dictionary<string, ExpressionMapping>();
            if (expressions != null)
            {
                foreach (var mapping in expressions)
                {
                    if (!string.IsNullOrEmpty(mapping.expression))
                    {
                        expressionCache[mapping.expression] = mapping;
                    }
                }
            }
            
            // 确保 speaking 不为 null
            speaking ??= new SpeakingConfig();
            
            // 确保 headPat 不为 null
            headPat ??= new HeadPatConfig();

            if (Prefs.DevMode)
            {
                Log.Message($"[RenderTreeDef] Loaded {defName}: " +
                           $"{expressions?.Count ?? 0} expressions, " +
                           $"{speaking?.visemeMap?.Count ?? 0} viseme mappings");
            }
        }
        
        // ─────────────────────────────────────────────
        // 公共 API
        // ─────────────────────────────────────────────
        
        /// <summary>
        /// 获取表情后缀
        /// </summary>
        /// <param name="expression">表情类型</param>
        /// <param name="variantIndex">变体编号 (0=基础, 1-5=变体)</param>
        /// <returns>后缀字符串 (如 "_happy", "_happy1")</returns>
        public string GetExpressionSuffix(ExpressionType expression, int variantIndex = 0)
        {
            string exprName = expression.ToString();
            
            if (expressionCache != null && expressionCache.TryGetValue(exprName, out var mapping))
            {
                return mapping.GetVariantSuffix(variantIndex);
            }
            
            // 回退到默认硬编码逻辑
            return GetDefaultExpressionSuffix(expression, variantIndex);
        }

        /// <summary>
        /// 获取眼睛纹理配置
        /// </summary>
        public string GetEyesTexture(ExpressionType expression, int variantIndex = 0)
        {
            string exprName = expression.ToString();
            if (expressionCache != null && expressionCache.TryGetValue(exprName, out var mapping))
            {
                return mapping.GetVariantEyes(variantIndex);
            }
            return null;
        }

        /// <summary>
        /// 获取嘴巴纹理配置
        /// </summary>
        public string GetMouthTexture(ExpressionType expression, int variantIndex = 0)
        {
            string exprName = expression.ToString();
            if (expressionCache != null && expressionCache.TryGetValue(exprName, out var mapping))
            {
                return mapping.GetVariantMouth(variantIndex);
            }
            return null;
        }
        
        /// <summary>
        /// 获取表情变体数量
        /// </summary>
        public int GetVariantCount(ExpressionType expression)
        {
            string exprName = expression.ToString();
            
            if (expressionCache != null && expressionCache.TryGetValue(exprName, out var mapping))
            {
                return mapping.GetVariantCount();
            }
            
            // 默认变体数量
            return expression == ExpressionType.Neutral ? 0 : 5;
        }
        
        /// <summary>
        /// 获取 Viseme 对应的纹理名称
        /// </summary>
        public string GetVisemeTextureName(VisemeCode viseme)
        {
            return speaking?.GetTextureName(viseme) ?? SpeakingConfigDefaults.GetDefaultTextureName(viseme);
        }
        
        /// <summary>
        /// 根据开合度获取 Viseme
        /// </summary>
        public VisemeCode GetVisemeFromOpenness(float openness)
        {
            return speaking?.GetVisemeFromOpenness(openness) ?? SpeakingConfigDefaults.GetDefaultVisemeFromOpenness(openness);
        }
        
        /// <summary>
        /// 根据 Azure Viseme ID 获取纹理名称
        /// </summary>
        public string GetTextureFromAzureVisemeId(int azureId)
        {
            return speaking?.GetTextureFromAzureId(azureId) ?? SpeakingConfigDefaults.GetDefaultAzureTexture(azureId);
        }
        
        // ─────────────────────────────────────────────
        // 默认值回退
        // ─────────────────────────────────────────────
        
        private static string GetDefaultExpressionSuffix(ExpressionType expression, int variantIndex)
        {
            string baseSuffix = expression switch
            {
                ExpressionType.Neutral => "",
                ExpressionType.Happy => "_happy",
                ExpressionType.Sad => "_sad",
                ExpressionType.Angry => "_angry",
                ExpressionType.Surprised => "_surprised",
                ExpressionType.Worried => "_worried",
                ExpressionType.Smug => "_smug",
                ExpressionType.Disappointed => "_disappointed",
                ExpressionType.Thoughtful => "_thoughtful",
                ExpressionType.Annoyed => "_annoyed",
                ExpressionType.Playful => "_playful",
                ExpressionType.Shy => "_shy",
                ExpressionType.Confused => "_confused",
                _ => ""
            };
            
            if (variantIndex <= 0 || string.IsNullOrEmpty(baseSuffix))
                return baseSuffix;
            
            return $"{baseSuffix}{variantIndex}";
        }
        
        // ─────────────────────────────────────────────
        // 静态工具方法（私有，仅内部使用）
        // ─────────────────────────────────────────────
        
        private static class SpeakingConfigDefaults
        {
            public static string GetDefaultTextureName(VisemeCode viseme)
            {
                return viseme switch
                {
                    VisemeCode.Closed => "Closed_mouth",
                    VisemeCode.Small => "USE_BASE",
                    VisemeCode.Medium => "medium_mouth",
                    VisemeCode.Large => "larger_mouth",
                    VisemeCode.Smile => "happy_mouth",
                    VisemeCode.OShape => "O_mouth",
                    _ => "Closed_mouth"
                };
            }
            
            public static VisemeCode GetDefaultVisemeFromOpenness(float openness)
            {
                if (openness < 0.10f) return VisemeCode.Closed;
                if (openness < 0.30f) return VisemeCode.Small;
                if (openness < 0.55f) return VisemeCode.Medium;
                if (openness < 0.80f) return VisemeCode.Large;
                return VisemeCode.OShape;
            }
            
            public static string GetDefaultAzureTexture(int azureId)
            {
                return azureId switch
                {
                    0 or 21 => "Closed_mouth",
                    1 or 2 or 9 or 11 => "larger_mouth",
                    6 or 7 or 8 => "happy_mouth",
                    3 or 13 or 14 => "O_mouth",
                    4 or 5 or 10 or 15 or 16 => "medium_mouth",
                    _ => "USE_BASE"
                };
            }
        }
        
        /// <summary>
        /// 将另一个 Def 的数据应用到此实例
        /// </summary>
        public void ApplyFrom(RenderTreeDef other)
        {
            if (other == null) return;
            this.expressions = other.expressions;
            this.speaking = other.speaking;
            
            // 重新构建缓存
            this.ResolveReferences();
        }

        /// <summary>
        /// 获取匹配的身体纹理
        /// </summary>
        public string GetBodyTexture(string posture, string apparelTag = null)
        {
            if (bodyMappings == null || bodyMappings.Count == 0) return null;

            // 优先匹配 posture 和 apparelTag 都符合的
            var match = bodyMappings.FirstOrDefault(m =>
                m.posture == posture &&
                (string.IsNullOrEmpty(m.apparelTag) || m.apparelTag == apparelTag));
            
            return match?.textureName;
        }

        /// <summary>
        /// 获取所有应显示的配件
        /// </summary>
        public List<string> GetActiveAccessories(string currentCondition)
        {
            if (accessories == null) return new List<string>();
            
            return accessories
                .Where(a => a.condition == "Always" || a.condition == currentCondition)
                .Select(a => a.textureName)
                .ToList();
        }

        /// <summary>
        /// 根据摸头持续时间获取当前阶段
        /// </summary>
        public HeadPatPhase GetHeadPatPhase(float duration)
        {
            if (headPat == null || !headPat.enabled || headPat.phases == null || headPat.phases.Count == 0)
                return null;
            
            // 查找持续时间超过阈值的最后一个阶段
            return headPat.phases
                .Where(p => duration >= p.durationThreshold)
                .OrderByDescending(p => p.durationThreshold)
                .FirstOrDefault();
        }
        
        /// <summary>
        /// ⭐ v1.14.0: 根据摸头持续时间获取当前阶段索引
        /// 用于确定性地追踪阶段变化，避免重复触发表情
        /// </summary>
        /// <param name="duration">摸头持续时间（秒）</param>
        /// <returns>阶段索引（-1 表示无阶段，0, 1, 2... 表示阶段编号）</returns>
        public int GetHeadPatPhaseIndex(float duration)
        {
            if (headPat == null || !headPat.enabled || headPat.phases == null || headPat.phases.Count == 0)
                return -1;
            
            // 按阈值升序排列阶段
            var sortedPhases = headPat.phases.OrderBy(p => p.durationThreshold).ToList();
            
            // 查找当前所处的阶段索引
            int currentIndex = -1;
            for (int i = 0; i < sortedPhases.Count; i++)
            {
                if (duration >= sortedPhases[i].durationThreshold)
                {
                    currentIndex = i;
                }
                else
                {
                    break;
                }
            }
            
            return currentIndex;
        }
    }
    
    /// <summary>
    /// RenderTreeDef 管理器（静态访问点）
    /// </summary>
    [StaticConstructorOnStartup]
    public static class RenderTreeDefManager
    {
        private static Dictionary<string, RenderTreeDef> cache = new Dictionary<string, RenderTreeDef>();
        private static RenderTreeDef defaultDef;

        static RenderTreeDefManager()
        {
            if (Prefs.DevMode)
            {
                Log.Message("[RenderTreeDefManager] Initializing and loading custom render tree configs...");
            }
            LoadCustomRenderTreeDefs();
        }

        private static void LoadCustomRenderTreeDefs()
        {
            foreach (var def in DefDatabase<RenderTreeDef>.AllDefs)
            {
                string path = RenderTreeConfigIO.GetDefaultSavePath(def.defName);
                if (System.IO.File.Exists(path))
                {
                    var customDef = RenderTreeConfigIO.LoadFromXml(path);
                    if (customDef != null)
                    {
                        def.ApplyFrom(customDef);
                        if (Prefs.DevMode)
                        {
                            Log.Message($"[RenderTreeDefManager] Applied custom config for {def.defName} from {path}");
                        }
                    }
                }
            }
        }
        
        /// <summary>
        /// 获取指定人格的 RenderTreeDef
        /// </summary>
        /// <param name="personaDefName">人格 DefName（如 "Sideria"）</param>
        /// <returns>对应的 RenderTreeDef，如果未找到则返回默认配置</returns>
        public static RenderTreeDef GetRenderTree(string personaDefName)
        {
            if (string.IsNullOrEmpty(personaDefName))
                return GetDefault();
            
            // 检查缓存
            if (cache.TryGetValue(personaDefName, out var cached))
                return cached;
            
            // 尝试查找专用的 RenderTreeDef
            // 命名规范: {PersonaDefName}_RenderTree
            string expectedDefName = $"{personaDefName}_RenderTree";
            var renderTree = DefDatabase<RenderTreeDef>.GetNamed(expectedDefName, errorOnFail: false);
            
            if (renderTree != null)
            {
                cache[personaDefName] = renderTree;
                return renderTree;
            }
            
            // 尝试从 NarratorPersonaDef 获取引用
            var personaDef = DefDatabase<NarratorPersonaDef>.GetNamed(personaDefName, errorOnFail: false);
            if (personaDef?.renderTreeDef != null)
            {
                cache[personaDefName] = personaDef.renderTreeDef;
                return personaDef.renderTreeDef;
            }
            
            // 回退到默认
            cache[personaDefName] = GetDefault();
            return cache[personaDefName];
        }
        
        /// <summary>
        /// 获取默认 RenderTreeDef
        /// </summary>
        public static RenderTreeDef GetDefault()
        {
            if (defaultDef == null)
            {
                // 尝试查找名为 "Default_RenderTree" 的定义
                defaultDef = DefDatabase<RenderTreeDef>.GetNamed("Default_RenderTree", errorOnFail: false);
                
                // 如果没有默认定义，创建一个运行时实例
                if (defaultDef == null)
                {
                    defaultDef = CreateRuntimeDefault();
                }
            }
            return defaultDef;
        }
        
        /// <summary>
        /// 清除缓存（游戏重载时调用）
        /// </summary>
        public static void ClearCache()
        {
            cache.Clear();
            defaultDef = null;
        }
        
        /// <summary>
        /// 创建运行时默认配置（无 XML 定义时使用）
        /// </summary>
        private static RenderTreeDef CreateRuntimeDefault()
        {
            var def = new RenderTreeDef
            {
                defName = "Runtime_Default_RenderTree",
                expressions = new List<ExpressionMapping>(),
                speaking = new TheSecondSeat.PersonaGeneration.SpeakingConfig()
            };
            
            // 添加所有表情的默认映射
            foreach (ExpressionType expr in Enum.GetValues(typeof(ExpressionType)))
            {
                if (expr == ExpressionType.Neutral)
                {
                    def.expressions.Add(new ExpressionMapping
                    {
                        expression = expr.ToString(),
                        suffix = "",
                        variants = new List<RenderTreeExpressionVariant>()
                    });
                }
                else
                {
                    string baseSuffix = $"_{expr.ToString().ToLower()}";
                    var mapping = new ExpressionMapping
                    {
                        expression = expr.ToString(),
                        suffix = baseSuffix,
                        variants = new List<RenderTreeExpressionVariant>()
                    };
                    
                    // 添加 5 个变体
                    for (int i = 1; i <= 5; i++)
                    {
                        mapping.variants.Add(new RenderTreeExpressionVariant
                        {
                            variant = i,
                            suffix = $"{baseSuffix}{i}"
                        });
                    }
                    
                    def.expressions.Add(mapping);
                }
            }
            
            // 添加默认的 Viseme 映射
            def.speaking.visemeMap = new List<VisemeTextureMapping>
            {
                new VisemeTextureMapping { viseme = "Closed", textureName = "Closed_mouth" },
                new VisemeTextureMapping { viseme = "Small", textureName = "USE_BASE" },
                new VisemeTextureMapping { viseme = "Medium", textureName = "medium_mouth" },
                new VisemeTextureMapping { viseme = "Large", textureName = "larger_mouth" },
                new VisemeTextureMapping { viseme = "Smile", textureName = "happy_mouth" },
                new VisemeTextureMapping { viseme = "OShape", textureName = "O_mouth" }
            };
            
            // 添加默认的开合度阈值
            def.speaking.opennessThresholds = new List<OpennessThreshold>
            {
                new OpennessThreshold { threshold = 0.10f, viseme = "Closed" },
                new OpennessThreshold { threshold = 0.30f, viseme = "Small" },
                new OpennessThreshold { threshold = 0.55f, viseme = "Medium" },
                new OpennessThreshold { threshold = 0.80f, viseme = "Large" },
                new OpennessThreshold { threshold = 1.00f, viseme = "OShape" }
            };
            
            // 添加默认的 Azure Viseme 映射
            def.speaking.azureVisemeMap = new List<AzureVisemeMapping>
            {
                new AzureVisemeMapping { azureId = 0, textureName = "Closed_mouth" },
                new AzureVisemeMapping { azureId = 21, textureName = "Closed_mouth" },
                new AzureVisemeMapping { azureId = 1, textureName = "larger_mouth" },
                new AzureVisemeMapping { azureId = 2, textureName = "larger_mouth" },
                new AzureVisemeMapping { azureId = 9, textureName = "larger_mouth" },
                new AzureVisemeMapping { azureId = 11, textureName = "larger_mouth" },
                new AzureVisemeMapping { azureId = 6, textureName = "happy_mouth" },
                new AzureVisemeMapping { azureId = 7, textureName = "happy_mouth" },
                new AzureVisemeMapping { azureId = 8, textureName = "happy_mouth" },
                new AzureVisemeMapping { azureId = 3, textureName = "O_mouth" },
                new AzureVisemeMapping { azureId = 13, textureName = "O_mouth" },
                new AzureVisemeMapping { azureId = 14, textureName = "O_mouth" },
                new AzureVisemeMapping { azureId = 4, textureName = "medium_mouth" },
                new AzureVisemeMapping { azureId = 5, textureName = "medium_mouth" },
                new AzureVisemeMapping { azureId = 10, textureName = "medium_mouth" },
                new AzureVisemeMapping { azureId = 15, textureName = "medium_mouth" },
                new AzureVisemeMapping { azureId = 16, textureName = "medium_mouth" }
            };
            
            return def;
        }
    }
}
