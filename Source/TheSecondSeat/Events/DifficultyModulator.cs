using System;
using System.Linq;
using RimWorld;
using Verse;
using TheSecondSeat.Storyteller;
using HarmonyLib;

namespace TheSecondSeat.Events
{
    /// <summary>
    /// 对话驱动的难度调整器 - 根据好感度和对话历史动态调整原版事件强度
    /// </summary>
    public static class DifficultyModulator
    {
        // 调整范围限制
        private const float MIN_MULTIPLIER = 0.5f;  // 最低 50%
        private const float MAX_MULTIPLIER = 1.5f;  // 最高 150%

        /// <summary>
        /// 调整事件参数（点数、强度）
        /// </summary>
        public static void AdjustIncidentParams(IncidentParms parms, StorytellerAgent agent)
        {
            if (parms?.target == null) return;

            // 获取难度系数
            float multiplier = GetDifficultyMultiplier(agent, parms.target as Map);

            // 应用到点数
            if (parms.points > 0)
            {
                float originalPoints = parms.points;
                parms.points *= multiplier;
                
                Log.Message($"[DifficultyModulator] 事件点数调整：{originalPoints:F0} → {parms.points:F0} (×{multiplier:F2})");
            }
        }

        /// <summary>
        /// 获取难度系数
        /// </summary>
        private static float GetDifficultyMultiplier(StorytellerAgent agent, Map map)
        {
            float multiplier = 1f;

            // 基于好感度调整
            if (agent.affinity > 60f)
            {
                // 好感度高 → 降低难度
                multiplier = 0.7f; // -30%
            }
            else if (agent.affinity > 30f)
            {
                // 好感度中等偏高 → 轻微降低
                multiplier = 0.85f; // -15%
            }
            else if (agent.affinity < -30f && agent.affinity >= -60f)
            {
                // 好感度低 → 增加难度
                multiplier = 1.2f; // +20%
            }
            else if (agent.affinity < -60f)
            {
                // 好感度非常低 → 显著增加难度
                multiplier = 1.3f; // +30%
            }

            // 限制调整范围
            multiplier = Math.Max(MIN_MULTIPLIER, Math.Min(MAX_MULTIPLIER, multiplier));

            return multiplier;
        }

        /// <summary>
        /// 调整事件权重（正面/负面事件发生概率）
        /// </summary>
        public static float GetEventWeightMultiplier(IncidentDef incident, StorytellerAgent agent)
        {
            if (incident == null || agent == null)
                return 1f;

            bool isPositive = IsPositiveEvent(incident);
            bool isNegative = IsNegativeEvent(incident);
            
            float multiplier = 1f;

            // 好感度高：增加正面事件，减少负面事件
            if (agent.affinity > 60f)
            {
                if (isPositive)
                    multiplier = 1.5f; // 正面事件 +50%
                else if (isNegative)
                    multiplier = 0.7f; // 负面事件 -30%
            }
            // 好感度低：减少正面事件，增加负面事件
            else if (agent.affinity < -30f)
            {
                if (isPositive)
                    multiplier = 0.7f; // 正面事件 -30%
                else if (isNegative)
                    multiplier = 1.3f; // 负面事件 +30%
            }

            // 限制范围
            multiplier = Math.Max(MIN_MULTIPLIER, Math.Min(MAX_MULTIPLIER, multiplier));

            if (multiplier != 1f)
            {
                Log.Message($"[DifficultyModulator] 事件 '{incident.defName}' 权重调整：×{multiplier:F2} (好感度: {agent.affinity:F0})");
            }

            return multiplier;
        }

        /// <summary>
        /// 判断是否为正面事件
        /// </summary>
        private static bool IsPositiveEvent(IncidentDef incident)
        {
            // 明确的正面事件
            if (incident == IncidentDefOf.TraderCaravanArrival ||
                incident == IncidentDefOf.WandererJoin ||
                incident == IncidentDefOf.FarmAnimalsWanderIn ||
                incident.defName.Contains("ResourcePod") ||
                incident.defName.Contains("Traveler") ||
                incident.defName.Contains("Trader"))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// 判断是否为负面事件
        /// </summary>
        private static bool IsNegativeEvent(IncidentDef incident)
        {
            // 明确的负面事件
            if (incident == IncidentDefOf.RaidEnemy ||
                incident == IncidentDefOf.ToxicFallout ||
                incident == IncidentDefOf.Eclipse ||
                incident.defName.Contains("Disease") ||
                incident.defName.Contains("Raid"))
            {
                return true;
            }

            // 通过分类判断
            if (incident.category == IncidentCategoryDefOf.ThreatBig ||
                incident.category == IncidentCategoryDefOf.ThreatSmall ||
                incident.category == IncidentCategoryDefOf.DiseaseHuman)
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// 发送难度调整通知
        /// </summary>
        public static void SendDifficultyAdjustmentNotification(StorytellerAgent agent)
        {
            string message = "";
            string title = "叙事者";

            if (agent.affinity > 60f)
            {
                message = $"看到你这么努力，我会适当减轻困难~\n\n" +
                         $"当前好感度：{agent.affinity:F0}\n" +
                         $"效果：袭击强度 -30%，正面事件 +50%";
                title = "叙事者的关怀";
            }
            else if (agent.affinity < -30f)
            {
                message = $"既然你不重视我们的关系，那就自己面对困难吧。\n\n" +
                         $"当前好感度：{agent.affinity:F0}\n" +
                         $"效果：袭击强度 +{(agent.affinity < -60f ? "30" : "20")}%，正面事件 -30%";
                title = "叙事者的冷漠";
            }
            else
            {
                // 中性好感度，不发送通知
                return;
            }

            Find.LetterStack.ReceiveLetter(
                title,
                message,
                agent.affinity > 60f ? LetterDefOf.PositiveEvent : LetterDefOf.NegativeEvent
            );
        }
    }

    /// <summary>
    /// Harmony Patch：拦截事件执行，调整强度
    /// </summary>
    [HarmonyPatch(typeof(IncidentWorker), nameof(IncidentWorker.TryExecute))]
    public static class Patch_IncidentWorker_TryExecute
    {
        [HarmonyPrefix]
        public static void Prefix(IncidentParms parms)
        {
            try
            {
                var agent = Current.Game?.GetComponent<Narrator.NarratorManager>()?.GetStorytellerAgent();
                if (agent == null) return;

                // 调整事件参数
                DifficultyModulator.AdjustIncidentParams(parms, agent);
            }
            catch (Exception ex)
            {
                Log.Error($"[Patch_IncidentWorker_TryExecute] 错误: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Harmony Patch：拦截事件权重计算
    /// </summary>
    [HarmonyPatch(typeof(StorytellerComp), "IncidentChanceFinal")]
    public static class Patch_StorytellerComp_IncidentChanceFinal
    {
        [HarmonyPostfix]
        public static void Postfix(IncidentDef def, ref float __result)
        {
            try
            {
                var agent = Current.Game?.GetComponent<Narrator.NarratorManager>()?.GetStorytellerAgent();
                if (agent == null) return;

                // 调整事件权重
                float multiplier = DifficultyModulator.GetEventWeightMultiplier(def, agent);
                __result *= multiplier;
            }
            catch (Exception ex)
            {
                Log.Error($"[Patch_StorytellerComp_IncidentChanceFinal] 错误: {ex.Message}");
            }
        }
    }
}
