using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace TheSecondSeat
{
    /// <summary>
    /// 唤龙之门技能属性
    /// 所有 Def 引用通过 defName 配置，子模组通过 XML 指定
    /// </summary>
    public class CompProperties_AbilityDragonGate : CompProperties_AbilityEffect
    {
        // === 数值配置 ===
        
        /// <summary>灵龙存在时间（Ticks）</summary>
        public int dragonDurationTicks = 15000;
        
        /// <summary>召唤半径</summary>
        public float summonRadius = 5f;
        
        /// <summary>守卫范围</summary>
        public float defendRadius = 15f;
        
        // === 通过 defName 配置的龙种类 ===
        public List<string> dragonKindDefNames = new List<string>();
        
        /// <summary>消散 Hediff 的 defName</summary>
        public string dissipationHediffDefName;

        /// <summary>赋予龙的额外 Hediff 列表</summary>
        public List<string> hediffsToGrant = new List<string>();

        /// <summary>赋予龙的技能列表</summary>
        public List<string> abilitiesToGrant = new List<string>();
        
        /// <summary>召唤特效 FleckDef 列表</summary>
        public List<string> summonFleckDefNames = new List<string> { "PsycastSkipFlashEntry", "PsycastAreaEffect" };

        // === 缓存的 Def 引用 ===
        private List<PawnKindDef> cachedDragonKindDefs;
        private HediffDef cachedDissipationHediffDef;

        public List<PawnKindDef> DragonKindDefs
        {
            get
            {
                if (cachedDragonKindDefs == null)
                {
                    cachedDragonKindDefs = new List<PawnKindDef>();
                    foreach (string defName in dragonKindDefNames)
                    {
                        PawnKindDef def = DefDatabase<PawnKindDef>.GetNamed(defName, false);
                        if (def != null)
                        {
                            cachedDragonKindDefs.Add(def);
                        }
                        else
                        {
                            Log.Error($"[DragonGate] PawnKindDef '{defName}' not found!");
                        }
                    }
                }
                return cachedDragonKindDefs;
            }
        }

        public HediffDef DissipationHediffDef
        {
            get
            {
                if (cachedDissipationHediffDef == null && !string.IsNullOrEmpty(dissipationHediffDefName))
                {
                    cachedDissipationHediffDef = DefDatabase<HediffDef>.GetNamed(dissipationHediffDefName, false);
                    if (cachedDissipationHediffDef == null)
                    {
                        Log.Error($"[DragonGate] HediffDef '{dissipationHediffDefName}' not found!");
                    }
                }
                return cachedDissipationHediffDef;
            }
        }

        public CompProperties_AbilityDragonGate()
        {
            compClass = typeof(CompAbilityEffect_DragonGate);
        }
    }

    /// <summary>
    /// 唤龙之门技能效果组件
    /// 召唤一只来自灵界的巨龙
    /// 方法已拆分为可维护的小型方法
    /// </summary>
    public class CompAbilityEffect_DragonGate : CompAbilityEffect
    {
        public new CompProperties_AbilityDragonGate Props => (CompProperties_AbilityDragonGate)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            Pawn caster = parent.pawn;
            IntVec3 targetPos = target.Cell;
            Map map = caster.Map;

            if (map == null)
            {
                return;
            }

            // 召唤灵龙
            SummonSpiritDragon(caster, targetPos, map);
        }

        /// <summary>
        /// 召唤灵界巨龙（主入口，拆分为多个辅助方法）
        /// </summary>
        private void SummonSpiritDragon(Pawn caster, IntVec3 position, Map map)
        {
            // 1. 找到合适的生成位置
            IntVec3 spawnPos = FindSpawnPosition(position, map);
            
            // 2. 播放召唤特效
            PlaySummonEffects(spawnPos, map);

            // 3. 选择龙种类
            PawnKindDef dragonKind = SelectDragonKind();
            if (dragonKind == null)
            {
                Log.Error("[DragonGate] No dragon PawnKindDefs configured in CompProperties!");
                return;
            }

            // 4. 生成龙 Pawn
            Pawn dragon = GenerateDragonPawn(dragonKind, caster, map);
            if (dragon == null)
            {
                Log.Error("[DragonGate] Failed to generate spirit dragon!");
                return;
            }

            // 5. 生成到地图
            GenSpawn.Spawn(dragon, spawnPos, map);

            // 6. 应用 Hediffs 和技能
            ApplyDragonHediffs(dragon);
            ApplyDragonAbilities(dragon);

            // 7. 设置 AI 行为（使用 LordJob 而非直接覆盖 Duty）
            SetupDragonAI(dragon, caster, map);

            // 8. 显示消息
            ShowSummonMessage(dragon, caster, dragonKind);
        }

        /// <summary>
        /// 找到合适的生成位置
        /// </summary>
        private IntVec3 FindSpawnPosition(IntVec3 position, Map map)
        {
            if (position.Standable(map))
            {
                return position;
            }
            return CellFinder.RandomClosewalkCellNear(position, map, 5);
        }

        /// <summary>
        /// 选择要召唤的龙种类
        /// </summary>
        private PawnKindDef SelectDragonKind()
        {
            List<PawnKindDef> dragonKinds = Props.DragonKindDefs;
            if (dragonKinds == null || dragonKinds.Count == 0)
            {
                return null;
            }
            return dragonKinds.RandomElement();
        }

        /// <summary>
        /// 生成龙 Pawn
        /// </summary>
        private Pawn GenerateDragonPawn(PawnKindDef dragonKind, Pawn caster, Map map)
        {
            PawnGenerationRequest request = new PawnGenerationRequest(
                kind: dragonKind,
                faction: caster.Faction,
                context: PawnGenerationContext.NonPlayer,
                tile: map.Tile,
                forceGenerateNewPawn: true,
                allowDead: false,
                allowDowned: false,
                canGeneratePawnRelations: false,
                mustBeCapableOfViolence: true,
                colonistRelationChanceFactor: 0f,
                forceAddFreeWarmLayerIfNeeded: false,
                allowGay: false,
                allowPregnant: false,
                allowFood: false,
                allowAddictions: false,
                inhabitant: false,
                certainlyBeenInCryptosleep: false,
                forceRedressWorldPawnIfFormerColonist: false,
                worldPawnFactionDoesntMatter: false,
                biocodeWeaponChance: 0f,
                biocodeApparelChance: 0f,
                extraPawnForExtraRelationChance: null,
                relationWithExtraPawnChanceFactor: 0f
            );

            Pawn dragon = PawnGenerator.GeneratePawn(request);
            
            if (dragon != null)
            {
                // 设置名字
                string dragonName = dragonKind.label ?? dragonKind.defName;
                dragon.Name = new NameSingle(dragonName, false);
            }
            
            return dragon;
        }

        /// <summary>
        /// 应用龙的 Hediffs
        /// </summary>
        private void ApplyDragonHediffs(Pawn dragon)
        {
            // 消散 Hediff（控制存在时间）
            HediffDef dissipationDef = Props.DissipationHediffDef;
            if (dissipationDef != null)
            {
                Hediff dissipation = HediffMaker.MakeHediff(dissipationDef, dragon);
                dissipation.Severity = 1f;
                dragon.health.AddHediff(dissipation);
            }

            // 额外 Hediffs
            if (Props.hediffsToGrant != null)
            {
                foreach (string hediffName in Props.hediffsToGrant)
                {
                    HediffDef hediffDef = DefDatabase<HediffDef>.GetNamedSilentFail(hediffName);
                    if (hediffDef != null)
                    {
                        dragon.health.AddHediff(hediffDef);
                    }
                }
            }
        }

        /// <summary>
        /// 应用龙的技能
        /// </summary>
        private void ApplyDragonAbilities(Pawn dragon)
        {
            if (Props.abilitiesToGrant == null || Props.abilitiesToGrant.Count == 0)
            {
                return;
            }

            if (dragon.abilities == null)
            {
                dragon.abilities = new Pawn_AbilityTracker(dragon);
            }
            
            foreach (string abilityName in Props.abilitiesToGrant)
            {
                AbilityDef abilityDef = DefDatabase<AbilityDef>.GetNamedSilentFail(abilityName);
                if (abilityDef != null)
                {
                    dragon.abilities.GainAbility(abilityDef);
                }
            }
        }

        /// <summary>
        /// 设置龙的 AI 行为
        /// 使用 LordJob 正确管理 AI，避免与其他 Mod 冲突
        /// </summary>
        private void SetupDragonAI(Pawn dragon, Pawn caster, Map map)
        {
            // 确保龙属于玩家派系
            if (dragon.Faction != caster.Faction)
            {
                dragon.SetFaction(caster.Faction);
            }

            // 使用 LordJob_DefendPoint 让龙守卫召唤点
            // 这是 RimWorld 原生的 AI 管理方式，与其他 Mod 兼容
            // ⭐ v1.7.0: 修复 LordJob_DefendPoint 构造函数参数 (1.5 变动)
            LordJob_DefendPoint lordJob = new LordJob_DefendPoint(caster.Position);
            
            LordMaker.MakeNewLord(caster.Faction, lordJob, map, new List<Pawn> { dragon });
            
            // 如果龙支持训练，训练攻击技能
            if (dragon.training != null)
            {
                TrainableDef release = TrainableDefOf.Release;
                if (dragon.training.CanBeTrained(release))
                {
                    dragon.training.Train(release, caster, true);
                }
            }
            
            Log.Message($"[DragonGate] Set {dragon.LabelShort} to defend mode at {caster.Position}.");
        }

        /// <summary>
        /// 播放召唤特效
        /// </summary>
        private void PlaySummonEffects(IntVec3 position, Map map)
        {
            Vector3 drawPos = position.ToVector3ShiftedWithAltitude(AltitudeLayer.MoteOverhead);
            
            // 使用配置的 FleckDef
            foreach (string fleckName in Props.summonFleckDefNames)
            {
                FleckDef fleckDef = DefDatabase<FleckDef>.GetNamedSilentFail(fleckName);
                if (fleckDef != null)
                {
                    FleckMaker.Static(position, map, fleckDef, 3f);
                }
            }
            
            // 默认的视觉效果
            FleckMaker.Static(position, map, FleckDefOf.PsycastSkipFlashEntry, 4f);
            FleckMaker.Static(position, map, FleckDefOf.PsycastAreaEffect, 5f);
            FleckMaker.Static(position, map, FleckDefOf.PsycastSkipInnerExit, 3f);
            FleckMaker.Static(position, map, FleckDefOf.PsycastSkipOuterRingExit, 5f);
            
            // 周围粒子效果
            PlayCircularParticles(position, map);
            
            // 烟雾和尘土
            FleckMaker.ThrowDustPuff(position, map, 4f);
            FleckMaker.ThrowSmoke(drawPos, map, 2f);
            FleckMaker.ThrowLightningGlow(drawPos, map, 1.5f);
        }

        /// <summary>
        /// 播放圆形粒子效果
        /// </summary>
        private void PlayCircularParticles(IntVec3 position, Map map)
        {
            for (int i = 0; i < 12; i++)
            {
                float angle = i * 30f;
                Vector3 offset = new Vector3(
                    Mathf.Cos(angle * Mathf.Deg2Rad) * 2.5f,
                    0,
                    Mathf.Sin(angle * Mathf.Deg2Rad) * 2.5f
                );
                IntVec3 effectPos = (position.ToVector3() + offset).ToIntVec3();
                if (effectPos.InBounds(map))
                {
                    FleckMaker.Static(effectPos, map, FleckDefOf.PsycastAreaEffect, 1.5f);
                    FleckMaker.ThrowLightningGlow(effectPos.ToVector3Shifted(), map, 0.8f);
                }
            }
        }

        /// <summary>
        /// 显示召唤消息
        /// </summary>
        private void ShowSummonMessage(Pawn dragon, Pawn caster, PawnKindDef dragonKind)
        {
            string dragonName = dragonKind.label ?? dragonKind.defName;
            
            MoteMaker.ThrowText(dragon.DrawPos, dragon.Map, 
                "TSS_DragonGate_Summon".Translate(caster.LabelShort, dragonName), Color.cyan);
            
            Messages.Message("TSS_DragonGate_SummonMessage".Translate(caster.LabelShort, dragonName),
                dragon, MessageTypeDefOf.PositiveEvent);

            Log.Message($"[DragonGate] {caster.LabelShort} summoned {dragonName} at {dragon.Position}");
        }

        public override bool CanApplyOn(LocalTargetInfo target, LocalTargetInfo dest)
        {
            if (!base.CanApplyOn(target, dest))
            {
                return false;
            }

            Pawn caster = parent.pawn;
            Map map = caster.Map;

            if (map == null)
            {
                return false;
            }

            IntVec3 cell = target.Cell;
            if (!cell.InBounds(map))
            {
                return false;
            }

            // 检查是否有足够空间
            if (!cell.Standable(map))
            {
                IntVec3 nearCell = CellFinder.RandomClosewalkCellNear(cell, map, 5);
                if (!nearCell.Standable(map))
                {
                    return false;
                }
            }

            return true;
        }

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            if (!base.Valid(target, throwMessages))
            {
                return false;
            }

            Pawn caster = parent.pawn;
            Map map = caster.Map;

            if (map == null)
            {
                if (throwMessages)
                {
                    Messages.Message("TSS_DragonGate_NotOnMap".Translate(), MessageTypeDefOf.RejectInput, false);
                }
                return false;
            }

            return true;
        }

        public override void DrawEffectPreview(LocalTargetInfo target)
        {
            // 绘制召唤范围预览
            GenDraw.DrawRadiusRing(target.Cell, Props.summonRadius);
        }
    }
}
