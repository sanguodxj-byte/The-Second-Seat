using System;
using Verse;
using RimWorld;

namespace TheSecondSeat.Components
{
    public class HediffCompProperties_Stacker : HediffCompProperties
    {
        public int maxStacks = 5;
        public float severityPerStack = 1.0f;
        public bool displayStackCount = true;
        
        // Optional: Trigger effect when max stacks reached
        // Options: "None", "Explode", "Heal", "Kill"
        public string onMaxStacks = "None";
        public float effectAmount = 0f; // Damage amount or Heal amount

        public HediffCompProperties_Stacker()
        {
            compClass = typeof(HediffComp_Stacker);
        }
    }

    public class HediffComp_Stacker : HediffComp
    {
        public HediffCompProperties_Stacker Props => (HediffCompProperties_Stacker)props;

        public int CurrentStacks
        {
            get
            {
                if (Props.severityPerStack <= 0) return 1;
                return (int)Math.Ceiling(parent.Severity / Props.severityPerStack);
            }
        }

        public override string CompLabelInBracketsExtra
        {
            get
            {
                if (Props.displayStackCount)
                {
                    return "x" + CurrentStacks;
                }
                return base.CompLabelInBracketsExtra;
            }
        }

        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);

            // Check Max Stacks Effect
            if (CurrentStacks >= Props.maxStacks)
            {
                HandleMaxStacks();
            }
        }

        private void HandleMaxStacks()
        {
            if (Props.onMaxStacks == "None") return;

            Pawn pawn = parent.pawn;
            if (pawn == null || !pawn.Spawned) return;

            switch (Props.onMaxStacks)
            {
                case "Explode":
                    GenExplosion.DoExplosion(pawn.Position, pawn.Map, 1.9f, DamageDefOf.Bomb, pawn, (int)Props.effectAmount);
                    parent.Severity = 0; // Reset stacks
                    break;
                case "Heal":
                    // Implement simple healing
                    break;
                case "Kill":
                    if (!pawn.Dead)
                    {
                        pawn.Kill(null, parent);
                    }
                    break;
            }
        }
    }
}
