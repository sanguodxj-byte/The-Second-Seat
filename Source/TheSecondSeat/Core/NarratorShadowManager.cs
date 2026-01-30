using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;
using TheSecondSeat.PersonaGeneration;

namespace TheSecondSeat.Core
{
    /// <summary>
    /// ⭐ 影子实体管理器 (The Shadow Entity Manager)
    /// 负责维护一个全档唯一的"影子 Pawn"，作为叙事者在游戏世界的实体锚点。
    /// 解决了叙事者记忆无法存档和身份不连续的问题。
    /// </summary>
    public class NarratorShadowManager : WorldComponent
    {
        private Pawn shadowPawn;
        
        // 缓存当前激活的 Persona DefName，用于检测是否需要重建 Pawn
        private string activePersonaDefName;

        public Pawn ShadowPawn => shadowPawn;

        public NarratorShadowManager(World world) : base(world)
        {
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref shadowPawn, "shadowPawn");
            Scribe_Values.Look(ref activePersonaDefName, "activePersonaDefName");
        }

        /// <summary>
        /// 获取或创建影子 Pawn
        /// </summary>
        public Pawn GetOrCreateShadowPawn(NarratorPersonaDef personaDef)
        {
            if (personaDef == null) return null;

            // 如果当前 Pawn 存在且 Def 匹配，直接返回
            // 注意：如果玩家切换了叙事者人格，我们需要更新影子 Pawn 的名字/外观，但最好保留记忆（如果是同一个灵魂）
            // 这里假设不同 Persona Def 代表不同实体，需要切换 Pawn 或重置 Pawn
            // 为了简单起见，如果 DefName 改变，我们更新现有 Pawn 的 Def 引用（如果可能）或者只是更新名字
            
            if (shadowPawn != null && !shadowPawn.Destroyed)
            {
                // 更新元数据
                if (activePersonaDefName != personaDef.defName)
                {
                    UpdateShadowPawnIdentity(shadowPawn, personaDef);
                    activePersonaDefName = personaDef.defName;
                }
                
                // ⭐ 修复：检查并动态添加缺失的 CompNarratorMemory (针对旧存档)
                EnsureMemoryComp(shadowPawn);
                
                return shadowPawn;
            }

            // 创建新的影子 Pawn
            shadowPawn = CreateShadowPawn(personaDef);
            activePersonaDefName = personaDef.defName;
            
            return shadowPawn;
        }

        private Pawn CreateShadowPawn(NarratorPersonaDef personaDef)
        {
            // 使用自定义的 Narrator Shadow 种类
            PawnKindDef kindDef = DefDatabase<PawnKindDef>.GetNamed("TSS_NarratorShadowKind");
            
            // 生成请求
            PawnGenerationRequest request = new PawnGenerationRequest(
                kindDef,
                Faction.OfPlayer,
                PawnGenerationContext.NonPlayer,
                -1,
                forceGenerateNewPawn: true,
                allowDead: false,
                allowDowned: false,
                canGeneratePawnRelations: true,
                mustBeCapableOfViolence: false,
                colonistRelationChanceFactor: 1f,
                forceAddFreeWarmLayerIfNeeded: false,
                allowGay: true,
                allowFood: true,
                allowAddictions: false,
                inhabitant: false,
                certainlyBeenInCryptosleep: false,
                forceRedressWorldPawnIfFormerColonist: false,
                worldPawnFactionDoesntMatter: false,
                biocodeWeaponChance: 0f,
                relationWithExtraPawnChanceFactor: 1f
            );

            Pawn pawn = PawnGenerator.GeneratePawn(request);
            
            // 设置名字
            UpdateShadowPawnIdentity(pawn, personaDef);

            // 确保 Comp 存在
            EnsureMemoryComp(pawn);
            
            // 将 Pawn 放入 WorldPawns 保持其存活但不出现在地图上
            if (!pawn.IsWorldPawn())
            {
                Find.WorldPawns.PassToWorld(pawn, PawnDiscardDecideMode.KeepForever);
            }

            Log.Message($"[The Second Seat] Created Shadow Pawn for {personaDef.narratorName}: {pawn.Name}");
            
            return pawn;
        }

        private void UpdateShadowPawnIdentity(Pawn pawn, NarratorPersonaDef personaDef)
        {
            pawn.Name = new NameTriple("", personaDef.narratorName, "");
            // 这里可以进一步定制外观、发型等以匹配 Persona
        }
        
        /// <summary>
        /// 确保 Pawn 拥有 CompNarratorMemory 组件
        /// </summary>
        private void EnsureMemoryComp(Pawn pawn)
        {
            if (pawn == null) return;
            
            var comp = pawn.GetComp<TheSecondSeat.Comps.CompNarratorMemory>();
            if (comp == null)
            {
                comp = new TheSecondSeat.Comps.CompNarratorMemory();
                comp.parent = pawn;
                pawn.AllComps.Add(comp);
                Log.Warning($"[TSS] Dynamically added CompNarratorMemory to shadow pawn {pawn.Name}");
            }
        }
        
        // 静态访问器
        public static NarratorShadowManager Instance => Find.World?.GetComponent<NarratorShadowManager>();
    }
}