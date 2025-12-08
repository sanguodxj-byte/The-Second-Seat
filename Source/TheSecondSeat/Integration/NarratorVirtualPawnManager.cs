using System;
using System.Collections.Generic;
using Verse;
using RimWorld;

namespace TheSecondSeat.Integration
{
    /// <summary>
    /// ? WorldComponent: 持久化叙事者虚拟 Pawn（随存档保存）
    /// </summary>
    public class NarratorVirtualPawnManager : WorldComponent
    {
        // ? 持久化存储：叙事者 DefName → 虚拟 Pawn ThingID
        private Dictionary<string, string> narratorPawnIDs = new Dictionary<string, string>();
        
        // ? 运行时缓存：叙事者 DefName → 实际 Pawn 实例
        private Dictionary<string, Pawn> narratorPawnCache = new Dictionary<string, Pawn>();

        public NarratorVirtualPawnManager(World world) : base(world)
        {
        }

        /// <summary>
        /// ? 获取或创建叙事者虚拟 Pawn（自动持久化）
        /// </summary>
        public Pawn GetOrCreateNarratorPawn(string narratorDefName, string narratorName)
        {
            // 1. 检查缓存
            if (narratorPawnCache.TryGetValue(narratorDefName, out Pawn cachedPawn))
            {
                if (cachedPawn != null && !cachedPawn.Destroyed)
                {
                    return cachedPawn;
                }
                else
                {
                    // 缓存的 Pawn 已销毁，清除
                    narratorPawnCache.Remove(narratorDefName);
                }
            }

            // 2. 检查是否有已保存的 Pawn ID
            if (narratorPawnIDs.TryGetValue(narratorDefName, out string thingID))
            {
                // 尝试在世界中查找这个 Pawn
                Pawn existingPawn = Find.WorldPawns.GetPawnByID(thingID);
                if (existingPawn != null && !existingPawn.Destroyed)
                {
                    narratorPawnCache[narratorDefName] = existingPawn;
                    Log.Message($"[NarratorVirtualPawnManager] 从存档恢复叙事者 Pawn: {narratorName}");
                    return existingPawn;
                }
            }

            // 3. 创建新的虚拟 Pawn
            Pawn newPawn = CreateVirtualPawn(narratorDefName, narratorName);
            
            // 4. 保存到持久化字典
            narratorPawnIDs[narratorDefName] = newPawn.ThingID;
            narratorPawnCache[narratorDefName] = newPawn;
            
            // 5. 添加到 WorldPawns（确保跨存档保存）
            Find.WorldPawns.PassToWorld(newPawn, PawnDiscardDecideMode.KeepForever);
            
            Log.Message($"[NarratorVirtualPawnManager] 创建叙事者虚拟 Pawn: {narratorName} (ID: {newPawn.ThingID})");
            
            return newPawn;
        }

        /// <summary>
        /// 创建虚拟 Pawn（不生成实体）
        /// </summary>
        private Pawn CreateVirtualPawn(string defName, string name)
        {
            try
            {
                PawnKindDef kind = PawnKindDefOf.Colonist;
                Faction faction = Faction.OfPlayer;
                
                Pawn pawn = PawnGenerator.GeneratePawn(kind, faction);
                
                // 设置名称
                pawn.Name = new NameSingle(name);
                
                // 添加 RimTalk 记忆组件（如果可用）
                RimTalkMemoryIntegration.AddMemoryComponentToPawn(pawn);
                
                return pawn;
            }
            catch (Exception ex)
            {
                Log.Error($"[NarratorVirtualPawnManager] 创建虚拟 Pawn 失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// ? 持久化保存（ExposeData）
        /// </summary>
        public override void ExposeData()
        {
            base.ExposeData();
            
            Scribe_Collections.Look(ref narratorPawnIDs, "narratorPawnIDs", LookMode.Value, LookMode.Value);
            
            // 加载后重建缓存
            if (Scribe.mode == LoadSaveMode.LoadingVars && narratorPawnIDs == null)
            {
                narratorPawnIDs = new Dictionary<string, string>();
            }
            
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                narratorPawnCache.Clear();
                Log.Message($"[NarratorVirtualPawnManager] 已从存档加载 {narratorPawnIDs.Count} 个叙事者 Pawn ID");
            }
        }

        /// <summary>
        /// 清理所有虚拟 Pawn（调试用）
        /// </summary>
        public void ClearAllVirtualPawns()
        {
            narratorPawnIDs.Clear();
            narratorPawnCache.Clear();
            Log.Warning("[NarratorVirtualPawnManager] 已清空所有虚拟 Pawn");
        }
    }
}
