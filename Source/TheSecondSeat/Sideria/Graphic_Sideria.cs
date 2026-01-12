using UnityEngine;
using Verse;

namespace TheSecondSeat.Sideria
{
    /// <summary>
    /// Custom Graphic class for Sideria Avatar Animal form.
    /// Implementation follows Koelime's Graphic_KoelimeAnimal pattern.
    /// </summary>
    public class Graphic_SideriaAnimal : Graphic_Multi
    {
        public override Mesh MeshAt(Rot4 rot)
        {
            Vector2 vector2 = this.drawSize;
            // 不再额外放大，使用与殖民者相同的尺寸
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
            return GraphicDatabase.Get<Graphic_SideriaAnimal>(this.path, newShader, this.drawSize, newColor, newColorTwo, this.data, this.maskPath);
        }
    }
}