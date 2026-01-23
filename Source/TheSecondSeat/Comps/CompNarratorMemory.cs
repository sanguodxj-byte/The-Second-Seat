using System.Collections.Generic;
using Verse;

namespace TheSecondSeat.Comps
{
    /// <summary>
    /// ⭐ 挂载在 Shadow Pawn 上的记忆组件
    /// 利用 RimWorld 原生存档系统保存叙事者的私有数据
    /// </summary>
    public class CompNarratorMemory : ThingComp
    {
        // 这里可以存储任何你想随 Pawn 保存的数据
        // 例如：对特定事件的看法、累积的好感度历史（如果 StorytellerAgent 不够用）
        
        public override void PostExposeData()
        {
            base.PostExposeData();
            // Scribe_Values.Look...
        }
    }
    
    // 对应的 Def 类 (虽然主要通过代码动态管理，但为了规范可以保留)
    public class CompProperties_NarratorMemory : CompProperties
    {
        public CompProperties_NarratorMemory()
        {
            this.compClass = typeof(CompNarratorMemory);
        }
    }
}