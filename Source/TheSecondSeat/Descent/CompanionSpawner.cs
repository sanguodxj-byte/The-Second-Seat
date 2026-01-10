using System;
using Verse;
using RimWorld;

namespace TheSecondSeat.Descent
{
    /// <summary>
    /// 伴随生物召唤器 - 负责龙等召唤物的生成
    /// </summary>
    public static class CompanionSpawner
    {
        /// <summary>
        /// 生成召唤物
        /// </summary>
        public static Pawn SpawnSummon(
            PawnKindDef pawnKind, 
            IntVec3 position, 
            Map map, 
            Faction faction, 
            Pawn currentCompanion,
            Pawn masterPawn,
            out Pawn newCompanion)
        {
            newCompanion = null;
            
            if (pawnKind == null || map == null || !position.IsValid) 
            {
                Log.Warning("[CompanionSpawner] Invalid spawn parameters");
                return null;
            }

            // 1. 如果已存在伴随生物，先清理旧的（保持唯一性）
            if (currentCompanion != null && !currentCompanion.Destroyed)
            {
                FleckMaker.ThrowSmoke(currentCompanion.DrawPos, currentCompanion.Map, 1.5f);
                currentCompanion.Destroy(DestroyMode.Vanish);
            }

            // 2. 生成新生物
            Pawn pawn;
            try
            {
                pawn = PawnGenerator.GeneratePawn(pawnKind, faction);
            }
            catch (Exception ex)
            {
                Log.Error($"[CompanionSpawner] Failed to generate pawn: {ex}");
                return null;
            }
            
            // 3. 确保 Animal 智力的生物拥有技能 Tracker
            if (pawn.abilities == null)
            {
                pawn.abilities = new Pawn_AbilityTracker(pawn);
            }

            GenSpawn.Spawn(pawn, position, map, WipeMode.Vanish);

            // 4. 赋予特定技能
            GrantDragonAbilities(pawn, pawnKind);

            // 5. 播放召唤特效
            FleckMaker.ThrowSmoke(position.ToVector3(), map, 2.0f);
            FleckMaker.ThrowLightningGlow(position.ToVector3(), map, 1.5f);
            
            // 6. 设置驯服状态
            SetupTaming(pawn, faction, masterPawn);

            newCompanion = pawn;
            Log.Message($"[CompanionSpawner] Spawned: {pawn.Name} (Kind: {pawnKind.defName})");
            return pawn;
        }

        /// <summary>
        /// 赋予龙族特定技能
        /// </summary>
        private static void GrantDragonAbilities(Pawn pawn, PawnKindDef pawnKind)
        {
            if (pawn.abilities == null) return;

            AbilityDef ability = null;
            
            if (pawnKind.defName == "Sideria_BloodThornDragon")
            {
                ability = DefDatabase<AbilityDef>.GetNamedSilentFail("Sideria_Skill_DestructiveRend");
            }
            else if (pawnKind.defName == "Sideria_RadiantDragon")
            {
                ability = DefDatabase<AbilityDef>.GetNamedSilentFail("Sideria_Skill_PunishingStrike");
            }
            
            if (ability != null)
            {
                pawn.abilities.GainAbility(ability);
                Log.Message($"[CompanionSpawner] Granted ability: {ability.defName}");
            }
        }

        /// <summary>
        /// 设置驯服状态
        /// </summary>
        private static void SetupTaming(Pawn pawn, Faction faction, Pawn masterPawn)
        {
            if (pawn.training == null || faction != Faction.OfPlayer) return;

            pawn.training.SetWantedRecursive(TrainableDefOf.Tameness, true);
            
            if (masterPawn != null && !masterPawn.Destroyed)
            {
                pawn.training.Train(TrainableDefOf.Tameness, masterPawn, true);
            }
        }

        /// <summary>
        /// 销毁伴随生物
        /// </summary>
        public static void DestroyCompanion(Pawn companion)
        {
            if (companion != null && companion.Spawned && !companion.Destroyed)
            {
                FleckMaker.ThrowSmoke(companion.DrawPos, companion.Map, 1.5f);
                companion.Destroy(DestroyMode.Vanish);
            }
        }
    }
}