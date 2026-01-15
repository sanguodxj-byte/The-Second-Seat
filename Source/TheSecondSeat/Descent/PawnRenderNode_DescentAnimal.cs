using Verse;

namespace TheSecondSeat.Descent
{
    /// <summary>
    /// Custom PawnRenderNode for Descent Entity Animal form.
    /// Generic implementation that can be used by any sub-mod's descent entity.
    /// Sub-mods reference this class via XML: nodeClass="TheSecondSeat.Descent.PawnRenderNode_DescentAnimal"
    /// </summary>
    public class PawnRenderNode_DescentAnimal : PawnRenderNode_AnimalPart_Body
    {
        public PawnRenderNode_DescentAnimal(Pawn pawn, PawnRenderNodeProperties props, PawnRenderTree tree) 
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