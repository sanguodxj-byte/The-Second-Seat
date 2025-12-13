using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using RimWorld;

namespace TheSecondSeat.PersonaGeneration
{
    /// <summary>
    /// 表情类型枚举
    /// ? 每个表情支持1-5个变体（通过运行时随机选择）
    /// </summary>
    public enum ExpressionType
    {
        Neutral,      // 中立
        Happy,        // 开心（支持 happy1-happy5）
        Sad,          // 悲伤（支持 sad1-sad5）
        Angry,        // 愤怒（支持 angry1-angry5）
        Surprised,    // 惊讶（支持 surprised1-surprised5）
        Worried,      // 担忧（支持 worried1-worried5）
        Smug,         // 得意（支持 smug1-smug5）
        Disappointed, // 失望（支持 disappointed1-disappointed5）
        Thoughtful,   // 沉思（支持 thoughtful1-thoughtful5）
        Annoyed,      // 恼怒（支持 annoyed1-annoyed5）
        Playful,      // 调皮（支持 playful1-playful5）
        Shy,          // 害羞（支持 shy1-shy5）
        Confused      // 疑惑（支持 confused1-confused5）- 触摸模式专用
    }

    /// <summary>
    /// 表情变化触发器
    /// </summary>
    public enum ExpressionTrigger
    {
        Manual,           // 手动指定
        Affinity,         // 好感度变化
        DialogueTone,     // 对话语气
        GameEvent,        // 游戏事件
        RandomVariation,  // 随机变化
        Processing        // ? 新增：AI处理中
    }

    /// <summary>
    /// 表情状态数据
    /// </summary>
    public class ExpressionState
    {
        public ExpressionType CurrentExpression { get; set; } = ExpressionType.Neutral;
        public ExpressionType PreviousExpression { get; set; } = ExpressionType.Neutral;
        public float TransitionProgress { get; set; } = 1f; // 1=完成，0=开始
        public int TransitionTicks { get; set; } = 0;
        public ExpressionTrigger LastTrigger { get; set; } = ExpressionTrigger.Manual;
        
        // 表情持续时间（秒）
        public float ExpressionDuration { get; set; } = 3f;
        public int ExpressionStartTick { get; set; } = 0;
        
        // 是否锁定表情（某些重要场景）
        public bool IsLocked { get; set; } = false;
        
        // ? 新增：当前选择的变体编号（0=基础版本，1-5=变体）
        public int CurrentVariant { get; set; } = 0;
    }

    /// <summary>
    /// 立绘表情系统
    /// ? 根据好感度、对话内容、游戏事件动态切换表情
    /// </summary>
    public static class ExpressionSystem
    {
        private static Dictionary<string, ExpressionState> expressionStates = new Dictionary<string, ExpressionState>();
        private static Dictionary<string, BreathingState> breathingStates = new Dictionary<string, BreathingState>(); // ? 新增：呼吸动画状态
        
        // 表情过渡持续时间（游戏tick）
        private const int TRANSITION_DURATION_TICKS = 30; // 约0.5秒
        
        // ? 表情持续时间（游戏tick）- 30秒
        private const int EXPRESSION_DURATION_TICKS = 1800;
        
        // ? 新增：呼吸动画状态类
        private class BreathingState
        {
            public float phase;        // 当前相位（弧度）
            public float speed;        // 呼吸速度
            public float amplitude;    // 呼吸振幅（像素）
            public long lastUpdateTime; // 上次更新时间（毫秒）
        }
        
        /// <summary>
        /// 获取人格的当前表情状态
        /// </summary>
        public static ExpressionState GetExpressionState(string personaDefName)
        {
            if (!expressionStates.ContainsKey(personaDefName))
            {
                expressionStates[personaDefName] = new ExpressionState();
            }
            return expressionStates[personaDefName];
        }
        
        /// <summary>
        /// 设置表情（带平滑过渡）
        /// ? 自动为所有表情类型随机选择变体（1-5）
        /// ? v1.6.20: 清除分层立绘缓存，强制重新合成
        /// ? v1.6.30: 应用感情驱动动画
        /// </summary>
        public static void SetExpression(string personaDefName, ExpressionType expression, int durationTicks = EXPRESSION_DURATION_TICKS, string reason = "")
        {
            var state = GetExpressionState(personaDefName);
            
            // 如果表情被锁定，跳过切换
            if (state.IsLocked)
            {
                if (Prefs.DevMode)
                {
                    Log.Message($"[ExpressionSystem] 表情被锁定，跳过切换: {personaDefName}");
                }
                return;
            }
            
            // 如果表情相同，跳过
            if (state.CurrentExpression == expression)
            {
                return;
            }
            
            // ? 清除旧表情的缓存（立即释放）
            PortraitLoader.ClearPortraitCache(personaDefName, state.CurrentExpression);
            AvatarLoader.ClearAvatarCache(personaDefName, state.CurrentExpression);
            
            // ? v1.6.20: 清除分层立绘缓存（强制重新合成新表情）
            LayeredPortraitCompositor.ClearCache(personaDefName, state.CurrentExpression);
            LayeredPortraitCompositor.ClearCache(personaDefName, expression);
            
            // ? 清除新表情的缓存，确保重新加载
            PortraitLoader.ClearPortraitCache(personaDefName, expression);
            AvatarLoader.ClearAvatarCache(personaDefName, expression);
            
            // ? 随机选择变体编号（1-5）
            // Neutral 表情不使用变体（variant = 0）
            if (expression == ExpressionType.Neutral)
            {
                state.CurrentVariant = 0;
            }
            else
            {
                state.CurrentVariant = UnityEngine.Random.Range(1, 6); // 1-5
            }
            
            // 开始过渡
            state.PreviousExpression = state.CurrentExpression;
            state.CurrentExpression = expression;
            state.TransitionProgress = 0f;
            state.TransitionTicks = 0;
            state.ExpressionStartTick = Find.TickManager.TicksGame;
            
            // ? v1.6.30: 应用感情驱动动画（自动调整眨眼、呼吸等）
            ApplyEmotionDrivenAnimation(personaDefName);
            
            if (Prefs.DevMode)
            {
                string reasonText = string.IsNullOrEmpty(reason) ? "未指定" : reason;
                Log.Message($"[ExpressionSystem] ? {personaDefName} 表情切换: {state.PreviousExpression} → {expression} (变体: {state.CurrentVariant}, 原因: {reasonText})");
            }
        }
        
        /// <summary>
        /// ? SetExpression重载 - 接受ExpressionTrigger参数
        /// </summary>
        public static void SetExpression(string personaDefName, ExpressionType expression, ExpressionTrigger trigger, int durationTicks = EXPRESSION_DURATION_TICKS)
        {
            var state = GetExpressionState(personaDefName);
            state.LastTrigger = trigger;  // 设置触发器类型
            
            SetExpression(personaDefName, expression, durationTicks, trigger.ToString());
        }
        
        /// <summary>
        /// ? 设置为思考表情（AI处理中）
        /// </summary>
        public static void SetThinkingExpression(string personaDefName)
        {
            SetExpression(personaDefName, ExpressionType.Thoughtful, ExpressionTrigger.Processing);
        }
        
        /// <summary>
        /// ? 根据好感度自动设置表情（修改：30以上为Happy）
        /// </summary>
        public static void UpdateExpressionByAffinity(string personaDefName, float affinity)
        {
            ExpressionType expression;
            
            // ? 修改：30以上好感度默认为Happy
            if (affinity >= 80f)
            {
                expression = ExpressionType.Happy; // 未来可以用 Happy2 或 Happy3
            }
            else if (affinity >= 60f)
            {
                expression = ExpressionType.Happy;
            }
            else if (affinity >= 30f)
            {
                expression = ExpressionType.Happy; // ? 30以上也是Happy
            }
            else if (affinity >= 0f)
            {
                expression = ExpressionType.Neutral;
            }
            else if (affinity >= -30f)
            {
                expression = ExpressionType.Annoyed;
            }
            else if (affinity >= -60f)
            {
                expression = ExpressionType.Disappointed;
            }
            else
            {
                expression = ExpressionType.Angry;
            }
            
            SetExpression(personaDefName, expression, ExpressionTrigger.Affinity);
        }
        
        /// <summary>
        /// ? 根据对话语气设置表情（扩展关键词）
        /// </summary>
        public static void UpdateExpressionByDialogueTone(string personaDefName, string dialogueText)
        {
            if (string.IsNullOrEmpty(dialogueText))
            {
                return;
            }
            
            // 分析对话文本中的情感关键词
            ExpressionType expression = ExpressionType.Neutral;
            
            // ? 开心关键词（大幅扩展）
            if (ContainsKeywords(dialogueText, new[] { 
                // 中文
                "哈哈", "嘻嘻", "哈", "真好", "太棒", "开心", "高兴", "喜欢", "不错", "很好", 
                "好的", "可以", "当然", "没问题", "乐意", "愉快", "快乐", "欢迎", "恭喜", 
                "祝贺", "赞", "厉害", "优秀", "完美", "太好了", "好极了", "妙", "绝", 
                "棒", "美", "酷", "帅", "爱", "喜", "乐", "笑", "嗯嗯", "好哒", "么么",
                "嘿嘿", "呵呵", "hiahia", "2333", "233", "666", "nice",
                // 英文
                "fantastic", "great", "wonderful", "happy", "haha", "good", "nice", 
                "excellent", "amazing", "awesome", "perfect", "love", "like", "enjoy",
                "glad", "pleased", "delighted", "cheerful", "joyful", "yay", "yeah",
                "cool", "brilliant", "superb", "terrific", "marvelous", "splendid"
            }))
            {
                expression = ExpressionType.Happy;
            }
            // ? 愤怒关键词（扩展）
            else if (ContainsKeywords(dialogueText, new[] { 
                // 中文
                "可恶", "该死", "混蛋", "愤怒", "生气", "讨厌", "烦", "滚", "闭嘴", 
                "废物", "蠢", "笨", "傻", "白痴", "可恨", "恼火", "火大", "气死",
                "不行", "绝对不", "休想", "别想", "不可能", "拒绝",
                // 英文
                "damn", "angry", "furious", "irritated", "hate", "stupid", "idiot",
                "shut up", "annoying", "rage", "mad", "pissed", "disgusting"
            }))
            {
                expression = ExpressionType.Angry;
            }
            // ? 悲伤关键词（扩展）
            else if (ContainsKeywords(dialogueText, new[] { 
                // 中文
                "悲伤", "难过", "可怜", "遗憾", "伤心", "哭", "泪", "悲", "惨", 
                "不幸", "可惜", "唉", "呜", "哎", "伤感", "凄凉", "心痛", "痛苦",
                "抱歉", "对不起", "失去", "离开", "死", "逝",
                // 英文
                "sad", "unfortunate", "regret", "pity", "cry", "tears", "sorrow",
                "sorry", "miss", "loss", "grief", "mourn", "tragic", "painful"
            }))
            {
                expression = ExpressionType.Sad;
            }
            // ? 惊讶关键词（扩展）
            else if (ContainsKeywords(dialogueText, new[] { 
                // 中文
                "什么", "不会吧", "真的", "天啊", "哇", "啊", "呀", "哦", "咦", 
                "居然", "竟然", "怎么", "为什么", "惊", "震惊", "意外", "没想到",
                "不敢相信", "吓", "我的天", "天哪", "我去", "卧槽", "woc",
                // 英文  
                "what", "really", "wow", "omg", "surprising", "shocked", "amazing",
                "unbelievable", "incredible", "unexpected", "seriously", "no way"
            }))
            {
                expression = ExpressionType.Surprised;
            }
            // ? 担忧关键词（扩展）
            else if (ContainsKeywords(dialogueText, new[] { 
                // 中文
                "担心", "忧虑", "危险", "小心", "注意", "警惕", "害怕", "恐惧", 
                "紧张", "焦虑", "不安", "可能会", "也许", "风险", "威胁", "问题",
                "糟糕", "不妙", "麻烦", "困难", "棘手",
                // 英文
                "worried", "concerned", "careful", "beware", "afraid", "fear",
                "nervous", "anxious", "danger", "risk", "threat", "trouble", "problem"
            }))
            {
                expression = ExpressionType.Worried;
            }
            // ? 调皮关键词（扩展）
            else if (ContainsKeywords(dialogueText, new[] { 
                // 中文
                "嘿嘿", "嘻嘻", "逗你", "开玩笑", "捉弄", "调皮", "坏笑", "偷笑",
                "嘿", "哼哼", "呵呵", "略略略", "嘻", "皮", "骗你的", "逗你玩",
                // 英文
                "playful", "teasing", "mischievous", "hehe", "hihi", "kidding",
                "joking", "prank", "naughty", "trick"
            }))
            {
                expression = ExpressionType.Playful;
            }
            // ? 得意关键词（扩展）
            else if (ContainsKeywords(dialogueText, new[] { 
                // 中文
                "看吧", "我说", "果然", "不出所料", "就知道", "当然", "必然", 
                "轻松", "简单", "小意思", "不在话下", "一般般", "还行吧", "哼",
                "本大人", "本小姐", "本尊", "朕",
                // 英文
                "told you", "as expected", "obviously", "of course", "naturally",
                "easy", "simple", "knew it", "predicted"
            }))
            {
                expression = ExpressionType.Smug;
            }
            // ? 失望关键词（扩展）
            else if (ContainsKeywords(dialogueText, new[] { 
                // 中文
                "失望", "算了", "唉", "无奈", "放弃", "没办法", "没用", "无语",
                "无力", "疲惫", "累", "烦", "懒得", "不想", "随便", "whatever",
                // 英文
                "disappointed", "sigh", "alas", "whatever", "give up", "useless",
                "hopeless", "tired", "exhausted", "boring"
            }))
            {
                expression = ExpressionType.Disappointed;
            }
            // ? 沉思关键词（扩展）
            else if (ContainsKeywords(dialogueText, new[] { 
                // 中文
                "嗯", "让我想想", "或许", "也许", "可能", "思考", "考虑", "分析",
                "研究", "琢磨", "推测", "判断", "认为", "觉得", "我想", "应该",
                "大概", "估计", "看来", "似乎", "好像", "...", "……",
                // 英文
                "hmm", "perhaps", "maybe", "consider", "think", "analyze", "ponder",
                "suppose", "guess", "probably", "seems", "apparently", "let me see"
            }))
            {
                expression = ExpressionType.Thoughtful;
            }
            // ? 烦躁关键词（扩展）
            else if (ContainsKeywords(dialogueText, new[] { 
                // 中文
                "烦", "吵", "够了", "行了", "知道了", "好了好了", "别说了", 
                "闭嘴", "安静", "不要", "停", "等等", "慢着", "打扰",
                // 英文
                "annoyed", "bothered", "enough", "stop", "quiet", "shut", "leave me"
            }))
            {
                expression = ExpressionType.Annoyed;
            }
            // ? 新增：害羞关键词
            else if (ContainsKeywords(dialogueText, new[] { 
                // 中文
                "害羞", "不好意思", "羞", "脸红", "羞涩", "难为情", "不敢", 
                "...", "……", "那个", "嗯嗯", "唔", "诶", "啊这", "emmm",
                "尴尬", "不太好", "有点", "感谢", "谢谢你", "太客气了",
                "不用", "没什么", "别这样", "不要这样", "你真是",
                // 英文
                "shy", "embarrassed", "blush", "awkward", "umm", "uh", "er",
                "thank you", "thanks", "appreciate", "grateful", "sorry"
            }))
            {
                expression = ExpressionType.Shy;
            }
            // ? 疑惑关键词（触摸模式专用）
            else if (ContainsKeywords(dialogueText, new[] {
                // 中文
                "啊", "什么", "怎么", "为何", "这是", "那是", "你是", "我是", "他是",
                "她是", "它是", "谁是", "在哪里", "什么时候", "为什么", "怎么样",
                "有什么", "没什么", "只不过", "难道", "岂不是", "莫非",
                // 英文
                "ah", "what", "how", "why", "this is", "that is", "you are", "i am",
                "he is", "she is", "it is", "who is", "where is", "when", "why",
                "what is", "nothing", "just", "did", "could", "couldn't",
                "would", "wouldn't", "might", "might not", "must", "mustn't",
                "can't", "cannot", "do", "don't", "does", "doesn't"
            }))
            {
                expression = ExpressionType.Confused;
            }
            
            if (expression != ExpressionType.Neutral)
            {
                SetExpression(personaDefName, expression, ExpressionTrigger.DialogueTone);
            }
        }
        
        /// <summary>
        /// 根据游戏事件设置表情
        /// </summary>
        public static void UpdateExpressionByEvent(string personaDefName, string eventType, bool isPositive)
        {
            ExpressionType expression;
            
            switch (eventType.ToLower())
            {
                case "colonist_death":
                    expression = ExpressionType.Sad;
                    break;
                    
                case "raid_victory":
                    expression = ExpressionType.Happy;
                    break;
                    
                case "raid_incoming":
                    expression = ExpressionType.Worried;
                    break;
                    
                case "major_loss":
                    expression = ExpressionType.Disappointed;
                    break;
                    
                case "great_success":
                    expression = ExpressionType.Smug;
                    break;
                    
                case "unexpected_event":
                    expression = ExpressionType.Surprised;
                    break;
                    
                default:
                    expression = isPositive ? ExpressionType.Happy : ExpressionType.Sad;
                    break;
            }
            
            SetExpression(personaDefName, expression, ExpressionTrigger.GameEvent);
        }
        
        /// <summary>
        /// 更新表情过渡动画
        /// </summary>
        public static void UpdateTransition(string personaDefName)
        {
            var state = GetExpressionState(personaDefName);
            
            // 如果过渡未完成
            if (state.TransitionProgress < 1f)
            {
                state.TransitionTicks++;
                state.TransitionProgress = Mathf.Clamp01((float)state.TransitionTicks / TRANSITION_DURATION_TICKS);
            }
            
            // 检查表情是否过期（自动恢复到中性）
            // ? Processing 触发器不会自动过期（等待AI响应完成）
            if (!state.IsLocked && state.TransitionProgress >= 1f && state.LastTrigger != ExpressionTrigger.Processing)
            {
                int elapsedTicks = Find.TickManager.TicksGame - state.ExpressionStartTick;
                
                if (elapsedTicks > EXPRESSION_DURATION_TICKS && state.CurrentExpression != ExpressionType.Neutral)
                {
                    // 表情过期，恢复到中性
                    SetExpression(personaDefName, ExpressionType.Neutral, ExpressionTrigger.RandomVariation);
                }
            }
        }
        
        /// <summary>
        /// 锁定/解锁表情
        /// </summary>
        public static void LockExpression(string personaDefName, bool locked)
        {
            var state = GetExpressionState(personaDefName);
            state.IsLocked = locked;
            Log.Message($"[ExpressionSystem] {personaDefName} 表情锁定状态: {locked}");
        }
        
        /// <summary>
        /// 获取表情对应的文件名后缀
        /// ? 支持所有表情类型的变体（1-5）
        /// ? 根据缓存的变体编号返回一致的后缀
        /// </summary>
        public static string GetExpressionSuffix(string personaDefName, ExpressionType expression)
        {
            // 基础后缀映射
            string baseSuffix = expression switch
            {
                ExpressionType.Neutral => "",
                ExpressionType.Happy => "_happy",
                ExpressionType.Sad => "_sad",
                ExpressionType.Angry => "_angry",
                ExpressionType.Surprised => "_surprised",
                ExpressionType.Worried => "_worried",
                ExpressionType.Smug => "_smug",
                ExpressionType.Disappointed => "_disappointed",
                ExpressionType.Thoughtful => "_thoughtful",
                ExpressionType.Annoyed => "_annoyed",
                ExpressionType.Playful => "_playful",
                ExpressionType.Shy => "_shy",
                ExpressionType.Confused => "_confused",
                _ => ""
            };

            // ? 使用缓存的变体编号
            var state = GetExpressionState(personaDefName);
            int variant = state.CurrentVariant;
            
            // 如果是变体 0（基础版本），直接返回基础后缀
            if (variant == 0 || string.IsNullOrEmpty(baseSuffix))
            {
                return baseSuffix;
            }
            
            // 返回带变体编号的后缀（如 _happy1, _happy2, _sad3...）
            return $"{baseSuffix}{variant}";
        }

        /// <summary>
        /// 重置所有表情状态
        /// </summary>
        public static void ResetAllExpressions()
        {
            expressionStates.Clear();
            Log.Message("[ExpressionSystem] 所有表情状态已重置");
        }
        
        // 辅助方法：检查文本是否包含关键词
        private static bool ContainsKeywords(string text, string[] keywords)
        {
            text = text.ToLowerInvariant();
            foreach (var keyword in keywords)
            {
                if (text.Contains(keyword.ToLowerInvariant()))
                {
                    return true;
                }
            }
            return false;
        }
        
        /// <summary>
        /// 获取调试信息
        /// </summary>
        public static string GetDebugInfo()
        {
            var info = $"[ExpressionSystem] 表情状态数量: {expressionStates.Count}\n";
            
            foreach (var kvp in expressionStates)
            {
                var state = kvp.Value;
                info += $"  {kvp.Key}:\n";
                info += $"    当前表情: {state.CurrentExpression}\n";
                info += $"    过渡进度: {state.TransitionProgress:P0}\n";
                info += $"    锁定状态: {state.IsLocked}\n";
                info += $"    触发器: {state.LastTrigger}\n";
            }
            
            return info;
        }
        
        /// <summary>
        /// ? 测试方法：手动触发表情切换（用于调试）
        /// </summary>
        public static void TestExpressionChange(string personaDefName)
        {
            var allExpressions = new[]
            {
                ExpressionType.Neutral,
                ExpressionType.Happy,
                ExpressionType.Sad,
                ExpressionType.Angry,
                ExpressionType.Surprised,
                ExpressionType.Worried,
                ExpressionType.Smug,
                ExpressionType.Disappointed,
                ExpressionType.Thoughtful,
                ExpressionType.Annoyed,
                ExpressionType.Playful,
                ExpressionType.Shy,          // 新增
                ExpressionType.Confused      // 新增疑惑
            };
            
            var state = GetExpressionState(personaDefName);
            int currentIndex = Array.IndexOf(allExpressions, state.CurrentExpression);
            int nextIndex = (currentIndex + 1) % allExpressions.Length;
            
            ExpressionType nextExpression = allExpressions[nextIndex];
            SetExpression(personaDefName, nextExpression, ExpressionTrigger.Manual);
            
            Messages.Message($"[测试] 表情切换: {state.CurrentExpression} → {nextExpression}", MessageTypeDefOf.NeutralEvent);
        }
        
        /// <summary>
        /// ? 获取呼吸动画偏移（完整版本）
        /// 基于正弦波实现平滑的呼吸动画效果
        /// </summary>
        public static float GetBreathingOffset(string personaDefName)
        {
            // 初始化呼吸状态
            if (!breathingStates.ContainsKey(personaDefName))
            {
                breathingStates[personaDefName] = new BreathingState
                {
                    phase = UnityEngine.Random.Range(0f, Mathf.PI * 2f),     // 随机初始相位
                    speed = UnityEngine.Random.Range(0.4f, 0.6f),            // 呼吸速度：0.4-0.6 秒/周期
                    amplitude = UnityEngine.Random.Range(1.5f, 2.5f),        // 振幅：1.5-2.5 像素
                    lastUpdateTime = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond
                };
            }
            
            var state = breathingStates[personaDefName];
            
            // 计算时间增量（秒）
            long currentTime = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
            float deltaTime = (currentTime - state.lastUpdateTime) / 1000f;
            state.lastUpdateTime = currentTime;
            
            // 更新相位
            state.phase += deltaTime * state.speed;
            
            // 相位归一化（保持在0-2π范围内）
            if (state.phase > Mathf.PI * 2f)
            {
                state.phase -= Mathf.PI * 2f;
            }
            
            // 计算呼吸偏移（正弦波）
            float offset = Mathf.Sin(state.phase) * state.amplitude;
            
            return offset;
        }
        
        /// <summary>
        /// ? 根据情绪调整呼吸速度
        /// 不同情绪有不同的呼吸模式
        /// </summary>
        public static void AdjustBreathingByEmotion(string personaDefName, ExpressionType emotion)
        {
            if (!breathingStates.ContainsKey(personaDefName))
            {
                // 如果不存在，先调用一次 GetBreathingOffset 来初始化
                GetBreathingOffset(personaDefName);
            }
            
            var state = breathingStates[personaDefName];
            
            // 根据情绪设置不同的呼吸参数
            switch (emotion)
            {
                case ExpressionType.Happy:
                case ExpressionType.Playful:
                    // 开心时呼吸轻快
                    state.speed = UnityEngine.Random.Range(0.6f, 0.8f);
                    state.amplitude = UnityEngine.Random.Range(2.0f, 3.0f);
                    break;
                    
                case ExpressionType.Worried:
                    // 担心时呼吸急促
                    state.speed = UnityEngine.Random.Range(0.8f, 1.2f);
                    state.amplitude = UnityEngine.Random.Range(2.5f, 3.5f);
                    break;
                    
                case ExpressionType.Sad:
                case ExpressionType.Disappointed:
                    // 悲伤时呼吸缓慢
                    state.speed = UnityEngine.Random.Range(0.3f, 0.4f);
                    state.amplitude = UnityEngine.Random.Range(1.0f, 1.5f);
                    break;
                    
                case ExpressionType.Angry:
                case ExpressionType.Annoyed:
                    // 生气时呼吸急促且剧烈
                    state.speed = UnityEngine.Random.Range(1.0f, 1.5f);
                    state.amplitude = UnityEngine.Random.Range(3.0f, 4.0f);
                    break;
                    
                case ExpressionType.Thoughtful:
                    // 思考时呼吸平稳
                    state.speed = UnityEngine.Random.Range(0.4f, 0.5f);
                    state.amplitude = UnityEngine.Random.Range(1.5f, 2.0f);
                    break;
                    
                default:
                    // 中性/其他情绪，恢复默认
                    state.speed = UnityEngine.Random.Range(0.4f, 0.6f);
                    state.amplitude = UnityEngine.Random.Range(1.5f, 2.5f);
                    break;
            }
        }
        
        /// <summary>
        /// ? 清除呼吸状态（用于重置或清理）
        /// </summary>
        public static void ClearBreathingState(string personaDefName)
        {
            if (breathingStates.ContainsKey(personaDefName))
            {
                breathingStates.Remove(personaDefName);
            }
        }
        
        /// <summary>
        /// ? 清除所有呼吸状态
        /// </summary>
        public static void ClearAllBreathingStates()
        {
            breathingStates.Clear();
        }
        
        /// <summary>
        /// ? v1.6.30: 感情动画参数
        /// </summary>
        public class EmotionAnimationParams
        {
            public float BlinkIntervalMin { get; }
            public float BlinkIntervalMax { get; }
            public float BreathingSpeed { get; }
            public float BreathingAmplitude { get; }
            public string DefaultMouthShape { get; }
            
            public EmotionAnimationParams(
                float blinkIntervalMin, 
                float blinkIntervalMax,
                float breathingSpeed,
                float breathingAmplitude,
                string defaultMouthShape)
            {
                BlinkIntervalMin = blinkIntervalMin;
                BlinkIntervalMax = blinkIntervalMax;
                BreathingSpeed = breathingSpeed;
                BreathingAmplitude = breathingAmplitude;
                DefaultMouthShape = defaultMouthShape;
            }
        }
        
        // ? v1.6.30: 感情驱动动画参数配置
        private static readonly Dictionary<ExpressionType, EmotionAnimationParams> emotionAnimationParams = new Dictionary<ExpressionType, EmotionAnimationParams>
        {
            // 中性：正常动画
            { ExpressionType.Neutral, new EmotionAnimationParams(
                blinkIntervalMin: 3.0f, blinkIntervalMax: 6.0f,
                breathingSpeed: 1.0f, breathingAmplitude: 1.0f,
                defaultMouthShape: "opened_mouth"
            )},
            
            // 开心：眨眼正常，呼吸轻快，微笑
            { ExpressionType.Happy, new EmotionAnimationParams(
                blinkIntervalMin: 2.5f, blinkIntervalMax: 5.0f,
                breathingSpeed: 1.2f, breathingAmplitude: 0.8f,
                defaultMouthShape: "larger_mouth"
            )},
            
            // 惊讶：眨眼频繁，呼吸加快，微张嘴
            { ExpressionType.Surprised, new EmotionAnimationParams(
                blinkIntervalMin: 1.5f, blinkIntervalMax: 3.0f,
                breathingSpeed: 1.5f, breathingAmplitude: 1.2f,
                defaultMouthShape: "larger_mouth"
            )},
            
            // 悲伤：眨眼缓慢，呼吸深沉，嘴角下垂
            { ExpressionType.Sad, new EmotionAnimationParams(
                blinkIntervalMin: 4.0f, blinkIntervalMax: 7.0f,
                breathingSpeed: 0.7f, breathingAmplitude: 1.3f,
                defaultMouthShape: "sad_mouth"
            )},
            
            // 愤怒：眨眼慢，呼吸急促，紧闭嘴巴
            { ExpressionType.Angry, new EmotionAnimationParams(
                blinkIntervalMin: 4.0f, blinkIntervalMax: 8.0f,
                breathingSpeed: 1.3f, breathingAmplitude: 1.1f,
                defaultMouthShape: "angry_mouth"
            )},
            
            // 疑惑：眨眼正常，呼吸正常，微张嘴
            { ExpressionType.Confused, new EmotionAnimationParams(
                blinkIntervalMin: 2.0f, blinkIntervalMax: 4.0f,
                breathingSpeed: 1.0f, breathingAmplitude: 1.0f,
                defaultMouthShape: "small_mouth"
            )},
            
            // 得意：眨眼慢，呼吸轻快，微笑
            { ExpressionType.Smug, new EmotionAnimationParams(
                blinkIntervalMin: 3.5f, blinkIntervalMax: 6.5f,
                breathingSpeed: 0.9f, breathingAmplitude: 0.9f,
                defaultMouthShape: "small1_mouth"
            )},
            
            // 害羞：眨眼频繁，呼吸加快，闭嘴
            { ExpressionType.Shy, new EmotionAnimationParams(
                blinkIntervalMin: 2.0f, blinkIntervalMax: 4.0f,
                breathingSpeed: 1.1f, breathingAmplitude: 0.9f,
                defaultMouthShape: "opened_mouth"
            )},
        };
        
        /// <summary>
        /// ? v1.6.30: 获取感情动画参数
        /// </summary>
        /// <param name="expression">表情类型</param>
        /// <returns>动画参数</returns>
        public static EmotionAnimationParams GetEmotionAnimationParams(ExpressionType expression)
        {
            if (emotionAnimationParams.TryGetValue(expression, out var params_))
            {
                return params_;
            }
            
            // 默认参数（中性）
            return emotionAnimationParams[ExpressionType.Neutral];
        }
        
        /// <summary>
        /// ? v1.6.30: 应用感情驱动动画（自动调整所有动画参数）
        /// </summary>
        /// <param name="personaDefName">人格 DefName</param>
        public static void ApplyEmotionDrivenAnimation(string personaDefName)
        {
            var state = GetExpressionState(personaDefName);
            var animParams = GetEmotionAnimationParams(state.CurrentExpression);
            
            // 1. 调整眨眼频率
            BlinkAnimationSystem.SetBlinkInterval(personaDefName, animParams.BlinkIntervalMin, animParams.BlinkIntervalMax);
            
            // 2. 调整呼吸动画
            AdjustBreathingByEmotion(personaDefName, state.CurrentExpression);
        }
    }
}
