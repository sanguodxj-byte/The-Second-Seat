using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.Sound; // ⭐ 添加 Sound 命名空间
using RimWorld;
using TheSecondSeat.Narrator;
using TheSecondSeat.PersonaGeneration;
using TheSecondSeat.Core; // ⭐ 添加

namespace TheSecondSeat.Descent
{
    /// <summary>
    /// ⭐ v1.6.63: 叙事者降临系统 - 完全通用化执行器
    /// 
    /// 核心原则：
    /// - 只读配置，不认人
    /// - 所有 Def 名称从 NarratorPersonaDef 读取
    /// - 禁止硬编码任何 DefName
    /// - 支持任意叙事者的降临配置
    /// 
    /// 使用：
    /// - NarratorDescentSystem.Instance.TriggerDescent(isHostile: false)
    /// </summary>
    public class NarratorDescentSystem : GameComponent
    {
        // ==================== 单例模式 ====================
        
        private static NarratorDescentSystem? instance;
        public static NarratorDescentSystem Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = Current.Game?.GetComponent<NarratorDescentSystem>();
                }
                return instance;
            }
        }
        
        // ==================== 状态字段 ====================
        
        private bool isDescending = false;
        private bool lastDescentWasHostile = false;
        private int lastDescentTick = 0;
        private const int DESCENT_COOLDOWN_TICKS = 36000; // 10分钟冷却
        
        private IntVec3 targetDescentLocation;
        
        // ==================== 配置参数 ====================
        
        public const float MIN_AFFINITY_FOR_FRIENDLY = 40f;  // 友好降临最低好感度
        public const int DESCENT_RANGE = 30;                 // 降临范围（格子）
        
        // ==================== 构造函数 ====================
        
        public NarratorDescentSystem(Game game) : base()
        {
            instance = this;
        }
        
        // ==================== ⭐ 公共API ====================
        
        /// <summary>
        /// ⭐ 触发降临（完全通用化）
        /// </summary>
        /// <param name="isHostile">是否为敌对降临</param>
        /// <param name="targetLoc">目标降临地点（可选，自动选择）</param>
        /// <returns>是否成功触发</returns>
        public bool TriggerDescent(bool isHostile, IntVec3? targetLoc = null)
        {
            // 1. 获取当前人格配置
            var manager = Current.Game?.GetComponent<NarratorManager>();
            var persona = manager?.GetCurrentPersona();
            
            if (persona == null)
            {
                Log.Error("[NarratorDescentSystem] 无法获取当前叙事者人格");
                Messages.Message("错误：无法获取叙事者配置", MessageTypeDefOf.RejectInput);
                return false;
            }
            
            // 2. ⭐ 检查是否支持降临（读取配置）
            if (string.IsNullOrEmpty(persona.descentPawnKind))
            {
                Log.Warning($"[NarratorDescentSystem] 叙事者 {persona.defName} 未配置 descentPawnKind，不支持降临");
                Messages.Message($"{persona.narratorName} 尚未支持实体化降临功能", MessageTypeDefOf.RejectInput);
                return false;
            }
            
            // 3. 检查基本条件
            if (!CanTriggerDescent(isHostile, persona, out string reason))
            {
                Messages.Message($"无法触发降临: {reason}", MessageTypeDefOf.RejectInput);
                return false;
            }
            
            // 4. 选择降临地点
            if (targetLoc == null)
            {
                targetLoc = SelectDescentLocation();
                if (targetLoc == null || !targetLoc.Value.IsValid)
                {
                    Messages.Message("找不到合适的降临地点！", MessageTypeDefOf.RejectInput);
                    return false;
                }
            }
            
            targetDescentLocation = targetLoc.Value;
            lastDescentWasHostile = isHostile;
            lastDescentTick = Find.TickManager.TicksGame;
            
            // 5. ⭐ 开始降临序列（通用化）
            StartDescentSequence(persona, isHostile);
            
            return true;
        }
        
        // ==================== ⭐ 私有方法 - 通用化逻辑 ====================
        
        /// <summary>
        /// ⭐ 检查是否可以触发降临（通用检查）
        /// </summary>
        private bool CanTriggerDescent(bool isHostile, NarratorPersonaDef persona, out string reason)
        {
            reason = "";
            
            // 1. 检查冷却时间
            int ticksSinceLastDescent = Find.TickManager.TicksGame - lastDescentTick;
            if (ticksSinceLastDescent < DESCENT_COOLDOWN_TICKS)
            {
                int remainingMinutes = (DESCENT_COOLDOWN_TICKS - ticksSinceLastDescent) / 3600;
                reason = $"冷却中，还需 {remainingMinutes} 分钟";
                return false;
            }
            
            // 2. 检查是否已有降临实体
            if (HasDescentPawnOnMap(persona))
            {
                reason = $"{persona.narratorName} 已经在场，无法重复降临";
                return false;
            }
            
            // 3. 检查好感度（仅友好模式）
            if (!isHostile)
            {
                var agent = Current.Game?.GetComponent<Storyteller.StorytellerAgent>();
                if (agent != null)
                {
                    float affinity = agent.GetAffinity();
                    if (affinity < MIN_AFFINITY_FOR_FRIENDLY)
                    {
                        reason = $"友好降临需要好感度 {MIN_AFFINITY_FOR_FRIENDLY}+，当前 {affinity:F0}";
                        return false;
                    }
                }
            }
            
            // 4. 检查地图
            if (Find.CurrentMap == null)
            {
                reason = "当前没有有效地图";
                return false;
            }
            
            return true;
        }
        
        /// <summary>
        /// ⭐ 开始降临序列（完全通用化）
        /// </summary>
        private void StartDescentSequence(NarratorPersonaDef persona, bool isHostile)
        {
            isDescending = true;
            
            try
            {
                // 1. ⭐ 播放立绘姿态动画（从配置读取）
                PlayPostureAnimation(persona);
                
                // 2. ⭐ 播放音效（从配置读取）
                PlayDescentSound(persona);
                
                // 3. ⭐ 显示通用信件
                SendDescentLetter(persona, isHostile);
                
                // 4. ⭐ 生成降临实体（延迟3秒后执行）
                LongEventHandler.QueueLongEvent(() =>
                {
                    SpawnPhysicalEntities(persona, isHostile);
                    isDescending = false;
                }, "Descending", false, null);
                
                Log.Message($"[NarratorDescentSystem] ⭐ 开始降临: {persona.defName}, 敌对={isHostile}");
            }
            catch (Exception ex)
            {
                Log.Error($"[NarratorDescentSystem] 降临序列失败: {ex}");
                isDescending = false;
            }
        }
        
        /// <summary>
        /// ⭐ 播放立绘姿态动画（从配置读取）
        /// </summary>
        private void PlayPostureAnimation(NarratorPersonaDef persona)
        {
            try
            {
                var panel = PortraitOverlaySystem.GetPanel();
                if (panel == null)
                {
                    Log.Warning("[NarratorDescentSystem] 立绘面板未初始化，跳过姿态动画");
                    return;
                }
                
                // ⭐ 从配置读取姿态和特效路径
                string posturePath = persona.descentPosturePath;
                string effectPath = persona.descentEffectPath;
                
                if (string.IsNullOrEmpty(posturePath))
                {
                    Log.Message("[NarratorDescentSystem] 未配置姿态动画，跳过");
                    return;
                }
                
                // ⭐ 调用通用姿态系统
                panel.TriggerPostureAnimation(
                    postureName: posturePath,
                    effectName: effectPath,
                    duration: 3.0f,
                    callback: () => Log.Message("[NarratorDescentSystem] 姿态动画完成")
                );
                
                Log.Message($"[NarratorDescentSystem] 播放姿态动画: {posturePath}, 特效: {effectPath ?? "无"}");
            }
            catch (Exception ex)
            {
                Log.Error($"[NarratorDescentSystem] 播放姿态动画失败: {ex}");
            }
        }
        
        /// <summary>
        /// ⭐ 生成降临实体（完全通用化，禁止硬编码）
        /// </summary>
        private void SpawnPhysicalEntities(NarratorPersonaDef persona, bool isHostile)
        {
            try
            {
                Map map = Find.CurrentMap;
                if (map == null)
                {
                    Log.Error("[NarratorDescentSystem] 地图为空，无法生成实体");
                    return;
                }
                
                // 1. ⭐ 生成空投舱/特效物（可选）
                SpawnSkyfaller(persona, map);
                
                // 2. ⭐ 生成主体实体（必须）
                Pawn mainPawn = SpawnMainPawn(persona, map, isHostile);
                
                if (mainPawn == null)
                {
                    Log.Error("[NarratorDescentSystem] 生成主体实体失败");
                    return;
                }
                
                // 3. ⭐ 生成伴随生物（可选）
                Pawn companion = SpawnCompanion(persona, map, mainPawn, isHostile);
                
                // 4. ⭐ 应用降临效果
                ApplyDescentEffects(persona, mainPawn, companion, isHostile);
                
                Log.Message($"[NarratorDescentSystem] ⭐ 降临实体生成完成: {mainPawn?.LabelShort ?? "未知"}");
            }
            catch (Exception ex)
            {
                Log.Error($"[NarratorDescentSystem] 生成降临实体失败: {ex}");
            }
        }
        
        /// <summary>
        /// ⭐ 生成空投舱/特效物（从配置读取 DefName）
        /// </summary>
        private void SpawnSkyfaller(NarratorPersonaDef persona, Map map)
        {
            try
            {
                string skyfallerDefName = persona.descentSkyfallerDef;
                
                if (string.IsNullOrEmpty(skyfallerDefName))
                {
                    // 使用默认空投舱
                    skyfallerDefName = "DropPodIncoming";
                }
                
                // ⭐ 通用化：从 DefDatabase 读取
                ThingDef skyfallerDef = DefDatabase<ThingDef>.GetNamedSilentFail(skyfallerDefName);
                
                if (skyfallerDef == null)
                {
                    Log.Warning($"[NarratorDescentSystem] 空投舱 Def 未找到: {skyfallerDefName}，跳过");
                    return;
                }
                
                // TODO: 生成空投舱（需要进一步实现）
                Log.Message($"[NarratorDescentSystem] 使用空投舱: {skyfallerDefName}");
            }
            catch (Exception ex)
            {
                Log.Warning($"[NarratorDescentSystem] 生成空投舱失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// ⭐ 生成主体实体（从配置读取 PawnKindDef）
        /// </summary>
        private Pawn SpawnMainPawn(NarratorPersonaDef persona, Map map, bool isHostile)
        {
            try
            {
                // ⭐ 禁止硬编码：从配置读取
                string pawnKindName = persona.descentPawnKind;
                
                PawnKindDef pawnKindDef = DefDatabase<PawnKindDef>.GetNamedSilentFail(pawnKindName);
                
                if (pawnKindDef == null)
                {
                    Log.Error($"[NarratorDescentSystem] PawnKindDef 未找到: {pawnKindName}");
                    return null;
                }
                
                // ⭐ 确定阵营
                Faction faction = isHostile ? Find.FactionManager.RandomEnemyFaction() : Faction.OfPlayer;
                
                // ⭐ 生成 Pawn
                Pawn pawn = PawnGenerator.GeneratePawn(pawnKindDef, faction);
                
                // ⭐ 设置名称（使用人格名称）
                pawn.Name = new NameTriple("", persona.narratorName, "");
                
                // ⭐ 生成到地图
                GenSpawn.Spawn(pawn, targetDescentLocation, map);
                
                Log.Message($"[NarratorDescentSystem] ⭐ 生成主体: {pawn.LabelShort} (PawnKind: {pawnKindName})");
                
                return pawn;
            }
            catch (Exception ex)
            {
                Log.Error($"[NarratorDescentSystem] 生成主体实体失败: {ex}");
                return null;
            }
        }
        
        /// <summary>
        /// ⭐ 生成伴随生物（从配置读取，可选）
        /// </summary>
        private Pawn SpawnCompanion(NarratorPersonaDef persona, Map map, Pawn master, bool isHostile)
        {
            try
            {
                // ⭐ 读取配置
                string companionKindName = persona.companionPawnKind;
                
                if (string.IsNullOrEmpty(companionKindName))
                {
                    Log.Message("[NarratorDescentSystem] 未配置伴随生物，跳过");
                    return null;
                }
                
                // ⭐ 通用化：从 DefDatabase 读取
                PawnKindDef companionKindDef = DefDatabase<PawnKindDef>.GetNamedSilentFail(companionKindName);
                
                if (companionKindDef == null)
                {
                    Log.Warning($"[NarratorDescentSystem] 伴随生物 PawnKindDef 未找到: {companionKindName}");
                    return null;
                }
                
                // ⭐ 确定阵营（与主体一致）
                Faction faction = master.Faction;
                
                // ⭐ 生成伴随生物
                Pawn companion = PawnGenerator.GeneratePawn(companionKindDef, faction);
                
                // ⭐ 生成到主体旁边
                IntVec3 companionLoc = targetDescentLocation + new IntVec3(2, 0, 0);
                GenSpawn.Spawn(companion, companionLoc, map);
                
                // ⭐ 建立 Bond 关系（如果支持）
                if (companion.RaceProps.Animal && master.IsColonist)
                {
                    companion.relations?.AddDirectRelation(PawnRelationDefOf.Bond, master);
                    Log.Message($"[NarratorDescentSystem] 建立 Bond: {master.LabelShort} <-> {companion.LabelShort}");
                }
                
                Log.Message($"[NarratorDescentSystem] ⭐ 生成伴随生物: {companion.LabelShort} (PawnKind: {companionKindName})");
                
                return companion;
            }
            catch (Exception ex)
            {
                Log.Warning($"[NarratorDescentSystem] 生成伴随生物失败: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// ⭐ 应用降临效果（通用化）
        /// </summary>
        private void ApplyDescentEffects(NarratorPersonaDef persona, Pawn mainPawn, Pawn companion, bool isHostile)
        {
            try
            {
                Map map = Find.CurrentMap;
                if (map == null) return;
                
                // 根据敌对/友好应用不同效果
                if (isHostile)
                {
                    ApplyHostileEffects(targetDescentLocation, map);
                }
                else
                {
                    ApplyFriendlyEffects(targetDescentLocation, map);
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[NarratorDescentSystem] 应用降临效果失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 应用友好效果
        /// </summary>
        private void ApplyFriendlyEffects(IntVec3 location, Map map)
        {
            // 1. 治愈周围的殖民者
            foreach (Pawn pawn in GenRadial.RadialDistinctThingsAround(location, map, 10f, true).OfType<Pawn>())
            {
                if (pawn.IsColonist && pawn.health != null)
                {
                    List<Hediff_Injury> injuries = new List<Hediff_Injury>();
                    pawn.health.hediffSet.GetHediffs(ref injuries);
                    
                    foreach (var injury in injuries)
                    {
                        injury.Heal(injury.Severity);
                    }
                }
            }
            
            Log.Message("[NarratorDescentSystem] 应用友好效果：治愈殖民者");
        }
        
        /// <summary>
        /// 应用敌对效果
        /// </summary>
        private void ApplyHostileEffects(IntVec3 location, Map map)
        {
            // 1. 对周围敌人造成伤害
            foreach (Pawn pawn in GenRadial.RadialDistinctThingsAround(location, map, 10f, true).OfType<Pawn>())
            {
                if (pawn.HostileTo(Faction.OfPlayer))
                {
                    pawn.TakeDamage(new DamageInfo(DamageDefOf.Burn, 50f));
                }
            }
            
            Log.Message("[NarratorDescentSystem] 应用敌对效果：伤害敌人");
        }
        
        /// <summary>
        /// ⭐ 播放降临音效（从配置读取）
        /// </summary>
        private void PlayDescentSound(NarratorPersonaDef persona)
        {
            try
            {
                string soundDefName = persona.descentSound;
                
                if (string.IsNullOrEmpty(soundDefName))
                {
                    Log.Message("[NarratorDescentSystem] 未配置降临音效，跳过");
                    return;
                }
                
                // ⭐ 通用化：从 DefDatabase 读取
                SoundDef soundDef = DefDatabase<SoundDef>.GetNamedSilentFail(soundDefName);
                
                if (soundDef == null)
                {
                    Log.Warning($"[NarratorDescentSystem] 音效 Def 未找到: {soundDefName}");
                    return;
                }
                
                // 播放音效（修复：使用正确的 RimWorld API）
                SoundStarter.PlayOneShot(soundDef, new TargetInfo(targetDescentLocation, Find.CurrentMap));
                
                Log.Message($"[NarratorDescentSystem] 播放音效: {soundDefName}");
            }
            catch (Exception ex)
            {
                Log.Warning($"[NarratorDescentSystem] 播放音效失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// ⭐ 发送降临信件（动态读取配置或使用通用文本）
        /// </summary>
        private void SendDescentLetter(NarratorPersonaDef persona, bool isHostile)
        {
            try
            {
                // ⭐ 读取配置的信件标题和内容
                string letterLabel = persona.descentLetterLabel;
                string letterText = persona.descentLetterText;
                
                // ⭐ 如果未配置，使用通用文本
                if (string.IsNullOrEmpty(letterLabel))
                {
                    letterLabel = isHostile ? 
                        $"{persona.narratorName} 降临（袭击）" : 
                        $"{persona.narratorName} 降临（援助）";
                }
                
                if (string.IsNullOrEmpty(letterText))
                {
                    letterText = isHostile ?
                        $"{persona.narratorName} 以敌对姿态降临到了这片土地！" :
                        $"{persona.narratorName} 响应你的呼唤，降临到了这片土地！";
                }
                
                // ⭐ 发送信件
                LetterDef letterDef = isHostile ? LetterDefOf.ThreatBig : LetterDefOf.PositiveEvent;
                
                Find.LetterStack.ReceiveLetter(
                    label: letterLabel,
                    text: letterText,
                    textLetterDef: letterDef,
                    lookTargets: new TargetInfo(targetDescentLocation, Find.CurrentMap)
                );
                
                Log.Message($"[NarratorDescentSystem] 发送信件: {letterLabel}");
            }
            catch (Exception ex)
            {
                Log.Warning($"[NarratorDescentSystem] 发送信件失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 选择降临地点
        /// </summary>
        private IntVec3? SelectDescentLocation()
        {
            try
            {
                Map map = Find.CurrentMap;
                if (map == null) return null;
                
                IntVec3 colonyCenter = map.Center;
                
                // 寻找合适的降临点
                for (int i = 0; i < 50; i++)
                {
                    IntVec3 candidate = colonyCenter + new IntVec3(
                        Rand.Range(-DESCENT_RANGE, DESCENT_RANGE),
                        0,
                        Rand.Range(-DESCENT_RANGE, DESCENT_RANGE)
                    );
                    
                    if (IsValidDescentLocation(candidate, map))
                    {
                        return candidate;
                    }
                }
                
                return CellFinder.TryFindRandomCellNear(colonyCenter, map, 20, 
                    (IntVec3 c) => IsValidDescentLocation(c, map), 
                    out IntVec3 result) ? result : (IntVec3?)null;
            }
            catch (Exception ex)
            {
                Log.Error($"[NarratorDescentSystem] 选择降临地点失败: {ex}");
                return null;
            }
        }
        
        /// <summary>
        /// 检查地点是否适合降临
        /// </summary>
        private bool IsValidDescentLocation(IntVec3 loc, Map map)
        {
            if (!loc.InBounds(map)) return false;
            if (!loc.Standable(map)) return false;
            if (loc.Roofed(map)) return false;
            
            // 检查周围空间
            for (int x = -1; x <= 1; x++)
            {
                for (int z = -1; z <= 1; z++)
                {
                    IntVec3 checkLoc = loc + new IntVec3(x, 0, z);
                    if (!checkLoc.InBounds(map) || !checkLoc.Standable(map))
                    {
                        return false;
                    }
                }
            }
            
            return true;
        }
        
        /// <summary>
        /// ⭐ 检查是否已有降临实体（通用化检查）
        /// </summary>
        private bool HasDescentPawnOnMap(NarratorPersonaDef persona)
        {
            if (Find.CurrentMap == null) return false;
            
            // ⭐ 读取配置的 PawnKindDef
            string pawnKindName = persona.descentPawnKind;
            
            foreach (Pawn pawn in Find.CurrentMap.mapPawns.AllPawns)
            {
                // 检查 PawnKindDef 是否匹配
                if (pawn.kindDef?.defName == pawnKindName)
                {
                    return true;
                }
                
                // 或者检查名称是否匹配
                if (pawn.Name != null && pawn.Name.ToStringShort == persona.narratorName)
                {
                    return true;
                }
            }
            
            return false;
        }
        
        /// <summary>
        /// 获取降临冷却剩余时间（秒）
        /// </summary>
        public int GetCooldownRemaining()
        {
            int ticksSinceLastDescent = Find.TickManager.TicksGame - lastDescentTick;
            int remainingTicks = Math.Max(0, DESCENT_COOLDOWN_TICKS - ticksSinceLastDescent);
            return remainingTicks / 60;
        }
        
        // ==================== 存档相关 ====================
        
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref isDescending, "isDescending", false);
            Scribe_Values.Look(ref lastDescentWasHostile, "lastDescentWasHostile", false);
            Scribe_Values.Look(ref lastDescentTick, "lastDescentTick", 0);
            Scribe_Values.Look(ref targetDescentLocation, "targetDescentLocation");
        }
    }
    
    // ==================== 向后兼容枚举（已弃用） ====================
    
    /// <summary>
    /// [已弃用] 降临模式枚举 - 保留用于向后兼容
    /// 新代码请使用 bool isHostile 参数
    /// </summary>
    [Obsolete("Use bool isHostile instead. Assist = false, Attack = true")]
    public enum DescentMode
    {
        Assist,  // 援助模式（友好降临） = isHostile: false
        Attack   // 袭击模式（敌对降临） = isHostile: true
    }
    
    /// <summary>
    /// [已弃用] 降临音效类型 - 保留用于向后兼容
    /// 新代码请直接使用 SoundDef 名称
    /// </summary>
    [Obsolete("Use SoundDef names directly from persona.descentSound")]
    public enum DescentSoundType
    {
        Preparation,
        Charging,
        Casting,
        Flight,
        Impact,
        Completion
    }
}
