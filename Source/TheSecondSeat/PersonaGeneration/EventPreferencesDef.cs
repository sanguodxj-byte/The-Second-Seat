using System.Collections.Generic;
using Verse;

namespace TheSecondSeat.PersonaGeneration
{
    /// <summary>
    /// 事件偏好定义 - 控制叙事者的事件生成倾向
    /// API: 可在XML中配置所有字段
    /// </summary>
    public class EventPreferencesDef : IExposable
    {
        /// <summary>API: 正面事件偏好 (-1=避免, 0=中性, 1=偏好)</summary>
        public float positiveEventBias = 0f;
        
        /// <summary>API: 负面事件偏好 (-1=避免, 0=中性, 1=偏好)</summary>
        public float negativeEventBias = 0f;
        
        /// <summary>API: 混沌程度 (0=有序, 1=混沌)</summary>
        public float chaosLevel = 0.3f;
        
        /// <summary>API: 干预频率 (0=罕见, 1=频繁)</summary>
        public float interventionFrequency = 0.5f;
        
        /// <summary>API: 偏好的事件类型列表（如 Raid, Eclipse, ToxicFallout）</summary>
        public List<string> preferredEventTypes = new List<string>();
        
        /// <summary>API: 偏好的威胁等级 (0.5-2.0, 1.0为标准)</summary>
        public float preferredThreatScale = 1.0f;
        
        /// <summary>API: 最小事件间隔（天）</summary>
        public float minEventInterval = 1.0f;

        public EventPreferencesDef()
        {
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref positiveEventBias, "positiveEventBias", 0f);
            Scribe_Values.Look(ref negativeEventBias, "negativeEventBias", 0f);
            Scribe_Values.Look(ref chaosLevel, "chaosLevel", 0.3f);
            Scribe_Values.Look(ref interventionFrequency, "interventionFrequency", 0.5f);
            Scribe_Values.Look(ref preferredThreatScale, "preferredThreatScale", 1.0f);
            Scribe_Values.Look(ref minEventInterval, "minEventInterval", 1.0f);
            Scribe_Collections.Look(ref preferredEventTypes, "preferredEventTypes", LookMode.Value);
            
            // 确保集合不为 null
            if (preferredEventTypes == null) preferredEventTypes = new List<string>();
        }
    }
}
