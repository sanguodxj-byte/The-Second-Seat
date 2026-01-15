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
    /// 唤龙之门技能效果组件
    /// 召唤一只来自灵界的巨龙
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
        /// 召唤灵界巨龙
        /// </summary>
        private void SummonSpiritDragon(Pawn caster, IntVec3 position, Map map)
        {
            // 找到合适的生成位置
            IntVec3 spawnPos = position;
            if (!spawnPos.Standable(map))
            {
                spawnPos = CellFinder.RandomClosewalkCellNear(position, map, 5);
            }

            // 召唤特效 - 门户效果
            CreateGateEffect(spawnPos, map);

            // 从 CompProperties 配置的龙种类中随机选择
            List<PawnKindDef> dragonKinds = Props.DragonKindDefs;
            if (dragonKinds == null || dragonKinds.Count == 0)
            {
                Log.Error("[DragonGate] No dragon PawnKindDefs configured in CompProperties!");
                return;
            }

            PawnKindDef dragonKind = dragonKinds.RandomElement();
            string dragonName = dragonKind.label ?? dragonKind.defName;

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

            if (dragon == null)
            {
                Log.Error("[DragonGate] Failed to generate spirit dragon!");
                return;
            }

            // 设置灵龙属性（使用选择的龙名）
            dragon.Name = new NameSingle(dragonName, false);

            // 生成到地图
            GenSpawn.Spawn(dragon, spawnPos, map);

            // 1. 添加消散 Hediff（控制存在时间）
            HediffDef dissipationDef = Props.DissipationHediffDef;
            if (dissipationDef != null)
            {
                Hediff dissipation = HediffMaker.MakeHediff(dissipationDef, dragon);
                dissipation.Severity = 1f;
                dragon.health.AddHediff(dissipation);
            }

            // 2. 赋予配置的 Hediffs
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

            // 3. 赋予配置的技能
            if (Props.abilitiesToGrant != null)
            {
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

            // 让灵龙主动攻击敌人
            SetDragonToAttackEnemies(dragon, caster);

            // 显示召唤消息
            MoteMaker.ThrowText(dragon.DrawPos, map, "TSS_DragonGate_Summon".Translate(caster.LabelShort, dragonName), Color.cyan);
            Messages.Message("TSS_DragonGate_SummonMessage".Translate(caster.LabelShort, dragonName),
                dragon, MessageTypeDefOf.PositiveEvent);

            Log.Message($"[DragonGate] {caster.LabelShort} summoned {dragonName} at {spawnPos}");
        }

        /// <summary>
        /// 创建门户特效 - 类似折跃的视觉效果
        /// </summary>
        private void CreateGateEffect(IntVec3 position, Map map)
        {
            Vector3 drawPos = position.ToVector3ShiftedWithAltitude(AltitudeLayer.MoteOverhead);
            
            // 折跃入口闪光效果（多层叠加增强视觉）
            FleckMaker.Static(position, map, FleckDefOf.PsycastSkipFlashEntry, 4f);
            FleckMaker.Static(position, map, FleckDefOf.PsycastSkipFlashEntry, 2.5f);
            
            // 主要闪光效果
            FleckMaker.Static(position, map, FleckDefOf.PsycastAreaEffect, 5f);
            
            // 内外双层光环效果
            FleckMaker.Static(position, map, FleckDefOf.PsycastSkipInnerExit, 3f);
            FleckMaker.Static(position, map, FleckDefOf.PsycastSkipOuterRingExit, 5f);
            
            // 周围粒子 - 创建门户边缘效果
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

            // 烟雾和尘土效果
            FleckMaker.ThrowDustPuff(position, map, 4f);
            FleckMaker.ThrowSmoke(drawPos, map, 2f);
            
            // 雷电光效
            FleckMaker.ThrowLightningGlow(drawPos, map, 1.5f);
            
            // TODO: 添加音效播放
        }

        /// <summary>
        /// 设置灵龙主动攻击敌人
        /// </summary>
        private void SetDragonToAttackEnemies(Pawn dragon, Pawn caster)
        {
            // 1. 确保龙属于玩家派系（如果不是，强制设置）
            if (dragon.Faction != caster.Faction)
            {
                dragon.SetFaction(caster.Faction);
            }

            // 2. 赋予 DefendPoint Lord，使其守卫召唤者附近并攻击敌人
            // 使用 DefendPoint 可以让龙在召唤者附近活动并攻击进入范围的敌人
            LordJob_DefendPoint lordJob = new LordJob_DefendPoint(caster.Position);
            LordMaker.MakeNewLord(caster.Faction, lordJob, caster.Map, new List<Pawn> { dragon });
            
            // 3. 强制设置 MindState Duty，防止被其他 AI 覆盖
            if (dragon.mindState != null)
            {
                dragon.mindState.duty = new PawnDuty(DutyDefOf.Defend, caster.Position);
            }

            // 4. 如果龙支持训练，仍然训练攻击技能作为备用
            if (dragon.training != null)
            {
                TrainableDef release = TrainableDefOf.Release;
                if (dragon.training.CanBeTrained(release))
                {
                    dragon.training.Train(release, caster, true);
                }
            }
            
            Log.Message($"[DragonGate] Set {dragon.LabelShort} to SearchAndDestroy mode.");
        }

        public override bool CanApplyOn(LocalTargetInfo target, LocalTargetInfo dest)
        {
            if (!base.CanApplyOn(target, dest))
            {
                return false;
            }

            // 检查目标位置是否有效
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
            GenDraw.DrawRadiusRing(target.Cell, 3f);
        }
    }

    /// <summary>
    /// 唤龙之门技能属性
    /// 所有 Def 引用通过 defName 配置，子模组通过 XML 指定
    /// </summary>
    public class CompProperties_AbilityDragonGate : CompProperties_AbilityEffect
    {
        public int dragonDurationTicks = 15000; // 灵龙存在时间，约4分钟
        public float summonRadius = 5f;
        
        // 通过 defName 配置的龙种类（支持多种龙随机召唤）
        public List<string> dragonKindDefNames = new List<string>();
        
        // 消散 Hediff 的 defName
        public string dissipationHediffDefName;

        // 赋予龙的额外 Hediff 列表
        public List<string> hediffsToGrant = new List<string>();

        // 赋予龙的技能列表
        public List<string> abilitiesToGrant = new List<string>();

        // 缓存的 Def 引用
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
}
