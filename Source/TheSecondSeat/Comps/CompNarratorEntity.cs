using Verse;
using RimWorld;

namespace TheSecondSeat
{
    public class CompProperties_NarratorEntity : CompProperties
    {
        public bool disableSlaughter = true;
        public bool disableRelease = true;
        public bool hideTrainingTab = true;
        public bool forceShowDraftGizmo = true;

        public CompProperties_NarratorEntity()
        {
            this.compClass = typeof(CompNarratorEntity);
        }
    }

    public class CompNarratorEntity : ThingComp
    {
        public CompProperties_NarratorEntity Props => (CompProperties_NarratorEntity)props;
    }
}