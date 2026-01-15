using UnityEngine;
using Verse;
using RimWorld;

namespace TheSecondSeat
{
    /// <summary>
    /// Sideria 的自定义渲染节点工作器
    /// 实现行走时的身体倾斜效果
    /// </summary>
    public class PawnRenderNodeWorker_SideriaBody : PawnRenderNodeWorker_AnimalBody
    {
        private const float WalkTiltAngle = 30f;

        public override Quaternion RotationFor(PawnRenderNode node, PawnDrawParms parms)
        {
            Quaternion baseRotation = base.RotationFor(node, parms);

            if (parms.pawn == null)
            {
                return baseRotation;
            }

            // 检查 pather 是否存在（尸体渲染时 pather 可能为 null）
            if (parms.pawn.pather == null)
            {
                return baseRotation;
            }

            // 检查是否在移动
            if (!parms.pawn.pather.Moving)
            {
                return baseRotation;
            }

            // 获取移动方向
            Rot4 facing = parms.facing;
            
            // 只在左右移动时倾斜
            // 在 RimWorld 中，物体平躺在 XZ 平面上，Y 轴垂直于平面（指向屏幕外/内）
            // 因此，绕 Y 轴旋转 (Vector3.up) 会让图片在屏幕平面内旋转
            
            if (facing == Rot4.East)
            {
                // 向右移动，顺时针倾斜（负角度）
                // 注意：RimWorld 的旋转方向可能与 Unity 标准相反，或者取决于相机
                // 通常负角度是顺时针
                return baseRotation * Quaternion.AngleAxis(WalkTiltAngle, Vector3.up);
            }
            else if (facing == Rot4.West)
            {
                // 向左移动，逆时针倾斜（正角度）
                return baseRotation * Quaternion.AngleAxis(-WalkTiltAngle, Vector3.up);
            }

            return baseRotation;
        }
    }
}
