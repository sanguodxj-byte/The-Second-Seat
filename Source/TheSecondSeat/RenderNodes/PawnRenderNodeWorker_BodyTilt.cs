using UnityEngine;
using Verse;
using RimWorld;

namespace TheSecondSeat
{
    /// <summary>
    /// Generic render node worker that tilts the body when moving.
    /// Replaces PawnRenderNodeWorker_SideriaBody.
    /// </summary>
    public class PawnRenderNodeWorker_BodyTilt : PawnRenderNodeWorker_AnimalBody
    {
        private const float WalkTiltAngle = 30f;

        public override Quaternion RotationFor(PawnRenderNode node, PawnDrawParms parms)
        {
            Quaternion baseRotation = base.RotationFor(node, parms);

            if (parms.pawn == null)
            {
                return baseRotation;
            }

            // Check if pather exists (corpses might have null pather)
            if (parms.pawn.pather == null)
            {
                return baseRotation;
            }

            // Check if moving
            if (!parms.pawn.pather.Moving)
            {
                return baseRotation;
            }

            // Get facing direction
            Rot4 facing = parms.facing;
            
            // Only tilt when moving East or West
            if (facing == Rot4.East)
            {
                // Moving right, tilt clockwise (negative angle)
                return baseRotation * Quaternion.AngleAxis(WalkTiltAngle, Vector3.up);
            }
            else if (facing == Rot4.West)
            {
                // Moving left, tilt counter-clockwise (positive angle)
                return baseRotation * Quaternion.AngleAxis(-WalkTiltAngle, Vector3.up);
            }

            return baseRotation;
        }
    }
}