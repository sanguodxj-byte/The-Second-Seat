using System.Collections.Generic;
using System.Linq;
using Verse;
using RimWorld;

namespace TheSecondSeat.Hediffs
{
    public class HediffCompProperties_DivineBody : HediffCompProperties
    {
        /// <summary>
        /// 神躯状态下赋予的技能列表
        /// </summary>
        public List<AbilityDef> abilityDefs;

        public HediffCompProperties_DivineBody()
        {
            this.compClass = typeof(HediffComp_DivineBody);
        }
    }

    public class HediffComp_DivineBody : HediffComp
    {
        public HediffCompProperties_DivineBody Props => (HediffCompProperties_DivineBody)props;

        private List<Ability> grantedAbilities = new List<Ability>();

        public override void CompPostMake()
        {
            base.CompPostMake();
            GrantAbilities();
        }

        public override void CompPostPostAdd(DamageInfo? dinfo)
        {
            base.CompPostPostAdd(dinfo);
            GrantAbilities();
        }

        public override void CompPostPostRemoved()
        {
            base.CompPostPostRemoved();
            RemoveAbilities();
        }

        private void GrantAbilities()
        {
            if (Props.abilityDefs == null || Pawn == null)
                return;

            // 如果 Pawn 没有 abilities tracker（例如动物），手动初始化一个
            if (Pawn.abilities == null)
            {
                Pawn.abilities = new Pawn_AbilityTracker(Pawn);
                if (Prefs.DevMode)
                {
                    Log.Message($"[HediffComp_DivineBody] Initialized abilities tracker for {Pawn.LabelShort}");
                }
            }

            foreach (var abilityDef in Props.abilityDefs)
            {
                if (abilityDef != null && !Pawn.abilities.AllAbilitiesForReading.Any(a => a.def == abilityDef))
                {
                    Pawn.abilities.GainAbility(abilityDef);
                    var ability = Pawn.abilities.AllAbilitiesForReading.Find(a => a.def == abilityDef);
                    if (ability != null)
                    {
                        grantedAbilities.Add(ability);
                        if (Prefs.DevMode)
                        {
                            Log.Message($"[HediffComp_DivineBody] Granted ability '{abilityDef.defName}' to {Pawn.LabelShort}");
                        }
                    }
                }
            }
        }

        private void RemoveAbilities()
        {
            if (Pawn?.abilities == null)
                return;

            foreach (var ability in grantedAbilities)
            {
                if (ability?.def != null)
                {
                    Pawn.abilities.RemoveAbility(ability.def);
                }
            }
            grantedAbilities.Clear();
        }

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Collections.Look(ref grantedAbilities, "grantedAbilities", LookMode.Reference);
            if (grantedAbilities == null)
            {
                grantedAbilities = new List<Ability>();
            }
        }
    }
}
