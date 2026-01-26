using System.Collections.Generic;
using Verse;

namespace TheSecondSeat.Defs
{
    /// <summary>
    /// ⭐ v2.9.0: 数据驱动的行为参数定义
    /// 所有硬编码的数值都应移至此 Def，通过 XML 配置
    /// </summary>
    public class NarratorBehaviorDef : Def
    {
        // ==================== 好感度阈值 ====================
        
        /// <summary>
        /// 自动执行建议所需的最低好感度
        /// 原硬编码: 85f
        /// </summary>
        public float minAffinityForAutoAction = 85f;
        
        /// <summary>
        /// 主动提出建议所需的最低好感度
        /// 原硬编码: 30f
        /// </summary>
        public float minAffinityForSuggestions = 30f;
        
        /// <summary>
        /// 批准收获建议所需的好感度
        /// 原硬编码: 60f
        /// </summary>
        public float affinityForHarvestApproval = 60f;
        
        /// <summary>
        /// 批准修复建议所需的好感度
        /// 原硬编码: 70f
        /// </summary>
        public float affinityForRepairApproval = 70f;
        
        /// <summary>
        /// 批准紧急撤退建议所需的好感度
        /// 原硬编码: 80f
        /// </summary>
        public float affinityForEmergencyApproval = 80f;
        
        /// <summary>
        /// 操控型人格自动执行的好感度阈值
        /// 原硬编码: 75f
        /// </summary>
        public float manipulativeAutoActionThreshold = 75f;
        
        // ==================== 健康阈值 ====================
        
        /// <summary>
        /// 低生命值阈值（触发紧急建议）
        /// 原硬编码: 50
        /// </summary>
        public int lowHealthThreshold = 50;
        
        /// <summary>
        /// 食物短缺阈值
        /// 原硬编码: 50
        /// </summary>
        public int lowFoodThreshold = 50;
        
        // ==================== 建筑相关 ====================
        
        /// <summary>
        /// 建筑受损阈值（低于此比例视为受损）
        /// 原硬编码: 0.7f
        /// </summary>
        public float buildingDamageThreshold = 0.7f;
        
        /// <summary>
        /// 触发修复建议的最少受损建筑数量
        /// 原硬编码: 3
        /// </summary>
        public int minDamagedBuildingsForSuggestion = 3;
        
        // ==================== 农业相关 ====================
        
        /// <summary>
        /// 触发收获建议的最少成熟作物数量
        /// 原硬编码: 10
        /// </summary>
        public int minMatureCropsForSuggestion = 10;
        
        // ==================== 时间间隔 ====================
        
        /// <summary>
        /// 自主行为检查间隔（ticks）
        /// 原硬编码: 18000 (5分钟)
        /// </summary>
        public int checkIntervalTicks = 18000;
        
        /// <summary>
        /// 拒绝建议时的好感度惩罚
        /// 原硬编码: -0.5f
        /// </summary>
        public float rejectSuggestionPenalty = -0.5f;
        
        // ==================== 静态访问器 ====================
        
        private static NarratorBehaviorDef cachedDefault;
        
        /// <summary>
        /// 获取默认行为定义
        /// </summary>
        public static NarratorBehaviorDef Default
        {
            get
            {
                if (cachedDefault == null)
                {
                    cachedDefault = DefDatabase<NarratorBehaviorDef>.GetNamed("DefaultBehavior", errorOnFail: false);
                    
                    // 如果没有找到，创建一个内存中的默认值
                    if (cachedDefault == null)
                    {
                        cachedDefault = new NarratorBehaviorDef
                        {
                            defName = "DefaultBehavior_Fallback"
                        };
                        
                        if (Prefs.DevMode)
                        {
                            Log.Warning("[TSS] NarratorBehaviorDef 'DefaultBehavior' not found, using fallback values");
                        }
                    }
                }
                
                return cachedDefault;
            }
        }
        
        /// <summary>
        /// 清除缓存（热重载时调用）
        /// </summary>
        public static void ClearCache()
        {
            cachedDefault = null;
        }
    }
}
