using System.Collections.Generic;
using System.Linq;
using Verse;
using RimWorld;

namespace TheSecondSeat.Hediffs
{
    /// <summary>
    /// 通用技能赋予组件 - 可以为任何 Pawn（包括动物）赋予技能
    /// 会自动初始化 abilities tracker（如果不存在）
    /// </summary>
    public class HediffCompProperties_GiveAbility : HediffCompProperties
    {
        /// <summary>
        /// 赋予的技能列表
        /// </summary>
        public List<AbilityDef> abilities;

        public HediffCompProperties_GiveAbility()
        {
            this.compClass = typeof(HediffComp_GiveAbility);
        }
    }

    /// <summary>
    /// 通用技能赋予 HediffComp
    /// 当 Hediff 添加时赋予技能，移除时收回技能
    /// 支持动物等默认没有 abilities tracker 的 Pawn
    /// </summary>
    public class HediffComp_GiveAbility : HediffComp
    {
        public HediffCompProperties_GiveAbility Props => (HediffCompProperties_GiveAbility)props;

        private List<AbilityDef> grantedAbilityDefs = new List<AbilityDef>();
        private bool abilitiesInitialized = false;

        public override void CompPostMake()
        {
            base.CompPostMake();
            TryGrantAbilities();
        }

        public override void CompPostPostAdd(DamageInfo? dinfo)
        {
            base.CompPostPostAdd(dinfo);
            TryGrantAbilities();
        }

        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);
            
            // 延迟初始化 - 某些情况下 Pawn 可能还没完全初始化
            if (!abilitiesInitialized && Pawn.Spawned)
            {
                TryGrantAbilities();
            }
        }

        public override void CompPostPostRemoved()
        {
            base.CompPostPostRemoved();
            RemoveAbilities();
        }

        /// <summary>
        /// 确保 Pawn 有 abilities tracker
        /// </summary>
        private bool EnsureAbilitiesTracker()
        {
            if (Pawn == null)
                return false;

            if (Pawn.abilities == null)
            {
                Pawn.abilities = new Pawn_AbilityTracker(Pawn);
                if (Prefs.DevMode)
                {
                    Log.Message($"[HediffComp_GiveAbility] Initialized abilities tracker for {Pawn.LabelShort}");
                }
            }
            return true;
        }

        private void TryGrantAbilities()
        {
            if (Props.abilities == null || Props.abilities.Count == 0)
            {
                abilitiesInitialized = true;
                return;
            }

            if (Pawn == null)
                return;

            if (!EnsureAbilitiesTracker())
                return;

            foreach (var abilityDef in Props.abilities)
            {
                if (abilityDef == null)
                    continue;

                // 检查是否已经有这个技能
                if (Pawn.abilities.AllAbilitiesForReading.Any(a => a.def == abilityDef))
                    continue;

                // 检查是否已经赋予过（避免重复赋予）
                if (grantedAbilityDefs.Contains(abilityDef))
                    continue;

                try
                {
                    Pawn.abilities.GainAbility(abilityDef);
                    grantedAbilityDefs.Add(abilityDef);

                    if (Prefs.DevMode)
                    {
                        Log.Message($"[HediffComp_GiveAbility] Granted ability '{abilityDef.defName}' to {Pawn.LabelShort}");
                    }
                }
                catch (System.Exception ex)
                {
                    Log.Warning($"[HediffComp_GiveAbility] Failed to grant ability '{abilityDef.defName}' to {Pawn.LabelShort}: {ex.Message}");
                }
            }

            abilitiesInitialized = true;
        }

        private void RemoveAbilities()
        {
            if (Pawn?.abilities == null)
                return;

            foreach (var abilityDef in grantedAbilityDefs)
            {
                if (abilityDef == null)
                    continue;

                try
                {
                    Pawn.abilities.RemoveAbility(abilityDef);
                    if (Prefs.DevMode)
                    {
                        Log.Message($"[HediffComp_GiveAbility] Removed ability '{abilityDef.defName}' from {Pawn.LabelShort}");
                    }
                }
                catch (System.Exception ex)
                {
                    Log.Warning($"[HediffComp_GiveAbility] Failed to remove ability '{abilityDef.defName}' from {Pawn.LabelShort}: {ex.Message}");
                }
            }
            grantedAbilityDefs.Clear();
        }

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Collections.Look(ref grantedAbilityDefs, "grantedAbilityDefs", LookMode.Def);
            Scribe_Values.Look(ref abilitiesInitialized, "abilitiesInitialized", false);
            
            if (grantedAbilityDefs == null)
            {
                grantedAbilityDefs = new List<AbilityDef>();
            }
        }
    }
}
