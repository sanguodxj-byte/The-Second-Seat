using Verse;

namespace TheSecondSeat.PersonaGeneration
{
    public class DialogueStyleDef : IExposable
    {
        public float formalityLevel = 0.5f;      // 0..1
        public float emotionalExpression = 0.5f; // 0..1
        public float verbosity = 0.5f;           // 0..1
        public float humorLevel = 0.3f;          // 0..1
        public float sarcasmLevel = 0.2f;        // 0..1

        // ? 补全缺失的属性
        public bool useEmoticons = false;        // 是否使用表情符号
        public bool useEllipsis = false;         // 是否使用省略号...
        public bool useExclamation = true;       // 是否使用感叹号！

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
