using Verse;
using RimWorld;

namespace TheSecondSeat
{
    public class CompProperties_EnsureHediff : CompProperties
    {
        public HediffDef hediffDef;

        public CompProperties_EnsureHediff()
        {
            this.compClass = typeof(CompEnsureHediff);
        }
    }

    public class CompEnsureHediff : ThingComp
    {
        public CompProperties_EnsureHediff Props => (CompProperties_EnsureHediff)this.props;
        private bool checkedOnce = false;

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            CheckAndAddHediff();
        }

        public override void CompTick()
        {
            base.CompTick();
            if (!checkedOnce)
            {
                CheckAndAddHediff();
                checkedOnce = true;
            }
        }

        private void CheckAndAddHediff()
        {
            if (parent is Pawn pawn && Props.hediffDef != null)
            {
                if (!pawn.health.hediffSet.HasHediff(Props.hediffDef))
                {
                    pawn.health.AddHediff(Props.hediffDef);
                    Log.Message($"[TheSecondSeat] Added hediff {Props.hediffDef.defName} to {pawn.LabelShort}");
                }
            }
        }
        
        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref checkedOnce, "checkedOnce", false);
        }
    }
}
