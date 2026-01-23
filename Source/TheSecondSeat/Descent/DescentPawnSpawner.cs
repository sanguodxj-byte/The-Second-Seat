using System;
using System.Linq;
using Verse;
using Verse.AI;
using RimWorld;
using TheSecondSeat.PersonaGeneration;

namespace TheSecondSeat.Descent
{
    /// <summary>
    /// 降临实体生成器 - 负责 Pawn 生成和初始化
    /// </summary>
    public static class DescentPawnSpawner
    {
        /// <summary>
        /// 生成降临实体
        /// </summary>
        public static Pawn SpawnDescentPawn(
            NarratorPersonaDef persona, 
            bool isHostile, 
            IntVec3 location, 
            Map map)
        {
            if (persona == null || map == null || location == IntVec3.Invalid)
            {
                Log.Error("[DescentPawnSpawner] Invalid parameters for spawning");
                return null;
            }

            PawnKindDef pawnKind = DefDatabase<PawnKindDef>.GetNamedSilentFail(persona.descentPawnKind);
            if (pawnKind == null)
            {
                Log.Error($"[DescentPawnSpawner] PawnKind not found: {persona.descentPawnKind}");
                return null;
            }

            // 1. 确定派系
            Faction faction = DetermineFaction(isHostile);
            Log.Message($"[DescentPawnSpawner] Faction: {faction?.Name ?? "null"}, IsHostile: {isHostile}");

            // 2. 生成 Pawn
            Pawn pawn;
            try
            {
                // 使用最简化的 PawnGenerationRequest，避免复杂参数导致的问题
                PawnGenerationRequest request = new PawnGenerationRequest(
                    pawnKind,
                    faction,
                    forceGenerateNewPawn: true
                );
                
                Log.Message($"[DescentPawnSpawner] Creating pawn with PawnKind: {pawnKind.defName}, Race: {pawnKind.race?.defName ?? "null"}");
                
                pawn = PawnGenerator.GeneratePawn(request);
                
                if (pawn == null)
                {
                    Log.Error("[DescentPawnSpawner] PawnGenerator returned null");
                    return null;
                }
                
                Log.Message($"[DescentPawnSpawner] Generated: {pawnKind.defName}, Intelligence: {pawn.RaceProps.intelligence}");
            }
            catch (Exception ex)
            {
                Log.Error($"[DescentPawnSpawner] Failed to generate pawn: {ex.Message}");
                Log.Error($"[DescentPawnSpawner] Stack: {ex.StackTrace}");
                return null;
            }

            // 3. 应用叙事者设定
            ApplyPersonaToPawn(pawn, persona);

            // 4. 投放到地图
            GenSpawn.Spawn(pawn, location, map);

            // 5. 确保 Animal 智力的生物拥有 AbilityTracker (关键步骤)
            // 必须在赋予技能和添加 Hediff 之前完成
            EnsureAbilityTracker(pawn);

            // 6. 添加 Hediff (可能包含 HediffCompProperties_GiveAbility)
            AddHediffs(pawn, persona);

            // 7. 赋予额外技能 (NarratorPersonaDef 中指定的)
            GrantAbilities(pawn, persona);

            // 8. 自动征召
            AutoDraft(pawn);

            Log.Message($"[DescentPawnSpawner] {persona.narratorName} spawned successfully");
            return pawn;
        }

        /// <summary>
        /// 确定派系
        /// </summary>
        private static Faction DetermineFaction(bool isHostile)
        {
            Faction faction = isHostile ? Faction.OfAncientsHostile : Faction.OfPlayer;
            
            if (isHostile && faction == null)
            {
                faction = Find.FactionManager.FirstFactionOfDef(FactionDefOf.AncientsHostile)
                         ?? Find.FactionManager.AllFactions.FirstOrDefault(f => f.HostileTo(Faction.OfPlayer));
            }
            
            return faction;
        }

        /// <summary>
        /// 应用叙事者设定
        /// </summary>
        private static void ApplyPersonaToPawn(Pawn pawn, NarratorPersonaDef persona)
        {
            if (pawn == null || persona == null) return;
            
            pawn.Name = new NameTriple(persona.narratorName, persona.narratorName, persona.narratorName);
        }

        /// <summary>
        /// 确保 Pawn 拥有 AbilityTracker
        /// 动物智力的 Pawn 默认没有 abilities，需要手动创建才能使用技能
        /// </summary>
        private static void EnsureAbilityTracker(Pawn pawn)
        {
            if (pawn.abilities == null)
            {
                pawn.abilities = new Pawn_AbilityTracker(pawn);
                Log.Message($"[DescentPawnSpawner] Created Pawn_AbilityTracker for {pawn.LabelShort}");
            }
        }

        /// <summary>
        /// 赋予额外技能 (NarratorPersonaDef 中指定的)
        /// </summary>
        private static void GrantAbilities(Pawn pawn, NarratorPersonaDef persona)
        {
            if (persona.abilitiesToGrant.NullOrEmpty()) return;
            if (pawn.abilities == null) return; // 安全检查

            foreach (string abilityDefName in persona.abilitiesToGrant)
            {
                AbilityDef abilityDef = DefDatabase<AbilityDef>.GetNamedSilentFail(abilityDefName);
                if (abilityDef != null)
                {
                    pawn.abilities.GainAbility(abilityDef);
                    Log.Message($"[DescentPawnSpawner] Granted ability: {abilityDefName}");
                }
                else
                {
                    Log.Warning($"[DescentPawnSpawner] AbilityDef not found: {abilityDefName}");
                }
            }
        }

        /// <summary>
        /// 添加 Hediff
        /// </summary>
        private static void AddHediffs(Pawn pawn, NarratorPersonaDef persona)
        {
            if (persona.hediffsToGrant.NullOrEmpty()) return;

            foreach (string hediffDefName in persona.hediffsToGrant)
            {
                HediffDef hediffDef = DefDatabase<HediffDef>.GetNamedSilentFail(hediffDefName);
                if (hediffDef != null)
                {
                    Hediff hediff = HediffMaker.MakeHediff(hediffDef, pawn);
                    pawn.health.AddHediff(hediff);
                    Log.Message($"[DescentPawnSpawner] Added hediff: {hediffDefName}");
                }
                else
                {
                    Log.Warning($"[DescentPawnSpawner] HediffDef not found: {hediffDefName}");
                }
            }
        }

        /// <summary>
        /// 自动征召 - 使用原版 drafter 组件（通过 Pawn_SpawnSetup_Patch 注入）
        /// </summary>
        private static void AutoDraft(Pawn pawn)
        {
            if (pawn.Faction != Faction.OfPlayer) return;

            // 使用原版 drafter（Sideria 通过 Pawn_SpawnSetup_Patch 注入了 drafter 组件）
            if (pawn.drafter != null)
            {
                pawn.drafter.Drafted = true;
                Log.Message($"[DescentPawnSpawner] Auto-drafted {pawn.LabelShort} using native drafter");
            }
            else
            {
                Log.Warning($"[DescentPawnSpawner] Cannot auto-draft {pawn.LabelShort}: no drafter component");
            }
        }
    }
}
