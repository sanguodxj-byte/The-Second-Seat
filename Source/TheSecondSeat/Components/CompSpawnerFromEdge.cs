using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using Verse.Sound;
using RimWorld;

namespace TheSecondSeat.Components
{
    /// <summary>
    /// ? v1.6.63: 深渊召唤组件 - 从地图边缘无限刷触手
    /// 
    /// 核心功能：
    /// - 定时从地图边缘生成敌对生物
    /// - 支持通用配置（生成什么、多久生一次、最多几个）
    /// - 绑定到Boss，Boss死后自动停止
    /// - 可配置AI行为（推进/保卫）
    /// 
    /// XML配置示例：
    /// <code>
    /// &lt;comps&gt;
    ///   &lt;li Class="TheSecondSeat.Components.CompProperties_SpawnerFromEdge"&gt;
    ///     &lt;spawnThingDef&gt;TSS_Cthulhu_Tentacle&lt;/spawnThingDef&gt;
    ///     &lt;spawnInterval&gt;600&lt;/spawnInterval&gt; &lt;!-- 10秒 --&gt;
    ///     &lt;spawnMaxCount&gt;20&lt;/spawnMaxCount&gt;
    ///     &lt;aiMode&gt;PushToCenter&lt;/aiMode&gt;
    ///   &lt;/li&gt;
    /// &lt;/comps&gt;
    /// </code>
    /// </summary>
    public class CompSpawnerFromEdge : ThingComp
    {
        private int ticksUntilNextSpawn = 0;
        private List<Pawn> spawnedPawns = new List<Pawn>();
        
        public CompProperties_SpawnerFromEdge Props => (CompProperties_SpawnerFromEdge)props;
        
        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            
            if (!respawningAfterLoad)
            {
                ResetSpawnTimer();
            }
        }
        
        public override void CompTick()
        {
            base.CompTick();
            
            // 检查Boss是否还活着
            if (!parent.Spawned || parent.Destroyed)
            {
                CleanupSpawnedPawns();
                return;
            }
            
            // 倒计时
            ticksUntilNextSpawn--;
            
            if (ticksUntilNextSpawn <= 0)
            {
                TrySpawnFromEdge();
                ResetSpawnTimer();
            }
            
            // 清理已死亡的生物
            CleanupDeadPawns();
        }
        
        /// <summary>
        /// 尝试从地图边缘生成生物
        /// </summary>
        private void TrySpawnFromEdge()
        {
            // 1. 检查是否达到最大数量
            if (spawnedPawns.Count >= Props.spawnMaxCount)
            {
                return;
            }
            
            Map map = parent.Map;
            if (map == null) return;
            
            // 2. 选择边缘位置
            IntVec3? spawnPos = TryFindEdgeSpawnLocation(map);
            if (spawnPos == null || !spawnPos.Value.IsValid)
            {
                Log.Warning("[CompSpawnerFromEdge] 找不到合适的边缘生成位置");
                return;
            }
            
            // 3. 生成生物
            PawnKindDef pawnKindDef = DefDatabase<PawnKindDef>.GetNamedSilentFail(Props.spawnPawnKind);
            if (pawnKindDef == null)
            {
                Log.Error($"[CompSpawnerFromEdge] PawnKindDef 未找到: {Props.spawnPawnKind}");
                return;
            }
            
            Faction faction = Props.friendlyFaction ? Faction.OfPlayer : Find.FactionManager.RandomEnemyFaction();
            Pawn pawn = PawnGenerator.GeneratePawn(pawnKindDef, faction);
            
            GenSpawn.Spawn(pawn, spawnPos.Value, map);
            spawnedPawns.Add(pawn);
            
            // 4. 赋予AI任务
            AssignAIBehavior(pawn, map);
            
            // 5. 播放特效
            PlaySpawnEffect(spawnPos.Value, map);
            
            Log.Message($"[CompSpawnerFromEdge] 生成触手: {pawn.LabelShort} at {spawnPos.Value}");
        }
        
        /// <summary>
        /// 寻找合适的边缘生成位置
        /// </summary>
        private IntVec3? TryFindEdgeSpawnLocation(Map map)
        {
            CellRect mapRect = new CellRect(0, 0, map.Size.x, map.Size.z);
            IEnumerable<IntVec3> edgeCells = mapRect.EdgeCells;
            
            // 随机打乱边缘格子顺序
            List<IntVec3> shuffledEdges = edgeCells.ToList();
            shuffledEdges.Shuffle();
            
            // 寻找第一个合法位置
            foreach (IntVec3 cell in shuffledEdges.Take(100)) // 最多尝试100次
            {
                if (IsValidSpawnLocation(cell, map))
                {
                    return cell;
                }
            }
            
            return null;
        }
        
        /// <summary>
        /// 检查位置是否适合生成
        /// </summary>
        private bool IsValidSpawnLocation(IntVec3 cell, Map map)
        {
            if (!cell.InBounds(map)) return false;
            if (!cell.Standable(map)) return false;
            if (cell.Roofed(map)) return false;
            if (cell.GetFirstBuilding(map) != null) return false;
            
            return true;
        }
        
        /// <summary>
        /// 赋予AI行为
        /// </summary>
        private void AssignAIBehavior(Pawn pawn, Map map)
        {
            switch (Props.aiMode)
            {
                case AIMode.PushToCenter:
                    // 创建一个向地图中心推进的 LordJob
                    IntVec3 centerPos = map.Center;
                    LordJob_AssaultColony lordJob = new LordJob_AssaultColony(pawn.Faction, true, true, false, false, true);
                    Lord lord = LordMaker.MakeNewLord(pawn.Faction, lordJob, map, new List<Pawn> { pawn });
                    break;
                    
                case AIMode.DefendBoss:
                    // 保护Boss的AI
                    if (parent is Pawn boss)
                    {
                        LordJob_DefendPoint defendJob = new LordJob_DefendPoint(boss.Position, null, 0f, true);
                        LordMaker.MakeNewLord(pawn.Faction, defendJob, map, new List<Pawn> { pawn });
                    }
                    break;
                    
                case AIMode.Wander:
                    // 漫游模式（默认AI）
                    break;
            }
        }
        
        /// <summary>
        /// 播放生成特效
        /// </summary>
        private void PlaySpawnEffect(IntVec3 pos, Map map)
        {
            // 绿色烟雾
            FleckMaker.ThrowSmoke(pos.ToVector3(), map, 2f);
            
            // 深渊音效
            if (!string.IsNullOrEmpty(Props.spawnSound))
            {
                SoundDef soundDef = DefDatabase<SoundDef>.GetNamedSilentFail(Props.spawnSound);
                if (soundDef != null)
                {
                    // ? 修复：使用正确的 PlayOneShot 调用
                    SoundInfo info = SoundInfo.InMap(new TargetInfo(pos, map));
                    soundDef.PlayOneShot(info);
                }
            }
        }
        
        /// <summary>
        /// 重置生成计时器
        /// </summary>
        private void ResetSpawnTimer()
        {
            int baseInterval = Props.spawnInterval;
            int variance = (int)(baseInterval * 0.2f); // ±20% 随机性
            ticksUntilNextSpawn = baseInterval + Random.Range(-variance, variance);
        }
        
        /// <summary>
        /// 清理已死亡的生物
        /// </summary>
        private void CleanupDeadPawns()
        {
            spawnedPawns.RemoveAll(p => p == null || p.Dead || p.Destroyed);
        }
        
        /// <summary>
        /// Boss死后清理所有生成的生物
        /// </summary>
        private void CleanupSpawnedPawns()
        {
            foreach (Pawn pawn in spawnedPawns)
            {
                if (pawn != null && !pawn.Dead && pawn.Spawned)
                {
                    if (Props.despawnOnBossDeath)
                    {
                        // 直接消失
                        pawn.Destroy();
                    }
                    else
                    {
                        // 狂暴（攻击最近的目标）
                        pawn.mindState.mentalStateHandler.TryStartMentalState(MentalStateDefOf.Berserk);
                    }
                }
            }
            spawnedPawns.Clear();
        }
        
        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref ticksUntilNextSpawn, "ticksUntilNextSpawn", 0);
            Scribe_Collections.Look(ref spawnedPawns, "spawnedPawns", LookMode.Reference);
            
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (spawnedPawns == null)
                {
                    spawnedPawns = new List<Pawn>();
                }
            }
        }
        
        public override string CompInspectStringExtra()
        {
            string info = $"活跃触手: {spawnedPawns.Count}/{Props.spawnMaxCount}\n";
            info += $"下次生成: {ticksUntilNextSpawn} ticks";
            return info;
        }
    }
    
    /// <summary>
    /// 组件配置类
    /// </summary>
    public class CompProperties_SpawnerFromEdge : CompProperties
    {
        /// <summary>生成的 PawnKindDef 名称</summary>
        public string spawnPawnKind = "";
        
        /// <summary>生成间隔（ticks）</summary>
        public int spawnInterval = 600; // 默认10秒
        
        /// <summary>场上最大数量</summary>
        public int spawnMaxCount = 20;
        
        /// <summary>AI行为模式</summary>
        public AIMode aiMode = AIMode.PushToCenter;
        
        /// <summary>是否为友好阵营</summary>
        public bool friendlyFaction = false;
        
        /// <summary>Boss死后是否消失</summary>
        public bool despawnOnBossDeath = true;
        
        /// <summary>生成音效</summary>
        public string spawnSound = "";
        
        public CompProperties_SpawnerFromEdge()
        {
            compClass = typeof(CompSpawnerFromEdge);
        }
    }
    
    /// <summary>
    /// AI行为模式枚举
    /// </summary>
    public enum AIMode
    {
        PushToCenter,  // 向地图中心推进
        DefendBoss,    // 保卫Boss
        Wander         // 随机漫游
    }
}
