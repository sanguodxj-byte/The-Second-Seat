using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.Sound;
using RimWorld;

namespace TheSecondSeat.Components
{
    /// <summary>
    /// ? v1.6.63: 理智光环组件 - 不可直视机制
    /// 
    /// 核心功能：
    /// - 检测谁在瞄准/注视Boss
    /// - 施加可堆叠的精神侵蚀Hediff
    /// - 支持范围光环（进入范围即掉理智）
    /// - 可配置惩罚严重度和恢复速度
    /// 
    /// XML配置示例：
    /// <code>
    /// &lt;comps&gt;
    ///   &lt;li Class="TheSecondSeat.Components.CompProperties_SanityAura"&gt;
    ///     &lt;radius&gt;10&lt;/radius&gt;
    ///     &lt;severityPerSecond&gt;0.02&lt;/severityPerSecond&gt;
    ///     &lt;linkedHediff&gt;TSS_MentalCorruption&lt;/linkedHediff&gt;
    ///     &lt;onlyWhenTargeting&gt;true&lt;/onlyWhenTargeting&gt;
    ///   &lt;/li&gt;
    /// &lt;/comps&gt;
    /// </code>
    /// </summary>
    public class CompSanityAura : ThingComp
    {
        private int tickCounter = 0;
        private const int CHECK_INTERVAL = 60; // 每秒检查一次
        
        private Dictionary<Pawn, float> affectedPawns = new Dictionary<Pawn, float>();
        
        public CompProperties_SanityAura Props => (CompProperties_SanityAura)props;
        
        public override void CompTick()
        {
            base.CompTick();
            
            if (!parent.Spawned) return;
            
            tickCounter++;
            
            if (tickCounter >= CHECK_INTERVAL)
            {
                tickCounter = 0;
                ProcessSanityDrain();
            }
        }
        
        /// <summary>
        /// 处理理智流失
        /// </summary>
        private void ProcessSanityDrain()
        {
            Map map = parent.Map;
            if (map == null) return;
            
            List<Pawn> colonists = map.mapPawns.FreeColonistsSpawned.ToList();
            
            foreach (Pawn colonist in colonists)
            {
                if (colonist == null || colonist.Dead) continue;
                
                bool shouldDrain = false;
                
                // 方案A：范围光环（只要在附近）
                if (!Props.onlyWhenTargeting)
                {
                    float distance = colonist.Position.DistanceTo(parent.Position);
                    if (distance <= Props.radius)
                    {
                        shouldDrain = true;
                    }
                }
                // 方案B：必须瞄准才触发
                else
                {
                    if (IsTargetingBoss(colonist))
                    {
                        shouldDrain = true;
                    }
                }
                
                if (shouldDrain)
                {
                    ApplySanityDamage(colonist);
                }
                else
                {
                    RecoverSanity(colonist);
                }
            }
            
            // 清理已死亡的Pawn
            affectedPawns = affectedPawns.Where(kvp => kvp.Key != null && !kvp.Key.Dead)
                                        .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }
        
        /// <summary>
        /// 检测殖民者是否在瞄准Boss
        /// </summary>
        private bool IsTargetingBoss(Pawn colonist)
        {
            // 方法1：检查当前攻击目标
            if (colonist.TargetCurrentlyAimingAt != null)
            {
                if (colonist.TargetCurrentlyAimingAt.Thing == parent)
                {
                    return true;
                }
            }
            
            // 方法2：检查Job目标
            if (colonist.CurJob != null)
            {
                LocalTargetInfo target = colonist.CurJob.targetA;
                if (target.Thing == parent)
                {
                    return true;
                }
            }
            
            // 方法3：严格的视线检测（可选，性能消耗大）
            if (Props.strictLineOfSight)
            {
                if (IsLookingAtBoss(colonist))
                {
                    return true;
                }
            }
            
            return false;
        }
        
        /// <summary>
        /// 严格的视线检测（判断殖民者是否面向Boss）
        /// </summary>
        private bool IsLookingAtBoss(Pawn colonist)
        {
            // 计算方向向量
            Vector3 directionToBoss = (parent.Position - colonist.Position).ToVector3();
            Vector3 colonistFacing = colonist.Rotation.FacingCell.ToVector3();
            
            // 计算夹角
            float angle = Vector3.Angle(colonistFacing, directionToBoss);
            
            // 如果夹角小于60度，认为在看Boss
            if (angle < 60f)
            {
                // 射线检测：确认视线无阻挡
                IntVec3 targetPos = parent.Position;
                if (GenSight.LineOfSight(colonist.Position, targetPos, colonist.Map, true))
                {
                    return true;
                }
            }
            
            return false;
        }
        
        /// <summary>
        /// 施加理智伤害
        /// </summary>
        private void ApplySanityDamage(Pawn colonist)
        {
            HediffDef hediffDef = DefDatabase<HediffDef>.GetNamedSilentFail(Props.linkedHediff);
            if (hediffDef == null)
            {
                Log.Error($"[CompSanityAura] HediffDef 未找到: {Props.linkedHediff}");
                return;
            }
            
            // 获取或添加Hediff
            Hediff hediff = colonist.health.hediffSet.GetFirstHediffOfDef(hediffDef);
            if (hediff == null)
            {
                hediff = HediffMaker.MakeHediff(hediffDef, colonist);
                colonist.health.AddHediff(hediff);
            }
            
            // 增加严重度
            float severityIncrease = Props.severityPerSecond * (CHECK_INTERVAL / 60f);
            hediff.Severity += severityIncrease;
            
            // 记录受影响时间
            if (!affectedPawns.ContainsKey(colonist))
            {
                affectedPawns[colonist] = 0f;
            }
            affectedPawns[colonist] += CHECK_INTERVAL / 60f;
            
            // 播放视觉效果
            if (Rand.Chance(0.1f)) // 10%概率播放特效
            {
                PlaySanityDrainEffect(colonist);
            }
        }
        
        /// <summary>
        /// 恢复理智
        /// </summary>
        private void RecoverSanity(Pawn colonist)
        {
            HediffDef hediffDef = DefDatabase<HediffDef>.GetNamedSilentFail(Props.linkedHediff);
            if (hediffDef == null) return;
            
            Hediff hediff = colonist.health.hediffSet.GetFirstHediffOfDef(hediffDef);
            if (hediff != null)
            {
                // 自然恢复
                float recoveryRate = Props.severityPerSecond * 0.5f; // 恢复速度是损失的一半
                hediff.Severity -= recoveryRate * (CHECK_INTERVAL / 60f);
                
                // 如果完全恢复，移除Hediff
                if (hediff.Severity <= 0f)
                {
                    colonist.health.RemoveHediff(hediff);
                    affectedPawns.Remove(colonist);
                }
            }
        }
        
        /// <summary>
        /// 播放理智流失特效
        /// </summary>
        private void PlaySanityDrainEffect(Pawn colonist)
        {
            // 紫色烟雾
            FleckMaker.ThrowMetaPuff(colonist.Position.ToVector3(), colonist.Map);
            
            // 播放音效
            if (!string.IsNullOrEmpty(Props.drainSound))
            {
                SoundDef soundDef = DefDatabase<SoundDef>.GetNamedSilentFail(Props.drainSound);
                if (soundDef != null)
                {
                    // ? 修复：使用正确的 PlayOneShot 调用
                    SoundInfo info = SoundInfo.InMap(new TargetInfo(colonist.Position, colonist.Map));
                    soundDef.PlayOneShot(info);
                }
            }
        }
        
        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref tickCounter, "tickCounter", 0);
            Scribe_Collections.Look(ref affectedPawns, "affectedPawns", LookMode.Reference, LookMode.Value);
            
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (affectedPawns == null)
                {
                    affectedPawns = new Dictionary<Pawn, float>();
                }
            }
        }
        
        public override string CompInspectStringExtra()
        {
            int affectedCount = affectedPawns.Count;
            return $"受影响的殖民者: {affectedCount}";
        }
        
        /// <summary>
        /// 绘制光环范围（调试用）
        /// </summary>
        public override void PostDraw()
        {
            base.PostDraw();
            
            if (Prefs.DevMode && Find.Selector.IsSelected(parent))
            {
                // 绘制光环范围圆圈
                GenDraw.DrawRadiusRing(parent.Position, Props.radius);
            }
        }
    }
    
    /// <summary>
    /// 组件配置类
    /// </summary>
    public class CompProperties_SanityAura : CompProperties
    {
        /// <summary>光环半径</summary>
        public float radius = 10f;
        
        /// <summary>每秒增加的严重度</summary>
        public float severityPerSecond = 0.02f;
        
        /// <summary>关联的 HediffDef 名称</summary>
        public string linkedHediff = "";
        
        /// <summary>是否只在瞄准时触发</summary>
        public bool onlyWhenTargeting = true;
        
        /// <summary>是否使用严格的视线检测</summary>
        public bool strictLineOfSight = false;
        
        /// <summary>理智流失音效</summary>
        public string drainSound = "";
        
        public CompProperties_SanityAura()
        {
            compClass = typeof(CompSanityAura);
        }
    }
}
