using System;
using System.Collections.Generic;
using HarmonyLib;
using Verse;
using RimWorld;

namespace TheSecondSeat.Patches
{
    /// <summary>
    /// ⭐ v2.9.0: 事件驱动的建筑伤害缓存系统
    /// 替代全图扫描 listerThings.AllThings 的现代实现
    /// </summary>
    public static class BuildingDamageEventPatches
    {
        // 每个地图的受损建筑缓存
        private static Dictionary<int, HashSet<Thing>> damagedBuildingsByMap = new Dictionary<int, HashSet<Thing>>();
        
        // 伤害阈值（低于此比例视为受损）
        private const float DAMAGE_THRESHOLD = 0.7f;
        
        /// <summary>
        /// 获取指定地图的受损建筑集合
        /// </summary>
        public static HashSet<Thing> GetDamagedBuildings(Map map)
        {
            if (map == null) return new HashSet<Thing>();
            
            if (!damagedBuildingsByMap.TryGetValue(map.uniqueID, out var buildings))
            {
                buildings = new HashSet<Thing>();
                damagedBuildingsByMap[map.uniqueID] = buildings;
            }
            
            return buildings;
        }
        
        /// <summary>
        /// 快速检查是否有受损建筑（O(1) 复杂度）
        /// </summary>
        public static bool HasDamagedBuildings(Map map, int minCount = 1)
        {
            if (map == null) return false;
            
            if (!damagedBuildingsByMap.TryGetValue(map.uniqueID, out var buildings))
            {
                return false;
            }
            
            // 清理已销毁的建筑
            buildings.RemoveWhere(t => t == null || t.Destroyed || !t.Spawned);
            
            return buildings.Count >= minCount;
        }
        
        /// <summary>
        /// 获取受损建筑数量
        /// </summary>
        public static int GetDamagedBuildingCount(Map map)
        {
            if (map == null) return 0;
            
            if (!damagedBuildingsByMap.TryGetValue(map.uniqueID, out var buildings))
            {
                return 0;
            }
            
            // 清理无效引用
            buildings.RemoveWhere(t => t == null || t.Destroyed || !t.Spawned);
            
            return buildings.Count;
        }
        
        /// <summary>
        /// 添加受损建筑到缓存
        /// </summary>
        internal static void AddDamagedBuilding(Thing building)
        {
            if (building?.Map == null) return;
            if (building.def?.building == null) return;
            
            int mapId = building.Map.uniqueID;
            if (!damagedBuildingsByMap.TryGetValue(mapId, out var buildings))
            {
                buildings = new HashSet<Thing>();
                damagedBuildingsByMap[mapId] = buildings;
            }
            
            buildings.Add(building);
        }
        
        /// <summary>
        /// 从缓存移除已修复的建筑
        /// </summary>
        internal static void RemoveRepairedBuilding(Thing building)
        {
            if (building?.Map == null) return;
            
            int mapId = building.Map.uniqueID;
            if (damagedBuildingsByMap.TryGetValue(mapId, out var buildings))
            {
                buildings.Remove(building);
            }
        }
        
        /// <summary>
        /// 清理指定地图的缓存（地图移除时调用）
        /// </summary>
        public static void ClearMapCache(int mapId)
        {
            damagedBuildingsByMap.Remove(mapId);
        }
        
        /// <summary>
        /// 清理所有缓存
        /// </summary>
        public static void ClearAllCaches()
        {
            damagedBuildingsByMap.Clear();
        }
    }
    
    /// <summary>
    /// Harmony Patch: 监听 Thing.PostApplyDamage
    /// </summary>
    [HarmonyPatch(typeof(Thing), nameof(Thing.PostApplyDamage))]
    public static class Thing_PostApplyDamage_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(Thing __instance, DamageInfo dinfo, float totalDamageDealt)
        {
            try
            {
                // 只关心建筑
                if (__instance?.def?.building == null) return;
                if (!__instance.def.useHitPoints) return;
                if (__instance.Destroyed || !__instance.Spawned) return;
                
                // 检查是否低于伤害阈值
                float healthRatio = (float)__instance.HitPoints / __instance.MaxHitPoints;
                if (healthRatio < 0.7f)
                {
                    BuildingDamageEventPatches.AddDamagedBuilding(__instance);
                }
            }
            catch (Exception ex)
            {
                // 静默失败，不影响游戏
                if (Prefs.DevMode)
                {
                    Log.Warning($"[TSS] BuildingDamage patch error: {ex.Message}");
                }
            }
        }
    }
    
    // 注意：GenSpawn.Respawn 在 RimWorld 1.6 中不存在
    // 修复完成事件通过 Thing.HitPoints setter patch 来处理
    
    /// <summary>
    /// Harmony Patch: 监听建筑 HitPoints 变化
    /// 使用 Property Setter Patch
    /// </summary>
    [HarmonyPatch(typeof(Thing))]
    [HarmonyPatch("HitPoints", MethodType.Setter)]
    public static class Thing_HitPoints_Setter_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(Thing __instance)
        {
            try
            {
                if (__instance?.def?.building == null) return;
                if (!__instance.def.useHitPoints) return;
                if (__instance.Destroyed || !__instance.Spawned) return;
                
                // 如果完全修复，从缓存移除
                if (__instance.HitPoints >= __instance.MaxHitPoints)
                {
                    BuildingDamageEventPatches.RemoveRepairedBuilding(__instance);
                }
                // 如果受损，添加到缓存
                else if (__instance.HitPoints < __instance.MaxHitPoints * 0.7f)
                {
                    BuildingDamageEventPatches.AddDamagedBuilding(__instance);
                }
            }
            catch (Exception ex)
            {
                if (Prefs.DevMode)
                {
                    Log.Warning($"[TSS] HitPoints setter patch error: {ex.Message}");
                }
            }
        }
    }
    
    /// <summary>
    /// Harmony Patch: 地图移除时清理缓存
    /// </summary>
    [HarmonyPatch(typeof(Map), nameof(Map.FinalizeLoading))]
    public static class Map_FinalizeLoading_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(Map __instance)
        {
            // 地图加载后初始化缓存（可选：扫描已有受损建筑）
            // 为了性能，我们不做初始扫描，让事件驱动逐步填充
        }
    }
    
    /// <summary>
    /// Harmony Patch: 建筑销毁时从缓存移除
    /// </summary>
    [HarmonyPatch(typeof(Thing), nameof(Thing.Destroy))]
    public static class Thing_Destroy_Patch
    {
        [HarmonyPrefix]
        public static void Prefix(Thing __instance)
        {
            try
            {
                if (__instance?.def?.building != null)
                {
                    BuildingDamageEventPatches.RemoveRepairedBuilding(__instance);
                }
            }
            catch
            {
                // 静默失败
            }
        }
    }
}
