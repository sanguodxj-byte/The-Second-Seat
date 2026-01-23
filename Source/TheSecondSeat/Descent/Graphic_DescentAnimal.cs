using UnityEngine;
using Verse;

namespace TheSecondSeat.Descent
{
    /// <summary>
    /// Custom Graphic class for Descent Entity Animal form.
    /// Generic implementation that can be used by any sub-mod's descent entity.
    /// Sub-mods reference this class via XML: graphicClass="TheSecondSeat.Descent.Graphic_DescentAnimal"
    /// </summary>
    public class Graphic_DescentAnimal : Graphic_Multi
    {
        public override Mesh MeshAt(Rot4 rot)
        {
            Vector2 vector2 = this.drawSize;
            // Use same size as colonists
            if (rot.IsHorizontal && !this.ShouldDrawRotated)
            {
                vector2 = vector2.Rotated();
            }
            return (rot == Rot4.West && this.WestFlipped) || (rot == Rot4.East && this.EastFlipped)
                ? MeshPool.GridPlaneFlip(vector2)
                : MeshPool.GridPlane(vector2);
        }

        public override Graphic GetColoredVersion(Shader newShader, Color newColor, Color newColorTwo)
        {
            return GraphicDatabase.Get<Graphic_DescentAnimal>(this.path, newShader, this.drawSize, newColor, newColorTwo, this.data, this.maskPath);
        }
    }
}