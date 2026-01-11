using Verse;
using UnityEngine;
using RimWorld;

namespace TheSecondSeat.Sideria
{
    public class PawnRenderNodeWorker_SideriaBody : PawnRenderNodeWorker
    {
        protected override Graphic GetGraphic(PawnRenderNode node, PawnDrawParms parms)
        {
            if (parms.pawn == null)
            {
                return null;
            }

            // Define the base texture path directly
            string texPath = "Sideria/Narrators/Descent/Pawn/Sideria_Full";

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
            
            // This is a simplified example. A real implementation might need more robust caching.
            return GraphicDatabase.Get<Graphic_Single>(pathWithRotation, ShaderDatabase.Cutout, Vector2.one * 2.5f, Color.white);
        }
    }
}