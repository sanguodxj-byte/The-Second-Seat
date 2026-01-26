using System.Collections.Generic;
using Verse;
using TheSecondSeat.PersonaGeneration;

namespace TheSecondSeat.TTS
{
    /// <summary>
    /// 配置口型映射规则 (Lite Mapping)
    /// 允许在 XML 中定义字符分组到 VisemeCode 的映射
    /// </summary>
    public class LipSyncMappingDef : Def
    {
        /// <summary>
        /// 音素分组到 Viseme 的映射列表
        /// </summary>
        public List<GroupMapping> mappings = new List<GroupMapping>();

        /// <summary>
        /// 起势 (Attack) 使用的 Viseme
        /// </summary>
        public VisemeCode attackViseme = VisemeCode.Small;

        /// <summary>
        /// 收势 (Release) 使用的 Viseme
        /// </summary>
        public VisemeCode releaseViseme = VisemeCode.Small;

        /// <summary>
        /// 默认的回退 Viseme (当字符不在任何分组时使用)
        /// </summary>
        public VisemeCode defaultViseme = VisemeCode.Small;

        /// <summary>
        /// 核心口型保持的帧数 (Sustain Duration)
        /// </summary>
        public int sustainFrames = 2;

        /// <summary>
        /// 查找指定分组对应的 Viseme
        /// </summary>
        public VisemeCode GetVisemeFor(PhonemeGroup group)
        {
            if (mappings != null)
            {
                foreach (var mapping in mappings)
                {
                    if (mapping.group == group)
                    {
                        return mapping.viseme;
                    }
                }
            }
            return defaultViseme;
        }

        public class GroupMapping
        {
            public PhonemeGroup group;
            public VisemeCode viseme;
        }
    }
}