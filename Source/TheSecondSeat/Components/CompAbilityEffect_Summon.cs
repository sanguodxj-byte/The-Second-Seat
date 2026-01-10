using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;
using TheSecondSeat.Descent;

namespace TheSecondSeat.Components
{
    public class CompProperties_AbilitySummon : CompProperties_AbilityEffect
    {
        public PawnKindDef pawnKind;
        public int count = 1;
        public bool playerFaction = true;
        public new bool sendLetter = false;
        
        public CompProperties_AbilitySummon()
        {
            compClass = typeof(CompAbilityEffect_Summon);
        }
    }

    public class CompAbilityEffect_Summon : CompAbilityEffect
    {
        public new CompProperties_AbilitySummon Props => (CompProperties_AbilitySummon)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            
            Map map = parent.pawn.Map;
            IntVec3 spawnPos = target.Cell;

            if (!spawnPos.IsValid || !spawnPos.InBounds(map))
            {
                spawnPos = parent.pawn.Position;
            }

            // 寻找最近的可通行位置
            spawnPos = CellFinder.RandomClosewalkCellNear(spawnPos, map, 3, null);

            Faction faction = Props.playerFaction ? Faction.OfPlayer : null;

            for (int i = 0; i < Props.count; i++)
            {
                IntVec3 pos = spawnPos;
                if (i > 0)
                {
                    pos = CellFinder.RandomClosewalkCellNear(spawnPos, map, 2, null);
                }

                Pawn pawn = PawnGenerator.GeneratePawn(Props.pawnKind, faction);
                GenSpawn.Spawn(pawn, pos, map, WipeMode.Vanish);
            }

            if (Props.sendLetter)
            {
                Find.LetterStack.ReceiveLetter("Summoned", $"Summoned {Props.count} {Props.pawnKind.label}.", LetterDefOf.NeutralEvent, new TargetInfo(spawnPos, map));
            }
        }
    }
}
