using Verse;
using RimWorld;
using System.Collections.Generic;
using TheSecondSeat.Descent;

namespace TheSecondSeat.Testing
{
    [StaticConstructorOnStartup]
    public static class EventTester
    {
        static EventTester()
        {
            Log.Message("[EventTester] 事件测试工具已加载（含降临调试）");
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
            
            // ⭐ v1.6.81: 降临调试
            options.Add(new FloatMenuOption("---降临系统---", () => {}));
            options.Add(new FloatMenuOption("触发友好降临", () => TriggerDescent(false)));
            options.Add(new FloatMenuOption("触发敌对降临", () => TriggerDescent(true)));
            options.Add(new FloatMenuOption("强制叙事者回归", TriggerDescentReturn));
            options.Add(new FloatMenuOption("检查降临系统状态", CheckDescentSystem));
            
            // ⭐ v1.6.82: 降临动画类型测试
            options.Add(new FloatMenuOption("---动画类型测试---", () => {}));
            options.Add(new FloatMenuOption("📦 空投仓降临", () => TriggerDescentWithAnimation("DropPod")));
            options.Add(new FloatMenuOption("🦅 实体飞掠降临", () => TriggerDescentWithAnimation("DragonFlyby")));
            options.Add(new FloatMenuOption("🌀 传送门降临", () => TriggerDescentWithAnimation("Portal")));
            options.Add(new FloatMenuOption("⚡ 闪电降临", () => TriggerDescentWithAnimation("Lightning")));
            
            Find.WindowStack.Add(new FloatMenu(options));
        }
        
        #region ⭐ v1.6.81: 降临调试方法
        
        /// <summary>
        /// ⭐ 触发叙事者降临
        /// </summary>
        public static void TriggerDescent(bool isHostile)
        {
            var descentSystem = NarratorDescentSystem.Instance;
            
            if (descentSystem == null)
            {
                Messages.Message("降临系统未初始化（需要进入游戏）", MessageTypeDefOf.RejectInput);
                Log.Error("[EventTester] NarratorDescentSystem.Instance is null");
                return;
            }
            
            string modeText = isHostile ? "敌对" : "友好";
            Log.Message($"[EventTester] 尝试触发{modeText}降临...");
            
            bool success = descentSystem.TriggerDescent(isHostile);
            
            if (success)
            {
                Messages.Message($"已触发{modeText}降临！", MessageTypeDefOf.PositiveEvent);
            }
            else
            {
                Messages.Message($"降临失败（可能在冷却中或不支持降临）", MessageTypeDefOf.RejectInput);
            }
        }
        
        /// <summary>
        /// ⭐ 强制叙事者回归
        /// </summary>
        public static void TriggerDescentReturn()
        {
            var descentSystem = NarratorDescentSystem.Instance;
            
            if (descentSystem == null)
            {
                Messages.Message("降临系统未初始化", MessageTypeDefOf.RejectInput);
                return;
            }
            
            bool success = descentSystem.TriggerReturn(forceImmediate: true);
            
            if (success)
            {
                Messages.Message("叙事者已强制回归", MessageTypeDefOf.NeutralEvent);
            }
            else
            {
                Messages.Message("没有降临实体可回归", MessageTypeDefOf.RejectInput);
            }
        }
        
        /// <summary>
        /// ⭐ v1.6.82: 使用指定动画类型触发降临
        /// </summary>
        public static void TriggerDescentWithAnimation(string animationType)
        {
            var descentSystem = NarratorDescentSystem.Instance;
            
            if (descentSystem == null)
            {
                Messages.Message("降临系统未初始化（需要进入游戏）", MessageTypeDefOf.RejectInput);
                Log.Error("[EventTester] NarratorDescentSystem.Instance is null");
                return;
            }
            
            // 获取当前人格
            var manager = Current.Game?.GetComponent<Narrator.NarratorManager>();
            var persona = manager?.GetCurrentPersona();
            
            if (persona == null)
            {
                Messages.Message("无法获取当前叙事者人格", MessageTypeDefOf.RejectInput);
                return;
            }
            
            // 临时修改动画类型
            string originalType = persona.descentAnimationType;
            persona.descentAnimationType = animationType;
            
            Log.Message($"[EventTester] 测试降临动画: {animationType} (原类型: {originalType})");
            
            // 触发降临
            bool success = descentSystem.TriggerDescent(isHostile: false);
            
            // 恢复原始动画类型（延迟执行，确保降临已开始）
            LongEventHandler.ExecuteWhenFinished(() =>
            {
                persona.descentAnimationType = originalType;
            });
            
            if (success)
            {
                Messages.Message($"✅ 使用 {GetAnimationTypeName(animationType)} 触发降临", MessageTypeDefOf.PositiveEvent);
            }
            else
            {
                Messages.Message($"❌ 降临失败（可能在冷却中）", MessageTypeDefOf.RejectInput);
            }
        }
        
        /// <summary>
        /// 获取动画类型的中文名称
        /// </summary>
        private static string GetAnimationTypeName(string animationType)
        {
            return animationType switch
            {
                "DropPod" => "空投仓",
                "DragonFlyby" => "实体飞掠",
                "Portal" => "传送门",
                "Lightning" => "闪电",
                _ => animationType
            };
        }
        
        /// <summary>
        /// ⭐ 检查降临系统状态
        /// </summary>
        public static void CheckDescentSystem()
        {
            var descentSystem = NarratorDescentSystem.Instance;
            
            if (descentSystem == null)
            {
                Messages.Message("降临系统未初始化（需要进入游戏）", MessageTypeDefOf.RejectInput);
                return;
            }
            
            // 获取当前人格信息
            var manager = Current.Game?.GetComponent<Narrator.NarratorManager>();
            var persona = manager?.GetCurrentPersona();
            
            Log.Message("========== 降临系统状态 ==========");
            Log.Message($"当前人格: {persona?.narratorName ?? "无"}");
            Log.Message($"支持降临: {(!string.IsNullOrEmpty(persona?.descentPawnKind))}");
            Log.Message($"降临PawnKind: {persona?.descentPawnKind ?? "未配置"}");
            Log.Message($"伴随生物: {persona?.companionPawnKind ?? "无"}");
            Log.Message($"降临音效: {persona?.descentSound ?? "无"}");
            Log.Message($"冷却剩余: {descentSystem.GetCooldownRemaining()} 秒");
            Log.Message("=====================================");
            
            string status = !string.IsNullOrEmpty(persona?.descentPawnKind)
                ? $"✅ {persona.narratorName} 支持降临 (冷却: {descentSystem.GetCooldownRemaining()}秒)"
                : $"❌ {persona?.narratorName ?? "当前人格"} 不支持降临";
            
            Messages.Message(status, MessageTypeDefOf.NeutralEvent);
        }
        
        #endregion
    }
}
