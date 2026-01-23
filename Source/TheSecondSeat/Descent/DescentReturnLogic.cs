using System;
using System.Linq;
using Verse;
using RimWorld;
using TheSecondSeat.Narrator;
using TheSecondSeat.PersonaGeneration;

namespace TheSecondSeat.Descent
{
    /// <summary>
    /// 降临回归逻辑 - 负责判断回归时机和性格影响
    /// </summary>
    public static class DescentReturnLogic
    {
        // 回归判断参数
        private const int MIN_DESCENT_DURATION = 18000;   // 最短降临时间 5分钟
        private const int MAX_DESCENT_DURATION = 216000;  // 最长降临时间 1小时

        /// <summary>
        /// 判断叙事者是否应该回归
        /// </summary>
        public static bool ShouldReturn(Pawn descentPawn, int descentStartTick, out int newStartTick)
        {
            newStartTick = descentStartTick;
            
            if (descentPawn == null) return false;
            
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
                Log.Message("[DescentReturnLogic] 降临时间过长，强制回归");
                return true;
            }
            
            // 3. 获取人格配置
            var manager = Current.Game?.GetComponent<NarratorManager>();
            var persona = manager?.GetCurrentPersona();
            if (persona == null) return true;
            
            // 4. 基于性格判断回归时机
            return ShouldReturnBasedOnPersonality(persona, elapsedTicks, ref newStartTick);
        }

        /// <summary>
        /// 基于性格判断是否回归
        /// </summary>
        private static bool ShouldReturnBasedOnPersonality(NarratorPersonaDef persona, int elapsedTicks, ref int startTick)
        {
            var manager = Current.Game?.GetComponent<NarratorManager>();
            var agent = manager?.GetStorytellerAgent();
            float affinity = agent?.GetAffinity() ?? 0f;
            
            // 基础停留时间（30分钟）
            int baseStayDuration = 108000;
            
            // ==================== 性格影响停留时间 ====================
            
            // 社交型人格：喜欢和殖民者相处，停留更久
            if (HasPersonalityTag(persona, "社交", "外向", "友善"))
            {
                baseStayDuration = (int)(baseStayDuration * 1.5f);
            }
            
            // 高傲/独立型人格：不喜欢长时间停留
            if (HasPersonalityTag(persona, "高傲", "冷淡", "独立"))
            {
                baseStayDuration = (int)(baseStayDuration * 0.7f);
            }
            
            // 好奇型人格：想看看殖民地
            if (HasPersonalityTag(persona, "好奇", "活泼", "调皮"))
            {
                baseStayDuration = (int)(baseStayDuration * 1.3f);
            }
            
            // 懒惰/悠闲型：可能蹭顿饭再走
            if (HasPersonalityTag(persona, "懒惰", "悠闲", "随性"))
            {
                if (HasGoodFood())
                {
                    baseStayDuration = (int)(baseStayDuration * 1.8f);
                }
            }
            
            // ==================== 好感度影响 ====================
            
            if (affinity > 80f)
            {
                baseStayDuration = (int)(baseStayDuration * 1.2f);
            }
            else if (affinity < 30f)
            {
                baseStayDuration = (int)(baseStayDuration * 0.5f);
            }
            
            // ==================== 随机因素 ====================
            
            float randomFactor = Rand.Range(0.9f, 1.2f);
            baseStayDuration = (int)(baseStayDuration * randomFactor);
            
            // 如果已经超过计算出的停留时间，应该回归
            if (elapsedTicks > baseStayDuration)
            {
                // 10%概率继续停留（"再待一会儿"）
                if (Rand.Chance(0.1f))
                {
                    Log.Message($"[DescentReturnLogic] {persona.narratorName} 决定再待一会儿");
                    startTick += 36000;
                    return false;
                }
                
                return true;
            }
            
            return false;
        }

        /// <summary>
        /// 检查人格是否有特定标签
        /// </summary>
        private static bool HasPersonalityTag(NarratorPersonaDef persona, params string[] tags)
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
        /// 检查是否有好吃的食物
        /// </summary>
        private static bool HasGoodFood()
        {
            var map = Find.CurrentMap;
            if (map == null) return false;
            
            return map.listerThings.ThingsInGroup(ThingRequestGroup.FoodSourceNotPlantOrTree)
                .Any(t => t.def.IsNutritionGivingIngestible && 
                         t.TryGetComp<CompIngredients>() != null);
        }
    }
}