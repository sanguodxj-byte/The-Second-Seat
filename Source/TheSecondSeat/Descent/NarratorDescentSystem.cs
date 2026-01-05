using System;
using System.Collections.Generic;
using System.IO;
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
    /// ⭐ v1.6.80: 叙事者降临系统 - 完全通用化执行器
    ///
    /// 新增功能：
    /// - 降临时强制关闭立绘面板，使用默认头像
    /// - 叙事者回归后自动恢复立绘和头像
    /// - 叙事者自主判断回归时机（基于性格）
    /// - ⭐ v1.6.80: 巨龙飞掠动画（替代空投仓）
    /// - ⭐ v1.6.80: 实体状态监控（睡眠/昏迷/束缚/死亡自动销毁）
    /// - ⭐ v1.6.80: 好感度惩罚机制
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
        
        // ⭐ v1.6.72: 新增回归相关字段
        private Pawn? currentDescentPawn = null;     // 当前降临的实体
        private Pawn? currentCompanionPawn = null;   // ⭐ v1.8.5: 追踪伴随生物（如龙）
        private int descentStartTick = 0;            // 降临开始时间
        private bool portraitWasOpen = false;         // 降临前立绘是否打开
        private int lastReturnCheckTick = 0;          // 上次检查回归的时间
        private const int RETURN_CHECK_INTERVAL = 60;  // 每1秒检查一次状态（更频繁）
        
        // ⭐ v1.6.81: 当前活动的动画提供者
        private IDescentAnimationProvider currentAnimationProvider = null;

        /// <summary>
        /// ⭐ 判断是否处于降临活动状态（包括动画中、等待生成中、实体存在中）
        /// 供 UI 系统调用以决定是否隐藏立绘
        /// </summary>
        public bool IsDescentActive => isDescending || currentDescentPawn != null || pendingSpawn || currentAnimationProvider != null;
        
        // ⭐ v1.6.80: 降临原因追踪（用于好感度惩罚）
        private bool wasInCombat = false;
        
        // ==================== 配置参数 ====================
        
        public const float MIN_AFFINITY_FOR_FRIENDLY = 40f;
        public const int DESCENT_RANGE = 30;
        
        // ⭐ v1.6.72: 回归判断参数
        private const int MIN_DESCENT_DURATION = 18000;   // 最短降临时间 5分钟
        private const int MAX_DESCENT_DURATION = 216000;  // 最长降临时间 1小时
        
        // ⭐ v1.6.80: 好感度惩罚参数
        private const float AFFINITY_PENALTY_COMBAT = 0.10f;     // 战斗原因销毁：扣除10%
        private const float AFFINITY_PENALTY_NON_COMBAT = 0.50f; // 非战斗原因销毁：扣除50%
        
        // ==================== 构造函数 ====================
        
        public NarratorDescentSystem(Game game) : base()
        {
            instance = this;
        }
        
        // ==================== ⭐ 公共API ====================
        
        /// <summary>
        /// ⭐ 检查是否可以降临（用于命令预检查）
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

            // 1. 检查是否正在进行中
            if (IsDescentActive)
            {
                reason = "降临正在进行中或实体已存在";
                return false;
            }

            // 2. 检查冷却
            int ticksSinceLastDescent = Find.TickManager.TicksGame - lastDescentTick;
            if (ticksSinceLastDescent < DESCENT_COOLDOWN_TICKS)
            {
                int remainingMinutes = (DESCENT_COOLDOWN_TICKS - ticksSinceLastDescent) / 3600;
                reason = $"冷却中，还需 {remainingMinutes} 分钟";
                return false;
            }
            
            // 3. 检查是否已存在
            if (HasDescentPawnOnMap(persona))
            {
                reason = $"{persona.narratorName} 已经在场";
                return false;
            }
            
            // 3. 检查是否支持降临
            if (string.IsNullOrEmpty(persona.descentPawnKind))
            {
                reason = $"{persona.narratorName} 不支持实体化降临";
                return false;
            }

            reason = "";
            return true;
        }

        /// <summary>
        /// ⭐ 触发降临（完全通用化）
        /// </summary>
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
            descentStartTick = Find.TickManager.TicksGame;
            
            // ⭐ v1.7.2: 仅记录状态，不要关闭！动画结束后再关闭
            bool wasVisible = PortraitOverlaySystem.IsEnabled();
            portraitWasOpen = wasVisible;
            
            // ⭐ 修复：不要在这里关闭面板，否则看不到姿态动画
            // 面板将在 PlayPostureAnimation 的回调中关闭
            
            // 5. ⭐ 开始降临序列（通用化）
            StartDescentSequence(persona, isHostile);
            
            return true;
        }
        
        /// <summary>
        /// ⭐ v1.6.72: 叙事者主动回归
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
                // 1. 播放回归动画（逆向播放降临动画）
                PlayReturnAnimation();
                
                // 2. 移除降临实体
                currentDescentPawn.Destroy(DestroyMode.Vanish);
                currentDescentPawn = null;

                // ⭐ v1.8.5: 移除伴随生物
                if (currentCompanionPawn != null && !currentCompanionPawn.Destroyed)
                {
                    currentCompanionPawn.Destroy(DestroyMode.Vanish);
                }
                currentCompanionPawn = null;
                
                // 3. 恢复立绘面板
                RestorePortraitPanel();
                
                // 4. 显示回归消息
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
        
        // ==================== ⭐ v1.6.72: 新增方法 ====================
        
        /// <summary>
        /// ⭐ v1.6.81: 每帧检查是否应该回归或强制销毁
        /// ⭐ v1.6.83: 添加延迟生成检查
        /// </summary>
        public override void GameComponentTick()
        {
            base.GameComponentTick();
            
            // ⭐ v1.6.81: 更新当前动画提供者
            if (currentAnimationProvider?.IsPlaying == true)
            {
                currentAnimationProvider.Update(Time.deltaTime);
            }
            
            // ⭐ v1.6.83: 检查是否需要执行延迟生成
            if (pendingSpawn)
            {
                // DEBUG LOG
                if (Find.TickManager.TicksGame % 60 == 0) // Log every second
                {
                    Log.Message($"[NarratorDescentSystem] Pending Spawn Check: CurrentTick={Find.TickManager.TicksGame}, TargetTick={spawnDelayEndTick}, Remaining={spawnDelayEndTick - Find.TickManager.TicksGame}");
                }

                if (Find.TickManager.TicksGame >= spawnDelayEndTick)
                {
                    Log.Message($"[NarratorDescentSystem] Triggering ExecutePendingSpawn at Tick {Find.TickManager.TicksGame}");
                    ExecutePendingSpawn();
                }
            }
            
            // 没有降临实体，跳过
            if (currentDescentPawn == null)
            {
                return;
            }
            
            // ⭐ v1.6.80: 检查实体是否已被外部销毁
            if (currentDescentPawn.Destroyed)
            {
                HandlePawnDestroyed(wasInCombat);
                return;
            }
            
            // ⭐ v1.6.80: 检查实体是否陷入不可行动状态 (每帧检查，确保立即响应)
            // ⭐ v1.8.5: 同时检查伴随生物状态
            if (ShouldForceDestroy(out bool isCombatReason))
            {
                ForceDestroyDescentPawn(isCombatReason);
                return;
            }

            // 检查间隔
            int currentTick = Find.TickManager.TicksGame;
            if (currentTick - lastReturnCheckTick < RETURN_CHECK_INTERVAL)
            {
                return;
            }
            
            lastReturnCheckTick = currentTick;
            
            // ⭐ v1.6.80: 追踪战斗状态
            UpdateCombatStatus();
            
            // 检查是否应该回归
            if (ShouldReturn())
            {
                TriggerReturn();
            }
        }
        
        /// <summary>
        /// ⭐ v1.6.80: 检查是否应该强制销毁降临实体
        /// ⭐ v1.8.5: 增加对伴随生物（如龙）的状态检查
        /// </summary>
        private bool ShouldForceDestroy(out bool isCombatReason)
        {
            isCombatReason = false;
            
            // 1. 检查主体状态
            if (CheckPawnStatus(currentDescentPawn, ref isCombatReason, "主体"))
            {
                return true;
            }

            // 2. 检查伴随生物状态 (如果存在)
            if (currentCompanionPawn != null && currentCompanionPawn.Spawned)
            {
                if (CheckPawnStatus(currentCompanionPawn, ref isCombatReason, "伴随生物"))
                {
                    return true;
                }
            }
            
            return false;
        }

        /// <summary>
        /// ⭐ v1.8.5: 通用实体状态检查
        /// </summary>
        private bool CheckPawnStatus(Pawn pawn, ref bool isCombatReason, string label)
        {
            if (pawn == null || !pawn.Spawned) return false;

            // 检查死亡
            if (pawn.Dead)
            {
                isCombatReason = wasInCombat;
                Log.Message($"[NarratorDescentSystem] {label}死亡，战斗状态: {wasInCombat}");
                return true;
            }
            
            // 检查睡眠
            if (pawn.CurJob?.def == JobDefOf.LayDown ||
                pawn.jobs?.curDriver?.asleep == true)
            {
                // 伴随生物睡觉不触发回归
                if (label == "伴随生物") return false;

                isCombatReason = false;
                Log.Message($"[NarratorDescentSystem] {label}陷入睡眠状态");
                return true;
            }
            
            // 检查昏迷（心智状态）
            if (pawn.InMentalState ||
                pawn.health?.Downed == true)
            {
                isCombatReason = wasInCombat;
                Log.Message($"[NarratorDescentSystem] {label}昏迷/倒地，战斗状态: {wasInCombat}");
                return true;
            }
            
            // 检查束缚
            if (pawn.IsPrisoner ||
                pawn.guest?.IsPrisoner == true)
            {
                isCombatReason = false;
                Log.Message($"[NarratorDescentSystem] {label}被束缚/囚禁");
                return true;
            }
            
            // 检查无法行动
            if (!pawn.health?.capacities?.CapableOf(PawnCapacityDefOf.Moving) == true)
            {
                isCombatReason = wasInCombat;
                Log.Message($"[NarratorDescentSystem] {label}无法移动，战斗状态: {wasInCombat}");
                return true;
            }

            return false;
        }
        
        /// <summary>
        /// ⭐ v1.6.80: 强制销毁降临实体并应用好感度惩罚
        /// </summary>
        private void ForceDestroyDescentPawn(bool isCombatReason)
        {
            if (currentDescentPawn == null)
            {
                return;
            }
            
            try
            {
                // 1. 计算好感度惩罚
                float penaltyRate = isCombatReason ? AFFINITY_PENALTY_COMBAT : AFFINITY_PENALTY_NON_COMBAT;
                ApplyAffinityPenalty(penaltyRate, isCombatReason);
                
                // 2. 销毁实体（如果还存在）
                if (currentDescentPawn != null && currentDescentPawn.Spawned && !currentDescentPawn.Destroyed)
                {
                    currentDescentPawn.Destroy(DestroyMode.Vanish);
                }
                currentDescentPawn = null;

                // ⭐ v1.8.5: 销毁伴随生物
                if (currentCompanionPawn != null && currentCompanionPawn.Spawned && !currentCompanionPawn.Destroyed)
                {
                    currentCompanionPawn.Destroy(DestroyMode.Vanish);
                }
                currentCompanionPawn = null;
                
                // 3. 恢复立绘
                RestorePortraitPanel();
                
                // 4. 显示消息
                var manager = Current.Game?.GetComponent<NarratorManager>();
                var persona = manager?.GetCurrentPersona();
                
                string reasonText = isCombatReason ? "在战斗中" : "因意外情况";
                string penaltyText = $"(好感度 -{penaltyRate * 100:F0}%)";
                
                Messages.Message(
                    $"{persona?.narratorName ?? "叙事者"} {reasonText}被迫离开了... {penaltyText}",
                    isCombatReason ? MessageTypeDefOf.NegativeEvent : MessageTypeDefOf.CautionInput
                );
                
                Log.Message($"[NarratorDescentSystem] 降临实体被强制销毁，原因: {(isCombatReason ? "战斗" : "非战斗")}，惩罚: {penaltyRate * 100:F0}%");
            }
            catch (Exception ex)
            {
                Log.Error($"[NarratorDescentSystem] 强制销毁失败: {ex}");
                currentDescentPawn = null;
            }
        }
        
        /// <summary>
        /// ⭐ v1.6.80: 处理Pawn被外部销毁的情况
        /// </summary>
        private void HandlePawnDestroyed(bool wasInCombat)
        {
            // 应用惩罚
            float penaltyRate = wasInCombat ? AFFINITY_PENALTY_COMBAT : AFFINITY_PENALTY_NON_COMBAT;
            ApplyAffinityPenalty(penaltyRate, wasInCombat);
            
            currentDescentPawn = null;
            
            // ⭐ v1.8.5: 清理伴随生物
            if (currentCompanionPawn != null && !currentCompanionPawn.Destroyed)
            {
                currentCompanionPawn.Destroy(DestroyMode.Vanish);
            }
            currentCompanionPawn = null;

            // 恢复立绘
            RestorePortraitPanel();
            
            Log.Message($"[NarratorDescentSystem] 检测到降临实体被外部销毁，应用惩罚: {penaltyRate * 100:F0}%");
        }
        
        /// <summary>
        /// ⭐ v1.6.80: 应用好感度惩罚
        /// ⭐ v1.6.82: 修复 - 从 NarratorManager 获取 StorytellerAgent
        /// </summary>
        private void ApplyAffinityPenalty(float penaltyRate, bool isCombatReason)
        {
            var manager = Current.Game?.GetComponent<NarratorManager>();
            var agent = manager?.GetStorytellerAgent();
            if (agent == null)
            {
                Log.Warning("[NarratorDescentSystem] 无法获取 StorytellerAgent，跳过好感度惩罚");
                return;
            }
            
            float currentAffinity = agent.GetAffinity();
            float penalty = currentAffinity * penaltyRate;
            
            // 确保惩罚为负数
            if (penalty > 0) penalty = -penalty;
            
            string reason = isCombatReason ? "降临形态战斗中被击败" : "降临形态因意外状况被迫终止";
            agent.ModifyAffinity(penalty, reason);
            
            Log.Message($"[NarratorDescentSystem] 应用好感度惩罚: {penalty:F1} (原因: {reason})");
        }
        
        /// <summary>
        /// ⭐ v1.6.80: 更新战斗状态追踪
        /// </summary>
        private void UpdateCombatStatus()
        {
            if (currentDescentPawn == null || !currentDescentPawn.Spawned)
            {
                return;
            }
            
            // 检查是否正在战斗
            bool currentlyInCombat =
                currentDescentPawn.CurJob?.def == JobDefOf.AttackMelee ||
                currentDescentPawn.CurJob?.def == JobDefOf.AttackStatic ||
                currentDescentPawn.mindState?.enemyTarget != null ||
                currentDescentPawn.stances?.curStance is Stance_Warmup;
            
            // 一旦进入战斗，保持标记
            if (currentlyInCombat)
            {
                wasInCombat = true;
            }
        }
        
        /// <summary>
        /// ⭐ v1.6.72: 判断叙事者是否应该回归
        /// </summary>
        private bool ShouldReturn()
        {
            if (currentDescentPawn == null) return false;
            
            int currentTick = Find.TickManager.TicksGame;
            int elapsedTicks = currentTick - descentStartTick;
            
            // 1. 检查最短降临时间（必须至少停留5分钟）
            if (elapsedTicks < MIN_DESCENT_DURATION)
            {
                return false;
            }
            
            // 2. 检查最长降临时间（超过1小时强制回归）
            if (elapsedTicks > MAX_DESCENT_DURATION)
            {
                Log.Message("[NarratorDescentSystem] 降临时间过长，强制回归");
                return true;
            }
            
            // 3. 获取人格配置
            var manager = Current.Game?.GetComponent<NarratorManager>();
            var persona = manager?.GetCurrentPersona();
            if (persona == null) return true;
            
            // 4. 基于性格判断回归时机
            return ShouldReturnBasedOnPersonality(persona, elapsedTicks);
        }
        
        /// <summary>
        /// ⭐ v1.6.72: 基于性格判断是否回归
        /// </summary>
        private bool ShouldReturnBasedOnPersonality(NarratorPersonaDef persona, int elapsedTicks)
        {
            // ⭐ v1.6.82: 修复 - 从 NarratorManager 获取好感度
            var manager = Current.Game?.GetComponent<NarratorManager>();
            var agent = manager?.GetStorytellerAgent();
            float affinity = agent?.GetAffinity() ?? 0f;
            
            // 基础停留时间（30分钟）
            int baseStayDuration = 108000;
            
            // ==================== 性格影响停留时间 ====================
            
            // 社交型人格：喜欢和殖民者相处，停留更久
            if (HasPersonalityTag(persona, "社交", "外向", "友善"))
            {
                baseStayDuration = (int)(baseStayDuration * 1.5f); // 延长50%
            }
            
            // 高傲/独立型人格：不喜欢长时间停留
            if (HasPersonalityTag(persona, "高傲", "冷淡", "独立"))
            {
                baseStayDuration = (int)(baseStayDuration * 0.7f); // 缩短30%
            }
            
            // 好奇型人格：想看看殖民地
            if (HasPersonalityTag(persona, "好奇", "活泼", "调皮"))
            {
                baseStayDuration = (int)(baseStayDuration * 1.3f); // 延长30%
            }
            
            // 懒惰/悠闲型：可能蹭顿饭再走
            if (HasPersonalityTag(persona, "懒惰", "悠闲", "随性"))
            {
                // 检查是否有食物
                if (HasGoodFood())
                {
                    baseStayDuration = (int)(baseStayDuration * 1.8f); // 大幅延长
                }
            }
            
            // ==================== 好感度影响 ====================
            
            if (affinity > 80f)
            {
                // 高好感度：更愿意停留
                baseStayDuration = (int)(baseStayDuration * 1.2f);
            }
            else if (affinity < 30f)
            {
                // 低好感度：快速离开
                baseStayDuration = (int)(baseStayDuration * 0.5f);
            }
            
            // ==================== 随机因素 ====================
            
            // 添加10-20%的随机波动
            float randomFactor = Rand.Range(0.9f, 1.2f);
            baseStayDuration = (int)(baseStayDuration * randomFactor);
            
            // 如果已经超过计算出的停留时间，应该回归
            if (elapsedTicks > baseStayDuration)
            {
                // 10%概率继续停留（"再待一会儿"）
                if (Rand.Chance(0.1f))
                {
                    Log.Message($"[NarratorDescentSystem] {persona.narratorName} 决定再待一会儿");
                    descentStartTick += 36000; // 延长5分钟
                    return false;
                }
                
                return true;
            }
            
            return false;
        }
        
        /// <summary>
        /// ⭐ v1.6.72: 检查人格是否有特定标签
        /// </summary>
        private bool HasPersonalityTag(NarratorPersonaDef persona, params string[] tags)
        {
            if (persona.toneTags == null) return false;
            
            foreach (var tag in tags)
            {
                if (persona.toneTags.Any(t => t.Contains(tag)))
                {
                    return true;
                }
            }
            
            return false;
        }
        
        /// <summary>
        /// ⭐ v1.6.72: 检查是否有好吃的食物
        /// </summary>
        private bool HasGoodFood()
        {
            var map = Find.CurrentMap;
            if (map == null) return false;
            
            // 检查是否有精致餐食或更好的食物
            return map.listerThings.ThingsInGroup(ThingRequestGroup.FoodSourceNotPlantOrTree)
                .Any(t => t.def.IsNutritionGivingIngestible && 
                         t.TryGetComp<CompIngredients>() != null);
        }
        
        /// <summary>
        /// ⭐ v1.6.72: 恢复立绘面板
        /// </summary>
        private void RestorePortraitPanel()
        {
            try
            {
                if (portraitWasOpen)
                {
                    PortraitOverlaySystem.Toggle(true); // 恢复立绘面板
                    Log.Message("[NarratorDescentSystem] 立绘面板已恢复");
                }
                
                // 恢复头像
                var manager = Current.Game?.GetComponent<NarratorManager>();
                var persona = manager?.GetCurrentPersona();
                if (persona != null)
                {
                    // 重新加载头像
                    PortraitLoader.LoadPortrait(persona);
                    Log.Message("[NarratorDescentSystem] 头像已恢复");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[NarratorDescentSystem] 恢复立绘面板失败: {ex}");
            }
        }
        
        /// <summary>
        /// ⭐ v1.6.72: 播放回归动画（降临动画逆向播放）
        /// </summary>
        private void PlayReturnAnimation()
        {
            try
            {
                var manager = Current.Game?.GetComponent<NarratorManager>();
                var persona = manager?.GetCurrentPersona();
                if (persona == null) return;
                
                // 播放回归音效
                string returnSound = persona.descentSound; // 使用相同音效
                if (!string.IsNullOrEmpty(returnSound))
                {
                    SoundDef soundDef = DefDatabase<SoundDef>.GetNamedSilentFail(returnSound);
                    if (soundDef != null && currentDescentPawn != null)
                    {
                        SoundStarter.PlayOneShot(soundDef, new TargetInfo(currentDescentPawn.Position, Find.CurrentMap));
                    }
                }
                
                // 播放特效（光芒消失）
                if (currentDescentPawn != null)
                {
                    // TODO: 添加回归特效（光柱上升、身影消失）
                    FleckMaker.ThrowSmoke(currentDescentPawn.DrawPos, Find.CurrentMap, 2f);
                }
                
                Log.Message("[NarratorDescentSystem] 播放回归动画");
            }
            catch (Exception ex)
            {
                Log.Warning($"[NarratorDescentSystem] 播放回归动画失败: {ex.Message}");
            }
        }
        
        // ==================== 私有方法 - 通用化逻辑 ====================
        
        /// <summary>
        /// ⭐ 检查是否可以触发降临（通用检查）
        /// ⭐ v1.6.80: 修复好感度检查绕过漏洞
        /// </summary>
        private bool CanTriggerDescent(bool isHostile, NarratorPersonaDef persona, out string reason)
        {
            reason = "";
            
            // 1. 检查是否正在进行中
            if (IsDescentActive)
            {
                reason = "降临正在进行中或实体已存在";
                return false;
            }

            // 2. 检查冷却时间
            int ticksSinceLastDescent = Find.TickManager.TicksGame - lastDescentTick;
            if (ticksSinceLastDescent < DESCENT_COOLDOWN_TICKS)
            {
                int remainingMinutes = (DESCENT_COOLDOWN_TICKS - ticksSinceLastDescent) / 3600;
                reason = $"冷却中，还需 {remainingMinutes} 分钟";
                return false;
            }
            
            // 3. 检查是否已有降临实体
            if (HasDescentPawnOnMap(persona))
            {
                reason = $"{persona.narratorName} 已经在场，无法重复降临";
                return false;
            }
            
            // 3. 检查好感度（仅友好模式）
            // ⭐ v1.6.82: 修复 - 从 NarratorManager 获取 StorytellerAgent
            if (!isHostile)
            {
                var manager = Current.Game?.GetComponent<NarratorManager>();
                var agent = manager?.GetStorytellerAgent();
                if (agent == null)
                {
                    reason = "无法获取好感度系统，降临失败";
                    Log.Warning("[NarratorDescentSystem] StorytellerAgent 为 null，拒绝友好降临");
                    return false;
                }
                
                float affinity = agent.GetAffinity();
                if (affinity < MIN_AFFINITY_FOR_FRIENDLY)
                {
                    reason = $"友好降临需要好感度 {MIN_AFFINITY_FOR_FRIENDLY}+，当前 {affinity:F0}";
                    return false;
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
        /// ⭐ 开始降临序列（通用化）
        /// </summary>
        private void StartDescentSequence(NarratorPersonaDef persona, bool isHostile)
        {
            isDescending = true;
            
            // ⭐ v1.6.81: 使用动画提供者
            // 使用注册表获取动画提供者
            string animType = persona.descentAnimationType;
            if (string.IsNullOrEmpty(animType))
            {
                animType = "DropPod";
            }
            
            currentAnimationProvider = DescentAnimationRegistry.GetProvider(animType);
            
            // 如果获取失败，使用默认提供者
            if (currentAnimationProvider == null)
            {
                currentAnimationProvider = new DefaultDropPodAnimationProvider();
            }
            
            // 开始动画
            currentAnimationProvider.StartAnimation(
                Find.CurrentMap,
                targetDescentLocation,
                persona,
                isHostile,
                () => {
                    // 动画完成回调
                    ExecutePendingSpawn();
                }
            );
            
            Log.Message($"[NarratorDescentSystem] 开始降临序列: {persona.narratorName}, 动画: {currentAnimationProvider.GetType().Name}");
        }

        // ==================== 延迟生成机制 ====================
        
        private bool pendingSpawn = false;
        private int spawnDelayEndTick = 0;
        
        /// <summary>
        /// ⭐ v1.6.83: 安排延迟生成（供动画调用）
        /// </summary>
        public void ScheduleSpawn(int delayTicks)
        {
            pendingSpawn = true;
            spawnDelayEndTick = Find.TickManager.TicksGame + delayTicks;
            Log.Message($"[NarratorDescentSystem] 安排延迟生成: {delayTicks} ticks 后 (Target: {spawnDelayEndTick})");
        }
        
        /// <summary>
        /// ⭐ v1.6.83: 执行挂起的生成任务
        /// </summary>
        private void ExecutePendingSpawn()
        {
            pendingSpawn = false;
            
            var manager = Current.Game?.GetComponent<NarratorManager>();
            var persona = manager?.GetCurrentPersona();
            
            if (persona != null)
            {
                SpawnDescentPawn(persona, lastDescentWasHostile);
            }
            
            // 动画结束清理
            isDescending = false;
            currentAnimationProvider = null;
        }

        /// <summary>
        /// ⭐ 生成降临实体
        /// </summary>
        private void SpawnDescentPawn(NarratorPersonaDef persona, bool isHostile)
        {
            if (targetDescentLocation == IntVec3.Invalid || Find.CurrentMap == null)
            {
                Log.Error("[NarratorDescentSystem] 降临地点无效");
                return;
            }
            
            PawnKindDef pawnKind = DefDatabase<PawnKindDef>.GetNamedSilentFail(persona.descentPawnKind);
            if (pawnKind == null)
            {
                Log.Error($"[NarratorDescentSystem] 找不到 PawnKind: {persona.descentPawnKind}");
                return;
            }
            
            // 1. 生成实体
            Faction faction = isHostile ? Faction.OfAncientsHostile : Faction.OfPlayer;
            // 如果是敌对，确保有敌对派系
            if (isHostile && faction == null)
            {
                faction = Find.FactionManager.FirstFactionOfDef(FactionDefOf.AncientsHostile) 
                         ?? Find.FactionManager.AllFactions.FirstOrDefault(f => f.HostileTo(Faction.OfPlayer));
            }

            Pawn pawn = PawnGenerator.GeneratePawn(pawnKind, faction);
            
            // 2. ⭐ 应用叙事者特性（名字、外观等）
            ApplyPersonaToPawn(pawn, persona);
            
            // 3. 投放到地图
            GenSpawn.Spawn(pawn, targetDescentLocation, Find.CurrentMap);
            
            // 4. 设置状态
            currentDescentPawn = pawn;
            currentCompanionPawn = null; // 重置伴随生物
            
            // 5. 发送通知
            string letterLabel = isHostile ? "叙事者降临（敌对）" : "叙事者降临";
            string letterText = isHostile 
                ? $"{persona.narratorName} 以敌对形态降临了！小心！" 
                : $"{persona.narratorName} 亲自降临到了殖民地。";
                
            Find.LetterStack.ReceiveLetter(letterLabel, letterText, 
                isHostile ? LetterDefOf.ThreatBig : LetterDefOf.PositiveEvent, 
                pawn);
                
            // 6. ⭐ v1.6.80: 强制关闭立绘面板（如果开启）
            if (PortraitOverlaySystem.IsEnabled())
            {
                portraitWasOpen = true;
                PortraitOverlaySystem.Toggle(false);
            }
            
            // 7. ⭐ v1.8.5: 生成伴随生物（如龙）
            SpawnCompanion(persona, pawn, isHostile);
            
            Log.Message($"[NarratorDescentSystem] {persona.narratorName} 实体化完成");
        }
        
        /// <summary>
        /// ⭐ v1.8.5: 生成伴随生物
        /// </summary>
        private void SpawnCompanion(NarratorPersonaDef persona, Pawn master, bool isHostile)
        {
            // 检查是否有伴随生物配置
            // 这里我们假设伴随生物的 PawnKind 定义在 persona 中，或者硬编码检查 Sideria
            // 目前 Sideria 是通过 Sideria_DescentRace 自动生成的吗？
            // 不，XML中 Sideria_DescentRace 没有自动生成龙的逻辑，龙是独立的 PawnKind (Sideria_Dragon)
            
            // ⭐ v1.8.6: 使用 companionPawnKind 字段读取伴随生物配置
            if (!string.IsNullOrEmpty(persona.companionPawnKind))
            {
                PawnKindDef companionKind = DefDatabase<PawnKindDef>.GetNamedSilentFail(persona.companionPawnKind);
                if (companionKind != null)
                {
                    Pawn companion = PawnGenerator.GeneratePawn(companionKind, master.Faction);
                    
                    // 放置在主人附近
                    IntVec3 loc = CellFinder.RandomClosewalkCellNear(master.Position, master.Map, 3, null);
                    GenSpawn.Spawn(companion, loc, master.Map);
                    
                    // 设置关系（如果是动物）
                    if (companion.training != null)
                    {
                        companion.training.SetWantedRecursive(TrainableDefOf.Tameness, true);
                        companion.training.Train(TrainableDefOf.Tameness, master, true);
                    }
                    
                    // 记录伴随生物
                    currentCompanionPawn = companion;
                    Log.Message($"[NarratorDescentSystem] 伴随生物 {companion.Name} 已生成");
                }
            }
        }
        
        /// <summary>
        /// ⭐ 将叙事者设定应用到 Pawn
        /// </summary>
        private void ApplyPersonaToPawn(Pawn pawn, NarratorPersonaDef persona)
        {
            if (pawn == null || persona == null) return;
            
            // 1. 名字
            pawn.Name = new NameTriple(persona.narratorName, persona.narratorName, persona.narratorName);
            
            // 2. 技能（可选：根据叙事者风格调整技能）
            // ...
        }
        
        /// <summary>
        /// ⭐ 选择合适的降临地点
        /// </summary>
        private IntVec3? SelectDescentLocation()
        {
            Map map = Find.CurrentMap;
            if (map == null) return null;
            
            // 优先选择殖民地中心附近的空地
            IntVec3 center = map.Center;
            
            // 尝试找到一个安全的着陆点
            if (CellFinder.TryFindRandomCellNear(center, map, DESCENT_RANGE, 
                c => !c.Fogged(map) && c.Standable(map) && c.GetRoof(map) == null, 
                out IntVec3 result))
            {
                return result;
            }
            
            // 如果找不到，尝试任意无屋顶区域
            if (CellFinderLoose.TryGetRandomCellWith(
                c => !c.Fogged(map) && c.Standable(map) && c.GetRoof(map) == null, 
                map, 1000, out result))
            {
                return result;
            }
            
            return null;
        }
        
        /// <summary>
        /// ⭐ 检查地图上是否已有该叙事者的实体
        /// </summary>
        private bool HasDescentPawnOnMap(NarratorPersonaDef persona)
        {
            if (currentDescentPawn != null && !currentDescentPawn.Destroyed)
            {
                return true;
            }
            
            // 双重检查：遍历地图上的 Pawn
            // (防止存档加载后引用丢失)
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
        
        // ==================== 数据保存 ====================
        
        public override void ExposeData()
        {
            base.ExposeData();
            
            Scribe_Values.Look(ref isDescending, "isDescending", false);
            Scribe_Values.Look(ref lastDescentWasHostile, "lastDescentWasHostile", false);
            Scribe_Values.Look(ref lastDescentTick, "lastDescentTick", 0);
            Scribe_Values.Look(ref targetDescentLocation, "targetDescentLocation");
            
            Scribe_References.Look(ref currentDescentPawn, "currentDescentPawn");
            Scribe_References.Look(ref currentCompanionPawn, "currentCompanionPawn"); // ⭐ v1.8.5
            
            Scribe_Values.Look(ref descentStartTick, "descentStartTick", 0);
            Scribe_Values.Look(ref portraitWasOpen, "portraitWasOpen", false);
            
            // ⭐ v1.6.83: 保存延迟生成状态
            Scribe_Values.Look(ref pendingSpawn, "pendingSpawn", false);
            Scribe_Values.Look(ref spawnDelayEndTick, "spawnDelayEndTick", 0);
            
            // 如果正在降临中加载存档，重置状态以防卡死
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                ValidateStateConsistency();
            }
        }
        
        /// <summary>
        /// ⭐ 验证状态一致性（防止坏档）
        /// </summary>
        private void ValidateStateConsistency()
        {
            // 如果标记为正在降临，但没有实体且没有动画，重置
            if (isDescending && currentDescentPawn == null && !pendingSpawn)
            {
                isDescending = false;
            }
            
            // 如果实体已死亡或消失，清理引用
            if (currentDescentPawn != null && (currentDescentPawn.Destroyed || currentDescentPawn.Dead))
            {
                currentDescentPawn = null;
                // ⭐ v1.8.5: 同时清理伴随生物引用
                currentCompanionPawn = null;
            }
        }
        
        /// <summary>
        /// ⭐ 获取当前降临实体
        /// </summary>
        public Pawn? GetDescentPawn()
        {
            return currentDescentPawn;
        }

        /// <summary>
        /// ⭐ 获取冷却剩余时间（测试用）
        /// </summary>
        public string GetCooldownRemaining()
        {
            int ticksSinceLastDescent = Find.TickManager.TicksGame - lastDescentTick;
            if (ticksSinceLastDescent >= DESCENT_COOLDOWN_TICKS)
            {
                return "Ready";
            }
            
            int remainingTicks = DESCENT_COOLDOWN_TICKS - ticksSinceLastDescent;
            return remainingTicks.ToStringTicksToPeriod();
        }
    }
}
