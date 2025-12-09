using System.Collections.Generic;
using Verse;
using RimWorld;

namespace TheSecondSeat.Events
{
    /// <summary>
    /// 事件类别枚举
    /// </summary>
    public enum EventCategory
    {
        Positive,      // 正面事件
        Negative,      // 负面事件
        Neutral,       // 中性事件
        Challenge,     // 挑战事件
        Reward,        // 奖励事件
        Story          // 剧情事件
    }

    /// <summary>
    /// 叙事者事件定义
    /// 用于定义可触发的自定义游戏事件
    /// </summary>
    public class StorytellerEventDef : Def
    {
        public string eventName = "Unknown Event";
        public new string description = "";  // ? 添加new关键字隐藏基类成员
        public EventCategory category = EventCategory.Neutral;
        public float baseWeight = 1.0f;
        public int minDaysSinceStart = 0;
        public List<string> requiredMods = new List<string>();
        public IncidentDef incidentDef = null;  // ? 添加缺失的字段
        
        public StorytellerEventDef()
        {
        }
    }
}
