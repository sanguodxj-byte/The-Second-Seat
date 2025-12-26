using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using RimWorld;

namespace TheSecondSeat.Descent
{
    /// <summary>
    /// Sideria 降临触发器
    /// 直接使用 NarratorDescentSystem 的 TriggerDescent 方法
    /// </summary>
    public static class SideriaDescentTrigger
    {
        /// <summary>
        /// 触发 Sideria 友好降临
        /// </summary>
        public static void TriggerFriendlyDescent()
        {
            var system = NarratorDescentSystem.Instance;
            if (system == null)
            {
                Log.Error("[SideriaDescentTrigger] NarratorDescentSystem 未初始化");
                return;
            }
            
            // 检查当前叙事者是否为 Sideria
            var manager = Current.Game?.GetComponent<Narrator.NarratorManager>();
            var persona = manager?.GetCurrentPersona();
            
            if (persona == null || !persona.defName.Contains("Sideria"))
            {
                Messages.Message("当前叙事者不是 Sideria，无法触发降临", MessageTypeDefOf.RejectInput);
                return;
            }
            
            // 触发友好降临
            bool success = system.TriggerDescent(isHostile: false);
            
            if (success)
            {
                Log.Message("[SideriaDescentTrigger] Sideria 友好降临已触发");
                
                // 特殊效果：治愈所有殖民者
                foreach (Pawn pawn in Find.CurrentMap.mapPawns.FreeColonists)
                {
                    if (pawn.health != null)
                    {
                        List<Hediff_Injury> injuries = new List<Hediff_Injury>();
                        pawn.health.hediffSet.GetHediffs(ref injuries);
                        
                        foreach (var injury in injuries)
                        {
                            injury.Heal(injury.Severity * 0.5f); // 治愈 50% 伤害
                        }
                    }
                }
                
                Messages.Message("Sideria 的降临带来了治愈之光！", MessageTypeDefOf.PositiveEvent);
            }
        }
        
        /// <summary>
        /// 触发 Sideria 敌对降临（测试用）
        /// </summary>
        public static void TriggerHostileDescent()
        {
            var system = NarratorDescentSystem.Instance;
            if (system == null)
            {
                Log.Error("[SideriaDescentTrigger] NarratorDescentSystem 未初始化");
                return;
            }
            
            // 检查当前叙事者是否为 Sideria
            var manager = Current.Game?.GetComponent<Narrator.NarratorManager>();
            var persona = manager?.GetCurrentPersona();
            
            if (persona == null || !persona.defName.Contains("Sideria"))
            {
                Messages.Message("当前叙事者不是 Sideria，无法触发降临", MessageTypeDefOf.RejectInput);
                return;
            }
            
            // 触发敌对降临
            bool success = system.TriggerDescent(isHostile: true);
            
            if (success)
            {
                Log.Message("[SideriaDescentTrigger] Sideria 敌对降临已触发");
                
                // 特殊效果：对敌人造成额外伤害
                foreach (Pawn pawn in Find.CurrentMap.mapPawns.AllPawns)
                {
                    if (pawn.HostileTo(Faction.OfPlayer))
                    {
                        pawn.TakeDamage(new DamageInfo(DamageDefOf.Burn, 100f));
                    }
                }
                
                Messages.Message("Sideria 的愤怒降临了！", MessageTypeDefOf.ThreatBig);
            }
        }
    }
}
