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
    /// </summary>
    public class OutfitState : IExposable
    {
        public OutfitTier currentTier = OutfitTier.Neutral;
        public string currentOutfitPath = "";
        public int lastChangeTimestamp = 0;
        public int changeIntervalTicks = 30000; // 12小时 = 30,000 ticks
        
        public void ExposeData()
        {
            Scribe_Values.Look(ref currentTier, "currentTier", OutfitTier.Neutral);
            Scribe_Values.Look(ref currentOutfitPath, "currentOutfitPath", "");
            Scribe_Values.Look(ref lastChangeTimestamp, "lastChangeTimestamp", 0);
            Scribe_Values.Look(ref changeIntervalTicks, "changeIntervalTicks", 30000);
        }
    }
    
    /// <summary>
    /// 服装差分系统
    /// ? 根据好感度和时间动态更换人格的服装立绘
    /// </summary>
    public static class OutfitSystem
    {
        private static Dictionary<string, OutfitState> outfitStates = new Dictionary<string, OutfitState>();
        
        private const string OUTFITS_PATH = "UI/Narrators/9x16/{0}/Outfits/";
        
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
    }
}
