using Verse;

namespace TheSecondSeat.PersonaGeneration
{
    public class EventPreferencesDef : IExposable
    {
        public float positiveEventBias = 0f;     // -1.0 ~ 1.0
        public float negativeEventBias = 0f;
        public float chaosLevel = 0f;            // 0 ~ 1.0
        public float interventionFrequency = 0.5f; // 0 ~ 1.0

        public EventPreferencesDef()
        {
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref positiveEventBias, "positiveEventBias", 0f);
            Scribe_Values.Look(ref negativeEventBias, "negativeEventBias", 0f);
            Scribe_Values.Look(ref chaosLevel, "chaosLevel", 0f);
            Scribe_Values.Look(ref interventionFrequency, "interventionFrequency", 0.5f);
        }
    }
}
