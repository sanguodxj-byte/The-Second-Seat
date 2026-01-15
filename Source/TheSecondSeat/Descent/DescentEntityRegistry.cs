using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using RimWorld;
using TheSecondSeat.PersonaGeneration;

namespace TheSecondSeat.Descent
{
    /// <summary>
    /// 降临实体注册表 - 通用系统
    /// 在游戏加载时自动收集所有 NarratorPersonaDef 中配置的 descentPawnKind，
    /// 提供统一的 API 来判断任何 Pawn 是否为降临实体。
    /// 
    /// 子模组只需在其 NarratorPersonaDef 中配置 descentPawnKind，
    /// 主模组的所有 Patches 会自动识别该降临体。
    /// </summary>
    public static class DescentEntityRegistry
    {
        // 缓存所有降临体的 ThingDef.defName
        private static HashSet<string> _descentRaceDefNames;
        
        // 降临体 defName -> 对应的 NarratorPersonaDef 映射
        private static Dictionary<string, NarratorPersonaDef> _descentToPersonaMap;
        
        // 是否已初始化
        private static bool _initialized = false;
        
        /// <summary>
        /// 初始化注册表（在游戏加载完 Defs 后调用）
        /// </summary>
        public static void Initialize()
        {
            if (_initialized) return;
            
            _descentRaceDefNames = new HashSet<string>();
            _descentToPersonaMap = new Dictionary<string, NarratorPersonaDef>();
            
            // 遍历所有 NarratorPersonaDef，收集 descentPawnKind
            foreach (var personaDef in DefDatabase<NarratorPersonaDef>.AllDefsListForReading)
            {
                if (personaDef == null) continue;
                
                // 检查是否启用了降临模式且有 descentPawnKind
                if (!personaDef.hasDescentMode) continue;
                if (string.IsNullOrEmpty(personaDef.descentPawnKind)) continue;
                
                // 获取 PawnKindDef 来找到对应的 ThingDef（种族）
                var pawnKindDef = DefDatabase<PawnKindDef>.GetNamedSilentFail(personaDef.descentPawnKind);
                if (pawnKindDef?.race == null)
                {
                    Log.Warning($"[TSS-DescentRegistry] PawnKindDef '{personaDef.descentPawnKind}' not found or has no race for persona '{personaDef.defName}'");
                    continue;
                }
                
                string raceDefName = pawnKindDef.race.defName;
                
                // 注册到集合
                _descentRaceDefNames.Add(raceDefName);
                _descentToPersonaMap[raceDefName] = personaDef;
                
                Log.Message($"[TSS-DescentRegistry] Registered descent entity: race='{raceDefName}' from persona='{personaDef.defName}'");
            }
            
            _initialized = true;
            Log.Message($"[TSS-DescentRegistry] Initialized with {_descentRaceDefNames.Count} descent race(s)");
        }
        
        /// <summary>
        /// 确保已初始化
        /// </summary>
        private static void EnsureInitialized()
        {
            if (!_initialized)
            {
                Initialize();
            }
        }
        
        /// <summary>
        /// 判断 Pawn 是否为降临实体
        /// </summary>
        /// <param name="pawn">要检查的 Pawn</param>
        /// <returns>是否为降临实体</returns>
        public static bool IsDescentEntity(Pawn pawn)
        {
            if (pawn?.def == null) return false;
            
            EnsureInitialized();
            
            return _descentRaceDefNames.Contains(pawn.def.defName);
        }
        
        /// <summary>
        /// 判断 ThingDef 是否为降临实体种族
        /// </summary>
        /// <param name="thingDef">要检查的 ThingDef</param>
        /// <returns>是否为降临实体种族</returns>
        public static bool IsDescentRace(ThingDef thingDef)
        {
            if (thingDef == null) return false;
            
            EnsureInitialized();
            
            return _descentRaceDefNames.Contains(thingDef.defName);
        }
        
        /// <summary>
        /// 获取降临实体对应的 NarratorPersonaDef
        /// </summary>
        /// <param name="pawn">降临实体 Pawn</param>
        /// <returns>对应的 NarratorPersonaDef，如果不是降临实体则返回 null</returns>
        public static NarratorPersonaDef GetPersonaFor(Pawn pawn)
        {
            if (pawn?.def == null) return null;
            
            EnsureInitialized();
            
            if (_descentToPersonaMap.TryGetValue(pawn.def.defName, out var personaDef))
            {
                return personaDef;
            }
            
            return null;
        }
        
        /// <summary>
        /// 获取所有已注册的降临体种族 defName
        /// </summary>
        public static IReadOnlyCollection<string> GetAllDescentRaceDefNames()
        {
            EnsureInitialized();
            return _descentRaceDefNames;
        }
        
        /// <summary>
        /// 获取降临实体应该获得的技能列表
        /// </summary>
        /// <param name="pawn">降临实体 Pawn</param>
        /// <returns>技能 defName 列表</returns>
        public static List<string> GetAbilitiesToGrant(Pawn pawn)
        {
            var personaDef = GetPersonaFor(pawn);
            if (personaDef?.abilitiesToGrant != null)
            {
                return personaDef.abilitiesToGrant;
            }
            return new List<string>();
        }
        
        /// <summary>
        /// 获取降临实体应该获得的 Hediff 列表
        /// </summary>
        /// <param name="pawn">降临实体 Pawn</param>
        /// <returns>Hediff defName 列表</returns>
        public static List<string> GetHediffsToGrant(Pawn pawn)
        {
            var personaDef = GetPersonaFor(pawn);
            if (personaDef?.hediffsToGrant != null)
            {
                return personaDef.hediffsToGrant;
            }
            return new List<string>();
        }
        
        /// <summary>
        /// 重置注册表（用于热重载或测试）
        /// </summary>
        public static void Reset()
        {
            _descentRaceDefNames = null;
            _descentToPersonaMap = null;
            _initialized = false;
        }
    }
}