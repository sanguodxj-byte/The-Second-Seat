using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;
using RimWorld;

namespace TheSecondSeat
{
    public class CompProperties_SideriaAutoAbility : CompProperties
    {
        public CompProperties_SideriaAutoAbility()
        {
            this.compClass = typeof(CompSideriaAutoAbility);
        }
    }

    public class CompSideriaAutoAbility : ThingComp
    {
        // 检查间隔：每 3 秒 (180 ticks)
        private const int CheckInterval = 180;

        public override void CompTick()
        {
            base.CompTick();
            
            Pawn pawn = parent as Pawn;
            if (pawn == null || !pawn.Spawned || pawn.Dead || pawn.Downed) return;

            // 仅在非玩家派系（访客/援助）时生效
            if (pawn.Faction == Faction.OfPlayer) return;

            // 频率控制
            if (!pawn.IsHashIntervalTick(CheckInterval)) return;

            TryUseAbilities(pawn);
        }

        private void TryUseAbilities(Pawn pawn)
        {
            if (pawn.abilities == null) return;

            // 寻找目标：当前瞄准的，或者 AI 意图攻击的
            Thing target = (pawn.TargetCurrentlyAimingAt.IsValid ? pawn.TargetCurrentlyAimingAt.Thing : null);
            if (target == null) target = pawn.mindState?.enemyTarget;
            
            // 如果没有当前目标，尝试寻找视野内最近的敌对 Pawn
            if (target == null && pawn.Map != null)
            {
                target = GenClosest.ClosestThingReachable(
                    pawn.Position,
                    pawn.Map,
                    ThingRequest.ForGroup(ThingRequestGroup.Pawn),
                    PathEndMode.Touch,
                    TraverseParms.For(pawn),
                    25f, // 搜索半径
                    t => t is Pawn p && p.HostileTo(pawn) && !p.Downed
                );
            }

            // 如果仍无目标，且不需要目标的技能（如召唤）可能仍可使用
            // 但为了简化，我们假设战斗状态下才使用技能

            if (target == null) return;

            // 遍历并尝试使用技能
            foreach (var ability in pawn.abilities.abilities)
            {
                if (!ability.CanCast) continue;

                // 1. 猩红绽放 Crimson Bloom
                if (ability.def.defName == "Sideria_CrimsonBloom")
                {
                    if (ability.verb.CanHitTarget(target))
                    {
                        ability.verb.TryStartCastOn(target);
                        return; // 每次只施放一个
                    }
                }
                // 2. 厄运投掷 Calamity Throw
                else if (ability.def.defName == "Sideria_CalamityThrow")
                {
                    if (ability.verb.CanHitTarget(target))
                    {
                        ability.verb.TryStartCastOn(target);
                        return;
                    }
                }
                // 3. 唤龙之门 Dragon Gate
                else if (ability.def.defName == "Sideria_DragonGate")
                {
                    // 限制场上龙的数量，避免过多
                    if (!HasTooManyDragons(pawn.Map))
                    {
                        // 寻找附近空地召唤
                        IntVec3 cell = CellFinder.RandomClosewalkCellNear(pawn.Position, pawn.Map, 3, null);
                        if (cell.IsValid)
                        {
                            // DragonGate 需要 LocalTargetInfo
                            ability.verb.TryStartCastOn(new LocalTargetInfo(cell));
                            return;
                        }
                    }
                }
            }
        }

        private bool HasTooManyDragons(Map map)
        {
            if (map == null) return true;
            // 统计当前地图上 Sideria 灵龙的数量
            // 假设 DefName 包含 "Sideria_SpiritDragon"
            int count = 0;
            foreach (Pawn p in map.mapPawns.AllPawnsSpawned)
            {
                if (p.def.defName.Contains("Sideria_SpiritDragon") && !p.Dead)
                {
                    count++;
                }
            }
            return count >= 2; // 限制最多同时存在 2 条龙，避免过于 OP
        }
    }
}
