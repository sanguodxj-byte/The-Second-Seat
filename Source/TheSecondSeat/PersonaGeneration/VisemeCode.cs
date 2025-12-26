using System;
using System.Collections.Generic;

namespace TheSecondSeat.PersonaGeneration
{
    /// <summary>
    /// ⭐ v1.6.74: 口型视素编码枚举（增强版 - 支持音素映射）
    /// 基于张嘴大小分类，兼容 Azure TTS、IPA、ARPABET、中文拼音
    /// </summary>
    public enum VisemeCode
    {
        /// <summary>闭嘴 (静音状态)</summary>
        Closed = 0,
        
        /// <summary>小嘴 (小张嘴，如 "i", "u")</summary>
        Small = 1,
        
        /// <summary>中等张嘴 (如 "e", "o")</summary>
        Medium = 2,
        
        /// <summary>大张嘴 (如 "a")</summary>
        Large = 3,
        
        /// <summary>咧嘴 (预留，用于笑声)</summary>
        Smile = 4,
        
        /// <summary>O型嘴 (预留，用于惊讶)</summary>
        OShape = 5
    }
    
    /// <summary>
    /// ⭐ v1.6.74: 视素辅助工具（增强版 - 支持音素到 Viseme 的映射）
    /// 支持：IPA 国际音标、ARPABET、中文拼音、Azure TTS Viseme ID
    /// </summary>
    public static class VisemeHelper
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 音素到 Viseme 的映射表
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// ⭐ v1.6.74: IPA 国际音标 → Viseme 映射
        /// </summary>
        private static readonly Dictionary<string, VisemeCode> IPAtoViseme = new Dictionary<string, VisemeCode>
        {
            // 闭嘴音（Bilabial stops, nasals, silence）
            { "p", VisemeCode.Closed },
            { "b", VisemeCode.Closed },
            { "m", VisemeCode.Closed },
            { "silence", VisemeCode.Closed },
            
            // 小嘴音（High front vowels, diphthongs）
            { "i", VisemeCode.Small },      // beat
            { "ɪ", VisemeCode.Small },      // bit
            { "e", VisemeCode.Small },      // bet
            { "ɛ", VisemeCode.Small },      // bet (alternate)
            { "eɪ", VisemeCode.Small },     // bait
            
            // 中嘴音（Mid-low vowels）
            { "æ", VisemeCode.Medium },     // bat
            { "ʌ", VisemeCode.Medium },     // but
            { "ə", VisemeCode.Medium },     // schwa (about)
            { "ɜ", VisemeCode.Medium },     // bird
            { "ɝ", VisemeCode.Medium },     // bird (r-colored)
            
            // 大嘴音（Low back vowels）
            { "ɑ", VisemeCode.Large },      // father
            { "ɔ", VisemeCode.Large },      // bought
            { "ɒ", VisemeCode.Large },      // lot (British)
            { "aɪ", VisemeCode.Large },     // bite
            { "aʊ", VisemeCode.Large },     // bout
            
            // O型嘴音（Rounded vowels, labialized consonants）
            { "o", VisemeCode.OShape },     // boat
            { "oʊ", VisemeCode.OShape },    // boat (diphthong)
            { "u", VisemeCode.OShape },     // boot
            { "ʊ", VisemeCode.OShape },     // book
            { "w", VisemeCode.OShape },     // wet
            { "hw", VisemeCode.OShape },    // wheat
            
            // 咧嘴音（Smiling sounds, dental/palatal）
            { "s", VisemeCode.Smile },
            { "z", VisemeCode.Smile },
            { "ʃ", VisemeCode.Smile },      // ship
            { "ʒ", VisemeCode.Smile },      // measure
            { "tʃ", VisemeCode.Smile },     // chip
            { "dʒ", VisemeCode.Smile },     // judge
            { "j", VisemeCode.Smile },      // yes
            
            // 其他辅音（默认闭嘴或小嘴）
            { "t", VisemeCode.Small },
            { "d", VisemeCode.Small },
            { "n", VisemeCode.Small },
            { "l", VisemeCode.Small },
            { "r", VisemeCode.Small },
            { "k", VisemeCode.Small },
            { "g", VisemeCode.Small },
            { "ŋ", VisemeCode.Closed },     // sing
            { "h", VisemeCode.Small },
            { "f", VisemeCode.Small },
            { "v", VisemeCode.Small },
            { "θ", VisemeCode.Small },      // think
            { "ð", VisemeCode.Small }       // that
        };
        
        /// <summary>
        /// ⭐ v1.6.74: ARPABET 音标 → Viseme 映射（Azure TTS 使用）
        /// </summary>
        private static readonly Dictionary<string, VisemeCode> ARPABETtoViseme = new Dictionary<string, VisemeCode>
        {
            // 闭嘴音
            { "P", VisemeCode.Closed },
            { "B", VisemeCode.Closed },
            { "M", VisemeCode.Closed },
            { "SIL", VisemeCode.Closed },   // silence
            
            // 小嘴音
            { "IY", VisemeCode.Small },     // beat
            { "IH", VisemeCode.Small },     // bit
            { "EH", VisemeCode.Small },     // bet
            { "EY", VisemeCode.Small },     // bait
            
            // 中嘴音
            { "AE", VisemeCode.Medium },    // bat
            { "AH", VisemeCode.Medium },    // but
            { "ER", VisemeCode.Medium },    // bird
            
            // 大嘴音
            { "AA", VisemeCode.Large },     // father
            { "AO", VisemeCode.Large },     // bought
            { "AY", VisemeCode.Large },     // bite
            { "AW", VisemeCode.Large },     // bout
            
            // O型嘴音
            { "OW", VisemeCode.OShape },    // boat
            { "UH", VisemeCode.OShape },    // book
            { "UW", VisemeCode.OShape },    // boot
            { "W", VisemeCode.OShape },
            
            // 咧嘴音
            { "S", VisemeCode.Smile },
            { "Z", VisemeCode.Smile },
            { "SH", VisemeCode.Smile },
            { "ZH", VisemeCode.Smile },
            { "CH", VisemeCode.Smile },
            { "JH", VisemeCode.Smile },
            { "Y", VisemeCode.Smile },
            
            // 其他辅音
            { "T", VisemeCode.Small },
            { "D", VisemeCode.Small },
            { "N", VisemeCode.Small },
            { "L", VisemeCode.Small },
            { "R", VisemeCode.Small },
            { "K", VisemeCode.Small },
            { "G", VisemeCode.Small },
            { "NG", VisemeCode.Closed },
            { "HH", VisemeCode.Small },
            { "F", VisemeCode.Small },
            { "V", VisemeCode.Small },
            { "TH", VisemeCode.Small },
            { "DH", VisemeCode.Small }
        };
        
        /// <summary>
        /// ⭐ v1.6.74: 中文拼音 → Viseme 映射
        /// </summary>
        private static readonly Dictionary<string, VisemeCode> PinyinToViseme = new Dictionary<string, VisemeCode>
        {
            // 闭嘴音
            { "b", VisemeCode.Closed },
            { "p", VisemeCode.Closed },
            { "m", VisemeCode.Closed },
            
            // 小嘴音（前高元音、齿音）
            { "i", VisemeCode.Small },
            { "yi", VisemeCode.Small },
            { "e", VisemeCode.Small },
            { "ye", VisemeCode.Small },
            { "ü", VisemeCode.Small },
            { "yu", VisemeCode.Small },
            
            // 中嘴音（央元音、前低元音）
            { "a", VisemeCode.Medium },
            { "ya", VisemeCode.Medium },
            { "ei", VisemeCode.Medium },
            { "en", VisemeCode.Medium },
            { "eng", VisemeCode.Medium },
            { "er", VisemeCode.Medium },
            
            // 大嘴音（后低元音）
            { "ai", VisemeCode.Large },
            { "ao", VisemeCode.Large },
            { "an", VisemeCode.Large },
            { "ang", VisemeCode.Large },
            
            // O型嘴音（圆唇元音）
            { "o", VisemeCode.OShape },
            { "ou", VisemeCode.OShape },
            { "ong", VisemeCode.OShape },
            { "u", VisemeCode.OShape },
            { "wu", VisemeCode.OShape },
            { "w", VisemeCode.OShape },
            
            // 咧嘴音（齿擦音、塞擦音）
            { "s", VisemeCode.Smile },
            { "z", VisemeCode.Smile },
            { "c", VisemeCode.Smile },
            { "si", VisemeCode.Smile },
            { "zi", VisemeCode.Smile },
            { "ci", VisemeCode.Smile },
            { "x", VisemeCode.Smile },
            { "q", VisemeCode.Smile },
            { "j", VisemeCode.Smile },
            { "sh", VisemeCode.Smile },
            { "zh", VisemeCode.Smile },
            { "ch", VisemeCode.Smile },
            
            // 其他辅音
            { "d", VisemeCode.Small },
            { "t", VisemeCode.Small },
            { "n", VisemeCode.Small },
            { "l", VisemeCode.Small },
            { "g", VisemeCode.Small },
            { "k", VisemeCode.Small },
            { "h", VisemeCode.Small },
            { "f", VisemeCode.Small },
            { "r", VisemeCode.Small }
        };
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 公共 API
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 从字符串解析 VisemeCode
        /// </summary>
        public static VisemeCode ParseViseme(string visemeStr)
        {
            if (string.IsNullOrEmpty(visemeStr))
                return VisemeCode.Closed;
            
            if (Enum.TryParse<VisemeCode>(visemeStr, true, out var result))
                return result;
            
            // 兼容旧版开合度（0.0-1.0）
            if (float.TryParse(visemeStr, out float openness))
            {
                return OpennessToViseme(openness);
            }
            
            return VisemeCode.Closed;
        }
        
        /// <summary>
        /// 开合度转视素编码
        /// </summary>
        public static VisemeCode OpennessToViseme(float openness)
        {
            if (openness < 0.2f) return VisemeCode.Closed;
            if (openness < 0.5f) return VisemeCode.Small;
            if (openness < 0.8f) return VisemeCode.Medium;
            return VisemeCode.Large;
        }
        
        /// <summary>
        /// ⭐ v1.6.74: 从 Viseme 编码转换为开合度（0-1）
        /// 用于平滑过渡动画
        /// </summary>
        public static float VisemeToOpenness(VisemeCode viseme)
        {
            return viseme switch
            {
                VisemeCode.Closed => 0.0f,
                VisemeCode.Small => 0.25f,
                VisemeCode.Medium => 0.5f,
                VisemeCode.Large => 0.75f,
                VisemeCode.Smile => 0.3f,   // 咧嘴微开
                VisemeCode.OShape => 0.6f,  // O型中开
                _ => 0.0f
            };
        }
        
        /// <summary>
        /// 获取对应的纹理名称
        /// </summary>
        public static string GetTextureName(VisemeCode viseme)
        {
            return viseme switch
            {
                VisemeCode.Closed => "closed_mouth",
                VisemeCode.Small => "small_mouth",
                VisemeCode.Medium => "medium_mouth",
                VisemeCode.Large => "larger_mouth",
                VisemeCode.Smile => "smile_mouth",
                VisemeCode.OShape => "o_mouth",
                _ => "closed_mouth"
            };
        }
        
        /// <summary>
        /// ⭐ v1.6.74: 获取 Viseme 对应的纹理名称（兼容 MouthAnimationSystem）
        /// </summary>
        public static string VisemeToTextureName(VisemeCode viseme)
        {
            return viseme switch
            {
                VisemeCode.Closed => null,              // 使用 base_body 默认嘴型
                VisemeCode.Small => "small_mouth",
                VisemeCode.Medium => "medium_mouth",
                VisemeCode.Large => "larger_mouth",
                VisemeCode.Smile => "smile_mouth",
                VisemeCode.OShape => "o_mouth",
                _ => null
            };
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // ⭐ v1.6.74: 音素解析 API
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// ⭐ v1.6.74: 从 IPA 音标获取 Viseme 编码
        /// </summary>
        public static VisemeCode GetVisemeFromIPA(string ipa)
        {
            if (string.IsNullOrEmpty(ipa)) return VisemeCode.Closed;
            
            if (IPAtoViseme.TryGetValue(ipa.ToLower(), out var viseme))
            {
                return viseme;
            }
            
            return VisemeCode.Closed;
        }
        
        /// <summary>
        /// ⭐ v1.6.74: 从 ARPABET 音标获取 Viseme 编码
        /// </summary>
        public static VisemeCode GetVisemeFromARPABET(string arpabet)
        {
            if (string.IsNullOrEmpty(arpabet)) return VisemeCode.Closed;
            
            // 移除数字后缀（如 AE1 → AE）
            string cleanArpabet = System.Text.RegularExpressions.Regex.Replace(arpabet, @"\d", "");
            
            if (ARPABETtoViseme.TryGetValue(cleanArpabet.ToUpper(), out var viseme))
            {
                return viseme;
            }
            
            return VisemeCode.Closed;
        }
        
        /// <summary>
        /// ⭐ v1.6.74: 从中文拼音获取 Viseme 编码
        /// </summary>
        public static VisemeCode GetVisemeFromPinyin(string pinyin)
        {
            if (string.IsNullOrEmpty(pinyin)) return VisemeCode.Closed;
            
            // 移除声调数字（如 ni3 → ni）
            string cleanPinyin = System.Text.RegularExpressions.Regex.Replace(pinyin, @"\d", "");
            
            if (PinyinToViseme.TryGetValue(cleanPinyin.ToLower(), out var viseme))
            {
                return viseme;
            }
            
            return VisemeCode.Closed;
        }
        
        /// <summary>
        /// ⭐ v1.6.74: 解析 Azure TTS Viseme 事件
        /// Azure TTS 可以返回实时 Viseme 序列（Viseme ID 基于 SAPI 标准）
        /// </summary>
        public static VisemeCode ParseAzureVisemeId(int visemeId)
        {
            return visemeId switch
            {
                0 => VisemeCode.Closed,    // Silence
                1 => VisemeCode.Medium,    // AE
                2 => VisemeCode.Large,     // AA
                3 => VisemeCode.Medium,    // AO
                4 => VisemeCode.Small,     // EH
                5 => VisemeCode.Medium,    // ER
                6 => VisemeCode.Small,     // IH
                7 => VisemeCode.Small,     // IY
                8 => VisemeCode.OShape,    // UW
                9 => VisemeCode.OShape,    // UH
                10 => VisemeCode.Medium,   // AH
                11 => VisemeCode.Small,    // EY
                12 => VisemeCode.Large,    // AY
                13 => VisemeCode.Large,    // AW
                14 => VisemeCode.OShape,   // OW
                15 => VisemeCode.OShape,   // OY
                16 => VisemeCode.OShape,   // W
                17 => VisemeCode.Smile,    // Y
                18 => VisemeCode.Small,    // R
                19 => VisemeCode.Small,    // L
                20 => VisemeCode.Smile,    // S
                21 => VisemeCode.Smile,    // Z
                _ => VisemeCode.Closed
            };
        }
    }
    
    /// <summary>
    /// ⭐ v1.6.66: 情绪枚举（与 ExpressionType 对应）
    /// </summary>
    public enum EmotionType
    {
        Neutral,    // 中性
        Happy,      // 开心
        Sad,        // 悲伤
        Angry,      // 愤怒
        Surprised,  // 惊讶
        Confused,   // 困惑
        Shy,        // 害羞
        Smug        // 得意
    }
}
