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
        /// ⭐ v3.2.0: 准备并投放已有的 Shadow Pawn
        /// </summary>
        public static void PreparePawnForDescent(
            Pawn pawn,
            NarratorPersonaDef persona,
            bool isHostile,
            IntVec3 location,
            Map map,
            bool playerControlled = true)
        {
            if (pawn == null || persona == null || map == null) return;

            // 1. 确定并设置派系
            Faction faction = DetermineFaction(isHostile, playerControlled);
            if (pawn.Faction != faction)
            {
                pawn.SetFaction(faction);
            }

            // 2. 投放到地图
            if (pawn.Spawned)
            {
                // 如果已经在地图上（不应该发生，但防御性处理），瞬移
                pawn.Position = location;
                pawn.Notify_Teleported();
            }
            else
            {
                GenSpawn.Spawn(pawn, location, map);
            }

            // 3. 恢复状态（如果是从 World 回来的）
            // 确保没有被清除的临时 Hediff
            
            // 4. 确保能力组件存在
            EnsureAbilityTracker(pawn);

            // 5. 应用 Persona 设定 (名字等)
            ApplyPersonaToPawn(pawn, persona);

            // 6. 重新添加 Hediff (如果丢失)
            // 注意：不要重复添加永久性 Hediff，这里主要用于添加降临状态的 Hediff
            AddHediffs(pawn, persona);

            // 7. 赋予技能 (如果缺失)
            GrantAbilities(pawn, persona);

            // 8. 自动征召
            AutoDraft(pawn);

            Log.Message($"[DescentPawnSpawner] {pawn.Name} descended successfully");
        }

        /// <summary>
        /// 生成降临实体 (旧方法，现在用于首次创建或兼容)
        /// </summary>
        public static Pawn SpawnDescentPawn(
            NarratorPersonaDef persona, 
            bool isHostile, 
            IntVec3 location, 
            Map map,
            bool playerControlled = true)
        {
            // 此方法现在主要作为 fallback 或用于生成新的 Shadow Pawn
            
            PawnKindDef pawnKind = DefDatabase<PawnKindDef>.GetNamedSilentFail(persona.descentPawnKind);
            if (pawnKind == null)
            {
                Log.Error($"[DescentPawnSpawner] PawnKind not found: {persona.descentPawnKind}");
                return null;
            }

            Faction faction = DetermineFaction(isHostile, playerControlled);
            
            PawnGenerationRequest request = new PawnGenerationRequest(
                pawnKind,
                faction,
                forceGenerateNewPawn: true
            );
            
            try
            {
                Pawn pawn = PawnGenerator.GeneratePawn(request);
                if (pawn == null) return null;

                PreparePawnForDescent(pawn, persona, isHostile, location, map, playerControlled);
                return pawn;
            }
            catch (Exception ex)
            {
                Log.Error($"[DescentPawnSpawner] Failed to generate pawn: {ex}");
                return null;
            }
        }

        /// <summary>
        /// 确定派系
        /// ⭐ v2.9.8: 根据 isHostile 和 playerControlled 确定派系
        /// - 敌对模式：使用敌对派系（AncientsHostile）
        /// - 受控援助：使用玩家派系（可征召控制）
        /// - 自主援助：使用友好 NPC 派系（不受玩家控制，但会自动战斗）
        /// </summary>
        private static Faction DetermineFaction(bool isHostile, bool playerControlled)
        {
            if (isHostile)
            {
                // 敌对模式：使用敌对派系
                Faction hostileFaction = Faction.OfAncientsHostile
                    ?? Find.FactionManager.FirstFactionOfDef(FactionDefOf.AncientsHostile)
                    ?? Find.FactionManager.AllFactions.FirstOrDefault(f => f.HostileTo(Faction.OfPlayer));
                return hostileFaction;
            }
            
            if (playerControlled)
            {
                // 受控援助：使用玩家派系（可以征召）
                return Faction.OfPlayer;
            }
            else
            {
                // ⭐ 自主援助：使用帝国或其他友好 NPC 派系
                // 这样叙事者会自主行动保护殖民地，但不受玩家直接控制
                Faction allyFaction = Find.FactionManager.AllFactions
                    .FirstOrDefault(f => f != Faction.OfPlayer 
                                      && !f.HostileTo(Faction.OfPlayer) 
                                      && !f.IsPlayer
                                      && f.def.humanlikeFaction);
                
                if (allyFaction != null)
                {
                    Log.Message($"[DescentPawnSpawner] Using ally faction for autonomous assist: {allyFaction.Name}");
                    return allyFaction;
                }
                
                // 回退：如果没有友好派系，使用玩家派系
                Log.Warning("[DescentPawnSpawner] No ally faction found, falling back to player faction");
                return Faction.OfPlayer;
            }
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
                    // 避免重复添加
                    if (!pawn.abilities.abilities.Any(a => a.def == abilityDef))
                    {
                        pawn.abilities.GainAbility(abilityDef);
                        Log.Message($"[DescentPawnSpawner] Granted ability: {abilityDefName}");
                    }
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
                    // ⭐ 修复：避免重复添加 Hediff
                    if (!pawn.health.hediffSet.HasHediff(hediffDef))
                    {
                        Hediff hediff = HediffMaker.MakeHediff(hediffDef, pawn);
                        pawn.health.AddHediff(hediff);
                        Log.Message($"[DescentPawnSpawner] Added hediff: {hediffDefName}");
                    }
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
