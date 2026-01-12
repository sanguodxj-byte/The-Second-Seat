using UnityEngine;
using Verse;

namespace TheSecondSeat.Sideria
{
    /// <summary>
    /// 隐形渲染 Worker - 用于隐藏标准节点（Head, Hair, Beard, Apparel 等）
    /// 在 RimWorld 1.6 中，删除这些节点会导致硬编码检查崩溃（如 Gene_Blink 寻找 EyeLeft/EyeRight）。
    /// 因此我们保留完整的节点层级结构，但在渲染阶段强制隐藏它们。
    ///
    /// 关键点：
    /// - 必须保留 Human 的完整节点结构（Head -> Eyes, Hair, Beard 等）
    /// - CanDrawNow 返回 false 跳过渲染
    /// - GetGraphic 返回 null 作为双重保险
    /// </summary>
    public class PawnRenderNodeWorker_Invisible : PawnRenderNodeWorker
    {
        /// <summary>
        /// 核心：禁止绘制。永远返回 false，跳过渲染。
        /// </summary>
        public override bool CanDrawNow(PawnRenderNode node, PawnDrawParms parms)
        {
            return false;
        }

        /// <summary>
        /// 双重保险：如果代码强行获取贴图，返回 null。
        /// 使用 protected override 匹配基类签名。
        /// </summary>
        protected override Graphic GetGraphic(PawnRenderNode node, PawnDrawParms parms)
        {
            return null;
        }
    }
}