using Verse;

namespace TheSecondSeat.Sideria
{
    /// <summary>
    /// Custom PawnRenderNode for Sideria Avatar Animal form.
    /// Implementation follows Koelime's PawnRenderNode_Koelime pattern.
    /// </summary>
    public class PawnRenderNode_SideriaAnimal : PawnRenderNode_AnimalPart_Body
    {
        public PawnRenderNode_SideriaAnimal(Pawn pawn, PawnRenderNodeProperties props, PawnRenderTree tree) 
            : base(pawn, props, tree)
        {
        }

        public override GraphicMeshSet MeshSetFor(Pawn pawn)
        {
            Graphic graphic = this.GraphicFor(pawn);
            if (graphic != null)
            {
                return new GraphicMeshSet(
                    graphic.MeshAt(Rot4.North), 
                    graphic.MeshAt(Rot4.East), 
                    graphic.MeshAt(Rot4.South), 
                    graphic.MeshAt(Rot4.West)
                );
            }
            return null;
        }
    }
}