using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using RimWorld;
using UnityEngine; // ? 添加UnityEngine命名空间用于Mathf
using Verse.Sound; // ? 添加用于SoundStarter

namespace TheSecondSeat.Framework.Actions
{
    /// <summary>
    /// 高阶动作集合 - "上帝级"能力
    /// 
    /// 包含强大的游戏干预能力：
    /// - 天罚雷击
    /// - 健康状态操控
    /// - 强制触发原版事件
    /// 
    /// ?? 警告：这些动作权限很高，请谨慎使用
    /// </summary>
    
    // ============================================
    // 天罚系统
    // ============================================
    
    /// <summary>
    /// 雷击动作 - 在指定位置降下天罚
    /// 
    /// XML示例：
    /// <![CDATA[
    /// <li Class="TheSecondSeat.Framework.Actions.StrikeLightningAction">
    ///   <strikeMode>Random</strikeMode>
    ///   <strikeCount>3</strikeCount>
    ///   <damageAmount>100</damageAmount>
    ///   <radius>5</radius>
    /// </li>
    /// ]]>
    /// </summary>
    public class StrikeLightningAction : TSSAction
    {
        /// <summary>雷击模式</summary>
        public enum LightningStrikeMode
        {
            /// <summary>随机位置</summary>
            Random,
            /// <summary>地图中心</summary>
            MapCenter,
            /// <summary>最近的敌人</summary>
            NearestEnemy,
            /// <summary>最近的殖民者（慎用！）</summary>
            NearestColonist,
            /// <summary>指定坐标</summary>
            Specific
        }
        
        /// <summary>雷击模式</summary>
        public LightningStrikeMode strikeMode = LightningStrikeMode.Random;
        
        /// <summary>雷击次数</summary>
        public int strikeCount = 1;
        
        /// <summary>伤害量（每次雷击）</summary>
        public float damageAmount = 100f;
        
        /// <summary>AOE范围半径</summary>
        public float radius = 3f;
        
        /// <summary>指定坐标（仅在strikeMode=Specific时使用）</summary>
        public IntVec3 targetCell = IntVec3.Invalid;
        
        /// <summary>是否造成火灾</summary>
        public bool causesFire = true;
        
        /// <summary>是否播放音效</summary>
        public bool playSound = true;
        
        public override void Execute(Map map, Dictionary<string, object> context)
        {
            if (map == null)
            {
                Log.Warning("[StrikeLightningAction] Map is null, cannot strike lightning");
                return;
            }
            
            for (int i = 0; i < strikeCount; i++)
            {
                IntVec3 strikePos = GetStrikePosition(map);
                
                if (!strikePos.IsValid)
                {
                    if (Prefs.DevMode)
                    {
                        Log.Warning($"[StrikeLightningAction] Failed to find valid strike position (attempt {i + 1}/{strikeCount})");
                    }
                    continue;
                }
                
                // 执行雷击
                ExecuteLightningStrike(map, strikePos);
                
                if (Prefs.DevMode)
                {
                    Log.Message($"[StrikeLightningAction] Lightning strike {i + 1}/{strikeCount} at {strikePos}");
                }
            }
        }
        
        /// <summary>
        /// 获取雷击位置
        /// </summary>
        private IntVec3 GetStrikePosition(Map map)
        {
            switch (strikeMode)
            {
                case LightningStrikeMode.Random:
                    // 随机可站立位置
                    return CellFinderLoose.RandomCellWith(
                        (IntVec3 c) => c.Standable(map) && !c.Fogged(map),
                        map,
                        1000
                    );
                    
                case LightningStrikeMode.MapCenter:
                    return map.Center;
                    
                case LightningStrikeMode.NearestEnemy:
                    // 找最近的敌对生物
                    Pawn enemy = map.mapPawns.AllPawnsSpawned
                        .Where(p => p.HostileTo(Faction.OfPlayer) && !p.Downed)
                        .OrderBy(p => p.Position.DistanceTo(map.Center))
                        .FirstOrDefault();
                    
                    if (enemy != null)
                    {
                        return enemy.Position;
                    }
                    // 找不到敌人则随机位置
                    return CellFinderLoose.RandomCellWith(
                        (IntVec3 c) => c.Standable(map),
                        map,
                        1000
                    );
                    
                case LightningStrikeMode.NearestColonist:
                    // 找最近的殖民者（慎用！）
                    Pawn colonist = map.mapPawns.FreeColonists
                        .OrderBy(p => p.Position.DistanceTo(map.Center))
                        .FirstOrDefault();
                    
                    if (colonist != null)
                    {
                        return colonist.Position;
                    }
                    return map.Center;
                    
                case LightningStrikeMode.Specific:
                    if (targetCell.IsValid && targetCell.InBounds(map))
                    {
                        return targetCell;
                    }
                    return map.Center;
                    
                default:
                    return IntVec3.Invalid;
            }
        }
        
        /// <summary>
        /// 执行雷击效果
        /// </summary>
        private void ExecuteLightningStrike(Map map, IntVec3 pos)
        {
            // ? 修复：使用正确的音效播放API
            if (playSound && SoundDefOf.Thunder_OnMap != null)
            {
                SoundStarter.PlayOneShotOnCamera(SoundDefOf.Thunder_OnMap, null);
            }
            
            // 生成闪电视觉效果
            FlashLightning(map, pos);
            
            // 造成伤害
            DealLightningDamage(map, pos);
            
            // ? 修复：点燃火焰（添加instigator参数）
            if (causesFire)
            {
                FireUtility.TryStartFireIn(pos, map, Rand.Range(0.1f, 0.5f), null);
            }
        }
        
        /// <summary>
        /// 闪电视觉效果
        /// </summary>
        private void FlashLightning(Map map, IntVec3 pos)
        {
            // ? 修复：使用简化的闪电效果
            // 创建小型爆炸效果模拟闪电
            GenExplosion.DoExplosion(
                center: pos,
                map: map,
                radius: 2.5f,
                damType: DamageDefOf.Flame,
                instigator: null,
                damAmount: (int)(damageAmount * 0.5f), // 视觉效果伤害减半
                armorPenetration: 999f,
                explosionSound: null,
                weapon: null,
                projectile: null,
                intendedTarget: null,
                postExplosionSpawnThingDef: null,
                postExplosionSpawnChance: 0f,
                postExplosionSpawnThingCount: 0,
                applyDamageToExplosionCellsNeighbors: false,
                preExplosionSpawnThingDef: null,
                preExplosionSpawnChance: 0f,
                preExplosionSpawnThingCount: 0,
                chanceToStartFire: 0f,
                damageFalloff: true
            );
        }
        
        /// <summary>
        /// 造成雷击伤害
        /// </summary>
        private void DealLightningDamage(Map map, IntVec3 pos)
        {
            // 获取范围内的所有物体
            IEnumerable<Thing> affectedThings = GenRadial.RadialDistinctThingsAround(
                pos, 
                map, 
                radius, 
                true
            );
            
            foreach (Thing thing in affectedThings)
            {
                // 计算距离衰减
                float distance = thing.Position.DistanceTo(pos);
                float damageMultiplier = 1f - (distance / radius);
                float actualDamage = damageAmount * damageMultiplier;
                
                if (actualDamage <= 0) continue;
                
                // 造成电击伤害
                DamageInfo dinfo = new DamageInfo(
                    DamageDefOf.Flame, 
                    actualDamage,
                    999f, // 护甲穿透
                    -1f,
                    null,
                    null,
                    null,
                    DamageInfo.SourceCategory.ThingOrUnknown
                );
                
                thing.TakeDamage(dinfo);
                
                // 对生物造成额外的眩晕效果
                if (thing is Pawn pawn && !pawn.Dead)
                {
                    pawn.stances.stunner.StunFor(Mathf.RoundToInt(60 * damageMultiplier), null);
                }
            }
        }
        
        public override string GetDescription()
        {
            return $"Strike Lightning ({strikeMode}, {strikeCount}x, {damageAmount} dmg)";
        }
    }
    
    // ============================================
    // 健康状态操控
    // ============================================
    
    /// <summary>
    /// 添加健康状态动作 - 给小人添加Hediff（疾病、增益、义体等）
    /// 
    /// XML示例：
    /// <![CDATA[
    /// <li Class="TheSecondSeat.Framework.Actions.GiveHediffAction">
    ///   <hediffDef>Flu</hediffDef>
    ///   <targetMode>Random</targetMode>
    ///   <severity>0.5</severity>
    ///   <targetCount>3</targetCount>
    /// </li>
    /// ]]>
    /// </summary>
    public class GiveHediffAction : TSSAction
    {
        /// <summary>目标选择模式</summary>
        public enum TargetSelectionMode
        {
            /// <summary>随机殖民者</summary>
            Random,
            /// <summary>所有殖民者</summary>
            AllColonists,
            /// <summary>最健康的殖民者</summary>
            Healthiest,
            /// <summary>最虚弱的殖民者</summary>
            Weakest,
            /// <summary>随机敌人</summary>
            RandomEnemy
        }
        
        /// <summary>Hediff定义（疾病、增益等）</summary>
        public HediffDef hediffDef = null;
        
        /// <summary>目标选择模式</summary>
        public TargetSelectionMode targetMode = TargetSelectionMode.Random;
        
        /// <summary>严重程度（0.0-1.0）</summary>
        public float severity = 0.5f;
        
        /// <summary>目标数量</summary>
        public int targetCount = 1;
        
        /// <summary>作用身体部位（留空表示随机或全身）</summary>
        public BodyPartDef targetBodyPart = null;
        
        /// <summary>是否显示通知</summary>
        public bool showNotification = true;
        
        public override void Execute(Map map, Dictionary<string, object> context)
        {
            if (hediffDef == null)
            {
                Log.Error("[GiveHediffAction] hediffDef is null");
                return;
            }
            
            if (map == null)
            {
                Log.Warning("[GiveHediffAction] Map is null");
                return;
            }
            
            // 获取目标列表
            List<Pawn> targets = GetTargets(map);
            
            if (targets.Count == 0)
            {
                if (Prefs.DevMode)
                {
                    Log.Warning("[GiveHediffAction] No valid targets found");
                }
                return;
            }
            
            // 随机选择指定数量的目标
            int actualCount = Math.Min(targetCount, targets.Count);
            List<Pawn> selectedTargets = targets.InRandomOrder().Take(actualCount).ToList();
            
            foreach (Pawn target in selectedTargets)
            {
                ApplyHediff(target);
            }
        }
        
        /// <summary>
        /// 获取目标列表
        /// </summary>
        private List<Pawn> GetTargets(Map map)
        {
            List<Pawn> candidates = new List<Pawn>();
            
            switch (targetMode)
            {
                case TargetSelectionMode.Random:
                case TargetSelectionMode.AllColonists:
                    candidates = map.mapPawns.FreeColonists.ToList();
                    break;
                    
                case TargetSelectionMode.Healthiest:
                    candidates = map.mapPawns.FreeColonists
                        .OrderByDescending(p => p.health.summaryHealth.SummaryHealthPercent)
                        .ToList();
                    break;
                    
                case TargetSelectionMode.Weakest:
                    candidates = map.mapPawns.FreeColonists
                        .OrderBy(p => p.health.summaryHealth.SummaryHealthPercent)
                        .ToList();
                    break;
                    
                case TargetSelectionMode.RandomEnemy:
                    candidates = map.mapPawns.AllPawnsSpawned
                        .Where(p => p.HostileTo(Faction.OfPlayer) && !p.Dead)
                        .ToList();
                    break;
            }
            
            return candidates.Where(p => p != null && !p.Dead).ToList();
        }
        
        /// <summary>
        /// 应用Hediff到目标
        /// </summary>
        private void ApplyHediff(Pawn target)
        {
            // ? 增强空值检查
            if (target == null)
            {
                Log.Warning("[GiveHediffAction] Target pawn is null");
                return;
            }
            
            if (target.Dead)
            {
                Log.Warning($"[GiveHediffAction] Target {target.LabelShort} is already dead");
                return;
            }
            
            if (target.health == null)
            {
                Log.Error($"[GiveHediffAction] Target {target.LabelShort} has null health component");
                return;
            }
            
            if (target.health.hediffSet == null)
            {
                Log.Error($"[GiveHediffAction] Target {target.LabelShort} has null hediffSet");
                return;
            }
            
            try
            {
                BodyPartRecord part = null;
                
                // 如果指定了身体部位，尝试找到对应部位
                if (targetBodyPart != null)
                {
                    var parts = target.health.hediffSet.GetNotMissingParts();
                    if (parts != null)
                    {
                        part = parts.FirstOrDefault(p => p != null && p.def == targetBodyPart);
                    }
                }
                
                // ? 验证hediffDef不为null
                if (hediffDef == null)
                {
                    Log.Error("[GiveHediffAction] hediffDef is null, cannot create hediff");
                    return;
                }
                
                // 添加Hediff
                Hediff hediff = HediffMaker.MakeHediff(hediffDef, target, part);
                
                if (hediff == null)
                {
                    Log.Error($"[GiveHediffAction] Failed to create hediff {hediffDef.defName}");
                    return;
                }
                
                hediff.Severity = severity;
                target.health.AddHediff(hediff, part);
                
                // 显示通知
                if (showNotification)
                {
                    string partName = part != null ? part.Label : "全身";
                    Messages.Message(
                        $"{target.LabelShort} 被施加了 {hediffDef.LabelCap} ({partName})",
                        target,
                        MessageTypeDefOf.NeutralEvent
                    );
                }
                
                if (Prefs.DevMode)
                {
                    Log.Message($"[GiveHediffAction] Applied {hediffDef.defName} to {target.LabelShort} (severity: {severity})");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[GiveHediffAction] Failed to apply hediff to {target.LabelShort}: {ex.Message}\nStack trace: {ex.StackTrace}");
            }
        }
        
        public override string GetDescription()
        {
            return $"Give Hediff: {hediffDef?.defName ?? "None"} ({targetMode}, {targetCount}x)";
        }
        
        public override bool Validate(out string error)
        {
            if (hediffDef == null)
            {
                error = "hediffDef is null";
                return false;
            }
            
            if (severity < 0 || severity > 1)
            {
                error = $"severity must be between 0 and 1: {severity}";
                return false;
            }
            
            return base.Validate(out error);
        }
    }
    
    // ============================================
    // 事件强制触发
    // ============================================
    
    /// <summary>
    /// 强制触发原版事件动作 - 触发RimWorld的IncidentDef
    /// 
    /// XML示例：
    /// <![CDATA[
    /// <li Class="TheSecondSeat.Framework.Actions.StartIncidentAction">
    ///   <incidentDef>RaidEnemy</incidentDef>
    ///   <points>500</points>
    ///   <forced>true</forced>
    /// </li>
    /// ]]>
    /// </summary>
    public class StartIncidentAction : TSSAction
    {
        /// <summary>事件定义</summary>
        public IncidentDef incidentDef = null;
        
        /// <summary>事件点数（影响袭击规模等）</summary>
        public float points = -1f; // -1表示使用默认值
        
        /// <summary>是否强制触发（忽略冷却和条件）</summary>
        public bool forced = false;
        
        /// <summary>目标派系（留空表示随机）</summary>
        public FactionDef targetFaction = null;
        
        /// <summary>是否允许大规模事件</summary>
        public bool allowBigThreat = true;
        
        public override void Execute(Map map, Dictionary<string, object> context)
        {
            if (incidentDef == null)
            {
                Log.Error("[StartIncidentAction] incidentDef is null");
                return;
            }
            
            if (map == null)
            {
                Log.Warning("[StartIncidentAction] Map is null");
                return;
            }
            
            // 创建事件参数
            IncidentParms parms = StorytellerUtility.DefaultParmsNow(
                incidentDef.category,
                map
            );
            
            // 设置点数
            if (points > 0)
            {
                parms.points = points;
            }
            
            // 设置目标派系
            if (targetFaction != null)
            {
                parms.faction = Find.FactionManager.FirstFactionOfDef(targetFaction);
            }
            
            // 设置其他参数
            parms.forced = forced;
            parms.target = map;
            
            if (!allowBigThreat)
            {
                parms.points = Math.Min(parms.points, 1000f);
            }
            
            // 尝试触发事件
            if (incidentDef.Worker.TryExecute(parms))
            {
                if (Prefs.DevMode)
                {
                    Log.Message($"[StartIncidentAction] Successfully triggered incident: {incidentDef.defName} (points: {parms.points})");
                }
            }
            else
            {
                if (Prefs.DevMode)
                {
                    Log.Warning($"[StartIncidentAction] Failed to trigger incident: {incidentDef.defName}");
                }
            }
        }
        
        public override string GetDescription()
        {
            string pointsStr = points > 0 ? $"{points} points" : "default points";
            return $"Start Incident: {incidentDef?.defName ?? "None"} ({pointsStr})";
        }
        
        public override bool Validate(out string error)
        {
            if (incidentDef == null)
            {
                error = "incidentDef is null";
                return false;
            }
            
            if (points < 0 && points != -1)
            {
                error = $"points must be positive or -1 (default): {points}";
                return false;
            }
            
            return base.Validate(out error);
        }
    }
    
    // ============================================
    // 叙事者语音系统
    // ============================================
    
    /// <summary>
    /// 叙事者语音动作 - 调用框架内置的TTSService播放语音
    /// 
    /// XML示例：
    /// <![CDATA[
    /// <li Class="TheSecondSeat.Framework.Actions.NarratorSpeakAction">
    ///   <textKey>TSS_Event_DivinePunishment</textKey>
    ///   <text>你的所作所为让我不得不采取措施了...</text>
    ///   <personaDefName>Sideria_Default</personaDefName>
    ///   <showDialogue>true</showDialogue>
    /// </li>
    /// ]]>
    /// </summary>
    public class NarratorSpeakAction : TSSAction
    {
        /// <summary>翻译键（优先使用）</summary>
        public string textKey = "";
        
        /// <summary>直接文本（如果没有翻译键）</summary>
        public string text = "";
        
        /// <summary>指定人格DefName（留空使用当前人格）</summary>
        public string personaDefName = "";
        
        /// <summary>是否显示对话框</summary>
        public bool showDialogue = true;
        
        public override void Execute(Map map, Dictionary<string, object> context)
        {
            try
            {
                // 1. 确定最终文本
                string finalText = GetFinalText();
                
                if (string.IsNullOrEmpty(finalText))
                {
                    if (Prefs.DevMode)
                    {
                        Log.Warning("[NarratorSpeakAction] No text or textKey provided");
                    }
                    return;
                }
                
                // 2. 显示对话框（可选）
                if (showDialogue)
                {
                    ShowDialogueBox(finalText);
                }
                
                // 3. 调用TTS服务播放语音
                PlayTTS(finalText);
                
                if (Prefs.DevMode)
                {
                    Log.Message($"[NarratorSpeakAction] Speaking: {finalText.Substring(0, Math.Min(50, finalText.Length))}...");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[NarratorSpeakAction] Failed to execute: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 获取最终文本（优先使用翻译键）
        /// </summary>
        private string GetFinalText()
        {
            // 优先使用翻译键
            if (!string.IsNullOrEmpty(textKey))
            {
                try
                {
                    string translated = textKey.Translate();
                    if (!string.IsNullOrEmpty(translated) && translated != textKey)
                    {
                        return translated;
                    }
                }
                catch
                {
                    // 翻译失败，继续使用直接文本
                }
            }
            
            // 备用：使用直接文本
            return text;
        }
        
        /// <summary>
        /// 显示对话框
        /// </summary>
        private void ShowDialogueBox(string message)
        {
            try
            {
                var dialog = new Dialog_MessageBox(message);
                Find.WindowStack.Add(dialog);
            }
            catch (Exception ex)
            {
                if (Prefs.DevMode)
                {
                    Log.Warning($"[NarratorSpeakAction] Failed to show dialogue: {ex.Message}");
                }
            }
        }
        
        /// <summary>
        /// 调用TTS服务播放语音
        /// </summary>
        private void PlayTTS(string textToSpeak)
        {
            try
            {
                // 获取TTS服务实例
                var ttsService = TheSecondSeat.TTS.TTSService.Instance;
                
                if (ttsService == null)
                {
                    if (Prefs.DevMode)
                    {
                        Log.Warning("[NarratorSpeakAction] TTSService not available");
                    }
                    return;
                }
                
                // 异步播放（不阻塞主线程）
                // 注意：SpeakAsync是异步方法，但我们直接调用它让它在后台运行
                System.Threading.Tasks.Task.Run(async () =>
                {
                    try
                    {
                        await ttsService.SpeakAsync(textToSpeak, personaDefName);
                    }
                    catch (Exception ex)
                    {
                        if (Prefs.DevMode)
                        {
                            Log.Warning($"[NarratorSpeakAction] TTS playback failed: {ex.Message}");
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                if (Prefs.DevMode)
                {
                    Log.Warning($"[NarratorSpeakAction] Failed to start TTS: {ex.Message}");
                }
            }
        }
        
        public override string GetDescription()
        {
            string displayText = !string.IsNullOrEmpty(textKey) ? textKey : text;
            if (displayText.Length > 30)
            {
                displayText = displayText.Substring(0, 30) + "...";
            }
            return $"Narrator Speak: \"{displayText}\"";
        }
        
        public override bool Validate(out string error)
        {
            if (string.IsNullOrEmpty(textKey) && string.IsNullOrEmpty(text))
            {
                error = "Both textKey and text are empty";
                return false;
            }
            
            return base.Validate(out error);
        }
    }
}
