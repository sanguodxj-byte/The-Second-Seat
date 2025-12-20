using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using TheSecondSeat.Narrator;

namespace TheSecondSeat.Events
{
    /// <summary>
    /// 好感度驱动事件系统 - 根据AI好感度动态触发正面/负面事件
    /// ? 新增：事件冷却时间，防止 AI 暴走
    /// </summary>
    public class AffinityDrivenEvents
    {
        private static AffinityDrivenEvents? instance;
        public static AffinityDrivenEvents Instance => instance ??= new AffinityDrivenEvents();

        // ? 事件冷却字典：事件类型 → 上次触发时间（游戏 Tick）
        private Dictionary<string, int> eventCooldowns = new Dictionary<string, int>();
        
        // ? 冷却时间配置（游戏 Tick）
        private const int NEGATIVE_EVENT_COOLDOWN = 60000 * 24; // 24 小时（游戏内时间）
        private const int POSITIVE_EVENT_COOLDOWN = 60000 * 12; // 12 小时
        private const int NEUTRAL_EVENT_COOLDOWN = 60000 * 6;   // 6 小时

        /// <summary>
        /// ? 检查事件是否在冷却中
        /// </summary>
        private bool IsEventOnCooldown(string eventType, int cooldownTicks)
        {
            if (!eventCooldowns.TryGetValue(eventType, out int lastTriggerTick))
            {
                return false; // 从未触发过，无冷却
            }

            int currentTick = Find.TickManager.TicksGame;
            int elapsedTicks = currentTick - lastTriggerTick;

            if (elapsedTicks < cooldownTicks)
            {
                int remainingHours = (cooldownTicks - elapsedTicks) / 2500; // 2500 ticks ≈ 1 小时
                Log.Message($"[AffinityDrivenEvents] 事件 '{eventType}' 冷却中，剩余 {remainingHours} 小时");
                return true;
            }

            return false;
        }

        /// <summary>
        /// ? 记录事件触发时间
        /// </summary>
        private void RecordEventTrigger(string eventType)
        {
            eventCooldowns[eventType] = Find.TickManager.TicksGame;
            Log.Message($"[AffinityDrivenEvents] 事件 '{eventType}' 已触发，开始冷却");
        }

        /// <summary>
        /// 触发负面事件（AI 惩罚玩家）
        /// ? 新增：冷却时间保护
        /// </summary>
        public void TriggerNegativeEvent(Map map, float severity = 0.5f)
        {
            // ? 检查冷却时间
            if (IsEventOnCooldown("NegativeEvent", NEGATIVE_EVENT_COOLDOWN))
            {
                Log.Warning("[AffinityDrivenEvents] 负面事件冷却中，跳过触发");
                Messages.Message("叙事者的负面事件冷却中（24小时一次）", MessageTypeDefOf.RejectInput);
                return;
            }

            try
            {
                // ? 根据严重程度选择事件
                IncidentDef? incident = null;

                if (severity >= 0.8f)
                {
                    // 极高严重：机械族/虫族
                    var possibleIncidents = new[]
                    {
                        IncidentDefOf.RaidEnemy,
                        DefDatabase<IncidentDef>.GetNamed("Infestation", errorOnFail: false)
                    }.Where(i => i != null).ToList();

                    incident = possibleIncidents.RandomElement();
                }
                else if (severity >= 0.5f)
                {
                    // 中等严重：疾病/事故
                    var possibleIncidents = new[]
                    {
                        DefDatabase<IncidentDef>.GetNamed("Disease_Plague", errorOnFail: false),
                        DefDatabase<IncidentDef>.GetNamed("ShortCircuit", errorOnFail: false)
                    }.Where(i => i != null).ToList();

                    incident = possibleIncidents.Any() ? possibleIncidents.RandomElement() : IncidentDefOf.RaidEnemy;
                }
                else
                {
                    // 轻度：心灵头痛
                    incident = DefDatabase<IncidentDef>.GetNamed("PsychicDrone", errorOnFail: false) ?? IncidentDefOf.RaidEnemy;
                }

                if (incident == null)
                {
                    Log.Warning("[AffinityDrivenEvents] 无可用负面事件");
                    return;
                }

                // ? 触发事件
                IncidentParms parms = StorytellerUtility.DefaultParmsNow(incident.category, map);
                parms.forced = true;
                
                if (incident.Worker.TryExecute(parms))
                {
                    // ? 记录冷却
                    RecordEventTrigger("NegativeEvent");
                    
                    Log.Message($"[AffinityDrivenEvents] 负面事件已触发: {incident.LabelCap}");
                    Messages.Message($"叙事者触发了负面事件: {incident.LabelCap}", MessageTypeDefOf.NegativeEvent);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[AffinityDrivenEvents] 触发负面事件失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 触发正面事件（AI 奖励玩家）
        /// ? 新增：冷却时间保护
        /// </summary>
        public void TriggerPositiveEvent(Map map)
        {
            // ? 检查冷却时间
            if (IsEventOnCooldown("PositiveEvent", POSITIVE_EVENT_COOLDOWN))
            {
                Log.Message("[AffinityDrivenEvents] 正面事件冷却中，跳过触发");
                return;
            }

            try
            {
                var possibleIncidents = new[]
                {
                    IncidentDefOf.TraderCaravanArrival,
                    DefDatabase<IncidentDef>.GetNamed("ResourcePodCrash", errorOnFail: false),
                    DefDatabase<IncidentDef>.GetNamed("WandererJoin", errorOnFail: false)
                }.Where(i => i != null).ToList();

                var incident = possibleIncidents.RandomElement();

                if (incident == null)
                {
                    Log.Warning("[AffinityDrivenEvents] 无可用正面事件");
                    return;
                }

                IncidentParms parms = StorytellerUtility.DefaultParmsNow(incident.category, map);
                parms.forced = true;
                
                if (incident.Worker.TryExecute(parms))
                {
                    // ? 记录冷却
                    RecordEventTrigger("PositiveEvent");
                    
                    Log.Message($"[AffinityDrivenEvents] 正面事件已触发: {incident.LabelCap}");
                    Messages.Message($"叙事者赠予你: {incident.LabelCap}", MessageTypeDefOf.PositiveEvent);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[AffinityDrivenEvents] 触发正面事件失败: {ex.Message}");
            }
        }

        /// <summary>
        /// ? 获取所有可用事件（用于UI显示）
        /// </summary>
        public List<string> GetAllEvents()
        {
            return new List<string>
            {
                "NegativeEvent",
                "PositiveEvent"
            };
        }
        
        /// <summary>
        /// ? 触发指定事件（通用方法）
        /// </summary>
        public void TriggerEvent(Map map, string eventType, float severity = 0.5f)
        {
            if (eventType == "NegativeEvent")
            {
                TriggerNegativeEvent(map, severity);
            }
            else if (eventType == "PositiveEvent")
            {
                TriggerPositiveEvent(map);
            }
            else
            {
                Log.Warning($"[AffinityDrivenEvents] 未知事件类型: {eventType}");
            }
        }

        /// <summary>
        /// ? 清除所有冷却（调试用）
        /// </summary>
        public void ClearAllCooldowns()
        {
            eventCooldowns.Clear();
            Log.Warning("[AffinityDrivenEvents] 已清除所有事件冷却");
        }

        /// <summary>
        /// ? 获取剩余冷却时间（小时）
        /// </summary>
        public float GetRemainingCooldownHours(string eventType, int cooldownTicks)
        {
            if (!eventCooldowns.TryGetValue(eventType, out int lastTriggerTick))
            {
                return 0f;
            }

            int currentTick = Find.TickManager.TicksGame;
            int elapsedTicks = currentTick - lastTriggerTick;
            int remainingTicks = Math.Max(0, cooldownTicks - elapsedTicks);

            return remainingTicks / 2500f; // 转换为小时
        }
    }
}
