using Verse;
using RimWorld;
using System.Collections.Generic;

namespace TheSecondSeat.Testing
{
    [StaticConstructorOnStartup]
    public static class EventTester
    {
        static EventTester()
        {
            Log.Message("[EventTester] 事件测试工具已加载");
        }

        public static void TriggerWelcomeGift()
        {
            if (Framework.NarratorEventManager.Instance != null)
            {
                Framework.NarratorEventManager.Instance.ForceTriggerEvent("TSS_Event_WelcomeGift");
                Messages.Message("已触发见面礼事件", MessageTypeDefOf.PositiveEvent);
            }
            else
            {
                Messages.Message("事件管理器未初始化", MessageTypeDefOf.RejectInput);
            }
        }

        public static void TriggerDivineWrath()
        {
            if (Framework.NarratorEventManager.Instance != null)
            {
                Framework.NarratorEventManager.Instance.ForceTriggerEvent("TSS_Event_DivineWrath");
                Messages.Message("已触发神罚事件", MessageTypeDefOf.NegativeEvent);
            }
            else
            {
                Messages.Message("事件管理器未初始化", MessageTypeDefOf.RejectInput);
            }
        }

        public static void TriggerMechRaid()
        {
            if (Framework.NarratorEventManager.Instance != null)
            {
                Framework.NarratorEventManager.Instance.ForceTriggerEvent("TSS_Event_MechRaid");
                Messages.Message("已触发敌袭警报", MessageTypeDefOf.NeutralEvent);
            }
            else
            {
                Messages.Message("事件管理器未初始化", MessageTypeDefOf.RejectInput);
            }
        }
        
        public static void ListAllEvents()
        {
            var events = DefDatabase<Framework.NarratorEventDef>.AllDefs;
            int count = 0;
            
            Log.Message("========== TSS 自定义事件列表 ==========");
            foreach (var evt in events)
            {
                string label = string.IsNullOrEmpty(evt.eventLabel) ? "无标签" : evt.eventLabel;
                Log.Message($"[{count + 1}] {evt.defName} - {label}");
                count++;
            }
            Log.Message($"========== 共 {count} 个事件 ==========");
            
            Messages.Message($"已加载 {count} 个事件（详见日志）", MessageTypeDefOf.NeutralEvent);
        }
        
        public static void CheckEventSystem()
        {
            if (Framework.NarratorEventManager.Instance == null)
            {
                Messages.Message("事件管理器未初始化", MessageTypeDefOf.RejectInput);
                return;
            }
            
            var events = DefDatabase<Framework.NarratorEventDef>.AllDefs;
            int eventCount = 0;
            int customEventCount = 0;
            
            foreach (var evt in events)
            {
                eventCount++;
                if (evt.defName.StartsWith("TSS_Event_"))
                {
                    customEventCount++;
                }
            }
            
            Messages.Message($"系统正常 已加载{eventCount}个事件（{customEventCount}个自定义）", MessageTypeDefOf.PositiveEvent);
        }
        
        public static void ShowTestMenu()
        {
            List<FloatMenuOption> options = new List<FloatMenuOption>();
            
            options.Add(new FloatMenuOption("触发见面礼（+500银）", TriggerWelcomeGift));
            options.Add(new FloatMenuOption("触发神罚（雷击）", TriggerDivineWrath));
            options.Add(new FloatMenuOption("触发敌袭警报", TriggerMechRaid));
            options.Add(new FloatMenuOption("列出所有事件", ListAllEvents));
            options.Add(new FloatMenuOption("检查事件系统", CheckEventSystem));
            
            Find.WindowStack.Add(new FloatMenu(options));
        }
    }
}
