using System.Collections.Generic;
using RimWorld;
using Verse;

namespace TheSecondSeat
{
    /// <summary>
    /// DefModExtension for abilities that grant divine shields (absorbs damage until depleted).
    /// Attached to HediffDef to mark it as a divine shield hediff.
    /// </summary>
    public class DefModExtension_DivineShield : DefModExtension
    {
        public int maxStacks = 1;
        public float shieldPerStack = 100f;
        public bool absorbAllDamage = false;
        public bool removeOnDeplete = true;
    }
    
    /// <summary>
    /// DefModExtension for marking HediffDefs as "Divine Body" types that grant damage immunity.
    /// Pawns with hediffs marked with this extension will be immune to non-combat damage.
    ///
    /// Usage in XML (on HediffDef):
    /// <modExtensions>
    ///   <li Class="TheSecondSeat.DefModExtension_DivineBody">
    ///     <allowedDamageCategories>
    ///       <li>Sharp</li>
    ///       <li>Blunt</li>
    ///     </allowedDamageCategories>
    ///     <allowWeaponDamage>true</allowWeaponDamage>
    ///     <allowMedicalDamage>true</allowMedicalDamage>
    ///     <allowPawnInstigatorDamage>true</allowPawnInstigatorDamage>
    ///   </li>
    /// </modExtensions>
    /// </summary>
    public class DefModExtension_DivineBody : DefModExtension
    {
        /// <summary>
        /// List of DamageArmorCategory defNames that bypass the shield.
        /// Default allows Sharp and Blunt (melee/ranged combat damage).
        /// </summary>
        public List<string> allowedDamageCategories = new List<string> { "Sharp", "Blunt" };

        /// <summary>
        /// If true, damage from weapons (Weapon != null) always bypasses the shield.
        /// </summary>
        public bool allowWeaponDamage = true;

        /// <summary>
        /// If true, surgical and execution damage always bypasses the shield.
        /// </summary>
        public bool allowMedicalDamage = true;

        /// <summary>
        /// If true, damage from other pawns (unarmed combat) bypasses the shield.
        /// </summary>
        public bool allowPawnInstigatorDamage = true;
    }

    /// <summary>
    /// DefModExtension for summoned creatures.
    /// Attached to ThingDef or PawnKindDef.
    /// </summary>
    public class DefModExtension_SummonedCreature : DefModExtension
    {
        public bool isSpiritDragon = false;
        public string dissipationHediffDefName;
        public int lifetimeTicks = 2500; // Default ~1 minute
        
        private HediffDef cachedDissipationHediff;
        public HediffDef DissipationHediff
        {
            get
            {
                if (cachedDissipationHediff == null && !string.IsNullOrEmpty(dissipationHediffDefName))
                {
                    cachedDissipationHediff = DefDatabase<HediffDef>.GetNamed(dissipationHediffDefName, false);
                }
                return cachedDissipationHediff;
            }
        }
    }

    /// <summary>
    /// DefModExtension for grab/hold jobs.
    /// Attached to JobDef to specify which HediffDef to use for grab status.
    /// - grabbedHediffDefName: HediffDef applied to the target (disables movement)
    /// - holdingHediffDefName: HediffDef applied to the caster (provides throw/slam Gizmo buttons)
    /// </summary>
    public class DefModExtension_GrabJob : DefModExtension
    {
        /// <summary>
        /// HediffDef name to apply to the grabbed TARGET (disables movement).
        /// </summary>
        public string grabbedHediffDefName;
        
        /// <summary>
        /// HediffDef name to apply to the CASTER (provides throw/slam Gizmo buttons).
        /// Should be a Hediff_CalamityHolding with HediffComp_CalamityHoldActions.
        /// </summary>
        public string holdingHediffDefName;
        
        private HediffDef cachedGrabbedHediff;
        public HediffDef GrabbedHediff
        {
            get
            {
                if (cachedGrabbedHediff == null && !string.IsNullOrEmpty(grabbedHediffDefName))
                {
                    cachedGrabbedHediff = DefDatabase<HediffDef>.GetNamed(grabbedHediffDefName, false);
                }
                return cachedGrabbedHediff;
            }
        }
        
        private HediffDef cachedHoldingHediff;
        public HediffDef HoldingHediff
        {
            get
            {
                if (cachedHoldingHediff == null && !string.IsNullOrEmpty(holdingHediffDefName))
                {
                    cachedHoldingHediff = DefDatabase<HediffDef>.GetNamed(holdingHediffDefName, false);
                }
                return cachedHoldingHediff;
            }
        }
    }
    
    /// <summary>
    /// DefModExtension for marking hediffs as crimson bloom marks.
    /// Attached to HediffDef.
    /// </summary>
    public class DefModExtension_CrimsonMark : DefModExtension
    {
        public int maxStacks = 5;
        public float damagePerStack = 20f;
        public bool explodeOnMaxStacks = true;
        public float explosionRadius = 3f;
    }
}
