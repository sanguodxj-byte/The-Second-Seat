using System;
using System.Collections.Generic;
using System.Linq; // ? 添加 Linq 命名空间
using UnityEngine;
using Verse;
using RimWorld;
using TheSecondSeat.Narrator;
using TheSecondSeat.PersonaGeneration;

namespace TheSecondSeat.Descent
{
    /// <summary>
    /// ? v2.0.0: 叙事者降临系统 - 核心管理器
    /// 
    /// 功能：
    /// - 触发降临模式（援助/袭击）
    /// - 协调立绘切换、特效播放、动画播放
    /// - 生成降临后的小人和召唤物
    /// - 管理降临状态和冷却
    /// 
    /// 使用：
    /// - NarratorDescentSystem.Instance.TriggerDescent(DescentMode.Assist)
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
        
        private DescentState currentState = DescentState.Idle;
        private DescentMode lastDescentMode = DescentMode.Assist;
        private int lastDescentTick = 0;
        private const int DESCENT_COOLDOWN_TICKS = 36000; // 10分钟冷却（60秒/分 * 60帧/秒 * 10）
        
        private DescentAnimationController? animationController;
        private DescentEffectRenderer? effectRenderer;
        private IntVec3 targetDescentLocation;
        
        // ==================== 配置参数 ====================
        
        public const float MIN_AFFINITY_FOR_ASSIST = 40f;    // 援助模式最低好感度
        public const float MIN_AFFINITY_FOR_ATTACK = -100f;  // 袭击模式最低好感度（总是可用）
        public const int DESCENT_RANGE = 30;                 // 降临范围（格子）
        
        // ==================== 构造函数 ====================
        
        public NarratorDescentSystem(Game game) : base()
        {
            instance = this;
        }
        
        // ==================== 公共API ====================
        
        /// <summary>
        /// 触发降临模式
        /// </summary>
        /// <param name="mode">降临模式（援助/袭击）</param>
        /// <param name="targetLoc">目标降临地点（可选，自动选择）</param>
        /// <returns>是否成功触发</returns>
        public bool TriggerDescent(DescentMode mode, IntVec3? targetLoc = null)
        {
            // 1. 检查是否可以触发
            if (!CanTriggerDescent(mode, out string reason))
            {
                Messages.Message($"无法触发降临: {reason}", MessageTypeDefOf.RejectInput);
                Log.Warning($"[NarratorDescentSystem] Cannot trigger descent: {reason}");
                return false;
            }
            
            // 2. 检查状态
            if (currentState != DescentState.Idle)
            {
                Messages.Message("降临正在进行中，请稍候...", MessageTypeDefOf.RejectInput);
                return false;
            }
            
            // 3. 选择降临地点
            if (targetLoc == null)
            {
                targetLoc = SelectDescentLocation(mode);
                if (targetLoc == null || !targetLoc.Value.IsValid)
                {
                    Messages.Message("找不到合适的降临地点！", MessageTypeDefOf.RejectInput);
                    return false;
                }
            }
            
            targetDescentLocation = targetLoc.Value;
            lastDescentMode = mode;
            
            // 4. 开始降临序列
            Log.Message($"[NarratorDescentSystem] Triggering descent: Mode={mode}, Location={targetDescentLocation}");
            StartDescentSequence(mode);
            
            return true;
        }
        
        /// <summary>
        /// 检查是否可以触发降临
        /// </summary>
        public bool CanTriggerDescent(DescentMode mode, out string reason)
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
            
            // 2. 检查是否已有叙事者小人
            if (HasNarratorPawnOnMap())
            {
                reason = "叙事者已经在场，无法重复降临";
                return false;
            }
            
            // 3. 检查好感度
            var agent = Current.Game?.GetComponent<Storyteller.StorytellerAgent>();
            if (agent != null)
            {
                float affinity = agent.GetAffinity();
                
                if (mode == DescentMode.Assist && affinity < MIN_AFFINITY_FOR_ASSIST)
                {
                    reason = $"援助模式需要好感度 {MIN_AFFINITY_FOR_ASSIST}+，当前 {affinity:F0}";
                    return false;
                }
                
                // 袭击模式总是可用，但高好感度时提示
                if (mode == DescentMode.Attack && affinity > 60f)
                {
                    // 允许触发，但记录警告
                    Log.Warning($"[NarratorDescentSystem] Triggering attack descent at high affinity ({affinity:F0})");
                }
            }
            
            // 4. 检查是否有有效地图
            if (Find.CurrentMap == null)
            {
                reason = "当前没有有效地图";
                return false;
            }
            
            return true;
        }
        
        /// <summary>
        /// 获取降临冷却剩余时间（秒）
        /// </summary>
        public int GetCooldownRemaining()
        {
            int ticksSinceLastDescent = Find.TickManager.TicksGame - lastDescentTick;
            int remainingTicks = Math.Max(0, DESCENT_COOLDOWN_TICKS - ticksSinceLastDescent);
            return remainingTicks / 60; // 转换为秒
        }
        
        /// <summary>
        /// 检查是否有叙事者小人在地图上
        /// </summary>
        public bool HasNarratorPawnOnMap()
        {
            if (Find.CurrentMap == null) return false;
            
            foreach (Pawn pawn in Find.CurrentMap.mapPawns.FreeColonists)
            {
                // 检查是否是叙事者小人（通过标记或特殊属性）
                if (pawn.def.defName.Contains("Narrator") || 
                    pawn.LabelShort.Contains("叙事者"))
                {
                    return true;
                }
            }
            
            return false;
        }
        
        // ==================== 私有方法 ====================
        
        /// <summary>
        /// 开始降临序列
        /// </summary>
        private void StartDescentSequence(DescentMode mode)
        {
            try
            {
                currentState = DescentState.PreparingPosture;
                lastDescentTick = Find.TickManager.TicksGame;
                
                // 1. 初始化控制器
                if (animationController == null)
                {
                    animationController = new DescentAnimationController();
                }
                
                if (effectRenderer == null)
                {
                    effectRenderer = new DescentEffectRenderer();
                }
                
                // 2. 开始姿势切换动画
                animationController.StartPostureSequence(mode, OnPostureSequenceComplete);
                
                // 3. 播放音效（可选）
                PlayDescentSound(mode, DescentSoundType.Preparation);
                
                // 4. 显示消息
                string modeText = mode == DescentMode.Assist ? "援助" : "袭击";
                Messages.Message($"?? 叙事者降临 - {modeText}模式启动！", MessageTypeDefOf.PositiveEvent);
                
                Log.Message($"[NarratorDescentSystem] Descent sequence started: {mode}");
            }
            catch (Exception ex)
            {
                Log.Error($"[NarratorDescentSystem] Failed to start descent sequence: {ex}");
                currentState = DescentState.Idle;
            }
        }
        
        /// <summary>
        /// 姿势序列完成回调
        /// </summary>
        private void OnPostureSequenceComplete()
        {
            currentState = DescentState.PlayingCinematic;
            
            // 开始过场动画
            animationController?.StartCinematic(lastDescentMode, targetDescentLocation, OnCinematicComplete);
            
            Log.Message("[NarratorDescentSystem] Posture sequence completed, starting cinematic");
        }
        
        /// <summary>
        /// 过场动画完成回调
        /// </summary>
        private void OnCinematicComplete()
        {
            currentState = DescentState.SpawningEntities;
            
            try
            {
                // 1. 生成叙事者小人
                Pawn narratorPawn = SpawnNarratorPawn(targetDescentLocation);
                
                // 2. 生成召唤物
                Pawn summonPawn = SpawnSummonCreature(targetDescentLocation, lastDescentMode);
                
                // 3. 播放降落特效
                effectRenderer?.PlayImpactEffect(targetDescentLocation, lastDescentMode);
                
                // 4. 播放音效
                PlayDescentSound(lastDescentMode, DescentSoundType.Impact);
                
                // 5. 应用降临效果
                ApplyDescentEffects(targetDescentLocation, lastDescentMode);
                
                // 6. 完成降临
                currentState = DescentState.Idle;
                
                string modeText = lastDescentMode == DescentMode.Assist ? "援助" : "袭击";
                Messages.Message($"? 叙事者降临完成！{modeText}开始！", MessageTypeDefOf.PositiveEvent);
                
                Log.Message("[NarratorDescentSystem] Descent completed successfully");
            }
            catch (Exception ex)
            {
                Log.Error($"[NarratorDescentSystem] Failed to spawn entities: {ex}");
                currentState = DescentState.Idle;
            }
        }
        
        /// <summary>
        /// 选择降临地点
        /// </summary>
        private IntVec3? SelectDescentLocation(DescentMode mode)
        {
            try
            {
                Map map = Find.CurrentMap;
                if (map == null) return null;
                
                // 获取玩家殖民地中心
                IntVec3 colonyCenter = map.Center;
                
                // 寻找合适的降临点
                for (int i = 0; i < 50; i++) // 最多尝试50次
                {
                    // 在殖民地周围随机选点
                    IntVec3 candidate = colonyCenter + new IntVec3(
                        Rand.Range(-DESCENT_RANGE, DESCENT_RANGE),
                        0,
                        Rand.Range(-DESCENT_RANGE, DESCENT_RANGE)
                    );
                    
                    // 检查地点是否合适
                    if (IsValidDescentLocation(candidate, map))
                    {
                        return candidate;
                    }
                }
                
                // 如果找不到，返回殖民地中心附近
                return CellFinder.TryFindRandomCellNear(colonyCenter, map, 20, 
                    (IntVec3 c) => IsValidDescentLocation(c, map), 
                    out IntVec3 result) ? result : (IntVec3?)null;
            }
            catch (Exception ex)
            {
                Log.Error($"[NarratorDescentSystem] Failed to select descent location: {ex}");
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
            if (loc.Roofed(map)) return false; // 不能在屋顶下
            
            // 检查周围是否有足够空间（3x3区域）
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
        /// 生成叙事者小人
        /// </summary>
        private Pawn SpawnNarratorPawn(IntVec3 location)
        {
            try
            {
                // 获取当前人格
                var manager = Current.Game?.GetComponent<NarratorManager>();
                var persona = manager?.GetCurrentPersona();
                
                if (persona == null)
                {
                    Log.Error("[NarratorDescentSystem] No persona found for pawn generation");
                    return null;
                }
                
                // TODO: 创建叙事者小人 Def（参考 DescentPawnDef.cs）
                // PawnKindDef pawnKindDef = DefDatabase<PawnKindDef>.GetNamed($"Narrator_{persona.defName}";
                
                // 临时：使用默认小人
                PawnKindDef pawnKindDef = PawnKindDefOf.Colonist;
                
                // 生成小人
                Pawn pawn = PawnGenerator.GeneratePawn(pawnKindDef, Faction.OfPlayer);
                
                // 设置名称
                pawn.Name = new NameTriple("", persona.narratorName, "");
                
                // 生成到地图
                GenSpawn.Spawn(pawn, location, Find.CurrentMap);
                
                Log.Message($"[NarratorDescentSystem] Spawned narrator pawn: {pawn.LabelShort} at {location}");
                
                return pawn;
            }
            catch (Exception ex)
            {
                Log.Error($"[NarratorDescentSystem] Failed to spawn narrator pawn: {ex}");
                return null;
            }
        }
        
        /// <summary>
        /// 生成召唤物
        /// </summary>
        private Pawn SpawnSummonCreature(IntVec3 location, DescentMode mode)
        {
            try
            {
                // TODO: 创建巨龙召唤物 Def（参考 DescentPawnDef.cs）
                // PawnKindDef summonDef = DefDatabase<PawnKindDef>.GetNamed("NarratorDragon");
                
                // ? 修复：使用存在的PawnKindDef
                PawnKindDef summonDef = DefDatabase<PawnKindDef>.AllDefsListForReading.FirstOrDefault(
                    pk => pk.defName.Contains("Wolf") || pk.defName.Contains("Warg")
                );
                
                if (summonDef == null)
                {
                    summonDef = PawnKindDefOf.Colonist; // 回退到殖民者
                }
                
                // 生成召唤物
                Pawn summon = PawnGenerator.GeneratePawn(summonDef, Faction.OfPlayer);
                
                // 调整位置（在叙事者旁边）
                IntVec3 summonLoc = location + new IntVec3(2, 0, 0);
                GenSpawn.Spawn(summon, summonLoc, Find.CurrentMap);
                
                Log.Message($"[NarratorDescentSystem] Spawned summon creature at {summonLoc}");
                
                return summon;
            }
            catch (Exception ex)
            {
                Log.Error($"[NarratorDescentSystem] Failed to spawn summon: {ex}");
                return null;
            }
        }
        
        /// <summary>
        /// 应用降临效果
        /// </summary>
        private void ApplyDescentEffects(IntVec3 location, DescentMode mode)
        {
            try
            {
                Map map = Find.CurrentMap;
                if (map == null) return;
                
                // 根据模式应用不同效果
                if (mode == DescentMode.Assist)
                {
                    ApplyAssistEffects(location, map);
                }
                else
                {
                    ApplyAttackEffects(location, map);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[NarratorDescentSystem] Failed to apply descent effects: {ex}");
            }
        }
        
        /// <summary>
        /// 应用援助效果
        /// </summary>
        private void ApplyAssistEffects(IntVec3 location, Map map)
        {
            // 1. 治愈周围的殖民者
            foreach (Pawn pawn in GenRadial.RadialDistinctThingsAround(location, map, 10f, true).OfType<Pawn>())
            {
                if (pawn.IsColonist && pawn.health != null)
                {
                    // ? 修复：使用正确的GetHediffs方法签名
                    List<Hediff_Injury> injuries = new List<Hediff_Injury>();
                    pawn.health.hediffSet.GetHediffs(ref injuries);
                    
                    foreach (var injury in injuries)
                    {
                        injury.Heal(injury.Severity);
                    }
                    
                    Log.Message($"[NarratorDescentSystem] Healed colonist: {pawn.LabelShort}");
                }
            }
            
            // 2. 提升心情
            // TODO: 添加心情Buff
            
            // 3. 播放治愈特效
            effectRenderer?.PlayAuraEffect(location, DescentMode.Assist, 5f);
        }
        
        /// <summary>
        /// 应用袭击效果
        /// </summary>
        private void ApplyAttackEffects(IntVec3 location, Map map)
        {
            // 1. 对周围敌人造成伤害
            foreach (Pawn pawn in GenRadial.RadialDistinctThingsAround(location, map, 10f, true).OfType<Pawn>())
            {
                if (pawn.HostileTo(Faction.OfPlayer))
                {
                    // 造成伤害
                    pawn.TakeDamage(new DamageInfo(DamageDefOf.Burn, 50f));
                    
                    Log.Message($"[NarratorDescentSystem] Damaged enemy: {pawn.LabelShort}");
                }
            }
            
            // 2. 制造恐慌
            // TODO: 添加恐慌效果
            
            // 3. 播放攻击特效
            effectRenderer?.PlayAuraEffect(location, DescentMode.Attack, 5f);
        }
        
        /// <summary>
        /// 播放降临音效
        /// </summary>
        private void PlayDescentSound(DescentMode mode, DescentSoundType type)
        {
            try
            {
                // TODO: 实现音效播放
                // SoundDef soundDef = GetDescentSound(mode, type);
                // soundDef.PlayOneShot(new TargetInfo(targetDescentLocation, Find.CurrentMap));
                
                Log.Message($"[NarratorDescentSystem] Playing sound: {mode} - {type}");
            }
            catch (Exception ex)
            {
                Log.Warning($"[NarratorDescentSystem] Failed to play sound: {ex.Message}");
            }
        }
        
        // ==================== 存档相关 ====================
        
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref currentState, "currentState", DescentState.Idle);
            Scribe_Values.Look(ref lastDescentMode, "lastDescentMode", DescentMode.Assist);
            Scribe_Values.Look(ref lastDescentTick, "lastDescentTick", 0);
            Scribe_Values.Look(ref targetDescentLocation, "targetDescentLocation");
        }
    }
    
    // ==================== 枚举定义 ====================
    
    /// <summary>
    /// 降临模式
    /// </summary>
    public enum DescentMode
    {
        Assist,  // 援助模式（治愈、祝福）
        Attack   // 袭击模式（伤害、诅咒）
    }
    
    /// <summary>
    /// 降临状态
    /// </summary>
    public enum DescentState
    {
        Idle,               // 空闲
        PreparingPosture,   // 准备姿势
        PlayingCinematic,   // 播放过场动画
        SpawningEntities,   // 生成实体
        Completed           // 完成
    }
    
    /// <summary>
    /// 降临音效类型
    /// </summary>
    public enum DescentSoundType
    {
        Preparation,  // 准备
        Charging,     // 蓄力
        Casting,      // 施法
        Flight,       // 飞行
        Impact,       // 冲击
        Completion    // 完成
    }
}
