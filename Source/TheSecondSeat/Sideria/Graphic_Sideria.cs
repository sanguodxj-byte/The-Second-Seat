using UnityEngine;
using Verse;

namespace TheSecondSeat.Sideria
{
    public class Graphic_Sideria : Graphic_Multi
    {
        public override Material MatSingle
        {
            get
            {
                // South is default
                return this.MatSouth;
            }
        }

        public override Material MatAt(Rot4 rot, Thing thing = null)
        {
            switch (rot.AsInt)
            {
                case 0: // North
                    return this.MatNorth;
                case 1: // East
                    return this.MatEast;
                case 2: // South
                    return this.MatSouth;
                case 3: // West
                    return this.MatEast; // Use East texture and let the engine flip it
                default:
                    return this.MatSouth;
            }
        }
    }
}