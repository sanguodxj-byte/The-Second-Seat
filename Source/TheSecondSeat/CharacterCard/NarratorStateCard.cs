using System.Collections.Generic;
using TheSecondSeat.PersonaGeneration; // 引用 VisionAnalysisResult

namespace TheSecondSeat.CharacterCard
{
    /// <summary>
    /// ⭐ 角色状态卡：聚合了所有动态特征的快照
    /// </summary>
    public class NarratorStateCard
    {
        // === 基础身份 ===
        public string Name { get; set; }
        public string Label { get; set; }
        public string Role { get; set; } // "Assistant", "Opponent"

        // === 生物几律 (BioRhythm) ===
        public BioState Bio { get; set; } = new BioState();

        // === 心理与社交 (Psycho-Social) ===
        public PsychoState Mind { get; set; } = new PsychoState();

        // === 视觉认知 (Visual Identity) ===
        public VisualState Appearance { get; set; } = new VisualState();

        // === 降临状态 (Descent State) ===
        public DescentState Descent { get; set; } = new DescentState();

        public class BioState
        {
            public string EnergyLevel { get; set; } // "Energetic", "Tired", "Exhausted"
            public string HungerLevel { get; set; } // "Full", "Hungry"
            public string TimeOfDay { get; set; }   // "Midnight", "Morning"
            public bool IsSleepy { get; set; }
        }

        public class PsychoState
        {
            public string CurrentEmotion { get; set; } // "Happy", "Annoyed"
            public string AffinityTier { get; set; }   // "Soulmate", "Partner", "Stranger"
            public float AffinityValue { get; set; }
            public List<string> ActiveTraits { get; set; } = new List<string>(); // 当前激活的性格标签
        }

        public class VisualState
        {
            public bool HasVisualContext { get; set; }
            public List<string> VisualTags { get; set; } = new List<string>(); // ["White Hair", "Maid Outfit"]
            public string Description { get; set; } // "穿着女仆装的银发少女..."
            public string DominantColor { get; set; } // 主要色调名称
            
            /// <summary>
            /// ⭐ 表情与心情的一致性检查结果
            /// </summary>
            public ConsistencyState Consistency { get; set; } = new ConsistencyState();
        }
        
        /// <summary>
        /// ⭐ 表情与心情一致性状态
        /// 用于智能掩盖策略：只在检测到不一致时才切换思考表情
        /// </summary>
        public class ConsistencyState
        {
            /// <summary>
            /// 当前表情是否与心情一致
            /// </summary>
            public bool IsConsistent { get; set; } = true;
            
            /// <summary>
            /// 不一致时的警告消息
            /// </summary>
            public string WarningMessage { get; set; } = "";
            
            /// <summary>
            /// 当前显示的表情类型
            /// </summary>
            public string CurrentExpression { get; set; } = "Neutral";
            
            /// <summary>
            /// 根据心情应该显示的表情类型
            /// </summary>
            public string ExpectedExpression { get; set; } = "Neutral";
            
            /// <summary>
            /// 不一致的严重程度 (0-1)
            /// 0 = 完全一致, 1 = 严重不一致
            /// </summary>
            public float SeverityLevel { get; set; } = 0f;
        }

        public class DescentState
        {
            /// <summary>是否正在降临过程中（动画播放中）</summary>
            public bool IsDescending { get; set; }
            
            /// <summary>是否处于降临状态（实体形态存在于世界中）</summary>
            public bool IsDescentActive { get; set; }
            
            /// <summary>降临冷却剩余时间</summary>
            public string CooldownRemaining { get; set; } = "Ready";
            
            /// <summary>
            /// 当前存在形态：
            /// - "Portrait": 立绘形态
            /// - "Physical": 实体形态
            /// </summary>
            public string CurrentForm { get; set; } = "Portrait";
            
            /// <summary>当前形态的详细描述</summary>
            public string FormDescription { get; set; } = "";
        }
    }
}
