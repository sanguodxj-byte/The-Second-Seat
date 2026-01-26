using System.Collections.Generic;
using TheSecondSeat.Storyteller;
using TheSecondSeat.CharacterCard; // ⭐ 引入 CharacterCard

namespace TheSecondSeat.PersonaGeneration.Scriban
{
    /// <summary>
    /// Scriban 模板的上下文数据模型
    /// 包含所有用于渲染 System Prompt 的数据
    /// </summary>
    public class PromptContext
    {
        // ⭐ v2.2.0: 角色状态卡 (聚合核心)
        public NarratorStateCard Card { get; set; }

        // 叙事者信息
        public NarratorInfo Narrator { get; set; }

        /// <summary>
        /// 计算属性：Agent.DialogueStyle 的习惯列表
        /// 供模板使用 {{ habits }}
        /// </summary>
        public List<string> Habits
        {
            get
            {
                var list = new List<string>();
                if (Agent?.DialogueStyle == null) return list;
                if (Agent.DialogueStyle.UseEmoticons) list.Add("Use emoticons");
                if (Agent.DialogueStyle.UseEllipsis) list.Add("Use ellipsis");
                if (Agent.DialogueStyle.UseExclamation) list.Add("Use exclamation marks");
                return list;
            }
        }
        
        // 代理/玩家关系信息
        public AgentInfo Agent { get; set; }
        
        // 游戏状态 (Wealth, Population, Threats)
        public GameInfo Game { get; set; }
        
        // 设置与元数据
        public MetaInfo Meta { get; set; }
        
        // 遗留兼容层：用于存放目前通过复杂 C# 逻辑生成的文本块
        // 在完全重构前，这些块仍由 C# 生成并传入
        public Dictionary<string, string> Snippets { get; set; } = new Dictionary<string, string>();

        // ⭐ v2.3.0: 视觉与文本分析专用数据
        public AnalysisInfo Analysis { get; set; }

        // ⭐ v2.5.0: 服装系统变量
        /// <summary>
        /// 可用服装列表（格式化字符串，用于提示词显示）
        /// 在模板中使用 {{ available_outfits }}
        /// </summary>
        public string AvailableOutfits { get; set; }

        /// <summary>
        /// 当前服装标签
        /// 在模板中使用 {{ current_outfit }}
        /// </summary>
        public string CurrentOutfit { get; set; }
    }

    public class AnalysisInfo
    {
        public List<string> SelectedTraits { get; set; }
        public string UserSupplement { get; set; }
        public string BiographyText { get; set; }
    }

    public class NarratorInfo
    {
        /// <summary>Persona 的 DefName (用于查找专属 Prompt 文件夹)</summary>
        public string DefName { get; set; }
        public string Name { get; set; }
        public string Label { get; set; }
        public string Biography { get; set; }
        public List<string> VisualTags { get; set; }
        public string DescentAnimation { get; set; }
        public string CustomPrompt { get; set; }
        
        // ⭐ v2.1.0: 新增人格相关属性
        /// <summary>人格标签（如：善良、病娇、傲娇）</summary>
        public List<string> PersonalityTags { get; set; }
        
        /// <summary>语气标签</summary>
        public List<string> ToneTags { get; set; }
        
        /// <summary>禁用词列表</summary>
        public List<string> ForbiddenWords { get; set; }
        
        /// <summary>特殊能力列表</summary>
        public List<string> SpecialAbilities { get; set; }
        
        /// <summary>仁慈度 (0.0-1.0): 0=残酷无情，1=仁慈宽容</summary>
        public float MercyLevel { get; set; }
        
        /// <summary>混乱度 (0.0-1.0): 0=逻辑严谨，1=随机混乱</summary>
        public float ChaosLevel { get; set; }
        
        /// <summary>强势度 (0.0-1.0): 0=顺从玩家，1=无视玩家意见</summary>
        public float DominanceLevel { get; set; }
    }
    
    public class AgentInfo
    {
        public float Affinity { get; set; }
        public string Mood { get; set; }
        public DialogueStyleInfo DialogueStyle { get; set; }
    }

    public class DialogueStyleInfo
    {
        public float Formality { get; set; }
        public float Emotional { get; set; }
        public float Verbosity { get; set; }
        public float Humor { get; set; }
        public float Sarcasm { get; set; }
        public bool UseEmoticons { get; set; }
        public bool UseEllipsis { get; set; }
        public bool UseExclamation { get; set; }
    }

    public class GameInfo
    {
        // 预留给未来扩展游戏状态信息
        public float Wealth { get; set; }
        public int ColonistCount { get; set; }
    }

    public class MetaInfo
    {
        public string DifficultyMode { get; set; } // "Assistant", "Opponent", "Engineer"
        public string LanguageInstruction { get; set; }
        public string ModSettingsPrompt { get; set; }
    }
}
