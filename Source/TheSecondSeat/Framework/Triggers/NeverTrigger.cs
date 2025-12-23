using System.Collections.Generic;
using Verse;

namespace TheSecondSeat.Framework.Triggers
{
    /// <summary>
    /// ⭐ 永不触发触发器 - 用于测试事件
    /// 
    /// 此触发器的IsSatisfied()永远返回false，
    /// 确保事件只能通过ForceTriggerEvent()手动触发
    /// 
    /// XML使用示例：
    /// <code>
    /// <triggers>
    ///   <li Class="TheSecondSeat.Framework.Triggers.NeverTrigger" />
    /// </triggers>
    /// </code>
    /// </summary>
    public class NeverTrigger : TSSTrigger
    {
        public override bool IsSatisfied(Map map, Dictionary<string, object> context)
        {
            return false;  // 永远返回false，阻止自动触发
        }
        
        public override bool Validate(out string error)
        {
            error = "";
            return true;
        }
        
        public override string GetDescription()
        {
            return "Never Auto-Trigger (Manual Only)";
        }
    }
}
