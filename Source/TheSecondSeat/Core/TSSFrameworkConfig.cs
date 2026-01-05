using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace TheSecondSeat.Core
{
    /// <summary>
    /// 🏗️ TSS 框架配置类
    /// 集中管理所有可扩展的配置项，支持附属 Mod 覆盖默认值
    /// 
    /// 使用方式：
    /// - 主 Mod：定义默认值
    /// - 附属 Mod：通过 StaticConstructorOnStartup 覆盖配置
    /// 
    /// 示例：
    /// [StaticConstructorOnStartup]
    /// public static class MyModInit {
    ///     static MyModInit() {
    ///         TSSFrameworkConfig.AssetPaths.PortraitSearchPaths.Add("MyMod/Portraits/{0}/base");
    ///         TSSFrameworkConfig.TTS.DefaultVoiceName = "ja-JP-NanamiNeural";
    ///     }
    /// }
    /// </summary>
    public static class TSSFrameworkConfig
    {
        // ============================================
        // 📁 资源路径配置
        // ============================================
        
        public static class AssetPaths
        {
            /// <summary>立绘搜索路径模板（{0} = personaName）</summary>
            public static List<string> PortraitSearchPaths { get; } = new()
            {
                "UI/Narrators/9x16/{0}/base",
                "UI/Narrators/9x16/{0}",
                "UI/Narrators/{0}",
                "Narrators/Layered/{0}/base",
                "UI/HeroArt/{0}"
            };
            
            /// <summary>降临资源搜索路径模板（{0} = category, {1} = personaName, {2} = assetName）</summary>
            public static List<string> DescentAssetPaths { get; } = new()
            {
                "{1}/Narrators/Descent/{0}/{2}",
                "UI/Narrators/Descent/{0}/{1}/{2}",
                "UI/Narrators/Descent/{0}/{2}",
                "Narrators/Descent/{0}/{2}"
            };
            
            /// <summary>降临姿态检查路径模板（{0} = personaName）</summary>
            public static List<string> DescentPostureCheckPaths { get; } = new()
            {
                "{0}/Narrators/Descent/Postures/standing",
                "{0}/Narrators/Descent/Effects/glow",
                "UI/Narrators/Descent/Postures/{0}/standing",
                "Narrators/Descent/Postures/{0}/standing",
                "UI/Narrators/Descent/Postures/standing",
                "Narrators/Descent/Postures/standing",
                "UI/Narrators/Descent/Effects/{0}/glow",
                "UI/Narrators/Descent/Effects/glow"
            };
            
            /// <summary>默认占位符路径</summary>
            public static string DefaultPlaceholderPath { get; set; } = "UI/Narrators/Default/Placeholder";
        }
        
        // ============================================
        // 🎤 TTS 语音配置
        // ============================================
        
        public static class TTS
        {
            /// <summary>默认语音名称（TTS）</summary>
            public static string DefaultVoiceName { get; set; } = "zh-CN-XiaoxiaoNeural";
            
            /// <summary>默认 TTS 提供者</summary>
            public static string DefaultProvider { get; set; } = "edge";
            
            /// <summary>默认 Azure 区域</summary>
            public static string DefaultAzureRegion { get; set; } = "eastus";
            
            /// <summary>默认语速</summary>
            public static float DefaultSpeechRate { get; set; } = 1.0f;
            
            /// <summary>默认音量</summary>
            public static float DefaultVolume { get; set; } = 1.0f;
            
            /// <summary>默认音调</summary>
            public static float DefaultPitch { get; set; } = 1.0f;
        }
        
        // ============================================
        // ⬇️ 降临系统配置
        // ============================================
        
        public static class Descent
        {
            /// <summary>默认降临持续时间（秒）</summary>
            public static float DefaultDuration { get; set; } = 300f;
            
            /// <summary>默认降临冷却时间（秒）</summary>
            public static float DefaultCooldown { get; set; } = 600f;
            
            /// <summary>默认降临天降物 DefName</summary>
            public static string DefaultSkyfallerDef { get; set; } = "DropPodIncoming";
            
            // ============================================
            // ⭐ 通用路径模板（子mod无需重复配置）
            // ============================================
            
            /// <summary>
            /// ⭐ 降临姿态路径模板（{0} = personaName, {1} = postureName）
            /// 示例结果: "PersonaName/Narrators/Descent/Postures/casting"
            /// </summary>
            public static string PosturePathTemplate { get; set; } = "{0}/Narrators/Descent/Postures/{1}";
            
            /// <summary>
            /// ⭐ 降临特效路径模板（{0} = personaName, {1} = effectName）
            /// 示例结果: "PersonaName/Narrators/Descent/Effects/assist"
            /// </summary>
            public static string EffectPathTemplate { get; set; } = "{0}/Narrators/Descent/Effects/{1}";
            
            /// <summary>
            /// ⭐ 降临阴影路径模板（{0} = personaName）
            /// 示例结果: "PersonaName/Narrators/Descent/Effects/DragonShadow"
            /// </summary>
            public static string ShadowPathTemplate { get; set; } = "{0}/Narrators/Descent/Effects/DragonShadow";
            
            /// <summary>
            /// ⭐ 默认姿态名称（子mod可省略配置）
            /// </summary>
            public static string DefaultPostureName { get; set; } = "descent_pose";
            
            /// <summary>
            /// ⭐ 默认特效名称（子mod可省略配置）
            /// </summary>
            public static string DefaultEffectName { get; set; } = "effect_assist";
            
            /// <summary>
            /// ⭐ 生成完整的姿态路径
            /// </summary>
            public static string GetPosturePath(string personaName, string postureName = null)
            {
                postureName = string.IsNullOrEmpty(postureName) ? DefaultPostureName : postureName;
                return string.Format(PosturePathTemplate, personaName, postureName);
            }
            
            /// <summary>
            /// ⭐ 生成完整的特效路径
            /// </summary>
            public static string GetEffectPath(string personaName, string effectName = null)
            {
                effectName = string.IsNullOrEmpty(effectName) ? DefaultEffectName : effectName;
                return string.Format(EffectPathTemplate, personaName, effectName);
            }
            
            /// <summary>
            /// ⭐ 生成完整的阴影路径
            /// </summary>
            public static string GetShadowPath(string personaName)
            {
                return string.Format(ShadowPathTemplate, personaName);
            }
        }
        
        // ============================================
        // 🖼️ 立绘系统配置
        // ============================================
        
        public static class Portrait
        {
            /// <summary>立绘原始宽度（像素）</summary>
            public static float OriginalWidth { get; set; } = 2308f;
            
            /// <summary>立绘原始高度（像素）</summary>
            public static float OriginalHeight { get; set; } = 3544f;
            
            /// <summary>默认缩放比例</summary>
            public static float DefaultScaleFactor { get; set; } = 0.15f;
            
            /// <summary>立绘面板水平偏移</summary>
            public static float PanelOffsetX { get; set; } = 10f;
            
            /// <summary>立绘面板垂直偏移（负值向上）</summary>
            public static float PanelOffsetY { get; set; } = -40f;
            
            // ============================================
            // ⭐ 通用路径模板（子mod无需重复配置）
            // ============================================
            
            /// <summary>
            /// ⭐ 立绘基础路径模板（{0} = personaName）
            /// 示例结果: "UI/Narrators/9x16/PersonaName/base"
            /// </summary>
            public static string BasePathTemplate { get; set; } = "UI/Narrators/9x16/{0}/base";
            
            /// <summary>
            /// ⭐ 生成完整的立绘路径
            /// </summary>
            public static string GetPortraitPath(string personaName)
            {
                return string.Format(BasePathTemplate, personaName);
            }
        }
        
        // ============================================
        // 🎭 人格系统配置
        // ============================================
        
        public static class Persona
        {
            /// <summary>禁用分层立绘的叙事者 DefName 列表（原版叙事者）</summary>
            public static HashSet<string> VanillaStorytellers { get; } = new()
            {
                "Cassandra_Classic",
                "Phoebe_Chillax",
                "Randy_Random",
                "Igor_Invader",
                "Luna_Protector"
            };
            
            /// <summary>人格名称提取时需移除的后缀</summary>
            public static List<string> NameSuffixesToRemove { get; } = new()
            {
                "_Default", "_Classic", "_Custom", "_Persona",
                "_Chillax", "_Random", "_Invader", "_Protector"
            };
            
            /// <summary>默认人格名称</summary>
            public static string DefaultNarratorName { get; set; } = "Unknown";
            
            /// <summary>⭐ 传记最大长度（Token优化，0=不限制）</summary>
            public static int BiographyMaxLength { get; set; } = 500;
        }
        
        // ============================================
        // 💾 缓存配置
        // ============================================
        
        public static class Cache
        {
            /// <summary>最大缓存条目数</summary>
            public static int MaxCacheSize { get; set; } = 100;
            
            /// <summary>缓存过期 Tick 数（约 1000 tick/秒）</summary>
            public static int CacheExpireTicks { get; set; } = 60000;
        }
        
        // ============================================
        // 💕 互动系统配置
        // ============================================
        
        public static class Interaction
        {
            /// <summary>悬停激活触摸模式时间（秒）</summary>
            public static float HoverActivationTime { get; set; } = 1.0f;
            
            /// <summary>触摸冷却时间（秒）</summary>
            public static float TouchCooldown { get; set; } = 0.3f;
            
            /// <summary>头部摸摸阈值（像素移动距离）</summary>
            public static float HeadRubThreshold { get; set; } = 60f;
            
            /// <summary>头部摸摸进度衰减速度</summary>
            public static float HeadRubDecayRate { get; set; } = 20f;
            
            /// <summary>头部摸摸冷却时间（秒）</summary>
            public static float HeadPatCooldown { get; set; } = 3.0f;
            
            /// <summary>高好感度阈值</summary>
            public static float HighAffinityThreshold { get; set; } = 60f;
            
            /// <summary>低好感度阈值</summary>
            public static float LowAffinityThreshold { get; set; } = -20f;
            
            /// <summary>头部摸摸好感度奖励</summary>
            public static float HeadPatAffinityBonus { get; set; } = 3f;
            
            /// <summary>身体戳戳好感度奖励</summary>
            public static float PokeAffinityBonus { get; set; } = 1f;
            
            /// <summary>连续触摸好感度奖励</summary>
            public static float TouchComboAffinityBonus { get; set; } = 5f;
        }
        
        // ============================================
        // 🎨 UI 颜色配置
        // ============================================
        
        public static class Colors
        {
            /// <summary>高好感度文字颜色</summary>
            public static Color HighAffinityTextColor { get; set; } = new(1f, 0.7f, 0.8f);
            
            /// <summary>中等好感度文字颜色</summary>
            public static Color NeutralAffinityTextColor { get; set; } = new(0.8f, 0.9f, 1f);
            
            /// <summary>低好感度文字颜色</summary>
            public static Color LowAffinityTextColor { get; set; } = new(0.7f, 0.7f, 0.7f);
            
            /// <summary>占位符背景色</summary>
            public static Color PlaceholderBackground { get; set; } = new(0.2f, 0.2f, 0.25f, 1f);
            
            /// <summary>占位符边框色</summary>
            public static Color PlaceholderBorder { get; set; } = new(0.4f, 0.4f, 0.5f, 1f);
        }
        
        // ============================================
        // 🔧 扩展注册 API
        // ============================================
        
        /// <summary>
        /// 注册附属 Mod 的立绘搜索路径
        /// </summary>
        /// <param name="pathTemplate">路径模板，使用 {0} 作为 personaName 占位符</param>
        /// <param name="priority">优先级（0=最高，插入到列表开头）</param>
        public static void RegisterPortraitPath(string pathTemplate, bool highPriority = false)
        {
            if (string.IsNullOrEmpty(pathTemplate)) return;
            
            if (highPriority)
                AssetPaths.PortraitSearchPaths.Insert(0, pathTemplate);
            else
                AssetPaths.PortraitSearchPaths.Add(pathTemplate);
            
            if (Prefs.DevMode)
                Log.Message($"[TSSFrameworkConfig] Registered portrait path: {pathTemplate}");
        }
        
        /// <summary>
        /// 注册附属 Mod 的降临资源路径
        /// </summary>
        public static void RegisterDescentAssetPath(string pathTemplate, bool highPriority = false)
        {
            if (string.IsNullOrEmpty(pathTemplate)) return;
            
            if (highPriority)
                AssetPaths.DescentAssetPaths.Insert(0, pathTemplate);
            else
                AssetPaths.DescentAssetPaths.Add(pathTemplate);
            
            if (Prefs.DevMode)
                Log.Message($"[TSSFrameworkConfig] Registered descent asset path: {pathTemplate}");
        }
        
        /// <summary>
        /// 添加需禁用分层立绘的叙事者
        /// </summary>
        public static void AddVanillaStoryteller(string defName)
        {
            if (string.IsNullOrEmpty(defName)) return;
            Persona.VanillaStorytellers.Add(defName);
        }
        
        /// <summary>
        /// 移除需禁用分层立绘的叙事者（允许为其启用分层立绘）
        /// </summary>
        public static void RemoveVanillaStoryteller(string defName)
        {
            if (string.IsNullOrEmpty(defName)) return;
            Persona.VanillaStorytellers.Remove(defName);
        }
    }
}