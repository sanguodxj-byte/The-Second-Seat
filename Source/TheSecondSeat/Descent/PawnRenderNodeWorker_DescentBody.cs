using Verse;
using UnityEngine;
using RimWorld;

namespace TheSecondSeat.Descent
{
    /// <summary>
    /// Generic PawnRenderNodeWorker for Descent Entity body rendering.
    /// Sub-mods can use this as a base or directly in XML: workerClass="TheSecondSeat.Descent.PawnRenderNodeWorker_DescentBody"
    /// 
    /// Note: This implementation uses a hardcoded texture path. Sub-mods should either:
    /// 1. Override this class for custom texture logic
    /// 2. Use PawnRenderNodeProperties.texPath in XML to specify textures
    /// 
    /// For more flexibility, consider using PawnRenderNodeWorker_DescentBodyConfigurable
    /// which reads texture path from PawnRenderNodeProperties.
    /// </summary>
    public class PawnRenderNodeWorker_DescentBody : PawnRenderNodeWorker
    {
        protected override Graphic GetGraphic(PawnRenderNode node, PawnDrawParms parms)
        {
            if (parms.pawn == null)
            {
                return null;
            }

            // Get texture path from node properties if available, otherwise use a fallback
            string texPath = node.Props?.texPath;
            if (string.IsNullOrEmpty(texPath))
            {
                // Fallback: try to use pawn's graphic data
                texPath = parms.pawn.Drawer?.renderer?.BodyGraphic?.path;
                if (string.IsNullOrEmpty(texPath))
                {
                    return null;
                }
            }

            // Determine texture path based on rotation
            string pathWithRotation;
            switch (parms.pawn.Rotation.AsInt)
            {
                case 0: // North
                    pathWithRotation = texPath + "_north";
                    break;
                case 1: // East
                    pathWithRotation = texPath + "_east";
                    break;
                case 2: // South
                    pathWithRotation = texPath + "_south";
                    break;
                case 3: // West
                    // For West, we use the East texture and the engine will flip it.
                    pathWithRotation = texPath + "_east";
                    break;
                default:
                    pathWithRotation = texPath + "_south";
                    break;
            }

            // Get draw size from node properties or use default
            Vector2 drawSize = node.Props?.drawSize ?? Vector2.one * 2.5f;
            
            return GraphicDatabase.Get<Graphic_Single>(pathWithRotation, ShaderDatabase.Cutout, drawSize, Color.white);
        }
    }

    /// <summary>
    /// Invisible rendering Worker - Used to hide standard nodes (Head, Hair, Beard, Apparel, etc.)
    /// In RimWorld 1.6, deleting these nodes causes hardcoded checks to crash (e.g., Gene_Blink looking for EyeLeft/EyeRight).
    /// So we keep the complete node hierarchy structure, but force-hide them at render time.
    ///
    /// Key points:
    /// - Must retain Human's complete node structure (Head -> Eyes, Hair, Beard, etc.)
    /// - CanDrawNow returns false to skip rendering
    /// - GetGraphic returns null as double insurance
    /// </summary>
    public class PawnRenderNodeWorker_Invisible : PawnRenderNodeWorker
    {
        /// <summary>
        /// Core: Disable drawing. Always returns false, skips rendering.
        /// </summary>
        public override bool CanDrawNow(PawnRenderNode node, PawnDrawParms parms)
        {
            return false;
        }

        /// <summary>
        /// Double insurance: If code forcibly gets texture, return null.
        /// Uses protected override to match base class signature.
        /// </summary>
        protected override Graphic GetGraphic(PawnRenderNode node, PawnDrawParms parms)
        {
            return null;
        }
    }
}