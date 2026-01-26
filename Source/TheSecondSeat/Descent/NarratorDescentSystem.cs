using System;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.Sound;
using RimWorld;
using TheSecondSeat.Narrator;
using TheSecondSeat.PersonaGeneration;
using TheSecondSeat.Core;

namespace TheSecondSeat.Descent
{
    /// <summary>
    /// 叙事者降临系统 - 核心协调器
    /// 重构版：将具体逻辑委托给辅助类
    /// </summary>
    public class NarratorDescentSystem : GameComponent
    {
        // ==================== 单例模式 ====================
        
        // ⭐ v2.9.8: 降临模式枚举
        public enum DescentMode
        {
            /// <summary>敌对模式 - 加入敌对派系，攻击殖民地</summary>
            Hostile,
            /// <summary>受控援助 - 加入玩家派系，可征召控制</summary>
            ControlledAssist,
            /// <summary>自主援助 - 加入盟友派系，自主行动保护殖民地</summary>
            AutonomousAssist
        }
        
        private static NarratorDescentSystem instance;
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
        private bool lastDescentPlayerControlled = true; // ⭐ v2.9.8: 是否由玩家控制
        private int lastDescentTick = 0;
        private const int DESCENT_COOLDOWN_TICKS = 36000; // ⭐ v2.4.0: 正式版冷却时间（10分钟）
        
        private IntVec3 targetDescentLocation;
        
        private Pawn currentDescentPawn = null;
        private Pawn currentCompanionPawn = null;
        private int descentStartTick = 0;
        private bool portraitWasOpen = false;
        private int lastReturnCheckTick = 0;
        private const int RETURN_CHECK_INTERVAL = 60;
        
        private IDescentAnimationProvider currentAnimationProvider = null;
        private DescentAnimationController animationController = new DescentAnimationController(); // ⭐ v2.0.0
        private bool wasInCombat = false;

        // 延迟生成
        private bool pendingSpawn = false;
        private int spawnDelayEndTick = 0;
        
        // ==================== 配置参数 ====================
        
        public const float MIN_AFFINITY_FOR_FRIENDLY = 40f;
        public const int DESCENT_RANGE = 30;
        
        /// <summary>
        /// 判断是否处于降临活动状态
        /// </summary>
        public bool IsDescentActive => isDescending || currentDescentPawn != null || pendingSpawn || currentAnimationProvider != null;
        
        // ==================== 构造函数 ====================
        
        public NarratorDescentSystem(Game game) : base()
        {
            instance = this;
        }
        
        // ==================== 公共API ====================
        
        /// <summary>
        /// 检查是否可以降临
        /// </summary>
        public bool CanDescendNow(out string reason)
        {
            var manager = Current.Game?.GetComponent<NarratorManager>();
            var persona = manager?.GetCurrentPersona();
            
            if (persona == null)
            {
                reason = "无法获取当前叙事者人格";
                return false;
            }

            if (IsDescentActive)
            {
                reason = "降临正在进行中或实体已存在";
                return false;
            }

            int ticksSinceLastDescent = Find.TickManager.TicksGame - lastDescentTick;
            if (ticksSinceLastDescent < DESCENT_COOLDOWN_TICKS)
            {
                int remainingMinutes = (DESCENT_COOLDOWN_TICKS - ticksSinceLastDescent) / 3600;
                reason = $"冷却中，还需 {remainingMinutes} 分钟";
                return false;
            }
            
            if (HasDescentPawnOnMap(persona))
            {
                reason = $"{persona.narratorName} 已经在场";
                return false;
            }
            
            if (string.IsNullOrEmpty(persona.descentPawnKind))
            {
                reason = $"{persona.narratorName} 不支持实体化降临";
                return false;
            }

            reason = "";
            return true;
        }

        /// <summary>
        /// 触发降临
        /// ⭐ v2.9.8: 添加 playerControlled 参数，区分受控/自主援助
        /// </summary>
        /// <param name="isHostile">是否为敌对模式</param>
        /// <param name="targetLoc">目标位置（可选）</param>
        /// <param name="playerControlled">援助模式时是否受玩家控制（默认 true）</param>
        public bool TriggerDescent(bool isHostile, IntVec3? targetLoc = null, bool playerControlled = true)
        {
            var manager = Current.Game?.GetComponent<NarratorManager>();
            var persona = manager?.GetCurrentPersona();
            
            if (persona == null)
            {
                Log.Error("[NarratorDescentSystem] 无法获取当前叙事者人格");
                Messages.Message("错误：无法获取叙事者配置", MessageTypeDefOf.RejectInput);
                return false;
            }
            
            if (string.IsNullOrEmpty(persona.descentPawnKind))
            {
                Log.Warning($"[NarratorDescentSystem] 叙事者 {persona.defName} 未配置 descentPawnKind");
                Messages.Message($"{persona.narratorName} 尚未支持实体化降临功能", MessageTypeDefOf.RejectInput);
                return false;
            }
            
            if (!CanTriggerDescent(isHostile, persona, out string reason))
            {
                Messages.Message($"无法触发降临: {reason}", MessageTypeDefOf.RejectInput);
                return false;
            }
            
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
            lastDescentPlayerControlled = playerControlled; // ⭐ v2.9.8
            lastDescentTick = Find.TickManager.TicksGame;
            descentStartTick = Find.TickManager.TicksGame;
            portraitWasOpen = PortraitOverlaySystem.IsEnabled();
            
            Log.Message($"[NarratorDescentSystem] TriggerDescent: isHostile={isHostile}, playerControlled={playerControlled}");
            StartDescentSequence(persona, isHostile);
            return true;
        }
        
        /// <summary>
        /// 叙事者主动回归
        /// </summary>
        public bool TriggerReturn(bool forceImmediate = false)
        {
            if (currentDescentPawn == null || !currentDescentPawn.Spawned)
            {
                Log.Warning("[NarratorDescentSystem] 没有降临实体，无法回归");
                return false;
            }
            
            try
            {
                PlayReturnAnimation();
                
                currentDescentPawn.Destroy(DestroyMode.Vanish);
                currentDescentPawn = null;

                CompanionSpawner.DestroyCompanion(currentCompanionPawn);
                currentCompanionPawn = null;
                
                RestorePortraitPanel();
                
                var manager = Current.Game?.GetComponent<NarratorManager>();
                var persona = manager?.GetCurrentPersona();
                if (persona != null)
                {
                    Messages.Message(
                        $"{persona.narratorName} 已回归虚空，继续以意识形态陪伴你。",
                        MessageTypeDefOf.NeutralEvent
                    );
                }
                
                Log.Message("[NarratorDescentSystem] 叙事者已回归");
                return true;
            }
            catch (Exception ex)
            {
                Log.Error($"[NarratorDescentSystem] 回归失败: {ex}");
                return false;
            }
        }
        
        /// <summary>
        /// 安排延迟生成
        /// </summary>
        public void ScheduleSpawn(int delayTicks)
        {
            pendingSpawn = true;
            spawnDelayEndTick = Find.TickManager.TicksGame + delayTicks;
            Log.Message($"[NarratorDescentSystem] 安排延迟生成: {delayTicks} ticks");
        }

        /// <summary>
        /// 通用召唤方法
        /// </summary>
        public void SpawnSummon(PawnKindDef pawnKind, IntVec3 position, Map map, Faction faction, bool isCompanion = true)
        {
            Pawn newCompanion;
            CompanionSpawner.SpawnSummon(pawnKind, position, map, faction, currentCompanionPawn, currentDescentPawn, out newCompanion);
            
            if (isCompanion)
            {
                currentCompanionPawn = newCompanion;
            }
        }

        /// <summary>
        /// 获取当前降临实体
        /// </summary>
        public Pawn GetDescentPawn() => currentDescentPawn;

        /// <summary>
        /// 获取冷却剩余时间
        /// </summary>
        public string GetCooldownRemaining()
        {
            int ticksSinceLastDescent = Find.TickManager.TicksGame - lastDescentTick;
            if (ticksSinceLastDescent >= DESCENT_COOLDOWN_TICKS) return "Ready";
            return (DESCENT_COOLDOWN_TICKS - ticksSinceLastDescent).ToStringTicksToPeriod();
        }
        
        // ==================== GameComponent ====================
        
        // ⭐ v2.5.0: 使用固定时间步长，避免 Tick 与帧时间混淆
        private const float TICK_INTERVAL = 1f / 60f;
        
        public override void GameComponentTick()
        {
            base.GameComponentTick();
            
            // 更新动画 - 使用固定时间步长，不受游戏速度影响
            // 注意：视觉动画应该使用 TICK_INTERVAL 而非 Time.deltaTime
            // Time.deltaTime 是渲染帧时间，在 GameComponentTick 中使用会导致游戏加速时动画过快
            if (currentAnimationProvider?.IsPlaying == true)
            {
                currentAnimationProvider.Update(TICK_INTERVAL);
            }
            
            // ⭐ v2.5.0: 更新姿势动画控制器（使用固定时间步长）
            animationController.Update(TICK_INTERVAL);
            
            // 检查延迟生成
            if (pendingSpawn && Find.TickManager.TicksGame >= spawnDelayEndTick)
            {
                ExecutePendingSpawn();
            }
            
            if (currentDescentPawn == null) return;
            
            // 检查实体是否已被外部销毁
            if (currentDescentPawn.Destroyed)
            {
                HandlePawnDestroyed();
                return;
            }
            
            // 检查是否需要强制销毁
            if (DescentStateMonitor.ShouldForceDestroy(currentDescentPawn, currentCompanionPawn, wasInCombat, out bool isCombatReason))
            {
                ForceDestroyDescentPawn(isCombatReason);
                return;
            }

            // 定时检查
            int currentTick = Find.TickManager.TicksGame;
            if (currentTick - lastReturnCheckTick < RETURN_CHECK_INTERVAL) return;
            lastReturnCheckTick = currentTick;
            
            // 更新战斗状态
            wasInCombat = DescentStateMonitor.UpdateCombatStatus(currentDescentPawn, wasInCombat);
            
            // 检查是否应该回归
            if (DescentReturnLogic.ShouldReturn(currentDescentPawn, descentStartTick, out int newStartTick))
            {
                TriggerReturn();
            }
            else
            {
                descentStartTick = newStartTick;
            }
        }
        
        public override void ExposeData()
        {
            base.ExposeData();
            
            Scribe_Values.Look(ref isDescending, "isDescending", false);
            Scribe_Values.Look(ref lastDescentWasHostile, "lastDescentWasHostile", false);
            Scribe_Values.Look(ref lastDescentTick, "lastDescentTick", 0);
            Scribe_Values.Look(ref targetDescentLocation, "targetDescentLocation");
            
            Scribe_References.Look(ref currentDescentPawn, "currentDescentPawn");
            Scribe_References.Look(ref currentCompanionPawn, "currentCompanionPawn");
            
            Scribe_Values.Look(ref descentStartTick, "descentStartTick", 0);
            Scribe_Values.Look(ref portraitWasOpen, "portraitWasOpen", false);
            Scribe_Values.Look(ref pendingSpawn, "pendingSpawn", false);
            Scribe_Values.Look(ref spawnDelayEndTick, "spawnDelayEndTick", 0);
            
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                ValidateStateConsistency();
            }
        }
        
        // ==================== 私有方法 ====================
        
        private bool CanTriggerDescent(bool isHostile, NarratorPersonaDef persona, out string reason)
        {
            reason = "";
            
            if (IsDescentActive)
            {
                reason = "降临正在进行中或实体已存在";
                return false;
            }

            int ticksSinceLastDescent = Find.TickManager.TicksGame - lastDescentTick;
            if (ticksSinceLastDescent < DESCENT_COOLDOWN_TICKS)
            {
                int remainingMinutes = (DESCENT_COOLDOWN_TICKS - ticksSinceLastDescent) / 3600;
                reason = $"冷却中，还需 {remainingMinutes} 分钟";
                return false;
            }
            
            if (HasDescentPawnOnMap(persona))
            {
                reason = $"{persona.narratorName} 已经在场，无法重复降临";
                return false;
            }
            
            if (!isHostile)
            {
                var manager = Current.Game?.GetComponent<NarratorManager>();
                var agent = manager?.GetStorytellerAgent();
                if (agent == null)
                {
                    reason = "无法获取好感度系统";
                    return false;
                }
                
                float affinity = agent.GetAffinity();
                if (affinity < MIN_AFFINITY_FOR_FRIENDLY)
                {
                    reason = $"友好降临需要好感度 {MIN_AFFINITY_FOR_FRIENDLY}+，当前 {affinity:F0}";
                    return false;
                }
            }
            
            if (Find.CurrentMap == null)
            {
                reason = "当前没有有效地图";
                return false;
            }
            
            return true;
        }
        
        private void StartDescentSequence(NarratorPersonaDef persona, bool isHostile)
        {
            isDescending = true;
            
            // ⭐ v2.0.0: 确保立绘打开并播放姿势动画
            if (!PortraitOverlaySystem.IsEnabled())
            {
                portraitWasOpen = false;
                PortraitOverlaySystem.Toggle(true);
            }
            else
            {
                portraitWasOpen = true;
            }
            
            // ⭐ v2.4.0: 播放降临开始音效
            PlayDescentStartSound(persona);
            
            // 开始姿势切换序列
            animationController.StartPostureSequence(isHostile, () =>
            {
                // 姿势动画完成后关闭立绘
                if (PortraitOverlaySystem.IsEnabled())
                {
                    PortraitOverlaySystem.Toggle(false);
                }
                
                // ⭐ v2.4.0: 播放降临着陆音效
                PlayDescentLandingSound(persona);
            });
            
            string animType = persona.descentAnimationType ?? "DropPod";
            currentAnimationProvider = DescentAnimationRegistry.GetProvider(animType) ?? new DefaultDropPodAnimationProvider();
            
            currentAnimationProvider.StartAnimation(
                Find.CurrentMap,
                targetDescentLocation,
                persona,
                isHostile,
                () => ExecutePendingSpawn()
            );
            
            Log.Message($"[NarratorDescentSystem] 开始降临序列: {persona.narratorName}");
        }
        
        /// <summary>
        /// ⭐ v2.4.0: 播放降临开始音效
        /// </summary>
        private void PlayDescentStartSound(NarratorPersonaDef persona)
        {
            try
            {
                // 优先使用人格定义的降临音效
                string soundDefName = persona.descentSound;
                if (string.IsNullOrEmpty(soundDefName))
                {
                    // 回退到默认音效
                    soundDefName = "PowerOn"; // RimWorld 内置音效
                }
                
                SoundDef soundDef = DefDatabase<SoundDef>.GetNamedSilentFail(soundDefName);
                if (soundDef != null)
                {
                    SoundStarter.PlayOneShotOnCamera(soundDef);
                    
                    if (Prefs.DevMode)
                    {
                        Log.Message($"[NarratorDescentSystem] 播放降临开始音效: {soundDefName}");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[NarratorDescentSystem] 播放降临开始音效失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// ⭐ v2.4.0: 播放降临着陆音效
        /// </summary>
        private void PlayDescentLandingSound(NarratorPersonaDef persona)
        {
            try
            {
                // 使用着陆音效（如果有定义）
                string soundDefName = "DropPod_Open"; // RimWorld 内置音效
                
                SoundDef soundDef = DefDatabase<SoundDef>.GetNamedSilentFail(soundDefName);
                if (soundDef != null && Find.CurrentMap != null)
                {
                    SoundStarter.PlayOneShot(soundDef, new TargetInfo(targetDescentLocation, Find.CurrentMap));
                    
                    // 同时播放视觉特效
                    FleckMaker.ThrowSmoke(targetDescentLocation.ToVector3Shifted(), Find.CurrentMap, 3f);
                    FleckMaker.ThrowDustPuff(targetDescentLocation, Find.CurrentMap, 2f);
                    
                    if (Prefs.DevMode)
                    {
                        Log.Message($"[NarratorDescentSystem] 播放降临着陆音效: {soundDefName}");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[NarratorDescentSystem] 播放降临着陆音效失败: {ex.Message}");
            }
        }
        
        private void ExecutePendingSpawn()
        {
            pendingSpawn = false;
            
            var manager = Current.Game?.GetComponent<NarratorManager>();
            var persona = manager?.GetCurrentPersona();
            
            if (persona != null)
            {
                // ⭐ v2.9.8: 传递 playerControlled 参数
                currentDescentPawn = DescentPawnSpawner.SpawnDescentPawn(
                    persona, 
                    lastDescentWasHostile, 
                    targetDescentLocation, 
                    Find.CurrentMap,
                    lastDescentPlayerControlled);
                currentCompanionPawn = null;
                
                if (currentDescentPawn != null)
                {
                    SendDescentLetter(persona, lastDescentWasHostile);
                    
                    // ⭐ v2.0.0: 不在这里关闭立绘，由姿势动画回调控制
                    // if (PortraitOverlaySystem.IsEnabled())
                    // {
                    //     portraitWasOpen = true;
                    //     PortraitOverlaySystem.Toggle(false);
                    // }
                }
            }
            
            isDescending = false;
            currentAnimationProvider = null;
        }

        private void SendDescentLetter(NarratorPersonaDef persona, bool isHostile)
        {
            string letterLabel = isHostile ? "叙事者降临（敌对）" : "叙事者降临";
            string letterText = isHostile 
                ? $"{persona.narratorName} 以敌对形态降临了！小心！" 
                : $"{persona.narratorName} 亲自降临到了殖民地。";
                
            Find.LetterStack.ReceiveLetter(letterLabel, letterText, 
                isHostile ? LetterDefOf.ThreatBig : LetterDefOf.PositiveEvent, 
                currentDescentPawn);
        }

        private void ForceDestroyDescentPawn(bool isCombatReason)
        {
            if (currentDescentPawn == null) return;
            
            try
            {
                float penaltyRate = DescentStateMonitor.GetPenaltyRate(isCombatReason);
                ApplyAffinityPenalty(penaltyRate, isCombatReason);
                
                if (currentDescentPawn != null && currentDescentPawn.Spawned && !currentDescentPawn.Destroyed)
                {
                    currentDescentPawn.Destroy(DestroyMode.Vanish);
                }
                currentDescentPawn = null;

                CompanionSpawner.DestroyCompanion(currentCompanionPawn);
                currentCompanionPawn = null;
                
                RestorePortraitPanel();
                
                var manager = Current.Game?.GetComponent<NarratorManager>();
                var persona = manager?.GetCurrentPersona();
                
                string reasonText = isCombatReason ? "在战斗中" : "因意外情况";
                Messages.Message(
                    $"{persona?.narratorName ?? "叙事者"} {reasonText}被迫离开了... (好感度 -{penaltyRate * 100:F0}%)",
                    isCombatReason ? MessageTypeDefOf.NegativeEvent : MessageTypeDefOf.CautionInput
                );
            }
            catch (Exception ex)
            {
                Log.Error($"[NarratorDescentSystem] 强制销毁失败: {ex}");
                currentDescentPawn = null;
            }
        }
        
        private void HandlePawnDestroyed()
        {
            float penaltyRate = DescentStateMonitor.GetPenaltyRate(wasInCombat);
            ApplyAffinityPenalty(penaltyRate, wasInCombat);
            
            currentDescentPawn = null;
            CompanionSpawner.DestroyCompanion(currentCompanionPawn);
            currentCompanionPawn = null;
            RestorePortraitPanel();
        }
        
        private void ApplyAffinityPenalty(float penaltyRate, bool isCombatReason)
        {
            var manager = Current.Game?.GetComponent<NarratorManager>();
            var agent = manager?.GetStorytellerAgent();
            if (agent == null) return;
            
            float currentAffinity = agent.GetAffinity();
            float penalty = -currentAffinity * penaltyRate;
            
            string reason = isCombatReason ? "降临形态战斗中被击败" : "降临形态因意外状况被迫终止";
            agent.ModifyAffinity(penalty, reason);
        }
        
        private void RestorePortraitPanel()
        {
            try
            {
                if (portraitWasOpen)
                {
                    PortraitOverlaySystem.Toggle(true);
                }
                
                var manager = Current.Game?.GetComponent<NarratorManager>();
                var persona = manager?.GetCurrentPersona();
                if (persona != null)
                {
                    PortraitLoader.LoadPortrait(persona);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[NarratorDescentSystem] 恢复立绘面板失败: {ex}");
            }
        }
        
        private void PlayReturnAnimation()
        {
            try
            {
                var manager = Current.Game?.GetComponent<NarratorManager>();
                var persona = manager?.GetCurrentPersona();
                if (persona == null) return;
                
                if (!string.IsNullOrEmpty(persona.descentSound))
                {
                    SoundDef soundDef = DefDatabase<SoundDef>.GetNamedSilentFail(persona.descentSound);
                    if (soundDef != null && currentDescentPawn != null)
                    {
                        SoundStarter.PlayOneShot(soundDef, new TargetInfo(currentDescentPawn.Position, Find.CurrentMap));
                    }
                }
                
                if (currentDescentPawn != null)
                {
                    FleckMaker.ThrowSmoke(currentDescentPawn.DrawPos, Find.CurrentMap, 2f);
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[NarratorDescentSystem] 播放回归动画失败: {ex.Message}");
            }
        }
        
        private IntVec3? SelectDescentLocation()
        {
            Map map = Find.CurrentMap;
            if (map == null) return null;
            
            IntVec3 center = map.Center;
            
            if (CellFinder.TryFindRandomCellNear(center, map, DESCENT_RANGE, 
                c => !c.Fogged(map) && c.Standable(map) && c.GetRoof(map) == null, 
                out IntVec3 result))
            {
                return result;
            }
            
            if (CellFinderLoose.TryGetRandomCellWith(
                c => !c.Fogged(map) && c.Standable(map) && c.GetRoof(map) == null, 
                map, 1000, out result))
            {
                return result;
            }
            
            return null;
        }
        
        private bool HasDescentPawnOnMap(NarratorPersonaDef persona)
        {
            if (currentDescentPawn != null && !currentDescentPawn.Destroyed)
            {
                return true;
            }
            
            if (Find.CurrentMap != null)
            {
                var existing = Find.CurrentMap.mapPawns.AllPawnsSpawned
                    .FirstOrDefault(p => p.kindDef.defName == persona.descentPawnKind);
                    
                if (existing != null)
                {
                    currentDescentPawn = existing;
                    return true;
                }
            }
            
            return false;
        }
        
        private void ValidateStateConsistency()
        {
            if (isDescending && currentDescentPawn == null && !pendingSpawn)
            {
                isDescending = false;
            }
            
            if (currentDescentPawn != null && (currentDescentPawn.Destroyed || currentDescentPawn.Dead))
            {
                currentDescentPawn = null;
                currentCompanionPawn = null;
            }
        }
    }
}
