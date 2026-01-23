using System;
using Verse;

namespace TheSecondSeat
{
    /// <summary>
    /// 兼容性类：用于处理旧 XML 或错误配置中使用的 <TheSecondSeat.RenderTreeDef> 标签
    /// </summary>
    [Obsolete("Use TheSecondSeat.PersonaGeneration.RenderTreeDef instead")]
    public class RenderTreeDef : TheSecondSeat.PersonaGeneration.RenderTreeDef
    {
    }
}