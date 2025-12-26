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
    /// ⭐ v1.6.72: 叙事者降临系统 - 完全通用化执行器
    /// 
    /// 新增功能：
    /// - 降临时强制关闭立绘面板，使用默认头像
    /// - 叙事者回归后自动恢复立绘和头像
    /// - 叙事者自主判断回归时机（基于性格）
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
        private int descentStartTick = 0;            // 降临开始时间
        private bool portraitWasOpen = false;         // 降临前立绘是否打开
        private int lastReturnCheckTick = 0;          // 上次检查回归的时间
        private const int RETURN_CHECK_INTERVAL = 600; // 每10秒检查一次回归
        
        // ==================== 配置参数 ====================
        
        public const float MIN_AFFINITY_FOR_FRIENDLY = 40f;
        public const int DESCENT_RANGE = 30;
        
        // ⭐ v1.6.72: 回归判断参数
        private const int MIN_DESCENT_DURATION = 18000;   // 最短降临时间 5分钟
        private const int MAX_DESCENT_DURATION = 216000;  // 最长降临时间 1小时
        
        // ==================== 构造函数 ====================
        
        public NarratorDescentSystem(Game game) : base()
        {
            instance = this;
        }
        
        // ==================== ⭐ 公共API ====================
        
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
            
            // ⭐ v1.6.72: 记录立绘状态并关闭
            bool wasVisible = PortraitOverlaySystem.IsEnabled();
            portraitWasOpen = wasVisible;
            
            if (wasVisible)
            {
                PortraitOverlaySystem.Toggle(false); // 关闭立绘面板
                Log.Message("[NarratorDescentSystem] 立绘面板已关闭");
            }
            
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
        /// ⭐ v1.6.72: 每帧检查是否应该回归
        /// </summary>
        public override void GameComponentTick()
        {
            base.GameComponentTick();
            
            // 没有降临实体，跳过
            if (currentDescentPawn == null || !currentDescentPawn.Spawned)
            {
                return;
            }
            
            // 检查间隔
            int currentTick = Find.TickManager.TicksGame;
            if (currentTick - lastReturnCheckTick < RETURN_CHECK_INTERVAL)
            {
                return;
            }
            
            lastReturnCheckTick = currentTick;
            
            // 检查是否应该回归
            if (ShouldReturn())
            {
                TriggerReturn();
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
            // 获取好感度和性格标签
            var agent = Current.Game?.GetComponent<Storyteller.StorytellerAgent>();
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
                
                // ⭐ v1.6.72: 从配置读取姿态和特效路径（支持人格专属文件夹）
                string personaName = SanitizeFileName(persona.narratorName);
                
                // 姿态路径：UI/Narrators/Descent/Postures/{PersonaName}/{PostureName}
                string posturePath = string.IsNullOrEmpty(persona.descentPosturePath) 
                    ? null 
                    : $"{personaName}/{persona.descentPosturePath}";
                
                // 特效路径：UI/Narrators/Descent/Effects/{PersonaName}/{EffectName}
                string effectPath = string.IsNullOrEmpty(persona.descentEffectPath) 
                    ? null 
                    : $"{personaName}/{persona.descentEffectPath}";
                
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
        /// 清理文件名中的非法字符
        /// </summary>
        private static string SanitizeFileName(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                return "UnnamedPersona";
            }
            
            char[] invalidChars = Path.GetInvalidFileNameChars();
            string sanitized = fileName;
            
            foreach (char c in invalidChars)
            {
                sanitized = sanitized.Replace(c, '_');
            }
            
            sanitized = sanitized.Replace(" ", "_");
            sanitized = sanitized.Replace("(", "");
            sanitized = sanitized.Replace(")", "");
            sanitized = sanitized.Replace("[", "");
            sanitized = sanitized.Replace("]", "");
            
            return sanitized;
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
                
                Log.Message($"[NarratorDescentSystem] ⭐ 降臨实体生成完成: {mainPawn?.LabelShort ?? "未知"}");
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
                string pawnKindName = persona.descentPawnKind;
                PawnKindDef pawnKindDef = DefDatabase<PawnKindDef>.GetNamedSilentFail(pawnKindName);
                
                if (pawnKindDef == null)
                {
                    Log.Error($"[NarratorDescentSystem] PawnKindDef 未找到: {pawnKindName}");
                    return null;
                }
                
                Faction faction = isHostile ? Find.FactionManager.RandomEnemyFaction() : Faction.OfPlayer;
                Pawn pawn = PawnGenerator.GeneratePawn(pawnKindDef, faction);
                pawn.Name = new NameTriple("", persona.narratorName, "");
                GenSpawn.Spawn(pawn, targetDescentLocation, map);
                
                // ⭐ v1.6.72: 保存降临实体引用
                currentDescentPawn = pawn;
                
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
            
            // ⭐ v1.6.72: 新增存档字段
            Scribe_References.Look(ref currentDescentPawn, "currentDescentPawn");
            Scribe_Values.Look(ref descentStartTick, "descentStartTick", 0);
            Scribe_Values.Look(ref portraitWasOpen, "portraitWasOpen", false);
            Scribe_Values.Look(ref lastReturnCheckTick, "lastReturnCheckTick", 0);
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
