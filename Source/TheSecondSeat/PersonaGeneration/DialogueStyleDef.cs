using Verse;

namespace TheSecondSeat.PersonaGeneration
{
    /// <summary>
    /// 对话风格定义 - 控制叙事者的说话方式
    /// API: 可在XML中配置所有字段
    /// </summary>
    public class DialogueStyleDef : IExposable
    {
        /// <summary>API: 正式程度 (0=随意, 1=正式)</summary>
        public float formalityLevel = 0.5f;
        
        /// <summary>API: 情感表达程度 (0=冷静, 1=热情)</summary>
        public float emotionalExpression = 0.5f;
        
        /// <summary>API: 详细程度 (0=简洁, 1=详细)</summary>
        public float verbosity = 0.5f;
        
        /// <summary>API: 幽默程度 (0=严肃, 1=幽默)</summary>
        public float humorLevel = 0.3f;
        
        /// <summary>API: 讽刺程度 (0=直白, 1=讽刺)</summary>
        public float sarcasmLevel = 0.2f;

        /// <summary>API: 是否使用表情符号（如 ~、?）</summary>
        public bool useEmoticons = false;
        
        /// <summary>API: 是否使用省略号（...）</summary>
        public bool useEllipsis = false;
        
        /// <summary>API: 是否使用感叹号（!）</summary>
        public bool useExclamation = true;

        public DialogueStyleDef()
        {
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref formalityLevel, "formalityLevel", 0.5f);
            Scribe_Values.Look(ref emotionalExpression, "emotionalExpression", 0.5f);
            Scribe_Values.Look(ref verbosity, "verbosity", 0.5f);
            Scribe_Values.Look(ref humorLevel, "humorLevel", 0.3f);
            Scribe_Values.Look(ref sarcasmLevel, "sarcasmLevel", 0.2f);
            Scribe_Values.Look(ref useEmoticons, "useEmoticons", false);
            Scribe_Values.Look(ref useEllipsis, "useEllipsis", false);
            Scribe_Values.Look(ref useExclamation, "useExclamation", true);
        }
    }
}
