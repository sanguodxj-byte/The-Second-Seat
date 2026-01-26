using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Verse;

namespace TheSecondSeat.PersonaGeneration
{
    /// <summary>
    /// 服装等级枚举（根据好感度）
    /// </summary>
    public enum OutfitTier
    {
        Hostile,    // -100 ~ -50: 敌意，冷峻疏离
        Cold,       // -50 ~ -10: 冷漠，正式距离
        Neutral,    // -10 ~ +30: 中性，日常专业
        Warm,       // +30 ~ +60: 温暖，亲切舒适
        Intimate,   // +60 ~ +85: 亲密，放松私密
        Devoted     // +85 ~ +100: 献身，极度亲密
    }
    
    /// <summary>
    /// 服装状态（每个人格独立）
    /// ⭐ v2.5.0: 增加 OutfitDef 支持
    /// </summary>
    public class OutfitState : IExposable
    {
        public OutfitTier currentTier = OutfitTier.Neutral;
        public string currentOutfitPath = "";
        public int lastChangeTimestamp = 0;
        public int changeIntervalTicks = 30000; // 12小时 = 30,000 ticks
        
        // ⭐ v2.5.0: OutfitDef 支持
        public string currentOutfitDefName = "";  // 当前激活的 OutfitDef
        public string currentOutfitTag = "";      // 当前服装标签
        public int lastDefCheckTimestamp = 0;     // 上次检查 OutfitDef 的时间
        
        public void ExposeData()
        {
            Scribe_Values.Look(ref currentTier, "currentTier", OutfitTier.Neutral);
            Scribe_Values.Look(ref currentOutfitPath, "currentOutfitPath", "");
            Scribe_Values.Look(ref lastChangeTimestamp, "lastChangeTimestamp", 0);
            Scribe_Values.Look(ref changeIntervalTicks, "changeIntervalTicks", 30000);
            
            // ⭐ v2.5.0: 保存 OutfitDef 状态
            Scribe_Values.Look(ref currentOutfitDefName, "currentOutfitDefName", "");
            Scribe_Values.Look(ref currentOutfitTag, "currentOutfitTag", "");
            Scribe_Values.Look(ref lastDefCheckTimestamp, "lastDefCheckTimestamp", 0);
        }
    }
    
    /// <summary>
    /// 服装差分系统
    /// ? 根据好感度和时间动态更换人格的服装立绘
    /// ⭐ v2.5.0: 支持 OutfitDef 条件切换
    /// </summary>
    public static class OutfitSystem
    {
        private static Dictionary<string, OutfitState> outfitStates = new Dictionary<string, OutfitState>();
        
        // ⭐ v2.5.0: 好感度缓存（供 OutfitDef 使用）
        private static Dictionary<string, float> affinityCache = new Dictionary<string, float>();
        
        private const string OUTFITS_PATH = "UI/Narrators/9x16/{0}/Outfits/";
        
        // ⭐ v2.5.0: OutfitDef 检查间隔（ticks）
        private const int OUTFIT_DEF_CHECK_INTERVAL = 2500; // 约1游戏小时
        
        /// <summary>
        /// 获取服装状态
        /// </summary>
        public static OutfitState GetOutfitState(string personaDefName)
        {
            if (!outfitStates.ContainsKey(personaDefName))
            {
                outfitStates[personaDefName] = new OutfitState();
            }
            return outfitStates[personaDefName];
        }
        
        /// <summary>
        /// 根据好感度获取服装等级
        /// ? 注意：affinity 是 StorytellerAgent 的 -100 到 +100 范围
        /// </summary>
        public static OutfitTier GetOutfitTierByAffinity(float affinity)
        {
            if (affinity >= 85f) return OutfitTier.Devoted;     // 850+
            if (affinity >= 60f) return OutfitTier.Intimate;    // 600+
            if (affinity >= 30f) return OutfitTier.Warm;        // 300+
            if (affinity >= -10f) return OutfitTier.Neutral;    // -100 ~ 300
            if (affinity >= -50f) return OutfitTier.Cold;       // -500 ~ -100
            return OutfitTier.Hostile;                          // -1000 ~ -500
        }
        
        /// <summary>
        /// 检查是否需要更换服装
        /// </summary>
        public static bool ShouldChangeOutfit(string personaDefName, float currentAffinity)
        {
            var state = GetOutfitState(personaDefName);
            
            // 检查1: 好感度等级变化（立即更换）
            var newTier = GetOutfitTierByAffinity(currentAffinity);
            if (newTier != state.currentTier)
            {
                Log.Message($"[OutfitSystem] {personaDefName} 好感度等级变化: {state.currentTier} → {newTier}");
                return true;
            }
            
            // 检查2: 时间间隔（12小时）
            int currentTick = Find.TickManager.TicksGame;
            int elapsedTicks = currentTick - state.lastChangeTimestamp;
            
            if (elapsedTicks >= state.changeIntervalTicks)
            {
                Log.Message($"[OutfitSystem] {personaDefName} 服装定时更换: {elapsedTicks} ticks ({elapsedTicks / 2500}小时)");
                return true;
            }
            
            return false;
        }
        
        /// <summary>
        /// 更换服装
        /// </summary>
        public static string ChangeOutfit(string personaDefName, float affinity)
        {
            var state = GetOutfitState(personaDefName);
            var newTier = GetOutfitTierByAffinity(affinity);
            
            // 获取该等级的所有可用服装
            var availableOutfits = GetAvailableOutfits(personaDefName, newTier);
            
            if (availableOutfits.Count == 0)
            {
                // 没有服装差分，清空当前服装
                state.currentOutfitPath = "";
                state.currentTier = newTier;
                Log.Message($"[OutfitSystem] {personaDefName} 没有 {newTier} 等级的服装差分");
                return null;
            }
            
            // 随机选择一个（尽量避免重复）
            string newOutfit;
            if (availableOutfits.Count == 1)
            {
                newOutfit = availableOutfits[0];
            }
            else
            {
                // 排除当前服装，避免连续相同
                var candidates = availableOutfits
                    .Where(o => o != state.currentOutfitPath)
                    .ToList();
                
                if (candidates.Count > 0)
                {
                    newOutfit = candidates.RandomElement();
                }
                else
                {
                    newOutfit = availableOutfits.RandomElement();
                }
            }
            
            // 更新状态
            state.currentTier = newTier;
            state.currentOutfitPath = newOutfit;
            state.lastChangeTimestamp = Find.TickManager.TicksGame;
            
            Log.Message($"[OutfitSystem] ? {personaDefName} 更换服装: {newTier} - {Path.GetFileNameWithoutExtension(newOutfit)}");
            
            return newOutfit;
        }
        
        /// <summary>
        /// 获取指定等级的所有可用服装
        /// </summary>
        private static List<string> GetAvailableOutfits(string personaDefName, OutfitTier tier)
        {
            var outfits = new List<string>();
            string tierName = tier.ToString().ToLower();
            string basePath = string.Format(OUTFITS_PATH, personaDefName);
            
            // 检查所有变体（1-9）
            for (int i = 1; i <= 9; i++)
            {
                string path = basePath + $"{tierName}_{i}";
                var texture = ContentFinder<Texture2D>.Get(path, false);
                
                if (texture != null)
                {
                    outfits.Add(path);
                }
            }
            
            return outfits;
        }
        
        /// <summary>
        /// 获取当前服装路径
        /// </summary>
        public static string GetCurrentOutfitPath(string personaDefName)
        {
            var state = GetOutfitState(personaDefName);
            return state.currentOutfitPath;
        }
        
        /// <summary>
        /// 手动设置服装
        /// </summary>
        public static void SetOutfit(string personaDefName, string outfitPath)
        {
            var state = GetOutfitState(personaDefName);
            state.currentOutfitPath = outfitPath;
            state.lastChangeTimestamp = Find.TickManager.TicksGame;
            
            Log.Message($"[OutfitSystem] 手动设置服装: {personaDefName} - {outfitPath}");
        }
        
        /// <summary>
        /// 清空所有服装状态
        /// </summary>
        public static void ClearAllStates()
        {
            outfitStates.Clear();
            Log.Message("[OutfitSystem] 清空所有服装状态");
        }
        
        /// <summary>
        /// 获取下次更换倒计时（游戏小时）
        /// </summary>
        public static float GetTimeUntilNextChange(string personaDefName)
        {
            var state = GetOutfitState(personaDefName);
            int currentTick = Find.TickManager.TicksGame;
            int elapsedTicks = currentTick - state.lastChangeTimestamp;
            int remainingTicks = state.changeIntervalTicks - elapsedTicks;
            
            if (remainingTicks <= 0)
            {
                return 0f;
            }
            
            // 转换为游戏小时（1小时 = 2500 ticks）
            return remainingTicks / 2500f;
        }
        
        /// <summary>
        /// 获取调试信息
        /// </summary>
        public static string GetDebugInfo()
        {
            var info = $"[OutfitSystem] 服装状态数量: {outfitStates.Count}\n";
            
            foreach (var kvp in outfitStates)
            {
                var state = kvp.Value;
                float hoursUntilChange = GetTimeUntilNextChange(kvp.Key);
                
                info += $"  {kvp.Key}:\n";
                info += $"    服装等级: {state.currentTier}\n";
                info += $"    当前服装: {Path.GetFileNameWithoutExtension(state.currentOutfitPath)}\n";
                info += $"    下次更换: {hoursUntilChange:F1} 小时后\n";
            }
            
            return info;
        }
        
        /// <summary>
        /// 保存所有服装状态
        /// </summary>
        public static void ExposeData()
        {
            Scribe_Collections.Look(ref outfitStates, "outfitStates", LookMode.Value, LookMode.Deep);
            
            if (Scribe.mode == LoadSaveMode.LoadingVars && outfitStates == null)
            {
                outfitStates = new Dictionary<string, OutfitState>();
            }
        }
        
        // =====================================================
        // ⭐ v2.5.0: OutfitDef 系统 - 基于条件的自动服装切换
        // =====================================================
        
        /// <summary>
        /// ⭐ v2.5.0: 更新好感度缓存（供 OutfitDef 评估使用）
        /// </summary>
        public static void UpdateAffinityCache(string personaDefName, float affinity)
        {
            affinityCache[personaDefName] = affinity;
        }
        
        /// <summary>
        /// ⭐ v2.5.0: 获取当前好感度
        /// </summary>
        public static float GetCurrentAffinity(string personaDefName)
        {
            if (affinityCache.TryGetValue(personaDefName, out float affinity))
            {
                return affinity;
            }
            return 0f;
        }
        
        /// <summary>
        /// ⭐ v2.5.0: 检查并应用 OutfitDef 切换
        /// 由 GameComponent 定期调用
        /// </summary>
        /// <returns>当前激活的 OutfitDef，如果没有则返回 null</returns>
        public static OutfitDef CheckAndApplyOutfitDef(string personaDefName)
        {
            var state = GetOutfitState(personaDefName);
            
            // 检查间隔（避免每帧检查）
            int currentTick = 0;
            try
            {
                currentTick = Find.TickManager?.TicksGame ?? 0;
            }
            catch { /* 游戏未加载 */ }
            
            // 每隔一段时间检查一次
            if (currentTick > 0 && currentTick - state.lastDefCheckTimestamp < OUTFIT_DEF_CHECK_INTERVAL)
            {
                // 返回当前激活的 OutfitDef
                if (!string.IsNullOrEmpty(state.currentOutfitDefName))
                {
                    return DefDatabase<OutfitDef>.GetNamedSilentFail(state.currentOutfitDefName);
                }
                return null;
            }
            
            state.lastDefCheckTimestamp = currentTick;
            
            // 创建评估上下文
            var context = OutfitEvaluationContext.CreateCurrent(personaDefName);
            
            // 选择最佳服装
            var bestOutfit = OutfitDefManager.SelectBestOutfit(personaDefName, context);
            
            // 检查是否需要切换
            string newDefName = bestOutfit?.defName ?? "";
            
            if (newDefName != state.currentOutfitDefName)
            {
                // 服装变化
                string oldTag = state.currentOutfitTag;
                string newTag = bestOutfit?.outfitTag ?? "";
                
                state.currentOutfitDefName = newDefName;
                state.currentOutfitTag = newTag;
                state.lastChangeTimestamp = currentTick;
                
                if (Prefs.DevMode)
                {
                    Log.Message($"[OutfitSystem] 🎭 {personaDefName} 服装切换: {oldTag} → {newTag}");
                }
                
                // 触发切换事件
                OnOutfitDefChanged?.Invoke(personaDefName, bestOutfit);
                
                // 清除立绘缓存以强制重新合成
                if (bestOutfit != null)
                {
                    LayeredPortraitCompositor.ClearAllCache();
                }
            }
            
            return bestOutfit;
        }
        
        /// <summary>
        /// ⭐ v2.5.0: 获取当前激活的 OutfitDef
        /// </summary>
        public static OutfitDef GetCurrentOutfitDef(string personaDefName)
        {
            var state = GetOutfitState(personaDefName);
            if (string.IsNullOrEmpty(state.currentOutfitDefName))
            {
                return null;
            }
            return DefDatabase<OutfitDef>.GetNamedSilentFail(state.currentOutfitDefName);
        }
        
        /// <summary>
        /// ⭐ v2.5.0: 获取当前服装标签
        /// </summary>
        public static string GetCurrentOutfitTag(string personaDefName)
        {
            var state = GetOutfitState(personaDefName);
            return state.currentOutfitTag ?? "";
        }
        
        /// <summary>
        /// ⭐ v2.5.0: 手动设置 OutfitDef
        /// </summary>
        public static void SetOutfitDef(string personaDefName, string outfitDefName)
        {
            var state = GetOutfitState(personaDefName);
            var outfitDef = DefDatabase<OutfitDef>.GetNamedSilentFail(outfitDefName);
            
            state.currentOutfitDefName = outfitDefName ?? "";
            state.currentOutfitTag = outfitDef?.outfitTag ?? "";
            
            try
            {
                state.lastChangeTimestamp = Find.TickManager?.TicksGame ?? 0;
            }
            catch { /* 忽略 */ }
            
            Log.Message($"[OutfitSystem] 手动设置服装定义: {personaDefName} → {outfitDefName}");
            
            // 触发切换事件
            OnOutfitDefChanged?.Invoke(personaDefName, outfitDef);
            
            // 清除缓存
            LayeredPortraitCompositor.ClearAllCache();
        }
        
        /// <summary>
        /// ⭐ v2.5.0: 通过标签设置服装
        /// </summary>
        public static void SetOutfitByTag(string personaDefName, string outfitTag)
        {
            var outfitDef = OutfitDefManager.GetByTag(personaDefName, outfitTag);
            if (outfitDef != null)
            {
                SetOutfitDef(personaDefName, outfitDef.defName);
            }
            else if (Prefs.DevMode)
            {
                Log.Warning($"[OutfitSystem] 未找到服装标签: {outfitTag} for {personaDefName}");
            }
        }
        
        /// <summary>
        /// ⭐ v2.5.0: 清除当前 OutfitDef（恢复默认）
        /// </summary>
        public static void ClearOutfitDef(string personaDefName)
        {
            var state = GetOutfitState(personaDefName);
            state.currentOutfitDefName = "";
            state.currentOutfitTag = "";
            
            Log.Message($"[OutfitSystem] 清除服装定义: {personaDefName}");
            
            OnOutfitDefChanged?.Invoke(personaDefName, null);
            LayeredPortraitCompositor.ClearAllCache();
        }
        
        /// <summary>
        /// ⭐ v2.5.0: 检查是否处于睡眠时间
        /// </summary>
        public static bool IsSleepingTime()
        {
            int hour = DateTime.Now.Hour;
            return hour >= 22 || hour < 7;
        }
        
        /// <summary>
        /// ⭐ v2.5.0: 检查是否应该穿睡衣
        /// </summary>
        public static bool ShouldWearPajamas(string personaDefName)
        {
            // 检查是否有睡衣定义
            var pajamas = OutfitDefManager.GetByTag(personaDefName, "Pajamas");
            if (pajamas == null) return false;
            
            var context = OutfitEvaluationContext.CreateCurrent(personaDefName);
            return pajamas.EvaluateConditions(context);
        }
        
        /// <summary>
        /// ⭐ v2.5.0: 服装切换事件
        /// </summary>
        public static event Action<string, OutfitDef> OnOutfitDefChanged;
        
        /// <summary>
        /// ⭐ v2.5.0: 获取扩展调试信息
        /// </summary>
        public static string GetExtendedDebugInfo()
        {
            var info = GetDebugInfo();
            
            info += "\n[OutfitDef 状态]\n";
            
            foreach (var kvp in outfitStates)
            {
                var state = kvp.Value;
                info += $"  {kvp.Key}:\n";
                info += $"    当前 OutfitDef: {state.currentOutfitDefName}\n";
                info += $"    当前标签: {state.currentOutfitTag}\n";
                
                // 列出可用服装
                var outfits = OutfitDefManager.GetOutfitsForPersona(kvp.Key);
                if (outfits.Count > 0)
                {
                    info += $"    可用服装: {string.Join(", ", outfits.Select(o => o.outfitTag))}\n";
                }
            }
            
            info += $"\n睡眠时间: {IsSleepingTime()}\n";
            info += $"当前小时: {DateTime.Now.Hour}\n";
            
            return info;
        }
    }
}
