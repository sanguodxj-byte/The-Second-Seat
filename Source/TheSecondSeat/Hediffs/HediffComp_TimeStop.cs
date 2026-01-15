using TheSecondSeat.Components;
using Verse;

namespace TheSecondSeat.Hediffs
{
    public class HediffCompProperties_TimeStop : HediffCompProperties
    {
        public HediffCompProperties_TimeStop()
        {
            this.compClass = typeof(HediffComp_TimeStop);
        }
    }

    public class HediffComp_TimeStop : HediffComp
    {
        public HediffCompProperties_TimeStop Props => (HediffCompProperties_TimeStop)props;

        public override void CompPostMake()
        {
            base.CompPostMake();
            Register();
        }

        public override void CompPostPostAdd(DamageInfo? dinfo)
        {
            base.CompPostPostAdd(dinfo);
            Register();
        }

        public override void CompPostPostRemoved()
        {
            base.CompPostPostRemoved();
            Unregister();
        }
        
        // Ensure registration persists after load
        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);
            // Redundant check to ensure registration (e.g. if added via dev mode or weird loading state)
            if (Pawn.Spawned && !Pawn.Dead)
            {
                GameComponent_TimeStop.Instance?.RegisterCaster(Pawn);
            }
        }

        private void Register()
        {
            if (Pawn != null)
            {
                GameComponent_TimeStop.Instance?.RegisterCaster(Pawn);
            }
        }

        private void Unregister()
        {
            if (Pawn != null)
            {
                GameComponent_TimeStop.Instance?.UnregisterCaster(Pawn);
            }
        }
    }
}
