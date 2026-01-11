using System;
using System.Collections.Generic;
using Verse;

namespace TheSecondSeat.Core
{
    /// <summary>
    /// ? v1.6.66: 情绪追踪系统
    /// 
    /// 功能：
    /// - 记录 AI 每次对话的情绪标签
    /// - 影响后续对话的语气和表情
    /// - 与好感度系统协同工作
    /// 
    /// 使用：
    /// 1. ProcessResponse 中调用 RecordEmotion(emotion)
    /// 2. SystemPromptGenerator 中使用 GetEmotionTrend() 调整语气
    /// </summary>
    public class EmotionTracker : GameComponent
    {
        // 单例模式
        private static EmotionTracker? instance;
        public static EmotionTracker Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = Current.Game?.GetComponent<EmotionTracker>();
                }
                return instance;
            }
        }
        
        // ===== 数据结构 =====
        
        /// <summary>
        /// 情绪记录
        /// </summary>
        public class EmotionRecord
        {
            public PersonaGeneration.EmotionType Emotion;
            public int Tick;                  // 记录时间
            public string Context;            // 上下文（玩家消息或事件）
            
            public EmotionRecord(PersonaGeneration.EmotionType emotion, string context = "")
            {
                Emotion = emotion;
                Tick = Find.TickManager.TicksGame;
                Context = context;
            }
        }
        
        // ===== 数据存储 =====
        
        /// <summary>最近的情绪记录（最多保留 20 条）</summary>
        private List<EmotionRecord> recentEmotions = new List<EmotionRecord>();
        
        /// <summary>情绪统计（每种情绪的出现次数）</summary>
        private Dictionary<PersonaGeneration.EmotionType, int> emotionCounts = new Dictionary<PersonaGeneration.EmotionType, int>();
        
        /// <summary>当前主导情绪</summary>
        private PersonaGeneration.EmotionType currentDominantEmotion = PersonaGeneration.EmotionType.Neutral;
        
        // ===== 配置参数 =====
        
        /// <summary>最多保留的情绪记录数量</summary>
        private const int MAX_EMOTION_RECORDS = 20;
        
        /// <summary>情绪趋势窗口（最近 N 条记录）</summary>
        private const int TREND_WINDOW = 5;
        
        // ===== 构造函数 =====
        
        public EmotionTracker(Game game) : base()
        {
            instance = this;
        }
        
        // ===== 公共 API =====
        
        /// <summary>
        /// 记录一次情绪
        /// </summary>
        /// <param name="emotionStr">情绪字符串（如 "happy", "sad"）</param>
        /// <param name="context">上下文（可选）</param>
        public void RecordEmotion(string emotionStr, string context = "")
        {
            if (string.IsNullOrEmpty(emotionStr))
            {
                emotionStr = "neutral";
            }
            
            // 解析情绪类型
            if (!Enum.TryParse<PersonaGeneration.EmotionType>(emotionStr, true, out var emotion))
            {
                Log.Warning($"[EmotionTracker] 无法解析情绪: {emotionStr}，使用 Neutral");
                emotion = PersonaGeneration.EmotionType.Neutral;
            }
            
            // 创建记录
            var record = new EmotionRecord(emotion, context);
            recentEmotions.Add(record);
            
            // 限制记录数量
            if (recentEmotions.Count > MAX_EMOTION_RECORDS)
            {
                recentEmotions.RemoveAt(0);
            }
            
            // 更新统计
            if (!emotionCounts.ContainsKey(emotion))
            {
                emotionCounts[emotion] = 0;
            }
            emotionCounts[emotion]++;
            
            // 更新主导情绪
            UpdateDominantEmotion();
            
            Log.Message($"[EmotionTracker] 记录情绪: {emotion} (上下文: {context})");
        }
        
        /// <summary>
        /// 获取情绪趋势（最近 N 条记录的主导情绪）
        /// </summary>
        /// <returns>情绪趋势描述</returns>
        public string GetEmotionTrend()
        {
            if (recentEmotions.Count == 0)
            {
                return "中性";
            }
            
            // 获取最近 N 条记录
            int startIndex = Math.Max(0, recentEmotions.Count - TREND_WINDOW);
            var recentRecords = recentEmotions.GetRange(startIndex, recentEmotions.Count - startIndex);
            
            // 统计最近情绪
            var trendCounts = new Dictionary<PersonaGeneration.EmotionType, int>();
            foreach (var record in recentRecords)
            {
                if (!trendCounts.ContainsKey(record.Emotion))
                {
                    trendCounts[record.Emotion] = 0;
                }
                trendCounts[record.Emotion]++;
            }
            
            // 找出最多的情绪
            PersonaGeneration.EmotionType trendEmotion = PersonaGeneration.EmotionType.Neutral;
            int maxCount = 0;
            foreach (var kvp in trendCounts)
            {
                if (kvp.Value > maxCount)
                {
                    maxCount = kvp.Value;
                    trendEmotion = kvp.Key;
                }
            }
            
            // 返回趋势描述
            return EmotionToChineseName(trendEmotion);
        }
        
        /// <summary>
        /// 获取当前主导情绪
        /// </summary>
        public PersonaGeneration.EmotionType GetDominantEmotion()
        {
            return currentDominantEmotion;
        }
        
        /// <summary>
        /// 获取最近一次情绪
        /// </summary>
        public PersonaGeneration.EmotionType GetLastEmotion()
        {
            if (recentEmotions.Count == 0)
            {
                return PersonaGeneration.EmotionType.Neutral;
            }
            
            return recentEmotions[recentEmotions.Count - 1].Emotion;
        }
        
        /// <summary>
        /// 清除所有情绪记录
        /// </summary>
        public void Clear()
        {
            recentEmotions.Clear();
            emotionCounts.Clear();
            currentDominantEmotion = PersonaGeneration.EmotionType.Neutral;
            Log.Message("[EmotionTracker] 情绪记录已清空");
        }
        
        // ===== 私有方法 =====
        
        /// <summary>
        /// 更新主导情绪（全局统计）
        /// </summary>
        private void UpdateDominantEmotion()
        {
            PersonaGeneration.EmotionType maxEmotion = PersonaGeneration.EmotionType.Neutral;
            int maxCount = 0;
            
            foreach (var kvp in emotionCounts)
            {
                if (kvp.Value > maxCount)
                {
                    maxCount = kvp.Value;
                    maxEmotion = kvp.Key;
                }
            }
            
            currentDominantEmotion = maxEmotion;
        }
        
        /// <summary>
        /// 情绪类型转中文名称
        /// </summary>
        private string EmotionToChineseName(PersonaGeneration.EmotionType emotion)
        {
            return emotion switch
            {
                PersonaGeneration.EmotionType.Happy => "开心",
                PersonaGeneration.EmotionType.Sad => "悲伤",
                PersonaGeneration.EmotionType.Angry => "愤怒",
                PersonaGeneration.EmotionType.Surprised => "惊讶",
                PersonaGeneration.EmotionType.Confused => "困惑",
                PersonaGeneration.EmotionType.Shy => "害羞",
                PersonaGeneration.EmotionType.Smug => "得意",
                _ => "中性"
            };
        }
        
        // ===== 存档支持 =====
        
        public override void ExposeData()
        {
            base.ExposeData();
            
            // 保存/加载情绪记录（简化：只保存最近 10 条）
            // TODO: 实现 IExposable 序列化
            
            Scribe_Values.Look(ref currentDominantEmotion, "currentDominantEmotion", PersonaGeneration.EmotionType.Neutral);
        }
    }
}
